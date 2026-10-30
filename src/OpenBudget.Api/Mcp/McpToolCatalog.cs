using System.Text.Json;
using System.Text.Json.Nodes;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.Api.Mcp;

/// <summary>
/// All OpenBudget MCP tools. Tools never touch the database directly: each one dispatches to the same minimal-API
/// endpoint the web app uses (see <see cref="ApiDispatcher"/>), so validation, LedgerGate locking, optimistic
/// versions, command receipts and audit trails are identical.
/// </summary>
public static partial class McpToolCatalog
{
    private enum Effect { Read, Write, Destructive }

    private static readonly Lazy<IReadOnlyList<McpToolDefinition>> tools = new(() =>
    {
        var all = BudgetTools().Concat(PlanTools()).Concat(LedgerTools()).Concat(ScheduleTools()).Concat(PlanningTools())
            .Concat(SharingTools()).Concat(AdminTools()).ToList();
        var duplicate = all.GroupBy(x => x.Name).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate MCP tool {duplicate.Key}.");
        return all;
    });

    public static IReadOnlyList<McpToolDefinition> All => tools.Value;

    public static McpToolDefinition? Find(string? name) => name is null ? null : All.FirstOrDefault(x => x.Name == name);

    public static IEnumerable<McpToolDefinition> For(AgentPermission permission) => All.Where(x => McpPermissions.CanUse(permission, x.Permissions));

    private static (string, JsonObject, bool) CommandIdArg => O("commandId",
        Uuid("Optional idempotency key. Reuse the same value to safely retry an identical write after a lost response; omit to generate one."));
    private static (string, JsonObject, bool) LedgerVersionArg => O("version",
        Int("Expected ledger version from your last read (accounts_list/transactions_list 'version'). Omit to use the current version. A stale value returns a conflict.", 1, long.MaxValue));

    private static McpToolDefinition Tool(string name, string title, string description, IReadOnlySet<AgentPermission> permissions, Effect effect,
        JsonObject schema, Func<McpToolContext, Task<McpToolOutcome>> handler, bool budgetScoped = true, Func<JsonElement, bool>? confirmWhen = null, bool idempotent = false)
    {
        var properties = (JsonObject)schema["properties"]!;
        if (budgetScoped)
        {
            var ordered = new JsonObject { ["budgetId"] = BudgetId() };
            foreach (var (key, value) in properties.ToList()) { properties.Remove(key); ordered[key] = value; }
            schema["properties"] = ordered;
            properties = ordered;
            var required = schema["required"] as JsonArray ?? [];
            schema.Remove("required");
            schema["required"] = new JsonArray(new JsonNode?[] { "budgetId" }.Concat(required.Select(x => (JsonNode?)x!.GetValue<string>())).ToArray());
        }
        var needsConfirm = effect == Effect.Destructive || confirmWhen is not null;
        if (needsConfirm)
            properties["confirm"] = Bool("Must be true to perform this destructive action. Only set it after the user explicitly confirmed this specific change.");
        var suffix = $" Requires a {McpPermissions.Describe(permissions)} token.";
        if (effect == Effect.Destructive) suffix += " Destructive: requires confirm=true.";
        else if (confirmWhen is not null) suffix += " Some options are destructive and then require confirm=true.";
        return new McpToolDefinition(name, title, description + suffix, permissions, budgetScoped, effect == Effect.Read,
            effect == Effect.Destructive || confirmWhen is not null, schema, handler, idempotent || effect == Effect.Read,
            confirmWhen is null ? null : confirmWhen);
    }

    private static McpToolOutcome Done(ApiResponse response, string summary, JsonElement? result = null) => new(response, summary, result);

