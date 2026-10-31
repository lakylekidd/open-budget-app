using System.Text.Json;
using System.Text.Json.Nodes;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.Api.Mcp;

public static partial class McpToolCatalog
{
    private static readonly string[] AccountTypes = ["checking", "savings", "cash", "creditCard", "lineOfCredit"];
    private static readonly string[] FlagColors = ["red", "orange", "yellow", "green", "blue", "purple"];

    private static JsonObject SplitSchema() => Obj("A split line.",
        R("categoryId", Id("Category ID for this split.")), R("amount", Money("Split amount (negative for outflow).")),
        O("payee", Str("Optional split payee.", 160)), O("memo", Str("Optional split memo.", 1000)));

    private static (string, JsonObject, bool)[] TransactionFields() =>
    [
        R("accountId", Uuid("Account ID from accounts_list.")),
        R("date", Date("Transaction date YYYY-MM-DD.")),
        R("amount", Money("Amount: negative for outflows (spending), positive for inflows.")),
        O("payee", Str("Payee name; created if new.", 160)),
        O("memo", Str("Memo.", 1000)),
        O("categoryId", Id("Category ID, or 'inflow' style IDs as shown by plan_get_month. Omit for split or transfer-free uncategorized entries.")),
        O("status", Enum("Cleared status, default uncleared.", "uncleared", "cleared")),
        O("splits", Array("Split lines; their amounts must sum to the transaction amount.", SplitSchema(), 100)),
        O("repeat", Enum("Also create a schedule repeating from this date. Default never.", RecurrenceRules.Repeats)),
    ];

    private static object TransactionBody(McpToolContext c, long version) => new
    {
        commandId = c.CommandId(), version, accountId = c.Guid("accountId"), date = c.Str("date"), amount = c.Money("amount"),
        payee = c.OptStr("payee"), memo = c.OptStr("memo"), categoryId = c.OptStr("categoryId"), status = c.OptStr("status") ?? "uncleared",
        splits = c.Element("splits"), repeat = c.OptStr("repeat") ?? "never", confirmReconciledEdit = c.Bool("confirmReconciledEdit")
    };

    private static async Task<McpToolOutcome> LedgerWriteAsync(McpToolContext c, Func<long, Task<ApiResponse>> send, string summary)
    {
        var version = await c.LedgerVersionAsync();
        if (version is null) return McpToolOutcome.NotFound("Budget not found.");
        var response = await send(version.Value);
        return Done(response, $"{summary} Ledger version is now {Number(response.Body, "version")}.");
    }

