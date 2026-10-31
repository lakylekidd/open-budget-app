using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

/// <summary>Reorders every account in the active budget. <c>AccountIds</c> lists each account exactly once, first to last.</summary>
public sealed record ReorderAccountsRequest(Guid CommandId, long Version, List<Guid>? AccountIds);

// Account order is one persisted, budget-wide sequence (Accounts.SortOrder). The UI shows it within each sidebar section.
public static class AccountOrderRules
{
    public const string InvalidOrder = "Account order must list every account in this budget exactly once.";

    /// <summary>Returns an error when <paramref name="submitted"/> is not a permutation of <paramref name="existing"/>.</summary>
    public static string? Validate(IReadOnlyCollection<Guid> existing, IReadOnlyList<Guid>? submitted)
    {
        if (submitted is null || submitted.Count != existing.Count) return InvalidOrder;
        var unique = submitted.ToHashSet();
        return unique.Count == submitted.Count && unique.SetEquals(existing) ? null : InvalidOrder;
    }

    /// <summary>A new account appends after every existing account.</summary>
    public static int NextSortOrder(IEnumerable<int> existing) => existing.DefaultIfEmpty(-1).Max() + 1;
}

public sealed partial class LedgerService
{
    public Task<MutationDto> ReorderAccountsAsync(ReorderAccountsRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, "account.order", request, async () =>
        {
            var accounts = await db.Accounts.OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(ct);
            if (AccountOrderRules.Validate(accounts.Select(a => a.Id).ToList(), request.AccountIds) is { } error) throw new LedgerException(error);
            var before = accounts.Select(a => a.Id).ToList();
            var byId = accounts.ToDictionary(a => a.Id);
            for (var index = 0; index < request.AccountIds!.Count; index++) byId[request.AccountIds[index]].SortOrder = index;
            return new Change(Guid.Empty, before, request.AccountIds);
        }, ct);
}
