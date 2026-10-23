using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class OpenBudgetUser : IdentityUser
{
    public bool IsSuspended { get; set; }
    public bool IsBootstrapAdministrator { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe = false);
public sealed record UserSessionDto(string Id, string UserName, string? Email, bool IsAdministrator);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UpdateProfileRequest(string UserName);
public sealed record RegisterRequest(string UserName, string Email, string Password);
public sealed record TokenRequest(string UserId, string Token);
public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);
public sealed record RegistrationModeRequest(string Mode);
public sealed record ForgotPasswordRequest(string Email);
public sealed record AdminUserDto(string Id, string UserName, string? Email, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt, string Status, bool IsBootstrapAdministrator, DateTimeOffset? LockoutEnd, string MembershipPlanId, int? BudgetLimitOverride);
public sealed record AdminAuditDto(Guid Id, string ActorUserId, string TargetUserId, string Action, DateTimeOffset CreatedAt);
public sealed record AdminMembershipCountDto(string MembershipPlanId, string Name, int UserCount);
public sealed record AdminRegistrationDto(string UserId, string UserName, DateTimeOffset CreatedAt, string Status);
public sealed record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int PendingUsers,
    int LockedUsers,
    int SuspendedUsers,
    int TotalBudgets,
    int ActiveBudgets,
    IReadOnlyList<AdminMembershipCountDto> UsersByMembership,
    IReadOnlyList<AdminRegistrationDto> RecentRegistrations,
    IReadOnlyList<AdminAuditDto> RecentSecurityEvents);
public sealed record AdminMembershipRequest(string MembershipPlanId, int? BudgetLimitOverride);
public sealed record AdminInviteUserRequest(string UserName, string Email, string MembershipPlanId = "free");

