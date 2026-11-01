using System.Text.Json;
using System.Text.Json.Nodes;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.Api.Mcp;

// Wishlist (entitlement "wishlist"), Plan structure ordering and transaction images. Every tool dispatches to the endpoint the
// web app uses, so the entitlement policy, wishlist version checks and image rules apply unchanged.
public static partial class McpToolCatalog
{
    private static (string, JsonObject, bool) WishlistVersionArg => O("version",
        Int("Expected Wishlist version from wishlist_get. Omit to use the current one; a stale value returns a conflict with the current Wishlist.", 0, long.MaxValue));

    private static async Task<McpToolOutcome> WishlistWriteAsync(McpToolContext c, string method, string template, Func<long, object> body,
        IReadOnlyDictionary<string, string> route, string summary)
    {
        var version = c.OptLong("version");
        if (version is null)
        {
            var current = await c.Get("/api/budgets/{id}/wishlist", Route.Of(("id", c.BudgetId!)));
            if (!current.IsSuccess) return Done(current, "");
            version = Number(current.Body, "version");
        }
        var response = await c.Send(method, template, body(version!.Value), route);
        return Done(response, $"{summary} Wishlist version is now {Number(response.Body, "version")}.");
    }

    private static Dictionary<string, string> WishlistRoute(McpToolContext c, params (string Key, string Value)[] extra) =>
        new[] { ("id", c.BudgetId!) }.Concat(extra).ToDictionary(x => x.Item1, x => x.Item2);

    private static (string, JsonObject, bool)[] WishlistItemFields() =>
    [
        R("name", Str("Item name.", WishlistRules.MaxItemNameLength, 1)),
        R("cost", Money("Expected cost (positive).", 0.01m, WishlistRules.MaxCost)),
        O("url", Str("Optional http(s) link to the item.", WishlistRules.MaxUrlLength)),
        O("description", Str("Optional description.", WishlistRules.MaxDescriptionLength)),
    ];

    private static IEnumerable<McpToolDefinition> WishlistTools()
    {
        yield return Tool("wishlist_get", "Get Wishlist",
            "Reads the Wishlist for a month: enabled flag, version, buckets (with their wl-{bucketId} Plan category, available money and progress toward the next item), " +
            "active items in desire order, and purchased/removed history. Requires the wishlist membership entitlement.",
            McpPermissions.Read, Effect.Read, Obj(O("month", Month("Plan month to evaluate progress in (YYYY-MM). Default: current month."))),
            async c =>
            {
                var response = await c.Get("/api/budgets/{id}/wishlist", Route.Of(("id", c.BudgetId!)), Query(("month", c.OptStr("month"))));
                return Done(response, $"{Count(response.Body, "buckets")} Wishlist bucket(s); version {Number(response.Body, "version")}.");
            });

        yield return Tool("wishlist_set_enabled", "Enable or disable Wishlist",
            "Enables the Wishlist (restores its Plan group and bucket categories) or disables it (archives the group; nothing is deleted).",
            McpPermissions.AdminOnly, Effect.Write, Obj(R("enabled", Bool("True enables, false disables.")), WishlistVersionArg),
            c => WishlistWriteAsync(c, "POST", c.Bool("enabled") ? "/api/budgets/{id}/wishlist/enable" : "/api/budgets/{id}/wishlist/disable",
                version => new { version }, WishlistRoute(c), c.Bool("enabled") ? "Wishlist enabled." : "Wishlist disabled."),
            confirmWhen: args => args.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False);

        yield return Tool("wishlist_buckets_create", "Create Wishlist bucket", "Creates a Wishlist bucket and its Plan category.",
            McpPermissions.AdminOnly, Effect.Write, Obj(R("name", Str("Bucket name.", WishlistRules.MaxBucketNameLength, 1)), WishlistVersionArg),
            c => WishlistWriteAsync(c, "POST", "/api/budgets/{id}/wishlist/buckets", version => new { version, name = c.Str("name") }, WishlistRoute(c), "Wishlist bucket created."));

        yield return Tool("wishlist_buckets_rename", "Rename Wishlist bucket", "Renames a Wishlist bucket (and its Plan category).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("bucketId", Uuid("Bucket ID from wishlist_get.")), R("name", Str("New bucket name.", WishlistRules.MaxBucketNameLength, 1)), WishlistVersionArg),
            c => WishlistWriteAsync(c, "PUT", "/api/budgets/{id}/wishlist/buckets/{bucketId}", version => new { version, name = c.Str("name") },
                WishlistRoute(c, ("bucketId", c.Str("bucketId"))), "Wishlist bucket renamed."));

        yield return Tool("wishlist_buckets_delete", "Delete Wishlist bucket",
            "Deletes a Wishlist bucket. Its Plan category is archived (history and carried money are kept).",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("bucketId", Uuid("Bucket ID.")), WishlistVersionArg),
            c => WishlistWriteAsync(c, "POST", "/api/budgets/{id}/wishlist/buckets/{bucketId}/delete", version => new { version },
                WishlistRoute(c, ("bucketId", c.Str("bucketId"))), "Wishlist bucket deleted."));

