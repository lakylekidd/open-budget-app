using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;

namespace OpenBudget.Api;

public sealed record EnableBank(string Name, string Country, string? Bic, string? Logo, long MaximumConsentValidity);
public sealed record EnableAccount(string Uid, string Name, string Currency, string IdentificationHash);
public sealed record EnableSession(string SessionId, DateTimeOffset ValidUntil, IReadOnlyList<EnableAccount> Accounts);
public sealed record EnableTransaction(string TransactionId, string? EntryReference, string ProviderStatus, DateOnly Date, decimal Amount, string Currency, string Payee, string Memo);
public sealed record EnableTransactionPage(IReadOnlyList<EnableTransaction> Transactions, string? ContinuationKey);
public sealed class EnableBankingProviderException(int statusCode) : Exception($"Enable Banking request failed with HTTP {statusCode}.")
{
    public int StatusCode { get; } = statusCode;
}

public interface IEnableBankingClient
{
    Task<IReadOnlyList<EnableBank>> GetBanksAsync(string country, CancellationToken ct);
    Task<string> StartAuthorizationAsync(string country, string bankName, string state, string redirectUrl, string psuId, DateTimeOffset validUntil, CancellationToken ct);
    Task<EnableSession> CreateSessionAsync(string code, CancellationToken ct);
    Task<EnableTransactionPage> GetTransactionsAsync(string sessionId, string accountUid, DateOnly dateFrom, string? continuationKey, CancellationToken ct);
    Task RevokeSessionAsync(string sessionId, CancellationToken ct);
}

public sealed class EnableBankingClient(IHttpClientFactory clients, IConfiguration configuration, TimeProvider clock) : IEnableBankingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<EnableBank>> GetBanksAsync(string country, CancellationToken ct)
    {
        using var document = await GetAsync($"/aspsps?country={Uri.EscapeDataString(country)}&psu_type=personal&service=AIS", ct);
        if (!document.RootElement.TryGetProperty("aspsps", out var aspsps) || aspsps.ValueKind != JsonValueKind.Array) return [];
        return aspsps.EnumerateArray().Select(item => new EnableBank(
            item.String("name"), item.String("country"), item.OptionalString("bic"), item.OptionalString("logo"), item.Long("maximum_consent_validity"))).ToList();
    }

    public async Task<string> StartAuthorizationAsync(string country, string bankName, string state, string redirectUrl, string psuId, DateTimeOffset validUntil, CancellationToken ct)
    {
        using var document = await PostAsync("/auth", new
        {
            access = new { transactions = true, valid_until = validUntil.ToString("O", CultureInfo.InvariantCulture) },
            aspsp = new { name = bankName, country }, state, redirect_url = redirectUrl, psu_type = "personal", psu_id = psuId
        }, ct);
        return document.RootElement.String("url");
    }

    public async Task<EnableSession> CreateSessionAsync(string code, CancellationToken ct)
    {
        using var document = await PostAsync("/sessions", new { code }, ct);
        var root = document.RootElement;
        var access = root.GetProperty("access");
        var accounts = root.GetProperty("accounts").EnumerateArray().Select(item => new EnableAccount(
            item.String("uid"), item.OptionalString("product") ?? item.OptionalString("details") ?? "Bank account", item.OptionalString("currency") ?? "EUR",
            item.OptionalString("identification_hash") ?? "")).ToList();
        return new(root.String("session_id"), access.GetProperty("valid_until").GetDateTimeOffset(), accounts);
    }

    public async Task<EnableTransactionPage> GetTransactionsAsync(string sessionId, string accountUid, DateOnly dateFrom, string? continuationKey, CancellationToken ct)
    {
        var query = $"date_from={dateFrom:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(continuationKey) ? "" : $"&continuation_key={Uri.EscapeDataString(continuationKey)}");
        using var document = await GetAsync($"/accounts/{Uri.EscapeDataString(accountUid)}/transactions?{query}", ct);
        var root = document.RootElement;
        var rows = root.GetProperty("transactions").EnumerateArray().Select(ParseTransaction).ToList();
        return new(rows, root.OptionalString("continuation_key"));
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/sessions/{Uri.EscapeDataString(sessionId)}");
        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw ProviderFailure(response.StatusCode);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(request, ct);
        return await ReadJsonAsync(response, ct);
    }

    private async Task<JsonDocument> PostAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: JsonOptions) };
        using var response = await SendAsync(request, ct);
        return await ReadJsonAsync(response, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var baseAddress = configuration["EnableBanking:BaseAddress"] ?? "https://api.enablebanking.com";
        request.RequestUri = new Uri(new Uri(baseAddress, UriKind.Absolute), request.RequestUri!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await clients.CreateClient("enablebanking").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) throw ProviderFailure(response.StatusCode);
        try { return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
        catch (JsonException) { throw new InvalidOperationException("Enable Banking returned an invalid response."); }
    }

    private string CreateJwt()
    {
        var appId = configuration["EnableBanking:ApplicationId"];
        var privateKeyPath = configuration["EnableBanking:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
            throw new InvalidOperationException("Enable Banking is not configured.");
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "RS256", kid = appId }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new { iss = "enablebanking.com", aud = "api.enablebanking.com", iat = now, exp = now + 300 }));
        var unsigned = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Encode(signature)}";
    }

    private static EnableTransaction ParseTransaction(JsonElement item)
    {
        var rawAmount = decimal.Parse(item.GetProperty("transaction_amount").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
        var indicator = item.OptionalString("credit_debit_indicator");
        var amount = indicator == "DBIT" ? -Math.Abs(rawAmount) : indicator == "CRDT" ? Math.Abs(rawAmount) : rawAmount;
        var status = item.OptionalString("status") ?? "BOOK";
        var dateText = item.OptionalString(status == "BOOK" ? "booking_date" : "transaction_date")
            ?? item.OptionalString("value_date") ?? item.OptionalString("booking_date")
            ?? throw new InvalidOperationException("Enable Banking returned a transaction without a date.");
        var payee = amount < 0 ? item.GetPropertyOrDefault("creditor")?.OptionalString("name") : item.GetPropertyOrDefault("debtor")?.OptionalString("name");
        var remittance = item.GetPropertyOrDefault("remittance_information");
        var memo = remittance?.ValueKind == JsonValueKind.Array ? string.Join(" ", remittance.Value.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
            : remittance?.ValueKind == JsonValueKind.String ? remittance.Value.GetString() ?? "" : "";
        var id = item.OptionalString("transaction_id") ?? item.OptionalString("entry_reference") ?? item.OptionalString("transaction_reference")
            ?? throw new InvalidOperationException("Enable Banking returned a transaction without a stable reference.");
        return new(id, item.OptionalString("entry_reference"), status == "BOOK" ? "booked" : "pending", DateOnly.Parse(dateText, CultureInfo.InvariantCulture), amount,
            item.GetProperty("transaction_amount").OptionalString("currency") ?? "", payee ?? "", memo);
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static EnableBankingProviderException ProviderFailure(System.Net.HttpStatusCode status) => new((int)status);
}

internal static class EnableJsonExtensions
{
    public static string String(this JsonElement element, string name) => element.GetProperty(name).GetString() ?? "";
    public static string? OptionalString(this JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    public static long Long(this JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value : null;
}