public interface IAccountEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IConfiguration configuration) : IAccountEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(configuration["Smtp:From"]);

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("SMTP delivery is not configured.");
        using var client = new SmtpClient(configuration["Smtp:Host"], int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587)
        {
            EnableSsl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var ssl) || ssl,
            Credentials = string.IsNullOrWhiteSpace(configuration["Smtp:Username"]) ? CredentialCache.DefaultNetworkCredentials :
                new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"])
        };
        using var message = new MailMessage(configuration["Smtp:From"]!, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public static class AuthenticationSetup
{
    public const string AdministratorRole = "Administrator";

    public static IServiceCollection AddOpenBudgetIdentity(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddRateLimiter(options => options.AddPolicy("login", context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })));
        services.AddRateLimiter(options => options.AddPolicy("account-email", context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })));
        services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
        services.AddIdentity<OpenBudgetUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = environment.IsDevelopment() ? 5 : 8;
            options.Password.RequireDigit = !environment.IsDevelopment();
            options.Password.RequireUppercase = !environment.IsDevelopment();
            options.Password.RequireLowercase = !environment.IsDevelopment();
            options.Password.RequireNonAlphanumeric = !environment.IsDevelopment();
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedAccount = true;
        })
        .AddEntityFrameworkStores<BudgetDbContext>()
        .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "OpenBudget.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<OpenBudgetUser>>();
                var identity = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;
                var principal = context.Principal;
                var user = principal is null ? null : await users.GetUserAsync(principal);
                var ticketStamp = principal?.FindFirstValue(identity.ClaimsIdentity.SecurityStampClaimType);
                var currentStamp = user is null ? null : await users.GetSecurityStampAsync(user);
                if (user is null || user.IsSuspended || user.RequiresApproval || !string.Equals(ticketStamp, currentStamp, StringComparison.Ordinal))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.AddAuthorization(options =>
        {
            if (!environment.IsEnvironment("Testing"))
                options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        return services;
    }

    public static async Task EnsureBootstrapAdministratorAsync(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var userManager = services.GetRequiredService<UserManager<OpenBudgetUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(AdministratorRole))
            await roleManager.CreateAsync(new IdentityRole(AdministratorRole));

        var username = configuration["BootstrapAdmin:Username"];
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (environment.IsDevelopment())
        {
            username ??= "admin";
            email ??= "admin@localhost";
            password ??= "admin";
        }
        else if (environment.IsProduction() && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)))
        {
            throw new InvalidOperationException("Production requires BootstrapAdmin:Username, BootstrapAdmin:Email, and BootstrapAdmin:Password to provision the initial administrator.");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = new OpenBudgetUser { UserName = username, Email = email, EmailConfirmed = true, IsBootstrapAdministrator = true };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException("Bootstrap administrator could not be created: " + string.Join("; ", created.Errors.Select(error => error.Code)));
        }
        else if (!user.IsBootstrapAdministrator || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The configured bootstrap administrator conflicts with an existing account.");
        }
        if (!await userManager.IsInRoleAsync(user, AdministratorRole))
            await userManager.AddToRoleAsync(user, AdministratorRole);

        var db = services.GetRequiredService<BudgetDbContext>();
        var membership = await db.UserMemberships.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (membership is null)
            db.UserMemberships.Add(new UserMembershipEntity { UserId = user.Id, MembershipPlanId = "pro" });
        else if (membership.MembershipPlanId != "pro")
            membership.MembershipPlanId = "pro";
        var legacyBudget = await db.BudgetPlans.SingleOrDefaultAsync(x => x.Id == "default");
        if (legacyBudget is not null && legacyBudget.OwnerUserId is null)
            legacyBudget.OwnerUserId = user.Id;
        await db.SaveChangesAsync();
    }

    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").AllowAnonymous();
        auth.MapPost("/login", async (LoginRequest request, UserManager<OpenBudgetUser> users, SignInManager<OpenBudgetUser> signIn, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrEmpty(request.Password))
                return Results.BadRequest(new { error = "Username/email and password are required." });
            var user = request.UserNameOrEmail.Contains('@')
                ? await users.FindByEmailAsync(request.UserNameOrEmail.Trim())
                : await users.FindByNameAsync(request.UserNameOrEmail.Trim());
            if (user is null || user.IsSuspended || user.RequiresApproval) return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Unauthorized();
            user.LastLoginAt = DateTimeOffset.UtcNow;
            await users.UpdateAsync(user);
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        }).RequireRateLimiting("login");
        auth.MapGet("/session", async (ClaimsPrincipal principal, UserManager<OpenBudgetUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null || user.IsSuspended || user.RequiresApproval) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        });
        auth.MapPost("/logout", async (SignInManager<OpenBudgetUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, SignInManager<OpenBudgetUser> signIn, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await users.UpdateSecurityStampAsync(user);
            await AddAdminAuditAsync(db, principal, user.Id, "user.password.change", ct);
            await transaction.CommitAsync(ct);
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        auth.MapPost("/register", async (RegisterRequest request, BudgetDbContext db, UserManager<OpenBudgetUser> users, IAccountEmailSender email, HttpContext context, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var mode = await ReadRegistrationModeAsync(db, ct);
            if (mode == "Disabled") return Results.Problem("Registration is disabled.", statusCode: StatusCodes.Status403Forbidden);
            if (!email.IsConfigured) return Results.Problem("Account email delivery is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 64 || string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Username and email are required and must be within supported lengths." });
            var user = new OpenBudgetUser { UserName = request.UserName.Trim(), Email = request.Email.Trim(), RequiresApproval = mode == "ApprovalRequired" };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            db.UserMemberships.Add(new UserMembershipEntity { UserId = user.Id, MembershipPlanId = "free" });
            await AddAdminAuditAsync(db, user.Id, user.Id, "user.register", ct);
            await transaction.CommitAsync(ct);
            try
            {
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                await email.SendAsync(user.Email!, "Verify your OpenBudget account", BuildLink(context, "/confirm-email", user.Id, token, configuration, environment), ct);
                return Results.Accepted(value: new { message = "If the account can be created, a verification message has been sent." });
            }
            catch
            {
                await users.DeleteAsync(user);
                return Results.Problem("Verification message could not be sent. Try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireRateLimiting("account-email");
        auth.MapPost("/confirm-email", async (TokenRequest request, UserManager<OpenBudgetUser> users) =>
        {
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null) return Results.BadRequest(new { error = "Verification link is invalid or expired." });
            if (!TryDecodeToken(request.Token, out var token)) return Results.BadRequest(new { error = "Verification link is invalid or expired." });
            var result = await users.ConfirmEmailAsync(user, token);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { error = "Verification link is invalid or expired." });
        });
        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, UserManager<OpenBudgetUser> users, IAccountEmailSender email, HttpContext context, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
            if (user is not null && user.EmailConfirmed && email.IsConfigured)
            {
                try
                {
                    var token = await users.GeneratePasswordResetTokenAsync(user);
                    await email.SendAsync(user.Email!, "Reset your OpenBudget password", BuildLink(context, "/reset-password", user.Id, token, configuration, environment), ct);
                }
                catch { /* Keep the response identical to avoid disclosing account existence. */ }
            }
            return Results.Accepted(value: new { message = "If the account exists, a reset message has been sent." });
        }).RequireRateLimiting("account-email");
        auth.MapPost("/reset-password", async (ResetPasswordRequest request, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.BadRequest(new { error = "A new password is required." });
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null) return Results.BadRequest(new { error = "Reset link is invalid or expired." });
            if (!TryDecodeToken(request.Token, out var token)) return Results.BadRequest(new { error = "Reset link is invalid or expired." });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await AddAdminAuditAsync(db, "system", request.UserId, "user.password.reset", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        app.MapGet("/api/auth/registration-mode", async (BudgetDbContext db, CancellationToken ct) =>
            Results.Ok(new { mode = await ReadRegistrationModeAsync(db, ct) })).AllowAnonymous();
        app.MapPut("/api/admin/settings/registration-mode", async (RegistrationModeRequest request, ClaimsPrincipal principal, BudgetDbContext db, CancellationToken ct) =>
        {
            if (request.Mode is not ("Disabled" or "ApprovalRequired" or "Open")) return Results.BadRequest(new { error = "Mode must be Disabled, ApprovalRequired, or Open." });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var setting = await db.ApplicationSettings.SingleOrDefaultAsync(x => x.Key == "RegistrationMode", ct);
            if (setting is null) db.ApplicationSettings.Add(new ApplicationSettingEntity { Key = "RegistrationMode", Value = request.Mode });
            else setting.Value = request.Mode;
            await db.SaveChangesAsync(ct);
            await AddAdminAuditAsync(db, principal, "application", "settings.registration-mode", ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { mode = request.Mode });
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapGet("/api/admin/users", async (string? q, string? status, UserManager<OpenBudgetUser> users, BudgetDbContext db, CancellationToken ct) =>
        {
            var query = users.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();
                query = query.Where(user => user.UserName!.Contains(search) || (user.Email != null && user.Email.Contains(search)));
            }
            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                if (status is not ("Active" or "Pending" or "Locked" or "Suspended"))
                    return Results.BadRequest(new { error = "Status must be Active, Pending, Locked or Suspended." });
                var now = DateTimeOffset.UtcNow;
                query = status switch
                {
                    "Active" => query.Where(user => !user.IsSuspended && user.EmailConfirmed && !user.RequiresApproval && (user.LockoutEnd == null || user.LockoutEnd <= now)),
                    "Pending" => query.Where(user => !user.IsSuspended && (!user.EmailConfirmed || user.RequiresApproval)),
                    "Locked" => query.Where(user => !user.IsSuspended && user.LockoutEnd > now),
                    "Suspended" => query.Where(user => user.IsSuspended),
                    _ => query
                };
            }
            var results = await query.OrderByDescending(user => user.CreatedAt).Take(100).ToListAsync(ct);
            var userIds = results.Select(user => user.Id).ToArray();
            var memberships = await db.UserMemberships.AsNoTracking().Where(item => userIds.Contains(item.UserId))
                .ToDictionaryAsync(item => item.UserId, ct);
            var rows = new List<AdminUserDto>(results.Count);
            foreach (var user in results)
            {
                var userStatus = user.IsSuspended ? "Suspended" : user.LockoutEnd > DateTimeOffset.UtcNow ? "Locked" : user.EmailConfirmed && !user.RequiresApproval ? "Active" : "Pending";
                memberships.TryGetValue(user.Id, out var membership);
                rows.Add(new AdminUserDto(user.Id, user.UserName!, user.Email, user.CreatedAt, user.LastLoginAt, userStatus, user.IsBootstrapAdministrator, user.LockoutEnd, membership?.MembershipPlanId ?? "free", membership?.BudgetLimitOverride));
            }
            return Results.Ok(rows);
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapGet("/api/admin/users/{id}/history", async (string id, UserManager<OpenBudgetUser> users, BudgetDbContext db, CancellationToken ct) =>
        {
            if (!await users.Users.AnyAsync(user => user.Id == id, ct)) return Results.NotFound();
            var history = await db.AdminAuditLogs.AsNoTracking().Where(entry => entry.TargetUserId == id)
                .OrderByDescending(entry => entry.CreatedAt).Take(50)
                .Select(entry => new AdminAuditDto(entry.Id, entry.ActorUserId, entry.TargetUserId, entry.Action, entry.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(history);
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/invite", async (AdminInviteUserRequest request, ClaimsPrincipal principal, BudgetDbContext db,
            UserManager<OpenBudgetUser> users, IAccountEmailSender email, HttpContext context, IConfiguration configuration,
            IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var userName = request.UserName?.Trim();
            var address = request.Email?.Trim();
            if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64 || string.IsNullOrWhiteSpace(address) || address.Length > 256)
                return Results.BadRequest(new { error = "Username and email are required and must be within supported lengths." });
            if (!await db.MembershipPlans.AnyAsync(plan => plan.Id == request.MembershipPlanId, ct))
                return Results.BadRequest(new { error = "The selected membership tier does not exist." });
            if (!email.IsConfigured)
                return Results.Problem("Account email delivery is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

            var user = new OpenBudgetUser { UserName = userName, Email = address };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var temporaryPassword = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) + "Aa1!";
            var created = await users.CreateAsync(user, temporaryPassword);
            if (!created.Succeeded) return Results.BadRequest(new { errors = created.Errors.Select(error => error.Code) });
            db.UserMemberships.Add(new UserMembershipEntity { UserId = user.Id, MembershipPlanId = request.MembershipPlanId });
            await AddAdminAuditAsync(db, principal, user.Id, "user.invite", ct);
            await transaction.CommitAsync(ct);

            try
            {
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                var link = BuildLink(context, "/confirm-email", user.Id, token, configuration, environment);
                await email.SendAsync(user.Email!, "You are invited to OpenBudget",
                    $"Verify your email using this link: {link}\n\nAfter verification, use Forgot password on the sign-in page to set your password.", ct);
                return Results.Accepted(value: new { message = "An account verification link has been sent." });
            }
            catch
            {
                await users.DeleteAsync(user);
                return Results.Problem("Invitation email could not be sent. Try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapGet("/api/admin/membership-plans", async (BudgetDbContext db, CancellationToken ct) =>
            Results.Ok(await db.MembershipPlans.AsNoTracking().OrderBy(plan => plan.Id).ToListAsync(ct)))
            .RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPut("/api/admin/users/{id}/membership", async (string id, AdminMembershipRequest request, ClaimsPrincipal principal, BudgetDbContext db, CancellationToken ct) =>
        {
            if (request.BudgetLimitOverride is < 1 or > 1000)
                return Results.BadRequest(new { error = "A budget limit override must be between 1 and 1000, or omitted to use the membership tier." });
            if (!await db.Users.AnyAsync(user => user.Id == id, ct)) return Results.NotFound();
            if (!await db.MembershipPlans.AnyAsync(plan => plan.Id == request.MembershipPlanId, ct))
                return Results.BadRequest(new { error = "The selected membership tier does not exist." });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var membership = await db.UserMemberships.SingleOrDefaultAsync(item => item.UserId == id, ct);
            if (membership is null)
            {
                membership = new UserMembershipEntity { UserId = id, MembershipPlanId = request.MembershipPlanId, BudgetLimitOverride = request.BudgetLimitOverride };
                db.UserMemberships.Add(membership);
            }
            else
            {
                membership.MembershipPlanId = request.MembershipPlanId;
                membership.BudgetLimitOverride = request.BudgetLimitOverride;
                membership.Version++;
            }
            await db.SaveChangesAsync(ct);
            await AddAdminAuditAsync(db, principal, id, "user.membership.update", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapGet("/api/admin/dashboard", async (BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var accounts = users.Users.AsNoTracking();
            var total = await accounts.CountAsync(ct);
            var active = await accounts.CountAsync(user => !user.IsSuspended && user.EmailConfirmed && !user.RequiresApproval && (user.LockoutEnd == null || user.LockoutEnd <= now), ct);
            var pending = await accounts.CountAsync(user => !user.IsSuspended && (!user.EmailConfirmed || user.RequiresApproval), ct);
            var locked = await accounts.CountAsync(user => !user.IsSuspended && user.LockoutEnd > now, ct);
            var suspended = await accounts.CountAsync(user => user.IsSuspended, ct);
            var budgets = await db.BudgetPlans.CountAsync(ct);
            var membershipCounts = await (from user in accounts
                join membership in db.UserMemberships.AsNoTracking() on user.Id equals membership.UserId into userMemberships
                from membership in userMemberships.DefaultIfEmpty()
                group user by membership == null ? "free" : membership.MembershipPlanId into grouped
                select new { MembershipPlanId = grouped.Key, UserCount = grouped.Count() })
                .ToDictionaryAsync(item => item.MembershipPlanId, item => item.UserCount, ct);
            var plans = await db.MembershipPlans.AsNoTracking().OrderBy(plan => plan.Id).ToListAsync(ct);
            var memberships = plans.Select(plan => new AdminMembershipCountDto(plan.Id, plan.Name,
                membershipCounts.GetValueOrDefault(plan.Id))).ToList();
            var recentRegistrations = await accounts.OrderByDescending(user => user.CreatedAt).Take(5)
                .Select(user => new AdminRegistrationDto(user.Id, user.UserName!, user.CreatedAt,
                    user.IsSuspended ? "Suspended" : user.LockoutEnd > now ? "Locked" : user.EmailConfirmed && !user.RequiresApproval ? "Active" : "Pending"))
                .ToListAsync(ct);
            var recentSecurityEvents = await db.AdminAuditLogs.AsNoTracking().OrderByDescending(entry => entry.CreatedAt).Take(10)
                .Select(entry => new AdminAuditDto(entry.Id, entry.ActorUserId, entry.TargetUserId, entry.Action, entry.CreatedAt))
                .ToListAsync(ct);
            var activeBudgets = await db.BudgetPlans.CountAsync(budget => !budget.IsArchived, ct);
            return Results.Ok(new AdminDashboardDto(total, active, pending, locked, suspended, budgets, activeBudgets,
                memberships, recentRegistrations, recentSecurityEvents));
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/approve", async (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            if (!user.EmailConfirmed) return Results.Conflict(new { error = "Verify the user's email before approval." });
            if (user.IsBootstrapAdministrator || !user.RequiresApproval) return Results.Conflict(new { error = "Only pending registrations can be approved." });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            user.RequiresApproval = false;
            var result = await users.UpdateAsync(user);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await AddAdminAuditAsync(db, principal, user.Id, "registration.approve", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/reject", async (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            if (user.IsBootstrapAdministrator || !user.RequiresApproval) return Results.Conflict(new { error = "Only pending registrations can be rejected." });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            user.RequiresApproval = false;
            user.IsSuspended = true;
            var result = await users.UpdateAsync(user);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await AddAdminAuditAsync(db, principal, user.Id, "registration.reject", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/suspend", (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
            SetSuspensionAsync(id, true, principal, db, users, ct)).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/reactivate", (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
            SetSuspensionAsync(id, false, principal, db, users, ct)).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/unlock", async (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await users.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await users.ResetAccessFailedCountAsync(user);
            await AddAdminAuditAsync(db, principal, user.Id, "user.unlock", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapPost("/api/admin/users/{id}/password-reset", async (string id, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, IAccountEmailSender email, HttpContext context, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
                return Results.Conflict(new { error = "A verified email address is required before a password reset can be sent." });
            if (!email.IsConfigured)
                return Results.Problem("Account email delivery is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            try
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var link = BuildLink(context, "/reset-password", user.Id, token, configuration, environment);
                await email.SendAsync(user.Email, "Reset your OpenBudget password", link, ct);
            }
            catch when (!ct.IsCancellationRequested)
            {
                return Results.Problem("Password reset email could not be sent.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await AddAdminAuditAsync(db, principal, user.Id, "user.password-reset.initiated", ct);
            await transaction.CommitAsync(ct);
            return Results.Accepted(value: new { message = "A password reset link has been sent to the user's verified email address." });
        }).RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        app.MapGet("/api/admin/audit", async (BudgetDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AdminAuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100)
                .Select(x => new AdminAuditDto(x.Id, x.ActorUserId, x.TargetUserId, x.Action, x.CreatedAt)).ToListAsync(ct)))
            .RequireAuthorization(policy => policy.RequireRole(AdministratorRole));
        auth.MapGet("/profile", async (ClaimsPrincipal principal, UserManager<OpenBudgetUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        }).RequireAuthorization();
        auth.MapPut("/profile", async (UpdateProfileRequest request, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, SignInManager<OpenBudgetUser> signIn, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var userName = request.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64)
                return Results.BadRequest(new { error = "Username is required and must be no longer than 64 characters." });
            if (string.Equals(user.UserName, userName, StringComparison.Ordinal))
                return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, await users.IsInRoleAsync(user, AdministratorRole)));
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await users.SetUserNameAsync(user, userName);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
            await AddAdminAuditAsync(db, principal, user.Id, "user.profile.update", ct);
            await transaction.CommitAsync(ct);
            await signIn.RefreshSignInAsync(user);
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        }).RequireAuthorization();
    }

    private static async Task<string> ReadRegistrationModeAsync(BudgetDbContext db, CancellationToken ct) =>
        await db.ApplicationSettings.Where(x => x.Key == "RegistrationMode").Select(x => x.Value).SingleOrDefaultAsync(ct) ?? "Disabled";

    private static async Task<IResult> SetSuspensionAsync(string id, bool suspended, ClaimsPrincipal principal, BudgetDbContext db, UserManager<OpenBudgetUser> users, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        if (user.IsBootstrapAdministrator) return Results.Conflict(new { error = "The permanent administrator cannot be suspended." });
        if (suspended && users.GetUserId(principal) == user.Id) return Results.Conflict(new { error = "Administrators cannot suspend their own account." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        user.IsSuspended = suspended;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors.Select(error => error.Code) });
        await AddAdminAuditAsync(db, principal, user.Id, suspended ? "user.suspend" : "user.reactivate", ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task AddAdminAuditAsync(BudgetDbContext db, ClaimsPrincipal principal, string targetId, string action, CancellationToken ct)
        => await AddAdminAuditAsync(db, principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", targetId, action, ct);

    private static async Task AddAdminAuditAsync(BudgetDbContext db, string actorId, string targetId, string action, CancellationToken ct)
    {
        db.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            ActorUserId = actorId,
            TargetUserId = targetId,
            Action = action
        });
        await db.SaveChangesAsync(ct);
    }

    private static string BuildLink(HttpContext context, string path, string userId, string token, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredOrigin = configuration["AccountEmail:PublicOrigin"];
        var origin = configuredOrigin;
        if (string.IsNullOrWhiteSpace(origin) && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            origin = $"{context.Request.Scheme}://{context.Request.Host}";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) || baseUri.AbsolutePath != "/" ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() || environment.IsEnvironment("Testing"))))
            throw new InvalidOperationException("AccountEmail:PublicOrigin must be a trusted absolute HTTPS origin in shared environments.");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, new Dictionary<string, string?>
        {
            ["userId"] = userId,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });
        return $"{baseUri.GetLeftPart(UriPartial.Authority)}{query}";
    }

    private static bool TryDecodeToken(string encoded, out string token)
    {
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded)); return true; }
        catch (Exception error) when (error is FormatException or ArgumentException) { token = ""; return false; }
    }
}
