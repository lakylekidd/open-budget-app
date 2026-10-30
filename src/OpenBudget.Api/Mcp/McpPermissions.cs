namespace OpenBudget.Api.Mcp;

/// <summary>
/// Server-side permission matrix for MCP tools. Each tool declares the token levels allowed to list and call it;
/// budget-scoped tools additionally require the budget to be in the token scope (Budget/Reporting) and accessible
/// to the token creator at call time (all levels).
/// </summary>
public static class McpPermissions
{
    public static readonly IReadOnlySet<AgentPermission> All = new HashSet<AgentPermission> { AgentPermission.Reporting, AgentPermission.Budget, AgentPermission.Admin };
    public static readonly IReadOnlySet<AgentPermission> Read = new HashSet<AgentPermission> { AgentPermission.Reporting, AgentPermission.Admin };
    public static readonly IReadOnlySet<AgentPermission> PlanWrite = new HashSet<AgentPermission> { AgentPermission.Budget, AgentPermission.Admin };
    public static readonly IReadOnlySet<AgentPermission> AdminOnly = new HashSet<AgentPermission> { AgentPermission.Admin };

    public static bool CanUse(AgentPermission level, IReadOnlySet<AgentPermission> allowed) => allowed.Contains(level);

    /// <summary>
    /// True when the token's own scope covers the budget. Admin tokens cover every budget the creator can access;
    /// creator access is checked separately against live data for every call.
    /// </summary>
    public static bool BudgetInTokenScope(AgentPermission level, IReadOnlyCollection<string> tokenBudgetIds, string budgetId) =>
        level == AgentPermission.Admin || tokenBudgetIds.Contains(budgetId, StringComparer.Ordinal);

    public static string Describe(IReadOnlySet<AgentPermission> allowed) =>
        allowed.Count == 3 ? "Reporting, Budget or Admin" : string.Join(" or ", allowed.OrderBy(x => x).Select(x => x.ToString()));

    public static bool TryParse(string? value, out AgentPermission permission)
    {
        permission = default;
        return value is "Admin" or "Budget" or "Reporting" && Enum.TryParse(value, out permission);
    }
}
