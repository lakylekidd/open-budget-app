# Milestone 13 — Wishlist

**Status:** Implemented (2026-10-30).

## Goal

Let a budget keep ranked wish lists in custom buckets (for example Electronics, Games, or Small/Large/XL), fund each bucket from Plan like any other category, and see when the most-wanted item is affordable.

## Dependencies

Milestone 1 (versioned month snapshots, carry-forward, archive rules), Milestone 2a/8 (owned/shared budgets and membership entitlements), Milestone 3 (target-style Plan row text and category details panel), the credit-account slice of Milestone 5 (pattern for a server-owned Plan group).

## Entitlement and enablement

- Wishlist is gated by the `wishlist` membership entitlement. The `Wishlist` migration seeds tier defaults: **Free denied, Plus and Pro allowed**. Administrators change tier defaults in the admin panel's **Feature entitlements** card and set a per-user **Tier default / Allow / Deny** override under **Feature overrides** in the Users table. A user override wins over the tier.
- Every `/api/budgets/{id}/wishlist/...` endpoint requires the entitlement (`403` otherwise) and owner/editor access to the budget (`404` otherwise). Shared budgets: each editor needs their own entitlement to use the Wishlist screen; Plan rows show Wishlist progress to everyone who can open the budget.
- Within an entitled budget, the user turns Wishlist on per budget (Wishlist nav item → **Enable Wishlist**). The nav item sits directly below Long-Term Planning; it is hidden for users without the entitlement, shows "Off" while the budget's Wishlist is disabled, and shows a green count of buckets whose next item is ready to buy.

## Enable/disable semantics

- **Enable** creates (or restores) a `Wishlist` group (`wishlist`) in every initialized month, plus one category `wl-{bucketId}` per bucket, named and ordered like the buckets. The current server month is initialized first, so the group appears in Plan immediately; later months inherit it through normal carry-forward.
- **Disable** archives the Wishlist group in every initialized month. No row is deleted and no money moves: Available stays in the archived categories (archived categories keep carrying positive balances, as for any archived category) and Ready to Assign is unchanged. Buckets and items are kept. **Re-enable** un-archives the group with the same categories and money.
- A membership downgrade does not disable or archive anything; Plan keeps working with the existing categories, only the Wishlist screen/API become unavailable.

## Buckets and Plan integration

- Buckets can be created, renamed, reordered and deleted. In the UI they are a horizontal tab strip (see **Bucket tabs** below); the same API commands back every control. Names are 1–60 characters, unique per budget (case-insensitive); at most 50 active buckets.
- Each bucket owns one Plan category with a stable logical ID `wl-{bucketId}`. Creating, renaming and reordering buckets update the category in every initialized month in the same `LedgerGate` transaction, advancing each changed month's version (an open Plan save then gets the usual `409` and reloads).
- Deleting a bucket archives it and its category. Deletion is **blocked** while the category has a non-zero Available in the current month, a later initialized month or the latest initialized month ("Move this bucket's money … before deleting it"), because archived categories cannot be assigned or moved from and Wishlist categories cannot be restored in Plan.
- Plan saves may assign/move money in Wishlist categories and may move the whole Wishlist group, but cannot rename, archive, add, remove, reorder or move Wishlist categories, rename/archive the group, or put other categories into it (`400`). The Plan UI hides/disables those controls for the group, like the Credit Card Payments group.
- Bucket price range: the overview shows `minCost`–`maxCost` of the bucket's active items (for example "€20.00–€150.00"). The range is informational; it is not enforced.

## Items, ordering and history

