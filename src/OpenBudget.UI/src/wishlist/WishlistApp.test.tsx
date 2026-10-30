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
