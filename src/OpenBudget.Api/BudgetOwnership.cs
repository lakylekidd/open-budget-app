using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record OwnedBudgetDto(string Id, string Name, string Currency, long Version, bool IsArchived, DateTimeOffset CreatedAt);
public sealed record CreateOwnedBudgetRequest(string Name, string Currency = "EUR");
public sealed record RenameOwnedBudgetRequest(string Name, long Version);
public sealed record BudgetLimitDto(string Membership, string MembershipName, int? MaxActiveBudgets, int ActiveBudgets, int? BudgetLimitOverride);

public sealed class BudgetOwnershipService(BudgetDbContext db)
{
    public async Task<IResult> ListAsync(string userId, bool includeArchived, CancellationToken cancellationToken)
    {
        var budgets = await db.BudgetPlans.AsNoTracking()
            .Where(x => x.OwnerUserId == userId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.CreatedAt).Select(x => new OwnedBudgetDto(x.Id, x.Name, x.Currency, x.Version, x.IsArchived, x.CreatedAt))
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
    }
}
