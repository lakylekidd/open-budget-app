using System.Text.Json;
using System.Text.Json.Nodes;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.Api.Mcp;

// Account types, loan/contract terms and the loan account tools. Account types mirror the web app's "Add Account" list:
// cash and credit accounts take a signed opening balance; mortgages, loans and contracts take loan details instead.
public static partial class McpToolCatalog
{
    private static readonly string[] CashAccountTypes = ["checking", "savings", "cash"];
    private static readonly string[] AccountTypes = [.. CashAccountTypes, .. CreditCardRules.CreditTypes, .. LoanRules.LoanTypes];

    private static readonly string AccountTypeHelp =
        $"Cash: {string.Join(", ", CashAccountTypes)}. Credit: {string.Join(", ", CreditCardRules.CreditTypes)}. " +
        $"Mortgages and loans (require 'loan'): {string.Join(", ", LoanRules.LoanTypes)}.";

    private static JsonObject NewPairedCategorySchema(string description) => Obj(description,
        R("name", Str("New category name.", LoanRules.MaxCategoryNameLength, 1)),
        O("groupId", Id("Existing active category group ID (from plan_get_month). Set exactly one of groupId or newGroupName.")),
        O("newGroupName", Str("Name of a new category group to create. Set exactly one of groupId or newGroupName.", LoanRules.MaxGroupNameLength, 1)));

    private static JsonObject ContractSchema() => Obj("Contract (phone/device) details. Required for accountType=contract; not allowed for other types. Contracts are interest-free.",
        R("months", Int("Contract length in months.", 1, LoanRules.MaxContractMonths)),
        R("startDate", Date("Contract start date YYYY-MM-DD.")),
        R("includesDevice", Bool("True when the contract includes a device paid off over the contract.")),
        O("upfrontCost", Money("Upfront cost paid at signing. Default 0.", 0m)),
        O("monthlyServiceCost", Money("Regular monthly contract (service) cost. Default 0.", 0m)),
        O("deviceTotal", Money("Device price spread over the contract (only when includesDevice). Default 0.", 0m)),
        O("discountMonths", Int("Number of first months at a discounted service cost. Default 0.", 0, LoanRules.MaxContractMonths)),
        O("discountedServiceCost", Nullable(Money("Discounted monthly service cost; required when discountMonths > 0.", 0m))));

    /// <summary>Loan terms as the web app's loan forms collect them. <paramref name="create"/> adds the opening balance owed.</summary>
    private static JsonObject LoanSchema(bool create)
    {
        var fields = new List<(string, JsonObject, bool)>();
        if (create)
            fields.Add(O("balanceOwed", Money("Current balance you owe as a positive amount; it is recorded as debt (a negative opening balance). Required for every loan type except contract.", 0.01m)));
        fields.AddRange([
            O("interestRate", Decimal("Annual interest rate in percent (0-100, at most three decimals), e.g. 4.25. Required for loans; omit for contracts.", 0m, 100m)),
            O("minimumPayment", Money("Monthly payment required by the lender, including principal, interest, escrow and fees. Required (> 0) for loans; omit for contracts.", 0m)),
            O("escrowAmount", Money("Part of the monthly payment that is escrow or fees (must be less than minimumPayment). Default 0.", 0m)),
            O("contract", ContractSchema()),
            O("pairedCategoryId", Id("Existing active Plan category to pair with this loan (payments spend from it). Omit for no pairing.")),
            O("newPairedCategory", NewPairedCategorySchema("Create a new Plan category and pair it (instead of pairedCategoryId).")),
            O("separateDeviceCategory", Bool("Contracts that include a device: use a separate category for the device share of each payment.")),
            O("devicePairedCategoryId", Id("Existing category for the device share (requires separateDeviceCategory).")),
            O("newDevicePairedCategory", NewPairedCategorySchema("Create a new category for the device share (requires separateDeviceCategory).")),
        ]);
        return Obj($"Loan details. Required when accountType is a loan type ({string.Join(", ", LoanRules.LoanTypes)}); not allowed for cash or credit accounts. " +
            "Loans need interestRate and minimumPayment (and balanceOwed when creating); contracts need contract and no interest or payment. There is no separate term field: the payoff date is projected from the payment (see loans_get / loans_simulate).",
            [.. fields]);
    }

