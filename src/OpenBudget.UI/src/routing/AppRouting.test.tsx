import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { budgetKeyFor, routeNotices } from './routes';

// App-level URL routing: redirects, deep links, nav -> URL, back/forward, leave-guards and budget switching.
type Category = { id: string; name: string; assigned: number; activity: number; available: number; target: number; icon: string; snoozed: boolean; sortOrder: number; isArchived: boolean };
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const month = (value: string, assigned = 0) => ({
  month: value, note: '', version: 1, fundsAdded: 0, openingReadyToAssign: 500, ledgerIncome: 0, readyToAssign: 500 - assigned,
  groups: [{ id: 'home', name: 'Home', sortOrder: 0, isArchived: false, categories: [{ id: 'utilities', name: 'Electricity', assigned, activity: 0, available: assigned, target: 0, icon: '', snoozed: false, sortOrder: 0, isArchived: false } satisfies Category] }],
});
const account = (id: string, name: string, extra: Record<string, unknown> = {}) => ({ id, name, isClosed: false, balance: 100, clearedBalance: 100, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0, ...extra });
const budgets = [
  { id: 'default', name: 'Test budget', currency: 'EUR', version: 1, isArchived: false, createdAt: '2026-01-01T00:00:00Z' },
  { id: 'travel-budget', name: 'Travel', currency: 'EUR', version: 1, isArchived: false, createdAt: '2026-02-01T00:00:00Z' },
];
const key = budgetKeyFor('default');
const travelKey = budgetKeyFor('travel-budget');
let accountsByBudget: Record<string, ReturnType<typeof account>[]>;
let holdSave: boolean;
let releaseSave: (() => void) | undefined;

const activeBudget = () => /openbudget\.activeBudgetId=([^;]+)/.exec(document.cookie)?.[1] ?? 'default';

beforeEach(() => {
  window.history.replaceState(null, '', '/');
  window.localStorage.clear();
  document.cookie = 'openbudget.activeBudgetId=; Path=/; Max-Age=0';
  holdSave = false; releaseSave = undefined;
  accountsByBudget = {
    default: [account('3f9a1111-0000-4000-8000-000000000001', 'Salary'), account('7c2e2222-0000-4000-8000-000000000002', 'Old Savings', { isClosed: true })],
    'travel-budget': [account('b0b00000-0000-4000-8000-000000000003', 'Travel Card')],
  };
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path === '/api/auth/session') return json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: true });
    if (path === '/api/accounts') return json({ version: 1, accounts: accountsByBudget[activeBudget()] ?? [] });
    if (path === '/api/budgets?includeArchived=true' || path === '/api/budgets') return json({ budgets, limit: null });
    if (path.startsWith('/api/budgets/') && path.includes('/months/')) {
      const value = path.slice(-7);
      if (init?.method === 'PUT') {
        const request = JSON.parse(String(init.body));
        if (holdSave) await new Promise<void>(resolve => { releaseSave = resolve; });
        return json({ ...month(value, request.groups[0].categories[0].assigned), version: request.version + 1 });
      }
      return json(month(value));
    }
    if (path.endsWith('/sharing')) return json({ manager: { id: 'u1', name: 'alice', email: 'alice@example.test', role: 'manager', joinedAt: '2026-01-01T00:00:00Z' }, members: [], invitations: [], canManage: true, invitesRemaining: 4 });
    if (path.includes('/plan-actions')) return json([]);
    if (path.startsWith('/api/long-term-planning/')) {
      const year = Number(path.split('/').at(-1));
      return json({ year, version: 1, hasPlanningData: false, months: Array.from({ length: 12 }, (_, index) => ({ month: `${year}-${String(index + 1).padStart(2, '0')}`, expectedIncome: 0, hasExpectedIncome: false, expectedExpenses: 0, remaining: 0, hasActualData: false })), groups: [] });
    }
    if (path.startsWith('/api/schedules?')) return json({ version: 1, page: 1, pageSize: 100, total: 0, items: [] });
    if (path.startsWith('/api/register-views/')) return json({ version: 1, datePreset: 'all', fromMonth: null, toMonth: null, showReconciled: false, showRunningBalance: false, showFlags: false, showImages: false, showScheduledTransactions: true });
    if (path === '/api/flags') return json({ version: 1, flags: [] });
    if (path.startsWith('/api/transactions?')) return json({ version: 1, page: 1, pageSize: 100, total: 0, items: [], accounts: { version: 1, accounts: accountsByBudget[activeBudget()] ?? [] } });
    return json({}, 404);
  }));
});
afterEach(() => vi.unstubAllGlobals());

const planSaved = () => screen.findByText('Plan saved');
const path = () => window.location.pathname;
const nav = () => within(screen.getByRole('navigation', { name: 'Main navigation' }));
const toast = () => screen.findByText((_, element) => element?.classList.contains('toast') === true);
async function goBack() { await act(async () => { window.history.back(); await new Promise(resolve => setTimeout(resolve, 20)); }); }
async function goForward() { await act(async () => { window.history.forward(); await new Promise(resolve => setTimeout(resolve, 20)); }); }

