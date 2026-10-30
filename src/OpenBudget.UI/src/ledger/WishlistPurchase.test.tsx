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

// Synthetic data: Games (two open items, listed out of desire order), an empty bucket, and Home (one open item).
const checking: Account = { id: 'checking', name: 'Checking', isClosed: false, balance: 500, clearedBalance: 500, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0 };
const item = (id: string, bucketId: string, name: string, cost: number, sortOrder: number): WishlistItem =>
  ({ id, bucketId, name, url: null, cost, description: '', sortOrder, status: 'active', createdAt: '2026-07-21T12:00:00Z', purchasedAt: null, removedAt: null });
const bucket = (id: string, name: string, sortOrder: number, items: WishlistItem[]) =>
  ({ id, name, sortOrder, categoryId: `wl-${id}`, available: 100, minCost: null, maxCost: null, progress: null, items, purchased: [], removed: [] });
const overview: WishlistOverview = { isEnabled: true, version: 4, month: '2026-10', readyCount: 0, buckets: [
  bucket('games', 'Games', 0, [item('console', 'games', 'Console', 450, 1), item('racing', 'games', 'Racing game', 59.99, 0)]),
  bucket('empty', 'Empty bucket', 1, []),
  bucket('home', 'Home', 2, [item('lamp', 'home', 'Lamp', 40, 0)]),
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
    if (path.startsWith('/api/budget/')) return json({ groups: [{ id: 'daily', name: 'Daily', categories: [{ id: 'food', name: 'Food' }] }, { id: 'wishlist', name: 'Wishlist', categories: [{ id: 'wl-games', name: 'Games' }, { id: 'wl-home', name: 'Home' }] }] });
    if (path === '/api/payees') return json(['Shop']);
    if (path === '/api/payees/manage') return json({ version: 3, payees: [] });
    throw new Error(`Unexpected request ${path}`);
  }));
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

const user = () => userEvent.setup();
const linkButton = () => screen.queryByRole('button', { name: /^Link wishlist item/ });
const entry: Entry = { id: 't1', accountId: 'checking', date: '2026-10-20', amount: -5, payee: 'Shop', memo: '', categoryId: 'food', status: 'uncleared', transferId: null, isOpening: false, splits: [], needsAttention: false, runningBalance: 0, transferAccountName: null, categoryName: 'Food', isLocked: false };

it('shows the Link wishlist item button only when Wishlist is available and a transaction is being added, without the old mode switch', async () => {
  const plain = render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} />);
  await screen.findByRole('combobox', { name: 'Category' });
  expect(linkButton()).not.toBeInTheDocument();
  plain.unmount();
  const edit = render(<TransactionForm accounts={[checking]} entry={entry} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  expect(linkButton()).not.toBeInTheDocument();
  edit.unmount();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  expect(linkButton()).toBeInTheDocument();
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
  expect(screen.queryByRole('radio', { name: 'Wishlist' })).not.toBeInTheDocument();
  // Splits are regular transactions only.
  await user().click(screen.getByRole('button', { name: '+ Add split' }));
  expect(linkButton()).not.toBeInTheDocument();
});

it('shows the button beside Memo in the register add row only when Wishlist is available', async () => {
  const u = user();
  const hidden = render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={null} />);
  await screen.findByText('No transactions match these filters.');
  await u.click(screen.getByRole('button', { name: 'Add Transaction' }));
  expect(screen.getByRole('textbox', { name: 'Memo' })).toBeInTheDocument();
  expect(linkButton()).not.toBeInTheDocument();
  hidden.unmount();
  render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={wishlist} />);
  await screen.findByText('No transactions match these filters.');
  await u.click(screen.getByRole('button', { name: 'Add Transaction' }));
  const memo = screen.getByRole('textbox', { name: 'Memo' });
  expect(memo.closest('td')).toContainElement(linkButton());
  expect(screen.queryByRole('radiogroup', { name: 'Transaction type' })).not.toBeInTheDocument();
});