    /// <summary>
    /// Checks the account type / loan details combination before anything is written, mirroring the web app's forms
    /// (which only offer loan fields for loan types and require balance, rate and payment for loans). Services validate again.
    /// </summary>
    private static string? ValidateAccountTypeArguments(string accountType, JsonElement? loan, bool create, bool hasOpeningBalance)
    {
        var isLoan = LoanRules.IsLoan(accountType);
        if (!isLoan)
            return loan is null ? null : $"'loan' is only allowed for loan account types ({string.Join(", ", LoanRules.LoanTypes)}); '{accountType}' is a {LoanRules.Family(accountType)} account.";
        if (create && hasOpeningBalance)
            return $"Loan account types do not take openingBalance; pass the amount owed as loan.balanceOwed (positive){(LoanRules.IsContract(accountType) ? " â€” for contracts the balance is calculated from loan.contract" : "")}.";
        if (loan is not { } terms)
            return create
                ? $"accountType '{accountType}' is a loan type and requires 'loan' details: " +
                  (LoanRules.IsContract(accountType) ? "loan.contract (months, startDate, includesDevice and costs)." : "loan.balanceOwed (positive amount owed), loan.interestRate (annual %) and loan.minimumPayment (monthly payment).")
                : null;
        bool Has(string name) => terms.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null;
        if (LoanRules.IsContract(accountType))
        {
            if (!Has("contract")) return "Contract accounts require loan.contract (months, startDate, includesDevice and costs).";
            if (Has("balanceOwed") || Has("interestRate") || Has("minimumPayment") || Has("escrowAmount"))
                return "Contracts are interest-free and their balance and payments come from loan.contract; omit balanceOwed, interestRate, minimumPayment and escrowAmount.";
            return null;
        }
        if (Has("contract")) return "Only accountType 'contract' takes loan.contract.";
        var missing = new[] { create ? "balanceOwed" : null, "interestRate", "minimumPayment" }.OfType<string>().Where(name => !Has(name)).ToList();
        if (missing.Count > 0) return $"Loan accounts require {string.Join(", ", missing.Select(name => "loan." + name))}.";
        if (terms.GetProperty("minimumPayment").GetDecimal() <= 0) return "loan.minimumPayment must be greater than 0 (the monthly payment required by your lender).";
        return null;
    }

    /// <summary>Maps validated MCP loan arguments to the API's LoanTermsRequest shape.</summary>
    private static object? LoanTermsBody(JsonElement? loan)
    {
        if (loan is not { } terms) return null;
        JsonElement? Get(string name) => terms.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;
        return new
        {
            interestRate = Get("interestRate")?.GetDecimal() ?? 0m,
            minimumPayment = Get("minimumPayment")?.GetDecimal() ?? 0m,
            escrowAmount = Get("escrowAmount")?.GetDecimal() ?? 0m,
            pairedCategoryId = Get("pairedCategoryId")?.GetString(),
            contract = Get("contract"),
            newPairedCategory = Get("newPairedCategory"),
            separateDeviceCategory = Get("separateDeviceCategory")?.GetBoolean() ?? false,
            devicePairedCategoryId = Get("devicePairedCategoryId")?.GetString(),
            newDevicePairedCategory = Get("newDevicePairedCategory"),
        };
    }

    private static decimal OpeningBalanceFor(McpToolContext c, string accountType)
    {
        if (!LoanRules.IsLoan(accountType)) return c.OptMoney("openingBalance") ?? 0m;
        if (LoanRules.IsContract(accountType)) return 0m;
        return -c.Element("loan")!.Value.GetProperty("balanceOwed").GetDecimal();
    }

