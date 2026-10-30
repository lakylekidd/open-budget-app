import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { CreditPaymentDetails } from './CreditPaymentDetails';
import { formatMoney, type Account } from './ledger/api';

const card: Account = { id: 'card-1', name: 'Mastercard', isClosed: false, balance: -2674.47, clearedBalance: -2674.47, unclearedBalance: 0, accountType: 'creditCard',
  isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0 };
const progress = { neededForPayoff: 2674.47, availableForPayment: 500.40, assignedThisMonth: 0, toGo: 2174.07, percent: 18, assignThisMonth: 362.35, monthsRemaining: 6, isPastDue: false, status: 'behind' };
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });

let target: Record<string, unknown> | null;
let version: number;
let requests: { path: string; method: string; body: Record<string, unknown> }[];
let failNext: 'lost' | 'conflict' | null;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 21, 12));
  target = null; version = 7; requests = []; failNext = null;
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path.startsWith('/api/accounts/card-1/credit-summary/')) return json({ accountId: 'card-1', month: '2026-10', priorBalance: -2674.07, spendingAndOutflows: -134.40, interestCharges: 0, paymentsAndInflows: 134, currentBalance: -2674.47 });
    if (path === '/api/accounts/card-1/payoff-target/2026-10') return json({ accountId: 'card-1', month: '2026-10', version, target, progress: target ? progress : null });
    if (init?.method) {
      const body = JSON.parse(String(init.body));
      requests.push({ path, method: init.method, body });
      if (failNext === 'lost') { failNext = null; return json({ error: 'Gateway timeout' }, 504); }
      if (failNext === 'conflict') { failNext = null; version++; return json({ error: 'The ledger changed in another session. Reload, review the latest entries, and retry.' }, 409); }
      if (path === '/api/accounts/card-1/payoff-target' && init.method === 'PUT')
        target = { kind: body.kind, targetMonth: body.targetMonth, monthlyAmount: body.monthlyAmount, payoffAmount: 2674.07, createdAt: '2026-10-21T12:00:00Z', updatedAt: `2026-10-21T12:00:0${requests.length}Z` };
      if (path === '/api/accounts/card-1/payoff-target/delete') target = null;
      version++;
      return json({ id: 'card-1', version });
    }
    throw new Error('Unexpected request ' + path);
  }));
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

function renderPanel(onChanged = vi.fn()) {
  render(<CreditPaymentDetails account={card} month="2026-10" available={500.40} money={formatMoney} onClose={() => {}} onChanged={onChanged} />);
  return { onChanged, panel: screen.getByRole('region', { name: 'Mastercard payment' }) };
}
async function openEditor() {
  const button = await screen.findByRole('button', { name: 'Create a Debt Payoff Target' });
  await waitFor(() => expect(button).toBeEnabled());
  await userEvent.click(button);
  return screen.getByRole('form', { name: 'Debt payoff target' });
}

it('creates a pay-by-date target from the payment panel and shows server-calculated progress', async () => {
  const { onChanged } = renderPanel();
  const editor = await openEditor();
  expect(within(editor).getByText('Target')).toBeInTheDocument();
  expect(within(editor).queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument();
  const type = within(editor).getByRole('button', { name: 'Target type' });
  expect(type).toHaveTextContent('Pay Off Balance by Date');
  await userEvent.click(type);
  const options = within(within(editor).getByRole('listbox')).getAllByRole('option');
  expect(options.map(option => option.textContent)).toEqual(['✓Pay Off Balance by Date', '▦Pay Specific Amount Monthly']);
  expect(options[0]).toHaveAttribute('aria-selected', 'true');
  await userEvent.click(within(editor).getByRole('button', { name: /Making minimum payments\?/ }));
  expect(within(editor).getByText(/Assign at least your card’s minimum payment/)).toBeInTheDocument();
  await userEvent.click(options[0]);
  expect(within(editor).queryByRole('listbox')).not.toBeInTheDocument();

  const month = within(editor).getByRole('combobox', { name: 'Target month' });
  const year = within(editor).getByRole('combobox', { name: 'Target year' });
  expect(within(year).getAllByRole('option').map(option => option.textContent)).toEqual(Array.from({ length: 10 }, (_, index) => String(2026 + index)));
  await userEvent.selectOptions(year, '2026');
  expect(within(month).getByRole('option', { name: 'September' })).toBeDisabled();
  expect(within(month).getByRole('option', { name: 'October' })).toBeEnabled();
  await userEvent.selectOptions(year, '2027');
  expect(within(month).getByRole('option', { name: 'January' })).toBeEnabled();
  await userEvent.selectOptions(month, 'March');
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));

  await waitFor(() => expect(requests).toHaveLength(1));
  expect(requests[0]).toMatchObject({ path: '/api/accounts/card-1/payoff-target', method: 'PUT', body: { kind: 'payByDate', targetMonth: '2027-03', monthlyAmount: null, version: 7 } });
  expect(requests[0].body.commandId).toEqual(expect.any(String));
  const summary = await screen.findByRole('region', { name: 'Debt payoff target' });
  expect(onChanged).toHaveBeenCalledTimes(1);
  expect(summary).toHaveTextContent('Pay Off Balance by Date');
  expect(summary).toHaveTextContent('Pay off €2,674.07');
  expect(summary).toHaveTextContent('By Mar 2027');
  expect(within(summary).getByRole('progressbar', { name: 'Target progress' })).toHaveAttribute('aria-valuenow', '18');
  expect(within(summary).getByRole('progressbar', { name: 'Target progress' })).toHaveClass('behind');
  expect(within(summary).getByRole('status')).toHaveTextContent('Assign €362.35 this month to stay on track');
  expect(summary).toHaveTextContent('Needed for Payoff€2,674.47');
  expect(summary).toHaveTextContent('Available for Payment€500.40');
  expect(summary).toHaveTextContent('To Go€2,174.07');
  expect(screen.queryByText('Payment')).not.toBeInTheDocument();
  expect(screen.getByText(/Current Balance/)).toBeInTheDocument();
  await userEvent.click(within(summary).getByRole('button', { name: 'Target' }));
  expect(summary).not.toHaveTextContent('To Go');
});

