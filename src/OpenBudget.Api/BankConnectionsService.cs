using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record StartBankConnectionRequest(string Country, string BankName, int SyncIntervalMinutes = 60);
public sealed record MapBankAccountRequest(Guid AccountId);
public sealed record ReviewBankImportRequest(string Action, Guid? MatchedTransactionId = null, bool UseSuggestedPayee = false);
public sealed record BankAccountLinkDto(Guid Id, string Name, string Currency, Guid? AccountId, bool IsAvailable);
public sealed record BankConnectionDto(Guid Id, string BankName, string Country, string Status, DateTimeOffset AccessValidUntil,
    int SyncIntervalMinutes, DateTimeOffset? LastSyncedAt, DateTimeOffset NextSyncAt, string? LastErrorCode, IReadOnlyList<BankAccountLinkDto> Accounts);
public sealed record BankCandidateDto(Guid Id, DateOnly Date, decimal Amount, string Payee, decimal Score);
public sealed record BankImportDto(Guid Id, Guid LinkedAccountId, string AccountName, DateOnly Date, decimal Amount, string Currency,
    string Payee, string Memo, string ProviderStatus, string ReviewStatus, BankCandidateDto? Candidate, string? SuggestedPayee, decimal PayeeSimilarity);
public sealed record BankOverviewDto(IReadOnlyList<BankConnectionDto> Connections, int PendingImportCount, bool ProviderConfigured);

