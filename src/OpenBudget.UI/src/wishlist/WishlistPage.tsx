import { useMemo, useState, type FormEvent } from 'react';
import { MoneyInput } from '../MoneyInput';
import { LedgerDialog } from '../ledger/LedgerDialog';
import { parseMoney } from '../ledger/api';
import { WishlistIcon, WishlistProgressBar } from './WishlistPlanParts';
import {
  addedAgo, isWebLink, reorder, sendWishlistCommand, WishlistConflictError,
  type WishlistAccess, type WishlistBucket, type WishlistItem, type WishlistOverview, type WishlistPurchase,
} from './wishlistApi';
import './wishlist.css';

type Props = {
  budgetId: string;
  currency: string;
  access: WishlistAccess;
  onAccessChange: (access: WishlistAccess) => void;
  onRetry: () => void;
  onNavigate: (page: string) => void;
  /** Opens Accounts with the add-transaction dialog prefilled in Wishlist mode for the item. */
  onRecordPurchase?: (item: WishlistItem) => void;
  /** Shows a purchase's linked transaction in its account register. */
  onOpenTransaction?: (purchase: WishlistPurchase) => void;
};
type ItemDraft = { name: string; url: string; cost: string; description: string; bucketId: string };
type Confirm = { kind: 'disable' } | { kind: 'deleteBucket'; bucket: WishlistBucket } | { kind: 'purchase'; bucket: WishlistBucket; item: WishlistItem };
type Notice = { text: string; action?: { label: string; page: string } };