it('lists open items grouped by bucket in desire order and is keyboard operable', async () => {
  const u = user();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  const button = linkButton()!;
  expect(button).toHaveAttribute('aria-haspopup', 'listbox');
  expect(button).toHaveAttribute('aria-expanded', 'false');
  button.focus();
  await u.keyboard('{Enter}');
  const list = screen.getByRole('listbox', { name: 'Wishlist items' });
  expect(button).toHaveAttribute('aria-expanded', 'true');
  expect(list).toHaveFocus();
  const groups = within(list).getAllByRole('group');
  expect(groups.map(group => group.getAttribute('aria-label'))).toEqual(['Games', 'Home']);
  expect(within(groups[0]).getAllByRole('option').map(option => option.getAttribute('aria-label'))).toEqual(['Racing game (€59.99)', 'Console (€450.00)']);
  expect(within(groups[1]).getByRole('option', { name: 'Lamp (€40.00)' })).toBeInTheDocument();
  // Arrow keys move the active option (wrapping); Escape closes and returns focus to the button.
  const activeName = () => document.getElementById(list.getAttribute('aria-activedescendant')!)?.getAttribute('aria-label');
  expect(activeName()).toBe('Racing game (€59.99)');
  await u.keyboard('{ArrowDown}{ArrowDown}');
  expect(activeName()).toBe('Lamp (€40.00)');
  await u.keyboard('{ArrowDown}');
  expect(activeName()).toBe('Racing game (€59.99)');
  await u.keyboard('{ArrowUp}');
  expect(activeName()).toBe('Lamp (€40.00)');
  await u.keyboard('{Escape}');
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  expect(button).toHaveFocus();
  // An outside click closes the popup.
  await u.click(button);
  expect(screen.getByRole('listbox', { name: 'Wishlist items' })).toBeInTheDocument();
  await u.click(document.body);
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  // ArrowDown on the button opens it; Enter picks the active option and focus moves to the next field.
  button.focus();
  await u.keyboard('{ArrowDown}{ArrowDown}{Enter}');
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  expect(screen.getByLabelText('Memo')).toHaveValue('Console');
  await waitFor(() => expect(screen.getByLabelText('Status')).toHaveFocus());
});

it('selecting an item sets memo and category but never the payee, shows the link, and submits the item id', async () => {
  const submit = vi.fn(async () => {}); const u = user();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={submit} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  await u.type(screen.getByLabelText('Payee'), 'Big Box Store');
  await u.type(screen.getByLabelText('Amount'), '-429.50');
  await u.click(linkButton()!);
  await u.click(screen.getByRole('option', { name: 'Console (€450.00)' }));
  expect(screen.getByLabelText('Payee')).toHaveValue('Big Box Store');
  expect(screen.getByLabelText('Memo')).toHaveValue('Console');
  expect(screen.getByRole('combobox', { name: 'Category' })).toHaveValue('Wishlist: Games');
  // The amount is left as entered.
  expect(screen.getByLabelText('Amount')).toHaveValue('-429.50');
  const button = screen.getByRole('button', { name: 'Link wishlist item (linked: Console)' });
  expect(button).toHaveClass('is-linked');
  expect(button).toHaveAttribute('title', 'Linked wishlist item: Console');
  expect(screen.getByRole('status')).toHaveTextContent('Linked to Wishlist item Console');
  // The linked item is marked selected when the popup is reopened.
  await u.click(button);
  expect(screen.getByRole('option', { name: 'Console (€450.00)' })).toHaveAttribute('aria-selected', 'true');
  await u.keyboard('{Escape}');

  // The user may edit the memo afterwards; the link stays.
  await u.type(screen.getByLabelText('Memo'), ' (refurbished)');
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenCalledWith(expect.objectContaining({ amount: -429.5, payee: 'Big Box Store', memo: 'Console (refurbished)', categoryId: 'wl-games', splits: [], wishlistItemId: 'console', accountId: 'checking' }));

  // An inflow cannot be a Wishlist purchase.
  submit.mockClear();
  await u.clear(screen.getByLabelText('Amount')); await u.type(screen.getByLabelText('Amount'), '40');
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(screen.getByRole('alert')).toHaveTextContent('enter a negative amount');
  expect(submit).not.toHaveBeenCalled();
});

