using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenBudget.Api.Realtime;

namespace OpenBudget.Api.Mcp;

/// <summary>Registers agent tokens, the MCP server at <c>/mcp</c> and real-time budget change notifications.</summary>
public static class AgentAccessSetup
{
    public const string McpPath = "/mcp";
    public const string AuthorizationPolicy = "mcp-agent-token";
    public const string RateLimitPolicy = "mcp-agent-token";
    public const long MaxRequestBodyBytes = 1024 * 1024;

    public static IServiceCollection AddOpenBudgetAgentAccess(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ApiDispatcher>();
        services.AddScoped<AgentTokenService>();
        services.AddSingleton<BudgetChangePublisher>();
        services.AddSingleton<BudgetChangeInterceptor>();
        // Composes with any later AddDbContext registration (including test overrides), so notifications always follow commits.
        services.ConfigureDbContext<BudgetDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<BudgetChangeInterceptor>()));

        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AgentTokenAuthenticationHandler>(AgentTokenClaims.Scheme, null);
        services.AddAuthorization(options => options.AddPolicy(AuthorizationPolicy, policy => policy
            .AddAuthenticationSchemes(AgentTokenClaims.Scheme) // bearer only: the session cookie is never accepted on /mcp
            .RequireAuthenticatedUser()
            .RequireClaim(AgentTokenClaims.TokenId)));
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicy, context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = Math.Clamp(configuration.GetValue("Mcp:RateLimit:PermitLimit", 120), 1, 10_000);
                var windowSeconds = Math.Clamp(configuration.GetValue("Mcp:RateLimit:WindowSeconds", 60), 1, 3600);
                var key = context.User.FindFirstValue(AgentTokenClaims.TokenId) ?? "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                return RateLimitPartition.GetFixedWindowLimiter($"{key}/{permitLimit}/{windowSeconds}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0, AutoReplenishment = true
                });
            });
            var previous = options.OnRejected;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(McpPath))
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded for this agent token. Retry later." }, cancellationToken);
                    return;
                }
                if (previous is not null) await previous(context, cancellationToken);
            };
        });

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "openbudget", Title = "OpenBudget", Version = "1.0.0" };
                options.ServerInstructions = McpServerText.Instructions;
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler(McpToolHandlers.ListToolsAsync)
            .WithCallToolHandler(McpToolHandlers.CallToolAsync);
        return services;
    }

    public static WebApplication MapOpenBudgetAgentAccess(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(McpPath))
            {
                if (!context.Request.IsHttps && !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "HTTPS is required for the MCP endpoint." }, context.RequestAborted);
                    return;
                }
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit) limit.MaxRequestBodySize = MaxRequestBodyBytes;
                context.Response.Headers.CacheControl = "no-store";
            }
            await next(context);
        });
        AgentTokenService.MapEndpoints(app);
        BudgetEventsEndpoint.Map(app);
        app.MapMcp(McpPath).RequireAuthorization(AuthorizationPolicy).RequireRateLimiting(RateLimitPolicy);
        return app;
    }
}

public static class McpServerText
{
    public const string Instructions =
        "OpenBudget is an envelope budgeting app. Start with budgets_list and pass its budget id as budgetId. " +
        "Money values are decimals in the budget currency with at most two decimal places; outflows are negative. " +
        "Writes use optimistic concurrency: you may pass the version you last read, or omit it to use the current one. " +
        "A conflict error means the data changed elsewhere; re-read and retry. Pass a commandId to retry a ledger write safely. " +
        "Destructive tools require confirm=true, which you must only send after the user explicitly confirmed the specific action. " +
        "Changes appear immediately in open OpenBudget sessions.";
}

/// <summary>MCP list/call handlers: permission filtering, argument validation, budget scope, confirmation and audit.</summary>
public static class McpToolHandlers
{
    private static readonly Dictionary<AgentPermission, List<Tool>> ProtocolTools = Enum.GetValues<AgentPermission>()
        .ToDictionary(level => level, level => McpToolCatalog.For(level).Select(ToProtocol).ToList());