        yield return Tool("wishlist_buckets_reorder", "Reorder Wishlist buckets", "Sets the order of all active Wishlist buckets (send every active bucket ID once).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("bucketIds", Array("Active bucket IDs in the new order.", Uuid("Bucket ID."), WishlistRules.MaxActiveBuckets, 1)), WishlistVersionArg),
            c => WishlistWriteAsync(c, "PUT", "/api/budgets/{id}/wishlist/buckets/order", version => new { version, bucketIds = c.Element("bucketIds") },
                WishlistRoute(c), "Wishlist buckets reordered."));

        yield return Tool("wishlist_items_create", "Add Wishlist item", "Adds an item to the end of a bucket's desire order.",
            McpPermissions.AdminOnly, Effect.Write, Obj([R("bucketId", Uuid("Bucket ID.")), .. WishlistItemFields(), WishlistVersionArg]),
            c => WishlistWriteAsync(c, "POST", "/api/budgets/{id}/wishlist/buckets/{bucketId}/items",
                version => new { version, name = c.Str("name"), url = c.OptStr("url"), cost = c.Money("cost"), description = c.OptStr("description") },
                WishlistRoute(c, ("bucketId", c.Str("bucketId"))), "Wishlist item added."));

        yield return Tool("wishlist_items_update", "Update Wishlist item",
            "Replaces an active item's name, cost, link and description (send every field you want to keep); bucketId moves it to another bucket.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj([R("itemId", Uuid("Item ID.")), .. WishlistItemFields(), O("bucketId", Uuid("Move to this bucket (optional).")), WishlistVersionArg]),
            c => WishlistWriteAsync(c, "PUT", "/api/budgets/{id}/wishlist/items/{itemId}",
                version => new { version, name = c.Str("name"), url = c.OptStr("url"), cost = c.Money("cost"), description = c.OptStr("description"), bucketId = c.OptGuid("bucketId") },
                WishlistRoute(c, ("itemId", c.Str("itemId"))), "Wishlist item updated."));

        yield return Tool("wishlist_items_reorder", "Reorder Wishlist items", "Sets the desire order of a bucket's active items (send every active item ID once).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("bucketId", Uuid("Bucket ID.")), R("itemIds", Array("Active item IDs in the new order.", Uuid("Item ID."), WishlistRules.MaxActiveItemsPerBucket, 1)), WishlistVersionArg),
            c => WishlistWriteAsync(c, "PUT", "/api/budgets/{id}/wishlist/buckets/{bucketId}/items/order", version => new { version, itemIds = c.Element("itemIds") },
                WishlistRoute(c, ("bucketId", c.Str("bucketId"))), "Wishlist items reordered."));

