import { BucketNameInput, type BucketEditing } from './BucketNameInput';
import { WishlistRowStatus } from './WishlistPlanParts';
import { addedAgo, type WishlistBucket, type WishlistItem, type WishlistPurchase } from './wishlistApi';

type Money = (value: number) => string;
type DragSource = { bucketId: string; itemId: string };
type DropTarget = { bucketId: string; itemId?: string };

type Props = {
  buckets: WishlistBucket[];
  collapsed: string[];
  editing: BucketEditing | null;
  nameError: string;
  /** Unique prefix (from useId) for the inline-error element ids. */
  idPrefix: string;
  money: Money;
  busy: boolean;
  drag: DragSource | null;
  dropTarget: DropTarget | null;
  onToggle: (bucketId: string) => void;
  onToggleAll: () => void;
  onStartCreate: () => void;
  onStartRename: (bucketId: string) => void;
  onCommitName: (name: string) => Promise<boolean>;
  onCancelEdit: () => void;
  onEditName: () => void;
  onMoveBucket: (from: number, to: number) => void;
  onDeleteBucket: (bucket: WishlistBucket) => void;
  onAddItem: (bucket: WishlistBucket) => void;
  onEditItem: (item: WishlistItem) => void;
  onPurchase: (bucket: WishlistBucket, item: WishlistItem) => void;
  onRemove: (item: WishlistItem) => void;
  onRestore: (item: WishlistItem) => void;
  onMoveItem: (bucket: WishlistBucket, from: number, to: number) => void;
  onDragStart: (source: DragSource) => void;
  onDragEnd: () => void;
  onDragOverTarget: (target: DropTarget) => void;
  /** Dropping on an item row reorders (same bucket) or moves (other bucket); dropping on a bucket row moves to that bucket. */
  onDropOn: (bucket: WishlistBucket, targetItemId?: string, onBucketRow?: boolean) => void;
  onOpenTransaction?: (purchase: WishlistPurchase) => void;
};

const Chevron = () => <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="m8 10 4 4 4-4" /></svg>;

