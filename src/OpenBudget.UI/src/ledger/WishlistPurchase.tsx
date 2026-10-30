import { useId, type KeyboardEvent } from 'react';
import { addedAgo, wishlistPath, type WishlistBucket, type WishlistItem, type WishlistOverview } from '../wishlist/wishlistApi';
import { formatMoney } from './api';
import './wishlistPurchase.css';

// Wishlist "Record purchase" support for the add-transaction flows (register inline row and dialog form).
// Present only when the budget's Wishlist is entitled AND enabled (App passes null otherwise).
export type WishlistContext = { budgetId: string; overview: WishlistOverview };
export type TransactionMode = 'regular' | 'wishlist';
export type WishlistChoice = { bucket: WishlistBucket; item: WishlistItem };

// Autofill choices: payee = item name (≤ 120 chars, fits the 160-char payee); memo = "Wishlist: <item>" (item links can be
// longer than the 1000-char memo); category = the bucket's locked wl-{bucketId} category; amount = the item cost as an outflow.
export function wishlistPrefill({ bucket, item }: WishlistChoice) {
  return { payee: item.name, memo: `Wishlist: ${item.name}`, categoryId: bucket.categoryId, outflow: item.cost };
}

export const wishlistPurchasePath = (budgetId: string, itemId: string) =>
  `${wishlistPath(budgetId)}/items/${encodeURIComponent(itemId)}/purchase-transaction`;

// Finds an active item (and its bucket); defaults to the bucket's next item (top of the desire order).
export function findWishlistChoice(overview: WishlistOverview, itemId?: string | null, bucketId?: string | null): WishlistChoice | null {
  for (const bucket of overview.buckets) {
    if (bucketId && bucket.id !== bucketId) continue;
    const item = itemId ? bucket.items.find(candidate => candidate.id === itemId) : bucket.items[0];
    if (item) return { bucket, item };
  }
  return null;
}

// Compact segmented Regular/Wishlist switch: a radio group with roving arrow-key selection.
export function TransactionModeToggle({ mode, disabled, onChange }: { mode: TransactionMode; disabled?: boolean; onChange: (mode: TransactionMode) => void }) {
  const modes: [TransactionMode, string][] = [['regular', 'Regular'], ['wishlist', 'Wishlist']];
  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return;
    event.preventDefault();
    const next = mode === 'regular' ? 'wishlist' : 'regular';
    onChange(next);
    (event.currentTarget.parentElement?.querySelector(`[data-mode="${next}"]`) as HTMLButtonElement | null)?.focus();
  }
  return <div className="transaction-mode-toggle" role="radiogroup" aria-label="Transaction type">
    {modes.map(([value, label]) => <button key={value} type="button" role="radio" data-mode={value} aria-checked={mode === value} tabIndex={mode === value ? 0 : -1}
      disabled={disabled} onKeyDown={onKeyDown} onClick={() => onChange(value)}>{label}</button>)}
  </div>;
}

// Bucket selector + item selector + "Buying: …" summary. Buckets without active items are listed but disabled.
export function WishlistPurchasePicker({ overview, choice, disabled, form, onChoose }: {
  overview: WishlistOverview; choice: WishlistChoice | null; disabled?: boolean; form?: string; onChoose: (choice: WishlistChoice) => void;
}) {
  const bucketId = useId(); const itemId = useId();
  const buckets = overview.buckets;
  return <div className="wishlist-purchase-picker">
    <label htmlFor={bucketId}>Wishlist bucket</label>
    <select id={bucketId} form={form} disabled={disabled} value={choice?.bucket.id ?? ''} onChange={event => {
      const next = findWishlistChoice(overview, null, event.target.value);
      if (next) onChoose(next);
    }}>
      <option value="" disabled>Choose a bucket…</option>
      {buckets.map(bucket => {
        const next = bucket.items[0];
        return <option key={bucket.id} value={bucket.id} disabled={!next}>
          {next ? `${bucket.name} — ${next.name} (${formatMoney(next.cost)})` : `${bucket.name} — no items`}
        </option>;
      })}
    </select>
    {choice && <>
      <label htmlFor={itemId}>Item</label>
      <select id={itemId} form={form} disabled={disabled} value={choice.item.id} onChange={event => {
        const next = findWishlistChoice(overview, event.target.value, choice.bucket.id);
        if (next) onChoose(next);
      }}>
        {choice.bucket.items.map((item, index) => <option key={item.id} value={item.id}>{index + 1}. {item.name} ({formatMoney(item.cost)})</option>)}
      </select>
      <p className="wishlist-purchase-buying" role="status">Buying: <strong>{choice.item.name}</strong> · {formatMoney(choice.item.cost)} · {addedAgo(choice.item.createdAt)}</p>
    </>}
    {buckets.length === 0 && <p className="wishlist-purchase-empty">No Wishlist buckets yet. Add items on the Wishlist screen.</p>}
  </div>;
}

// The locked category shown in place of the category picker while in Wishlist mode.
export function WishlistLockedCategory({ choice }: { choice: WishlistChoice | null }) {
  return <span className="wishlist-locked-category" aria-label="Category (set by Wishlist bucket)">{choice ? `Wishlist: ${choice.bucket.name}` : 'Choose a bucket'}</span>;
}
