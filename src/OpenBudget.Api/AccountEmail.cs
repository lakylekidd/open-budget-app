using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record AccountEmailContent(string Text, string Html);
public sealed record SmtpSettingsDto(string Host, int Port, string From, string? Username, bool EnableSsl, bool HasPassword, bool IsConfigured, string Source);
public sealed record UpdateSmtpSettingsRequest(string Host, int Port, string From, string? Username, string? Password, bool EnableSsl);
public sealed record SendTestEmailRequest(string To);

public interface IAccountEmailSender
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);
    Task SendAsync(string to, string subject, AccountEmailContent content, CancellationToken cancellationToken);
}

public sealed class AccountEmailSettingsService(BudgetDbContext db, IConfiguration configuration, IDataProtectionProvider dataProtection)
{
    private readonly IDataProtector protector = dataProtection.CreateProtector("OpenBudget.Api.AccountEmailSettings.v1");

    public async Task<SmtpSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stored = await db.SmtpSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (stored is not null)
        {
            var storedConfigured = await GetStoredConnectionAsync(stored, cancellationToken) is not null;
            return new SmtpSettingsDto(stored.Host, stored.Port, stored.From, stored.Username, stored.EnableSsl, stored.EncryptedPassword is not null, storedConfigured, "admin");
        }

        var host = configuration["Smtp:Host"]?.Trim() ?? "";
        var from = configuration["Smtp:From"]?.Trim() ?? "";
        var username = NullIfWhiteSpace(configuration["Smtp:Username"]);
        var hasPassword = !string.IsNullOrWhiteSpace(configuration["Smtp:Password"]);
        var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        var ssl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var configuredSsl) || configuredSsl;
        var configured = IsValidConnection(host, port, from) && (username is null || hasPassword);
        return new SmtpSettingsDto(host, port, from, username, ssl, hasPassword, configured, configured ? "environment" : "none");
    }

    public async Task<(SmtpSettingsDto? Settings, string? Error)> SaveAsync(UpdateSmtpSettingsRequest request, CancellationToken cancellationToken)
    {
        var host = request.Host?.Trim() ?? "";
        var from = request.From?.Trim() ?? "";
        var username = NullIfWhiteSpace(request.Username);
        if (!IsValidConnection(host, request.Port, from))
            return (null, "Enter a valid SMTP host, port, and From email address.");
        if (request.Password?.Length > 512 || username?.Length > 256)
            return (null, "SMTP credentials are too long.");

        var settings = await db.SmtpSettings.SingleOrDefaultAsync(cancellationToken);
        var encryptedPassword = settings?.EncryptedPassword;
        if (username is null)
            encryptedPassword = null;
        else if (!string.IsNullOrWhiteSpace(request.Password))
            encryptedPassword = protector.Protect(request.Password);
        else if (string.IsNullOrWhiteSpace(encryptedPassword))
            return (null, "Enter an SMTP password when using a username.");

        if (settings is null)
        {
            settings = new SmtpSettingsEntity { Id = 1 };
            db.SmtpSettings.Add(settings);
        }
        settings.Host = host;
        settings.Port = request.Port;
        settings.From = from;
        settings.Username = username;
        settings.EncryptedPassword = encryptedPassword;
        settings.EnableSsl = request.EnableSsl;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (new SmtpSettingsDto(settings.Host, settings.Port, settings.From, settings.Username, settings.EnableSsl, settings.EncryptedPassword is not null, true, "admin"), null);
    }

    public async Task<SmtpConnectionSettings?> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var stored = await db.SmtpSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (stored is not null)
            return await GetStoredConnectionAsync(stored, cancellationToken);

        var host = configuration["Smtp:Host"]?.Trim() ?? "";
        var from = configuration["Smtp:From"]?.Trim() ?? "";
        var username = NullIfWhiteSpace(configuration["Smtp:Username"]);
        var password = configuration["Smtp:Password"];
        var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        var ssl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var configuredSsl) || configuredSsl;
        return IsValidConnection(host, port, from) && (username is null || !string.IsNullOrWhiteSpace(password))
            ? new SmtpConnectionSettings(host, port, from, username, password, ssl)
            : null;
    }

    private Task<SmtpConnectionSettings?> GetStoredConnectionAsync(SmtpSettingsEntity settings, CancellationToken cancellationToken)
    {
        if (!IsValidConnection(settings.Host, settings.Port, settings.From) || (settings.Username is not null && settings.EncryptedPassword is null))
            return Task.FromResult<SmtpConnectionSettings?>(null);
        try
        {
            var password = settings.EncryptedPassword is null ? null : protector.Unprotect(settings.EncryptedPassword);
            return Task.FromResult<SmtpConnectionSettings?>(new SmtpConnectionSettings(settings.Host, settings.Port, settings.From, settings.Username, password, settings.EnableSsl));
        }
        catch (CryptographicException)
        {
            return Task.FromResult<SmtpConnectionSettings?>(null);
        }
    }

    private static bool IsValidConnection(string host, int port, string from) =>
        !string.IsNullOrWhiteSpace(host) && Uri.CheckHostName(host) != UriHostNameType.Unknown && port is >= 1 and <= 65535 && IsEmailAddress(from);

    internal static bool IsEmailAddress(string value)
    {
        try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SmtpConnectionSettings(string Host, int Port, string From, string? Username, string? Password, bool EnableSsl);

public sealed class SmtpAccountEmailSender(AccountEmailSettingsService settings) : IAccountEmailSender
{
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) => await settings.GetConnectionAsync(cancellationToken) is not null;

    public async Task SendAsync(string to, string subject, AccountEmailContent content, CancellationToken cancellationToken)
    {
        var connection = await settings.GetConnectionAsync(cancellationToken) ?? throw new InvalidOperationException("SMTP delivery is not configured.");
        using var client = new SmtpClient(connection.Host, connection.Port)
        {
            EnableSsl = connection.EnableSsl,
            Credentials = connection.Username is null ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(connection.Username, connection.Password)
        };
        using var message = new MailMessage(connection.From, to, subject, content.Html)
        {
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(content.Text, Encoding.UTF8, "text/plain"));
        await client.SendMailAsync(message, cancellationToken);
    }
}