        yield return Tool("wishlist_items_set_status", "Set Wishlist item status",
            "Marks an item purchased (without a ledger transaction — use transactions_create with wishlistItemId to record the spending), removed, or restores it to active.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("itemId", Uuid("Item ID.")), R("status", Enum("New status.", "purchased", "removed", "active")), WishlistVersionArg),
            c => WishlistWriteAsync(c, "POST", c.Str("status") switch
                {
                    "purchased" => "/api/budgets/{id}/wishlist/items/{itemId}/purchase",
                    "removed" => "/api/budgets/{id}/wishlist/items/{itemId}/remove",
                    _ => "/api/budgets/{id}/wishlist/items/{itemId}/restore",
                }, version => new { version }, WishlistRoute(c, ("itemId", c.Str("itemId"))), $"Wishlist item marked {c.Str("status")}."),
            confirmWhen: args => args.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "removed");
    }

    private static IEnumerable<McpToolDefinition> StructureTools()
    {
        yield return Tool("category_groups_move", "Reorder category group", "Moves a category group to a zero-based position in a Plan month's group order (as drag and drop does).",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("groupId", Id("Category group ID.")), R("position", Int("Zero-based target position; past the end moves it last.", 0, 1000)),
                O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month =>
            {
                var groups = month["groups"]!.AsArray();
                var ordered = groups.Select(x => x!.AsObject()).OrderBy(x => x["sortOrder"]!.GetValue<int>()).ToList();
                var group = ordered.FirstOrDefault(x => x["id"]!.GetValue<string>() == c.Str("groupId"));
                if (group is null) return "Category group not found in this month.";
                ordered.Remove(group);
                ordered.Insert(Math.Min(c.OptInt("position")!.Value, ordered.Count), group);
                for (var i = 0; i < ordered.Count; i++) ordered[i]["sortOrder"] = i;
                return null;
            }, $"Moved category group {c.Str("groupId")}.", []));

        yield return Tool("categories_move", "Move category",
            "Moves a category to a zero-based position within its group or into another active group (as drag and drop does). Archived categories and credit card payment categories cannot be moved between groups.",
            McpPermissions.AdminOnly, Effect.Write,
            Obj(R("month", Month()), R("categoryId", Id("Category ID.")), R("groupId", Id("Destination category group ID (may be its current group).")),
                R("position", Int("Zero-based position in the destination group; past the end moves it last.", 0, 1000)),
                O("version", Int("Expected Plan month version.", 1, long.MaxValue))),
            async c => await EditMonthAsync(c, month =>
            {
                var groups = month["groups"]!.AsArray().Select(x => x!.AsObject()).ToList();
                var source = groups.FirstOrDefault(g => g["categories"]!.AsArray().Any(x => x!["id"]!.GetValue<string>() == c.Str("categoryId")));
                var destination = groups.FirstOrDefault(g => g["id"]!.GetValue<string>() == c.Str("groupId"));
                if (source is null) return "Category not found in this month.";
                if (destination is null) return "Category group not found in this month.";
                var sourceItems = source["categories"]!.AsArray();
                var category = sourceItems.Select(x => x!.AsObject()).Single(x => x["id"]!.GetValue<string>() == c.Str("categoryId"));
                sourceItems.Remove(category);
                var destinationItems = destination["categories"]!.AsArray();
                var target = destinationItems.Select(x => x!.AsObject()).OrderBy(x => x["sortOrder"]!.GetValue<int>()).ToList();
                destinationItems.Clear();
                target.Insert(Math.Min(c.OptInt("position")!.Value, target.Count), category);
                for (var i = 0; i < target.Count; i++) { target[i]["sortOrder"] = i; destinationItems.Add(target[i]); }
                if (!ReferenceEquals(source, destination))
                    foreach (var (item, index) in sourceItems.Select(x => x!.AsObject()).OrderBy(x => x["sortOrder"]!.GetValue<int>()).Select((x, i) => (x, i)).ToList())
                        item["sortOrder"] = index;
                return null;
            }, $"Moved category {c.Str("categoryId")}.", [c.Str("categoryId")]));
    }

    private static IEnumerable<McpToolDefinition> ImageTools()
    {
        yield return Tool("transaction_images_list", "List transaction images",
            "Lists a transaction's attached receipt images (id, file name, type, size, dimensions, source, created time). Image bytes are not returned.",
            McpPermissions.Read, Effect.Read, Obj(R("transactionId", Uuid("Transaction ID."))),
            async c =>
            {
                var response = await c.Get("/api/transactions/{id}/images", Route.Of(("id", c.Str("transactionId"))));
                return Done(response, $"{Count(response.Body, "images")} image(s).");
            });

        yield return Tool("transaction_images_delete", "Delete transaction image", "Deletes one image attached to a transaction.",
            McpPermissions.AdminOnly, Effect.Destructive, Obj(R("imageId", Uuid("Image ID from transaction_images_list.")), CommandIdArg),
            async c => Done(await c.Send("POST", "/api/transaction-images/{imageId}/delete", new { commandId = c.CommandId() }, Route.Of(("imageId", c.Str("imageId")))),
                "Transaction image deleted."));
    }
}