it('reopens a saved target with its values, cancels without saving and deletes it', async () => {
  target = { kind: 'payByDate', targetMonth: '2027-03', monthlyAmount: null, payoffAmount: 2674.07, createdAt: '2026-10-01T00:00:00Z', updatedAt: '2026-10-01T00:00:00Z' };
  const { onChanged } = renderPanel();
  await userEvent.click(await screen.findByRole('button', { name: 'Edit Target' }));
  let editor = screen.getByRole('form', { name: 'Debt payoff target' });
  expect(within(editor).getByRole('combobox', { name: 'Target month' })).toHaveDisplayValue('March');
  expect(within(editor).getByRole('combobox', { name: 'Target year' })).toHaveDisplayValue('2027');
  await userEvent.selectOptions(within(editor).getByRole('combobox', { name: 'Target year' }), '2029');
  await userEvent.click(within(editor).getByRole('button', { name: 'Cancel' }));
  expect(requests).toHaveLength(0);
  expect(screen.getByRole('region', { name: 'Debt payoff target' })).toHaveTextContent('By Mar 2027');

  await userEvent.click(screen.getByRole('button', { name: 'Edit Target' }));
  editor = screen.getByRole('form', { name: 'Debt payoff target' });
  expect(within(editor).getByRole('combobox', { name: 'Target year' })).toHaveDisplayValue('2027');
  await userEvent.click(within(editor).getByRole('button', { name: /Delete/ }));
  await waitFor(() => expect(requests).toHaveLength(1));
  expect(requests[0]).toMatchObject({ path: '/api/accounts/card-1/payoff-target/delete', method: 'POST', body: { version: 7 } });
  expect(await screen.findByRole('button', { name: 'Create a Debt Payoff Target' })).toBeInTheDocument();
  expect(screen.queryByRole('region', { name: 'Debt payoff target' })).not.toBeInTheDocument();
  expect(onChanged).toHaveBeenCalledTimes(1);
});

it('saves a monthly amount target after validating the amount', async () => {
  renderPanel();
  const editor = await openEditor();
  await userEvent.click(within(editor).getByRole('button', { name: 'Target type' }));
  await userEvent.click(within(editor).getByRole('option', { name: /Pay Specific Amount Monthly/ }));
  expect(within(editor).queryByRole('combobox', { name: 'Target month' })).not.toBeInTheDocument();
  const amount = within(editor).getByRole('textbox', { name: 'Amount per month' });
  await userEvent.type(amount, '0');
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
  expect(within(editor).getByRole('alert')).toHaveTextContent('Enter a positive monthly amount');
  expect(requests).toHaveLength(0);
  await userEvent.clear(amount); await userEvent.type(amount, '250,50');
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
  await waitFor(() => expect(requests).toHaveLength(1));
  expect(requests[0].body).toMatchObject({ kind: 'monthlyAmount', targetMonth: null, monthlyAmount: 250.5 });
  expect(await screen.findByRole('region', { name: 'Debt payoff target' })).toHaveTextContent('Pay €250.50 monthly');
});

it('retries a lost save with the same command and asks for a reload after a ledger conflict', async () => {
  renderPanel();
  let editor = await openEditor();
  failNext = 'lost';
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
  expect(await screen.findByText(/The save outcome is unknown/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'Retry save' }));
  await screen.findByRole('region', { name: 'Debt payoff target' });
  expect(requests).toHaveLength(2);
  expect(requests[1].body.commandId).toBe(requests[0].body.commandId);
  expect(requests[1].body.version).toBe(7);

  await userEvent.click(screen.getByRole('button', { name: 'Edit Target' }));
  editor = screen.getByRole('form', { name: 'Debt payoff target' });
  failNext = 'conflict';
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
  expect(await screen.findByText(/The ledger changed in another session/)).toBeInTheDocument();
  expect(within(editor).getByRole('button', { name: 'Save Target' })).toBeDisabled();
  await userEvent.click(screen.getByRole('button', { name: 'Reload target' }));
  await waitFor(() => expect(within(editor).getByRole('button', { name: 'Save Target' })).toBeEnabled());
  await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
  await waitFor(() => expect(requests).toHaveLength(4));
  expect(requests[3].body.version).toBe(9);
  expect(requests[3].body.commandId).not.toBe(requests[2].body.commandId);
});