    private static IEnumerable<McpToolDefinition> LoanTools()
    {
        yield return Tool("loans_get", "Get loan details",
            "Reads a mortgage, loan or contract account: balance, interest rate, monthly payment, escrow, original balance, % remaining, paired categories, " +
            "payoff projection, balance history, monthly activity and (contracts) the payment schedule.",
            McpPermissions.Read, Effect.Read, Obj(R("accountId", Uuid("Loan account ID from accounts_list."))),
            async c => Done(await c.Get("/api/accounts/{id}/loan", Route.Of(("id", c.Str("accountId")))), "Loan details loaded."));

        yield return Tool("loans_simulate", "Simulate loan payoff",
            "Read-only payoff simulation for a loan (not contracts): pass a monthly payment or a payoff month (not both), plus an optional one-time extra payment this month. Nothing is saved.",
            McpPermissions.Read, Effect.Read,
            Obj(R("accountId", Uuid("Loan account ID.")), O("payment", Money("Monthly payment to simulate (including escrow/fees).", 0m)),
                O("payoffMonth", Month("Target payoff month YYYY-MM; solves for the payment.")), O("extra", Money("One-time extra payment this month.", 0m))),
            async c =>
            {
                if (c.Has("payment") && c.Has("payoffMonth")) return McpToolOutcome.Invalid("Choose either payment or payoffMonth, not both.");
                var response = await c.Get("/api/accounts/{id}/loan/simulation", Route.Of(("id", c.Str("accountId"))),
                    Query(("payment", c.OptMoney("payment")?.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("payoffMonth", c.OptStr("payoffMonth")),
                        ("extra", c.OptMoney("extra")?.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                return Done(response, "Loan payoff simulated.");
            });

        yield return Tool("loans_pairings_list", "List loan category pairings",
            "Lists every Plan category paired with a loan or contract (one category can be paired with only one loan slot).",
            McpPermissions.Read, Effect.Read, Obj(),
            async c => { var response = await c.Get("/api/loan-pairings"); return Done(response, $"{Count(response.Body, "items")} loan pairing(s)."); });

        yield return Tool("loans_save_terms", "Save loan terms",
            "Replaces a loan or contract account's terms and category pairing (send every field you want to keep; read loans_get first).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountId", Uuid("Loan account ID.")), R("loan", LoanSchema(create: false)), LedgerVersionArg, CommandIdArg),
            async c =>
            {
                var accounts = await c.Get("/api/accounts");
                if (!accounts.IsSuccess || accounts.Body is not { } body) return Done(accounts, "");
                var account = body.GetProperty("accounts").EnumerateArray().FirstOrDefault(x => x.GetProperty("id").GetGuid() == c.Guid("accountId"));
                if (account.ValueKind != JsonValueKind.Object) return McpToolOutcome.NotFound("Account not found.");
                var type = account.GetProperty("accountType").GetString()!;
                if (!LoanRules.IsLoan(type)) return McpToolOutcome.Invalid($"Only loan accounts ({string.Join(", ", LoanRules.LoanTypes)}) have loan terms; this account is '{type}'.");
                if (ValidateAccountTypeArguments(type, c.Element("loan"), create: false, hasOpeningBalance: false) is { } error) return McpToolOutcome.Invalid(error);
                var version = c.OptLong("version") ?? body.GetProperty("version").GetInt64();
                var response = await c.Send("PUT", "/api/accounts/{id}/loan", new { commandId = c.CommandId(), version, terms = LoanTermsBody(c.Element("loan")) },
                    Route.Of(("id", c.Str("accountId"))));
                return Done(response, $"Loan terms saved. Ledger version is now {Number(response.Body, "version")}.");
            });

        yield return Tool("loans_confirm_balance", "Confirm loan balance",
            "Confirms a loan's balance at the lender. Without balance the current balance is confirmed; with a different balance (negative = owed, as accounts_list shows it) a balance adjustment transaction is recorded.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("accountId", Uuid("Loan account ID.")), O("balance", Money("Lender balance as a ledger balance (negative for money owed).")), LedgerVersionArg, CommandIdArg),
            c => LedgerWriteAsync(c, version => c.Send("POST", "/api/accounts/{id}/loan/balance", new { commandId = c.CommandId(), version, balance = c.OptMoney("balance") },
                Route.Of(("id", c.Str("accountId")))), "Loan balance confirmed."));

        yield return Tool("accounts_credit_activity", "Credit account monthly activity",
            "Reads a credit card's categorized activity for a month: spending, returns, funded spending, payments and total activity.",
            McpPermissions.Read, Effect.Read, Obj(R("accountId", Uuid("Credit account ID.")), R("month", Month())),
            async c => Done(await c.Get("/api/accounts/{id}/credit-activity/{month}", Route.Of(("id", c.Str("accountId")), ("month", c.Str("month")))), "Credit activity loaded."));
    }
}
