using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace OpenBudget.Api;

public sealed record OwnedBudgetDto(string Id, string Name, string Currency, long Version, bool IsArchived, DateTimeOffset CreatedAt, string AccessRole = "owner");
public sealed record CreateOwnedBudgetRequest(string Name, string Currency = "EUR");
public sealed record RenameOwnedBudgetRequest(string Name, long Version);
public sealed record DeleteOwnedBudgetRequest(long Version);
public sealed record BudgetLimitDto(string Membership, string MembershipName, int? MaxActiveBudgets, int ActiveBudgets, int? BudgetLimitOverride);

public sealed class BudgetOwnershipService(BudgetDbContext db, BudgetRequestContext budgetContext, BudgetSharingService sharing)
{
    public Task<bool> OwnsBudgetAsync(string userId, string budgetId, CancellationToken cancellationToken) =>
        db.BudgetPlans.AnyAsync(x => x.Id == budgetId && x.OwnerUserId == userId && !x.IsArchived, cancellationToken);

    public Task<bool> CanAccessBudgetAsync(string userId, string budgetId, CancellationToken cancellationToken) =>
        sharing.CanAccessBudgetAsync(userId, budgetId, cancellationToken);

    public async Task<IResult> ListAsync(string userId, bool includeArchived, CancellationToken cancellationToken)
    {
        await sharing.AcceptPendingInvitationsAsync(userId, cancellationToken);
        var budgets = await db.BudgetPlans.AsNoTracking()
            .Where(x => (x.OwnerUserId == userId || db.BudgetMembers.Any(member => member.BudgetId == x.Id && member.UserId == userId)) && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.CreatedAt).Select(x => new OwnedBudgetDto(x.Id, x.Name, x.Currency, x.Version, x.IsArchived, x.CreatedAt, x.OwnerUserId == userId ? "owner" : "editor"))
            .ToListAsync(cancellationToken);
        var membership = await GetLimitAsync(userId, cancellationToken);
        return Results.Ok(new { budgets, limit = membership });
    }