export function WishlistPage({ budgetId, currency, access, onAccessChange, onRetry, onNavigate, onRecordPurchase, onOpenTransaction }: Props) {
  const money = useMemo(() => { const format = new Intl.NumberFormat('en-IE', { style: 'currency', currency: currency || 'EUR' }); return (value: number) => format.format(value); }, [currency]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [bucketDialog, setBucketDialog] = useState<{ bucket?: WishlistBucket } | null>(null);
  const [bucketName, setBucketName] = useState('');
  const [bucketError, setBucketError] = useState('');
  const [itemDialog, setItemDialog] = useState<{ item?: WishlistItem } | null>(null);
  const [itemDraft, setItemDraft] = useState<ItemDraft>({ name: '', url: '', cost: '', description: '', bucketId: '' });
  const [itemError, setItemError] = useState('');
  const [confirm, setConfirm] = useState<Confirm | null>(null);
  const [drag, setDrag] = useState<{ bucketId: string; itemId: string } | null>(null);
  const [dropTarget, setDropTarget] = useState<{ bucketId: string; itemId?: string } | null>(null);

  async function run(path: string, method: 'POST' | 'PUT', body: Record<string, unknown> = {}): Promise<WishlistOverview | null> {
    if (access.status !== 'ready') return null;
    setBusy(true); setError('');
    try {
      const overview = await sendWishlistCommand(budgetId, path, method, { version: access.overview.version, ...body });
      onAccessChange({ status: 'ready', overview });
      return overview;
    } catch (failure) {
      if (failure instanceof WishlistConflictError && failure.current) {
        onAccessChange({ status: 'ready', overview: failure.current });
        setError('Wishlist changed in another session. The latest version is shown; review it and try again.');
      } else setError(failure instanceof Error ? failure.message : 'Wishlist could not be saved.');
      return null;
    } finally { setBusy(false); }
  }

  function openBucketDialog(bucket?: WishlistBucket) { setBucketDialog({ bucket }); setBucketName(bucket?.name ?? ''); setBucketError(''); }
  async function saveBucket(event: FormEvent) {
    event.preventDefault();
    const name = bucketName.trim();
    if (!name || name.length > 60) { setBucketError('Enter a bucket name of up to 60 characters.'); return; }
    const saved = bucketDialog?.bucket ? await run(`/buckets/${bucketDialog.bucket.id}`, 'PUT', { name }) : await run('/buckets', 'POST', { name });
    if (saved) setBucketDialog(null);
  }

  function openItemDialog(bucketId: string, item?: WishlistItem) {
    setItemDialog({ item });
    setItemDraft(item ? { name: item.name, url: item.url ?? '', cost: item.cost.toFixed(2), description: item.description, bucketId: item.bucketId } : { name: '', url: '', cost: '', description: '', bucketId });
    setItemError('');
  }
  async function saveItem(event: FormEvent) {
    event.preventDefault();
    const name = itemDraft.name.trim();
    const cost = parseMoney(itemDraft.cost);
    if (!name || name.length > 120) { setItemError('Enter a name of up to 120 characters.'); return; }
    if (cost === null || cost <= 0) { setItemError('Enter a cost greater than zero, for example 49.99 or 40+9.99.'); return; }
    if (itemDraft.url.trim().length > 2048 || !isWebLink(itemDraft.url)) { setItemError('Links must start with http:// or https://.'); return; }
    if (itemDraft.description.length > 1000) { setItemError('Descriptions can be up to 1000 characters.'); return; }
    const body = { name, url: itemDraft.url.trim() || null, cost, description: itemDraft.description.trim(), bucketId: itemDraft.bucketId };
    const saved = itemDialog?.item ? await run(`/items/${itemDialog.item.id}`, 'PUT', body) : await run(`/buckets/${itemDraft.bucketId}/items`, 'POST', body);
    if (saved) setItemDialog(null);
  }

  function moveItem(bucket: WishlistBucket, from: number, to: number) {
    const next = reorder(bucket.items, from, to);
    if (next === bucket.items) return;
    void run(`/buckets/${bucket.id}/items/order`, 'PUT', { itemIds: next.map(item => item.id) });
  }
  function moveBucket(buckets: WishlistBucket[], from: number, to: number) {
    const next = reorder(buckets, from, to);
    if (next !== buckets) void run('/buckets/order', 'PUT', { bucketIds: next.map(bucket => bucket.id) });
  }
  function dropOn(bucket: WishlistBucket, targetItemId?: string) {
    const source = drag;
    setDrag(null); setDropTarget(null);
    if (!source || busy) return;
    if (source.bucketId !== bucket.id) {
      const sourceItem = access.status === 'ready' ? access.overview.buckets.find(b => b.id === source.bucketId)?.items.find(item => item.id === source.itemId) : undefined;
      if (sourceItem) void run(`/items/${sourceItem.id}`, 'PUT', { name: sourceItem.name, url: sourceItem.url, cost: sourceItem.cost, description: sourceItem.description, bucketId: bucket.id });
      return;
    }
    const from = bucket.items.findIndex(item => item.id === source.itemId);
    const to = targetItemId ? bucket.items.findIndex(item => item.id === targetItemId) : bucket.items.length - 1;
    moveItem(bucket, from, to);
  }

  async function confirmAction() {
    if (!confirm) return;
    if (confirm.kind === 'disable') { if (await run('/disable', 'POST')) { setConfirm(null); setNotice({ text: 'Wishlist is off. Its Plan categories are archived with their money until you turn it back on.' }); } return; }
    if (confirm.kind === 'deleteBucket') { if (await run(`/buckets/${confirm.bucket.id}/delete`, 'POST')) setConfirm(null); return; }
    const { bucket, item } = confirm;
    if (await run(`/items/${item.id}/purchase`, 'POST')) {
      setConfirm(null);
      setNotice({ text: `${item.name} moved to Purchased. Record the spending in an account with the category “${bucket.name}” so the money leaves this bucket.`, action: { label: 'Record spending in Accounts', page: 'Accounts' } });
    }
  }

  const heading = <div className="wishlist-heading"><div><span className="eyebrow">SAVE FOR WHAT YOU WANT</span><h1>Wishlist</h1></div>
    {access.status === 'ready' && access.overview.isEnabled && <div className="wishlist-heading-actions">
      <button type="button" className="wishlist-primary" disabled={busy} onClick={() => openBucketDialog()}>Add bucket</button>
      <button type="button" disabled={busy} onClick={() => setConfirm({ kind: 'disable' })}>Turn off Wishlist</button>
    </div>}</div>;

  if (access.status === 'loading') return <div className="wishlist-page">{heading}<p className="wishlist-state" aria-busy="true">Loading wishlist…</p></div>;
  if (access.status === 'error') return <div className="wishlist-page">{heading}<p className="wishlist-state wishlist-error" role="alert">{access.message} <button type="button" onClick={onRetry}>Retry</button></p></div>;
  if (access.status === 'forbidden') return <div className="wishlist-page">{heading}<section className="wishlist-intro"><div className="wishlist-intro-icon"><WishlistIcon size={28} /></div><h2>Wishlist is a Plus and Pro feature</h2><p>Your current membership does not include Wishlist. Ask your administrator to upgrade your membership or enable Wishlist for your account.</p></section></div>;

  const overview = access.overview;
  if (!overview.isEnabled) return <div className="wishlist-page">{heading}
    {error && <p className="wishlist-error" role="alert">{error}</p>}
    {notice && <p className="wishlist-notice" role="status">{notice.text}</p>}
    <section className="wishlist-intro" aria-label="Enable Wishlist">
      <div className="wishlist-intro-icon"><WishlistIcon size={28} /></div>
      <h2>Save for the things you want</h2>
      <p>Group wishes into buckets like Electronics or Games, rank items by how much you want them, and fund each bucket from Plan. Wishlist tells you when the top item is affordable.</p>
      <ul><li>Each bucket becomes a category in a <b>Wishlist</b> group in Plan.</li><li>Assign money to it like any other category.</li><li>Turning Wishlist off archives those categories without moving money.</li></ul>
      {overview.buckets.length > 0 && <p>Your {overview.buckets.length} saved bucket{overview.buckets.length === 1 ? '' : 's'} and their money return when you turn Wishlist back on.</p>}
      <button type="button" className="wishlist-primary" disabled={busy} onClick={() => void run('/enable', 'POST')}>Enable Wishlist</button>
    </section></div>;

  return <div className="wishlist-page">
    {heading}
    <p className="wishlist-subtitle">Each bucket is funded by its Plan category. Progress uses Available in {new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(new Date(`${overview.month}-01T12:00:00`))}.</p>
    {error && <p className="wishlist-error" role="alert">{error}</p>}
    {notice && <p className="wishlist-notice" role="status">{notice.text}{notice.action && <button type="button" onClick={() => onNavigate(notice.action!.page)}>{notice.action.label}</button>}<button type="button" aria-label="Dismiss" onClick={() => setNotice(null)}>×</button></p>}
    {overview.buckets.length === 0 ? <section className="wishlist-empty"><strong>No Buckets</strong><p>Create a bucket such as Electronics, Games or Small/Large to start ranking the things you want.</p><button type="button" className="wishlist-primary" disabled={busy} onClick={() => openBucketDialog()}>Add bucket</button></section>
      : <div className="wishlist-buckets">{overview.buckets.map((bucket, bucketIndex) => {
        const progress = bucket.progress;
        return <section key={bucket.id} className={`wishlist-bucket ${progress?.canPurchase ? 'is-ready' : ''}`} aria-label={`${bucket.name} bucket`}>
          <header className="wishlist-bucket-head">
            <div><h2>{bucket.name}</h2><span className="wishlist-range">{bucket.minCost === null ? 'No items' : bucket.minCost === bucket.maxCost ? money(bucket.minCost) : `${money(bucket.minCost)}–${money(bucket.maxCost ?? bucket.minCost)}`}</span></div>
            {progress?.canPurchase && <span className="wishlist-ready-badge">Ready to buy</span>}
            <div className="wishlist-bucket-tools">
              <button type="button" aria-label={`Move ${bucket.name} bucket up`} disabled={busy || bucketIndex === 0} onClick={() => moveBucket(overview.buckets, bucketIndex, bucketIndex - 1)}>↑</button>
              <button type="button" aria-label={`Move ${bucket.name} bucket down`} disabled={busy || bucketIndex === overview.buckets.length - 1} onClick={() => moveBucket(overview.buckets, bucketIndex, bucketIndex + 1)}>↓</button>
              <button type="button" aria-label={`Rename ${bucket.name} bucket`} disabled={busy} onClick={() => openBucketDialog(bucket)}>Rename</button>
              <button type="button" aria-label={`Delete ${bucket.name} bucket`} disabled={busy} onClick={() => setConfirm({ kind: 'deleteBucket', bucket })}>Delete</button>
            </div>
          </header>
          {progress ? <div className="wishlist-progress-block">
            <p className="wishlist-amounts" data-testid={`progress-${bucket.id}`}>{money(Math.max(progress.available, 0))} of {money(progress.cost)}{progress.canPurchase ? '' : ` — ${money(progress.remaining)} to go`}</p>
            <WishlistProgressBar progress={progress} money={money} />
            {progress.canPurchase && <p className="wishlist-can-buy">You can buy <b>{progress.nextItemName}</b> now.</p>}
          </div> : <p className="wishlist-funds">{money(bucket.available)} available</p>}
          {bucket.items.length === 0 ? <p className="wishlist-items-empty">No items yet. Add the things you want and rank them by desire.</p>
            : <ol className="wishlist-items" onDragOver={event => { if (drag) { event.preventDefault(); } }} onDrop={event => { event.preventDefault(); dropOn(bucket, dropTarget?.bucketId === bucket.id ? dropTarget.itemId : undefined); }}>
              {bucket.items.map((item, index) => <li key={item.id} className={`wishlist-item ${index === 0 ? 'is-next' : ''} ${dropTarget?.itemId === item.id ? 'drop-target' : ''}`} draggable={!busy}
                onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', item.id); setDrag({ bucketId: bucket.id, itemId: item.id }); }}
                onDragEnd={() => { setDrag(null); setDropTarget(null); }}
                onDragOver={event => { if (!drag) return; event.preventDefault(); setDropTarget({ bucketId: bucket.id, itemId: item.id }); }}
                onDrop={event => { event.preventDefault(); event.stopPropagation(); dropOn(bucket, item.id); }}>
                <span className="wishlist-handle" aria-hidden="true">⋮⋮</span>
                <span className="wishlist-rank">{index + 1}</span>
                <div className="wishlist-item-main">
                  <div className="wishlist-item-title">{item.url ? <a href={item.url} target="_blank" rel="noopener noreferrer">{item.name}</a> : <strong>{item.name}</strong>}{index === 0 && <span className="wishlist-next-tag">Next</span>}</div>
                  {item.description && <p>{item.description}</p>}
                  <small>{addedAgo(item.createdAt)}</small>
                </div>
                <strong className="wishlist-cost">{money(item.cost)}</strong>
                <div className="wishlist-item-tools">
                  <button type="button" aria-label={`Move ${item.name} up`} disabled={busy || index === 0} onClick={() => moveItem(bucket, index, index - 1)}>↑</button>
                  <button type="button" aria-label={`Move ${item.name} down`} disabled={busy || index === bucket.items.length - 1} onClick={() => moveItem(bucket, index, index + 1)}>↓</button>
                  <button type="button" aria-label={`Edit ${item.name}`} disabled={busy} onClick={() => openItemDialog(bucket.id, item)}>Edit</button>
                  <button type="button" aria-label={`Mark ${item.name} purchased`} className={index === 0 && progress?.canPurchase ? 'wishlist-primary' : ''} disabled={busy} onClick={() => setConfirm({ kind: 'purchase', bucket, item })}>Purchased</button>
                  <button type="button" aria-label={`Remove ${item.name}`} disabled={busy} onClick={() => void run(`/items/${item.id}/remove`, 'POST')}>Remove</button>
                </div>
              </li>)}
            </ol>}
          <button type="button" className="wishlist-add-item" disabled={busy} onClick={() => openItemDialog(bucket.id)}>+ Add item to {bucket.name}</button>
          <History title="Purchased" items={bucket.purchased} money={money} busy={busy} onRestore={item => void run(`/items/${item.id}/restore`, 'POST')} onOpenTransaction={onOpenTransaction} />
          <History title="Removed" items={bucket.removed} money={money} busy={busy} onRestore={item => void run(`/items/${item.id}/restore`, 'POST')} />
        </section>;
      })}</div>}

    {bucketDialog && <LedgerDialog title={bucketDialog.bucket ? 'Rename bucket' : 'New bucket'} onClose={() => setBucketDialog(null)}>
      <form className="wishlist-form" onSubmit={event => void saveBucket(event)} noValidate>
        <label>Bucket name<input autoFocus maxLength={60} value={bucketName} onChange={event => setBucketName(event.target.value)} placeholder="Electronics" /></label>
        {(bucketError || error) && <p className="wishlist-error" role="alert">{bucketError || error}</p>}
        <footer><button type="button" onClick={() => setBucketDialog(null)}>Cancel</button><button type="submit" className="button-primary" disabled={busy}>{busy ? 'Saving…' : 'Save bucket'}</button></footer>
      </form>
    </LedgerDialog>}

    {itemDialog && <LedgerDialog title={itemDialog.item ? 'Edit item' : 'New wishlist item'} onClose={() => setItemDialog(null)}>
      <form className="wishlist-form" onSubmit={event => void saveItem(event)} noValidate>
        <label>Name<input autoFocus maxLength={120} value={itemDraft.name} onChange={event => setItemDraft(draft => ({ ...draft, name: event.target.value }))} /></label>
        <label>Cost<MoneyInput aria-label="Cost" className="wishlist-input" value={itemDraft.cost} onChange={cost => setItemDraft(draft => ({ ...draft, cost }))} placeholder="0.00" /></label>
        <label>Link<input type="url" maxLength={2048} value={itemDraft.url} onChange={event => setItemDraft(draft => ({ ...draft, url: event.target.value }))} placeholder="https://" /></label>
        <label>Description<textarea maxLength={1000} rows={3} value={itemDraft.description} onChange={event => setItemDraft(draft => ({ ...draft, description: event.target.value }))} /></label>
        {itemDialog.item && <label>Bucket<select value={itemDraft.bucketId} onChange={event => setItemDraft(draft => ({ ...draft, bucketId: event.target.value }))}>{overview.buckets.map(bucket => <option key={bucket.id} value={bucket.id}>{bucket.name}</option>)}</select></label>}
        {(itemError || error) && <p className="wishlist-error" role="alert">{itemError || error}</p>}
        <footer><button type="button" onClick={() => setItemDialog(null)}>Cancel</button><button type="submit" className="button-primary" disabled={busy}>{busy ? 'Saving…' : 'Save item'}</button></footer>
      </form>
    </LedgerDialog>}

    {confirm && <LedgerDialog title={confirm.kind === 'disable' ? 'Turn off Wishlist?' : confirm.kind === 'deleteBucket' ? `Delete ${confirm.bucket.name}?` : 'Mark as purchased?'} onClose={() => setConfirm(null)}>
      <div className="wishlist-form">
        <p>{confirm.kind === 'disable' ? 'The Wishlist screen is hidden and the Wishlist group in Plan is archived. Buckets, items and any money in their categories are kept and come back when you turn Wishlist on again.'
          : confirm.kind === 'deleteBucket' ? `The bucket and its Plan category are archived. A bucket that still holds money cannot be deleted; move its money to another category in Plan first.`
          : onRecordPurchase ? `Record purchase opens an account's add-transaction form prefilled with ${confirm.item.name} (${money(confirm.item.cost)}) in the category “${confirm.bucket.name}”; saving it marks the item purchased. Mark purchased only moves it to the Purchased history without recording spending.`
          : `${confirm.item.name} (${money(confirm.item.cost)}) moves to the Purchased history of ${confirm.bucket.name}. Afterwards, record the spending in an account with the category “${confirm.bucket.name}”.`}</p>
        {error && <p className="wishlist-error" role="alert">{error}</p>}
        <footer><button type="button" onClick={() => setConfirm(null)}>Cancel</button>
          {confirm.kind === 'purchase' && onRecordPurchase ? <>
            <button type="button" disabled={busy} onClick={() => void confirmAction()}>Mark purchased only</button>
            <button type="button" className="button-primary" disabled={busy} onClick={() => { const { item } = confirm; setConfirm(null); onRecordPurchase(item); }}>Record purchase</button>
          </> : <button type="button" className="button-primary" disabled={busy} onClick={() => void confirmAction()}>{confirm.kind === 'disable' ? 'Turn off' : confirm.kind === 'deleteBucket' ? 'Delete bucket' : 'Mark purchased'}</button>}</footer>
      </div>
    </LedgerDialog>}
  </div>;
}

// Collapsed purchase/removal history; restoring puts the item back at the bottom of its bucket.
// A purchase recorded as a transaction shows "Purchased <date> · <amount spent>" and links to that transaction.
function History({ title, items, money, busy, onRestore, onOpenTransaction }: { title: string; items: WishlistItem[]; money: (value: number) => string; busy: boolean; onRestore: (item: WishlistItem) => void; onOpenTransaction?: (purchase: WishlistPurchase) => void }) {
  if (items.length === 0) return null;
  const date = (value: string) => new Date(value).toLocaleDateString('en-GB');
  return <details className="wishlist-history">
    <summary>{title} ({items.length})</summary>
    <ul>{items.map(item => <li key={item.id}><span>{item.name}</span>
      {item.purchase ? <small>Purchased {date(`${item.purchase.date}T12:00:00`)} · {money(item.purchase.amount)}{onOpenTransaction && <> · <button type="button" className="wishlist-history-link" aria-label={`View transaction for ${item.name}`} onClick={() => onOpenTransaction(item.purchase!)}>View transaction</button></>}</small>
        : <small>{money(item.cost)} · {date(item.purchasedAt ?? item.removedAt ?? item.createdAt)}</small>}
      <button type="button" aria-label={`Restore ${item.name}`} disabled={busy} onClick={() => onRestore(item)}>Restore</button></li>)}</ul>
  </details>;
}