/** Plan-style table: each bucket is a collapsible group row (like a Plan category group) and its ranked items are the rows beneath it. */
export function BucketTable({ buckets, collapsed, editing, nameError, idPrefix, money, busy, drag, dropTarget, onToggle, onToggleAll, onStartCreate, onStartRename, onCommitName, onCancelEdit, onEditName, onMoveBucket, onDeleteBucket, onAddItem, onEditItem, onPurchase, onRemove, onRestore, onMoveItem, onDragStart, onDragEnd, onDragOverTarget, onDropOn, onOpenTransaction }: Props) {
  const errorId = `${idPrefix}-name-error`;
  const allCollapsed = buckets.length > 0 && buckets.every(bucket => collapsed.includes(bucket.id));
  const nameInput = (initialValue: string) => <BucketNameInput initialValue={initialValue} busy={busy} error={nameError} errorId={errorId} onCommit={onCommitName} onCancel={onCancelEdit} onEdit={onEditName} />;

  return <div className="wishlist-table" role="table" aria-label="Wishlist buckets">
    <div className="wishlist-table-head" role="row">
      <span className="wishlist-head-name" role="columnheader">
        <button type="button" className="wishlist-collapse" aria-label={allCollapsed ? 'Expand all' : 'Collapse all'} aria-expanded={!allCollapsed} disabled={buckets.length === 0} onClick={onToggleAll}><span className={`group-chevron ${allCollapsed ? 'closed' : ''}`}><Chevron /></span></button>
        <span>BUCKET</span>
      </span>
      <span className="wishlist-head-money" role="columnheader">COST</span>
      <span className="wishlist-head-money" role="columnheader">AVAILABLE</span>
    </div>

    {buckets.map((bucket, bucketIndex) => {
      const isCollapsed = collapsed.includes(bucket.id);
      const progress = bucket.progress;
      const range = bucket.minCost === null ? '' : bucket.minCost === bucket.maxCost ? money(bucket.minCost) : `${money(bucket.minCost)}–${money(bucket.maxCost ?? bucket.minCost)}`;
      const renaming = editing?.kind === 'rename' && editing.bucketId === bucket.id;
      const availableClass = bucket.available < 0 ? 'negative' : progress?.canPurchase ? 'positive' : '';
      return <section key={bucket.id} className="wishlist-group" role="rowgroup" aria-label={bucket.name}>
        <div role="row" className={`wishlist-group-row ${dropTarget?.bucketId === bucket.id && !dropTarget.itemId ? 'drop-target' : ''} ${progress?.canPurchase ? 'is-ready' : ''}`}
          onDragOver={event => { if (!drag || drag.bucketId === bucket.id) return; event.preventDefault(); if (dropTarget?.bucketId !== bucket.id || dropTarget.itemId) onDragOverTarget({ bucketId: bucket.id }); }}
          onDrop={event => { event.preventDefault(); onDropOn(bucket, undefined, true); }}>
          <div className="wishlist-group-name" role="rowheader">
            <button type="button" className="wishlist-collapse" aria-label={`Toggle ${bucket.name}`} aria-expanded={!isCollapsed} onClick={() => onToggle(bucket.id)}><span className={`group-chevron ${isCollapsed ? 'closed' : ''}`}><Chevron /></span></button>
            {renaming ? nameInput(bucket.name)
              : <button type="button" className="wishlist-group-label" title="Click to rename" disabled={busy} onClick={() => onStartRename(bucket.id)}>{bucket.name}</button>}
            {!renaming && <button type="button" className="wishlist-group-add" aria-label={`Add item to ${bucket.name}`} title="Add item" disabled={busy} onClick={() => onAddItem(bucket)}>+</button>}
            {!renaming && progress && <span className="wishlist-group-status" data-testid={`progress-${bucket.id}`}><WishlistRowStatus progress={progress} money={money} /></span>}
            {!renaming && <div className="wishlist-order-tools" role="group" aria-label={`${bucket.name} bucket actions`}>
              <button type="button" aria-label={`Move ${bucket.name} bucket up`} title="Move bucket up" disabled={busy || bucketIndex === 0} onClick={() => onMoveBucket(bucketIndex, bucketIndex - 1)}>↑</button>
              <button type="button" aria-label={`Move ${bucket.name} bucket down`} title="Move bucket down" disabled={busy || bucketIndex === buckets.length - 1} onClick={() => onMoveBucket(bucketIndex, bucketIndex + 1)}>↓</button>
              <button type="button" aria-label={`Delete ${bucket.name} bucket`} title="Delete bucket" disabled={busy} onClick={() => onDeleteBucket(bucket)}>Delete</button>
            </div>}
          </div>
          <span className="wishlist-group-money" role="cell">{range}</span>
          <div className={`available-cell ${availableClass}`} role="cell"><span>{money(bucket.available)}</span></div>
        </div>
        {renaming && nameError && <p id={errorId} className="wishlist-error wishlist-table-error" role="alert">{nameError}</p>}

        {!isCollapsed && <>
          {bucket.items.length === 0 && <p className="wishlist-items-empty" role="row">No items yet. Use + to add the things you want and rank them by desire.</p>}
          {bucket.items.map((item, index) => <div key={item.id} role="row" className={`wishlist-row ${index === 0 ? 'is-next' : ''} ${dropTarget?.itemId === item.id ? 'drop-target' : ''}`} draggable={!busy} aria-label={item.name}
            onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', item.id); onDragStart({ bucketId: bucket.id, itemId: item.id }); }}
            onDragEnd={onDragEnd}
            onDragOver={event => { if (!drag) return; event.preventDefault(); if (dropTarget?.itemId !== item.id) onDragOverTarget({ bucketId: bucket.id, itemId: item.id }); }}
            onDrop={event => { event.preventDefault(); event.stopPropagation(); onDropOn(bucket, item.id); }}>
            <div className="wishlist-row-cell" role="cell">
              <div className="wishlist-row-main">
                <div className="wishlist-row-title"><span className="wishlist-rank" aria-label={`Desire rank ${index + 1}`}>{index + 1}</span>
                  {item.url ? <a className="wishlist-row-name" href={item.url} target="_blank" rel="noopener noreferrer">{item.name}</a> : <strong className="wishlist-row-name">{item.name}</strong>}
                  {index === 0 && <span className="wishlist-next-tag">Next</span>}</div>
                <p className="wishlist-row-detail">{item.description && <>{item.description} · </>}{addedAgo(item.createdAt)}</p>
                {index === 0 && progress && <div className={`progress-track ${progress.canPurchase ? 'funded' : ''}`} role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(Math.min(1, Math.max(0, progress.fundedFraction)) * 100)}
                  aria-label={`${money(Math.max(progress.available, 0))} of ${money(progress.cost)} saved for ${progress.nextItemName}`}><span style={{ width: `${Math.round(Math.min(1, Math.max(0, progress.fundedFraction)) * 100)}%` }} /></div>}
              </div>
              <div className="wishlist-item-tools">
                <button type="button" aria-label={`Move ${item.name} up`} disabled={busy || index === 0} onClick={() => onMoveItem(bucket, index, index - 1)}>↑</button>
                <button type="button" aria-label={`Move ${item.name} down`} disabled={busy || index === bucket.items.length - 1} onClick={() => onMoveItem(bucket, index, index + 1)}>↓</button>
                <button type="button" aria-label={`Edit ${item.name}`} disabled={busy} onClick={() => onEditItem(item)}>Edit</button>
                <button type="button" aria-label={`Mark ${item.name} purchased`} className={index === 0 && progress?.canPurchase ? 'wishlist-primary' : ''} disabled={busy} onClick={() => onPurchase(bucket, item)}>Purchased</button>
                <button type="button" aria-label={`Remove ${item.name}`} disabled={busy} onClick={() => onRemove(item)}>Remove</button>
              </div>
            </div>
            <strong className="wishlist-row-money" role="cell">{money(item.cost)}</strong>
            <span role="cell" />
          </div>)}
          <History title="Purchased" items={bucket.purchased} money={money} busy={busy} onRestore={onRestore} onOpenTransaction={onOpenTransaction} />
          <History title="Removed" items={bucket.removed} money={money} busy={busy} onRestore={onRestore} />
        </>}
      </section>;
    })}

    {editing?.kind === 'new' && <div className="wishlist-group-row is-draft" role="row"><div className="wishlist-group-name" role="rowheader"><span className="wishlist-collapse" aria-hidden="true" />{nameInput('')}</div></div>}
    {editing?.kind === 'new' && nameError && <p id={errorId} className="wishlist-error wishlist-table-error" role="alert">{nameError}</p>}
    <button type="button" className="wishlist-add-bucket" aria-label="Add bucket" disabled={busy} onClick={() => { if (editing?.kind !== 'new') onStartCreate(); }}><span className="circle-plus" aria-hidden="true">+</span> Bucket</button>
  </div>;
}

