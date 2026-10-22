using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class LedgerApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    [Fact]
    public async Task Opening_balance_rename_restart_and_command_replay_are_persistent()
    {
        await using var app = await fixture.CreateAppAsync();
        var client = app.CreateClient();
        var request = new CreateAccountRequest(Guid.NewGuid(), 1, "Checking", Date, 123.45m);
        var first = await Post<MutationDto>(client, "/api/accounts", request);
        var replay = await Post<MutationDto>(client, "/api/accounts", request);
        Assert.Equal(first, replay);
        var accounts = await Accounts(client);
        Assert.Equal(123.45m, Assert.Single(accounts.Accounts).Balance);
        var register = await Register(client);
        Assert.True(Assert.Single(register.Items).IsOpening);
        Assert.Equal(123.45m, (await Budget(client)).ReadyToAssign);
        await Put<MutationDto>(client, $"/api/accounts/{first.Id}", new UpdateAccountRequest(Guid.NewGuid(), accounts.Version, "Main cash", false));
        await using var restarted = new TestApp(app.ConnectionString);
        var afterRestart = await Accounts(restarted.CreateClient());
        Assert.Equal("Main cash", Assert.Single(afterRestart.Accounts).Name);
        Assert.Equal(123.45m, afterRestart.Accounts[0].Balance);
        await using var db = app.CreateDb();
        Assert.Equal(2, await db.LedgerCommands.CountAsync());
        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Categorized_spending_income_refunds_splits_edits_and_deletes_reconcile()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 1000);
        var spend = await AddTransaction(client, account, -30, "groceries", "Shop");
        await AddTransaction(client, account, 200, null, "Salary");
        await AddTransaction(client, account, 5, "groceries", "Shop");
        var splitRequest = (await Request(client, account, -20)) with { Splits = [new("groceries", -12), new("rent", -8)] };
        var split = await Post<MutationDto>(client, "/api/transactions", splitRequest);
        Assert.Equal(1155, (await Accounts(client)).Accounts[0].Balance);
        var budget = await Budget(client);
        Assert.Equal(1200, budget.ReadyToAssign);
        Assert.Equal(-37, Category(budget, "groceries").Activity);
        Assert.Equal(-8, Category(budget, "rent").Activity);

        // Change both the amount and the split layout; persisted split keys must remain valid.
        await Put<MutationDto>(client, $"/api/transactions/{split.Id}", splitRequest with { CommandId = Guid.NewGuid(), Version = (await Accounts(client)).Version, Splits = [new("groceries", -20)] });
        await Put<MutationDto>(client, $"/api/transactions/{spend.Id}", await Request(client, account, -40, "rent", "Shop"));
        budget = await Budget(client);
        Assert.Equal(-15, Category(budget, "groceries").Activity);
        Assert.Equal(-40, Category(budget, "rent").Activity);
        await Delete(client, split.Id);
        budget = await Budget(client);
        Assert.Equal(5, Category(budget, "groceries").Activity);
        Assert.Equal(-40, Category(budget, "rent").Available);
        Assert.Equal(1165, (await Accounts(client)).Accounts[0].Balance);
        await using var db = app.CreateDb();
        Assert.True((await db.Transactions.SingleAsync(t => t.Id == split.Id)).IsDeleted);
        var history = await client.GetFromJsonAsync<AuditPageDto>($"/api/ledger/audit?resourceId={split.Id}");
        Assert.Equal(3, history!.Total);
        Assert.Contains(history.Items, item => item.Kind.StartsWith("transaction.update") && item.BeforeJson.Contains("-12") && item.AfterJson.Contains("-20"));
        Assert.Equal(2, await db.Payees.CountAsync());
    }

    [Fact]
    public async Task Uncategorized_spending_reduces_ready_to_assign_until_categorized()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        var entry = await AddTransaction(client, account, -25);
        Assert.Equal(75, (await Budget(client)).ReadyToAssign);
        var register = await Register(client, "categoryId=uncategorized");
        Assert.True(Assert.Single(register.Items).NeedsAttention);
        await Put<MutationDto>(client, $"/api/transactions/{entry.Id}", await Request(client, account, -25, "groceries"));
        Assert.Equal(100, (await Budget(client)).ReadyToAssign);
        Assert.Equal(-25, Category(await Budget(client), "groceries").Available);
        Assert.Empty((await Register(client, "categoryId=uncategorized")).Items);
    }

    [Fact]
    public async Task Transfers_are_paired_atomic_neutral_editable_deletable_and_idempotent()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var from = await AddAccount(client, 100); var to = await AddAccount(client, 0, "Savings");
        var request = new TransferRequest(Guid.NewGuid(), (await Accounts(client)).Version, from, to, Date, 30, "Move funds");
        var transfer = await Post<MutationDto>(client, "/api/transfers", request);
        Assert.Equal(transfer, await Post<MutationDto>(client, "/api/transfers", request));
        var pair = await Register(client, $"transferId={transfer.Id}");
        Assert.Equal(2, pair.Total); Assert.Equal(0, pair.Items.Sum(t => t.Amount));
        var accounts = await Accounts(client);
        Assert.Equal(70, accounts.Accounts.Single(a => a.Id == from).Balance);
        Assert.Equal(30, accounts.Accounts.Single(a => a.Id == to).Balance);
        Assert.Equal(100, (await Budget(client)).ReadyToAssign);
        Assert.All((await Budget(client)).Groups.SelectMany(g => g.Categories), c => Assert.Equal(0, c.Activity));
        await Put<MutationDto>(client, $"/api/transfers/{pair.Items.Single(t => t.Amount > 0).Id}", request with { CommandId = Guid.NewGuid(), Version = accounts.Version, Amount = 45 });
        Assert.Equal(55, (await Accounts(client)).Accounts.Single(a => a.Id == from).Balance);
        await Delete(client, transfer.Id);
        Assert.Empty((await Register(client, $"transferId={transfer.Id}")).Items);
        Assert.Equal(100, (await Accounts(client)).Accounts.Single(a => a.Id == from).Balance);
    }

    [Fact]
    public async Task Historical_edits_propagate_into_existing_later_months_and_survive_restart()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        var spend = await AddTransaction(client, account, -10, "groceries");
        var next = await client.GetFromJsonAsync<BudgetDto>("/api/budget/2027-01");
        Assert.Equal(-10, Category(next!, "groceries").Available);
        await Put<MutationDto>(client, $"/api/transactions/{spend.Id}", await Request(client, account, -25, "groceries"));
        await AddTransaction(client, account, 50);
        await using var restarted = new TestApp(app.ConnectionString);
        var read = restarted.CreateClient();
        next = await read.GetFromJsonAsync<BudgetDto>("/api/budget/2027-01");
        Assert.Equal(-25, Category(next!, "groceries").Available);
        Assert.Equal(150, next!.ReadyToAssign);
        Assert.Equal(125, (await Accounts(read)).Accounts[0].Balance);
        // Replaying reads must not reapply carry-forward deltas.
        Assert.Equivalent(next, await Get<BudgetDto>(read, "/api/budget/2027-01"), strict: true);
    }

    [Fact]
    public async Task Moving_a_transaction_between_months_and_accounts_reverses_original_activity()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100); var other = await AddAccount(client, 0, "Other");
        var spend = await AddTransaction(client, account, -10, "groceries");
        await Put<MutationDto>(client, $"/api/transactions/{spend.Id}", (await Request(client, other, -15, "rent")) with { Date = new(2026, 11, 1) });
        Assert.Equal(0, Category(await Budget(client), "groceries").Activity);
        var november = (await client.GetFromJsonAsync<BudgetDto>("/api/budget/2026-11"))!;
        Assert.Equal(-15, Category(november, "rent").Activity);
        Assert.Equal(0, Category(november, "groceries").Available);
        var accounts = await Accounts(client);
        Assert.Equal(100, accounts.Accounts.Single(a => a.Id == account).Balance);
        Assert.Equal(-15, accounts.Accounts.Single(a => a.Id == other).Balance);
    }

    [Fact]
    public async Task Reconciliation_requires_zero_difference_and_locks_history()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        var spend = await AddTransaction(client, account, -20, "groceries");
        var invalid = await client.PostAsJsonAsync($"/api/accounts/{account}/reconcile", new ReconcileRequest(Guid.NewGuid(), (await Accounts(client)).Version, 80));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await Post<MutationDto>(client, $"/api/transactions/{spend.Id}/status", new StatusRequest(Guid.NewGuid(), (await Accounts(client)).Version, "cleared"));
        await Post<MutationDto>(client, $"/api/accounts/{account}/reconcile", new ReconcileRequest(Guid.NewGuid(), (await Accounts(client)).Version, 80));
        Assert.All((await Register(client)).Items, t => Assert.Equal("reconciled", t.Status));
        var blocked = await client.PutAsJsonAsync($"/api/transactions/{spend.Id}", await Request(client, account, -21, "groceries"));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        blocked = await client.PostAsJsonAsync($"/api/transactions/{spend.Id}/delete", new DeleteTransactionRequest(Guid.NewGuid(), (await Accounts(client)).Version));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal(80, (await db.Reconciliations.SingleAsync()).StatementBalance);
        Assert.Equal(80, (await Accounts(client)).Accounts[0].ClearedBalance);
    }

    [Fact]
    public async Task Closing_requires_zero_resolved_balance_preserves_history_and_allows_reopening()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 10);
        var close = new UpdateAccountRequest(Guid.NewGuid(), (await Accounts(client)).Version, "Cash", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{account}", close)).StatusCode);
        var spend = await AddTransaction(client, account, -10, "groceries");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{account}", close with { Version = (await Accounts(client)).Version })).StatusCode);
        await Post<MutationDto>(client, $"/api/transactions/{spend.Id}/status", new StatusRequest(Guid.NewGuid(), (await Accounts(client)).Version, "cleared"));
        await Put<MutationDto>(client, $"/api/accounts/{account}", close with { Version = (await Accounts(client)).Version });
        Assert.True((await Accounts(client)).Accounts[0].IsClosed);
        Assert.Equal(2, (await Register(client)).Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", await Request(client, account, 1))).StatusCode);
        await Put<MutationDto>(client, $"/api/accounts/{account}", close with { CommandId = Guid.NewGuid(), Version = (await Accounts(client)).Version, IsClosed = false });
        Assert.False((await Accounts(client)).Accounts[0].IsClosed);
    }

    [Fact]
    public async Task Combined_filters_and_pagination_are_stable_include_splits_and_distinguish_empty_results()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        for (var i = 0; i < 4; i++) await AddTransaction(client, account, -i - 1, "groceries", "Market");
        await AddTransaction(client, account, -5, "rent", "Landlord");
        await Post<MutationDto>(client, "/api/transactions", (await Request(client, account, -6, payee: "Split shop")) with { Splits = [new("groceries", -4), new("rent", -2)] });
        var splitFiltered = await Register(client, $"accountId={account}&search=split&categoryId=rent&status=uncleared");
        Assert.Equal(2, Assert.Single(splitFiltered.Items).Splits.Count);
        var filter = $"accountId={account}&search=market&from=2026-10-01&to=2026-10-31&categoryId=groceries&status=uncleared&pageSize=2";
        var first = await Register(client, filter + "&page=1"); var second = await Register(client, filter + "&page=2");
        Assert.Equal(4, first.Total); Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(t => t.Id).Intersect(second.Items.Select(t => t.Id)));
        Assert.Equal(first.Items.Select(t => t.Id), (await Register(client, filter + "&page=1")).Items.Select(t => t.Id));
        Assert.Empty((await Register(client, filter + "&page=3")).Items);
        Assert.Empty((await Register(client, filter.Replace("search=market", "search=no-match"))).Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/transactions?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/transactions?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/transactions?from=2026-11-01&to=2026-10-01")).StatusCode);
    }

    [Fact]
    public async Task Invalid_requests_do_not_change_balance_budget_version_or_audit()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100); var before = await Budget(client);
        var valid = await Request(client, account, -10);
        TransactionRequest[] invalid = [valid with { Amount = -1.001m }, valid with { CategoryId = "unknown" }, valid with { Date = new(1899, 1, 1) },
            valid with { Splits = [new("rent", -9)] }, valid with { AccountId = Guid.NewGuid() }, valid with { Status = "reconciled" },
            valid with { Splits = [new("rent", -5), new("unknown", -5)] }];
        foreach (var request in invalid)
        {
            var response = await client.PostAsJsonAsync("/api/transactions", request);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(100, (await Accounts(client)).Accounts[0].Balance);
        Assert.Equal(before.Version, (await Budget(client)).Version);
        await using var db = app.CreateDb();
        Assert.Equal(1, await db.LedgerCommands.CountAsync());
        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Concurrent_writes_and_reused_command_ids_return_conflict_without_lost_edits()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        var first = await Request(client, account, -1, "groceries");
        var second = first with { CommandId = Guid.NewGuid(), Amount = -2 };
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/transactions", first), client.PostAsJsonAsync("/api/transactions", second));
        Assert.Single(results, r => r.IsSuccessStatusCode); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = results[0].IsSuccessStatusCode ? first : second;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/transactions", winner with { Amount = -7 })).StatusCode);
        Assert.Equal(2, (await Register(client)).Total);
        // A transaction changes month version, so a stale Plan save cannot overwrite it.
        var old = await Budget(client);
        await AddTransaction(client, account, -3, "rent");
        var response = await client.PutAsJsonAsync("/api/budget/2026-10", Editable(old));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(-3, Category(await Budget(client), "rent").Activity);
    }

    [Fact]
    public async Task Simulated_persistence_failure_rolls_back_both_transfer_legs_budget_and_receipt()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var from = await AddAccount(client, 100); var to = await AddAccount(client, 0, "Savings");
        var before = await Accounts(client); var budget = await Budget(client);
        app.Failure.FailAuditSave = true;
        var request = new TransferRequest(Guid.NewGuid(), before.Version, from, to, Date, 25, "Atomic transfer");
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsJsonAsync("/api/transfers", request));
        Assert.Equal(before.Version, (await Accounts(client)).Version);
        Assert.Equal(100, (await Accounts(client)).Accounts.Single(a => a.Id == from).Balance);
        Assert.Equal(2, (await Register(client)).Total);
        Assert.Equal(budget.Version, (await Budget(client)).Version);
        await using var db = app.CreateDb(); Assert.False(await db.LedgerCommands.AnyAsync(c => c.Id == request.CommandId));
        await Post<MutationDto>(client, "/api/transfers", request);
        Assert.Equal(4, (await Register(client)).Total);
    }

    [Fact]
    public async Task Upgrade_preserves_legacy_activity_and_available_values()
    {
        await using var app = await fixture.CreateAppAsync();
        await using (var db = app.CreateDb())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261021145927_NamedBudget");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Budgets" ("Month", "BudgetId", "Note", "OpeningReadyToAssign", "FundsAdded", "Version") VALUES ('2026-10', 'default', '', 0, 100, 1);
                INSERT INTO "CategoryGroups" ("BudgetMonth", "Id", "Name", "SortOrder", "IsArchived") VALUES ('2026-10', 'daily', 'Daily', 0, false);
                INSERT INTO "Categories" ("BudgetMonth", "GroupId", "Id", "Name", "OpeningAvailable", "Assigned", "Activity", "Target", "Snoozed", "SortOrder", "IsArchived") VALUES ('2026-10', 'daily', 'groceries', 'Food', 20, 100, -30, 0, false, 0, false);
                """);
        }
        var client = app.CreateClient();
        var account = await AddAccount(client, 0);
        await AddTransaction(client, account, -5, "groceries");
        var category = Category(await Budget(client), "groceries");
        Assert.Equal(-35, category.Activity); Assert.Equal(85, category.Available);
        await using var check = app.CreateDb();
        Assert.Equal(-30, (await check.Categories.SingleAsync()).LegacyActivity);
    }

    [Fact]
    public async Task Reconciled_transfer_cannot_be_edited_or_deleted_through_the_other_leg()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var from = await AddAccount(client, 100); var to = await AddAccount(client, 0, "Savings");
        var transfer = await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), (await Accounts(client)).Version, from, to, Date, 20, ""));
        var pair = (await Register(client, $"transferId={transfer.Id}")).Items;
        var outgoing = pair.Single(t => t.Amount < 0); var incoming = pair.Single(t => t.Amount > 0);
        await Post<MutationDto>(client, $"/api/transactions/{outgoing.Id}/status", new StatusRequest(Guid.NewGuid(), (await Accounts(client)).Version, "cleared"));
        await Post<MutationDto>(client, $"/api/accounts/{from}/reconcile", new ReconcileRequest(Guid.NewGuid(), (await Accounts(client)).Version, 80));
        Assert.True((await Register(client, $"transferId={transfer.Id}")).Items.Single(t => t.Id == incoming.Id).IsLocked);
        var delete = await client.PostAsJsonAsync($"/api/transactions/{incoming.Id}/delete", new DeleteTransactionRequest(Guid.NewGuid(), (await Accounts(client)).Version));
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        var edit = await client.PutAsJsonAsync($"/api/transfers/{incoming.Id}", new TransferRequest(Guid.NewGuid(), (await Accounts(client)).Version, from, to, Date, 10, ""));
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(2, (await Register(client, $"transferId={transfer.Id}")).Total);
    }

    [Fact]
    public async Task Simulated_spending_failure_rolls_back_activity_income_and_future_carry_forward()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        var future = await Get<BudgetDto>(client, "/api/budget/2027-01");
        app.Failure.FailAuditSave = true;
        var request = await Request(client, account, -25, "groceries");
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsJsonAsync("/api/transactions", request));
        Assert.Equal(0, Category(await Budget(client), "groceries").Activity);
        Assert.Equivalent(future, await Get<BudgetDto>(client, "/api/budget/2027-01"), strict: true);
        Assert.Equal(100, (await Accounts(client)).Accounts[0].Balance);
        await Post<MutationDto>(client, "/api/transactions", request);
        Assert.Equal(-25, Category(await Get<BudgetDto>(client, "/api/budget/2027-01"), "groceries").Available);
    }

    [Fact]
    public async Task Plan_cannot_fabricate_income_or_change_server_owned_activity()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100); await AddTransaction(client, account, -10, "groceries");
        var before = await Budget(client);
        var fabricated = Editable(before) with { FundsAdded = 999 };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/budget/2026-10", fabricated)).StatusCode);
        var saved = await Put<BudgetDto>(client, "/api/budget/2026-10", Editable(before) with { Note = "Preserve ledger" });
        Assert.Equal(100, saved.ReadyToAssign); Assert.Equal(-10, Category(saved, "groceries").Activity);
        Assert.Equal(-10, Category(saved, "groceries").Available);
    }

    [Fact]
    public async Task Archived_categories_invalid_transfers_and_missing_commands_do_not_write()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100); var budget = await Budget(client);
        var editable = Editable(budget);
        var groups = editable.Groups.Select(g => g with { Categories = g.Categories.Select(c => c.Id == "groceries" ? c with { IsArchived = true } : c).ToList() }).ToList();
        await Put<BudgetDto>(client, "/api/budget/2026-10", editable with { Groups = groups });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", await Request(client, account, -10, "groceries"))).StatusCode);
        var version = (await Accounts(client)).Version;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transfers", new TransferRequest(Guid.NewGuid(), version, account, account, Date, 10, ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", (await Request(client, account, 10)) with { CommandId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/transactions/{Guid.NewGuid()}/status", new StatusRequest(Guid.NewGuid(), version, "cleared"))).StatusCode);
        Assert.Equal(version, (await Accounts(client)).Version);
        Assert.Equal(1, (await Register(client)).Total);
    }

    [Fact]
    public async Task Header_totals_and_running_balances_use_full_account_history_before_filters_and_pagination()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 1000); var other = await AddAccount(client, 999, "Other");
        await Post<MutationDto>(client, "/api/transactions", (await Request(client, account, -100, "groceries", "Shop")) with { Date = new(2026,10,22), Status = "cleared" });
        var edited = await Post<MutationDto>(client, "/api/transactions", (await Request(client, account, -50.25m, "rent", "Bill")) with { Date = new(2026,10,22) });
        await Post<MutationDto>(client, "/api/transactions", (await Request(client, account, 0.25m, "groceries", "Refund")) with { Date = new(2026,10,23), Status = "cleared" });
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), (await Accounts(client)).Version, account, other, new(2026,10,24), 30, "To savings"));
        var uncategorized = await Post<MutationDto>(client, "/api/transactions", (await Request(client, account, -5, payee: "Pending")) with { Date = new(2026,10,25) });
        var deleted = await AddTransaction(client, account, -999, "rent"); await Delete(client, deleted.Id);
        var first = await Register(client, $"accountId={account}&pageSize=2");
        var summary = first.Accounts.Accounts.Single(a => a.Id == account);
        Assert.Equal(900.25m, summary.ClearedBalance); Assert.Equal(-85.25m, summary.UnclearedBalance);
        Assert.Equal(815, summary.Balance); Assert.Equal(summary.Balance, summary.ClearedBalance + summary.UnclearedBalance);
        Assert.Equal(1, summary.NeedsCategoryCount); Assert.Null(summary.LastReconciledAt);
        Assert.Equal(815, first.Items[0].RunningBalance); Assert.Equal(820, first.Items[1].RunningBalance);
        Assert.Equal("Other", first.Items[1].TransferAccountName);
        var filtered = await Register(client, $"accountId={account}&search=Refund&from=2026-10-23&to=2026-10-23&categoryId=groceries&status=cleared&pageSize=1");
        Assert.Equal(850, Assert.Single(filtered.Items).RunningBalance);
        Assert.Equal("Everyday: Groceries", filtered.Items[0].CategoryName);
        Assert.Equal(summary, filtered.Accounts.Accounts.Single(a => a.Id == account));
        var all = await Register(client, $"accountId={account}");
        for (var i = 0; i < all.Items.Count - 1; i++)
            Assert.Equal(all.Items[i].RunningBalance - all.Items[i].Amount, all.Items[i + 1].RunningBalance);
        await Put<MutationDto>(client, $"/api/transactions/{edited.Id}", (await Request(client, account, -60.25m, "rent", "Bill")) with { Date = new(2026,10,22) });
        var page = await Register(client, $"accountId={account}&pageSize=1&page=2");
        Assert.Equal(810, Assert.Single(page.Items).RunningBalance); Assert.Equal(805, page.Accounts.Accounts.Single(a => a.Id == account).Balance);
        await Post<MutationDto>(client, $"/api/transactions/{uncategorized.Id}/status", new StatusRequest(Guid.NewGuid(), page.Version, "cleared"));
        var before = (await Accounts(client)).Accounts.Single(a => a.Id == account);
        await Post<MutationDto>(client, $"/api/accounts/{account}/reconcile", new ReconcileRequest(Guid.NewGuid(), (await Accounts(client)).Version, before.ClearedBalance));
        var after = (await Accounts(client)).Accounts.Single(a => a.Id == account);
        Assert.NotNull(after.LastReconciledAt); Assert.Equal(before.Balance, after.Balance); Assert.Equal(before.ClearedBalance, after.ClearedBalance);
        Assert.True((await Register(client, $"accountId={account}&search=Refund")).Items[0].IsLocked);
    }

    [Fact]
    public async Task Account_header_metadata_is_validated_persisted_and_does_not_claim_a_bank_connection()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var created = await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), 1, "Reserve", Date, 0, "savings"));
        await Put<MutationDto>(client, $"/api/accounts/{created.Id}", new UpdateAccountRequest(Guid.NewGuid(), created.Version, "Reserve", false, IsFavorite:true));
        var before = await Accounts(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{created.Id}", new UpdateAccountRequest(Guid.NewGuid(), before.Version, "Reserve", false, "credit"))).StatusCode);
        await using var restarted = new TestApp(app.ConnectionString);
        var actual = Assert.Single((await Accounts(restarted.CreateClient())).Accounts);
        Assert.Equal("savings", actual.AccountType); Assert.True(actual.IsFavorite); Assert.Equal("unlinked", actual.ConnectionStatus);
        Assert.Null(actual.LastReconciledAt); Assert.Equal(before.Version, (await Accounts(client)).Version);
    }

    [Fact]
    public async Task View_options_follow_the_account_across_clients_and_restart_without_changing_money()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var one = await AddAccount(client, 123.45m); var two = await AddAccount(client, 5);
        var path = $"/api/register-views/{one}";
        var defaults = await Get<RegisterViewDto>(client, path);
        Assert.Equal(new RegisterViewDto(0, "allDates", null, null, true, true, true, true), defaults);
        var before = await Accounts(client);
        var selected = defaults with { DatePreset = "custom", FromMonth = "2024-02", ToMonth = "2036-10", ShowReconciled = false, ShowRunningBalance = false, ShowFlags = false, ShowImages = false };
        var saved = await Put<RegisterViewDto>(client, path, selected);
        Assert.Equal(selected with { Version = 1 }, saved);
        Assert.Equal(saved, await Put<RegisterViewDto>(client, path, selected)); // lost-response retry
        Assert.Equal(defaults, await Get<RegisterViewDto>(client, $"/api/register-views/{two}"));
        Assert.Equal(defaults, await Get<RegisterViewDto>(client, "/api/register-views/all"));
        await using var restarted = new TestApp(app.ConnectionString);
        Assert.Equal(saved, await Get<RegisterViewDto>(restarted.CreateClient(), path));
        var after = await Accounts(client);
        Assert.Equal(before.Version, after.Version); Assert.Equal(before.Accounts, after.Accounts);
        await using var db = app.CreateDb(); Assert.Single(await db.RegisterViews.ToListAsync());
        Assert.Equal(2, await db.LedgerCommands.CountAsync());
    }

    [Fact]
    public async Task View_options_reject_invalid_missing_and_concurrent_writes_without_partial_changes()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 0); var path = $"/api/register-views/{account}";
        var original = await Get<RegisterViewDto>(client, path);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, original with { DatePreset = "custom", FromMonth = "2026-12", ToMonth = "2026-01" })).StatusCode);
        Assert.Equal(original, await Get<RegisterViewDto>(client, path));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/register-views/{Guid.NewGuid()}", original)).StatusCode);
        var otherClient = app.CreateClient();
        var replies = await Task.WhenAll(client.PutAsJsonAsync(path, original with { ShowFlags = false }), otherClient.PutAsJsonAsync(path, original with { ShowImages = false }));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = await Get<RegisterViewDto>(client, path);
        Assert.Equal(1, winner.Version); Assert.NotEqual(winner.ShowFlags, winner.ShowImages);
        var next = await Put<RegisterViewDto>(client, path, winner with { DatePreset = "lastYear" });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, original)).StatusCode);
        Assert.Equal(next, await Get<RegisterViewDto>(client, path));
    }

    [Fact]
    public async Task Hiding_reconciled_entries_filters_before_pagination_without_resetting_totals_or_running_balance()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var account = await AddAccount(client, 100);
        await Post<MutationDto>(client, $"/api/accounts/{account}/reconcile", new ReconcileRequest(Guid.NewGuid(), (await Accounts(client)).Version, 100));
        await AddTransaction(client, account, -10, null, "Shop"); await AddTransaction(client, account, 20, null, "Refund");
        var before = await Register(client, $"accountId={account}");
        var filtered = await Register(client, $"accountId={account}&includeReconciled=false&pageSize=1");
        Assert.Equal(2, filtered.Total); Assert.Single(filtered.Items); Assert.NotEqual("reconciled", filtered.Items[0].Status);
        Assert.Equal(before.Items.Single(t => t.Id == filtered.Items[0].Id).RunningBalance, filtered.Items[0].RunningBalance);
        Assert.Equal(before.Accounts.Accounts, filtered.Accounts.Accounts);
        Assert.Empty((await Register(client, $"accountId={account}&includeReconciled=false&status=reconciled")).Items);
        Assert.Equal(3, before.Total);
    }

    private static Task<SchedulesDto> Schedules(HttpClient client, string query = "") => Get<SchedulesDto>(client, "/api/schedules?" + query);
    private static async Task<MutationDto> ScheduleAction(HttpClient client, ScheduledItemDto item, string action, DateOnly? date = null, ScheduleTemplateDto? template = null) =>
        await Post<MutationDto>(client, $"/api/schedules/{item.Id}/actions", new ScheduleActionRequest(Guid.NewGuid(), (await Accounts(client)).Version, action, item.OccurrenceId, date, template));

    [Fact]
    public async Task Schedules_are_persisted_forecasts_and_post_once_with_normal_budget_projection_and_audit()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient(); var account = await AddAccount(client, 100);
        var request = new ScheduleRequest(Guid.NewGuid(), (await Accounts(client)).Version, account, new(2027,1,31), -20, "Bill", "Recurring", "groceries", [], "monthly");
        var created = await Post<MutationDto>(client, "/api/schedules", request);
        Assert.Equal(created, await Post<MutationDto>(client, "/api/schedules", request));
        var item = Assert.Single((await Schedules(client)).Items);
        Assert.Equal(80, item.Transaction.RunningBalance); Assert.Equal(100, (await Accounts(client)).Accounts[0].Balance);
        Assert.Single((await Register(client)).Items);
        await using var restarted = new TestApp(app.ConnectionString); var other = restarted.CreateClient();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(item), System.Text.Json.JsonSerializer.Serialize(Assert.Single((await Schedules(other)).Items)));
        var post = new ScheduleActionRequest(Guid.NewGuid(), (await Accounts(client)).Version, "post", item.OccurrenceId);
        var first = await Post<MutationDto>(client, $"/api/schedules/{item.Id}/actions", post);
        Assert.Equal(first, await Post<MutationDto>(other, $"/api/schedules/{item.Id}/actions", post));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions", post with {CommandId = Guid.NewGuid(), Version = first.Version})).StatusCode);
        Assert.Equal(80, (await Accounts(client)).Accounts[0].Balance);
        Assert.Equal(-20, Category(await Get<BudgetDto>(client,"/api/budget/2027-01"),"groceries").Activity);
        Assert.Equal(new DateOnly(2027,2,28), Assert.Single((await Schedules(client)).Items).Transaction.Date);
        await using var db = app.CreateDb();
        Assert.Equal("posted", Assert.Single(await db.ScheduleOccurrences.ToListAsync()).Disposition);
        Assert.Equal(2, await db.Transactions.CountAsync());
        Assert.Contains(await db.LedgerCommands.ToListAsync(), c=>c.Kind.StartsWith("schedule.post/"));
    }

    [Fact]
    public async Task Schedule_edits_skip_snooze_pause_end_and_one_off_changes_preserve_history_and_anchor()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient(); var account = await AddAccount(client, 100);
        var request = new ScheduleRequest(Guid.NewGuid(), (await Accounts(client)).Version, account, new(2027,1,31), -20, "Bill", "", "groceries", [], "monthly", Count:4);
        await Post<MutationDto>(client,"/api/schedules",request);
        var item = Assert.Single((await Schedules(client)).Items);
        await ScheduleAction(client,item,"editOnce",new(2027,2,2),new(-25,"One-off","", "groceries",[]));
        item = Assert.Single((await Schedules(client)).Items); Assert.Equal(-25,item.Transaction.Amount);
        await ScheduleAction(client,item,"snooze",new(2027,2,4));
        item = Assert.Single((await Schedules(client)).Items); Assert.Equal(new DateOnly(2027,2,4),item.Transaction.Date);
        await ScheduleAction(client,item,"pause");
        item = Assert.Single((await Schedules(client)).Items); Assert.True(item.IsPaused); Assert.Null(item.Transaction.RunningBalance);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions",new ScheduleActionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"post",item.OccurrenceId))).StatusCode);
        await ScheduleAction(client,item,"resume"); await ScheduleAction(client,item,"post");
        item = Assert.Single((await Schedules(client)).Items); Assert.Equal(-20,item.Transaction.Amount); Assert.Equal(new DateOnly(2027,2,28),item.Transaction.Date);
        await ScheduleAction(client,item,"skip"); item=Assert.Single((await Schedules(client)).Items);
        Assert.Equal(new DateOnly(2027,3,31),item.Transaction.Date);
        await Put<MutationDto>(client,$"/api/schedules/{item.Id}",request with {CommandId=Guid.NewGuid(),Version=(await Accounts(client)).Version,Date=new(2027,4,15),Amount=-30});
        item=Assert.Single((await Schedules(client)).Items); Assert.Equal(new DateOnly(2027,4,15),item.Transaction.Date);
        await ScheduleAction(client,item,"end"); Assert.Empty((await Schedules(client)).Items);
        Assert.Equal(75,(await Accounts(client)).Accounts[0].Balance);
        await using var db=app.CreateDb(); Assert.Equal(2,await db.Transactions.CountAsync()); Assert.Equal(2,await db.ScheduleOccurrences.CountAsync());
    }

    [Fact]
    public async Task Scheduled_transfers_splits_pagination_validation_and_concurrent_post_are_atomic()
    {
        await using var app=await fixture.CreateAppAsync(); var client=app.CreateClient(); var source=await AddAccount(client,100);var target=await AddAccount(client,0,"Reserve");
        var request=new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,source,new(2027,1,10),-10,null,"Transfer",null,[],"weekly",target);
        var invalid=await client.PostAsJsonAsync("/api/schedules",request with {ToAccountId=source}); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode); Assert.Empty((await Schedules(client)).Items);
        await Post<MutationDto>(client,"/api/schedules",request);
        var incoming=Assert.Single((await Schedules(client,$"accountId={target}")).Items); Assert.Equal(10,incoming.Transaction.Amount); Assert.Equal(10,incoming.Transaction.RunningBalance);
        var item=Assert.Single((await Schedules(client,$"accountId={source}")).Items);
        var action=new ScheduleActionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"post",item.OccurrenceId);
        var responses=await Task.WhenAll(client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions",action),app.CreateClient().PostAsJsonAsync($"/api/schedules/{item.Id}/actions",action with {CommandId=Guid.NewGuid()}));
        Assert.Single(responses,r=>r.IsSuccessStatusCode);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(100,(await Accounts(client)).Accounts.Sum(a=>a.Balance));
        var pair=(await Register(client)).Items.Where(t=>t.TransferId!=null).ToList();Assert.Equal(2,pair.Count);Assert.Equal(0,pair.Sum(t=>t.Amount));
        await Post<MutationDto>(client,"/api/schedules",request with {CommandId=Guid.NewGuid(),Version=(await Accounts(client)).Version,ToAccountId=null,Date=new(2027,1,12),CategoryId=null,Splits=[new("groceries",-4),new("rent",-6)],Repeat="never"});
        var page=await Schedules(client,$"accountId={source}&pageSize=1&from=2027-01-12&to=2027-01-12");Assert.Equal(1,page.Total);Assert.Single(page.Items);
        await ScheduleAction(client,page.Items[0],"post");Assert.Equal(90,(await Accounts(client)).Accounts.Sum(a=>a.Balance));
        Assert.Single((await Schedules(client)).Items);
    }

    [Fact]
    public async Task Confirmed_reconciled_edits_invalidate_snapshots_keep_audit_and_require_reconciliation_again()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        var entry=await AddTransaction(client,account,-10,"groceries","Shop");
        await Post<MutationDto>(client,$"/api/transactions/{entry.Id}/status",new StatusRequest(Guid.NewGuid(),(await Accounts(client)).Version,"cleared"));
        await Post<MutationDto>(client,$"/api/accounts/{account}/reconcile",new ReconcileRequest(Guid.NewGuid(),(await Accounts(client)).Version,90));
        var edit=(await Request(client,account,-15,"groceries","Shop")) with {ConfirmReconciledEdit=true,Status="cleared"};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/transactions/{entry.Id}",edit with {ConfirmReconciledEdit=false})).StatusCode);
        await Put<MutationDto>(client,$"/api/transactions/{entry.Id}",edit);
        var actual=(await Register(client)).Items.Single(t=>t.Id==entry.Id);Assert.Equal("cleared",actual.Status);Assert.Equal(-15,actual.Amount);
        Assert.Null((await Accounts(client)).Accounts[0].LastReconciledAt);
        await using var db=app.CreateDb();Assert.True(Assert.Single(await db.Reconciliations.ToListAsync()).IsInvalidated);
        var audit=(await db.LedgerCommands.OrderByDescending(c=>c.Version).FirstAsync());Assert.Contains("reconciled",audit.BeforeJson);Assert.Contains("invalidatedReconciliations",audit.AfterJson);
    }

    [Fact]
    public async Task Flag_colors_and_custom_names_persist_across_clients_without_affecting_reconciliation_or_money()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        var entry=Assert.Single((await Register(client)).Items);
        await Post<MutationDto>(client,$"/api/accounts/{account}/reconcile",new ReconcileRequest(Guid.NewGuid(),(await Accounts(client)).Version,100));
        var before=(await Accounts(client)).Accounts[0];var flags=await Get<FlagPaletteDto>(client,"/api/flags");Assert.Equal(6,flags.Flags.Count);
        var names=flags.Flags.Select(f=>f.Color=="blue"?f with{Name="Household pays"}:f).ToList();
        await Put<MutationDto>(client,"/api/flags",new RenameFlagsRequest(Guid.NewGuid(),flags.Version,names));
        var flag=new SetFlagRequest(Guid.NewGuid(),(await Accounts(client)).Version,"blue");
        var saved=await Post<MutationDto>(client,$"/api/transactions/{entry.Id}/flag",flag);
        Assert.Equal(saved,await Post<MutationDto>(client,$"/api/transactions/{entry.Id}/flag",flag));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/transactions/{entry.Id}/flag",flag with{CommandId=Guid.NewGuid(),Version=saved.Version,Color="pink"})).StatusCode);
        await using var restarted=new TestApp(app.ConnectionString);var other=restarted.CreateClient();
        Assert.Equal("blue",Assert.Single((await Register(other)).Items).FlagColor);
        Assert.Equal("Household pays",(await Get<FlagPaletteDto>(other,"/api/flags")).Flags.Single(f=>f.Color=="blue").Name);
        Assert.Equal(before,(await Accounts(other)).Accounts[0]);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync("/api/flags",new RenameFlagsRequest(Guid.NewGuid(),saved.Version,[new("red","")]))).StatusCode);
        await Post<MutationDto>(client,$"/api/transactions/{entry.Id}/flag",new SetFlagRequest(Guid.NewGuid(),saved.Version,null));
        Assert.Null(Assert.Single((await Register(client)).Items).FlagColor);
    }

    [Fact]
    public async Task Schedule_post_failure_rolls_back_occurrence_transaction_budget_and_audit_then_retries_safely()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,account,Date,-20,"Bill",null,"groceries",[],"monthly",FlagColor:"red"));
        var item=Assert.Single((await Schedules(client)).Items);var before=await Budget(client);var version=(await Accounts(client)).Version;
        var post=new ScheduleActionRequest(Guid.NewGuid(),version,"post",item.OccurrenceId);app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions",post));
        Assert.Equal(version,(await Accounts(client)).Version);Assert.Equal(before.Version,(await Budget(client)).Version);
        Assert.Single((await Register(client)).Items);Assert.Equal(item.OccurrenceId,Assert.Single((await Schedules(client)).Items).OccurrenceId);
        await using(var db=app.CreateDb()){Assert.Empty(await db.ScheduleOccurrences.ToListAsync());Assert.False(await db.LedgerCommands.AnyAsync(c=>c.Id==post.CommandId));}
        await Post<MutationDto>(client,$"/api/schedules/{item.Id}/actions",post);
        Assert.Equal("red",(await Register(client)).Items.Single(t=>!t.IsOpening).FlagColor);
        Assert.Equal(80,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Repeating_posted_entries_seed_one_future_rule_and_closed_accounts_block_posting_until_reopened()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,0);
        var tx=(await Request(client,account,10,null,"Income")) with{Repeat="monthly"};
        await Post<MutationDto>(client,"/api/transactions",tx);
        Assert.Equal(10,(await Accounts(client)).Accounts[0].Balance);var next=Assert.Single((await Schedules(client)).Items);
        Assert.Equal(Date.AddMonths(1),next.Transaction.Date);
        var reserve=await AddAccount(client,0,"Reserve");
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,reserve,Date,5,"Future income",null,null,[],"never",Count:1));
        var item=(await Schedules(client)).Items.Single(i=>i.Transaction.AccountId==reserve);
        await Put<MutationDto>(client,$"/api/accounts/{reserve}",new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(client)).Version,"Reserve",true));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions",new ScheduleActionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"post",item.OccurrenceId))).StatusCode);
        await Put<MutationDto>(client,$"/api/accounts/{reserve}",new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(client)).Version,"Reserve",false));
        await ScheduleAction(client,item,"post");Assert.Single((await Schedules(client)).Items);
    }

    [Fact]
    public async Task Account_notes_and_working_balance_adjustments_are_atomic_audited_idempotent_and_persistent()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        var request=new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(client)).Version,"Main cash",false,Notes:"Household notes\nSecond line",WorkingBalance:125.50m);
        var saved=await Put<MutationDto>(client,$"/api/accounts/{account}",request);
        Assert.Equal(saved,await Put<MutationDto>(client,$"/api/accounts/{account}",request));
        var entries=(await Register(client)).Items;var adjustment=Assert.Single(entries,t=>!t.IsOpening);Assert.Equal(25.50m,adjustment.Amount);Assert.Equal("cleared",adjustment.Status);Assert.Equal("Balance Adjustment",adjustment.Payee);
        await using var restarted=new TestApp(app.ConnectionString);var after=Assert.Single((await Accounts(restarted.CreateClient())).Accounts);
        Assert.Equal("Household notes\nSecond line",after.Notes);Assert.Equal(125.50m,after.Balance);Assert.Equal(after.Balance,after.ClearedBalance+after.UnclearedBalance);
        Assert.Equal(125.50m,(await Get<BudgetDto>(client,$"/api/budget/{adjustment.Date:yyyy-MM}")).ReadyToAssign);
        var noOp=request with{CommandId=Guid.NewGuid(),Version=saved.Version};await Put<MutationDto>(client,$"/api/accounts/{account}",noOp);Assert.Equal(2,(await Register(client)).Total);
        var before=await Accounts(client);var failing=request with{CommandId=Guid.NewGuid(),Version=before.Version,Notes="Must roll back",WorkingBalance=140};app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PutAsJsonAsync($"/api/accounts/{account}",failing));
        Assert.Equal(before.Version,(await Accounts(client)).Version);Assert.Equal(after.Notes,(await Accounts(client)).Accounts[0].Notes);Assert.Equal(125.50m,(await Accounts(client)).Accounts[0].Balance);Assert.Equal(2,(await Register(client)).Total);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/accounts/{account}",failing with{Notes=new string('x',1001)})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/accounts/{account}",failing with{WorkingBalance=125.501m})).StatusCode);
        await using var db=app.CreateDb();var audit=await db.LedgerCommands.SingleAsync(c=>c.Id==request.CommandId);Assert.Contains("adjustment",audit.AfterJson);Assert.Contains("100",audit.BeforeJson);
    }

    [Fact]
    public async Task Schedule_post_revalidates_archived_categories_and_end_count_preserves_existing_entries()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,account,Date,-5,"Bill",null,"groceries",[],"daily",Count:2));
        var item=Assert.Single((await Schedules(client)).Items);var budget=await Budget(client);var edit=Editable(budget);
        edit=edit with{Groups=edit.Groups.Select(g=>g with{Categories=g.Categories.Select(c=>c.Id=="groceries"?c with{IsArchived=true}:c).ToList()}).ToList()};
        await Put<BudgetDto>(client,"/api/budget/2026-10",edit);
        var version=(await Accounts(client)).Version;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/schedules/{item.Id}/actions",new ScheduleActionRequest(Guid.NewGuid(),version,"post",item.OccurrenceId))).StatusCode);
        Assert.Single((await Register(client)).Items);Assert.Equal(item.OccurrenceId,Assert.Single((await Schedules(client)).Items).OccurrenceId);
        budget=await Budget(client);edit=Editable(budget);edit=edit with{Groups=edit.Groups.Select(g=>g with{Categories=g.Categories.Select(c=>c.Id=="groceries"?c with{IsArchived=false}:c).ToList()}).ToList()};await Put<BudgetDto>(client,"/api/budget/2026-10",edit);
        await ScheduleAction(client,item,"post");item=Assert.Single((await Schedules(client)).Items);await ScheduleAction(client,item,"skip");
        Assert.Empty((await Schedules(client)).Items);Assert.Equal(95,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Confirmed_reconciled_transfer_edits_are_atomic_and_invalidate_both_snapshots_with_audit()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var from=await AddAccount(client,100);var to=await AddAccount(client,0,"Savings");
        var transfer=await Post<MutationDto>(client,"/api/transfers",new TransferRequest(Guid.NewGuid(),(await Accounts(client)).Version,from,to,Date,20,null));
        var pair=(await Register(client)).Items.Where(t=>t.TransferId==transfer.Id).ToList();
        foreach(var leg in pair)await Post<MutationDto>(client,$"/api/transactions/{leg.Id}/status",new StatusRequest(Guid.NewGuid(),(await Accounts(client)).Version,"cleared"));
        foreach(var id in new[]{from,to})await Post<MutationDto>(client,$"/api/accounts/{id}/reconcile",new ReconcileRequest(Guid.NewGuid(),(await Accounts(client)).Version,(await Accounts(client)).Accounts.Single(a=>a.Id==id).ClearedBalance));
        var edit=new TransferRequest(Guid.NewGuid(),(await Accounts(client)).Version,from,to,Date,25,"Corrected",ConfirmReconciledEdit:true);var path=$"/api/transfers/{pair[0].Id}";
        app.Failure.FailAuditSave=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PutAsJsonAsync(path,edit));
        Assert.All((await Accounts(client)).Accounts,a=>Assert.NotNull(a.LastReconciledAt));
        await Put<MutationDto>(client,path,edit);
        var accounts=(await Accounts(client)).Accounts;Assert.Equal(75,accounts.Single(a=>a.Id==from).Balance);Assert.Equal(25,accounts.Single(a=>a.Id==to).Balance);Assert.All(accounts,a=>Assert.Null(a.LastReconciledAt));
        Assert.All((await Register(client)).Items.Where(t=>t.TransferId==transfer.Id),t=>Assert.Equal("cleared",t.Status));
        await using var db=app.CreateDb();Assert.Equal(2,await db.Reconciliations.CountAsync(r=>r.IsInvalidated));Assert.Contains("invalidatedReconciliations",(await db.LedgerCommands.SingleAsync(c=>c.Id==edit.CommandId)).AfterJson);
    }

    [Fact]
    public async Task Zero_opening_entry_remains_editable_without_allowing_zero_regular_entries()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,0);var opening=Assert.Single((await Register(client)).Items);
        var edit=(await Request(client,account,0)) with{Status="cleared",Memo="Starting point"};
        await Put<MutationDto>(client,$"/api/transactions/{opening.Id}",edit);
        Assert.Equal("Starting point",Assert.Single((await Register(client)).Items).Memo);Assert.Equal(0,(await Accounts(client)).Accounts[0].Balance);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/transactions",edit with{CommandId=Guid.NewGuid(),Version=(await Accounts(client)).Version})).StatusCode);
    }

    [Fact]
    public async Task Due_schedules_materialize_once_for_approval_and_approve_clear_or_reject_updates_counts_and_balances()
    {
        await using var app=await fixture.CreateAppAsync();app.Clock.UtcNow=new(2027,1,30,12,0,0,TimeSpan.Zero);var client=app.CreateClient();var account=await AddAccount(client,100);
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,account,new(2027,1,31),-10,"Bill",null,"groceries",[],"monthly"));
        Assert.Equal(0,await Post<int>(client,"/api/schedules/process-due",new{}));Assert.Single((await Register(client)).Items);
        app.Clock.UtcNow=new(2027,1,31,12,0,0,TimeSpan.Zero);app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PostAsJsonAsync("/api/schedules/process-due",new{}));Assert.Single((await Register(client)).Items);
        var results=await Task.WhenAll(client.PostAsJsonAsync("/api/schedules/process-due",new{}),app.CreateClient().PostAsJsonAsync("/api/schedules/process-due",new{}));Assert.All(results,r=>Assert.True(r.IsSuccessStatusCode));
        var pending=Assert.Single((await Register(client)).Items,t=>!t.IsOpening);Assert.False(pending.IsApproved);Assert.True(pending.NeedsAttention);Assert.False(pending.NeedsCategory);
        var header=(await Accounts(client)).Accounts[0];Assert.Equal(1,header.UnapprovedCount);Assert.Equal(1,header.AttentionCount);Assert.Equal(100,header.ClearedBalance);Assert.Equal(-10,header.UnclearedBalance);Assert.Equal(90,header.Balance);
        Assert.Equal(new DateOnly(2027,2,28),Assert.Single((await Schedules(client)).Items).Transaction.Date);
        Assert.Single((await Register(client,"needsAttention=true")).Items);
        await Post<MutationDto>(client,$"/api/transactions/{pending.Id}/review",new ReviewTransactionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"approve"));
        Assert.Equal(0,(await Accounts(client)).Accounts[0].UnapprovedCount);Assert.Empty((await Register(client,"needsAttention=true")).Items);Assert.Equal(90,(await Accounts(client)).Accounts[0].Balance);
        app.Clock.UtcNow=new(2027,2,28,12,0,0,TimeSpan.Zero);Assert.Equal(1,await Post<int>(client,"/api/schedules/process-due",new{}));pending=Assert.Single((await Register(client)).Items,t=>!t.IsApproved);
        await Post<MutationDto>(client,$"/api/transactions/{pending.Id}/status",new StatusRequest(Guid.NewGuid(),(await Accounts(client)).Version,"cleared"));
        Assert.True((await Register(client)).Items.Single(t=>t.Id==pending.Id).IsApproved);Assert.Equal(0,(await Accounts(client)).Accounts[0].UnapprovedCount);
        app.Clock.UtcNow=new(2027,3,31,12,0,0,TimeSpan.Zero);await Post<int>(client,"/api/schedules/process-due",new{});pending=Assert.Single((await Register(client)).Items,t=>!t.IsApproved);
        var reject=new ReviewTransactionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"reject");var rejected=await Post<MutationDto>(client,$"/api/transactions/{pending.Id}/review",reject);Assert.Equal(rejected,await Post<MutationDto>(client,$"/api/transactions/{pending.Id}/review",reject));
        Assert.Equal(80,(await Accounts(client)).Accounts[0].Balance);Assert.Equal(0,await Post<int>(client,"/api/schedules/process-due",new{}));
        await using var db=app.CreateDb();Assert.Equal(3,await db.ScheduleOccurrences.CountAsync());Assert.True((await db.Transactions.SingleAsync(t=>t.Id==pending.Id)).IsDeleted);
    }

    [Fact]
    public async Task Automatic_posting_respects_local_date_snooze_and_closed_accounts_and_retries_after_reopening()
    {
        await using var app=await fixture.CreateAppAsync();app.Clock.Zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");app.Clock.UtcNow=new(2027,1,30,23,30,0,TimeSpan.Zero);
        var client=app.CreateClient();var account=await AddAccount(client,0);
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,account,new(2027,1,31),5,"Income",null,null,[],"daily"));var item=Assert.Single((await Schedules(client)).Items);
        await ScheduleAction(client,item,"snooze",new(2027,2,2));Assert.Equal(0,await Post<int>(client,"/api/schedules/process-due",new{}));
        await Put<MutationDto>(client,$"/api/accounts/{account}",new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(client)).Version,"Checking",true));
        app.Clock.UtcNow=new(2027,2,2,12,0,0,TimeSpan.Zero);Assert.Equal(0,await Post<int>(client,"/api/schedules/process-due",new{}));Assert.Contains("Reopen",Assert.Single((await Schedules(client)).Items).PostingError);
        await Put<MutationDto>(client,$"/api/accounts/{account}",new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(client)).Version,"Checking",false));
        Assert.Equal(3,await Post<int>(client,"/api/schedules/process-due",new{}));Assert.Equal(15,(await Accounts(client)).Accounts[0].Balance);Assert.Equal(3,(await Accounts(client)).Accounts[0].UnapprovedCount);Assert.Null(Assert.Single((await Schedules(client)).Items).PostingError);
    }

    [Fact]
    public async Task Automatically_posted_transfer_can_be_cleared_on_one_leg_and_rejected_atomically_on_the_other()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var from=await AddAccount(client,100);var to=await AddAccount(client,0,"Savings");
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,from,Date,-20,null,null,null,[],"monthly",to));
        Assert.Equal(1,await Post<int>(client,"/api/schedules/process-due",new{}));var pair=(await Register(client)).Items.Where(t=>t.TransferId!=null).ToList();Assert.All(pair,t=>Assert.False(t.IsApproved));Assert.All((await Accounts(client)).Accounts,a=>Assert.Equal(1,a.UnapprovedCount));
        var outgoing=pair.Single(t=>t.Amount<0);var incoming=pair.Single(t=>t.Amount>0);
        await Post<MutationDto>(client,$"/api/transactions/{outgoing.Id}/status",new StatusRequest(Guid.NewGuid(),(await Accounts(client)).Version,"cleared"));
        Assert.Equal(0,(await Accounts(client)).Accounts.Single(a=>a.Id==from).UnapprovedCount);Assert.Equal(1,(await Accounts(client)).Accounts.Single(a=>a.Id==to).UnapprovedCount);
        await Post<MutationDto>(client,$"/api/transactions/{incoming.Id}/review",new ReviewTransactionRequest(Guid.NewGuid(),(await Accounts(client)).Version,"reject"));
        Assert.DoesNotContain((await Register(client)).Items,t=>t.TransferId!=null);Assert.Equal(100,(await Accounts(client)).Accounts.Single(a=>a.Id==from).Balance);Assert.Equal(0,(await Accounts(client)).Accounts.Single(a=>a.Id==to).Balance);
    }

    private static Task<PayeesDto> ManagedPayees(HttpClient client) => Get<PayeesDto>(client,"/api/payees/manage");
    private static PayeeUpdateDto PayeeUpdate(PayeeDto p) => new(p.Id,p.Name,p.IsHidden,p.CategoryMode,p.DefaultCategoryId,p.Rules);

    [Fact]
    public async Task Payee_settings_rename_hide_reload_and_replay_preserve_history_and_schedules()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await AddTransaction(client,account,-10,"groceries","Market");
        await Post<MutationDto>(client,"/api/schedules",new ScheduleRequest(Guid.NewGuid(),(await Accounts(client)).Version,account,new(2026,11,21),-10,"Market",null,"groceries",[],"monthly"));
        var list=await ManagedPayees(client);var payee=Assert.Single(list.Payees);Assert.Equal("groceries",payee.EffectiveCategoryId);Assert.Equal(1,payee.TransactionCount);
        var request=new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(payee) with{Name="Local Market",IsHidden=true,CategoryMode="always",DefaultCategoryId="dining",Rules=[new("contains","MARKET-")]}]);
        var saved=await Put<MutationDto>(client,"/api/payees/manage",request);Assert.Equal(saved,await Put<MutationDto>(client,"/api/payees/manage",request));
        var reloaded=Assert.Single((await ManagedPayees(app.CreateClient())).Payees);Assert.Equal("Local Market",reloaded.Name);Assert.True(reloaded.IsHidden);Assert.Equal("dining",reloaded.EffectiveCategoryId);Assert.Single(reloaded.Rules);
        Assert.Empty(await Get<List<string>>(client,"/api/payees"));Assert.Equal("Local Market",Assert.Single((await Register(client,$"payeeId={payee.Id}")).Items).Payee);
        Assert.Equal("Local Market",Assert.Single((await Schedules(client)).Items).Transaction.Payee);Assert.Equal(90,(await Accounts(client)).Accounts[0].Balance);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync("/api/payees/manage",request with{CommandId=Guid.NewGuid()})).StatusCode);
        var audit=await Get<AuditPageDto>(client,"/api/ledger/audit");Assert.Single(audit.Items,a=>a.Kind=="payees.update");
    }

    [Fact]
    public async Task Payee_invalid_bulk_duplicate_delete_used_and_unknown_category_leave_everything_unchanged()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await AddTransaction(client,account,-1,"groceries","One");await AddTransaction(client,account,-1,"groceries","Two");var list=await ManagedPayees(client);var one=list.Payees[0];var two=list.Payees[1];
        foreach(var invalid in new[]{PayeeUpdate(two) with{Name="one"},PayeeUpdate(two) with{Rules=[new("contains","")]},PayeeUpdate(two) with{CategoryMode="always",DefaultCategoryId="missing"},PayeeUpdate(two) with{Delete=true}})
        {
            var result=await client.PutAsJsonAsync("/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(one) with{IsHidden=true},invalid]));Assert.Equal(HttpStatusCode.BadRequest,result.StatusCode);
            var after=await ManagedPayees(client);Assert.Equal(list.Version,after.Version);Assert.All(after.Payees,p=>Assert.False(p.IsHidden));Assert.Equal(new[]{"One","Two"},after.Payees.Select(p=>p.Name));
        }
        Assert.Equal(98,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Renaming_rules_import_hidden_payees_exact_before_contains_and_preserve_explicit_categories()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await AddTransaction(client,account,-1,"groceries","Power");await AddTransaction(client,account,-1,"groceries","Specific");var list=await ManagedPayees(client);
        var power=list.Payees.Single(p=>p.Name=="Power");var specific=list.Payees.Single(p=>p.Name=="Specific");
        await Put<MutationDto>(client,"/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(power) with{IsHidden=true,CategoryMode="always",DefaultCategoryId="dining",Rules=[new("contains","POWER")]},PayeeUpdate(specific) with{Rules=[new("is","Power exact")]}]));
        var import=await Request(client,account,-10,null,"  direct debit POWER co 123  ");var responses=await Task.WhenAll(client.PostAsJsonAsync("/api/transactions/import",import),app.CreateClient().PostAsJsonAsync("/api/transactions/import",import));
        var first=await Read<MutationDto>(responses[0]);Assert.Equal(first,await Read<MutationDto>(responses[1]));
        var entry=Assert.Single((await Register(client)).Items,t=>t.Id==first.Id);Assert.Equal("Power",entry.Payee);Assert.Equal("dining",entry.CategoryId);Assert.False(entry.IsApproved);Assert.Equal(1,(await Accounts(client)).Accounts[0].UnapprovedCount);
        var exact=await Post<MutationDto>(client,"/api/transactions/import",await Request(client,account,-3,"transport","POWER EXACT"));entry=(await Register(client)).Items.Single(t=>t.Id==exact.Id);Assert.Equal("Specific",entry.Payee);Assert.Equal("transport",entry.CategoryId);
        await AddTransaction(client,account,-1,null,"direct debit POWER co 123");Assert.Contains((await Register(client)).Items,t=>t.Payee=="direct debit POWER co 123"&&t.IsApproved&&t.CategoryId==null);
        var audit=await Get<AuditPageDto>(client,$"/api/ledger/audit?resourceId={first.Id}");Assert.Contains("direct debit POWER",Assert.Single(audit.Items).AfterJson);
        Assert.Equal(84,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Ambiguous_import_rules_and_invalid_rows_do_not_write_and_a_more_specific_rule_wins()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        await AddTransaction(client,account,-1,"groceries","Alpha");await AddTransaction(client,account,-1,"groceries","Beta");var list=await ManagedPayees(client);
        await Put<MutationDto>(client,"/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),list.Version,list.Payees.Select(p=>PayeeUpdate(p) with{Rules=[new("contains","bank")]}).ToList()));
        var version=(await Accounts(client)).Version;var ambiguous=await Request(client,account,-2,null,"Bank debit");Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/transactions/import",ambiguous)).StatusCode);Assert.Equal(version,(await Accounts(client)).Version);
        Assert.Equal(3,(await Register(client)).Total);Assert.Equal(2,(await ManagedPayees(client)).Payees.Count);
        list=await ManagedPayees(client);var beta=list.Payees.Single(p=>p.Name=="Beta");await Put<MutationDto>(client,"/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(beta) with{Rules=[new("contains","bank debit")]}]));
        var result=await Post<MutationDto>(client,"/api/transactions/import",await Request(client,account,-2,null,"Bank Debit reference"));Assert.Equal("Beta",(await Register(client)).Items.Single(t=>t.Id==result.Id).Payee);
        var invalid=await Request(client,account,-1.001m,null,"Bad import");Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/transactions/import",invalid)).StatusCode);Assert.DoesNotContain((await ManagedPayees(client)).Payees,p=>p.Name=="Bad import");
    }

    [Fact]
    public async Task Payee_history_and_never_policy_apply_to_imports_without_changing_old_categories()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        for(var i=0;i<3;i++)await Post<MutationDto>(client,"/api/transactions",(await Request(client,account,-1,i==2?"dining":"groceries","Store")) with{Date=Date.AddDays(i)});
        var list=await ManagedPayees(client);var payee=Assert.Single(list.Payees);Assert.Equal("groceries",payee.EffectiveCategoryId);
        var imported=await Post<MutationDto>(client,"/api/transactions/import",await Request(client,account,-1,null,"Store"));Assert.Equal("groceries",(await Register(client)).Items.Single(t=>t.Id==imported.Id).CategoryId);
        await Put<MutationDto>(client,"/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),(await Accounts(client)).Version,[PayeeUpdate(payee) with{CategoryMode="never"}]));
        imported=await Post<MutationDto>(client,"/api/transactions/import",await Request(client,account,-1,null,"Store"));Assert.Null((await Register(client)).Items.Single(t=>t.Id==imported.Id).CategoryId);
        Assert.Equal(3,(await Register(client)).Items.Count(t=>t.CategoryId=="groceries"));Assert.Null(Assert.Single((await ManagedPayees(client)).Payees).EffectiveCategoryId);
    }

    [Fact]
    public async Task Payee_save_and_import_failures_rollback_and_exact_retry_is_safe()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);await AddTransaction(client,account,-1,"groceries","Original");
        var list=await ManagedPayees(client);var payee=Assert.Single(list.Payees);var save=new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(payee) with{Name="Renamed",Rules=[new("contains","bank")]}]);
        app.Failure.FailAuditSave=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PutAsJsonAsync("/api/payees/manage",save));Assert.Equal("Original",Assert.Single((await ManagedPayees(client)).Payees).Name);Assert.Equal(list.Version,(await Accounts(client)).Version);
        await Put<MutationDto>(client,"/api/payees/manage",save);var import=await Request(client,account,-5,null,"Bank debit");app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PostAsJsonAsync("/api/transactions/import",import));Assert.Equal(99,(await Accounts(client)).Accounts[0].Balance);Assert.Equal(import.Version,(await Accounts(client)).Version);
        var saved=await Post<MutationDto>(client,"/api/transactions/import",import);Assert.Equal(saved,await Post<MutationDto>(client,"/api/transactions/import",import));Assert.Equal(94,(await Accounts(client)).Accounts[0].Balance);
        await using var db=app.CreateDb();Assert.Single(await db.Payees.ToListAsync());Assert.Single(await db.LedgerCommands.Where(c=>c.Kind=="transaction.import").ToListAsync());
    }

    [Fact]
    public async Task Concurrent_payee_edits_only_accept_one_version_and_unused_payees_can_be_deleted()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();await AddAccount(client,0);
        await using(var db=app.CreateDb()){db.Payees.Add(new PayeeEntity{Id=Guid.NewGuid(),Name="Unused",NormalizedName="UNUSED"});await db.SaveChangesAsync();}
        var list=await ManagedPayees(client);var payee=Assert.Single(list.Payees);var save=new SavePayeesRequest(Guid.NewGuid(),list.Version,[PayeeUpdate(payee) with{IsHidden=true}]);
        var result=await Task.WhenAll(client.PutAsJsonAsync("/api/payees/manage",save),app.CreateClient().PutAsJsonAsync("/api/payees/manage",save with{CommandId=Guid.NewGuid()}));Assert.Single(result,r=>r.IsSuccessStatusCode);Assert.Single(result,r=>r.StatusCode==HttpStatusCode.Conflict);
        await Put<MutationDto>(client,"/api/payees/manage",new SavePayeesRequest(Guid.NewGuid(),(await Accounts(client)).Version,[PayeeUpdate(payee) with{Delete=true}]));Assert.Empty((await ManagedPayees(app.CreateClient())).Payees);Assert.Equal(0,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Category_picker_month_snapshot_returns_persisted_group_and_available_after_create_assign_and_spend()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        var budget=await Budget(client);var editable=Editable(budget);const string categoryId="new-picker-category";
        var group=editable.Groups.Single(g=>g.Id=="daily");
        var created=group with{Categories=[..group.Categories,new CategoryUpdateDto(categoryId,"Household",0,0,null,false,group.Categories.Max(c=>c.SortOrder)+1,false)]};
        budget=await Put<BudgetDto>(client,"/api/budget/2026-10",editable with{Groups=editable.Groups.Select(g=>g.Id==group.Id?created:g).ToList()});
        Assert.Equal(0,Category(budget,categoryId).Available);
        editable=Editable(budget);budget=await Put<BudgetDto>(client,"/api/budget/2026-10",editable with{Groups=editable.Groups.Select(g=>g with{Categories=g.Categories.Select(c=>c.Id==categoryId?c with{Assigned=25}:c).ToList()}).ToList()});
        await AddTransaction(client,account,-12.35m,categoryId,"Household Store");
        var reloaded=await Budget(app.CreateClient());var category=Category(reloaded,categoryId);
        Assert.Equal("Everyday",reloaded.Groups.Single(g=>g.Categories.Any(c=>c.Id==categoryId)).Name);Assert.Equal(25,category.Assigned);Assert.Equal(-12.35m,category.Activity);Assert.Equal(12.65m,category.Available);Assert.Equal(75,reloaded.ReadyToAssign);
        var november=await Get<BudgetDto>(client,"/api/budget/2026-11");Assert.Equal(12.65m,Category(november,categoryId).Available);
        await AddTransaction(client,account,2.35m,categoryId,"Household Store");Assert.Equal(15,Category(await Budget(client),categoryId).Available);
        Assert.Equal(15,Category(await Get<BudgetDto>(client,"/api/budget/2026-11"),categoryId).Available);Assert.Equal(90,(await Accounts(client)).Accounts[0].Balance);
    }

    [Fact]
    public async Task Named_transaction_creates_a_persistent_payee_once_and_rejects_partial_creation()
    {
        await using var app=await fixture.CreateAppAsync();var client=app.CreateClient();var account=await AddAccount(client,100);
        var request=await Request(client,account,-5,"groceries","  New Cafe  ");
        var saved=await Post<MutationDto>(client,"/api/transactions",request);
        Assert.Equal(saved,await Post<MutationDto>(client,"/api/transactions",request));
        var reloaded=app.CreateClient();var payee=Assert.Single((await ManagedPayees(reloaded)).Payees);
        Assert.Equal("New Cafe",payee.Name);Assert.Equal(1,payee.TransactionCount);
        Assert.Contains("New Cafe",await Get<List<string>>(reloaded,"/api/payees"));
        await AddTransaction(client,account,-2,"groceries","new cafe");
        Assert.Equal(payee.Id,Assert.Single((await ManagedPayees(reloaded)).Payees).Id);
        var invalid=await Request(client,account,-1,"missing-category","Rejected Cafe");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/transactions",invalid)).StatusCode);
        var rollback=await Request(client,account,-1,"groceries","Rolled Back Cafe");app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.PostAsJsonAsync("/api/transactions",rollback));
        Assert.Single((await ManagedPayees(reloaded)).Payees);
        Assert.Equal(93,(await Accounts(reloaded)).Accounts.Single().Balance);
        Assert.Equal(rollback.Version,(await Accounts(reloaded)).Version);
        await Post<MutationDto>(client,"/api/transactions",rollback);
        Assert.Equal(2,(await ManagedPayees(reloaded)).Payees.Count);
    }

    private static Task<AccountsDto> Accounts(HttpClient client) => Get<AccountsDto>(client, "/api/accounts");
    private static Task<RegisterDto> Register(HttpClient client, string query = "") => Get<RegisterDto>(client, "/api/transactions?" + query);
    private static Task<BudgetDto> Budget(HttpClient client) => Get<BudgetDto>(client, "/api/budget/2026-10");
    private static CategoryDto Category(BudgetDto budget, string id) => budget.Groups.SelectMany(g => g.Categories).Single(c => c.Id == id);
    private static async Task<T> Get<T>(HttpClient client, string path) => (await client.GetFromJsonAsync<T>(path))!;
    private static async Task<Guid> AddAccount(HttpClient client, decimal balance, string name = "Checking") =>
        (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), (await Accounts(client)).Version, name, Date, balance))).Id;
    private static async Task<TransactionRequest> Request(HttpClient client, Guid accountId, decimal amount, string? category = null, string? payee = null) =>
        new(Guid.NewGuid(), (await Accounts(client)).Version, accountId, Date, amount, payee, "", category, "uncleared", []);
    private static async Task<MutationDto> AddTransaction(HttpClient client, Guid accountId, decimal amount, string? category = null, string? payee = null) =>
        await Post<MutationDto>(client, "/api/transactions", await Request(client, accountId, amount, category, payee));
    private static async Task Delete(HttpClient client, Guid id) =>
        await Post<MutationDto>(client, $"/api/transactions/{id}/delete", new DeleteTransactionRequest(Guid.NewGuid(), (await Accounts(client)).Version));
    private static async Task<T> Post<T>(HttpClient client, string path, object request) => await Read<T>(await client.PostAsJsonAsync(path, request));
    private static async Task<T> Put<T>(HttpClient client, string path, object request) => await Read<T>(await client.PutAsJsonAsync(path, request));
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static BudgetUpdateDto Editable(BudgetDto budget) => new(budget.Month, budget.Note, budget.Version, budget.FundsAdded,
        budget.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => new CategoryUpdateDto(c.Id, c.Name, c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived)).ToList())).ToList());

}
