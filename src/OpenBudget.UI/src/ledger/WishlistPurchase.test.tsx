import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { WishlistItem, WishlistOverview } from '../wishlist/wishlistApi';
import { AccountsPage } from './AccountsPage';
import { type Account, type Entry, type Register } from './api';
import { defaultFlags } from './Flags';
import { TransactionForm } from './TransactionForm';
import { defaultView } from './ViewOptions';
import type { WishlistContext } from './WishlistPurchase';

// Synthetic data: two funded buckets (one empty) in budget-1.
const checking: Account = { id: 'checking', name: 'Checking', isClosed: false, balance: 500, clearedBalance: 500, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0 };
const item = (id: string, bucketId: string, name: string, cost: number, sortOrder: number): WishlistItem =>
  ({ id, bucketId, name, url: null, cost, description: '', sortOrder, status: 'active', createdAt: '2026-07-21T12:00:00Z', purchasedAt: null, removedAt: null });
const bucket = (id: string, name: string, items: WishlistItem[]) =>
  ({ id, name, sortOrder: 0, categoryId: `wl-${id}`, available: 100, minCost: null, maxCost: null, progress: null, items, purchased: [], removed: [] });
const overview: WishlistOverview = { isEnabled: true, version: 4, month: '2026-10', readyCount: 0, buckets: [
  bucket('games', 'Games', [item('racing', 'games', 'Racing game', 59.99, 0), item('console', 'games', 'Console', 450, 1)]),
  bucket('empty', 'Empty bucket', []),
  bucket('home', 'Home', [item('lamp', 'home', 'Lamp', 40, 0)]),
] };
const wishlist: WishlistContext = { budgetId: 'budget-1', overview };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
let mutations: { path: string; body: Record<string, unknown> }[];

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-10-21T12:00:00Z'));
  mutations = [];
  const register: Register = { version: 3, page: 1, pageSize: 25, total: 0, accounts: { version: 3, accounts: [checking] }, items: [] };
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (init?.method && init.method !== 'GET' && !path.startsWith('/api/register-views/')) { mutations.push({ path, body: JSON.parse(String(init.body)) }); return json({ id: 'saved', version: 4 }); }
    if (path.startsWith('/api/register-views/')) return json(defaultView);
    if (path.startsWith('/api/schedules')) return json({ version: 3, page: 1, pageSize: 25, total: 0, items: [] });
    if (path === '/api/flags') return json({ version: 3, flags: defaultFlags });
    if (path.startsWith('/api/transactions')) return json(register);
    if (path.startsWith('/api/budget/')) return json({ groups: [{ id: 'daily', categories: [{ id: 'food', name: 'Food' }] }, { id: 'wishlist', name: 'Wishlist', categories: [{ id: 'wl-games', name: 'Games' }, { id: 'wl-home', name: 'Home' }] }] });
    if (path === '/api/payees') return json(['Shop']);
    if (path === '/api/payees/manage') return json({ version: 3, payees: [] });
    throw new Error(`Unexpected request ${path}`);
  }));
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

const user = () => userEvent.setup();

it('shows the Regular/Wishlist switch only when Wishlist is available and a transaction is being added', async () => {
  const { unmount } = render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} />);
  await screen.findByRole('combobox', { name: 'Category' });
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
  unmount();
  const entry: Entry = { id: 't1', accountId: 'checking', date: '2026-10-20', amount: -5, payee: 'Shop', memo: '', categoryId: 'food', status: 'uncleared', transferId: null, isOpening: false, splits: [], needsAttention: false, runningBalance: 0, transferAccountName: null, categoryName: 'Food', isLocked: false };
  const edit = render(<TransactionForm accounts={[checking]} entry={entry} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
  edit.unmount();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} />);
  const toggle = await screen.findByRole('radiogroup', { name: 'Transaction type' });
  expect(within(toggle).getByRole('radio', { name: 'Regular' })).toHaveAttribute('aria-checked', 'true');
  // Adding a split hides the switch (splits are regular transactions only).
  await user().click(screen.getByRole('button', { name: '+ Add split' }));
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
});

