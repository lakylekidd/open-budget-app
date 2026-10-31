import { useId, useMemo, useState, type FormEvent } from 'react';
import { MoneyInput } from '../MoneyInput';
import { LedgerDialog } from '../ledger/LedgerDialog';
import { parseMoney } from '../ledger/api';
import { BucketTable } from './BucketTable';
import { maxBucketNameLength, type BucketEditing } from './BucketNameInput';
import { WishlistIcon } from './WishlistPlanParts';
import {
  isWebLink, reorder, sendWishlistCommand, WishlistConflictError,
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

// Collapsed buckets are remembered per budget in this browser; storage can be unavailable (private mode, blocked site data).
const collapsedKey = (budgetId: string) => `openbudget.wishlist.collapsed.${budgetId}`;
function readCollapsed(budgetId: string): string[] {
  try {
    const stored: unknown = JSON.parse(window.localStorage.getItem(collapsedKey(budgetId)) ?? '[]');
    return Array.isArray(stored) ? stored.filter((id): id is string => typeof id === 'string') : [];
  } catch { return []; }
}
function writeCollapsed(budgetId: string, ids: string[]) {
  try { window.localStorage.setItem(collapsedKey(budgetId), JSON.stringify(ids)); } catch { /* the state just is not remembered */ }
}

export function WishlistPage({ budgetId, currency, access, onAccessChange, onRetry, onNavigate, onRecordPurchase, onOpenTransaction }: Props) {
  const money = useMemo(() => { const format = new Intl.NumberFormat('en-IE', { style: 'currency', currency: currency || 'EUR' }); return (value: number) => format.format(value); }, [currency]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState<Notice | null>(null);
  const idPrefix = useId();
  const [editing, setEditing] = useState<BucketEditing | null>(null);
  const [nameError, setNameError] = useState('');
  const [collapsedState, setCollapsedState] = useState(() => ({ budgetId, ids: readCollapsed(budgetId) }));
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

  const collapsed = collapsedState.budgetId === budgetId ? collapsedState.ids : readCollapsed(budgetId);
  function setCollapsed(ids: string[]) { setCollapsedState({ budgetId, ids }); writeCollapsed(budgetId, ids); }
  function toggleBucket(bucketId: string) { setCollapsed(collapsed.includes(bucketId) ? collapsed.filter(id => id !== bucketId) : [...collapsed, bucketId]); }
  function startEditing(next: BucketEditing) { setError(''); setNameError(''); setEditing(next); }
  function stopEditing() { setError(''); setNameError(''); setEditing(null); }

  // Saves the inline name editor (draft "+" tab or double-click rename). Resolves false to keep the editor open with its error.
  async function commitBucketName(rawName: string): Promise<boolean> {
    const name = rawName.trim();
    if (!name || name.length > maxBucketNameLength) { setNameError(`Enter a bucket name of up to ${maxBucketNameLength} characters.`); return false; }
    if (!editing || access.status !== 'ready') return false;
    setNameError('');
    const known = new Set(access.overview.buckets.map(bucket => bucket.id));
    const saved = editing.kind === 'rename' ? await run(`/buckets/${editing.bucketId}`, 'PUT', { name }) : await run('/buckets', 'POST', { name });
    if (!saved) return false;
    if (editing.kind === 'new') { const created = saved.buckets.find(bucket => !known.has(bucket.id)); if (created && collapsed.includes(created.id)) setCollapsed(collapsed.filter(id => id !== created.id)); }
    setEditing(null);
    return true;
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
  function dropOn(bucket: WishlistBucket, targetItemId?: string, onBucketRow = false) {
    const source = drag;
    setDrag(null); setDropTarget(null);
    if (!source || busy || (onBucketRow && source.bucketId === bucket.id)) return;
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
    {error && !editing && <p className="wishlist-error" role="alert">{error}</p>}
    {notice && <p className="wishlist-notice" role="status">{notice.text}{notice.action && <button type="button" onClick={() => onNavigate(notice.action!.page)}>{notice.action.label}</button>}<button type="button" aria-label="Dismiss" onClick={() => setNotice(null)}>×</button></p>}
    {overview.buckets.length === 0 ? <section className="wishlist-empty"><strong>No Buckets</strong><p>Use the + Bucket button to create a bucket such as Electronics, Games or Small/Large and start ranking the things you want.</p>
        <button type="button" className="wishlist-primary" disabled={busy} onClick={() => startEditing({ kind: 'new' })}>+ Bucket</button></section> : null}
    {(overview.buckets.length > 0 || editing?.kind === 'new') && <BucketTable buckets={overview.buckets} collapsed={collapsed} editing={editing} nameError={nameError || (editing ? error : '')} idPrefix={idPrefix} money={money} busy={busy} drag={drag} dropTarget={dropTarget}
      onToggle={toggleBucket} onToggleAll={() => setCollapsed(overview.buckets.every(bucket => collapsed.includes(bucket.id)) ? [] : overview.buckets.map(bucket => bucket.id))}
      onStartCreate={() => startEditing({ kind: 'new' })} onStartRename={bucketId => startEditing({ kind: 'rename', bucketId })}
      onCommitName={commitBucketName} onCancelEdit={stopEditing} onEditName={() => { setNameError(''); setError(''); }}
      onMoveBucket={(from, to) => moveBucket(overview.buckets, from, to)} onDeleteBucket={bucket => setConfirm({ kind: 'deleteBucket', bucket })}
      onAddItem={bucket => openItemDialog(bucket.id)} onEditItem={item => openItemDialog(item.bucketId, item)}
      onPurchase={(bucket, item) => setConfirm({ kind: 'purchase', bucket, item })} onRemove={item => void run(`/items/${item.id}/remove`, 'POST')} onRestore={item => void run(`/items/${item.id}/restore`, 'POST')}
      onMoveItem={moveItem} onDragStart={source => setDrag(source)} onDragEnd={() => { setDrag(null); setDropTarget(null); }}
      onDragOverTarget={target => setDropTarget(target)} onDropOn={dropOn} onOpenTransaction={onOpenTransaction} />}

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