    private static IEnumerable<McpToolDefinition> LedgerTools()
    {
        yield return Tool("accounts_list", "List accounts",
            "Lists accounts with type, balances (working, cleared, uncleared), closed/favorite flags, last reconciliation and attention counts, plus the ledger version.",
            McpPermissions.Read, Effect.Read, Obj(),
            async c =>
            {
                var response = await c.Get("/api/accounts");
                return Done(response, $"{Count(response.Body, "accounts")} account(s); ledger version {Number(response.Body, "version")}.");
            });

        yield return Tool("accounts_create", "Create account", "Creates a cash or credit account with an optional opening balance.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("name", Str("Account name.", 120, 1)), R("accountType", Enum("Account type.", AccountTypes)),
                R("date", Date("Opening balance date YYYY-MM-DD.")), O("openingBalance", Money("Opening balance (negative for credit debt). Default 0.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/accounts", new
            {
                commandId = c.CommandId(), version, name = c.Str("name"), date = c.Str("date"), openingBalance = c.OptMoney("openingBalance") ?? 0m, accountType = c.Str("accountType")
            }), "Account created."));

        yield return Tool("accounts_update", "Update account",
            "Updates an account: rename, close/reopen, change type within cash or credit, favorite, notes, or adjust the working balance (creates an adjustment transaction). Omitted fields keep their current values.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountId", Uuid("Account ID.")), O("name", Str("New name.", 120, 1)), O("isClosed", Bool("Close (true) or reopen (false). Closing requires a zero balance.")),
                O("accountType", Enum("New type (cannot switch between cash and credit).", AccountTypes)), O("isFavorite", Bool("Favorite flag.")),
                O("notes", Str("Account notes.", 1000)), O("workingBalance", Money("Target working balance; the difference is recorded as an adjustment.")), LedgerVersionArg, CommandIdArg),
            async c =>
            {
                var accounts = await c.Get("/api/accounts");
                if (!accounts.IsSuccess || accounts.Body is not { } body) return Done(accounts, "");
                var account = body.GetProperty("accounts").EnumerateArray().FirstOrDefault(x => x.GetProperty("id").GetGuid() == c.Guid("accountId"));
                if (account.ValueKind != JsonValueKind.Object) return McpToolOutcome.NotFound("Account not found.");
                var version = c.OptLong("version") ?? body.GetProperty("version").GetInt64();
                var response = await c.Send("PUT", "/api/accounts/{id}", new
                {
                    commandId = c.CommandId(), version, name = c.OptStr("name") ?? account.GetProperty("name").GetString(),
                    isClosed = c.OptBool("isClosed") ?? account.GetProperty("isClosed").GetBoolean(), accountType = c.OptStr("accountType"),
                    isFavorite = c.OptBool("isFavorite"), notes = c.OptStr("notes"), workingBalance = c.OptMoney("workingBalance")
                }, Route.Of(("id", c.Str("accountId"))));
                return Done(response, $"Account updated. Ledger version is now {Number(response.Body, "version")}.");
            });

        yield return Tool("accounts_reorder", "Reorder accounts",
            "Sets the budget's account order. accountIds must list every account from accounts_list (open and closed) exactly once, first to last.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountIds", Array("Every account ID in the new order.", Uuid("Account ID."), 1000, 1)), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/accounts/order", new { commandId = c.CommandId(), version, accountIds = c.Element("accountIds") }), "Accounts reordered."));

        yield return Tool("accounts_credit_summary", "Credit account monthly summary",
            "Reads a credit account's monthly summary: prior balance, spending, interest, payments and current balance.",
            McpPermissions.Read, Effect.Read, Obj(R("accountId", Uuid("Credit account ID.")), R("month", Month())),
            async c => Done(await c.Get("/api/accounts/{id}/credit-summary/{month}", Route.Of(("id", c.Str("accountId")), ("month", c.Str("month")))), "Credit summary loaded."));

        yield return Tool("reconcile_account", "Reconcile account",
            "Reconciles an account against a statement balance. The cleared balance must equal the statement balance; cleared entries become locked (reconciled).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountId", Uuid("Account ID.")), R("statementBalance", Money("Statement (bank) balance.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/accounts/{id}/reconcile",
                new { commandId = c.CommandId(), version, statementBalance = c.Money("statementBalance") }, Route.Of(("id", c.Str("accountId")))), "Account reconciled."));

        yield return Tool("transactions_list", "List transactions",
            "Lists register transactions newest first with filters and pagination. Each row includes running balance, category name, status, flags and lock state.",
            McpPermissions.Read, Effect.Read,
            Obj(O("accountId", Uuid("Only this account.")), O("transferId", Uuid("Only this transfer pair.")), O("search", Str("Payee/memo search text.", 160)),
                O("from", Date("Earliest date YYYY-MM-DD.")), O("to", Date("Latest date YYYY-MM-DD.")), O("categoryId", Id("Only this category, or 'uncategorized'.")),
                O("status", Enum("Cleared status filter.", "uncleared", "cleared", "reconciled")), O("payeeId", Uuid("Only this payee.")),
                O("includeReconciled", Bool("Include reconciled rows (default true).")), O("needsAttention", Bool("Only rows needing approval or a category.")),
                O("page", Int("Page number, default 1.", 1, 1_000_000)), O("pageSize", Int("Rows per page, default 50.", 1, 100))),
            async c =>
            {
                var response = await c.Get("/api/transactions", query: Query(("accountId", c.OptStr("accountId")), ("transferId", c.OptStr("transferId")),
                    ("search", c.OptStr("search")), ("from", c.OptStr("from")), ("to", c.OptStr("to")), ("categoryId", c.OptStr("categoryId")),
                    ("status", c.OptStr("status")), ("payeeId", c.OptStr("payeeId")), ("includeReconciled", c.OptBool("includeReconciled")?.ToString().ToLowerInvariant()),
                    ("needsAttention", c.OptBool("needsAttention")?.ToString().ToLowerInvariant()), ("page", c.OptInt("page")?.ToString()), ("pageSize", c.OptInt("pageSize")?.ToString())));
                return Done(response, $"{Count(response.Body, "items")} of {Number(response.Body, "total")} transaction(s); ledger version {Number(response.Body, "version")}.");
            });

        yield return Tool("transactions_create", "Create transaction", "Records a categorized, split or uncategorized transaction in an account. " +
            "With wishlistItemId it records a Wishlist purchase: an outflow in that item's bucket category (wl-{bucketId}) that also marks the active item purchased (requires the wishlist entitlement). " +
            "Payee and memo are stored as given (the UI convention is memo = item name, payee = the store); an already purchased or removed item is rejected.",
            McpPermissions.AdminOnly, Effect.Write, Obj([.. TransactionFields(), O("wishlistItemId", Uuid("Active Wishlist item bought by this outflow (optional).")), LedgerVersionArg, CommandIdArg]),
            c => LedgerWriteAsync(c, version => c.OptStr("wishlistItemId") is { } itemId
                ? c.Send("POST", "/api/budgets/{id}/wishlist/items/{itemId}/purchase-transaction", TransactionBody(c, version), Route.Of(("id", c.BudgetId!), ("itemId", itemId)))
                : c.Send("POST", "/api/transactions", TransactionBody(c, version)), "Transaction created."));

        yield return Tool("transactions_import", "Import transaction",
            "Records an imported transaction (marked for approval and subject to payee renaming rules), as file import does.",
            McpPermissions.AdminOnly, Effect.Write, Obj([.. TransactionFields(), LedgerVersionArg, CommandIdArg]),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/import", TransactionBody(c, version)), "Transaction imported."));

        yield return Tool("transactions_update", "Update transaction",
            "Replaces an existing transaction with the supplied values (read it with transactions_list first and send every field you want to keep). Reconciled entries need confirmReconciledEdit=true.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj([R("transactionId", Uuid("Transaction ID.")), .. TransactionFields(), O("confirmReconciledEdit", Bool("Allow editing a reconciled entry (invalidates that reconciliation).")), LedgerVersionArg, CommandIdArg]),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/transactions/{id}", TransactionBody(c, version), Route.Of(("id", c.Str("transactionId")))), "Transaction updated."));

        yield return Tool("transactions_delete", "Delete transaction", "Deletes a transaction (both legs for a transfer). Reconciled entries cannot be deleted.",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("transactionId", Uuid("Transaction ID.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/{id}/delete", new { commandId = c.CommandId(), version },
                Route.Of(("id", c.Str("transactionId")))), "Transaction deleted."));

        yield return Tool("transactions_set_status", "Set transaction cleared status", "Marks a transaction cleared or uncleared.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("transactionId", Uuid("Transaction ID.")), R("status", Enum("New status.", "uncleared", "cleared")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/{id}/status", new { commandId = c.CommandId(), version, status = c.Str("status") },
                Route.Of(("id", c.Str("transactionId")))), "Transaction status updated."));

        yield return Tool("transactions_review", "Approve or reject transaction", "Approves an unapproved (imported/auto-posted) transaction, or rejects it (deletes it).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("transactionId", Uuid("Transaction ID.")), R("action", Enum("approve or reject (reject deletes the entry).", "approve", "reject")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/{id}/review", new { commandId = c.CommandId(), version, action = c.Str("action") },
                Route.Of(("id", c.Str("transactionId")))), $"Transaction {(c.Str("action") == "approve" ? "approved" : "rejected")}."),
            confirmWhen: args => args.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String && action.GetString() == "reject");

        yield return Tool("transactions_bulk_update", "Bulk update transactions",
            "Applies one action to 1-100 selected transactions: status, flag, memo, move (to accountId), duplicate, repeat or delete.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("ids", Array("Transaction IDs (distinct).", Uuid("Transaction ID."), 100, 1)),
                R("action", Enum("Action to apply.", "status", "flag", "memo", "move", "duplicate", "repeat", "delete")),
                O("status", Enum("For action=status.", "uncleared", "cleared")), O("color", Nullable(Enum("For action=flag; null clears.", FlagColors))),
                O("memo", Str("For action=memo.", 1000)), O("accountId", Uuid("For action=move: destination account.")),
                O("repeat", Enum("For action=repeat.", RecurrenceRules.Repeats.Where(x => x != "never").ToArray())),
                O("confirmReconciledEdit", Bool("Allow changing reconciled entries.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/selection", new
            {
                commandId = c.CommandId(), version, ids = c.Element("ids"), action = c.Str("action"), status = c.OptStr("status"), color = c.OptStr("color"),
                memo = c.OptStr("memo"), accountId = c.OptGuid("accountId"), repeat = c.OptStr("repeat"), confirmReconciledEdit = c.Bool("confirmReconciledEdit")
            }), $"Applied {c.Str("action")} to the selected transactions."),
            confirmWhen: args => args.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String && action.GetString() == "delete");

        yield return Tool("transactions_set_flag", "Flag transaction", "Sets or clears a transaction's colored flag.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("transactionId", Uuid("Transaction ID.")), R("color", Nullable(Enum("Flag color; null clears.", FlagColors))), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transactions/{id}/flag", new { commandId = c.CommandId(), version, color = c.OptStr("color") },
                Route.Of(("id", c.Str("transactionId")))), "Transaction flag updated."));

        yield return Tool("transfers_create", "Create transfer", "Moves money between two open accounts as a paired, budget-neutral transfer.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("fromAccountId", Uuid("Source account.")), R("toAccountId", Uuid("Destination account.")), R("date", Date("Transfer date YYYY-MM-DD.")),
                R("amount", Money("Positive amount.", 0.01m)), O("memo", Str("Memo.", 1000)), O("repeat", Enum("Also schedule repeats. Default never.", RecurrenceRules.Repeats)),
                LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/transfers", new
            {
                commandId = c.CommandId(), version, fromAccountId = c.Guid("fromAccountId"), toAccountId = c.Guid("toAccountId"), date = c.Str("date"),
                amount = c.Money("amount"), memo = c.OptStr("memo"), repeat = c.OptStr("repeat") ?? "never"
            }), "Transfer created."));

        yield return Tool("transfers_update", "Update transfer", "Replaces a transfer pair's accounts, date, amount and memo. Pass the ID of either transfer leg.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("transactionId", Uuid("ID of either transfer leg.")), R("fromAccountId", Uuid("Source account.")), R("toAccountId", Uuid("Destination account.")),
                R("date", Date("Transfer date YYYY-MM-DD.")), R("amount", Money("Positive amount.", 0.01m)), O("memo", Str("Memo.", 1000)),
                O("confirmReconciledEdit", Bool("Allow editing reconciled legs.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/transfers/{id}", new
            {
                commandId = c.CommandId(), version, fromAccountId = c.Guid("fromAccountId"), toAccountId = c.Guid("toAccountId"), date = c.Str("date"),
                amount = c.Money("amount"), memo = c.OptStr("memo"), repeat = "never", confirmReconciledEdit = c.Bool("confirmReconciledEdit")
            }, Route.Of(("id", c.Str("transactionId")))), "Transfer updated."));

        yield return Tool("ledger_audit_list", "Ledger audit history",
            "Lists ledger command receipts (the financial audit trail: kind, version, timestamp and before/after snapshots), newest first.",
            McpPermissions.Read, Effect.Read,
            Obj(O("resourceId", Uuid("Only receipts for this account/transaction/transfer.")), O("page", Int("Page, default 1.", 1, 1_000_000)), O("pageSize", Int("Rows per page, default 50.", 1, 100))),
            async c =>
            {
                var response = await c.Get("/api/ledger/audit", query: Query(("resourceId", c.OptStr("resourceId")), ("page", c.OptInt("page")?.ToString()), ("pageSize", c.OptInt("pageSize")?.ToString())));
                return Done(response, $"{Count(response.Body, "items")} audit receipt(s).");
            });

        yield return Tool("payees_list", "List payees", "Lists payees with hidden flag, categorization mode, default category, renaming rules and transaction counts, plus the ledger version.",
            McpPermissions.Read, Effect.Read, Obj(),
            async c => { var response = await c.Get("/api/payees/manage"); return Done(response, $"{Count(response.Body, "payees")} payee(s)."); });

        yield return Tool("payees_save", "Update payees",
            "Updates payees: rename, hide, set categorization (history, always with defaultCategoryId, never), renaming rules, or delete (delete=true).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("payees", Array("Payee changes.", Obj("One payee.",
                    R("id", Uuid("Payee ID from payees_list.")), R("name", Str("Payee name.", 160, 1)), R("isHidden", Bool("Hide from suggestions.")),
                    R("categoryMode", Enum("Automatic categorization.", "history", "always", "never")), O("defaultCategoryId", Nullable(Id("Category for categoryMode=always."))),
                    R("rules", Array("Import renaming rules.", Obj(R("operator", Enum("Match operator.", "is", "contains")), R("value", Str("Text to match.", 160, 1))), 50)),
                    O("delete", Bool("Delete this payee."))), 200, 1)), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/payees/manage", new { commandId = c.CommandId(), version, payees = c.Element("payees") }), "Payees updated."),
            confirmWhen: args => args.TryGetProperty("payees", out var payees) && payees.ValueKind == JsonValueKind.Array &&
                payees.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("delete", out var delete) && delete.ValueKind == JsonValueKind.True));

        yield return Tool("flags_list", "List flags", "Lists the six flag colors and their names.",
            McpPermissions.Read, Effect.Read, Obj(), async c => Done(await c.Get("/api/flags"), "Flag palette loaded."));

        yield return Tool("flags_rename", "Rename flags", "Renames the flag colors. Send all six colors (red, orange, yellow, green, blue, purple) with their names.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("flags", Array("Flags to rename.", Obj(R("color", Enum("Flag color.", FlagColors)), R("name", Str("Display name.", 40, 1))), 6, 6)), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/flags", new { commandId = c.CommandId(), version, flags = c.Element("flags") }), "Flags renamed."));

        yield return Tool("credit_payoff_target_get", "Get credit payoff target", "Reads a credit account's payoff target and progress for a month.",
            McpPermissions.Read, Effect.Read, Obj(R("accountId", Uuid("Credit account ID.")), R("month", Month())),
            async c => Done(await c.Get("/api/accounts/{id}/payoff-target/{month}", Route.Of(("id", c.Str("accountId")), ("month", c.Str("month")))), "Credit payoff target loaded."));

        yield return Tool("credit_payoff_target_save", "Save credit payoff target",
            "Creates or replaces a credit account payoff target: payByDate (needs targetMonth) or monthlyAmount (needs monthlyAmount).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountId", Uuid("Credit account ID.")), R("kind", Enum("Target type.", "payByDate", "monthlyAmount")),
                O("targetMonth", Month("Pay-off month for payByDate (YYYY-MM).")), O("monthlyAmount", Money("Monthly payment for monthlyAmount.", 0.01m)), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/accounts/{id}/payoff-target", new
            {
                commandId = c.CommandId(), version, kind = c.Str("kind"), targetMonth = c.OptStr("targetMonth"), monthlyAmount = c.OptMoney("monthlyAmount")
            }, Route.Of(("id", c.Str("accountId")))), "Credit payoff target saved."));

        yield return Tool("credit_payoff_target_delete", "Delete credit payoff target", "Removes a credit account's payoff target.",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("accountId", Uuid("Credit account ID.")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/accounts/{id}/payoff-target/delete", new { commandId = c.CommandId(), version },
                Route.Of(("id", c.Str("accountId")))), "Credit payoff target deleted."));
    }

    private static JsonObject TemplateSchema() => Obj("Occurrence details.",
        R("amount", Money("Amount (negative for outflow).")), O("payee", Str("Payee.", 160)), O("memo", Str("Memo.", 1000)),
        O("categoryId", Id("Category ID.")), O("splits", Array("Split lines.", SplitSchema(), 100)));

    private static (string, JsonObject, bool)[] ScheduleFields() =>
    [
        R("accountId", Uuid("Account ID.")), R("date", Date("First/next date YYYY-MM-DD.")), R("amount", Money("Amount: negative for outflows.")),
        O("payee", Str("Payee.", 160)), O("memo", Str("Memo.", 1000)), O("categoryId", Id("Category ID.")),
        O("splits", Array("Split lines.", SplitSchema(), 100)), R("repeat", Enum("Frequency.", RecurrenceRules.Repeats)),
        O("toAccountId", Uuid("Destination account for a scheduled transfer.")), O("isPaused", Bool("Create/keep the schedule paused.")),
        O("endDate", Date("Optional last date YYYY-MM-DD.")), O("count", Int("Optional number of occurrences.", 1, 10000)),
        O("flagColor", Nullable(Enum("Flag color for posted entries.", FlagColors))), LedgerVersionArg, CommandIdArg
    ];

    private static object ScheduleBody(McpToolContext c, long version) => new
    {
        commandId = c.CommandId(), version, accountId = c.Guid("accountId"), date = c.Str("date"), amount = c.Money("amount"), payee = c.OptStr("payee"),
        memo = c.OptStr("memo"), categoryId = c.OptStr("categoryId"), splits = c.Element("splits"), repeat = c.Str("repeat"), toAccountId = c.OptGuid("toAccountId"),
        isPaused = c.Bool("isPaused"), endDate = c.OptStr("endDate"), count = c.OptInt("count"), flagColor = c.OptStr("flagColor")
    };

    private static IEnumerable<McpToolDefinition> ScheduleTools()
    {
        yield return Tool("schedules_list", "List scheduled transactions", "Lists upcoming scheduled transactions (next occurrence per schedule) with filters and pagination.",
            McpPermissions.Read, Effect.Read,
            Obj(O("accountId", Uuid("Only this account.")), O("from", Date("Earliest date.")), O("to", Date("Latest date.")),
                O("page", Int("Page, default 1.", 1, 1_000_000)), O("pageSize", Int("Rows per page, default 25.", 1, 100))),
            async c =>
            {
                var response = await c.Get("/api/schedules", query: Query(("accountId", c.OptStr("accountId")), ("from", c.OptStr("from")), ("to", c.OptStr("to")),
                    ("page", c.OptInt("page")?.ToString()), ("pageSize", c.OptInt("pageSize")?.ToString())));
                return Done(response, $"{Count(response.Body, "items")} of {Number(response.Body, "total")} scheduled transaction(s).");
            });

        yield return Tool("schedules_create", "Create scheduled transaction", "Creates a repeating or one-off scheduled transaction (or transfer with toAccountId).",
            McpPermissions.AdminOnly, Effect.Write, Obj(ScheduleFields()),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/schedules", ScheduleBody(c, version)), "Scheduled transaction created."));

        yield return Tool("schedules_update", "Update scheduled transaction", "Replaces a schedule's rule and template with the supplied values.",
            McpPermissions.AdminOnly, Effect.Write, Obj([R("scheduleId", Uuid("Schedule ID.")), .. ScheduleFields()]),
            c => LedgerWriteAsync(c, version => c.Send("PUT", "/api/schedules/{id}", ScheduleBody(c, version), Route.Of(("id", c.Str("scheduleId")))), "Scheduled transaction updated."));

        yield return Tool("schedules_action", "Act on scheduled occurrence",
            "Acts on a schedule's next occurrence: pause, resume, end (stop repeating), snooze (to date), editOnce (template + optional date), skip, or post now (optional date).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("scheduleId", Uuid("Schedule ID.")), R("occurrenceId", Uuid("Next occurrence ID from schedules_list.")),
                R("action", Enum("Action.", "pause", "resume", "end", "snooze", "editOnce", "skip", "post")),
                O("date", Date("Snooze/edit/post date YYYY-MM-DD.")), O("template", TemplateSchema()), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/schedules/{id}/actions", new
            {
                commandId = c.CommandId(), version, action = c.Str("action"), occurrenceId = c.Guid("occurrenceId"), date = c.OptStr("date"), template = c.Element("template")
            }, Route.Of(("id", c.Str("scheduleId")))), $"Schedule action {c.Str("action")} applied."),
            confirmWhen: args => args.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String && action.GetString() is "end" or "skip");

        yield return Tool("schedules_set_flag", "Flag scheduled transaction", "Sets or clears a schedule's flag color.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("scheduleId", Uuid("Schedule ID.")), R("color", Nullable(Enum("Flag color; null clears.", FlagColors))), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/schedules/{id}/flag", new { commandId = c.CommandId(), version, color = c.OptStr("color") },
                Route.Of(("id", c.Str("scheduleId")))), "Schedule flag updated."));

        yield return Tool("schedules_process_due", "Post due scheduled transactions", "Posts every scheduled occurrence that is due today or earlier (as the background worker does).",
            McpPermissions.AdminOnly, Effect.Write, Obj(),
            async c =>
            {
                var response = await c.Send("POST", "/api/schedules/process-due", null);
                return Done(response, $"Posted {(response.Body is { ValueKind: JsonValueKind.Number } n ? n.GetInt32() : 0)} due occurrence(s).",
                    ToElement(new { posted = response.Body is { ValueKind: JsonValueKind.Number } count ? count.GetInt32() : 0 }));
            });
    }

    private static IEnumerable<McpToolDefinition> PlanningTools()
    {
        yield return Tool("long_term_planning_get_year", "Get long-term plan", "Reads a year of long-term planning: monthly expected income/expenses and per-category planned and actual values.",
            McpPermissions.Read, Effect.Read, Obj(R("year", Int("Calendar year.", 1900, 9998))),
            async c => Done(await c.Get("/api/long-term-planning/{year}", Route.Of(("year", c.OptInt("year")!.Value.ToString()))), $"Long-term plan for {c.OptInt("year")} loaded."));

        yield return Tool("long_term_planning_get_activity", "Get planned activity", "Lists scheduled (planned) transactions contributing to a long-term planning month.",
            McpPermissions.Read, Effect.Read, Obj(R("month", Month())),
            async c => Done(await c.Get("/api/long-term-planning/{year}/activity/{month}", Route.Of(("year", c.Str("month")[..4]), ("month", c.Str("month")))), "Planned activity loaded."));

        yield return Tool("long_term_planning_set_override", "Set planned budget override",
            "Sets (or clears with amount=null) a category's planned budget override for a future long-term planning month.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("categoryId", Id("Category ID.")), R("amount", Nullable(Money("Planned amount; null clears the override.", 0m))),
                O("version", Int("Expected long-term planning version; omit to use the current one.", 1, long.MaxValue))),
            async c =>
            {
                var year = c.Str("month")[..4];
                var version = c.OptLong("version");
                if (version is null)
                {
                    var current = await c.Get("/api/long-term-planning/{year}", Route.Of(("year", year)));
                    if (!current.IsSuccess) return Done(current, "");
                    version = Number(current.Body, "version");
                }
                var response = await c.Send("PUT", "/api/long-term-planning/{year}", new
                {
                    version, month = c.Str("month"), field = "plannedBudgetOverride", categoryId = c.Str("categoryId"), amount = c.OptMoney("amount")
                }, Route.Of(("year", year)));
                return Done(response, $"Planned budget override updated (version {Number(response.Body, "version")}).");
            });
    }
}
