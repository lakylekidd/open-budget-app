# Milestone 12 — Icons and suggested icons

**Status:** Planned (not started). Open questions below must be answered before implementation.

## Goal

Give accounts, payees, category groups and subcategories a dedicated icon (emoji) shown in front of their name, chosen from a square picker, with an automatic suggestion when an item is named or renamed.

Today users type an emoji at the start of the name (for example "🛒 Groceries"). Categories already have a server-side `Icon` field (max 24 characters) that is not exposed as a picker; accounts and payees have no icon field.

## Dependencies

Milestone 2 (accounts, payees), Milestone 1 (category groups and subcategories), the credit-account slice of Milestone 5 (credit payment categories and their 💳 icon). Coordinate with Milestone 3 category targets, which also change category DTOs and the Plan row/detail UI.

## User value

Items are recognisable at a glance in the sidebar, register, payee list, category pickers and Plan, without typing emoji into names. New items get a sensible icon without extra effort.

## Vertical slice

- **UI:** a square icon button directly before the name field in the account form/edit popover, payee manager, group name popover and subcategory name popover. It opens an emoji picker with search, categories (recent, suggested, smileys, food, travel, objects, symbols…), keyboard navigation and a "No icon" choice. Icons render before names in the sidebar, register (account and payee columns), payee picker, category picker, Plan rows/groups, Long-Term Planning and Recent Moves.
- **API/domain:** icon fields on account, payee, category group and category contracts; validation (a single emoji grapheme, or empty; bounded length); suggestion service (see open question 1).
- **Persistence:** migration adding nullable `Icon` to `Accounts`, `Payees` and `CategoryGroups` (categories already have it). Icons are presentation metadata, so changes follow each entity's existing write path (ledger command for accounts/payees, versioned month save for categories and the shared cross-month ordering/metadata sync for groups).
- **Consistency:** credit payment categories keep 💳 (or follow their credit account's icon — open question 5); icons never affect money calculations or name matching for payees/rules.

## Acceptance criteria

1. A user can set, change and clear an icon for an account, payee, category group and subcategory using the square picker; the change persists across reload and on another device of the same budget.
2. The icon appears immediately before the name everywhere that item's name is shown (sidebar, register, pickers, Plan, Long-Term Planning, Recent Moves, payee manager), with consistent size and alignment, and is not part of the stored name.
3. When an item is created or renamed, an icon suggestion is produced from its name; for subcategories the parent group name is also used (for example group "Car" + subcategory "Insurance" suggests 🚗 rather than a generic 📄).
4. Suggestions are applied according to the agreed rule (open question 2) and are always shown as the first row of the picker; a user-chosen icon is never overwritten silently.
5. The picker supports search by keyword (e.g. "food" finds 🍔🛒), full keyboard use (arrows, Enter, Escape, Tab out), screen-reader labels with the emoji's name, and works at phone width.
6. Invalid icon input (multiple graphemes, text, over-length, control characters) is rejected by the API with a clear error and changes nothing.
7. Icon changes to category groups/subcategories keep the existing versioned month save semantics (stale version → 409, no lost edits) and apply to every initialized month like ordering does; account/payee icon changes use command IDs, ledger version and audit receipts like other ledger edits.
8. A migration preserves all existing names, balances and history. Existing leading emojis in names are handled according to open question 3, with the transformation documented and reversible.
9. If an external suggestion service is used, it is optional, times out quickly, falls back to the built-in matcher, never blocks saving, and only sends the minimum text needed (see open question 1).
10. Unit tests cover suggestion matching (including group + subcategory context), emoji validation and migration of leading emojis; PostgreSQL integration tests cover persistence, validation, versioning and budget isolation; UI tests cover the picker, suggestions, rendering before names, and keyboard use.

## Open questions

1. **Suggestion source.** Built-in keyword/synonym matcher only (offline, free, no data leaves the app), Azure OpenAI (smarter, but needs an Azure OpenAI resource with access via Managed Identity and Azure Key Vault, per-call cost, and sends category/payee names to that service — requires a data-handling decision), or both (matcher by default, Azure OpenAI when configured)?
2. **When to apply.** Auto-apply the suggestion only when the item has no icon yet; suggest only (picker's first row); or always replace on rename?
3. **Existing emoji in names.** Should a migration (or one-time prompt) move a leading emoji from the name into the new icon field, so "🛒 Groceries" becomes icon 🛒 + name "Groceries"? Automatic, opt-in per budget, or leave names untouched?
4. **Picker scope.** Emoji only, or also a set of monochrome line icons? Is a skin-tone selector needed? Should "recent" icons be per user or per budget?
5. **Credit payment categories.** Keep the fixed 💳 icon, or mirror the credit account's chosen icon?
6. **Payee icons.** Should payee icons show in the register payee column, only in the payee picker/manager, or both? Should transfer payees use the destination account's icon?
7. **Suggestions for existing items.** Offer a one-time "Suggest icons for all" action for items that have no icon?
8. **Placement in name editors.** Square icon button to the left of the name input (as in the request) in every popover/form — confirm for the compact Plan group/subcategory popovers on mobile.

## Product references

Requested by the product owner on 2026-10-30: a "small AI" that selects an icon when categories/subcategories (considering the group name) are renamed, plus a square dropdown to choose any icon/emoji for accounts, payees, categories and subcategories.
