using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;
using OpenBudget.Api.Mcp;

namespace OpenBudget.IntegrationTests;

/// <summary>Synthetic users, budgets, agent tokens and a minimal MCP Streamable HTTP JSON-RPC client for tests.</summary>
public static class McpTestKit
{
    public const string Password = "Strong-pass1!";
    public const string ProtocolVersion = "2025-06-18";

    public static async Task<string> CreateUserAsync(WebApplicationFactory<Program> app, string userName, bool administrator = false)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
        var user = new OpenBudgetUser { UserName = userName, Email = $"{userName}@example.test", EmailConfirmed = true };
        var result = await users.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(x => x.Code)));
        if (administrator)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(AuthenticationSetup.AdministratorRole)) await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            await users.AddToRoleAsync(user, AuthenticationSetup.AdministratorRole);
        }
        return user.Id;
    }

    public static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string userName)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    public static async Task<OwnedBudgetDto> CreateBudgetAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
    }

    public static async Task<CreatedAgentTokenDto> CreateTokenAsync(HttpClient client, string permission, params string[] budgetIds)
    {
        var response = await client.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest($"{permission} agent", permission, budgetIds, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
    }

    public static McpTestClient Mcp(WebApplicationFactory<Program> app, string? token) => new(app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }), token);
}

public sealed class McpTestClient(HttpClient http, string? token) : IDisposable
{
    private int id;
    public HttpClient Http { get; } = http;

    public async Task<HttpResponseMessage> PostRawAsync(string method, object? parameters, string? overrideToken = null, bool includeProtocolHeader = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = Interlocked.Increment(ref id), method, @params = parameters ?? new { } }), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (includeProtocolHeader && method != "initialize") request.Headers.Add("MCP-Protocol-Version", McpTestKit.ProtocolVersion);
        var bearer = overrideToken ?? token;
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await Http.SendAsync(request);
    }

    /// <summary>Returns the JSON-RPC response object (with either "result" or "error").</summary>
    public async Task<JsonElement> RpcAsync(string method, object? parameters = null)
    {
        using var response = await PostRawAsync(method, parameters);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"MCP HTTP {(int)response.StatusCode}: {text}");
        return Parse(text);
    }

    public static JsonElement Parse(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{')) return JsonDocument.Parse(trimmed).RootElement.Clone();
        // Streamable HTTP may answer with an SSE stream; take the last JSON-RPC message.
        var data = text.Split('\n').Where(line => line.StartsWith("data:")).Select(line => line[5..].Trim()).Last(line => line.StartsWith('{'));
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    public Task<JsonElement> InitializeAsync() => RpcAsync("initialize", new
    {
        protocolVersion = McpTestKit.ProtocolVersion,
        capabilities = new { },
        clientInfo = new { name = "openbudget-tests", version = "1.0.0" }
    });

    public async Task<List<string>> ListToolNamesAsync()
    {
        var response = await RpcAsync("tools/list");
        return response.GetProperty("result").GetProperty("tools").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();
    }

    public async Task<JsonElement> ListToolsAsync() => (await RpcAsync("tools/list")).GetProperty("result").GetProperty("tools");

    /// <summary>Returns the CallToolResult ("result"); fails on JSON-RPC level errors.</summary>
    public async Task<JsonElement> CallAsync(string name, object? arguments = null)
    {
        var response = await RpcAsync("tools/call", new { name, arguments = arguments ?? new { } });
        Assert.True(response.TryGetProperty("result", out var result), response.ToString());
        return result;
    }

    /// <summary>Returns the raw JSON-RPC response for calls that may fail at protocol level.</summary>
    public Task<JsonElement> CallRawAsync(string name, object? arguments = null) => RpcAsync("tools/call", new { name, arguments = arguments ?? new { } });

    public void Dispose() => Http.Dispose();
}

public static class McpResultExtensions
{
    public static bool IsError(this JsonElement result) => result.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True;
    public static JsonElement Structured(this JsonElement result) => result.GetProperty("structuredContent");
    public static string ErrorCode(this JsonElement result) => result.Structured().GetProperty("error").GetProperty("code").GetString()!;
    public static void AssertOk(this JsonElement result) => Assert.False(result.IsError(), result.ToString());
}
