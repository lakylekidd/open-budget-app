import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { AccountsPage } from './AccountsPage';
import { defaultView } from './ViewOptions';
import type { Account } from './api';
import { durationLabel, parseRate } from './loans';
import type { LoanDetails, LoanProjection } from './loans';

const base = { isClosed: false, clearedBalance: 0, unclearedBalance: 0, isFavorite: false, connectionStatus: 'unlinked' as const, lastReconciledAt: null, needsCategoryCount: 0 };
const checking: Account = { ...base, id: 'checking', name: 'ING', balance: 5000, clearedBalance: 5000, accountType: 'checking' };
const mortgage: Account = { ...base, id: 'mortgage', name: 'Mortgage (400K @ 3.47%)', balance: -389651.68, clearedBalance: -389651.68, accountType: 'mortgage' };
const phone: Account = { ...base, id: 'phone', name: 'Phone', balance: -675.08, clearedBalance: -675.08, accountType: 'contract' };

const track = (months: number, start = 389651.68) => Array.from({ length: months + 1 }, (_, i) => ({ month: `${2026 + Math.floor((9 + i) / 12)}-${String((9 + i) % 12 + 1).padStart(2, '0')}`, balance: Math.max(start - i * 1000, 0) }));
const minimumProjection: LoanProjection = { monthlyPayment: 1789.49, paymentsRemaining: 344, payoffMonth: '2055-06', interestRemaining: 225929.45, principalRemaining: 389651.68, remainingToPay: 615581.13, neverPaidOff: false, track: track(12) };
let loan: LoanDetails;
let mutations: { path: string; method: string; body: Record<string, unknown> }[];
let simulations: string[];
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  try { window.localStorage.clear(); } catch { /* ignore */ }
  mutations = []; simulations = [];
  loan = {
    accountId: 'mortgage', accountType: 'mortgage', version: 3, currentMonth: '2026-10', balance: -389651.68, interestRate: 3.47, minimumPayment: 1789.49,
    escrowAmount: 0, originalBalance: 397000, percentRemaining: 98.1, balanceConfirmedAt: null, estimatedInterestThisMonth: 1126.74,
    pairedCategory: { id: 'loan-mortgage', name: 'Mortgage (400K @ 3.47%)', groupName: 'Loans', available: 0, isArchived: false, isAutomatic: true, targetKind: null, targetMoreNeeded: null },
    projection: minimumProjection, history: [{ month: '2026-09', balance: 390312.52 }, { month: '2026-10', balance: 389651.68 }],
    activity: [
      { month: '2026-10', endBalance: -389651.68, estimatedInterest: 1128.65, payments: [{ id: 'p1', date: '2026-10-07', amount: 1789.49, kind: 'payment', description: '', accountName: 'ING' }], other: [] },
      { month: '2026-09', endBalance: -390312.52, estimatedInterest: 1130.55, payments: [], other: [] },
      { month: '2025-12', endBalance: -395531.08, estimatedInterest: 1140, payments: [], other: [] },
    ],
    contract: null,
  };
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (init?.method && init.method !== 'GET') {
      mutations.push({ path, method: init.method, body: JSON.parse(String(init.body)) });
      return response({ id: 'mortgage', version: 4 });
    }
    if (path.startsWith('/api/register-views/')) return response(defaultView);
    if (path.startsWith('/api/accounts/mortgage/loan/simulation')) {
      simulations.push(path);
      const query = new URL(path, 'http://x').searchParams;
      if (query.get('payoffMonth')) return response({ accountId: 'mortgage', currentMonth: '2026-10', balance: -389651.68, minimumPayment: 1789.49, extra: 0,
        projection: { ...minimumProjection, monthlyPayment: 2253.83, paymentsRemaining: 240, payoffMonth: query.get('payoffMonth'), interestRemaining: 151264.28, remainingToPay: 540915.96 } });
      const payment = Number(query.get('payment'));
      if (payment === 1000) return response({ accountId: 'mortgage', currentMonth: '2026-10', balance: -389651.68, minimumPayment: 1789.49, extra: 0,
        projection: { ...minimumProjection, monthlyPayment: 1000, paymentsRemaining: null, payoffMonth: null, neverPaidOff: true, interestRemaining: 0, remainingToPay: 0, track: track(0) } });
      return response({ accountId: 'mortgage', currentMonth: '2026-10', balance: -389651.68, minimumPayment: 1789.49, extra: Number(query.get('extra') ?? 0), projection: minimumProjection });
    }
    if (path === '/api/accounts/mortgage/loan') return response(loan);
    if (path === '/api/accounts/phone/loan') return response({ ...loan, accountId: 'phone', accountType: 'contract', balance: -675.08, interestRate: 0, minimumPayment: 44.99, pairedCategory: null,
      projection: { ...minimumProjection, monthlyPayment: 44.99, paymentsRemaining: 15, payoffMonth: '2027-12', interestRemaining: 0 },
      contract: { months: 24, startDate: '2026-01-31', endDate: '2027-12-31', includesDevice: true, upfrontCost: 50, monthlyServiceCost: 20, deviceTotal: 599.99, discountMonths: 6, discountedServiceCost: 10,
        totalCost: 1069.99, remainingDevice: 375.08, remainingService: 300, remainingCommitment: 675.08, remainingMonths: 15,
        schedule: [{ number: 1, dueDate: '2026-01-31', service: 10, device: 24.99, total: 34.99, isDiscounted: true, isPast: true }, { number: 24, dueDate: '2027-12-31', service: 20, device: 25.22, total: 45.22, isDiscounted: false, isPast: false }] } });
    if (path.startsWith('/api/accounts')) return response({ version: 3, accounts: [checking, mortgage, phone] });
    if (path.startsWith('/api/transactions')) return response({ version: 3, page: 1, pageSize: 25, total: 0, items: [], accounts: { version: 3, accounts: [checking, mortgage, phone] } });
    if (path.startsWith('/api/schedules')) return response({ version: 3, page: 1, pageSize: 25, total: 0, items: [] });
    if (path.startsWith('/api/budget/')) return response({ readyToAssign: 0, groups: [{ id: 'home', name: 'Home', categories: [{ id: 'rent', name: 'Rent', available: 0 }] }] });
    if (path === '/api/payees') return response([]);
    if (path === '/api/flags') return response({ version: 3, flags: [] });
    throw new Error(`Unexpected request ${path}`);
  }));
});