    public static ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        var token = AgentTokenIdentity.From(request.User) ?? throw new McpProtocolException("Agent token required.", McpErrorCode.InvalidRequest);
        return ValueTask.FromResult(new ListToolsResult { Tools = ProtocolTools[token.Permission] });
    }

    public static async ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var token = AgentTokenIdentity.From(request.User) ?? throw new McpProtocolException("Agent token required.", McpErrorCode.InvalidRequest);
        var services = request.Services ?? throw new InvalidOperationException("MCP request services are unavailable.");
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("OpenBudget.Mcp");
        var name = request.Params?.Name;
        var tool = McpToolCatalog.Find(name);
        var auditName = tool?.Name ?? SafeName(name);
        if (tool is null || !McpPermissions.CanUse(token.Permission, tool.Permissions))
        {
            // Tools outside the token's permission level are indistinguishable from unknown tools.
            await AuditAsync(services, logger, token, auditName, null, "denied", 403, cancellationToken);
            throw new McpProtocolException($"Unknown tool: '{auditName}'.", McpErrorCode.InvalidParams);
        }

        var arguments = request.Params?.Arguments is { } supplied
            ? JsonSerializer.SerializeToElement(supplied)
            : JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>());
        string? budgetId = null;
        CallToolResult result;
        string outcomeCode;
        int status;
        var validation = McpArgumentValidator.Validate(tool.InputSchema, arguments);
        if (validation is not null)
        {
            (result, outcomeCode, status) = (Error("invalid_arguments", 400, validation, null), "invalid", 400);
        }
        else
        {
            budgetId = tool.BudgetScoped ? arguments.GetProperty("budgetId").GetString() : null;
            if (budgetId is not null && !await BudgetAllowedAsync(services, token, budgetId, cancellationToken))
                (result, outcomeCode, status) = (Error("not_found", 404, "Budget not found.", null), "not_found", 404);
            else if (tool.RequiresConfirm(arguments) && !(arguments.TryGetProperty("confirm", out var confirm) && confirm.ValueKind == JsonValueKind.True))
                (result, outcomeCode, status) = (Error("confirmation_required", 400,
                    "This action is destructive. Ask the user to confirm it explicitly, then call again with confirm=true.", null), "confirm_required", 400);
            else
                (result, outcomeCode, status) = await ExecuteAsync(services, logger, tool, token, arguments, budgetId, cancellationToken);
        }
        await AuditAsync(services, logger, token, tool.Name, budgetId, outcomeCode, status, cancellationToken);
        return result;
    }

    private static async Task<(CallToolResult, string, int)> ExecuteAsync(IServiceProvider services, ILogger logger, McpToolDefinition tool,
        AgentTokenIdentity token, JsonElement arguments, string? budgetId, CancellationToken cancellationToken)
    {
        var users = services.GetRequiredService<UserManager<OpenBudgetUser>>();
        var user = await users.FindByIdAsync(token.UserId);
        if (user is null) return (Error("forbidden", 403, "Not permitted.", null), "denied", 403);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<OpenBudgetUser>>().CreateAsync(user);
        if (token.Permission != AgentPermission.Admin) principal = WithoutRoles(principal);
        var context = new McpToolContext(services.GetRequiredService<ApiDispatcher>(), principal, token, arguments, budgetId, cancellationToken);
        McpToolOutcome outcome;
        try
        {
            outcome = await tool.Handler(context);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Never log arguments or bodies: they may contain financial data or credentials.
            logger.LogError("MCP tool {Tool} failed with {ExceptionType}.", tool.Name, error.GetType().Name);
            return (Error("internal_error", 500, "The tool failed unexpectedly. No partial change was committed by OpenBudget services.", null), "error", 500);
        }

        var response = outcome.Response;
        if (response.IsSuccess)
        {
            var payload = outcome.Result ?? response.Body;
            var structured = payload switch
            {
                { ValueKind: JsonValueKind.Object } value => value,
                { ValueKind: JsonValueKind.Array } value => JsonSerializer.SerializeToElement(new { items = value }),
                { } value => JsonSerializer.SerializeToElement(new { value }),
                null => JsonSerializer.SerializeToElement(new { status = "ok" })
            };
            return (new CallToolResult
            {
                Content = [new TextContentBlock { Text = outcome.Summary }, new TextContentBlock { Text = structured.GetRawText() }],
                StructuredContent = structured,
                IsError = false
            }, "ok", response.StatusCode);
        }

        var (code, outcomeCode) = response.StatusCode switch
        {
            400 or 422 => ("invalid_request", "invalid"),
            401 or 403 => ("forbidden", "denied"),
            404 => ("not_found", "not_found"),
            409 => ("conflict", "conflict"),
            429 => ("rate_limited", "denied"),
            503 => ("unavailable", "error"),
            _ => ("error", "error")
        };
        var message = response.StatusCode switch
        {
            401 or 403 => "Not permitted.",
            404 => response.Error ?? "Not found.",
            409 when response.Error is null || response.Error.Contains("changed", StringComparison.OrdinalIgnoreCase) || response.Body?.TryGetProperty("current", out _) == true =>
                (response.Error ?? "The data changed in another session.") + " Re-read the latest data (included as 'current' when available) and retry.",
            _ => response.Error ?? $"The request failed ({response.StatusCode})."
        };
        JsonElement? current = response.Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("current", out var latest) ? latest : null;
        return (Error(code, response.StatusCode, message, current), outcomeCode, response.StatusCode);
    }

    private static async Task<bool> BudgetAllowedAsync(IServiceProvider services, AgentTokenIdentity token, string budgetId, CancellationToken cancellationToken)
    {
        if (!McpBudgetIds.IsValid(budgetId) || !McpPermissions.BudgetInTokenScope(token.Permission, token.BudgetIds, budgetId)) return false;
        // The creator must still own or edit the (active) budget right now.
        return await services.GetRequiredService<BudgetSharingService>().CanAccessBudgetAsync(token.UserId, budgetId, cancellationToken);
    }

    private static CallToolResult Error(string code, int status, string message, JsonElement? current)
    {
        var error = new JsonObject { ["code"] = code, ["status"] = status, ["message"] = message };
        if (current is { } value) error["current"] = JsonNode.Parse(value.GetRawText());
        var structured = JsonSerializer.SerializeToElement(new JsonObject { ["error"] = error });
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Error ({code}): {message}" }, new TextContentBlock { Text = structured.GetRawText() }],
            StructuredContent = structured,
            IsError = true
        };
    }

    private static ClaimsPrincipal WithoutRoles(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var claims = identity.Claims.Where(x => x.Type != identity.RoleClaimType && x.Type != ClaimTypes.Role);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));
    }

    private static async Task AuditAsync(IServiceProvider services, ILogger logger, AgentTokenIdentity token, string tool, string? budgetId, string outcome, int status, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            db.Add(new AgentAuditLogEntity
            {
                TokenId = token.TokenId, UserId = token.UserId, Tool = tool, BudgetId = budgetId is not null && McpBudgetIds.IsValid(budgetId) ? budgetId : null,
                Success = outcome == "ok", Outcome = outcome, StatusCode = status, CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow()
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Agent audit write failed for tool {Tool} with {ExceptionType}.", tool, error.GetType().Name);
        }
    }

    private static string SafeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        var safe = new string(name.Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_').Take(AgentAccessModel.ToolNameLength).ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }

    private static Tool ToProtocol(McpToolDefinition definition) => new()
    {
        Name = definition.Name,
        Title = definition.Title,
        Description = definition.Description,
        InputSchema = JsonSerializer.SerializeToElement(definition.InputSchema),
        Annotations = new ToolAnnotations
        {
            Title = definition.Title,
            ReadOnlyHint = definition.ReadOnly,
            DestructiveHint = definition.Destructive,
            IdempotentHint = definition.Idempotent,
            OpenWorldHint = false
        },
        Meta = new JsonObject
        {
            ["openbudget/permissions"] = new JsonArray(definition.Permissions.OrderBy(x => x).Select(x => (JsonNode?)x.ToString()).ToArray()),
            ["openbudget/budgetScoped"] = definition.BudgetScoped
        }
    };
}