    private static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value, JsonSerializerOptions.Web);

    private static int Count(JsonElement? body, string property) =>
        body is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array ? items.GetArrayLength() : 0;

    private static int Count(JsonElement? body) => body is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;

    private static string? Text(JsonElement? body, string property) =>
        body is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;

    private static long? Number(JsonElement? body, string property) =>
        body is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.Number ? item.GetInt64() : null;

    private static Dictionary<string, string?> Query(params (string Key, string? Value)[] values) =>
        values.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value);

    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- Budgets

    private static IEnumerable<McpToolDefinition> BudgetTools()
    {
        yield return Tool("budgets_list", "List budgets",
            "Lists the budgets this token can access (id, name, currency, version, archived flag and your role). Use a budget id from here for every budget-scoped tool.",
            McpPermissions.All, Effect.Read,
            Obj(O("includeArchived", Bool("Include archived budgets (Admin tokens only)."))),
            async c =>
            {
                var includeArchived = c.Token.Permission == AgentPermission.Admin && c.Bool("includeArchived");
                var response = await c.Get("/api/budgets", query: Query(("includeArchived", includeArchived ? "true" : "false")));
                if (!response.IsSuccess || response.Body is not { } body) return Done(response, "");
                var budgets = body.GetProperty("budgets").EnumerateArray()
                    .Where(x => McpPermissions.BudgetInTokenScope(c.Token.Permission, c.Token.BudgetIds, x.GetProperty("id").GetString()!))
                    .Select(x => x.Clone()).ToList();
                object result = c.Token.Permission == AgentPermission.Admin ? new { budgets, limit = body.GetProperty("limit") } : new { budgets };
                return Done(response, $"{budgets.Count} budget(s) available.", ToElement(result));
            }, budgetScoped: false);

        yield return Tool("budgets_create", "Create budget", "Creates a new budget owned by the token creator, subject to the membership budget limit.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("name", Str("Budget name.", 120, 1)), O("currency", Str("ISO 4217 currency code, default EUR.", 3, 3, "^[A-Z]{3}$"))),
            async c =>
            {
                var response = await c.Send("POST", "/api/budgets", new { name = c.Str("name"), currency = c.OptStr("currency") ?? "EUR" });
                return Done(response, $"Created budget {Text(response.Body, "id")}.");
            }, budgetScoped: false);

        yield return Tool("budgets_rename", "Rename budget", "Renames a budget the token creator owns.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("budgetId", BudgetId()), R("name", Str("New budget name.", 120, 1)), O("version", Int("Expected budget version; omit to use the current one.", 1, long.MaxValue))),
            async c =>
            {
                var version = c.OptLong("version") ?? await BudgetVersionAsync(c, c.Str("budgetId"));
                if (version is null) return McpToolOutcome.NotFound("Budget not found.");
                var response = await c.Send("PUT", "/api/budgets/{id}", new { name = c.Str("name"), version }, Route.Of(("id", c.Str("budgetId"))));
                return Done(response, "Budget renamed.");
            }, budgetScoped: false);

        yield return Tool("budgets_archive", "Archive budget", "Archives (hides) a budget the token creator owns. It can be restored later.",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("budgetId", BudgetId())),
            async c => Done(await c.Send("POST", "/api/budgets/{id}/archive", null, Route.Of(("id", c.Str("budgetId")))), "Budget archived."), budgetScoped: false);

        yield return Tool("budgets_restore", "Restore budget", "Restores an archived budget the token creator owns, subject to the active budget limit.",
            McpPermissions.AdminOnly, Effect.Write, Obj(R("budgetId", BudgetId())),
            async c => Done(await c.Send("POST", "/api/budgets/{id}/restore", null, Route.Of(("id", c.Str("budgetId")))), "Budget restored."), budgetScoped: false);

        yield return Tool("budgets_delete", "Delete budget", "Permanently deletes a budget the token creator owns, including all of its Plan, ledger and planning data. This cannot be undone.",
            McpPermissions.AdminOnly, Effect.Destructive,
            Obj(R("budgetId", BudgetId()), O("version", Int("Expected budget version; omit to use the current one.", 1, long.MaxValue))),
            async c =>
            {
                var version = c.OptLong("version") ?? await BudgetVersionAsync(c, c.Str("budgetId"));
                if (version is null) return McpToolOutcome.NotFound("Budget not found.");
                return Done(await c.Send("DELETE", "/api/budgets/{id}", new { version }, Route.Of(("id", c.Str("budgetId")))), "Budget permanently deleted.");
            }, budgetScoped: false);
    }

    private static async Task<long?> BudgetVersionAsync(McpToolContext c, string budgetId)
    {
        var list = await c.Get("/api/budgets", query: Query(("includeArchived", "true")));
        if (!list.IsSuccess || list.Body is not { } body) return null;
        foreach (var budget in body.GetProperty("budgets").EnumerateArray())
            if (budget.GetProperty("id").GetString() == budgetId) return budget.GetProperty("version").GetInt64();
        return null;
    }

    // ---------------------------------------------------------------- Plan, categories and targets

    private static IEnumerable<McpToolDefinition> PlanTools()
    {
        yield return Tool("plan_get_month", "Get Plan month",
            "Reads one Plan month: Ready to Assign, category groups and categories with assigned, activity, available, targets and target progress, plus the month version used for optimistic concurrency.",
            McpPermissions.All, Effect.Read, Obj(R("month", Month())),
            async c =>
            {
                var response = await c.Get("/api/budgets/{id}/months/{month}", Route.Of(("id", c.BudgetId!), ("month", c.Str("month"))));
                if (!response.IsSuccess || response.Body is not { } body) return Done(response, "");
                var categories = body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("categories").GetArrayLength());
                return Done(response, $"Plan {c.Str("month")}: {body.GetProperty("groups").GetArrayLength()} groups, {categories} categories, Ready to Assign {body.GetProperty("readyToAssign")}, version {body.GetProperty("version")}.");
            });

        yield return Tool("plan_set_assigned", "Set category assignment",
            "Sets the amount assigned to one category in a Plan month (an absolute value, not a delta). Changes nothing else in the month.",
            McpPermissions.PlanWrite, Effect.Write,
            Obj(R("month", Month()), R("categoryId", Id("Category ID from plan_get_month.")), R("assigned", Money("New assigned amount for the month.")),
                O("version", Int("Expected Plan month version from plan_get_month. Omit to use the current version; a stale value returns a conflict with the current month.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month =>
            {
                var category = FindCategory(month, c.Str("categoryId"));
                if (category is null) return "Category not found in this month.";
                category["assigned"] = c.Money("assigned");
                return null;
            }, $"Set {c.Str("categoryId")} assigned to {c.Money("assigned")} in {c.Str("month")}.", [c.Str("categoryId")]), idempotent: true);

        yield return Tool("plan_move_money", "Move money between categories",
            "Moves an amount of assigned money from one category to another in the same Plan month (recorded as a move in Recent Moves).",
            McpPermissions.PlanWrite, Effect.Write,
            Obj(R("month", Month()), R("fromCategoryId", Id("Category to take money from.")), R("toCategoryId", Id("Category to move money to.")),
                R("amount", Money("Positive amount to move.", 0.01m)),
                O("version", Int("Expected Plan month version. Omit to use the current version.", 1, long.MaxValue))),
            async c =>
            {
                if (c.Str("fromCategoryId") == c.Str("toCategoryId")) return McpToolOutcome.Invalid("Choose two different categories.");
                return await EditMonthAsync(c, month =>
                {
                    var from = FindCategory(month, c.Str("fromCategoryId"));
                    var to = FindCategory(month, c.Str("toCategoryId"));
                    if (from is null || to is null) return "Category not found in this month.";
                    if (!IsActive(month, from) || !IsActive(month, to)) return "Money can only be moved between active categories.";
                    from["assigned"] = from["assigned"]!.GetValue<decimal>() - c.Money("amount");
                    to["assigned"] = to["assigned"]!.GetValue<decimal>() + c.Money("amount");
                    return null;
                }, $"Moved {c.Money("amount")} from {c.Str("fromCategoryId")} to {c.Str("toCategoryId")} in {c.Str("month")}.", [c.Str("fromCategoryId"), c.Str("toCategoryId")]);
            });

        yield return Tool("plan_recent_actions", "Recent Plan moves",
            "Lists recent Plan assignment changes and moves (who changed what, newest first).",
            McpPermissions.All, Effect.Read,
            Obj(O("kind", Enum("Filter: all, moved or assigned.", "all", "moved", "assigned")), O("limit", Int("Maximum rows, default 50.", 1, 200))),
            async c =>
            {
                var response = await c.Get("/api/budgets/{id}/plan-actions", Route.Of(("id", c.BudgetId!)),
                    Query(("kind", c.OptStr("kind") is "all" ? null : c.OptStr("kind")), ("limit", (c.OptInt("limit") ?? 50).ToString())));
                return Done(response, $"{Count(response.Body)} recent Plan action(s).");
            });

        yield return Tool("plan_set_note", "Set Plan month note", "Replaces the note for a Plan month.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("note", Str("Month note (empty clears it).", 1000)), O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month => { month["note"] = c.Str("note"); return null; }, $"Updated the {c.Str("month")} note.", []));

        yield return Tool("category_groups_create", "Create category group", "Adds a category group to a Plan month (it carries forward to later months).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("name", Str("Group name.", 120, 1)), O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c =>
            {
                var id = $"group-{Guid.NewGuid()}";
                var outcome = await EditMonthAsync(c, month =>
                {
                    var groups = month["groups"]!.AsArray();
                    if (groups.Any(g => string.Equals(g!["name"]!.GetValue<string>().Trim(), c.Str("name").Trim(), StringComparison.OrdinalIgnoreCase)))
                        return "A category group with that name already exists.";
                    groups.Add(new JsonObject { ["id"] = id, ["name"] = c.Str("name").Trim(), ["sortOrder"] = groups.Count, ["isArchived"] = false, ["categories"] = new JsonArray() });
                    return null;
                }, $"Created category group {id}.", []);
                return outcome with { Result = outcome.Response.IsSuccess ? ToElement(new { groupId = id, version = Number(outcome.Response.Body, "version") }) : null };
            });

        yield return Tool("category_groups_update", "Update category group", "Renames, hides (archives) or unhides a category group in a Plan month.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("groupId", Id("Category group ID.")), O("name", Str("New name.", 120, 1)), O("isArchived", Bool("True hides the group; false unhides it.")),
                O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month =>
            {
                var group = month["groups"]!.AsArray().Select(x => x!.AsObject()).FirstOrDefault(x => x["id"]!.GetValue<string>() == c.Str("groupId"));
                if (group is null) return "Category group not found in this month.";
                if (c.OptStr("name") is { } name) group["name"] = name.Trim();
                if (c.OptBool("isArchived") is { } archived) group["isArchived"] = archived;
                return null;
            }, $"Updated category group {c.Str("groupId")}.", []));

        yield return Tool("categories_create", "Create category", "Adds a category to a category group in a Plan month (it carries forward to later months).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("groupId", Id("Category group ID.")), R("name", Str("Category name.", 160, 1)), O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c =>
            {
                var id = $"category-{Guid.NewGuid()}";
                var outcome = await EditMonthAsync(c, month =>
                {
                    var group = month["groups"]!.AsArray().Select(x => x!.AsObject()).FirstOrDefault(x => x["id"]!.GetValue<string>() == c.Str("groupId"));
                    if (group is null) return "Category group not found in this month.";
                    var categories = group["categories"]!.AsArray();
                    if (categories.Any(x => string.Equals(x!["name"]!.GetValue<string>().Trim(), c.Str("name").Trim(), StringComparison.OrdinalIgnoreCase)))
                        return "This group already has a category with that name.";
                    categories.Add(new JsonObject
                    {
                        ["id"] = id, ["name"] = c.Str("name").Trim(), ["assigned"] = 0m, ["target"] = 0m, ["icon"] = null,
                        ["snoozed"] = false, ["sortOrder"] = categories.Count, ["isArchived"] = false
                    });
                    return null;
                }, $"Created category {id}.", []);
                return outcome with { Result = outcome.Response.IsSuccess ? ToElement(new { categoryId = id, version = Number(outcome.Response.Body, "version") }) : null };
            });

        yield return Tool("categories_update", "Update category", "Renames a category, changes its icon, snoozes it, or hides (archives)/unhides it in a Plan month.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("categoryId", Id("Category ID.")), O("name", Str("New name.", 160, 1)), O("icon", Nullable(Str("Icon (emoji or short text); null clears.", 16))),
                O("snoozed", Bool("Snooze target reminders for this category.")), O("isArchived", Bool("True hides the category; false unhides it.")),
                O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month =>
            {
                var category = FindCategory(month, c.Str("categoryId"));
                if (category is null) return "Category not found in this month.";
                if (c.OptStr("name") is { } name) category["name"] = name.Trim();
                if (c.Arguments.TryGetProperty("icon", out var icon)) category["icon"] = icon.ValueKind == JsonValueKind.Null ? null : icon.GetString();
                if (c.OptBool("snoozed") is { } snoozed) category["snoozed"] = snoozed;
                if (c.OptBool("isArchived") is { } archived) category["isArchived"] = archived;
                return null;
            }, $"Updated category {c.Str("categoryId")}.", [c.Str("categoryId")]));

        yield return Tool("category_targets_get", "Get category target", "Reads a category's target and its progress for a Plan month.",
            McpPermissions.All, Effect.Read, Obj(R("categoryId", Id("Category ID.")), R("month", Month())),
            async c =>
            {
                var response = await c.Get("/api/budgets/{id}/categories/{categoryId}/target/{month}",
                    Route.Of(("id", c.BudgetId!), ("categoryId", c.Str("categoryId")), ("month", c.Str("month"))));
                return Done(response, response.Body is { } body && body.TryGetProperty("target", out var target) && target.ValueKind != JsonValueKind.Null
                    ? "Category has a target." : "Category has no target.");
            });

        yield return Tool("category_targets_save", "Create or update category target",
            "Creates or replaces a category target (weekly, monthly, yearly or custom). Weekly needs weekday 0-6; monthly may take dayOfMonth (null = last day); yearly/custom need dueDate; custom may repeat (repeatEvery + repeatUnit).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("categoryId", Id("Category ID.")), R("month", Month("Plan month the change is made from (YYYY-MM).")),
                R("kind", Enum("Target type.", "weekly", "monthly", "yearly", "custom")), R("amount", Money("Target amount.", 0.01m)),
                R("behavior", Enum("setAside: set aside another amount each period; refill: refill up to the amount.", "setAside", "refill")),
                O("weekday", Int("Weekly targets: 0 (Sunday) to 6 (Saturday).", 0, 6)), O("dayOfMonth", Nullable(Int("Monthly targets: 1-31, null for the last day.", 1, 31))),
                O("dueDate", Date("Yearly/custom targets: due date YYYY-MM-DD.")), O("repeatEvery", Int("Custom targets: repeat interval 1-99.", 1, 99)),
                O("repeatUnit", Enum("Custom targets: repeat unit.", "day", "week", "month", "year")),
                O("version", Int("Expected target version (0 when creating). Omit to use the current one.", 0, long.MaxValue)), CommandIdArg),
            async c =>
            {
                var version = c.OptLong("version") ?? await TargetVersionAsync(c);
                if (version is null) return McpToolOutcome.NotFound("Category not found in this month.");
                var response = await c.Send("PUT", "/api/budgets/{id}/categories/{categoryId}/target", new
                {
                    commandId = c.CommandId(), version, month = c.Str("month"), kind = c.Str("kind"), amount = c.Money("amount"), behavior = c.Str("behavior"),
                    weekday = c.OptInt("weekday"), dayOfMonth = c.OptInt("dayOfMonth"), dueDate = Iso(c.OptDate("dueDate")), repeatEvery = c.OptInt("repeatEvery"), repeatUnit = c.OptStr("repeatUnit")
                }, Route.Of(("id", c.BudgetId!), ("categoryId", c.Str("categoryId"))));
                return Done(response, "Category target saved.");
            });

        yield return Tool("category_targets_delete", "Delete category target", "Removes a category's target.",
            McpPermissions.AdminOnly, Effect.Destructive,
            Obj(R("categoryId", Id("Category ID.")), R("month", Month()), O("version", Int("Expected target version; omit to use the current one.", 1, long.MaxValue)), CommandIdArg),
            async c =>
            {
                var version = c.OptLong("version") ?? await TargetVersionAsync(c);
                if (version is null or 0) return McpToolOutcome.NotFound("This category has no target.");
                var response = await c.Send("POST", "/api/budgets/{id}/categories/{categoryId}/target/delete",
                    new { commandId = c.CommandId(), version, month = c.Str("month") }, Route.Of(("id", c.BudgetId!), ("categoryId", c.Str("categoryId"))));
                return Done(response, "Category target deleted.");
            });
    }

    private static async Task<long?> TargetVersionAsync(McpToolContext c)
    {
        var current = await c.Get("/api/budgets/{id}/categories/{categoryId}/target/{month}",
            Route.Of(("id", c.BudgetId!), ("categoryId", c.Str("categoryId")), ("month", c.Str("month"))));
        if (!current.IsSuccess || current.Body is not { } body) return null;
        return body.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.Object ? target.GetProperty("version").GetInt64() : 0;
    }

    private static JsonObject? FindCategory(JsonObject month, string categoryId) => month["groups"]!.AsArray()
        .SelectMany(g => g!["categories"]!.AsArray()).Select(x => x!.AsObject()).FirstOrDefault(x => x["id"]!.GetValue<string>() == categoryId);

    private static bool IsActive(JsonObject month, JsonObject category) =>
        !category["isArchived"]!.GetValue<bool>() && month["groups"]!.AsArray().Any(g => !g!["isArchived"]!.GetValue<bool>() &&
            g["categories"]!.AsArray().Any(x => ReferenceEquals(x, category)));

    /// <summary>
    /// Reads the month, applies one edit to the server's own snapshot and saves it with the month version. Only the
    /// edited fields change; a caller-supplied stale version surfaces the server's 409 with the current month.
    /// </summary>
    private static async Task<McpToolOutcome> EditMonthAsync(McpToolContext c, Func<JsonObject, string?> edit, string summary, IReadOnlyCollection<string> changedCategories)
    {
        var route = Route.Of(("id", c.BudgetId!), ("month", c.Str("month")));
        var current = await c.Get("/api/budgets/{id}/months/{month}", route);
        if (!current.IsSuccess || current.Body is not { } body) return Done(current, "");
        var month = JsonNode.Parse(body.GetRawText())!.AsObject();
        var error = edit(month);
        if (error is not null) return error.Contains("not found", StringComparison.OrdinalIgnoreCase) ? McpToolOutcome.NotFound(error) : McpToolOutcome.Invalid(error);
        var update = new JsonObject
        {
            ["month"] = month["month"]!.GetValue<string>(),
            ["note"] = month["note"]?.GetValue<string>() ?? "",
            ["version"] = c.OptLong("version") ?? month["version"]!.GetValue<long>(),
            ["fundsAdded"] = month["fundsAdded"]!.GetValue<decimal>(),
            ["groups"] = new JsonArray(month["groups"]!.AsArray().Select(g => (JsonNode?)new JsonObject
            {
                ["id"] = g!["id"]!.GetValue<string>(), ["name"] = g["name"]!.GetValue<string>(), ["sortOrder"] = g["sortOrder"]!.GetValue<int>(),
                ["isArchived"] = g["isArchived"]!.GetValue<bool>(),
                ["categories"] = new JsonArray(g["categories"]!.AsArray().Select(x => (JsonNode?)new JsonObject
                {
                    ["id"] = x!["id"]!.GetValue<string>(), ["name"] = x["name"]!.GetValue<string>(), ["assigned"] = x["assigned"]!.GetValue<decimal>(),
                    ["target"] = x["target"]!.GetValue<decimal>(), ["icon"] = x["icon"]?.GetValue<string>(), ["snoozed"] = x["snoozed"]!.GetValue<bool>(),
                    ["sortOrder"] = x["sortOrder"]!.GetValue<int>(), ["isArchived"] = x["isArchived"]!.GetValue<bool>()
                }).ToArray())
            }).ToArray())
        };
        var response = await c.Send("PUT", "/api/budgets/{id}/months/{month}", update, route);
        if (!response.IsSuccess || response.Body is not { } saved) return Done(response, "");
        var savedMonth = JsonNode.Parse(saved.GetRawText())!.AsObject();
        var categories = changedCategories.Select(id => FindCategory(savedMonth, id)).Where(x => x is not null)
            .Select(x => new { id = x!["id"]!.GetValue<string>(), name = x["name"]!.GetValue<string>(), assigned = x["assigned"]!.GetValue<decimal>(), available = x["available"]!.GetValue<decimal>() }).ToList();
        var result = ToElement(new
        {
            month = saved.GetProperty("month").GetString(), version = saved.GetProperty("version").GetInt64(),
            readyToAssign = saved.GetProperty("readyToAssign").GetDecimal(), categories
        });
        return Done(response, $"{summary} Ready to Assign is now {saved.GetProperty("readyToAssign")} (version {saved.GetProperty("version")}).", result);
    }
}
