import { WishlistProgressBar } from './WishlistPlanParts';
import { bucketPanelId, bucketTabId } from './BucketTabs';
import { addedAgo, type WishlistBucket, type WishlistItem, type WishlistPurchase } from './wishlistApi';

type Money = (value: number) => string;
type DragSource = { bucketId: string; itemId: string };
type DropTarget = { bucketId: string; itemId?: string };

type Props = {
  idPrefix: string;
  bucket: WishlistBucket;
  bucketIndex: number;
  bucketCount: number;
  money: Money;
  busy: boolean;
  drag: DragSource | null;
  dropTarget: DropTarget | null;
  onMoveBucket: (to: number) => void;
  onRenameBucket: () => void;
  onDeleteBucket: () => void;
  onAddItem: () => void;
  onEditItem: (item: WishlistItem) => void;
  onPurchase: (item: WishlistItem) => void;
  onRemove: (item: WishlistItem) => void;
  onRestore: (item: WishlistItem) => void;
  onMoveItem: (from: number, to: number) => void;
  onDragStart: (source: DragSource) => void;
  onDragEnd: () => void;
  onDragOverItem: (target: DropTarget) => void;
  onDropOn: (targetItemId?: string) => void;
  onOpenTransaction?: (purchase: WishlistPurchase) => void;
};

/** Tab panel for the selected bucket: progress, bucket actions, the ranked item rows (styled like Plan subcategory rows) and history. */
export function BucketPanel({ idPrefix, bucket, bucketIndex, bucketCount, money, busy, drag, dropTarget, onMoveBucket, onRenameBucket, onDeleteBucket, onAddItem, onEditItem, onPurchase, onRemove, onRestore, onMoveItem, onDragStart, onDragEnd, onDragOverItem, onDropOn, onOpenTransaction }: Props) {
  const progress = bucket.progress;
  const range = bucket.minCost === null ? 'No items' : bucket.minCost === bucket.maxCost ? money(bucket.minCost) : `${money(bucket.minCost)}–${money(bucket.maxCost ?? bucket.minCost)}`;
  return <section role="tabpanel" id={bucketPanelId(idPrefix)} aria-labelledby={bucketTabId(idPrefix, bucket.id)} tabIndex={-1} className={`wishlist-panel ${progress?.canPurchase ? 'is-ready' : ''}`}>
    <header className="wishlist-panel-head">
      <div className="wishlist-panel-summary">
        {progress ? <>
          <p className="wishlist-amounts" data-testid={`progress-${bucket.id}`}>{money(Math.max(progress.available, 0))} of {money(progress.cost)}{progress.canPurchase ? '' : ` — ${money(progress.remaining)} to go`}</p>
          <WishlistProgressBar progress={progress} money={money} />
          {progress.canPurchase && <p className="wishlist-can-buy">You can buy <b>{progress.nextItemName}</b> now.</p>}
        </> : <p className="wishlist-funds">{money(bucket.available)} available</p>}
        <span className="wishlist-range">{range}</span>
      </div>
      {progress?.canPurchase && <span className="wishlist-ready-badge">Ready to buy</span>}
      <div className="wishlist-bucket-tools" role="group" aria-label={`${bucket.name} bucket actions`}>
        <button type="button" aria-label={`Move ${bucket.name} bucket left`} title="Move bucket left" disabled={busy || bucketIndex === 0} onClick={() => onMoveBucket(bucketIndex - 1)}>←</button>
        <button type="button" aria-label={`Move ${bucket.name} bucket right`} title="Move bucket right" disabled={busy || bucketIndex === bucketCount - 1} onClick={() => onMoveBucket(bucketIndex + 1)}>→</button>
        <button type="button" aria-label={`Rename ${bucket.name} bucket`} disabled={busy} onClick={onRenameBucket}>Rename</button>
        <button type="button" aria-label={`Delete ${bucket.name} bucket`} disabled={busy} onClick={onDeleteBucket}>Delete</button>
      </div>
    </header>

    {bucket.items.length === 0 ? <p className="wishlist-items-empty">No items yet. Add the things you want and rank them by desire.</p>
      : <ol className="wishlist-items" onDragOver={event => { if (drag) event.preventDefault(); }} onDrop={event => { event.preventDefault(); onDropOn(dropTarget?.bucketId === bucket.id ? dropTarget.itemId : undefined); }}>
        {bucket.items.map((item, index) => <li key={item.id} className={`wishlist-row ${index === 0 ? 'is-next' : ''} ${dropTarget?.itemId === item.id ? 'drop-target' : ''}`} draggable={!busy}
          onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', item.id); onDragStart({ bucketId: bucket.id, itemId: item.id }); }}
          onDragEnd={onDragEnd}
          onDragOver={event => { if (!drag) return; event.preventDefault(); onDragOverItem({ bucketId: bucket.id, itemId: item.id }); }}
          onDrop={event => { event.preventDefault(); event.stopPropagation(); onDropOn(item.id); }}>
          <span className="wishlist-handle" aria-hidden="true">⋮⋮</span>
          <div className="wishlist-row-main">
            <div className="wishlist-row-title"><span className="wishlist-rank" aria-label={`Desire rank ${index + 1}`}>{index + 1}</span>
              {item.url ? <a className="wishlist-row-name" href={item.url} target="_blank" rel="noopener noreferrer">{item.name}</a> : <strong className="wishlist-row-name">{item.name}</strong>}
              {index === 0 && <span className="wishlist-next-tag">Next</span>}</div>
            <p className="wishlist-row-detail">{item.description && <>{item.description} · </>}{addedAgo(item.createdAt)}</p>
          </div>
          <strong className="wishlist-row-money">{money(item.cost)}</strong>
          <div className="wishlist-item-tools">
            <button type="button" aria-label={`Move ${item.name} up`} disabled={busy || index === 0} onClick={() => onMoveItem(index, index - 1)}>↑</button>
            <button type="button" aria-label={`Move ${item.name} down`} disabled={busy || index === bucket.items.length - 1} onClick={() => onMoveItem(index, index + 1)}>↓</button>
            <button type="button" aria-label={`Edit ${item.name}`} disabled={busy} onClick={() => onEditItem(item)}>Edit</button>
            <button type="button" aria-label={`Mark ${item.name} purchased`} className={index === 0 && progress?.canPurchase ? 'wishlist-primary' : ''} disabled={busy} onClick={() => onPurchase(item)}>Purchased</button>
            <button type="button" aria-label={`Remove ${item.name}`} disabled={busy} onClick={() => onRemove(item)}>Remove</button>
          </div>
        </li>)}
      </ol>}
    <button type="button" className="wishlist-add-item" disabled={busy} onClick={onAddItem}>+ Add item to {bucket.name}</button>
    <History title="Purchased" items={bucket.purchased} money={money} busy={busy} onRestore={onRestore} onOpenTransaction={onOpenTransaction} />
    <History title="Removed" items={bucket.removed} money={money} busy={busy} onRestore={onRestore} />
  </section>;
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
