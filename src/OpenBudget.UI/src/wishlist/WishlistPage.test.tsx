import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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
  window.localStorage.clear();
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

  await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
  await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), 'Electronics{Enter}');
  const bucket = await screen.findByRole('tabpanel', { name: 'Electronics' });
  expect(bucket).toHaveTextContent('No items yet');
  expect(screen.getByRole('tab', { name: 'Electronics' })).toHaveAttribute('aria-selected', 'true');
  expect(requests.at(-1)).toEqual({ path: '/buckets', method: 'POST', body: { version: 4, name: 'Electronics' } });

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
  const link = within(screen.getByRole('tabpanel', { name: 'Electronics' })).getByRole('link', { name: 'Headphones' });
  expect(link).toHaveAttribute('target', '_blank');
  expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  expect(screen.getByTestId('progress-b1')).toHaveTextContent('€0.00 of €49.99 — €49.99 to go');
});

it('shows the can-purchase state and reordering with keyboard buttons changes the next item and progress', async () => {
  vi.spyOn(Date, 'now').mockReturnValue(new Date('2026-10-21T12:00:00Z').getTime());
  render(<Harness initial={{ status: 'ready', overview: overview() }} />);
  const bucket = screen.getByRole('tabpanel', { name: 'Games' });
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
  const bucket = screen.getByRole('tabpanel', { name: 'Games' });
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
  const bucket = screen.getByRole('tabpanel', { name: 'Games' });
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

it('shows rows without a checkbox, in the shared Plan subcategory row style', () => {
  render(<Harness initial={{ status: 'ready', overview: overview() }} />);
  const panel = screen.getByRole('tabpanel', { name: 'Games' });
  const rows = within(panel).getAllByRole('listitem');
  expect(rows).toHaveLength(2);
  expect(within(panel).queryByRole('checkbox')).not.toBeInTheDocument();
  rows.forEach(row => { expect(row).toHaveClass('wishlist-row'); expect(row.querySelector('input')).toBeNull(); });
  expect(within(rows[0]).getByText('€59.99')).toHaveClass('wishlist-row-money');
});

it('recovers from a stale version by showing the latest wishlist', async () => {
  render(<Harness initial={{ status: 'ready', overview: { ...overview(), version: 1 } }} />);
  await userEvent.click(within(screen.getByRole('tabpanel', { name: 'Games' })).getByRole('button', { name: 'Move Console up' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Wishlist changed in another session');
  // The refreshed version lets the retry succeed.
  await userEvent.click(within(screen.getByRole('tabpanel', { name: 'Games' })).getByRole('button', { name: 'Move Console up' }));
  await waitFor(() => expect(requests.at(-1)?.body.version).toBe(3));
  await waitFor(() => expect(within(screen.getByRole('tabpanel', { name: 'Games' })).getAllByRole('listitem')[0]).toHaveTextContent('Console'));
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

describe('bucket tabs', () => {
  const addBucket = (id: string, name: string) => buckets.push({ id, name, available: 0 });
  const tabs = () => screen.getAllByRole('tab');

  it('renders one tab per bucket in order plus a trailing + tab, with ARIA tab semantics', () => {
    addBucket('b2', 'Books'); addBucket('b3', 'Travel');
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    expect(screen.getByRole('tablist', { name: 'Wishlist buckets' })).toBeInTheDocument();
    expect(tabs().map(tab => tab.textContent)).toEqual(['Games', 'Books', 'Travel', '+']);
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'false');
    const panel = screen.getByRole('tabpanel', { name: 'Games' });
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveAttribute('aria-controls', panel.id);
    expect(screen.getAllByRole('tabpanel')).toHaveLength(1);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('selects a tab on click and shows that bucket\'s items', async () => {
    addBucket('b2', 'Books');
    items.push(item('i9', 'b2', 'Novel', 12, 0));
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Books' }));
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    const panel = screen.getByRole('tabpanel', { name: 'Books' });
    expect(within(panel).getByText('Novel')).toBeInTheDocument();
    expect(within(panel).queryByText('Racing game')).not.toBeInTheDocument();
  });

  it('moves between tabs with arrow keys, Home and End, wrapping through the + tab without selecting it', async () => {
    addBucket('b2', 'Books'); addBucket('b3', 'Travel');
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    screen.getByRole('tab', { name: 'Games' }).focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveAttribute('tabindex', '-1');
    await userEvent.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'Add bucket' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.keyboard('{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'Add bucket' })).toHaveFocus();
    await userEvent.keyboard('{ArrowLeft}{Home}');
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveFocus();
  });

  it('creates a bucket from the + tab on Enter and selects the new tab', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    const input = screen.getByRole('textbox', { name: 'Bucket name' });
    expect(input).toHaveFocus();
    await userEvent.type(input, '  Books  {Enter}');
    expect(await screen.findByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
    expect(requests).toEqual([{ path: '/buckets', method: 'POST', body: { version: 3, name: 'Books' } }]);
    expect(screen.getByRole('tabpanel', { name: 'Books' })).toBeInTheDocument();
  });

  it('creates a bucket when the draft input loses focus', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), 'Books');
    await userEvent.click(document.body);
    expect(await screen.findByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    expect(requests).toHaveLength(1);
  });

  it('cancels the draft tab with Escape without calling the API', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), 'Books{Escape}');
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
    expect(tabs().map(tab => tab.textContent)).toEqual(['Games', '+']);
    expect(requests).toHaveLength(0);
  });

  it('cancels the draft tab when the name is empty or blank', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    await userEvent.keyboard('{Enter}');
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), '   ');
    await userEvent.click(document.body);
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
    expect(tabs().map(tab => tab.textContent)).toEqual(['Games', '+']);
    expect(requests).toHaveLength(0);
  });

  it('limits the draft name to the API maximum and rejects longer values inline', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    const input = screen.getByRole('textbox', { name: 'Bucket name' });
    expect(input).toHaveAttribute('maxlength', '60');
    fireEvent.change(input, { target: { value: 'x'.repeat(61) } });
    await userEvent.keyboard('{Enter}');
    expect(await screen.findByRole('alert')).toHaveTextContent('up to 60 characters');
    expect(requests).toHaveLength(0);
    expect(screen.getByRole('textbox', { name: 'Bucket name' })).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows a server error inline, keeps the draft open and lets the user fix it', async () => {
    const fetchMock = vi.mocked(globalThis.fetch);
    const original = fetchMock.getMockImplementation()! as typeof fetch;
    let reject = true;
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
      reject && init?.method === 'POST' ? json({ error: 'A bucket named Books already exists.' }, 400) : original(input, init));
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Add bucket' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), 'Books{Enter}');
    expect(await screen.findByRole('alert')).toHaveTextContent('A bucket named Books already exists.');
    const input = screen.getByRole('textbox', { name: 'Bucket name' });
    expect(input).toHaveValue('Books');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(input).toHaveFocus());
    reject = false;
    await userEvent.clear(input);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await userEvent.type(input, 'Reading{Enter}');
    expect(await screen.findByRole('tab', { name: 'Reading' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renames a tab inline on double-click with Enter, through the rename API', async () => {
    const fetchMock = vi.mocked(globalThis.fetch);
    const original = fetchMock.getMockImplementation()! as typeof fetch;
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input).replace('/api/budgets/budget-1/wishlist', '');
      if (init?.method === 'PUT' && path === '/buckets/b1') {
        const body = JSON.parse(String(init.body)) as { version: number; name: string };
        requests.push({ path, method: 'PUT', body });
        buckets[0].name = body.name; version++;
        return json(overview());
      }
      return original(input, init);
    });
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.dblClick(screen.getByRole('tab', { name: 'Games' }));
    const input = screen.getByRole('textbox', { name: 'Bucket name' });
    expect(input).toHaveFocus();
    expect(input).toHaveValue('Games');
    await userEvent.clear(input);
    await userEvent.type(input, 'Gaming{Enter}');
    expect(await screen.findByRole('tab', { name: 'Gaming' })).toHaveAttribute('aria-selected', 'true');
    expect(requests).toEqual([{ path: '/buckets/b1', method: 'PUT', body: { version: 3, name: 'Gaming' } }]);
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
  });

  it('cancels a rename with Escape, and when the name is unchanged or empty', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.dblClick(screen.getByRole('tab', { name: 'Games' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Bucket name' }), 'XYZ{Escape}');
    expect(screen.getByRole('tab', { name: 'Games' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Bucket name' })).not.toBeInTheDocument();
    await userEvent.dblClick(screen.getByRole('tab', { name: 'Games' }));
    await userEvent.clear(screen.getByRole('textbox', { name: 'Bucket name' }));
    await userEvent.keyboard('{Enter}');
    expect(screen.getByRole('tab', { name: 'Games' })).toBeInTheDocument();
    await userEvent.dblClick(screen.getByRole('tab', { name: 'Games' }));
    await userEvent.keyboard('{Enter}');
    expect(screen.getByRole('tab', { name: 'Games' })).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it('starts a rename from the keyboard with F2', async () => {
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    screen.getByRole('tab', { name: 'Games' }).focus();
    await userEvent.keyboard('{F2}');
    expect(screen.getByRole('textbox', { name: 'Bucket name' })).toHaveValue('Games');
  });

  it('keeps bucket rename, delete and reorder reachable from the panel', async () => {
    addBucket('b2', 'Books');
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    const panel = screen.getByRole('tabpanel', { name: 'Games' });
    expect(within(panel).getByRole('button', { name: 'Move Games bucket left' })).toBeDisabled();
    await userEvent.click(within(panel).getByRole('button', { name: 'Move Games bucket right' }));
    expect(requests.at(-1)).toEqual({ path: '/buckets/order', method: 'PUT', body: { version: 3, bucketIds: ['b2', 'b1'] } });
    await userEvent.click(within(panel).getByRole('button', { name: 'Rename Games bucket' }));
    expect(screen.getByRole('textbox', { name: 'Bucket name' })).toHaveValue('Games');
    await userEvent.keyboard('{Escape}');
    await userEvent.click(within(panel).getByRole('button', { name: 'Delete Games bucket' }));
    expect(screen.getByRole('dialog', { name: 'Delete Games?' })).toBeInTheDocument();
  });

  it('remembers the selected tab per budget across remounts', async () => {
    addBucket('b2', 'Books');
    const view = render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Books' }));
    expect(window.localStorage.getItem('openbudget.wishlist.bucket.budget-1')).toBe('b2');
    view.unmount();
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel', { name: 'Books' })).toBeInTheDocument();
  });

  it('falls back to the first bucket when the remembered bucket no longer exists', () => {
    window.localStorage.setItem('openbudget.wishlist.bucket.budget-1', 'gone');
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    expect(screen.getByRole('tab', { name: 'Games' })).toHaveAttribute('aria-selected', 'true');
  });

  it('still works when browser storage is unavailable', async () => {
    addBucket('b2', 'Books');
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Books' }));
    expect(screen.getByRole('tab', { name: 'Books' })).toHaveAttribute('aria-selected', 'true');
  });

  it('moves an item to another bucket by dropping it on that tab', async () => {
    addBucket('b2', 'Books');
    const fetchMock = vi.mocked(globalThis.fetch);
    render(<Harness initial={{ status: 'ready', overview: overview() }} />);
    const [racing] = within(screen.getByRole('tabpanel', { name: 'Games' })).getAllByRole('listitem');
    fireEvent.dragStart(racing, { dataTransfer: { effectAllowed: '', setData: () => undefined } });
    fireEvent.dragOver(screen.getByRole('tab', { name: 'Books' }), { dataTransfer: {} });
    fireEvent.drop(screen.getByRole('tab', { name: 'Books' }), { dataTransfer: {} });
    await waitFor(() => expect(fetchMock.mock.calls.some(([path, init]) => String(path).endsWith('/items/i1') && init?.method === 'PUT')).toBe(true));
    expect(JSON.parse(String(fetchMock.mock.calls.find(([path]) => String(path).endsWith('/items/i1'))![1]!.body)).bucketId).toBe('b2');
  });
});
