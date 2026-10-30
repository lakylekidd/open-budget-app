import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { wishlistPath, type WishlistBucket, type WishlistItem, type WishlistOverview } from '../wishlist/wishlistApi';
import { WishlistIcon } from '../wishlist/WishlistPlanParts';
import { formatMoney } from './api';
import { focusNextField } from './focusNext';
import './wishlistPurchase.css';

// Wishlist purchase linking for the add-transaction flows (register inline row and dialog form).
// Present only when the budget's Wishlist is entitled AND enabled (App passes null otherwise).
export type WishlistContext = { budgetId: string; overview: WishlistOverview };
export type WishlistChoice = { bucket: WishlistBucket; item: WishlistItem };

// Linking an item fills only Memo (= the item name) and Category (= the bucket's wl-{bucketId}). Payee stays the user's input.
export function wishlistLinkFill({ bucket, item }: WishlistChoice) {
  return { memo: item.name, categoryId: bucket.categoryId };
}

export const wishlistPurchasePath = (budgetId: string, itemId: string) =>
  `${wishlistPath(budgetId)}/items/${encodeURIComponent(itemId)}/purchase-transaction`;

// Open (not purchased/removed) items grouped by bucket, each bucket's items in desire order.
export function openWishlistGroups(overview: WishlistOverview): { bucket: WishlistBucket; items: WishlistItem[] }[] {
  return [...overview.buckets].sort((a, b) => a.sortOrder - b.sortOrder)
    .map(bucket => ({ bucket, items: bucket.items.filter(item => item.status === 'active').sort((a, b) => a.sortOrder - b.sortOrder) }))
    .filter(group => group.items.length > 0);
}

// Finds an open item and its bucket.
export function findWishlistChoice(overview: WishlistOverview, itemId: string | null | undefined): WishlistChoice | null {
  if (!itemId) return null;
  for (const { bucket, items } of openWishlistGroups(overview)) {
    const item = items.find(candidate => candidate.id === itemId);
    if (item) return { bucket, item };
  }
  return null;
}

type Option = { kind: 'unlink' } | { kind: 'item'; choice: WishlistChoice };

/**
 * Compact icon button placed next to the Memo input. Opens a listbox popup of open Wishlist items grouped by bucket.
 * Keyboard: Enter/Space/ArrowDown on the button opens it; Arrow keys/Home/End move; Enter selects; Escape or Tab closes.
 * While an item is linked the button shows an active state and names the item; the popup offers "Unlink".
 */
