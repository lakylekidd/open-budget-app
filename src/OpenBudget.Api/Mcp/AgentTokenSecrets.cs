using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenBudget.Api.Mcp;

/// <summary>Generates and verifies agent token secrets. The plaintext exists only in the creation response.</summary>
public static class AgentTokenSecrets
{
    public const string Prefix = "obk_";
    public const int SecretBytes = 32; // 256 bits of CSPRNG entropy.
    public const int DisplayPrefixLength = 12; // "obk_" + 8 characters; reveals 48 of 256 bits.
    // "obk_" + 43 base64url characters (32 bytes without padding).
    public static readonly int TokenLength = Prefix.Length + WebEncoders.Base64UrlEncode(new byte[SecretBytes]).Length;

    public sealed record GeneratedToken(string Plaintext, string Hash, string DisplayPrefix);

    public static GeneratedToken Generate()
    {
        var token = Prefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        return new GeneratedToken(token, Hash(token), token[..DisplayPrefixLength]);
    }

    /// <summary>Lowercase hex SHA-256. A fast unsalted hash is appropriate because the secret has 256 bits of entropy.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Cheap structural check before any database lookup; rejects other bearer formats and oversized input.</summary>
    public static bool HasValidShape(string? token)
    {
        if (token is null || token.Length != TokenLength || !token.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        for (var i = Prefix.Length; i < token.Length; i++)
        {
            var c = token[i];
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')) return false;
        }
        return true;
    }

    /// <summary>Constant-time comparison of a presented token with a stored hash.</summary>
    public static bool Verify(string token, string storedHash)
    {
        if (!HasValidShape(token) || storedHash.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash));
    }

    /// <summary>Extracts a bearer credential from an Authorization header value; null when absent or not a bearer.</summary>
    public static string? ReadBearer(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader) || authorizationHeader.Length > 512) return null;
        const string scheme = "Bearer ";
        if (!authorizationHeader.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return null;
        var value = authorizationHeader[scheme.Length..].Trim();
        return value.Length == 0 ? null : value;
    }
}