it('unlinks from the popup or when another category is chosen, and then saves a regular transaction', async () => {
  const submit = vi.fn(async () => {}); const u = user();
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={submit} wishlist={wishlist} />);
  await screen.findByRole('combobox', { name: 'Category' });
  await u.type(screen.getByLabelText('Amount'), '-40');
  await u.click(linkButton()!);
  await u.click(screen.getByRole('option', { name: 'Lamp (€40.00)' }));
  expect(screen.getByRole('button', { name: 'Link wishlist item (linked: Lamp)' })).toBeInTheDocument();

  await u.click(screen.getByRole('button', { name: 'Link wishlist item (linked: Lamp)' }));
  await u.click(screen.getByRole('option', { name: 'Unlink Lamp' }));
  expect(screen.getByRole('button', { name: 'Link wishlist item' })).not.toHaveClass('is-linked');
  expect(screen.queryByRole('option', { name: 'Unlink Lamp' })).not.toBeInTheDocument();
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenLastCalledWith(expect.not.objectContaining({ wishlistItemId: expect.anything() }));
  expect(submit).toHaveBeenLastCalledWith(expect.objectContaining({ memo: 'Lamp', categoryId: 'wl-home' }));

  // Re-link, then choose a non-Wishlist category: the link is dropped.
  await u.click(screen.getByRole('button', { name: 'Link wishlist item' }));
  await u.click(screen.getByRole('option', { name: 'Lamp (€40.00)' }));
  await u.click(screen.getByRole('combobox', { name: 'Category' }));
  await u.click(screen.getByRole('option', { name: /^Food/ }));
  expect(screen.getByRole('button', { name: 'Link wishlist item' })).not.toHaveClass('is-linked');
  await u.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenLastCalledWith(expect.objectContaining({ categoryId: 'food' }));
  expect(submit).toHaveBeenLastCalledWith(expect.not.objectContaining({ wishlistItemId: expect.anything() }));
});

it('opens with the requested item linked for Record purchase', async () => {
  render(<TransactionForm accounts={[checking]} selectedAccount="checking" busy={false} onSubmit={vi.fn()} wishlist={wishlist} initialWishlistItemId="console" />);
  expect(await screen.findByRole('button', { name: 'Link wishlist item (linked: Console)' })).toHaveClass('is-linked');
  expect(screen.getByLabelText('Memo')).toHaveValue('Console');
  expect(screen.getByLabelText('Payee')).toHaveValue('');
  expect(screen.getByLabelText('Amount')).toHaveValue('-450.00');
});

it('links an item from the register add row and saves through the purchase-transaction endpoint with the user payee', async () => {
  const u = user();
  render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={wishlist} />);
  await screen.findByText('No transactions match these filters.');
  await u.click(screen.getByRole('button', { name: 'Add Transaction' }));
  await u.keyboard('{Escape}');
  await u.type(screen.getByRole('combobox', { name: 'Payee' }), 'Game Shop');
  await u.click(linkButton()!);
  await u.click(screen.getByRole('option', { name: 'Racing game (€59.99)' }));
  expect(screen.getByRole('combobox', { name: 'Payee' })).toHaveValue('Game Shop');
  expect(screen.getByRole('textbox', { name: 'Memo' })).toHaveValue('Racing game');
  expect(screen.getByRole('combobox', { name: 'Category' })).toHaveValue('Wishlist: Games');
  // Focus moves on from the button to the next field in the row.
  await waitFor(() => expect(screen.getByRole('textbox', { name: 'Outflow' })).toHaveFocus());
  await u.type(screen.getByRole('textbox', { name: 'Outflow' }), '55');
  await u.click(screen.getByRole('button', { name: /^Save$/ }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/budgets/budget-1/wishlist/items/racing/purchase-transaction');
  expect(mutations[0].body).toMatchObject({ accountId: 'checking', amount: -55, payee: 'Game Shop', memo: 'Racing game', categoryId: 'wl-games', status: 'uncleared', splits: [], version: 3 });
  expect(mutations[0].body.commandId).toEqual(expect.any(String));
  expect(mutations[0].body).not.toHaveProperty('wishlistItemId');
});

it('opens the add-transaction dialog with the item linked for a Record purchase request and saves through the purchase endpoint', async () => {
  const u = user();
  render(<AccountsPage selectedAccount="" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} wishlist={wishlist} wishlistPurchaseRequest={{ itemId: 'lamp', token: 1 }} />);
  const dialog = await screen.findByRole('dialog', { name: 'Add transaction' });
  const button = within(dialog).getByRole('button', { name: 'Link wishlist item (linked: Lamp)' });
  // Escape inside the popup closes only the popup, not the dialog.
  await u.click(button);
  expect(screen.getByRole('option', { name: 'Unlink Lamp' })).toBeInTheDocument();
  await u.keyboard('{Escape}');
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  expect(screen.getByRole('dialog', { name: 'Add transaction' })).toBeInTheDocument();
  expect(button).toHaveFocus();
  await u.type(within(dialog).getByLabelText('Payee'), 'Lighting Co');
  await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Save transaction' })).toBeEnabled());
  await u.click(within(dialog).getByRole('button', { name: 'Save transaction' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/budgets/budget-1/wishlist/items/lamp/purchase-transaction');
  expect(mutations[0].body).toMatchObject({ accountId: 'checking', amount: -40, categoryId: 'wl-home', payee: 'Lighting Co', memo: 'Lamp' });
  expect(mutations[0].body).not.toHaveProperty('wishlistItemId');
});