// Collapsed purchase/removal history; restoring puts the item back at the bottom of its bucket.
// A purchase recorded as a transaction shows "Purchased <date> · <amount spent>" and links to that transaction.
function History({ title, items, money, busy, onRestore, onOpenTransaction }: { title: string; items: WishlistItem[]; money: Money; busy: boolean; onRestore: (item: WishlistItem) => void; onOpenTransaction?: (purchase: WishlistPurchase) => void }) {
  if (items.length === 0) return null;
  const date = (value: string) => new Date(value).toLocaleDateString('en-GB');
  return <details className="wishlist-history">
    <summary>{title} ({items.length})</summary>
    <ul>{items.map(item => <li key={item.id} className="wishlist-row wishlist-history-row"><span className="wishlist-row-name">{item.name}</span>
      {item.purchase ? <small>Purchased {date(`${item.purchase.date}T12:00:00`)} · {money(item.purchase.amount)}{onOpenTransaction && <> · <button type="button" className="wishlist-history-link" aria-label={`View transaction for ${item.name}`} onClick={() => onOpenTransaction(item.purchase!)}>View transaction</button></>}</small>
        : <small>{money(item.cost)} · {date(item.purchasedAt ?? item.removedAt ?? item.createdAt)}</small>}
      <button type="button" aria-label={`Restore ${item.name}`} disabled={busy} onClick={() => onRestore(item)}>Restore</button></li>)}</ul>
  </details>;
}
