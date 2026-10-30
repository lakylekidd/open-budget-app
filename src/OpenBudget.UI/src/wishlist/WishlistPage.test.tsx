import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { WishlistPage } from './WishlistPage';
import { addedAgo, isWebLink, type WishlistAccess, type WishlistItem, type WishlistOverview } from './wishlistApi';

// A small in-memory Wishlist server: progress is computed here the way the API does (top active item vs Available).
type ServerItem = WishlistItem;
let enabled: boolean;
let version: number;
let buckets: { id: string; name: string; available: number }[];
let items: ServerItem[];
let requests: { path: string; method: string; body: Record<string, unknown> }[];

function overview(): WishlistOverview {
  const result = buckets.map((bucket, index) => {
    const active = items.filter(item => item.bucketId === bucket.id && item.status === 'active').sort((a, b) => a.sortOrder - b.sortOrder);
    const next = active[0];
    const progress = enabled && next ? { nextItemId: next.id, nextItemName: next.name, cost: next.cost, available: bucket.available, remaining: Math.max(next.cost - bucket.available, 0), canPurchase: bucket.available >= next.cost, fundedFraction: Math.min(1, Math.max(0, bucket.available / next.cost)) } : null;
    return { id: bucket.id, name: bucket.name, sortOrder: index, categoryId: `wl-${bucket.id}`, available: bucket.available,
      minCost: active.length ? Math.min(...active.map(item => item.cost)) : null, maxCost: active.length ? Math.max(...active.map(item => item.cost)) : null,
      progress, items: active, purchased: items.filter(item => item.bucketId === bucket.id && item.status === 'purchased'), removed: items.filter(item => item.bucketId === bucket.id && item.status === 'removed') };
  });
  return { isEnabled: enabled, version, month: '2026-10', readyCount: result.filter(bucket => bucket.progress?.canPurchase).length, buckets: result };
}

const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const item = (id: string, bucketId: string, name: string, cost: number, sortOrder: number, extra: Partial<ServerItem> = {}): ServerItem =>
  ({ id, bucketId, name, url: null, cost, description: '', sortOrder, status: 'active', createdAt: '2026-07-21T12:00:00Z', purchasedAt: null, removedAt: null, ...extra });

beforeEach(() => {
  enabled = true; version = 3; requests = [];
  buckets = [{ id: 'b1', name: 'Games', available: 60 }];
  items = [item('i1', 'b1', 'Racing game', 59.99, 0, { url: 'https://shop.example/racing', description: 'Deluxe edition' }), item('i2', 'b1', 'Console', 450, 1)];
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input).replace('/api/budgets/budget-1/wishlist', '');
    const method = init?.method ?? 'GET';
    const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : {};
    if (method !== 'GET') requests.push({ path, method, body });
    if (method !== 'GET' && body.version !== version) return json({ error: 'Wishlist changed in another session.', current: overview() }, 409);
    if (path === '/enable') enabled = true;
    else if (path === '/disable') enabled = false;
    else if (path === '/buckets' && method === 'POST') buckets.push({ id: `b${buckets.length + 1}`, name: String(body.name), available: 0 });
    else if (/^\/buckets\/[^/]+\/items$/.test(path)) {
      const bucketId = path.split('/')[2];
      items.push(item(`i${items.length + 1}`, bucketId, String(body.name), Number(body.cost), items.filter(x => x.bucketId === bucketId && x.status === 'active').length, { url: (body.url as string | null) ?? null, description: String(body.description ?? '') }));
    } else if (/^\/buckets\/[^/]+\/items\/order$/.test(path)) {
      (body.itemIds as string[]).forEach((id, index) => { items.find(x => x.id === id)!.sortOrder = index; });
    } else if (/^\/items\/[^/]+\/purchase$/.test(path)) {
      const target = items.find(x => x.id === path.split('/')[2])!;
      target.status = 'purchased'; target.purchasedAt = '2026-10-21T12:00:00Z';
      items.filter(x => x.bucketId === target.bucketId && x.status === 'active').sort((a, b) => a.sortOrder - b.sortOrder).forEach((x, index) => { x.sortOrder = index; });
    } else if (method !== 'GET') return json({ error: 'Unexpected' }, 400);
    if (method !== 'GET') version++;
    return json(overview());
  }));
});
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });

function Harness({ initial, onNavigate = () => undefined }: { initial: WishlistAccess; onNavigate?: (page: string) => void }) {
  const [access, setAccess] = useState<WishlistAccess>(initial);
  return <WishlistPage budgetId="budget-1" currency="EUR" access={access} onAccessChange={setAccess} onRetry={() => undefined} onNavigate={onNavigate} />;
}

it('explains the upgrade when the membership does not include Wishlist', () => {
  render(<Harness initial={{ status: 'forbidden' }} />);
  expect(screen.getByRole('heading', { name: 'Wishlist is a Plus and Pro feature' })).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Enable Wishlist' })).not.toBeInTheDocument();
});

