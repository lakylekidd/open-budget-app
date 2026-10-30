using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api.Mcp;

public sealed record ApiResponse(int StatusCode, JsonElement? Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
    public string? Error => Body is { ValueKind: JsonValueKind.Object } body
        ? body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString()
        : body.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString()
        : body.TryGetProperty("errors", out var errors) ? errors.ToString() : null
        : null;
}

/// <summary>
/// Executes an existing OpenBudget minimal-API endpoint in-process on behalf of an agent token's creator, so MCP tools
/// use exactly the same request validation, application services, LedgerGate locking, optimistic versions, command
/// receipts and audit paths as the web app. Each call runs in a fresh DI scope with the budget partition selected
/// explicitly (instead of the browser's active-budget cookie). The endpoint's own authorization metadata (for example the
/// Administrator role) is evaluated against the creator's principal before the handler runs.
/// </summary>
public sealed class ApiDispatcher(EndpointDataSource endpoints, IServiceScopeFactory scopes, IHttpContextAccessor accessor)
{
    private readonly ConcurrentDictionary<(string Method, string Template), RouteEndpoint> cache = new();

    public async Task<ApiResponse> SendAsync(ClaimsPrincipal user, string? budgetId, string method, string template,
        IReadOnlyDictionary<string, string>? route = null, IReadOnlyDictionary<string, string?>? query = null, object? body = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Find(method, template);
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (budgetId is not null) services.GetRequiredService<BudgetRequestContext>().BudgetId = budgetId;

        var context = new DefaultHttpContext { RequestServices = services, User = user, RequestAborted = cancellationToken };
        var origin = accessor.HttpContext?.Request;
        context.Request.Scheme = origin?.Scheme ?? "https";
        context.Request.Host = origin?.Host ?? new HostString("localhost");
        context.Request.Method = method;
        var path = template;
        foreach (var (name, value) in route ?? new Dictionary<string, string>())
        {
            context.Request.RouteValues[name] = value;
            path = System.Text.RegularExpressions.Regex.Replace(path, "\\{" + name + "(:[^}]*)?\\}", Uri.EscapeDataString(value));
        }
        context.Request.Path = path;
        if (query is not null)
            context.Request.QueryString = QueryString.Create(query.Where(x => x.Value is not null).Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
        if (body is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(true));
        }
        else context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(false));
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        context.SetEndpoint(endpoint);

        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider,
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>())
            ?? await policyProvider.GetFallbackPolicyAsync();
        if (policy is not null && !(await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, context, policy)).Succeeded)
            return new ApiResponse(StatusCodes.Status403Forbidden, Json(new { error = "Not permitted." }));

        try
        {
            await endpoint.RequestDelegate!(context);
        }
        catch (LedgerException error)
        {
            return new ApiResponse(error.StatusCode, Json(new { error = error.Message }));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ApiResponse(StatusCodes.Status409Conflict, Json(new { error = "Data changed in another session. Reload and retry." }));
        }
        catch (BadHttpRequestException error)
        {
            return new ApiResponse(error.StatusCode, Json(new { error = "The request was rejected by input validation." }));
        }

        JsonElement? parsed = null;
        if (responseBody.Length > 0)
        {
            try { parsed = JsonDocument.Parse(responseBody.ToArray()).RootElement.Clone(); }
            catch (JsonException) { parsed = null; }
        }
        return new ApiResponse(context.Response.StatusCode, parsed);
    }

    private RouteEndpoint Find(string method, string template) => cache.GetOrAdd((method, Normalize(template)), key =>
        endpoints.Endpoints.OfType<RouteEndpoint>().SingleOrDefault(endpoint =>
            Normalize(endpoint.RoutePattern.RawText) == key.Template &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(key.Method, StringComparer.OrdinalIgnoreCase) == true)
        ?? throw new InvalidOperationException($"No endpoint is mapped for {key.Method} {key.Template}."));

    private static string Normalize(string? template) =>
        "/" + System.Text.RegularExpressions.Regex.Replace((template ?? "").Trim('/'), @"\{([A-Za-z0-9_]+)[^}]*\}", "{$1}");

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, JsonSerializerOptions.Web);

    private sealed class BodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