function show(selectedAccount = 'mortgage', onOpenCategory = vi.fn()) {
  render(<AccountsPage selectedAccount={selectedAccount} onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} onOpenCategory={onOpenCategory} />);
  return onOpenCategory;
}

it('formats durations and validates interest rates', () => {
  expect(durationLabel(344)).toBe('28 yrs, 8 mos');
  expect(durationLabel(12)).toBe('1 yr');
  expect(durationLabel(1)).toBe('1 mo');
  expect(durationLabel(null)).toBe('Never');
  expect(parseRate('3.47')).toBe(3.47);
  expect(parseRate('3.4705')).toBeNull();
  expect(parseRate('101')).toBeNull();
  expect(parseRate('abc')).toBeNull();
});

it('shows the Mortgages and Loans section with Contract and validates the add form before saving', async () => {
  const user = userEvent.setup();
  show('');
  await user.click(await screen.findByRole('button', { name: 'Add account' }));
  const loans = within(screen.getByRole('dialog', { name: 'Select Account Type' })).getByRole('region', { name: 'Mortgages and Loans' });
  expect(loans).toHaveTextContent('Accounts that have an outstanding balance you’re currently paying off, and aren’t spending from.');
  expect(within(loans).getAllByRole('button').map(button => button.textContent)).toEqual(['Mortgage', 'Auto Loan', 'Student Loan', 'Personal Loan', 'Medical Debt', 'Other Debt', 'Contract']);
  await user.click(within(loans).getByRole('button', { name: 'Auto Loan' }));
  const form = screen.getByRole('dialog', { name: 'Add Unlinked Account' });
  expect(within(form).getByRole('button', { name: 'Back' })).toBeInTheDocument();
  expect(within(form).getByLabelText('What type of account are you adding?')).toHaveValue('autoLoan');
  const next = within(form).getByRole('button', { name: 'Next' });
  expect(next).toBeDisabled();
  await user.type(within(form).getByLabelText('Give it a nickname'), 'Car');
  await user.type(within(form).getByLabelText('Current account balance'), '12000');
  await user.type(within(form).getByLabelText('Interest rate'), '4.9');
  expect(next).toBeDisabled();
  await user.type(within(form).getByLabelText('Monthly payment required by your lender'), '250+25');
  expect(next).toBeEnabled();
  await user.clear(within(form).getByLabelText('Interest rate')); await user.type(within(form).getByLabelText('Interest rate'), '4.9999');
  expect(next).toBeDisabled();
  await user.clear(within(form).getByLabelText('Interest rate')); await user.type(within(form).getByLabelText('Interest rate'), '4.9');
  await user.click(next);
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/accounts');
  expect(mutations[0].body).toMatchObject({ name: 'Car', accountType: 'autoLoan', openingBalance: -12000, loan: { interestRate: 4.9, minimumPayment: 275, createPairedCategory: true, contract: null } });
});