it('redirects / to the active budget plan without adding a history entry and titles the tab', async () => {
  const length = window.history.length;
  render(<App />); await planSaved();
  await waitFor(() => expect(path()).toBe(`/${key}/plan`));
  expect(window.history.length).toBe(length);
  expect(document.title).toBe('Plan · Test budget · OpenBudget');
});

it('updates the address bar from the main navigation, sidebar accounts and budget menu', async () => {
  render(<App />); await planSaved();
  await userEvent.click(nav().getByRole('button', { name: 'Long-Term' }));
  await waitFor(() => expect(path()).toBe(`/${key}/long-term`));
  await userEvent.click(nav().getByRole('button', { name: 'Reflect' }));
  await waitFor(() => expect(path()).toBe(`/${key}/reflect`));
  expect(screen.getByRole('heading', { name: 'Reflect' })).toBeInTheDocument();
  await userEvent.click(nav().getByRole('button', { name: 'All Accounts' }));
  await waitFor(() => expect(path()).toBe(`/${key}/account/all`));
  expect(document.title).toBe('All Accounts · Test budget · OpenBudget');
  await userEvent.click(await screen.findByRole('button', { name: /^Salary/ }));
  await waitFor(() => expect(path()).toBe(`/${key}/account/salary`));
  expect(document.title).toBe('Salary · Test budget · OpenBudget');
  await userEvent.click(screen.getByRole('button', { name: 'Budget menu' }));
  await userEvent.click(screen.getByRole('menuitem', { name: /Budget Together/ }));
  await waitFor(() => expect(path()).toBe(`/${key}/together`));
  await userEvent.click(nav().getByRole('button', { name: 'Plan' }));
  await waitFor(() => expect(path()).toBe(`/${key}/plan`));
});

it('keeps month navigation in the URL and opens a deep-linked month', async () => {
  window.history.replaceState(null, '', `/${key}/plan/2026-11`);
  render(<App />); await planSaved();
  expect(await screen.findByRole('button', { name: 'Nov 2026' })).toBeInTheDocument();
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/default/months/2026-11', expect.anything());
  expect(path()).toBe(`/${key}/plan/2026-11`);
  await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
  await waitFor(() => expect(path()).toBe(`/${key}/plan/2026-12`));
  await screen.findByRole('button', { name: 'Dec 2026' });
  await planSaved();
  await goBack();
  expect(await screen.findByRole('button', { name: 'Nov 2026' })).toBeInTheDocument();
  expect(path()).toBe(`/${key}/plan/2026-11`);
});

it('opens a deep-linked account, including closed accounts and id-suffixed links', async () => {
  window.history.replaceState(null, '', `/${key}/account/salary`);
  const { unmount } = render(<App />);
  await waitFor(() => expect(screen.getByRole('button', { name: /^Salary/ })).toHaveAttribute('aria-current', 'page'));
  expect(path()).toBe(`/${key}/account/salary`);
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([request]) => String(request).startsWith('/api/transactions?') && String(request).includes('accountId=3f9a1111'))).toBe(true));
  unmount();

  // A link from before a rename still resolves through its account-id suffix, then canonicalises.
  window.history.replaceState(null, '', `/${key}/account/old-name-7c2e`);
  render(<App />);
  await waitFor(() => expect(screen.getByRole('button', { name: /^Old Savings \(Closed\)/ })).toHaveAttribute('aria-current', 'page'));
  await waitFor(() => expect(path()).toBe(`/${key}/account/old-savings`));
});

it('falls back to All Accounts with a notice when an account slug no longer resolves', async () => {
  window.history.replaceState(null, '', `/${key}/account/renamed-card`);
  render(<App />);
  expect(await toast()).toHaveTextContent(routeNotices.accountNotFound);
  await waitFor(() => expect(path()).toBe(`/${key}/account/all`));
});

it('opens a deep-linked Long-Term Planning year', async () => {
  window.history.replaceState(null, '', `/${key}/long-term/2030`);
  render(<App />);
  expect(await screen.findByRole('heading', { name: 'Long-Term' })).toBeInTheDocument();
  await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/long-term-planning/2030', expect.anything()));
  expect(path()).toBe(`/${key}/long-term/2030`);

  await userEvent.click(screen.getByRole('button', { name: 'Next year' }));
  await waitFor(() => expect(path()).toBe(`/${key}/long-term/2031`));
  await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/long-term-planning/2031', expect.anything()));
  await goBack();
  expect(path()).toBe(`/${key}/long-term/2030`);
  await waitFor(() => expect(screen.getByLabelText('Year navigation')).toHaveTextContent('2030'));
});