it('autofills the dialog form from a bucket, lets the user pick another item and override the amount, and submits the item id', async () => {
  const submit = vi.fn(async () => {}); const u = user();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={submit} wishlist={wishlist} />);
  const wishlistRadio = await screen.findByRole('radio', { name: 'Wishlist' });
  // Keyboard: arrow keys move between the two modes.
  screen.getByRole('radio', { name: 'Regular' }).focus();
  await u.keyboard('{ArrowRight}');
  expect(wishlistRadio).toHaveAttribute('aria-checked', 'true');
  expect(wishlistRadio).toHaveFocus();

  // The first bucket with items is chosen and its next item autofills the form.
  const bucketSelect = screen.getByLabelText('Wishlist bucket');
  expect(bucketSelect).toHaveValue('games');
  expect(within(bucketSelect).getByRole('option', { name: 'Empty bucket — no items' })).toBeDisabled();
  expect(within(bucketSelect).getByRole('option', { name: /Games — Racing game/ })).toBeEnabled();
  expect(screen.getByLabelText('Payee')).toHaveValue('Racing game');
  expect(screen.getByLabelText('Memo')).toHaveValue('Wishlist: Racing game');
  expect(screen.getByLabelText('Amount')).toHaveValue('-59.99');
  expect(screen.getByLabelText('Category (set by Wishlist bucket)')).toHaveTextContent('Wishlist: Games');
  expect(screen.queryByRole('combobox', { name: 'Category' })).not.toBeInTheDocument();
  expect(screen.getByRole('status')).toHaveTextContent('Buying: Racing game · €59.99 · added 3 months ago');

  // Another active item in the same bucket, in desire order.
  await u.selectOptions(screen.getByLabelText('Item'), 'console');
  expect(screen.getByLabelText('Payee')).toHaveValue('Console');
  expect(screen.getByLabelText('Amount')).toHaveValue('-450.00');
  // The actual price differs.
  await u.clear(screen.getByLabelText('Amount')); await u.type(screen.getByLabelText('Amount'), '-429.50');
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenCalledWith(expect.objectContaining({ amount: -429.5, payee: 'Console', memo: 'Wishlist: Console', categoryId: 'wl-games', splits: [], wishlistItemId: 'console', accountId: 'checking' }));

  // Switching bucket refills everything; an inflow is refused.
  submit.mockClear();
  await u.selectOptions(screen.getByLabelText('Wishlist bucket'), 'home');
  expect(screen.getByLabelText('Payee')).toHaveValue('Lamp');
  expect(screen.getByLabelText('Category (set by Wishlist bucket)')).toHaveTextContent('Wishlist: Home');
  await u.clear(screen.getByLabelText('Amount')); await u.type(screen.getByLabelText('Amount'), '40');
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(screen.getByRole('alert')).toHaveTextContent('enter a negative amount');
  expect(submit).not.toHaveBeenCalled();
});

it('opens prefilled in Wishlist mode for a requested item', async () => {
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} initialWishlistItemId="console" />);
  expect(await screen.findByRole('radio', { name: 'Wishlist' })).toHaveAttribute('aria-checked', 'true');
  expect(screen.getByLabelText('Item')).toHaveValue('console');
  expect(screen.getByLabelText('Payee')).toHaveValue('Console');
  expect(screen.getByLabelText('Amount')).toHaveValue('-450.00');
});

it('records a Wishlist purchase from the register add row through the purchase-transaction endpoint', async () => {
  const u = user();
  render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={wishlist} />);
  await screen.findByText('No transactions match these filters.');
  await u.click(screen.getByRole('button', { name: 'Add Transaction' }));
  await u.keyboard('{Escape}');
  await u.click(screen.getByRole('radio', { name: 'Wishlist' }));
  expect(screen.getByRole('textbox', { name: 'Payee' })).toHaveValue('Racing game');
  expect(screen.getByRole('textbox', { name: 'Outflow' })).toHaveValue('59.99');
  expect(screen.getByRole('textbox', { name: 'Memo' })).toHaveValue('Wishlist: Racing game');
  expect(screen.getByRole('textbox', { name: 'Inflow' })).toBeDisabled();
  await u.clear(screen.getByRole('textbox', { name: 'Outflow' })); await u.type(screen.getByRole('textbox', { name: 'Outflow' }), '55');
  await u.click(screen.getByRole('button', { name: /^Save$/ }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/budgets/budget-1/wishlist/items/racing/purchase-transaction');
  expect(mutations[0].body).toMatchObject({ accountId: 'checking', amount: -55, payee: 'Racing game', memo: 'Wishlist: Racing game', categoryId: 'wl-games', status: 'uncleared', splits: [], version: 3 });
  expect(mutations[0].body.commandId).toEqual(expect.any(String));
  expect(mutations[0].body).not.toHaveProperty('wishlistItemId');
});

it('hides the switch in the register add row when Wishlist is not available', async () => {
  const u = user();
  render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={null} />);
  await screen.findByText('No transactions match these filters.');
  await u.click(screen.getByRole('button', { name: 'Add Transaction' }));
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
});

it('opens the add-transaction dialog prefilled for a Record purchase request and saves through the purchase endpoint', async () => {
  const u = user();
  render(<AccountsPage selectedAccount="" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={wishlist} wishlistPurchaseRequest={{ itemId: 'lamp', token: 1 }} />);
  const dialog = await screen.findByRole('dialog', { name: 'Add transaction' });
  expect(within(dialog).getByRole('radio', { name: 'Wishlist' })).toHaveAttribute('aria-checked', 'true');
  expect(within(dialog).getByLabelText('Wishlist bucket')).toHaveValue('home');
  expect(within(dialog).getByLabelText('Payee')).toHaveValue('Lamp');
  await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Save transaction' })).toBeEnabled());
  await u.click(within(dialog).getByRole('button', { name: 'Save transaction' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/budgets/budget-1/wishlist/items/lamp/purchase-transaction');
  expect(mutations[0].body).toMatchObject({ accountId: 'checking', amount: -40, categoryId: 'wl-home', payee: 'Lamp' });
  expect(mutations[0].body).not.toHaveProperty('wishlistItemId');
});