it('adds a contract with discount fields', async () => {
  const user = userEvent.setup();
  show('');
  await user.click(await screen.findByRole('button', { name: 'Add account' }));
  await user.click(within(screen.getByRole('region', { name: 'Mortgages and Loans' })).getByRole('button', { name: 'Contract' }));
  const form = screen.getByRole('dialog', { name: 'Add Unlinked Account' });
  await user.type(within(form).getByLabelText('Give it a nickname'), 'Phone');
  expect(within(form).getByRole('button', { name: 'Next' })).toBeDisabled();
  await user.selectOptions(within(form).getByLabelText('Contract length'), '12');
  await user.clear(within(form).getByLabelText('Start date')); await user.type(within(form).getByLabelText('Start date'), '2026-10-01');
  await user.type(within(form).getByLabelText('Monthly contract cost'), '20');
  await user.type(within(form).getByLabelText('Device total (spread over the contract)'), '600');
  await user.click(within(form).getByLabelText('Discounted for the first months'));
  await user.clear(within(form).getByLabelText('Discount months')); await user.type(within(form).getByLabelText('Discount months'), '3');
  await user.type(within(form).getByLabelText('Discounted monthly cost'), '25');
  expect(within(form).getByRole('button', { name: 'Next' })).toBeDisabled();
  await user.clear(within(form).getByLabelText('Discounted monthly cost')); await user.type(within(form).getByLabelText('Discounted monthly cost'), '10');
  await user.click(within(form).getByRole('button', { name: 'Next' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ accountType: 'contract', openingBalance: 0, loan: { interestRate: 0, contract: { months: 12, startDate: '2026-10-01', includesDevice: true, monthlyServiceCost: 20, deviceTotal: 600, discountMonths: 3, discountedServiceCost: 10 } } });
});

it('renders the loan page with stats, chart, paired category and monthly activity', async () => {
  const user = userEvent.setup();
  const onOpenCategory = show();
  const page = await screen.findByRole('region', { name: 'Mortgage (400K @ 3.47%) loan' });
  expect(screen.queryByRole('table', { name: 'Transaction register' })).not.toBeInTheDocument();
  const stats = within(page).getByLabelText('Loan summary');
  expect(stats).toHaveTextContent('€389,651.68Remaining Balance');
  expect(stats).toHaveTextContent('3.47%Interest Rate');
  expect(stats).toHaveTextContent('€1,789.49Minimum Payment');
  expect(stats).toHaveTextContent('Jun 2055Payoff Date');
  expect(page).toHaveTextContent('Updated never');
  expect(page).toHaveTextContent('€389,652 (98.1%)');
  expect(within(page).getByRole('img', { name: /balance: past and projected/ })).toBeInTheDocument();
  expect(page).toHaveTextContent('The minimum payment of €1,789.49 will pay off this loan in 28 yrs, 8 mos');
  expect(page).toHaveTextContent('Loans: Mortgage (400K @ 3.47%)');

  const activity = within(page).getByRole('region', { name: 'Account Activity' });
  const october = within(activity).getByRole('button', { name: /October 2026/ });
  expect(october).toHaveAttribute('aria-expanded', 'true');
  expect(activity).toHaveTextContent('€1,789.49 from ING on 7 October');
  expect(activity).toHaveTextContent('Interest Charges This Month');
  expect(activity).toHaveTextContent('-€1,128.65');
  expect(within(activity).queryByRole('button', { name: /December 2025/ })).not.toBeInTheDocument();
  await user.click(within(activity).getByRole('button', { name: /2025/ }));
  expect(within(activity).getByRole('button', { name: /December 2025/ })).toBeInTheDocument();

  await user.click(within(page).getByRole('button', { name: 'Create Debt Payment Target' }));
  expect(onOpenCategory).toHaveBeenCalledWith('loan-mortgage', true);
  await user.click(within(page).getByRole('button', { name: 'Dismiss payoff simulator banner' }));
  expect(within(page).queryByRole('button', { name: 'Try It Now' })).not.toBeInTheDocument();
});

it('runs the payoff simulator read-only: payment, payoff date, extra and never-paid-off', async () => {
  const user = userEvent.setup();
  show();
  await user.click(await screen.findByRole('button', { name: 'Try It Now' }));
  const dialog = screen.getByRole('dialog', { name: 'Loan Payoff Simulator' });
  expect(await within(dialog).findByText('Paying the minimum every month')).toBeInTheDocument();
  expect(dialog).toHaveTextContent('You have 344 payments remaining on your loan.');
  expect(dialog).toHaveTextContent('€225,929.45Interest Remaining');
  expect(dialog).toHaveTextContent('Remaining to Pay€615,581.13');
  await user.click(within(dialog).getByRole('button', { name: 'Payoff Date' }));
  const picker = within(dialog).getByRole('dialog', { name: 'Payoff Date picker' });
  expect(within(picker).getByRole('button', { name: 'Jun' })).toHaveAttribute('aria-pressed', 'true');
  for (let i = 0; i < 9; i++) await user.click(within(picker).getByRole('button', { name: 'Previous year' }));
  await user.click(within(picker).getByRole('button', { name: 'Oct' }));
  await waitFor(() => expect(within(dialog).getByLabelText('Monthly Payment')).toHaveValue('2253.83'));
  expect(simulations.some(path => path.includes('payoffMonth=2046-10'))).toBe(true);
  expect(dialog).toHaveTextContent('Paying €2,253.83 every month');
  await user.clear(within(dialog).getByLabelText('Monthly Payment')); await user.type(within(dialog).getByLabelText('Monthly Payment'), '1000');
  expect(await within(dialog).findByText('This payment never pays off the loan.')).toBeInTheDocument();
  await user.type(within(dialog).getByLabelText('One time extra payment'), '500');
  await waitFor(() => expect(simulations.some(path => path.includes('extra=500'))).toBe(true));
  await user.click(within(dialog).getByRole('button', { name: 'Done' }));
  expect(mutations).toHaveLength(0);
});

it('confirms the lender balance or records a correction', async () => {
  const user = userEvent.setup();
  show();
  await user.click(await screen.findByRole('button', { name: 'Update Balance' }));
  let dialog = screen.getByRole('dialog', { name: 'Update Balance' });
  expect(dialog).toHaveTextContent('Is your current balance at your lender');
  expect(dialog).toHaveTextContent('-€389,651.68 ?');
  await user.click(within(dialog).getByRole('button', { name: 'Yes' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/accounts/mortgage/loan/balance');
  expect(mutations[0].body).toMatchObject({ version: 3 });
  expect(mutations[0].body.balance).toBeUndefined();
  await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Update Balance' })).not.toBeInTheDocument());

  await user.click(screen.getByRole('button', { name: 'Update Balance' }));
  dialog = screen.getByRole('dialog', { name: 'Update Balance' });
  await user.click(within(dialog).getByRole('button', { name: 'No' }));
  const input = within(dialog).getByLabelText('Lender balance');
  await user.clear(input); await user.type(input, '-389000.10');
  await user.click(within(dialog).getByRole('button', { name: 'Save Balance' }));
  await waitFor(() => expect(mutations).toHaveLength(2));
  expect(mutations[1].body).toMatchObject({ balance: -389000.1 });
});

it('edits loan details, unpairs the category and preselects Add Activity as a payment transfer', async () => {
  const user = userEvent.setup();
  show();
  await user.click(await screen.findByRole('button', { name: 'Edit account' }));
  const dialog = screen.getByRole('dialog', { name: 'Edit Account' });
  expect(await within(dialog).findByLabelText('Loan Type')).toHaveValue('mortgage');
  expect(within(dialog).getByLabelText('Minimum Monthly Payment')).toHaveValue('1789.49');
  expect(within(dialog).getByLabelText('Interest Rate (%)')).toHaveValue('3.47');
  expect(dialog).toHaveTextContent('New transactions are unable to import into loan accounts.');
  expect(dialog).toHaveTextContent('Paired with “Mortgage (400K @ 3.47%)”');
  await user.type(within(dialog).getByLabelText('Monthly Escrow or Fee Amount'), '2000');
  await user.click(within(dialog).getByRole('button', { name: 'Save' }));
  expect(within(dialog).getByRole('alert')).toHaveTextContent('less than the monthly payment');
  expect(mutations).toHaveLength(0);
  await user.clear(within(dialog).getByLabelText('Monthly Escrow or Fee Amount')); await user.type(within(dialog).getByLabelText('Monthly Escrow or Fee Amount'), '150');
  await user.selectOptions(within(dialog).getByLabelText('Loan Type'), 'personalLoan');
  await user.click(within(dialog).getByRole('button', { name: 'Unpair Category' }));
  expect(within(dialog).getByRole('button', { name: 'Pair Category' })).toBeInTheDocument();
  await user.click(within(dialog).getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toMatchObject({ path: '/api/accounts/mortgage', method: 'PUT' });
  expect(mutations[0].body).toMatchObject({ accountType: 'personalLoan', workingBalance: -389651.68, loan: { escrowAmount: 150, interestRate: 3.47, pairedCategoryId: null, createPairedCategory: false } });

  await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Edit Account' })).not.toBeInTheDocument());
  await user.click(screen.getByRole('button', { name: 'Add Activity' }));
  const activity = screen.getByRole('dialog', { name: 'Add Activity' });
  expect(within(activity).getByLabelText('From account')).toHaveValue('checking');
  expect(within(activity).getByLabelText('To account')).toHaveValue('mortgage');
  await user.click(within(activity).getByRole('tab', { name: 'Interest, fee or correction' }));
  expect(within(activity).queryByText('Loading categories…')).not.toBeInTheDocument();
  expect(within(activity).getByLabelText('Account')).toHaveValue('mortgage');
});

it('shows the contract summary and schedule with the discount highlighted', async () => {
  show('phone');
  const page = await screen.findByRole('region', { name: 'Phone loan' });
  expect(page).toHaveTextContent('Remaining device cost€375.08');
  expect(page).toHaveTextContent('Remaining contract cost€300.00');
  expect(page).toHaveTextContent('Months remaining15');
  expect(page).toHaveTextContent('Total cost of contract€1,069.99');
  const schedule = within(page).getByRole('table', { name: 'Contract schedule' });
  expect(within(schedule).getAllByRole('row')[2]).toHaveClass('is-discounted');
  expect(within(schedule).getByText('Discount')).toBeInTheDocument();
  expect(within(page).queryByRole('button', { name: 'Try It Now' })).not.toBeInTheDocument();
  expect(page).toHaveTextContent('No category is paired');
});
