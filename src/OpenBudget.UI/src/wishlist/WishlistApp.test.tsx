import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../App';

// App-level Wishlist wiring: sidebar placement/visibility by entitlement, ready badge, Plan row status and guards.
type Access = 'forbidden' | 'disabled' | 'ready' | 'saving';
let access: Access;
let wlAvailable = 60;
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const progress = { nextItemId: 'i1', nextItemName: 'Racing game', cost: 59.99, available: 60, remaining: 0, canPurchase: true, fundedFraction: 1 };
const wishlistOverview = () => ({
  isEnabled: access !== 'disabled', version: 2, month: '2026-10', readyCount: access === 'ready' ? 1 : 0,
  buckets: access === 'disabled' ? [] : [{ id: 'b1', name: 'Games', sortOrder: 0, categoryId: 'wl-b1', available: 60, minCost: 59.99, maxCost: 59.99,
    progress: access === 'ready' ? progress : { ...progress, available: 20, remaining: 39.99, canPurchase: false, fundedFraction: 0.33 },
    items: [{ id: 'i1', bucketId: 'b1', name: 'Racing game', url: null, cost: 59.99, description: '', sortOrder: 0, status: 'active', createdAt: '2026-10-01T00:00:00Z', purchasedAt: null, removedAt: null }], purchased: [], removed: [] }],
});
const month = (value: string) => ({
  month: value, note: '', version: 1, fundsAdded: 0, openingReadyToAssign: 500, ledgerIncome: 0, readyToAssign: 440,
  groups: [
    { id: 'home', name: 'Home', sortOrder: 0, isArchived: false, categories: [{ id: 'utilities', name: 'Electricity', assigned: 0, activity: 0, available: 0, target: 0, icon: '', snoozed: false, sortOrder: 0, isArchived: false }] },
    { id: 'wishlist', name: 'Wishlist', sortOrder: 1, isArchived: false, categories: [{ id: 'wl-b1', name: 'Games', assigned: wlAvailable, activity: 0, available: wlAvailable, target: 0, icon: '🎁', snoozed: false, sortOrder: 0, isArchived: false, wishlistProgress: access === 'ready' ? progress : access === 'saving' ? { ...progress, available: wlAvailable, remaining: Math.max(59.99 - wlAvailable, 0), canPurchase: false, fundedFraction: wlAvailable / 59.99 } : null }] },
  ],
});

beforeEach(() => {
  window.history.replaceState(null, '', '/');
  window.localStorage.clear();
  access = 'ready';
  wlAvailable = 60;
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (init?.method === 'PUT' && path.startsWith('/api/budgets/budget-1/months/')) {
      // Echo the saved month; the server's wishlistProgress stays "last load" (stale) unless the page reloads.
      const body = JSON.parse(String(init.body));
      wlAvailable = body.groups.flatMap((g: { categories: { id: string; assigned: number }[] }) => g.categories).find((c: { id: string }) => c.id === 'wl-b1').assigned;
      const saved = month(path.slice(-7)); saved.version = body.version + 1;
      return json(saved);
    }
    if (path === '/api/auth/session') return json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: false });
    if (path === '/api/accounts') return json({ version: 1, accounts: [] });
    if (path === '/api/budgets?includeArchived=true') return json({ budgets: [{ id: 'budget-1', name: 'Home budget', currency: 'EUR', version: 1, isArchived: false, createdAt: '2026-01-01T00:00:00Z' }], limit: null });
    if (path === '/api/budgets/budget-1/wishlist') return access === 'forbidden' ? new Response(null, { status: 403 }) : json(wishlistOverview());
    if (path.startsWith('/api/budgets/budget-1/months/')) return json(month(path.slice(-7)));
    if (path.startsWith('/api/schedules?')) return json({ version: 1, page: 1, pageSize: 100, total: 0, items: [] });
    if (path.startsWith('/api/long-term-planning/')) return json({ year: 2026, version: 1, hasPlanningData: false, months: [], groups: [] });
    return json({}, 404);
  }));
});
afterEach(() => vi.unstubAllGlobals());

const sideNav = () => within(document.querySelector('.side-nav') as HTMLElement);
function navLabels() {
  return Array.from(document.querySelectorAll('.side-nav .nav-item')).map(item => item.getAttribute('title'));
}

