using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenBudget.Api.Mcp;

/// <summary>
/// One MCP tool. <see cref="Permissions"/> is the server-side permission matrix entry; <see cref="BudgetScoped"/> tools
/// take a required <c>budgetId</c> argument that must be in the token scope and accessible to the creator.
/// </summary>
public sealed record McpToolDefinition(
    string Name,
    string Title,
    string Description,
    IReadOnlySet<AgentPermission> Permissions,
    bool BudgetScoped,
    bool ReadOnly,
    bool Destructive,
    JsonObject InputSchema,
    Func<McpToolContext, Task<McpToolOutcome>> Handler,
    bool Idempotent = false,
    Func<JsonElement, bool>? NeedsConfirmation = null)
{
    /// <summary>True when this call must carry <c>confirm: true</c>.</summary>
    public bool RequiresConfirm(JsonElement arguments) => NeedsConfirmation?.Invoke(arguments) ?? Destructive;
}

public sealed record McpToolOutcome(ApiResponse Response, string Summary, JsonElement? Result = null)
{
    public static McpToolOutcome Invalid(string message) =>
        new(new ApiResponse(400, JsonSerializer.SerializeToElement(new { error = message }, JsonSerializerOptions.Web)), message);
    public static McpToolOutcome NotFound(string message) =>
        new(new ApiResponse(404, JsonSerializer.SerializeToElement(new { error = message }, JsonSerializerOptions.Web)), message);
}

/// <summary>Per-call state passed to tool handlers: validated arguments, the creator principal and the dispatcher.</summary>
public sealed class McpToolContext(ApiDispatcher api, ClaimsPrincipal creator, AgentTokenIdentity token, JsonElement arguments, string? budgetId, CancellationToken cancellationToken)
{
    public AgentTokenIdentity Token { get; } = token;
    public JsonElement Arguments { get; } = arguments;
    public string? BudgetId { get; } = budgetId;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public bool Has(string name) => Arguments.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null;
    public string Str(string name) => Arguments.GetProperty(name).GetString()!;
    public string? OptStr(string name) => Has(name) ? Arguments.GetProperty(name).GetString() : null;
    public int? OptInt(string name) => Has(name) ? Arguments.GetProperty(name).GetInt32() : null;
    public long? OptLong(string name) => Has(name) ? Arguments.GetProperty(name).GetInt64() : null;
    public decimal Money(string name) => Arguments.GetProperty(name).GetDecimal();
    public decimal? OptMoney(string name) => Has(name) ? Arguments.GetProperty(name).GetDecimal() : null;
    public bool? OptBool(string name) => Has(name) ? Arguments.GetProperty(name).GetBoolean() : null;
    public bool Bool(string name, bool fallback = false) => OptBool(name) ?? fallback;
    public Guid Guid(string name) => System.Guid.Parse(Str(name));
    public Guid? OptGuid(string name) => Has(name) ? System.Guid.Parse(Str(name)) : null;
    public DateOnly Date(string name) => DateOnly.ParseExact(Str(name), "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public DateOnly? OptDate(string name) => Has(name) ? Date(name) : null;
    public JsonElement? Element(string name) => Has(name) ? Arguments.GetProperty(name) : null;
    /// <summary>Caller-supplied command ID enables safe retries (exact replay); otherwise a new one is generated.</summary>
    public Guid CommandId() => OptGuid("commandId") ?? System.Guid.NewGuid();

    public Task<ApiResponse> Get(string template, IReadOnlyDictionary<string, string>? route = null, IReadOnlyDictionary<string, string?>? query = null) =>
        api.SendAsync(creator, BudgetId, "GET", template, route, query, null, CancellationToken);

    public Task<ApiResponse> Send(string method, string template, object? body, IReadOnlyDictionary<string, string>? route = null) =>
        api.SendAsync(creator, BudgetId, method, template, route, null, body, CancellationToken);

    /// <summary>Uses the caller's expected ledger version when supplied (so stale writes surface as 409), else the current one.</summary>
    public async Task<long?> LedgerVersionAsync()
    {
        if (OptLong("version") is { } version) return version;
        var accounts = await Get("/api/accounts");
        return accounts.IsSuccess && accounts.Body is { } body && body.TryGetProperty("version", out var current) ? current.GetInt64() : null;
    }
}

public static class Route
{
    public static IReadOnlyDictionary<string, string> Of(params (string Key, string Value)[] values) => values.ToDictionary(x => x.Key, x => x.Value);
}