- Item fields: name (1–120), optional link (absolute `http`/`https`, no credentials or whitespace, ≤ 2048 characters; rendered with `target="_blank" rel="noopener noreferrer"`), cost (decimal, > 0, two decimals, entered with the shared `MoneyInput` so arithmetic such as `40+9.99` works), description (≤ 1000), created date (shown as "added 3 months ago"). At most 200 active items per bucket.
- **Bucket tabs.** The Wishlist page shows one tab per bucket, in bucket order, as an ARIA tablist (`role=tablist/tab/tabpanel`, `aria-selected`, roving tabindex; ←/→/Home/End move between tabs and select the bucket; the strip scrolls horizontally). A trailing **+** tab creates a bucket: it adds an inline, auto-focused name input; Enter or blur saves (`POST /buckets`) and selects the new tab; Escape or an empty name discards the draft; names are trimmed, 1–60 characters and server errors appear inline under the strip. Double-clicking a tab (or F2, or **Rename** in the panel) renames it inline with the same behaviour (`PUT /buckets/{id}`). The selected tab is remembered per budget in `localStorage` (`openbudget.wishlist.bucket.{budgetId}`; the URL routes have no per-page detail segment for Wishlist) and falls back to the first bucket. The selected bucket's panel keeps the bucket-level **Move left/right** (reorder), **Rename** and **Delete** buttons; dropping an item on another tab moves it to that bucket. A green dot on a tab marks a bucket that can buy its next item.
- Item rows reuse the Plan subcategory row look (`--subcat-*` tokens in `styles.css`: row height, background, hover, name and money type, right-aligned money column, borders) and have no checkbox. Row actions (↑/↓, Edit, Purchased, Remove) appear on hover or focus.
- Active items are ordered by desire. Drag-and-drop and ↑/↓ buttons reorder them; the top item is the **next** purchase candidate. Editing an item can move it to another bucket (it joins the end of that bucket).
- **Mark purchased** keeps the item in a collapsed **Purchased** history with its purchase time. **Remove** keeps it in a collapsed **Removed** history (no hard delete). Either can be **restored** to the end of the bucket.
- Recording the spending: after marking an item purchased, the screen explains how to record the spending and links to Accounts. The user records the purchase in an account register with the bucket's category (Wishlist categories appear in the category picker under the Wishlist group), which reduces the bucket's Available like any other spending. Wishlist never creates transactions itself.

## Progress

For each bucket, with `A` = the bucket category's Available in the evaluated month (the server's current month on the Wishlist screen, the viewed month on Plan rows/details) and `C` = cost of the top active item:

