import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import type { WishlistBucket } from './wishlistApi';

export const maxBucketNameLength = 60;
export const bucketTabId = (idPrefix: string, bucketId: string) => `${idPrefix}-tab-${bucketId}`;
export const bucketPanelId = (idPrefix: string) => `${idPrefix}-panel`;
const addKey = '+';

/** What the strip is editing inline: a new draft tab at the end, or an existing tab being renamed. */
export type BucketEditing = { kind: 'new' } | { kind: 'rename'; bucketId: string };

type NameInputProps = {
  initialValue: string;
  busy: boolean;
  error: string;
  errorId: string;
  /** Resolves true when saved. A false result keeps the input open so the (inline) error can be fixed. */
  onCommit: (name: string) => Promise<boolean>;
  onCancel: () => void;
  onEdit: () => void;
};

// Inline name editor shared by the draft "+" tab and double-click rename. Enter or blur saves, Escape cancels, and an empty
// (or, when renaming, unchanged) name cancels. `settled` stops Escape/Enter/blur from handling the same edit twice.
function BucketNameInput({ initialValue, busy, error, errorId, onCommit, onCancel, onEdit }: NameInputProps) {
  const [value, setValue] = useState(initialValue);
  const settled = useRef(false);
  const field = useRef<HTMLInputElement>(null);

  async function commit() {
    if (settled.current || busy) return;
    const name = value.trim();
    if (!name || name === initialValue) { settled.current = true; onCancel(); return; }
    settled.current = true;
    if (!(await onCommit(name))) { settled.current = false; field.current?.focus(); }
  }

  useEffect(() => { field.current?.select(); }, []);

  return <input
    className="wishlist-tab-input"
    autoFocus
    ref={field}
    aria-label="Bucket name"
    aria-invalid={error ? true : undefined}
    aria-describedby={error ? errorId : undefined}
    maxLength={maxBucketNameLength}
    readOnly={busy}
    value={value}
    placeholder="Bucket name"
    onChange={event => { setValue(event.target.value); if (error) onEdit(); }}
    onKeyDown={event => {
      if (event.key === 'Enter') { event.preventDefault(); void commit(); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); settled.current = true; onCancel(); }
    }}
    onBlur={() => void commit()}
  />;
}

type Props = {
  buckets: WishlistBucket[];
  selectedId: string | null;
  editing: BucketEditing | null;
  busy: boolean;
  nameError: string;
  /** Unique prefix (from useId) shared with the tab panel so tabs and panel reference each other. */
  idPrefix: string;
  onSelect: (bucketId: string) => void;
  onStartCreate: () => void;
  onStartRename: (bucketId: string) => void;
  onCommitName: (name: string) => Promise<boolean>;
  onCancelEdit: () => void;
  onEditName: () => void;
  /** Highlighted while an item is dragged over a tab; dropping moves the item to that bucket. */
  dropBucketId: string | null;
  onItemDragOver: (bucketId: string) => boolean;
  onItemDrop: (bucketId: string) => void;
};

/** Horizontally scrolling ARIA tab strip: one tab per bucket, an inline draft tab and a trailing "+" tab that creates a bucket. */
export function BucketTabs({ buckets, selectedId, editing, busy, nameError, idPrefix, onSelect, onStartCreate, onStartRename, onCommitName, onCancelEdit, onEditName, dropBucketId, onItemDragOver, onItemDrop }: Props) {
  const errorId = `${idPrefix}-name-error`;
  const hintId = `${idPrefix}-ready-hint`;
  const panelId = bucketPanelId(idPrefix);
  const list = useRef<HTMLDivElement>(null);
  const keys = [...buckets.map(bucket => bucket.id), addKey];

  function focusTab(key: string) { Array.from(list.current?.querySelectorAll<HTMLElement>('[data-tab-key]') ?? []).find(tab => tab.dataset.tabKey === key)?.focus(); }

  // Roving tabindex with automatic activation: arrows move focus and select the bucket; the "+" tab only takes focus.
  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const key = (event.target as HTMLElement).dataset.tabKey;
    if (key === undefined) return;
    const index = keys.indexOf(key);
    let next = -1;
    if (event.key === 'ArrowRight') next = (index + 1) % keys.length;
    else if (event.key === 'ArrowLeft') next = (index - 1 + keys.length) % keys.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = keys.length - 1;
    else if (event.key === 'F2' && key !== addKey) { event.preventDefault(); onStartRename(key); return; }
    if (next < 0) return;
    event.preventDefault();
    focusTab(keys[next]);
    if (keys[next] !== addKey) onSelect(keys[next]);
  }

  const nameInput = (initialValue: string) =>
    <div role="presentation" className="wishlist-tab is-editing"><BucketNameInput initialValue={initialValue} busy={busy} error={nameError} errorId={errorId} onCommit={onCommitName} onCancel={onCancelEdit} onEdit={onEditName} /></div>;

  return <div className="wishlist-tabs">
    <span id={hintId} className="sr-only">Ready to buy</span>
    <div ref={list} className="wishlist-tablist" role="tablist" aria-label="Wishlist buckets" onKeyDown={onKeyDown}>
      {buckets.map(bucket => {
        if (editing?.kind === 'rename' && editing.bucketId === bucket.id) return <div key={bucket.id} role="presentation" className="wishlist-tab-slot">{nameInput(bucket.name)}</div>;
        const selected = bucket.id === selectedId;
        const ready = Boolean(bucket.progress?.canPurchase);
        return <button key={bucket.id} type="button" role="tab" id={bucketTabId(idPrefix, bucket.id)} data-tab-key={bucket.id}
          className={`wishlist-tab ${selected ? 'is-selected' : ''} ${dropBucketId === bucket.id ? 'drop-target' : ''}`}
          aria-selected={selected} aria-controls={selected ? panelId : undefined} aria-describedby={ready ? hintId : undefined}
          title="Double-click to rename" tabIndex={selected ? 0 : -1}
          onClick={() => onSelect(bucket.id)} onDoubleClick={() => { if (!busy) onStartRename(bucket.id); }}
          onDragOver={event => { if (onItemDragOver(bucket.id)) event.preventDefault(); }}
          onDrop={event => { event.preventDefault(); onItemDrop(bucket.id); }}>
          <span className="wishlist-tab-name">{bucket.name}</span>{ready && <span className="wishlist-tab-ready" aria-hidden="true" />}
        </button>;
      })}
      {editing?.kind === 'new' && <div role="presentation" className="wishlist-tab-slot">{nameInput('')}</div>}
      <button type="button" role="tab" data-tab-key={addKey} className="wishlist-tab wishlist-tab-add" aria-selected={false} aria-label="Add bucket"
        title="Add bucket" tabIndex={selectedId === null && buckets.length === 0 ? 0 : -1} disabled={busy}
        onClick={() => { if (editing?.kind === 'new') return; onStartCreate(); }}>+</button>
    </div>
    {nameError && <p id={errorId} className="wishlist-error wishlist-tabs-error" role="alert">{nameError}</p>}
  </div>;
}
