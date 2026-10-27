using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record BudgetPersonDto(string Id, string Name, string? Email, string Role, DateTimeOffset JoinedAt);
public sealed record BudgetInvitationDto(Guid Id, string Email, DateTimeOffset CreatedAt);
public sealed record BudgetSharingDto(BudgetPersonDto Manager, IReadOnlyList<BudgetPersonDto> Members, IReadOnlyList<BudgetInvitationDto> Invitations, bool CanManage, int InvitesRemaining);
public sealed record InviteBudgetMemberRequest(string Email);
public sealed record PlanActionDto(Guid Id, string Month, string Kind, decimal? Amount, string? FromCategoryName, string? ToCategoryName,
    string ActorUserId, string ActorName, string? ActorEmail, DateTimeOffset CreatedAt);
public sealed record PlanActionDraft(string Kind, decimal? Amount, string? FromCategoryName, string? ToCategoryName);

public static class PlanActionRules
{
    public static IReadOnlyList<PlanActionDraft> Describe(IReadOnlyDictionary<string, (string Name, decimal Assigned)> previous, IEnumerable<CategoryEntity> current)
    {
        var changes = current.Where(category => previous.TryGetValue(category.Id, out var old) && old.Assigned != category.Assigned)
            .Select(category => new { Category = category, Delta = category.Assigned - previous[category.Id].Assigned }).ToList();
        if (changes.Count == 2 && changes.Sum(change => change.Delta) == 0 && changes.Any(change => change.Delta < 0) && changes.Any(change => change.Delta > 0))
        {
            var source = changes.Single(change => change.Delta < 0);
            var target = changes.Single(change => change.Delta > 0);
            return [new PlanActionDraft("moved", target.Delta, source.Category.Name, target.Category.Name)];
        }
        if (changes.Count == 0) return [new PlanActionDraft("updated", null, null, null)];
        return changes.Select(change => new PlanActionDraft(change.Delta > 0 ? "assigned" : "removed", Math.Abs(change.Delta),
            change.Delta < 0 ? change.Category.Name : null, change.Delta > 0 ? change.Category.Name : null)).ToList();
    }
}

public sealed class BudgetSharingService(BudgetDbContext db, IAccountEmailSender email, TimeProvider clock)
{
    public const int CollaboratorLimit = 4;

    public Task<bool> CanAccessBudgetAsync(string userId, string budgetId, CancellationToken cancellationToken) =>
        db.BudgetPlans.AnyAsync(plan => plan.Id == budgetId && !plan.IsArchived &&
            (plan.OwnerUserId == userId || db.BudgetMembers.Any(member => member.BudgetId == budgetId && member.UserId == userId)), cancellationToken);

    public async Task AcceptPendingInvitationsAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.NormalizedEmail)) return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({userId}, 0))", cancellationToken);
        var invitations = await db.BudgetInvitations
            .Where(item => item.NormalizedEmail == user.NormalizedEmail)
            .ToListAsync(cancellationToken);
        if (invitations.Count == 0) return;

        var accessibleBudgetIds = await db.BudgetPlans.Where(plan => !plan.IsArchived && invitations.Select(item => item.BudgetId).Contains(plan.Id) && plan.OwnerUserId != userId)
            .Select(plan => plan.Id).ToListAsync(cancellationToken);
        var existing = await db.BudgetMembers.Where(member => member.UserId == userId && accessibleBudgetIds.Contains(member.BudgetId))
            .Select(member => member.BudgetId).ToListAsync(cancellationToken);
        foreach (var invitation in invitations.Where(item => accessibleBudgetIds.Contains(item.BudgetId)))
        {
            if (!existing.Contains(invitation.BudgetId))
                db.BudgetMembers.Add(new BudgetMemberEntity { BudgetId = invitation.BudgetId, UserId = userId, InvitedByUserId = invitation.InvitedByUserId, JoinedAt = clock.GetUtcNow() });
            db.BudgetInvitations.Remove(invitation);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IResult> GetAsync(string budgetId, string userId, CancellationToken cancellationToken)
    {
        if (!await CanAccessBudgetAsync(userId, budgetId, cancellationToken)) return Results.NotFound();
        var plan = await db.BudgetPlans.AsNoTracking().SingleAsync(item => item.Id == budgetId, cancellationToken);
        var owner = await db.Users.AsNoTracking().SingleAsync(item => item.Id == plan.OwnerUserId, cancellationToken);
        var members = await (from member in db.BudgetMembers.AsNoTracking()
            join user in db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.BudgetId == budgetId
            orderby member.JoinedAt
            select new BudgetPersonDto(user.Id, user.UserName ?? user.Email ?? "Member", user.Email, member.Role, member.JoinedAt)).ToListAsync(cancellationToken);
        var canManage = plan.OwnerUserId == userId;
        var invitations = canManage
            ? await db.BudgetInvitations.AsNoTracking().Where(item => item.BudgetId == budgetId).OrderBy(item => item.CreatedAt)
                .Select(item => new BudgetInvitationDto(item.Id, item.Email, item.CreatedAt)).ToListAsync(cancellationToken)
            : [];
        var manager = new BudgetPersonDto(owner.Id, owner.UserName ?? owner.Email ?? "Budget manager", owner.Email, "manager", plan.CreatedAt);
        return Results.Ok(new BudgetSharingDto(manager, members, invitations, canManage, Math.Max(0, CollaboratorLimit - members.Count - invitations.Count)));
    }

    public async Task<IResult> InviteAsync(string budgetId, string actorUserId, InviteBudgetMemberRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var address = request.Email?.Trim() ?? "";
        if (!AccountEmailSettingsService.IsEmailAddress(address)) return Results.BadRequest(new { error = "Enter a valid email address." });
        var normalized = address.ToUpperInvariant();
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(item => item.Id == budgetId && item.OwnerUserId == actorUserId && !item.IsArchived, cancellationToken);
        if (plan is null) return Results.NotFound();
        var owner = await db.Users.AsNoTracking().SingleAsync(item => item.Id == actorUserId, cancellationToken);
        if (owner.NormalizedEmail == normalized) return Results.Conflict(new { error = "You already manage this budget." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({budgetId}, 0))", cancellationToken);
        var memberCount = await db.BudgetMembers.CountAsync(item => item.BudgetId == budgetId, cancellationToken);
        var invitationCount = await db.BudgetInvitations.CountAsync(item => item.BudgetId == budgetId, cancellationToken);
        if (memberCount + invitationCount >= CollaboratorLimit) return Results.Conflict(new { error = "This budget already has the maximum number of members and pending invitations." });

        var invitedUser = await db.Users.SingleOrDefaultAsync(item => item.NormalizedEmail == normalized && item.EmailConfirmed, cancellationToken);
        if (invitedUser is not null && await db.BudgetMembers.AnyAsync(item => item.BudgetId == budgetId && item.UserId == invitedUser.Id, cancellationToken))
            return Results.Conflict(new { error = "That person already has access to this budget." });
        if (await db.BudgetInvitations.AnyAsync(item => item.BudgetId == budgetId && item.NormalizedEmail == normalized, cancellationToken))
            return Results.Conflict(new { error = "An invitation is already pending for that email address." });

        if (invitedUser is not null)
            db.BudgetMembers.Add(new BudgetMemberEntity { BudgetId = budgetId, UserId = invitedUser.Id, InvitedByUserId = actorUserId, JoinedAt = clock.GetUtcNow() });
        else
            db.BudgetInvitations.Add(new BudgetInvitationEntity { BudgetId = budgetId, Email = address, NormalizedEmail = normalized, InvitedByUserId = actorUserId, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (await email.IsConfiguredAsync(cancellationToken))
        {
            try
            {
                var link = $"{context.Request.Scheme}://{context.Request.Host}/?page={Uri.EscapeDataString("Budget Together")}";
                await email.SendAsync(address, $"Join {plan.Name} in OpenBudget", AccountEmailTemplates.BudgetInvitation(plan.Name, owner.UserName ?? owner.Email ?? "Your budget manager", link), cancellationToken);
            }
            catch when (!cancellationToken.IsCancellationRequested) { /* Access is persisted; the manager can retry the notification separately later. */ }
        }
        return Results.Accepted(value: new { message = invitedUser is null ? "Invitation saved. It will be accepted when this email signs in." : "Member added to the budget." });
    }

    public async Task<IResult> RemoveMemberAsync(string budgetId, string actorUserId, string memberUserId, CancellationToken cancellationToken)
    {
        if (!await db.BudgetPlans.AnyAsync(item => item.Id == budgetId && item.OwnerUserId == actorUserId && !item.IsArchived, cancellationToken)) return Results.NotFound();
        var member = await db.BudgetMembers.SingleOrDefaultAsync(item => item.BudgetId == budgetId && item.UserId == memberUserId, cancellationToken);
        if (member is null) return Results.NotFound();
        db.BudgetMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public async Task<IResult> CancelInvitationAsync(string budgetId, string actorUserId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (!await db.BudgetPlans.AnyAsync(item => item.Id == budgetId && item.OwnerUserId == actorUserId && !item.IsArchived, cancellationToken)) return Results.NotFound();
        var invitation = await db.BudgetInvitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.BudgetId == budgetId, cancellationToken);
        if (invitation is null) return Results.NotFound();
        db.BudgetInvitations.Remove(invitation);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public async Task<IResult> GetPlanActionsAsync(string budgetId, string userId, string? kind, int limit, CancellationToken cancellationToken)
    {
        if (!await CanAccessBudgetAsync(userId, budgetId, cancellationToken)) return Results.NotFound();
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        var query = db.PlanActions.AsNoTracking().Where(item => item.BudgetId == budgetId);
        if (normalizedKind == "moved") query = query.Where(item => item.Kind == "moved");
        else if (normalizedKind == "assigned") query = query.Where(item => item.Kind == "assigned" || item.Kind == "removed");
        var actions = await (from action in query
            join user in db.Users.AsNoTracking() on action.ActorUserId equals user.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby action.CreatedAt descending
            select new PlanActionDto(action.Id, action.Month, action.Kind, action.Amount, action.FromCategoryName, action.ToCategoryName,
                action.ActorUserId, actor == null ? "Former member" : actor.UserName ?? actor.Email ?? "Member", actor == null ? null : actor.Email, action.CreatedAt))
            .Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
        return Results.Ok(actions);
    }

    public static void MapEndpoints(WebApplication app)
    {
        static string? UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier);
        app.MapGet("/api/budgets/{id}/sharing", async (string id, ClaimsPrincipal principal, BudgetSharingService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.GetAsync(id, userId, ct) : Results.Unauthorized()).RequireAuthorization();
        app.MapPost("/api/budgets/{id}/invitations", async (string id, InviteBudgetMemberRequest request, ClaimsPrincipal principal, HttpContext context, BudgetSharingService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.InviteAsync(id, userId, request, context, ct) : Results.Unauthorized()).RequireAuthorization();
        app.MapDelete("/api/budgets/{id}/members/{memberUserId}", async (string id, string memberUserId, ClaimsPrincipal principal, BudgetSharingService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.RemoveMemberAsync(id, userId, memberUserId, ct) : Results.Unauthorized()).RequireAuthorization();
        app.MapDelete("/api/budgets/{id}/invitations/{invitationId:guid}", async (string id, Guid invitationId, ClaimsPrincipal principal, BudgetSharingService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.CancelInvitationAsync(id, userId, invitationId, ct) : Results.Unauthorized()).RequireAuthorization();
        app.MapGet("/api/budgets/{id}/plan-actions", async (string id, string? kind, int? limit, ClaimsPrincipal principal, BudgetSharingService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.GetPlanActionsAsync(id, userId, kind, limit ?? 100, ct) : Results.Unauthorized()).RequireAuthorization();
    }
}
