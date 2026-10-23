using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace OpenBudget.Api;

public sealed class OpenBudgetUser : IdentityUser
{
    public bool IsSuspended { get; set; }
    public bool IsBootstrapAdministrator { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe = false);
public sealed record UserSessionDto(string Id, string UserName, string? Email, bool IsAdministrator);

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
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<BudgetDbContext>()
        .AddDefaultTokenProviders();

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
                var user = await users.GetUserAsync(context.Principal!);
                if (user is null || user.IsSuspended)
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
            if (user is null || user.IsSuspended) return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        }).RequireRateLimiting("login");
        auth.MapGet("/session", async (ClaimsPrincipal principal, UserManager<OpenBudgetUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null || user.IsSuspended) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserSessionDto(user.Id, user.UserName!, user.Email, roles.Contains(AdministratorRole)));
        });
        auth.MapPost("/logout", async (SignInManager<OpenBudgetUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