    public async Task<IResult> CreateAsync(string userId, CreateOwnedBudgetRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return Results.BadRequest(new { error = "Budget name is required and must be 120 characters or fewer." });
        if (currency is null || currency.Length != 3 || currency.Any(x => x is < 'A' or > 'Z'))
            return Results.BadRequest(new { error = "Currency must be a three-letter code." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({userId}, 0))", cancellationToken);
        var limit = await GetEffectiveLimitAsync(userId, cancellationToken);
        var activeCount = await db.BudgetPlans.CountAsync(x => x.OwnerUserId == userId && !x.IsArchived, cancellationToken);
        if (limit is not null && activeCount >= limit.Value)
            return Results.Conflict(new { error = "Your active budget limit has been reached. Archive a budget or ask an administrator to change your membership." });

        var plan = new BudgetPlanEntity
        {
            Id = Guid.NewGuid().ToString("N"), OwnerUserId = userId, Name = name, Currency = currency,
            CreatedAt = DateTimeOffset.UtcNow, Version = 1
        };
        db.BudgetPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/budgets/{plan.Id}", ToDto(plan));
    }

    public async Task<IResult> RenameAsync(string userId, string budgetId, RenameOwnedBudgetRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || request.Version < 1)
            return Results.BadRequest(new { error = "Budget name and version are invalid." });
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(x => x.Id == budgetId && x.OwnerUserId == userId, cancellationToken);
        if (plan is null) return Results.NotFound();
        if (plan.Version != request.Version) return Results.Conflict(new { error = "This budget changed in another session." });
        plan.Name = name;
        plan.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(plan));
    }

    public async Task<IResult> SetArchivedAsync(string userId, string budgetId, bool archived, CancellationToken cancellationToken)
    {
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(x => x.Id == budgetId && x.OwnerUserId == userId, cancellationToken);
        if (plan is null) return Results.NotFound();
        if (plan.Id == "default") return Results.Conflict(new { error = "The legacy default budget cannot be archived." });
        if (plan.IsArchived == archived) return Results.NoContent();
        if (!archived)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({userId}, 0))", cancellationToken);
            var limit = await GetEffectiveLimitAsync(userId, cancellationToken);
            var activeCount = await db.BudgetPlans.CountAsync(x => x.OwnerUserId == userId && !x.IsArchived, cancellationToken);
            if (limit is not null && activeCount >= limit.Value)
                return Results.Conflict(new { error = "Your active budget limit has been reached. Archive a budget or ask an administrator to change your membership." });
            plan.IsArchived = false;
            plan.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        plan.IsArchived = true;
        plan.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteAsync(string userId, string budgetId, DeleteOwnedBudgetRequest request, CancellationToken cancellationToken)
    {
        if (request.Version < 1) return Results.BadRequest(new { error = "Budget version is invalid." });
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(x => x.Id == budgetId && x.OwnerUserId == userId, cancellationToken);
        if (plan is null) return Results.NotFound();
        if (plan.Id == "default") return Results.Conflict(new { error = "The legacy default budget cannot be deleted." });
        if (plan.Version != request.Version) return Results.Conflict(new { error = "This budget changed in another session." });

        budgetContext.BudgetId = budgetId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Transactions.RemoveRange(await db.Transactions.ToListAsync(cancellationToken));
        db.ScheduleOccurrences.RemoveRange(await db.ScheduleOccurrences.ToListAsync(cancellationToken));
        db.Schedules.RemoveRange(await db.Schedules.ToListAsync(cancellationToken));
        db.Reconciliations.RemoveRange(await db.Reconciliations.ToListAsync(cancellationToken));
        db.LedgerCommands.RemoveRange(await db.LedgerCommands.ToListAsync(cancellationToken));
        db.RegisterViews.RemoveRange(await db.RegisterViews.ToListAsync(cancellationToken));
        db.FlagDefinitions.RemoveRange(await db.FlagDefinitions.ToListAsync(cancellationToken));
        db.LongTermPlanningCategories.RemoveRange(await db.LongTermPlanningCategories.ToListAsync(cancellationToken));
        db.LongTermPlanningIncomes.RemoveRange(await db.LongTermPlanningIncomes.ToListAsync(cancellationToken));
        db.LongTermPlanningStates.RemoveRange(await db.LongTermPlanningStates.ToListAsync(cancellationToken));
        db.LedgerStates.RemoveRange(await db.LedgerStates.ToListAsync(cancellationToken));
        db.Accounts.RemoveRange(await db.Accounts.ToListAsync(cancellationToken));
        db.Payees.RemoveRange(await db.Payees.ToListAsync(cancellationToken));
        var months = await db.Budgets.Where(x => x.BudgetId == budgetId)
            .Include(x => x.Groups).ThenInclude(x => x.Categories).ToListAsync(cancellationToken);
        db.Budgets.RemoveRange(months);
        db.BudgetPlans.Remove(plan);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "This budget changed in another session." });
        }
    }

    private async Task<BudgetLimitDto> GetLimitAsync(string userId, CancellationToken cancellationToken)
    {
        var membership = await db.UserMemberships.AsNoTracking().Where(x => x.UserId == userId)
            .Join(db.MembershipPlans.AsNoTracking(), x => x.MembershipPlanId, x => x.Id,
                (userMembership, plan) => new { plan.Id, plan.Name, plan.MaxActiveBudgets, userMembership.BudgetLimitOverride })
            .SingleOrDefaultAsync(cancellationToken);
        var active = await db.BudgetPlans.CountAsync(x => x.OwnerUserId == userId && !x.IsArchived, cancellationToken);
        return membership is null
            ? new BudgetLimitDto("free", "Free", 1, active, null)
            : new BudgetLimitDto(membership.Id, membership.Name, membership.BudgetLimitOverride ?? membership.MaxActiveBudgets, active, membership.BudgetLimitOverride);
    }

    private async Task<int?> GetEffectiveLimitAsync(string userId, CancellationToken cancellationToken)
    {
        var membership = await db.UserMemberships.AsNoTracking().Where(x => x.UserId == userId)
            .Join(db.MembershipPlans.AsNoTracking(), x => x.MembershipPlanId, x => x.Id,
                (userMembership, plan) => new { plan.MaxActiveBudgets, userMembership.BudgetLimitOverride })
            .SingleOrDefaultAsync(cancellationToken);
        return membership is null ? 1 : membership.BudgetLimitOverride ?? membership.MaxActiveBudgets;
    }

    private static OwnedBudgetDto ToDto(BudgetPlanEntity plan) => new(plan.Id, plan.Name, plan.Currency, plan.Version, plan.IsArchived, plan.CreatedAt);

    public static void MapEndpoints(WebApplication app)
    {
        var budgets = app.MapGroup("/api/budgets").RequireAuthorization();
        budgets.MapGet("", async (ClaimsPrincipal principal, bool? includeArchived, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.ListAsync(userId, includeArchived ?? false, ct);
        });
        budgets.MapPost("", async (ClaimsPrincipal principal, CreateOwnedBudgetRequest request, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.CreateAsync(userId, request, ct);
        });
        budgets.MapPut("/{id}", async (string id, ClaimsPrincipal principal, RenameOwnedBudgetRequest request, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.RenameAsync(userId, id, request, ct);
        });
        budgets.MapPost("/{id}/archive", async (string id, ClaimsPrincipal principal, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.SetArchivedAsync(userId, id, true, ct);
        });
        budgets.MapPost("/{id}/restore", async (string id, ClaimsPrincipal principal, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.SetArchivedAsync(userId, id, false, ct);
        });
        budgets.MapDelete("/{id}", async (string id, [FromBody] DeleteOwnedBudgetRequest request, ClaimsPrincipal principal, BudgetOwnershipService service, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? (IResult)Results.Unauthorized() : await service.DeleteAsync(userId, id, request, ct);
        });
        budgets.MapGet("/{id}/months/{month}", async (string id, string month, ClaimsPrincipal principal, BudgetOwnershipService ownership, BudgetRequestContext budgetContext, BudgetService service, LedgerGate gate, CancellationToken ct) =>
        {
            if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            if (!await ownership.CanAccessBudgetAsync(userId, id, ct)) return Results.NotFound();
            budgetContext.BudgetId = id;
            return await gate.RunAsync(async () => Results.Ok(BudgetDto.From(await service.GetMonthAsync(id, month, ct))), ct);
        });
        budgets.MapPut("/{id}/months/{month}", async (string id, string month, BudgetUpdateDto request, ClaimsPrincipal principal, BudgetOwnershipService ownership, BudgetRequestContext budgetContext, BudgetService service, LedgerGate gate, CancellationToken ct) =>
        {
            if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
            if (request.Month != month) return Results.BadRequest(new { error = "Route month must match payload month." });
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            if (!await ownership.CanAccessBudgetAsync(userId, id, ct)) return Results.NotFound();
            budgetContext.BudgetId = id;
            var result = await gate.RunAsync(() => service.UpdateMonthAsync(id, request, userId, ct), ct, value => value.Error is null && !value.IsConflict);
            if (result.IsConflict) return Results.Conflict(new { error = "This month changed in another session.", current = result.Budget });
            if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Budget);
        });
    }

    private static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$") && DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out _);
}