public static class AccountEmailTemplates
{
    public static AccountEmailContent VerifyAccount(string link) => Build(
        "Verify your OpenBudget account", "Welcome to OpenBudget. Confirm your email to activate your account and start making every euro feel intentional.", "Verify email", link,
        "This verification link expires in two hours.");

    public static AccountEmailContent ResetPassword(string link) => Build(
        "Reset your OpenBudget password", "We received a request to reset your password. Choose a new password to get back to your budget.", "Reset password", link,
        "This reset link expires in two hours. If you did not request it, you can safely ignore this email.");

    public static AccountEmailContent Invitation(string link) => Build(
        "You’re invited to OpenBudget", "You have been invited to OpenBudget. Verify your email, then use Forgot password to choose your first password.", "Verify email", link,
        "This verification link expires in two hours.");

    public static AccountEmailContent Test() => Build(
        "OpenBudget email is ready", "Your OpenBudget email delivery is configured correctly. Password resets and account verification messages can now be delivered from this address.", null, null,
        "You received this message because an OpenBudget administrator sent a delivery test.");

    private static AccountEmailContent Build(string title, string message, string? action, string? link, string footer)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedMessage = WebUtility.HtmlEncode(message);
        var encodedFooter = WebUtility.HtmlEncode(footer);
        var textAction = action is null || link is null ? "" : $"\n\n{action}:\n{link}";
        var htmlAction = action is null || link is null ? "" : $"<a href=\"{WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;padding:13px 20px;border-radius:8px;background:#6d5df5;color:#ffffff;font-size:15px;font-weight:bold;text-decoration:none\">{WebUtility.HtmlEncode(action)}</a>";
        var text = $"OPENBUDGET\n\n{title}\n\n{message}{textAction}\n\n{footer}";
        var html = $"<!doctype html><html><body style=\"margin:0;padding:32px 16px;background:#f0f1f8;color:#1d1e36;font-family:Arial,sans-serif\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td align=\"center\"><table role=\"presentation\" width=\"100%\" style=\"max-width:560px;background:#ffffff;border:1px solid #dedff0;border-radius:18px;overflow:hidden\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"padding:30px 36px;background:linear-gradient(135deg,#6d5df5,#8f95ff)\"><div style=\"font-size:12px;letter-spacing:1.8px;font-weight:bold;color:#ffffff\">OPENBUDGET</div><div style=\"margin-top:8px;font-size:18px;color:#eef0ff\">Make every euro feel intentional.</div></td></tr><tr><td style=\"padding:34px 36px\"><h1 style=\"margin:0 0 16px;font-size:26px;line-height:1.2;color:#20213c\">{encodedTitle}</h1><p style=\"margin:0 0 26px;font-size:16px;line-height:1.6;color:#52536d\">{encodedMessage}</p>{htmlAction}<p style=\"margin:28px 0 0;padding-top:20px;border-top:1px solid #e6e6f0;font-size:13px;line-height:1.5;color:#777891\">{encodedFooter}</p></td></tr></table></td></tr></table></body></html>";
        return new AccountEmailContent(text, html);
    }
}
