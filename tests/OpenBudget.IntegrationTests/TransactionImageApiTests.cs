using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenBudget.Api;
using OpenBudget.Tests;

namespace OpenBudget.IntegrationTests;

public sealed class TransactionImageApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    [Fact]
    public async Task Uploads_list_download_with_safe_headers_count_in_register_and_audit_without_bumping_money()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, _) = await SignedInBudgetAsync(app, "alice");
        var account = await AddAccountAsync(client);
        var transaction = await AddTransactionAsync(client, account, -12.5m);
        var versionBefore = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;

        var single = await UploadAsync(client, transaction, Guid.NewGuid(), ("receipt.jpg", TestImages.Jpeg())).ReadAs<TransactionImageListDto>();
        var image = Assert.Single(single.Images);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal("receipt.jpg", image.FileName);
        Assert.Equal((640, 480), (image.Width, image.Height));
        Assert.Equal("upload", image.Source);

        var multiple = await UploadAsync(client, transaction, Guid.NewGuid(), ("../../scan.png", TestImages.Png), ("paste.gif", TestImages.Gif), ("phone.webp", TestImages.WebP())).ReadAs<TransactionImageListDto>();
        Assert.Equal(4, multiple.Images.Count);
        Assert.Contains(multiple.Images, x => x.FileName == "scan.png" && x.ContentType == "image/png");
        Assert.Equal(multiple.Images, (await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images);

        using var download = await client.GetAsync(image.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/jpeg", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("default-src 'none'; img-src 'self'; sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("inline; filename=\"receipt.jpg\"", download.Content.Headers.GetValues("Content-Disposition").Single());
        Assert.True(download.Headers.CacheControl!.Private);
        Assert.False(download.Headers.CacheControl.Public);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.False(TestImages.Contains(bytes, "GPSLatitude"), "EXIF GPS must be stripped before storage.");
        Assert.Equal(TestImages.Jpeg()[^9..], bytes[^9..]);

        var row = Assert.Single((await client.GetFromJsonAsync<RegisterDto>("/api/transactions"))!.Items, x => x.Id == transaction);
        Assert.Equal(4, row.ImageCount);
        var accounts = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!;
        Assert.Equal(versionBefore, accounts.Version);
        Assert.Equal(87.5m, accounts.Accounts.Single().Balance);

        var audit = (await client.GetFromJsonAsync<AuditPageDto>($"/api/ledger/audit?resourceId={transaction}"))!;
        var receipts = audit.Items.Where(x => x.Kind.StartsWith("transaction.image.add/")).ToList();
        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, x => { Assert.Contains("\"imageId\"", x.AfterJson); Assert.Contains("\"sizeBytes\"", x.AfterJson); Assert.DoesNotContain("receipt.jpg", x.AfterJson); Assert.True(x.AfterJson.Length < 2000); });

        await using var db = app.CreateDb();
        Assert.Equal(4, await db.TransactionImageBlobs.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Command_replay_returns_the_same_images_and_conflicting_reuse_is_rejected()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, _) = await SignedInBudgetAsync(app, "alice");
        var transaction = await AddTransactionAsync(client, await AddAccountAsync(client), -3);
        var command = Guid.NewGuid();
        var first = await UploadAsync(client, transaction, command, ("a.png", TestImages.Png)).ReadAs<TransactionImageListDto>();
        var replay = await UploadAsync(client, transaction, command, ("a.png", TestImages.Png)).ReadAs<TransactionImageListDto>();
        Assert.Equal(first.Images, replay.Images);
        Assert.Single(replay.Images);
        using var reused = await UploadAsync(client, transaction, command, ("b.gif", TestImages.Gif));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }

    [Fact]
    public async Task Rejects_wrong_types_oversized_files_and_too_many_images_without_partial_writes()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, _) = await SignedInBudgetAsync(app, "alice");
        var transaction = await AddTransactionAsync(client, await AddAccountAsync(client), -3);

        using var svg = await UploadAsync(client, transaction, Guid.NewGuid(), ("logo.png", TestImages.Svg));
        Assert.Equal(HttpStatusCode.BadRequest, svg.StatusCode);
        Assert.Contains("JPEG, PNG, WebP and GIF", await svg.Content.ReadAsStringAsync());
        using var html = await UploadAsync(client, transaction, Guid.NewGuid(), ("a.png", TestImages.Png), ("page.jpg", Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")));
        Assert.Equal(HttpStatusCode.BadRequest, html.StatusCode);
        var huge = new byte[TransactionImageValidator.MaxImageBytes + 1]; TestImages.Png.CopyTo(huge, 0);
        using var tooLarge = await UploadAsync(client, transaction, Guid.NewGuid(), ("big.png", huge));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        using var tooMany = await UploadAsync(client, transaction, Guid.NewGuid(), Enumerable.Range(0, 11).Select(i => ($"{i}.png", TestImages.Png)).ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        using var noCommand = await client.PostAsync($"/api/transactions/{transaction}/images", new MultipartFormDataContent { { new ByteArrayContent(TestImages.Png), "files", "a.png" } });
        Assert.Equal(HttpStatusCode.BadRequest, noCommand.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images);

        for (var batch = 0; batch < 2; batch++)
            (await UploadAsync(client, transaction, Guid.NewGuid(), Enumerable.Range(0, 10).Select(i => ($"{i}.png", TestImages.Png)).ToArray())).EnsureSuccessStatusCode();
        using var overLimit = await UploadAsync(client, transaction, Guid.NewGuid(), ("21.png", TestImages.Png));
        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
        Assert.Contains("at most 20", await overLimit.Content.ReadAsStringAsync());
        Assert.Equal(20, (await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images.Count);
    }

    [Fact]
    public async Task Images_are_isolated_by_budget_and_require_authentication()
    {
        await using var app = await fixture.CreateAppAsync();
        var (alice, _) = await SignedInBudgetAsync(app, "alice");
        var (bob, _) = await SignedInBudgetAsync(app, "bob");
        var transaction = await AddTransactionAsync(alice, await AddAccountAsync(alice), -3);
        var image = Assert.Single((await UploadAsync(alice, transaction, Guid.NewGuid(), ("a.png", TestImages.Png)).ReadAs<TransactionImageListDto>()).Images);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(image.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/transactions/{transaction}/images")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(bob, transaction, Guid.NewGuid(), ("b.png", TestImages.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/transaction-images/{image.Id}/delete", new DeleteTransactionImageRequest(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/transaction-images/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync(image.Url)).StatusCode);

        var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(image.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/transactions/{transaction}/images")).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_image_and_bytes_with_receipt_and_deleted_transactions_hide_images()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, _) = await SignedInBudgetAsync(app, "alice");
        var account = await AddAccountAsync(client);
        var transaction = await AddTransactionAsync(client, account, -3);
        var images = (await UploadAsync(client, transaction, Guid.NewGuid(), ("a.png", TestImages.Png), ("b.gif", TestImages.Gif)).ReadAs<TransactionImageListDto>()).Images;

        var command = new DeleteTransactionImageRequest(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/transaction-images/{images[0].Id}/delete", command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/transaction-images/{images[0].Id}/delete", command)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(images[0].Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/transaction-images/{images[0].Id}/delete", new DeleteTransactionImageRequest(Guid.NewGuid()))).StatusCode);
        Assert.Equal(images[1].Id, Assert.Single((await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images).Id);
        var audit = (await client.GetFromJsonAsync<AuditPageDto>($"/api/ledger/audit?resourceId={transaction}"))!;
        Assert.Single(audit.Items, x => x.Kind == $"transaction.image.delete/{images[0].Id}");
        await using (var db = app.CreateDb())
            Assert.Equal(1, await db.TransactionImageBlobs.IgnoreQueryFilters().CountAsync());

        var version = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;
        (await client.PostAsJsonAsync($"/api/transactions/{transaction}/delete", new DeleteTransactionRequest(Guid.NewGuid(), version))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transactions/{transaction}/images")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(images[1].Url)).StatusCode);
        await using (var db = app.CreateDb())
            Assert.Equal(1, await db.TransactionImages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Reconciled_transactions_accept_image_changes()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, _) = await SignedInBudgetAsync(app, "alice");
        var account = await AddAccountAsync(client);
        var transaction = await AddTransactionAsync(client, account, -3, "cleared");
        var version = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;
        (await client.PostAsJsonAsync($"/api/accounts/{account}/reconcile", new ReconcileRequest(Guid.NewGuid(), version, 97))).EnsureSuccessStatusCode();
        Assert.Equal("reconciled", (await client.GetFromJsonAsync<RegisterDto>("/api/transactions"))!.Items.Single(x => x.Id == transaction).Status);
        var image = Assert.Single((await UploadAsync(client, transaction, Guid.NewGuid(), ("a.png", TestImages.Png)).ReadAs<TransactionImageListDto>()).Images);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/transaction-images/{image.Id}/delete", new DeleteTransactionImageRequest(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Url_import_to_a_loopback_test_server_is_blocked_by_the_ssrf_guard()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var app = await fixture.CreateAppAsync();
        // Use the real fetcher; only the port restriction is relaxed so the local listener is reachable if the guard failed.
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransactionImageUrlFetcher>();
            services.AddSingleton<ITransactionImageUrlFetcher>(new SafeTransactionImageUrlFetcher(new TransactionImageUrlFetcherOptions { AllowNonDefaultPort = true }, NullLogger<SafeTransactionImageUrlFetcher>.Instance));
        }));
        var (client, _) = await SignedInBudgetAsync(guarded, "alice");
        var transaction = await AddTransactionAsync(client, await AddAccountAsync(client), -3);

        foreach (var url in new[] { $"https://localhost:{port}/receipt.png?token=secret", $"https://127.0.0.1:{port}/receipt.png", $"https://[::ffff:127.0.0.1]:{port}/a.png" })
        {
            using var response = await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", new ImportTransactionImageUrlRequest(Guid.NewGuid(), url));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("could not be downloaded", body);
            Assert.DoesNotContain("secret", body); Assert.DoesNotContain("127.0.0.1", body); Assert.DoesNotContain("localhost", body);
        }
        Assert.False(listener.Pending(), "The guard must refuse before any TCP connection reaches the loopback listener.");
        using var http = await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", new ImportTransactionImageUrlRequest(Guid.NewGuid(), $"http://localhost:{port}/a.png"));
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images);
    }

    [Fact]
    public async Task Url_import_through_an_allowed_fetcher_validates_strips_and_stores_the_image()
    {
        var stub = new StubFetcher();
        await using var app = await fixture.CreateAppAsync();
        await using var stubbed = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransactionImageUrlFetcher>();
            services.AddSingleton<ITransactionImageUrlFetcher>(stub);
        }));
        var (client, _) = await SignedInBudgetAsync(stubbed, "alice");
        var transaction = await AddTransactionAsync(client, await AddAccountAsync(client), -3);

        stub.Response = TestImages.Jpeg();
        var command = new ImportTransactionImageUrlRequest(Guid.NewGuid(), "https://images.example.test/receipts/coffee%20shop.jpeg?sig=abc");
        var result = await (await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", command)).ReadAs<TransactionImageListDto>();
        var image = Assert.Single(result.Images);
        Assert.Equal(("coffee shop.jpg", "image/jpeg", "url"), (image.FileName, image.ContentType, image.Source));
        Assert.Equal("https://images.example.test/receipts/coffee%20shop.jpeg?sig=abc", Assert.Single(stub.Requests).AbsoluteUri);
        Assert.False(TestImages.Contains(await client.GetByteArrayAsync(image.Url), "GPSLatitude"));
        // Exact replay does not fetch again.
        (await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", command)).EnsureSuccessStatusCode();
        Assert.Single(stub.Requests);
        var audit = (await client.GetFromJsonAsync<AuditPageDto>($"/api/ledger/audit?resourceId={transaction}"))!;
        Assert.DoesNotContain(audit.Items, x => x.AfterJson.Contains("sig=abc") || x.AfterJson.Contains("example.test"));

        stub.Response = TestImages.Svg;
        using var svg = await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", new ImportTransactionImageUrlRequest(Guid.NewGuid(), "https://images.example.test/logo.svg"));
        Assert.Equal(HttpStatusCode.BadRequest, svg.StatusCode);
        stub.Fail = true;
        using var failed = await client.PostAsJsonAsync($"/api/transactions/{transaction}/images/from-url", new ImportTransactionImageUrlRequest(Guid.NewGuid(), "https://images.example.test/missing.png"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        Assert.DoesNotContain("internal detail", await failed.Content.ReadAsStringAsync());
        Assert.Single((await client.GetFromJsonAsync<TransactionImageListDto>($"/api/transactions/{transaction}/images"))!.Images);
    }

    private sealed class StubFetcher : ITransactionImageUrlFetcher
    {
        public byte[] Response { get; set; } = [];
        public bool Fail { get; set; }
        public List<Uri> Requests { get; } = [];
        public Task<byte[]> FetchAsync(Uri url, long maxBytes, CancellationToken ct)
        {
            Requests.Add(url);
            if (Fail) throw new ImageFetchException("internal detail");
            return Task.FromResult(Response);
        }
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid transaction, Guid commandId, params (string Name, byte[] Content)[] files)
    {
        var form = new MultipartFormDataContent { { new StringContent(commandId.ToString()), "commandId" } };
        foreach (var file in files)
        {
            var content = new ByteArrayContent(file.Content);
            // The client type is deliberately misleading; the server must rely on magic bytes.
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(content, "files", file.Name);
        }
        return client.PostAsync($"/api/transactions/{transaction}/images", form);
    }

    private static async Task<(HttpClient Client, OwnedBudgetDto Budget)> SignedInBudgetAsync(WebApplicationFactory<Program> app, string username)
    {
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var created = await users.CreateAsync(new OpenBudgetUser { UserName = username, Email = $"{username}@example.test", EmailConfirmed = true }, "Strong-pass1!");
            Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(x => x.Code)));
        }
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Strong-pass1!"))).StatusCode);
        var response = await client.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Home"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var budget = (await response.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        return (client, budget);
    }

    private static async Task<Guid> AddAccountAsync(HttpClient client)
    {
        var version = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;
        return (await (await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(Guid.NewGuid(), version, "Checking", Date, 100))).ReadAs<MutationDto>()).Id;
    }

    private static async Task<Guid> AddTransactionAsync(HttpClient client, Guid account, decimal amount, string status = "uncleared")
    {
        var version = (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;
        var request = new TransactionRequest(Guid.NewGuid(), version, account, Date, amount, "Coffee", "", null, status, []);
        return (await (await client.PostAsJsonAsync("/api/transactions", request)).ReadAs<MutationDto>()).Id;
    }
}

internal static class ImageResponseExtensions
{
    public static async Task<T> ReadAs<T>(this Task<HttpResponseMessage> response) => await (await response).ReadAs<T>();
    public static async Task<T> ReadAs<T>(this HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
    }
}