export function WishlistLinkButton({ overview, linked, disabled, form, onLink, onUnlink }: {
  overview: WishlistOverview; linked: WishlistChoice | null; disabled?: boolean; form?: string;
  onLink: (choice: WishlistChoice) => void; onUnlink: () => void;
}) {
  const id = useId();
  const button = useRef<HTMLButtonElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [position, setPosition] = useState({ left: 0, top: 0, maxHeight: 320 });
  const groups = openWishlistGroups(overview);
  const options: Option[] = [...(linked ? [{ kind: 'unlink' } as const] : []), ...groups.flatMap(({ bucket, items }) => items.map(item => ({ kind: 'item', choice: { bucket, item } }) as const))];

  function close(refocus = true) { setOpen(false); if (refocus) button.current?.focus(); }
  function show() {
    if (disabled) return;
    const selected = linked ? options.findIndex(option => option.kind === 'item' && option.choice.item.id === linked.item.id) : -1;
    setActive(selected >= 0 ? selected : 0); setOpen(true);
  }
  function choose(index: number) {
    const option = options[index];
    if (!option) return;
    if (option.kind === 'unlink') onUnlink(); else onLink(option.choice);
    close();
    focusNextField(button.current);
  }

  useEffect(() => { if (disabled) setOpen(false); }, [disabled]);
  useEffect(() => {
    if (!open) return;
    function place() {
      const rect = button.current?.getBoundingClientRect(); if (!rect) return;
      const height = Math.min(360, window.innerHeight - 24); const below = window.innerHeight - rect.bottom - 12;
      const top = below >= height || below >= rect.top ? rect.bottom + 8 : Math.max(8, rect.top - height - 8);
      setPosition({ left: Math.max(8, Math.min(rect.right - 320, window.innerWidth - 328)), top, maxHeight: Math.max(160, Math.min(height, window.innerHeight - top - 8)) });
    }
    function outside(event: MouseEvent) {
      const target = event.target as Node;
      if (!popup.current?.contains(target) && !button.current?.contains(target)) setOpen(false);
    }
    place(); popup.current?.focus();
    document.addEventListener('mousedown', outside); window.addEventListener('resize', place); window.addEventListener('scroll', place, true);
    return () => { document.removeEventListener('mousedown', outside); window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); };
  }, [open]);
  useEffect(() => { if (open) popup.current?.querySelector('[data-active="true"]')?.scrollIntoView?.({ block: 'nearest' }); }, [open, active]);

  function onListKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const count = options.length;
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); return; }
    // The popup is portaled, so Tab returns to the button instead of leaving the row/dialog focus order.
    if (event.key === 'Tab') { event.preventDefault(); close(); return; }
    if (!count) return;
    if (event.key === 'ArrowDown') { event.preventDefault(); setActive(n => (n + 1) % count); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); setActive(n => (n - 1 + count) % count); }
    else if (event.key === 'Home') { event.preventDefault(); setActive(0); }
    else if (event.key === 'End') { event.preventDefault(); setActive(count - 1); }
    else if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); choose(active); }
  }

  const label = linked ? `Link wishlist item (linked: ${linked.item.name})` : 'Link wishlist item';
  let index = linked ? 1 : 0;
  const option = (i: number, name: string, content: ReactNode, selected: boolean, className = '') =>
    <div role="option" id={`${id}-${i}`} key={i} aria-label={name} aria-selected={selected} data-active={i === active} className={`wishlist-link-option ${className}`}
      onMouseDown={event => event.preventDefault()} onMouseEnter={() => setActive(i)} onClick={() => choose(i)}>{content}</div>;

  return <>
    <button ref={button} type="button" form={form} className={`wishlist-link-button ${linked ? 'is-linked' : ''}`} aria-label={label} title={linked ? `Linked wishlist item: ${linked.item.name}` : 'Link wishlist item'}
      aria-haspopup="listbox" aria-expanded={open} aria-controls={open ? `${id}-list` : undefined} disabled={disabled}
      onClick={() => (open ? close() : show())}
      onKeyDown={event => { if (event.key === 'ArrowDown' && !open) { event.preventDefault(); show(); } }}>
      <WishlistIcon size={16} />
    </button>
    {open && createPortal(<div ref={popup} id={`${id}-list`} className="wishlist-link-popup" role="listbox" aria-label="Wishlist items" tabIndex={-1}
      aria-activedescendant={options.length ? `${id}-${active}` : undefined} style={position} onKeyDown={onListKeyDown}>
      {linked && option(0, `Unlink ${linked.item.name}`, <>Unlink <b>{linked.item.name}</b></>, false, 'wishlist-unlink-option')}
      {groups.map(({ bucket, items }) => <div role="group" aria-label={bucket.name} key={bucket.id}>
        <h4 aria-hidden="true">{bucket.name}</h4>
        {items.map(item => {
          const i = index++;
          return option(i, `${item.name} (${formatMoney(item.cost)})`, <><span className="wishlist-link-name">{item.name}</span><span className="wishlist-link-cost">{formatMoney(item.cost)}</span></>,
            linked?.item.id === item.id);
        })}
      </div>)}
      {groups.length === 0 && <p className="wishlist-link-empty">No open wishlist items. Add items on the Wishlist screen.</p>}
    </div>, document.body)}
  </>;
}