it('hides the Wishlist nav item when the membership is not entitled', async () => {
  access = 'forbidden';
  render(<App />);
  await screen.findByText('Plan saved');
  await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/budget-1/wishlist', expect.anything()));
  expect(sideNav().queryByRole('button', { name: /^Wishlist/ })).not.toBeInTheDocument();
  expect(navLabels()).toContain('Long-Term');
});

it('places Wishlist directly below Long-Term Planning with a ready-to-buy badge and Plan row status', async () => {
  render(<App />);
  await screen.findByText('Plan saved');
  const nav = await sideNav().findByRole('button', { name: 'Wishlist, 1 ready to buy' });
  expect(nav).toHaveTextContent('1');
  const labels = navLabels();
  expect(labels.indexOf('Wishlist, 1 ready to buy')).toBe(labels.indexOf('Long-Term') + 1);

  const row = screen.getByRole('button', { name: 'Edit assigned for Games' }).closest('.category-row') as HTMLElement;
  expect(within(row).getByText(/You can buy/)).toHaveTextContent('You can buy Racing game');
  expect(row.querySelector('.wishlist-row-check')).toBeInTheDocument();
  expect(within(row).getByRole('button', { name: 'Archive category Games' })).toBeDisabled();
  expect(within(row).getByRole('button', { name: 'Move Games up' })).toBeDisabled();
  expect(screen.queryByRole('button', { name: 'Add category to Wishlist' })).not.toBeInTheDocument();

  await userEvent.click(nav);
  const bucket = await screen.findByRole('region', { name: 'Games bucket' });
  expect(within(bucket).getByText('Ready to buy')).toBeInTheDocument();
});

const checking = { id: 'checking', name: 'Checking', isClosed: false, balance: 500, clearedBalance: 500, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0 };
const purchaseEntry = { id: 'tx-racing', accountId: 'checking', date: '2026-10-19', amount: -54.5, payee: 'Game Shop', memo: 'Racing game', categoryId: 'wl-b1', status: 'uncleared', transferId: null, isOpening: false, splits: [], needsAttention: false, runningBalance: 445.5, transferAccountName: null, categoryName: 'Games', isLocked: false };
// Adds account/register responses on top of the base mock; registerRequests records each register query string.
function withAccounts(registerRequests: string[] = []) {
  const base = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path === '/api/accounts') return json({ version: 1, accounts: [checking] });
    if (path.startsWith('/api/register-views/')) return json({ version: 1, datePreset: 'all', fromMonth: null, toMonth: null, showReconciled: true, showRunningBalance: true, showFlags: true, showImages: false, showScheduledTransactions: true });
    if (path.startsWith('/api/transactions?')) { registerRequests.push(path); return json({ version: 1, page: 1, pageSize: 25, total: 1, items: [purchaseEntry], accounts: { version: 1, accounts: [checking] } }); }
    if (path === '/api/flags') return json({ version: 1, flags: [] });
    if (path === '/api/payees') return json([]);
    if (path === '/api/payees/manage') return json({ version: 1, payees: [] });
    if (path.startsWith('/api/budget/')) return json(month(path.slice(-7)));
    return base(input, init);
  });
}

it('Record purchase opens Accounts with the add-transaction dialog and the item linked', async () => {
  withAccounts();
  render(<App />);
  await screen.findByText('Plan saved');
  await userEvent.click(await sideNav().findByRole('button', { name: 'Wishlist, 1 ready to buy' }));
  await userEvent.click(await screen.findByRole('button', { name: 'Mark Racing game purchased' }));
  await userEvent.click(within(screen.getByRole('dialog', { name: 'Mark as purchased?' })).getByRole('button', { name: 'Record purchase' }));
  const dialog = await screen.findByRole('dialog', { name: 'Add transaction' });
  expect(within(dialog).getByRole('button', { name: 'Link wishlist item (linked: Racing game)' })).toHaveClass('is-linked');
  expect(within(dialog).getByLabelText('Memo')).toHaveValue('Racing game');
  expect(within(dialog).getByLabelText('Payee')).toHaveValue('');
  expect(within(dialog).getByLabelText('Amount')).toHaveValue('-59.99');
});

it.each([['forbidden', 'not entitled'], ['disabled', 'turned off for the budget']] as const)('hides the Link wishlist item button when Wishlist is %s (%s)', async (state, _reason) => {
  access = state;
  withAccounts();
  render(<App />);
  await screen.findByText('Plan saved');
  await userEvent.click(sideNav().getByRole('button', { name: /^All Accounts/ }));
  await userEvent.click(await screen.findByRole('button', { name: 'Add Transaction' }));
  expect(await screen.findByRole('textbox', { name: 'Memo' })).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /^Link wishlist item/ })).not.toBeInTheDocument();
});

