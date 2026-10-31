using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

/// <summary>The colour themes a user can pick. The UI owns the palettes; the server only stores and validates the key.</summary>
public static class UserThemes
{
    public const string Default = "midnight";
    public static readonly IReadOnlyList<string> Keys = ["midnight", "ocean", "forest", "rosewood", "graphite"];
    public static bool IsValid(string? key) => key is not null && Keys.Contains(key, StringComparer.Ordinal);
}

public sealed record UserPreferencesDto(string Theme);
public sealed record UpdateUserPreferencesRequest(string? Theme);

/// <summary>Per-account display preferences. One row per user, created on the first save; a missing row means the defaults.</summary>
public sealed class UserPreferenceEntity
{
    public string UserId { get; set; } = "";
    public string Theme { get; set; } = UserThemes.Default;
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class UserPreferenceService
{
    public static void MapEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/me/preferences").RequireAuthorization();

        group.MapGet("", async (ClaimsPrincipal principal, UserManager<OpenBudgetUser> users, BudgetDbContext db, CancellationToken ct) =>
        {
            var userId = users.GetUserId(principal);
            if (userId is null) return Results.Unauthorized();
            var theme = await db.UserPreferences.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Theme).SingleOrDefaultAsync(ct);
            // A stored key that is no longer offered falls back to the default instead of breaking the UI.
            return Results.Ok(new UserPreferencesDto(UserThemes.IsValid(theme) ? theme! : UserThemes.Default));
        });

        group.MapPut("", async (UpdateUserPreferencesRequest request, ClaimsPrincipal principal, UserManager<OpenBudgetUser> users, BudgetDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = users.GetUserId(principal);
            if (userId is null) return Results.Unauthorized();
            if (!UserThemes.IsValid(request.Theme))
                return Results.BadRequest(new { error = $"Theme must be one of: {string.Join(", ", UserThemes.Keys)}." });
            var theme = request.Theme!;
            var now = clock.GetUtcNow();

            // Update first; insert only when no row exists. A concurrent first save from another tab can lose the insert race,
            // in which case the row now exists and a second update applies the value (last write wins).
            var updated = await db.UserPreferences.Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Theme, theme).SetProperty(x => x.UpdatedAt, now), ct);
            if (updated == 0)
            {
                db.UserPreferences.Add(new UserPreferenceEntity { UserId = userId, Theme = theme, UpdatedAt = now });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    await db.UserPreferences.Where(x => x.UserId == userId)
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.Theme, theme).SetProperty(x => x.UpdatedAt, now), ct);
                }
            }
            return Results.Ok(new UserPreferencesDto(theme));
        });
    }
}