it('enables Wishlist, creates a bucket and adds an item using money arithmetic', async () => {
  enabled = false; buckets = []; items = [];
  render(<Harness initial={{ status: 'ready', overview: overview() }} />);
  expect(screen.getByRole('region', { name: 'Enable Wishlist' })).toHaveTextContent('Turning Wishlist off archives those categories without moving money.');
  await userEvent.click(screen.getByRole('button', { name: 'Enable Wishlist' }));
  expect(await screen.findByText('No Buckets')).toBeInTheDocument();
  expect(requests[0]).toEqual({ path: '/enable', method: 'POST', body: { version: 3 } });

  await userEvent.click(screen.getAllByRole('button', { name: 'Add bucket' })[0]);
  const bucketDialog = screen.getByRole('dialog', { name: 'New bucket' });
  await userEvent.click(within(bucketDialog).getByRole('button', { name: 'Save bucket' }));
  expect(within(bucketDialog).getByRole('alert')).toHaveTextContent('Enter a bucket name');
  await userEvent.type(within(bucketDialog).getByLabelText('Bucket name'), 'Electronics');
  await userEvent.click(within(bucketDialog).getByRole('button', { name: 'Save bucket' }));
  const bucket = await screen.findByRole('region', { name: 'Electronics bucket' });
  expect(bucket).toHaveTextContent('No items yet');

  await userEvent.click(within(bucket).getByRole('button', { name: '+ Add item to Electronics' }));
  const itemDialog = screen.getByRole('dialog', { name: 'New wishlist item' });
  await userEvent.type(within(itemDialog).getByLabelText('Name'), 'Headphones');
  const cost = within(itemDialog).getByRole('textbox', { name: 'Cost' });
  expect(cost.closest('.money-input-shell')?.querySelector('svg')).not.toBeNull();
  await userEvent.type(cost, '40+9.99');
  await userEvent.type(within(itemDialog).getByLabelText('Link'), 'javascript:alert(1)');
  await userEvent.click(within(itemDialog).getByRole('button', { name: 'Save item' }));
  expect(within(itemDialog).getByRole('alert')).toHaveTextContent('Links must start with http:// or https://.');
  await userEvent.clear(within(itemDialog).getByLabelText('Link'));
  await userEvent.type(within(itemDialog).getByLabelText('Link'), 'https://shop.example/headphones');
  await userEvent.click(within(itemDialog).getByRole('button', { name: 'Save item' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  expect(requests.at(-1)).toEqual({ path: '/buckets/b1/items', method: 'POST', body: { version: 5, name: 'Headphones', url: 'https://shop.example/headphones', cost: 49.99, description: '', bucketId: 'b1' } });
  const link = within(screen.getByRole('region', { name: 'Electronics bucket' })).getByRole('link', { name: 'Headphones' });
  expect(link).toHaveAttribute('target', '_blank');
  expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  expect(screen.getByTestId('progress-b1')).toHaveTextContent('€0.00 of €49.99 — €49.99 to go');
});

it('shows the can-purchase state and reordering with keyboard buttons changes the next item and progress', async () => {
  vi.spyOn(Date, 'now').mockReturnValue(new Date('2026-10-21T12:00:00Z').getTime());
  render(<Harness initial={{ status: 'ready', overview: overview() }} />);
  const bucket = screen.getByRole('region', { name: 'Games bucket' });
  expect(within(bucket).getByText('Ready to buy')).toBeInTheDocument();
  expect(within(bucket).getByText(/You can buy/)).toHaveTextContent('You can buy Racing game now.');
  expect(screen.getByTestId('progress-b1')).toHaveTextContent('€60.00 of €59.99');
  expect(within(bucket).getByText('€59.99–€450.00')).toBeInTheDocument();
  expect(within(bucket).getAllByText(/added 3 months ago/)).toHaveLength(2);

  await userEvent.click(within(bucket).getByRole('button', { name: 'Move Console up' }));
  await waitFor(() => expect(screen.getByTestId('progress-b1')).toHaveTextContent('€60.00 of €450.00 — €390.00 to go'));
  expect(requests.at(-1)).toEqual({ path: '/buckets/b1/items/order', method: 'PUT', body: { version: 3, itemIds: ['i2', 'i1'] } });
  expect(within(bucket).queryByText('Ready to buy')).not.toBeInTheDocument();
  expect(within(bucket).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '13');
  const first = within(bucket).getAllByRole('listitem')[0];
  expect(first).toHaveTextContent('Console');
  expect(first).toHaveTextContent('Next');
});

it('reorders by drag and drop', async () => {
  render(<Harness initial={{ status: 'ready', overview: overview() }} />);
  const bucket = screen.getByRole('region', { name: 'Games bucket' });
  const [racing, consoleItem] = within(bucket).getAllByRole('listitem');
  fireEvent.dragStart(consoleItem, { dataTransfer: { effectAllowed: '', setData: () => undefined } });
  fireEvent.dragOver(racing, { dataTransfer: {} });
  fireEvent.drop(racing, { dataTransfer: {} });
  await waitFor(() => expect(within(bucket).getAllByRole('listitem')[0]).toHaveTextContent('Console'));
  expect(requests.at(-1)?.body).toEqual({ version: 3, itemIds: ['i2', 'i1'] });
});

it('marks the next item purchased, keeps it in history and offers to record the spending', async () => {
  const navigate = vi.fn();
  render(<Harness initial={{ status: 'ready', overview: overview() }} onNavigate={navigate} />);
  const bucket = screen.getByRole('region', { name: 'Games bucket' });
  await userEvent.click(within(bucket).getByRole('button', { name: 'Mark Racing game purchased' }));
  await userEvent.click(within(screen.getByRole('dialog', { name: 'Mark as purchased?' })).getByRole('button', { name: 'Mark purchased' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Record the spending in an account with the category “Games”');
  const history = within(bucket).getByText('Purchased (1)');
  await userEvent.click(history);
  expect(history.closest('details')).toHaveTextContent('Racing game');
  expect(within(bucket).getAllByRole('listitem')[0]).toHaveTextContent('Console');
  await userEvent.click(screen.getByRole('button', { name: 'Record spending in Accounts' }));
  expect(navigate).toHaveBeenCalledWith('Accounts');
});

it('recovers from a stale version by showing the latest wishlist', async () => {
  render(<Harness initial={{ status: 'ready', overview: { ...overview(), version: 1 } }} />);
  await userEvent.click(within(screen.getByRole('region', { name: 'Games bucket' })).getByRole('button', { name: 'Move Console up' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Wishlist changed in another session');
  // The refreshed version lets the retry succeed.
  await userEvent.click(within(screen.getByRole('region', { name: 'Games bucket' })).getByRole('button', { name: 'Move Console up' }));
  await waitFor(() => expect(requests.at(-1)?.body.version).toBe(3));
  await waitFor(() => expect(within(screen.getByRole('region', { name: 'Games bucket' })).getAllByRole('listitem')[0]).toHaveTextContent('Console'));
});

it('formats item age and validates web links', () => {
  const now = new Date('2026-10-21T12:00:00Z').getTime();
  expect(addedAgo('2026-10-21T08:00:00Z', now)).toBe('added today');
  expect(addedAgo('2026-10-11T12:00:00Z', now)).toBe('added 10 days ago');
  expect(addedAgo('2026-07-21T12:00:00Z', now)).toBe('added 3 months ago');
  expect(addedAgo('2024-10-01T12:00:00Z', now)).toBe('added 2 years ago');
  expect(isWebLink('https://shop.example/x')).toBe(true);
  expect(isWebLink('')).toBe(true);
  expect(isWebLink('ftp://shop.example')).toBe(false);
  expect(isWebLink('javascript:alert(1)')).toBe(false);
});

it('offers Record purchase next to Mark purchased and hands the item to the account flow', async () => {
  const record = vi.fn();
  render(<WishlistPage budgetId="budget-1" currency="EUR" access={{ status: 'ready', overview: overview() }} onAccessChange={() => undefined} onRetry={() => undefined} onNavigate={() => undefined} onRecordPurchase={record} />);
  await userEvent.click(screen.getByRole('button', { name: 'Mark Racing game purchased' }));
  const dialog = screen.getByRole('dialog', { name: 'Mark as purchased?' });
  expect(dialog).toHaveTextContent('Record purchase opens an account');
  expect(within(dialog).getByRole('button', { name: 'Mark purchased only' })).toBeEnabled();
  await userEvent.click(within(dialog).getByRole('button', { name: 'Record purchase' }));
  expect(record).toHaveBeenCalledWith(expect.objectContaining({ id: 'i1', name: 'Racing game' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  // Opening the account flow does not mark the item purchased; saving the transaction does.
  expect(requests).toHaveLength(0);
});

it('shows a recorded purchase with its date and amount and links to the transaction', async () => {
  items[0] = { ...items[0], status: 'purchased', purchasedAt: '2026-10-20T12:00:00Z', purchase: { transactionId: 't-9', accountId: 'checking', date: '2026-10-19', amount: 54.5 } };
  items.push(item('i3', 'b1', 'Old controller', 25, 2, { status: 'purchased', purchasedAt: '2026-09-01T12:00:00Z' }));
  const open = vi.fn();
  render(<WishlistPage budgetId="budget-1" currency="EUR" access={{ status: 'ready', overview: overview() }} onAccessChange={() => undefined} onRetry={() => undefined} onNavigate={() => undefined} onOpenTransaction={open} />);
  const summary = screen.getByText('Purchased (2)');
  const history = summary.closest('details')!;
  await userEvent.click(summary);
  expect(history).toHaveTextContent('Purchased 19/10/2026 · €54.50');
  // A plain Mark purchased item has no linked transaction.
  expect(history).toHaveTextContent('€25.00 · 01/09/2026');
  expect(within(history).getAllByRole('button', { name: /View transaction/ })).toHaveLength(1);
  await userEvent.click(within(history).getByRole('button', { name: 'View transaction for Racing game' }));
  expect(open).toHaveBeenCalledWith({ transactionId: 't-9', accountId: 'checking', date: '2026-10-19', amount: 54.5 });
});