it('shows the Link wishlist item button in the register when Wishlist is entitled and enabled', async () => {
  withAccounts();
  render(<App />);
  await screen.findByText('Plan saved');
  await sideNav().findByRole('button', { name: 'Wishlist, 1 ready to buy' });
  await userEvent.click(sideNav().getByRole('button', { name: /^All Accounts/ }));
  await userEvent.click(await screen.findByRole('button', { name: 'Add Transaction' }));
  expect(await screen.findByRole('button', { name: 'Link wishlist item' })).toBeInTheDocument();
});

it('View transaction in the Purchased history opens the account register at that transaction and highlights it', async () => {
  const purchasedOverview = wishlistOverview();
  purchasedOverview.buckets[0].purchased = [{ id: 'i0', bucketId: 'b1', name: 'Old console', url: null, cost: 60, description: '', sortOrder: 0, status: 'purchased', createdAt: '2026-09-01T00:00:00Z', purchasedAt: '2026-10-19T12:00:00Z', removedAt: null,
    purchase: { transactionId: 'tx-racing', accountId: 'checking', date: '2026-10-19', amount: 54.5 } }] as never;
  const registerRequests: string[] = [];
  withAccounts(registerRequests);
  const withHistory = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
    String(input) === '/api/budgets/budget-1/wishlist' ? json(purchasedOverview) : withHistory(input, init));
  render(<App />);
  await screen.findByText('Plan saved');
  await userEvent.click(await sideNav().findByRole('button', { name: 'Wishlist, 1 ready to buy' }));
  await userEvent.click(await screen.findByText('Purchased (1)'));
  await userEvent.click(screen.getByRole('button', { name: 'View transaction for Old console' }));

  await waitFor(() => expect(window.location.pathname).toBe('/home-budget/account/checking'));
  expect(await screen.findByText(/Showing the transaction selected from the Wishlist screen/)).toBeInTheDocument();
  await waitFor(() => expect(document.querySelector('[data-transaction-id="tx-racing"]')).toHaveAttribute('aria-selected', 'true'));
  // The register is narrowed to the transaction's date so the row is on the first page.
  expect(registerRequests.some(path => path.includes('accountId=checking') && path.includes('from=2026-10-19') && path.includes('to=2026-10-19'))).toBe(true);
});

it('shows the enable call-to-action when entitled but disabled', async () => {
  access = 'disabled';
  render(<App />);
  await screen.findByText('Plan saved');
  const nav = await sideNav().findByRole('button', { name: 'Wishlist' });
  expect(nav).toHaveTextContent('Off');
  await userEvent.click(nav);
  expect(await screen.findByRole('button', { name: 'Enable Wishlist' })).toBeInTheDocument();
});

const wishlistRow = () => screen.getByRole('button', { name: 'Edit assigned for Games' }).closest('.category-row') as HTMLElement;
async function assignGames(value: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Edit assigned for Games' }));
  const input = screen.getByRole('textbox', { name: 'Assigned to Games' });
  await userEvent.clear(input);
  await userEvent.type(input, value);
  await userEvent.keyboard('{Enter}');
}

it('shows the remaining amount when the bucket cannot yet buy the next item', async () => {
  access = 'saving'; wlAvailable = 25;
  render(<App />);
  await screen.findByText('Plan saved');
  expect(await within(wishlistRow()).findByText(/to go for/)).toHaveTextContent('€34.99 to go for Racing game');
  expect(wishlistRow().querySelector('.wishlist-row-check')).not.toBeInTheDocument();
});

it('switches the Plan row to "You can buy" immediately when assigned reaches the cost', async () => {
  access = 'saving'; wlAvailable = 25;
  render(<App />);
  await screen.findByText('Plan saved');
  expect(await within(wishlistRow()).findByText(/to go for/)).toBeInTheDocument();
  await assignGames('59.98');
  await waitFor(() => expect(wishlistRow()).toHaveTextContent('€0.01 to go for Racing game'));
  await assignGames('59.99');
  expect(await within(wishlistRow()).findByText(/You can buy/)).toHaveTextContent('You can buy Racing game');
  expect(wishlistRow().querySelector('.wishlist-row-check')).toBeInTheDocument();
  expect(within(wishlistRow()).queryByText(/to go for/)).not.toBeInTheDocument();
});