public sealed class BankConnectionsService(
    BudgetDbContext db,
    IEnableBankingClient provider,
    IDataProtectionProvider dataProtection,
    IConfiguration configuration,
    TimeProvider clock)
{
    private const int DefaultSyncIntervalMinutes = 60;
    private const string StatePurpose = "OpenBudget.EnableBanking.State.v1";
    private readonly IDataProtector sessionProtector = dataProtection.CreateProtector("OpenBudget.EnableBanking.Session.v1");
    private readonly IDataProtector stateProtector = dataProtection.CreateProtector(StatePurpose);

    public static void MapEndpoints(WebApplication app)
    {
        var routes = app.MapGroup("/api/banking");
        routes.MapGet("/overview", async (ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverviewAsync(principal, ct))).RequireAuthorization();
        routes.MapGet("/banks", async (string country, ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            Results.Ok(await service.GetBanksAsync(principal, country, ct))).RequireAuthorization();
        routes.MapPost("/connections/start", async (StartBankConnectionRequest request, ClaimsPrincipal principal, HttpContext http, BankConnectionsService service, CancellationToken ct) =>
            Results.Ok(await service.StartAuthorizationAsync(principal, request, http, ct))).RequireAuthorization();
        routes.MapPost("/connections/{id:guid}/reauthorize", async (Guid id, ClaimsPrincipal principal, HttpContext http, BankConnectionsService service, CancellationToken ct) =>
            Results.Ok(await service.ReauthorizeAsync(principal, id, http, ct))).RequireAuthorization();
        routes.MapGet("/callback", async (string? state, string? code, string? error, BankConnectionsService service, CancellationToken ct) =>
            await service.CompleteAuthorizationAsync(state, code, error, ct)).AllowAnonymous();
        routes.MapPut("/accounts/{id:guid}/map", async (Guid id, MapBankAccountRequest request, ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            await service.MapAccountAsync(principal, id, request, ct)).RequireAuthorization();
        routes.MapPut("/connections/{id:guid}/interval", async (Guid id, int minutes, ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            await service.SetIntervalAsync(principal, id, minutes, ct)).RequireAuthorization();
        routes.MapPost("/connections/{id:guid}/sync", async (Guid id, ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            await service.SyncNowAsync(principal, id, ct)).RequireAuthorization();
        routes.MapDelete("/connections/{id:guid}", async (Guid id, ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            await service.DisconnectAsync(principal, id, ct)).RequireAuthorization();
        routes.MapGet("/imports", async (ClaimsPrincipal principal, BankConnectionsService service, CancellationToken ct) =>
            Results.Ok(await service.GetImportsAsync(principal, ct))).RequireAuthorization();
        routes.MapPost("/imports/{id:guid}/review", async (Guid id, ReviewBankImportRequest request, ClaimsPrincipal principal, BankConnectionsService service, LedgerService ledger, CancellationToken ct) =>
            await service.ReviewAsync(principal, id, request, ledger, ct)).RequireAuthorization();
    }

    public async Task<BankOverviewDto> GetOverviewAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var connections = await db.BankConnections.AsNoTracking().Where(x => x.BudgetId == db.ActiveBudgetId && x.OwnerUserId == userId)
            .Include(x => x.Accounts).OrderByDescending(x => x.AuthorizedAt).ToListAsync(ct);
        var pending = await db.BankImports.CountAsync(x => x.BudgetId == db.ActiveBudgetId && x.ReviewStatus == "pending", ct);
        var keyPath = configuration["EnableBanking:PrivateKeyPath"];
        var configured = !string.IsNullOrWhiteSpace(configuration["EnableBanking:ApplicationId"])
            && !string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath);
        return new(connections.Select(ToDto).ToList(), pending, configured);
    }

    public async Task<IReadOnlyList<EnableBank>> GetBanksAsync(ClaimsPrincipal principal, string country, CancellationToken ct)
    {
        await RequireBudgetAccessAsync(principal, ct);
        if (country.Length != 2 || country.Any(ch => !char.IsAsciiLetter(ch))) throw new LedgerException("Country must be a two-letter code.");
        return await provider.GetBanksAsync(country.ToUpperInvariant(), ct);
    }

    public async Task<object> StartAuthorizationAsync(ClaimsPrincipal principal, StartBankConnectionRequest request, HttpContext http, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        ValidateInterval(request.SyncIntervalMinutes);
        if (request.Country.Length != 2 || string.IsNullOrWhiteSpace(request.BankName) || request.BankName.Length > 160)
            throw new LedgerException("Choose a supported country and bank.");
        var country = request.Country.ToUpperInvariant();
        var bank = (await provider.GetBanksAsync(country, ct)).SingleOrDefault(x => string.Equals(x.Name, request.BankName, StringComparison.Ordinal));
        if (bank is null) throw new LedgerException("This bank is not currently supported for that country.");
        return await StartAuthorizationForBankAsync(userId, bank, country, request.SyncIntervalMinutes, null, http, ct);
    }

    public async Task<object> ReauthorizeAsync(ClaimsPrincipal principal, Guid id, HttpContext http, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var connection = await db.BankConnections.SingleOrDefaultAsync(x => x.Id == id && x.BudgetId == db.ActiveBudgetId
            && x.OwnerUserId == userId && x.Status != "disconnected", ct);
        if (connection is null) throw new LedgerException("Bank connection not found or disconnected.", 404);
        var bank = (await provider.GetBanksAsync(connection.Country, ct)).SingleOrDefault(x => string.Equals(x.Name, connection.AspspName, StringComparison.Ordinal));
        if (bank is null) throw new LedgerException("This bank is not currently supported for that country.");
        return await StartAuthorizationForBankAsync(userId, bank, connection.Country, connection.SyncIntervalMinutes, connection.Id, http, ct);
    }

    private async Task<object> StartAuthorizationForBankAsync(string userId, EnableBank bank, string country, int interval, Guid? reauthorizationConnectionId, HttpContext http, CancellationToken ct)
    {
        var callback = configuration["EnableBanking:RedirectUrl"];
        if (!Uri.TryCreate(callback, UriKind.Absolute, out var callbackUri) || callbackUri.Scheme != Uri.UriSchemeHttps && !http.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            throw new InvalidOperationException("EnableBanking:RedirectUrl must be an approved absolute HTTPS callback URL.");
        var now = clock.GetUtcNow();
        var maxConsentSeconds = bank.MaximumConsentValidity > 0 ? Math.Min(bank.MaximumConsentValidity, 90L * 24 * 60 * 60) : 90L * 24 * 60 * 60;
        var validUntil = now.AddSeconds(maxConsentSeconds);
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var stateRow = new BankAuthorizationStateEntity
        {
            State = stateProtector.Protect(state), UserId = userId, BudgetId = db.ActiveBudgetId, AspspName = bank.Name, Country = country,
            SyncIntervalMinutes = interval, ReauthorizationConnectionId = reauthorizationConnectionId, ExpiresAt = now.AddMinutes(15)
        };
        db.BankAuthorizationStates.Add(stateRow);
        await db.SaveChangesAsync(ct);
        try
        {
            var psuId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{db.ActiveBudgetId}"))).ToLowerInvariant();
            var url = await provider.StartAuthorizationAsync(country, bank.Name, state, callback, psuId, validUntil, ct);
            return new { authorizationUrl = url };
        }
        catch
        {
            db.BankAuthorizationStates.Remove(stateRow);
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<IResult> CompleteAuthorizationAsync(string? state, string? code, string? error, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length > 1000 || code?.Length > 2000 || error?.Length > 200)
            return Results.BadRequest(new { error = "Bank authorization could not be verified." });
        var stateRows = await db.BankAuthorizationStates.Where(x => x.ExpiresAt > clock.GetUtcNow()).ToListAsync(ct);
        var stateRow = stateRows.SingleOrDefault(row =>
        {
            try { return stateProtector.Unprotect(row.State) == state; }
            catch (CryptographicException) { return false; }
        });
        if (stateRow is null) return Results.BadRequest(new { error = "Bank authorization expired or was already used. Start again." });
        db.BankAuthorizationStates.Remove(stateRow);
        await db.SaveChangesAsync(ct); // consume the one-time state before exchanging the authorization code
        if (!string.IsNullOrWhiteSpace(error)) return Results.BadRequest(new { error = "Bank authorization was cancelled or declined." });
        if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "Bank authorization did not return a code." });
        var stillAllowed = await db.BudgetPlans.AnyAsync(x => x.Id == stateRow.BudgetId && !x.IsArchived && (x.OwnerUserId == stateRow.UserId
            || db.BudgetMembers.Any(member => member.BudgetId == x.Id && member.UserId == stateRow.UserId)), ct);
        if (!stillAllowed) return Results.BadRequest(new { error = "The selected budget is no longer available." });
        var session = await provider.CreateSessionAsync(code, ct);
        if (session.Accounts.Count == 0)
        {
            await provider.RevokeSessionAsync(session.SessionId, ct);
            return Results.BadRequest(new { error = "The bank did not provide any accounts." });
        }
        var now = clock.GetUtcNow();
        if (stateRow.ReauthorizationConnectionId is { } reauthorizationConnectionId)
        {
            var connection = await db.BankConnections.Include(x => x.Accounts).SingleOrDefaultAsync(x => x.Id == reauthorizationConnectionId
                && x.BudgetId == stateRow.BudgetId && x.OwnerUserId == stateRow.UserId && x.Status != "disconnected", ct);
            if (connection is null)
            {
                await provider.RevokeSessionAsync(session.SessionId, ct);
                return Results.BadRequest(new { error = "The bank connection is no longer available." });
            }
            string? previousSessionId = null;
            try { if (!string.IsNullOrEmpty(connection.ProtectedSessionId)) previousSessionId = sessionProtector.Unprotect(connection.ProtectedSessionId); }
            catch (CryptographicException) { }
            foreach (var linkedAccount in connection.Accounts) linkedAccount.IsAvailable = false;
            foreach (var account in session.Accounts)
            {
                var linkedAccount = string.IsNullOrEmpty(account.IdentificationHash) ? null
                    : connection.Accounts.SingleOrDefault(x => x.IdentificationHash == account.IdentificationHash);
                if (linkedAccount is null)
                {
                    linkedAccount = new LinkedBankAccountEntity { Id = Guid.NewGuid(), BudgetId = connection.BudgetId, ConnectionId = connection.Id };
                    connection.Accounts.Add(linkedAccount);
                }
                linkedAccount.ProviderAccountUid = account.Uid;
                linkedAccount.IdentificationHash = account.IdentificationHash;
                linkedAccount.Name = account.Name;
                linkedAccount.Currency = account.Currency;
                linkedAccount.IsAvailable = true;
            }
            connection.ProtectedSessionId = sessionProtector.Protect(session.SessionId);
            connection.AuthorizedAt = now;
            connection.AccessValidUntil = session.ValidUntil;
            connection.Status = "active";
            connection.NextSyncAt = now;
            connection.RetryAfter = null;
            connection.LastErrorCode = null;
            connection.FailureCount = 0;
            connection.SyncLeaseUntil = null;
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = stateRow.UserId, TargetUserId = stateRow.UserId, Action = "bank.reauthorize" });
            await db.SaveChangesAsync(ct);
            if (previousSessionId is not null)
                try { await provider.RevokeSessionAsync(previousSessionId, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch { }
        }
        else
        {
            var connection = new BankConnectionEntity
            {
                Id = Guid.NewGuid(), BudgetId = stateRow.BudgetId, OwnerUserId = stateRow.UserId,
                AspspName = stateRow.AspspName, Country = stateRow.Country, ProtectedSessionId = sessionProtector.Protect(session.SessionId),
                AuthorizedAt = now, AccessValidUntil = session.ValidUntil, Status = "active", SyncIntervalMinutes = stateRow.SyncIntervalMinutes,
                NextSyncAt = now, Accounts = session.Accounts.Select(account => new LinkedBankAccountEntity
                {
                    Id = Guid.NewGuid(), BudgetId = stateRow.BudgetId, ProviderAccountUid = account.Uid,
                    IdentificationHash = account.IdentificationHash, Name = account.Name, Currency = account.Currency
                }).ToList()
            };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.BankConnections.Add(connection);
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = stateRow.UserId, TargetUserId = stateRow.UserId, Action = "bank.connect" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return Results.Redirect(configuration["EnableBanking:CompletionRedirectUrl"] ?? "/?bankConnected=1");
    }

    public async Task<IResult> MapAccountAsync(ClaimsPrincipal principal, Guid linkedAccountId, MapBankAccountRequest request, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var linked = await db.LinkedBankAccounts.Include(x => x.Connection)
            .SingleOrDefaultAsync(x => x.Id == linkedAccountId && x.BudgetId == db.ActiveBudgetId && x.IsAvailable && x.Connection.OwnerUserId == userId, ct);
        if (linked is null) return Results.NotFound();
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.BudgetId == db.ActiveBudgetId && !x.IsClosed, ct);
        if (account is null) return Results.NotFound();
        if (await db.LinkedBankAccounts.AnyAsync(x => x.BudgetId == db.ActiveBudgetId && x.AccountId == account.Id && x.Id != linked.Id, ct))
            return Results.Conflict(new { error = "This OpenBudget account is already linked to a bank account." });
        linked.AccountId = account.Id;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(await db.BankConnections.Include(x => x.Accounts).SingleAsync(x => x.Id == linked.ConnectionId && x.OwnerUserId == userId, ct)));
    }

    public async Task<IResult> SetIntervalAsync(ClaimsPrincipal principal, Guid id, int minutes, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        ValidateInterval(minutes);
        var connection = await db.BankConnections.SingleOrDefaultAsync(x => x.Id == id && x.BudgetId == db.ActiveBudgetId && x.OwnerUserId == userId, ct);
        if (connection is null) return Results.NotFound();
        connection.SyncIntervalMinutes = minutes;
        connection.NextSyncAt = clock.GetUtcNow().AddMinutes(minutes);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(await db.BankConnections.Include(x => x.Accounts).SingleAsync(x => x.Id == id, ct)));
    }

    public async Task<IResult> SyncNowAsync(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var owned = await db.BankConnections.AnyAsync(x => x.Id == id && x.BudgetId == db.ActiveBudgetId && x.OwnerUserId == userId && x.Status == "active", ct);
        if (!owned) return Results.NotFound();
        await SyncConnectionAsync(id, ct, userRequested: true);
        return Results.Ok(await db.BankConnections.AsNoTracking().Where(x => x.Id == id).Include(x => x.Accounts).Select(x => ToDto(x)).SingleAsync(ct));
    }

    public async Task<IResult> DisconnectAsync(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var connection = await db.BankConnections.SingleOrDefaultAsync(x => x.Id == id && x.BudgetId == db.ActiveBudgetId && x.OwnerUserId == userId, ct);
        if (connection is null) return Results.NotFound();
        string? sessionId = null;
        try { if (!string.IsNullOrEmpty(connection.ProtectedSessionId)) sessionId = sessionProtector.Unprotect(connection.ProtectedSessionId); }
        catch (CryptographicException) { /* clear local access even when a lost key prevents provider revocation */ }
        connection.Status = "disconnected";
        connection.ProtectedSessionId = "";
        connection.SyncLeaseUntil = null;
        connection.RetryAfter = null;
        connection.NextSyncAt = DateTimeOffset.MaxValue;
        db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = userId, TargetUserId = userId, Action = "bank.disconnect" });
        await db.SaveChangesAsync(ct);
        try { if (sessionId is not null) await provider.RevokeSessionAsync(sessionId, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch { /* local disconnect prevents future sync even if revocation is temporarily unavailable */ }
        return Results.NoContent();
    }

    public async Task<IReadOnlyList<BankImportDto>> GetImportsAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        await RequireBudgetAccessAsync(principal, ct);
        var rows = await db.BankImports.AsNoTracking().Include(x => x.LinkedBankAccount)
            .Where(x => x.BudgetId == db.ActiveBudgetId && x.ReviewStatus == "pending")
            .OrderByDescending(x => x.Date).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        var candidateIds = rows.Where(x => x.MatchedTransactionId != null).Select(x => x.MatchedTransactionId!.Value).Distinct().ToArray();
        var candidates = await db.Transactions.Include(x => x.Payee).Where(x => candidateIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, ct);
        var payees = await db.Payees.AsNoTracking().Where(x => x.BudgetId == db.ActiveBudgetId && !x.IsHidden).ToListAsync(ct);
        return rows.Select(row =>
        {
            var candidate = row.MatchedTransactionId is not null && candidates.TryGetValue(row.MatchedTransactionId.Value, out var existing)
                ? new BankCandidateDto(existing.Id, existing.Date, existing.Amount, existing.Payee?.Name ?? "", row.MatchScore ?? 0) : null;
            var suggested = payees.Select(payee => new { Payee = payee, Score = BankMatchingRules.PayeeSimilarity(row.Payee, payee.Name) })
                .OrderByDescending(x => x.Score).FirstOrDefault();
            return new BankImportDto(row.Id, row.LinkedBankAccountId, row.LinkedBankAccount.Name, row.Date, row.Amount, row.Currency,
                row.Payee, row.Memo, row.ProviderStatus, row.ReviewStatus, candidate,
                suggested is { Score: >= 0.72m } ? suggested.Payee.Name : null, suggested?.Score ?? 0);
        }).ToList();
    }

    public async Task<IResult> ReviewAsync(ClaimsPrincipal principal, Guid id, ReviewBankImportRequest request, LedgerService ledger, CancellationToken ct)
    {
        var userId = await RequireBudgetAccessAsync(principal, ct);
        var row = await db.BankImports.Include(x => x.LinkedBankAccount).SingleOrDefaultAsync(x => x.Id == id && x.BudgetId == db.ActiveBudgetId, ct);
        if (row is null) return Results.NotFound();
        if (row.ReviewStatus != "pending") return Results.Conflict(new { error = "This bank item was already reviewed." });
        if (request.Action == "verify-match")
        {
            if (request.MatchedTransactionId != row.MatchedTransactionId || request.MatchedTransactionId is null) return Results.BadRequest(new { error = "Choose the suggested existing transaction to verify." });
            var exists = await db.Transactions.AnyAsync(x => x.Id == request.MatchedTransactionId && x.BudgetId == db.ActiveBudgetId
                && x.AccountId == row.LinkedBankAccount.AccountId && !x.IsDeleted && x.TransferId == null && !x.IsOpening, ct);
            if (!exists) return Results.Conflict(new { error = "The suggested transaction is no longer available." });
            row.ReviewStatus = "matched";
            row.UpdatedAt = clock.GetUtcNow();
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = userId, TargetUserId = userId, Action = "bank.match.verify" });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        if (request.Action == "dismiss")
        {
            row.ReviewStatus = "dismissed";
            row.UpdatedAt = clock.GetUtcNow();
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = userId, TargetUserId = userId, Action = "bank.import.dismiss" });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        if (request.Action != "accept" || row.LinkedBankAccount.AccountId is null)
            return Results.BadRequest(new { error = "Map this bank account to an OpenBudget account before importing transactions." });
        var version = await db.LedgerStates.Select(x => x.Version).SingleAsync(ct);
        var payee = row.Payee;
        if (request.UseSuggestedPayee)
        {
            var existingPayees = await db.Payees.AsNoTracking().Where(x => x.BudgetId == db.ActiveBudgetId && !x.IsHidden).ToListAsync(ct);
            var suggested = existingPayees.Select(x => new { Payee = x, Score = BankMatchingRules.PayeeSimilarity(row.Payee, x.Name) })
                .OrderByDescending(x => x.Score).FirstOrDefault();
            if (suggested is null || suggested.Score < 0.72m) return Results.BadRequest(new { error = "There is no sufficiently similar payee to reuse." });
            payee = suggested.Payee.Name;
        }
        var transaction = new TransactionRequest(Guid.NewGuid(), version, row.LinkedBankAccount.AccountId.Value, row.Date, row.Amount,
            payee, row.Memo, null, "uncleared", null);
        await ledger.SaveTransactionAsync(null, transaction, ct, imported: true, bankImportId: row.Id);
        return Results.NoContent();
    }

    public async Task<int> SyncDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.BankConnections.Where(x => x.Status == "active" && x.AccessValidUntil <= now)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "reauthorization_required")
                .SetProperty(x => x.LastErrorCode, "consent_expired"), ct);
        var ids = await db.BankConnections.Where(x => x.Status == "active" && x.NextSyncAt <= now && x.AccessValidUntil > now
                && x.Accounts.Any(account => account.IsAvailable && account.AccountId != null)
                && (x.SyncLeaseUntil == null || x.SyncLeaseUntil < now) && (x.RetryAfter == null || x.RetryAfter <= now))
            .OrderBy(x => x.NextSyncAt).Select(x => x.Id).Take(10).ToListAsync(ct);
        var synced = 0;
        foreach (var id in ids)
        {
            try { await SyncConnectionAsync(id, ct, userRequested: false); synced++; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { /* failure and retry state are recorded by SyncConnectionAsync */ }
        }
        return synced;
    }

    private async Task SyncConnectionAsync(Guid id, CancellationToken ct, bool userRequested)
    {
        var now = clock.GetUtcNow();
        var claimed = await db.BankConnections.Where(x => x.Id == id && x.Status == "active" && x.AccessValidUntil > now
            && (x.SyncLeaseUntil == null || x.SyncLeaseUntil < now))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SyncLeaseUntil, now.AddMinutes(15)), ct);
        if (claimed == 0) throw new LedgerException("The bank connection is busy or requires reconnection.", 409);
        try
        {
            db.ChangeTracker.Clear();
            var connection = await db.BankConnections.Include(x => x.Accounts).SingleAsync(x => x.Id == id, ct);
            if (!connection.Accounts.Any(x => x.AccountId is not null && x.IsAvailable)) throw new LedgerException("Map at least one bank account before syncing.");
            var sessionId = sessionProtector.Unprotect(connection.ProtectedSessionId);
            var from = connection.LastImportedDate?.AddDays(-5) ?? DateOnly.FromDateTime(now.UtcDateTime).AddDays(-90);
            DateOnly? latest = connection.LastImportedDate;
            foreach (var account in connection.Accounts.Where(x => x.AccountId is not null && x.IsAvailable))
            {
                string? continuation = null;
                var pages = 0;
                do
                {
                    var page = await provider.GetTransactionsAsync(sessionId, account.ProviderAccountUid, from, continuation, ct);
                    foreach (var transaction in page.Transactions)
                    {
                        if (latest is null || transaction.Date > latest) latest = transaction.Date;
                        await UpsertImportAsync(connection, account, transaction, ct);
                    }
                    await db.SaveChangesAsync(ct);
                    continuation = page.ContinuationKey;
                    if (++pages > 1000) throw new InvalidOperationException("Enable Banking returned too many transaction pages.");
                } while (!string.IsNullOrWhiteSpace(continuation));
            }
            connection.LastSyncedAt = clock.GetUtcNow();
            connection.LastImportedDate = latest;
            connection.NextSyncAt = clock.GetUtcNow().AddMinutes(connection.SyncIntervalMinutes);
            connection.RetryAfter = null;
            connection.LastErrorCode = null;
            connection.SyncLeaseUntil = null;
            connection.FailureCount = 0;
            await db.SaveChangesAsync(ct);
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = connection.OwnerUserId, TargetUserId = connection.OwnerUserId, Action = "bank.sync" });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception error)
        {
            db.ChangeTracker.Clear();
            var connection = await db.BankConnections.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (connection is not null)
            {
                var requiresConsent = error is EnableBankingProviderException { StatusCode: 401 or 403 or 404 or 410 };
                connection.FailureCount++;
                var delayMinutes = Math.Min(360, Math.Pow(2, Math.Min(connection.FailureCount, 8)));
                var retryAfter = requiresConsent ? (DateTimeOffset?)null : clock.GetUtcNow().AddMinutes(delayMinutes);
                connection.RetryAfter = retryAfter;
                connection.NextSyncAt = retryAfter ?? DateTimeOffset.MaxValue;
                connection.LastErrorCode = requiresConsent ? "consent_revoked" : error is LedgerException ? "account_mapping_required" : error is EnableBankingProviderException { StatusCode: 429 } ? "provider_rate_limited" : error.GetType().Name;
                connection.SyncLeaseUntil = null;
                if (requiresConsent || connection.AccessValidUntil <= clock.GetUtcNow()) connection.Status = "reauthorization_required";
                await db.SaveChangesAsync(ct);
            }
            if (userRequested) throw new LedgerException("Sync failed. Check the connection status and retry later.", 503);
        }
    }

    private async Task UpsertImportAsync(BankConnectionEntity connection, LinkedBankAccountEntity account, EnableTransaction transaction, CancellationToken ct)
    {
        var existing = await db.BankImports.SingleOrDefaultAsync(x => x.LinkedBankAccountId == account.Id && x.ProviderTransactionId == transaction.TransactionId, ct);
        if (existing is null && !string.IsNullOrWhiteSpace(transaction.EntryReference))
            existing = await db.BankImports.SingleOrDefaultAsync(x => x.LinkedBankAccountId == account.Id && x.EntryReference == transaction.EntryReference, ct);
        if (existing is not null)
        {
            if (existing.ReviewStatus == "pending")
            {
                existing.ProviderStatus = transaction.ProviderStatus;
                existing.Date = transaction.Date;
                existing.Amount = transaction.Amount;
                existing.Currency = transaction.Currency;
                existing.Payee = transaction.Payee;
                existing.Memo = transaction.Memo;
                existing.UpdatedAt = clock.GetUtcNow();
            }
            return;
        }
        var candidateRows = await db.Transactions.IgnoreQueryFilters().Include(x => x.Payee).Where(x => x.BudgetId == connection.BudgetId
            && x.AccountId == account.AccountId && !x.IsDeleted && !x.IsOpening && x.TransferId == null && x.BankImportId == null).ToListAsync(ct);
        var candidate = candidateRows.Select(x => new
        {
            Entry = x,
            Score = BankMatchingRules.CandidateScore(transaction.Date, transaction.Amount, transaction.Payee, x.Date, x.Amount, x.Payee?.Name ?? "")
        }).Where(x => x.Score >= 0.76m).OrderByDescending(x => x.Score).ThenBy(x => Math.Abs(x.Entry.Date.DayNumber - transaction.Date.DayNumber)).FirstOrDefault();
        db.BankImports.Add(new BankImportEntity
        {
            Id = Guid.NewGuid(), BudgetId = connection.BudgetId, LinkedBankAccountId = account.Id,
            ProviderTransactionId = transaction.TransactionId, EntryReference = transaction.EntryReference, ProviderStatus = transaction.ProviderStatus,
            Date = transaction.Date, Amount = transaction.Amount, Currency = transaction.Currency, Payee = transaction.Payee,
            Memo = transaction.Memo, ReviewStatus = "pending", MatchedTransactionId = candidate?.Entry.Id, MatchScore = candidate?.Score,
            ImportedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow()
        });
    }

    private async Task<string> RequireBudgetAccessAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) throw new LedgerException("Sign in required.", 401);
        if (db.ActiveBudgetId == BudgetRequestContext.DefaultBudgetId) throw new LedgerException("Select an owned budget before linking bank accounts.", 403);
        var access = await db.BudgetPlans.AnyAsync(x => x.Id == db.ActiveBudgetId && !x.IsArchived && (x.OwnerUserId == userId
            || db.BudgetMembers.Any(member => member.BudgetId == x.Id && member.UserId == userId)), ct);
        if (!access) throw new LedgerException("Budget not found.", 404);
        return userId;
    }

    private static void ValidateInterval(int minutes)
    {
        if (minutes is < 15 or > 1440) throw new LedgerException("Sync interval must be between 15 minutes and 24 hours.");
    }

    private static BankConnectionDto ToDto(BankConnectionEntity connection) => new(connection.Id, connection.AspspName, connection.Country,
        connection.Status, connection.AccessValidUntil, connection.SyncIntervalMinutes, connection.LastSyncedAt, connection.NextSyncAt,
        connection.LastErrorCode, connection.Accounts.Select(x => new BankAccountLinkDto(x.Id, x.Name, x.Currency, x.AccountId, x.IsAvailable)).ToList());
}

public sealed class BankSyncWorker(IServiceScopeFactory scopes, ILogger<BankSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<BankConnectionsService>().SyncDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Bank sync worker iteration failed ({ErrorType}).", error.GetType().Name); }
        }
    }
}