- `remaining = max(C − A, 0)`, `canPurchase = A ≥ C` (exactly equal counts as purchasable), `fundedFraction = A ≤ 0 ? 0 : min(A / C, 1)` truncated to 4 decimals.
- Purchased/removed items are skipped; a bucket without active items has no progress (`null`).
- Shown as "€X of €Y — €Z to go" with a progress bar; when purchasable: a **Ready to buy** badge on the bucket, "You can buy <item> now.", the nav count, and the Plan row text "You can buy <item>" with a checkmark (otherwise "€Z to go for <item>"; derived client-side from the row's live Available vs the item cost, in cents). The category details panel in Plan shows the same progress with an **Open Wishlist** link.
- Reordering items changes the next item immediately in the Wishlist overview and in the Plan month DTO (`wishlistProgress`).

## Concurrency and persistence

- One optimistic `version` per budget's Wishlist (`WishlistSettings.Version`) guards every Wishlist write (enable/disable, buckets and items). A stale version returns `409` with `{ error, current }` (the latest overview); the UI shows the latest data and asks the user to retry.
- Writes are serialized with Plan saves and ledger commands by the budget's `LedgerGate`; validation errors and conflicts roll back the whole transaction (including month initialization).
- Wishlist changes are not money movements, so they do not append ledger command receipts or Recent Moves entries.

## Acceptance criteria

1. A Free user does not see the Wishlist nav item and receives `403` from the Wishlist API; Plus/Pro users (or users with an Allow override) see it directly below Long-Term Planning; a Deny override hides it for a Plus user.
2. Enabling creates the Wishlist group and one category per bucket in every initialized month; later months inherit it.
3. Renaming and reordering buckets update category names and order in every month; Plan saves cannot rename, archive, reorder, move or delete Wishlist categories.
4. Assigning money in Plan updates bucket progress; Available equal to the next item's cost shows "Ready to buy" on the bucket and the nav count, and a checkmark with "You can buy <item>" on the Plan row.
5. Reordering items changes the next item and its progress in both the Wishlist screen and the Plan DTO.
6. Disabling archives the group without changing Available or Ready to Assign; re-enabling restores it with the same money.
7. Purchased and removed items stay in collapsible history and can be restored.
8. Invalid links (non-http(s)), invalid costs and over-long fields are rejected with `400` and no partial writes; stale versions return `409`.
9. Wishlist data is isolated per budget and shared with the budget's editors.
10. The screen works down to ~400px wide, uses the dark theme, and offers keyboard alternatives for drag-and-drop.

## Deviations from the original brief

- A single per-budget Wishlist version (not per-bucket versions) guards all Wishlist writes. It is simpler and still rejects every stale write; concurrent edits of different buckets require a retry.
- Bucket deletion is blocked while money remains (rather than archiving a funded category), because funded archived Wishlist categories could not be emptied.
- ~~Recording the purchase is a guided step (notice + link to Accounts) rather than a pre-filled transaction form.~~ Superseded by Wishlist purchase transactions below.

## Wishlist purchase transactions (2026-10-30)

- **Link wishlist item button (2026-10-30 rework).** There is no Regular/Wishlist mode switch. When adding a transaction (register inline row and the Add transaction dialog), a compact icon button with the Wishlist nav icon sits right next to the Memo input. It is shown only when the budget's Wishlist is entitled and enabled, and hidden when editing, for scheduled rows, transfers, splits and loan accounts.
- **Popup.** The button (`aria-haspopup="listbox"`, label "Link wishlist item") opens a listbox of the open (active) items grouped by bucket (bucket order), each bucket's items in desire order with name and cost. Enter/Space or ArrowDown on the button opens it; ArrowUp/ArrowDown (wrapping), Home and End move; Enter selects; Escape closes and returns focus to the button; Tab or an outside click closes it.
- **Selection.** Choosing an item sets Memo to exactly the item name and Category to the bucket's `wl-{bucketId}`. Payee and the amount are never touched; Payee is whatever the user enters (for example the store). Focus then moves to the next field (the same `focusNextField` behaviour as the payee/category pickers). The button shows an active state, its label becomes "Link wishlist item (linked: <item>)" with a matching tooltip, and a status line names the linked item. Reopening the popup marks the linked item and offers **Unlink <item>** as the first option. Choosing another category (or splitting) drops the link, because a Wishlist purchase must use its bucket's category. Memo stays editable after linking.
- **Saving.** `POST /api/budgets/{id}/wishlist/items/{itemId}/purchase-transaction` creates the transaction and marks the item purchased atomically in one ledger command (replay-safe with the command ID). Payee and memo are stored exactly as sent (both optional); the server never substitutes the item name. The server enforces an active item in this budget (an already purchased or removed item is `400`), an outflow, the bucket's category, no splits/repeat, Wishlist enabled and the `wishlist` entitlement, plus every normal transaction rule. Bucket Available and the next item update from the existing server calculations.
- **History.** A purchase recorded this way shows "Purchased <date> · <amount spent>" with a **View transaction** button that opens the account register at `/{budgetKey}/account/{slug}`, narrows it to the transaction's date and selects/highlights the row (with the "Showing the transaction selected from the Wishlist screen" banner). Plain Mark purchased items show the cost and date as before.
- **Undo/consistency.** Deleting the linked transaction (delete, bulk delete or rejecting it in review) returns the item to the wishlist at its previous desire position (or the end of the bucket if fewer items remain), clears the link and advances the Wishlist version. Restoring the item manually from history keeps the transaction (the money stays spent) and only clears the link.
- **Editing a linked transaction.** Rule: the link is kept while the transaction still spends from the item's bucket category. Editing the payee, memo, amount, date, account or status keeps the item purchased and linked. Editing it to another category, splitting it, or turning it into a transfer means it no longer bought the item from the bucket, so the same ledger command unlinks it and returns the item to its previous desire position (as for a delete); the transaction itself is kept. Moving the transaction back to the bucket category later does not re-link it; link it again by recording a new purchase. Rationale: the bucket's Available (and so its progress) only reflects spending in its category, so a purchase that no longer draws on the bucket should not hide the item.
- **Wishlist page.** The Purchased button's dialog offers **Record purchase** (opens Accounts with the Add transaction dialog, the item already linked: memo and category set and the item cost as the outflow; payee left empty; the item is marked purchased only when the transaction is saved) alongside **Mark purchased only**.
- **MCP.** `transactions_create` accepts optional `wishlistItemId` and then calls the purchase-transaction endpoint; payee and memo are taken from the tool arguments.