it('redirects unknown budgets and pages to a plan with a notice', async () => {
  window.history.replaceState(null, '', '/AAAAAAAAAAA/wishlist');
  const { unmount } = render(<App />);
  expect(await toast()).toHaveTextContent(routeNotices.budgetNotFound);
  await waitFor(() => expect(path()).toBe(`/${key}/plan`));
  unmount();

  window.history.replaceState(null, '', `/${travelKey}/does-not-exist`);
  render(<App />);
  expect(await toast()).toHaveTextContent(routeNotices.unknownPage);
  await waitFor(() => expect(path()).toBe(`/${travelKey}/plan`));
  expect(screen.getByRole('button', { name: 'Budget menu' })).toHaveTextContent('Travel');
});

it('maps legacy ?page= links to clean paths', async () => {
  window.history.replaceState(null, '', '/?page=Accounts');
  render(<App />);
  await waitFor(() => expect(path()).toBe(`/${key}/account/all`));
  expect(window.location.search).toBe('');
});

it('moves between pages with browser back and forward', async () => {
  render(<App />); await planSaved();
  await userEvent.click(nav().getByRole('button', { name: 'All Accounts' }));
  await waitFor(() => expect(path()).toBe(`/${key}/account/all`));
  await userEvent.click(await screen.findByRole('button', { name: /^Salary/ }));
  await waitFor(() => expect(path()).toBe(`/${key}/account/salary`));
  await goBack();
  expect(path()).toBe(`/${key}/account/all`);
  await waitFor(() => expect(screen.getByRole('button', { name: /^Salary/ })).not.toHaveAttribute('aria-current'));
  await goBack();
  expect(path()).toBe(`/${key}/plan`);
  expect(await screen.findByRole('heading', { name: 'Plan' })).toBeInTheDocument();
  await goForward();
  expect(path()).toBe(`/${key}/account/all`);
  expect(nav().getByRole('button', { name: 'All Accounts' })).toHaveClass('active');
});

it('blocks back/forward while a Plan edit is unsaved and restores the address bar', async () => {
  render(<App />); await planSaved();
  await userEvent.click(nav().getByRole('button', { name: 'Long-Term' }));
  await waitFor(() => expect(path()).toBe(`/${key}/long-term`));
  await userEvent.click(nav().getByRole('button', { name: 'Plan' }));
  await planSaved();
  await waitFor(() => expect(path()).toBe(`/${key}/plan`));

  holdSave = true;
  await userEvent.click(screen.getByRole('button', { name: 'Edit assigned for Electricity' }));
  const input = screen.getByRole('textbox', { name: 'Assigned to Electricity' });
  await userEvent.clear(input); await userEvent.type(input, '40'); await userEvent.keyboard('{Enter}');
  await screen.findByText('Saving plan…');

  await goBack();
  expect(await screen.findByText(/Wait for the plan to save/)).toBeInTheDocument();
  expect(path()).toBe(`/${key}/plan`);
  expect(screen.getByRole('heading', { name: 'Plan' })).toBeInTheDocument();

  await waitFor(() => expect(releaseSave).toBeDefined());
  await act(async () => { releaseSave?.(); });
  await planSaved();
  await goBack();
  await waitFor(() => expect(path()).toBe(`/${key}/long-term`));
  expect(await screen.findByRole('heading', { name: 'Long-Term' })).toBeInTheDocument();
}, 15000);

it('keeps the current page when switching budgets and falls back to All Accounts', async () => {
  render(<App />); await planSaved();
  await userEvent.click(nav().getByRole('button', { name: 'Long-Term' }));
  await waitFor(() => expect(path()).toBe(`/${key}/long-term`));
  await userEvent.click(screen.getByRole('button', { name: 'Budget menu' }));
  await userEvent.click(screen.getByRole('menuitem', { name: /Open Plan/ }));
  await userEvent.click(within(screen.getByRole('menu', { name: 'Recent Plans' })).getByRole('menuitem', { name: /Travel/ }));
  await waitFor(() => expect(path()).toBe(`/${travelKey}/long-term`));
  expect(document.title).toBe('Long-Term · Travel · OpenBudget');

  await userEvent.click(await screen.findByRole('button', { name: /^Travel Card/ }));
  await waitFor(() => expect(path()).toBe(`/${travelKey}/account/travel-card`));
  await userEvent.click(screen.getByRole('button', { name: 'Budget menu' }));
  await userEvent.click(screen.getByRole('menuitem', { name: /Open Plan/ }));
  await userEvent.click(within(screen.getByRole('menu', { name: 'Recent Plans' })).getByRole('menuitem', { name: /Test budget/ }));
  await waitFor(() => expect(path()).toBe(`/${key}/account/all`));

  // Back into the other budget's account: the budget switches, then the slug resolves once its accounts load.
  await goBack();
  await waitFor(() => expect(path()).toBe(`/${travelKey}/account/travel-card`));
  await waitFor(() => expect(screen.getByRole('button', { name: /^Travel Card/ })).toHaveAttribute('aria-current', 'page'));
  expect(screen.getByRole('button', { name: 'Budget menu' })).toHaveTextContent('Travel');
});
