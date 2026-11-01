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
let pairings: { accountId: string; accountName: string; categoryId: string; slot: string }[];
const cat = (id: string, name: string, sortOrder: number, isArchived = false) => ({ id, name, sortOrder, isArchived, available: 0 });
// Plan as the pairing step sees it: protected (Credit Card Payments, Wishlist) and archived rows must be hidden.
const planGroups = [
  { id: 'credit-card-payments', name: 'Credit Card Payments', sortOrder: 0, isArchived: false, categories: [cat('ccp-visa', 'Visa', 0)] },
  { id: 'must', name: 'Must Pay', sortOrder: 2, isArchived: false, categories: [cat('vve', 'VvE', 0), cat('mortgage-a', 'Mortgage (400K @ 3.47%)', 1), cat('old', 'Old bill', 2, true)] },
  { id: 'tax', name: 'Tax', sortOrder: 1, isArchived: false, categories: [cat('income-tax', 'Income Tax', 0), cat('ozb', 'OZB', 1)] },
  { id: 'wishlist', name: 'Wishlist', sortOrder: 3, isArchived: false, categories: [cat('wl-1', 'Camera', 0)] },
  { id: 'gone', name: 'Archived group', sortOrder: 4, isArchived: true, categories: [cat('gone-1', 'Gone', 0)] },
];
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  try { window.localStorage.clear(); } catch { /* ignore */ }
  mutations = []; simulations = [];
  pairings = [{ accountId: 'other-loan', accountName: 'Mortgage A', categoryId: 'mortgage-a', slot: 'loan' }];
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
    if (path === '/api/loan-pairings') return response({ items: pairings });
    if (path.startsWith('/api/budget/')) return response({ readyToAssign: 0, groups: planGroups });
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
  // Next opens the optional pairing step; Skip creates the account unpaired.
  const step = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
  expect(mutations).toHaveLength(0);
  await user.click(within(step).getByRole('button', { name: 'Skip' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].path).toBe('/api/accounts');
  expect(mutations[0].body).toMatchObject({ name: 'Car', accountType: 'autoLoan', openingBalance: -12000, loan: { interestRate: 4.9, minimumPayment: 275, contract: null,
    pairedCategoryId: null, newPairedCategory: null, separateDeviceCategory: false, devicePairedCategoryId: null, newDevicePairedCategory: null } });
});

async function openPairStep(user: ReturnType<typeof userEvent.setup>, name = 'Car') {
  await user.click(await screen.findByRole('button', { name: 'Add account' }));
  await user.click(within(screen.getByRole('region', { name: 'Mortgages and Loans' })).getByRole('button', { name: 'Auto Loan' }));
  const form = screen.getByRole('dialog', { name: 'Add Unlinked Account' });
  await user.type(within(form).getByLabelText('Give it a nickname'), name);
  await user.type(within(form).getByLabelText('Current account balance'), '12000');
  await user.type(within(form).getByLabelText('Interest rate'), '4.9');
  await user.type(within(form).getByLabelText('Monthly payment required by your lender'), '275');
  await user.click(within(form).getByRole('button', { name: 'Next' }));
  return screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
}

it('opens the pairing step after Next, keeps entered values on back and pairs an existing grouped category', async () => {
  const user = userEvent.setup();
  show('');
  let step = await openPairStep(user);
  expect(step).toHaveTextContent('Pair your loan with a plan category');
  expect(step).toHaveTextContent('Pairing works best when there is a one-to-one relationship between your loan account and a plan category.');
  expect(within(step).getByRole('group', { name: 'Which category do you want to pair your loan with?' })).toBeInTheDocument();
  expect(within(step).getByRole('radio', { name: /Select an existing category/ })).toBeChecked();
  expect(within(step).getByRole('radio', { name: /Create a new category/ })).not.toBeChecked();
  expect(within(step).getByRole('button', { name: 'Close dialog' })).toBeInTheDocument();
  const next = within(step).getByRole('button', { name: 'Next' });
  expect(next).toBeDisabled();

  // Back returns to the form with every value kept, and Next returns to the step.
  await user.click(within(step).getByRole('button', { name: 'Back' }));
  const form = screen.getByRole('dialog', { name: 'Add Unlinked Account' });
  expect(within(form).getByLabelText('Give it a nickname')).toHaveValue('Car');
  expect(within(form).getByLabelText('Interest rate')).toHaveValue('4.9');
  expect(within(form).getByLabelText('Monthly payment required by your lender')).toHaveValue('275');
  await user.click(within(form).getByRole('button', { name: 'Next' }));
  step = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });

  // Grouped in plan order; protected and archived rows hidden; another loan's category is disabled.
  const select = await within(step).findByLabelText('Select a category');
  const groups = within(select).getAllByRole('group');
  expect(groups.map(group => group.getAttribute('label'))).toEqual(['Tax', 'Must Pay']);
  expect(within(groups[1]).getAllByRole('option').map(option => option.textContent)).toEqual(['VvE', 'Mortgage (400K @ 3.47%)']);
  expect(within(select).getByRole('option', { name: 'Mortgage (400K @ 3.47%)' })).toBeDisabled();
  expect(within(select).getByRole('option', { name: 'Mortgage (400K @ 3.47%)' })).toHaveAttribute('title', 'Already paired with Mortgage A');
  expect(within(select).queryByRole('option', { name: 'Visa' })).not.toBeInTheDocument();
  expect(within(select).queryByRole('option', { name: 'Camera' })).not.toBeInTheDocument();
  expect(within(select).queryByRole('option', { name: 'Old bill' })).not.toBeInTheDocument();
  expect(within(select).queryByRole('option', { name: 'Gone' })).not.toBeInTheDocument();
  await user.selectOptions(select, 'ozb');
  expect(within(step).getByRole('button', { name: 'Next' })).toBeEnabled();
  await user.click(within(step).getByRole('button', { name: 'Next' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ name: 'Car', loan: { pairedCategoryId: 'ozb', newPairedCategory: null, separateDeviceCategory: false } });
});

it('creates a new category in an existing or new category group from the pairing step', async () => {
  const user = userEvent.setup();
  show('');
  const step = await openPairStep(user, 'Tesla');
  await user.click(within(step).getByRole('radio', { name: /Create a new category/ }));
  expect(within(step).queryByLabelText('Select a category')).not.toBeInTheDocument();
  const name = within(step).getByLabelText('Give your category a name');
  expect(name).toHaveValue('Tesla');
  const group = within(step).getByLabelText('Add to category group');
  expect(within(group).getAllByRole('option').filter(option => option.textContent).map(option => option.textContent)).toEqual(['New Category Group', 'Tax', 'Must Pay']);
  expect(within(step).getByRole('button', { name: 'Next' })).toBeDisabled();

  // Duplicate names in the chosen group are caught before saving.
  await user.selectOptions(group, 'tax');
  await user.clear(name); await user.type(name, 'ozb');
  expect(within(step).getByRole('alert')).toHaveTextContent('already has a category with that name');
  expect(within(step).getByRole('button', { name: 'Next' })).toBeDisabled();
  await user.clear(name); await user.type(name, 'Car loan');

  // New Category Group reveals the group name input.
  await user.selectOptions(group, 'New Category Group');
  const groupName = within(step).getByLabelText('Category group name');
  expect(within(step).getByRole('button', { name: 'Next' })).toBeDisabled();
  await user.type(groupName, 'tax');
  expect(within(step).getByRole('alert')).toHaveTextContent('category group with that name already exists');
  await user.clear(groupName); await user.type(groupName, 'Debts');
  // Switching back to "existing" and again to "new" keeps the typed values.
  await user.click(within(step).getByRole('radio', { name: /Select an existing category/ }));
  await user.click(within(step).getByRole('radio', { name: /Create a new category/ }));
  expect(within(step).getByLabelText('Category group name')).toHaveValue('Debts');
  await user.click(within(step).getByRole('button', { name: 'Next' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ name: 'Tesla', loan: { pairedCategoryId: null, newPairedCategory: { name: 'Car loan', groupId: null, newGroupName: 'Debts' } } });
});

it('keeps the pairing step open with the server error when creation fails', async () => {
  const user = userEvent.setup();
  show('');
  const fetchMock = vi.mocked(fetch);
  const original = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation(async (path, init) => init?.method === 'POST'
    ? response({ error: 'That category is already paired with another loan. A category can be paired with only one loan.' }, 400) : original(path, init));
  const step = await openPairStep(user);
  await user.selectOptions(await within(step).findByLabelText('Select a category'), 'vve');
  await user.click(within(step).getByRole('button', { name: 'Next' }));
  expect(await screen.findByText(/already paired with another loan/)).toBeInTheDocument();
  expect(screen.getByRole('dialog', { name: 'Optional: Pair a Category' })).toBeInTheDocument();
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

  // Contracts with a device: same category (default) or separate contract and device categories.
  const step = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
  expect(step).toHaveTextContent('Pair your contract with a plan category');
  expect(within(step).getByRole('radio', { name: /Use the same category for the device and contract/ })).toBeChecked();
  await user.click(within(step).getByRole('radio', { name: /Use separate categories/ }));
  const contractSection = within(step).getByRole('region', { name: 'Contract category' });
  const deviceSection = within(step).getByRole('region', { name: 'Device category' });
  await user.selectOptions(await within(contractSection).findByLabelText('Contract: Select a category'), 'vve');
  expect(within(step).getByRole('button', { name: 'Next' })).toBeDisabled();
  // The same category cannot cover both slots.
  await user.selectOptions(within(deviceSection).getByLabelText('Device: Select a category'), 'vve');
  expect(within(step).getByRole('alert')).toHaveTextContent('Choose different categories');
  await user.click(within(deviceSection).getByRole('radio', { name: /Create a new category/ }));
  expect(within(deviceSection).getByLabelText('Device: Give your category a name')).toHaveValue('Phone Device');
  await user.selectOptions(within(deviceSection).getByLabelText('Device: Add to category group'), 'must');
  await user.click(within(step).getByRole('button', { name: 'Next' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ accountType: 'contract', openingBalance: 0, loan: { interestRate: 0,
    contract: { months: 12, startDate: '2026-10-01', includesDevice: true, monthlyServiceCost: 20, deviceTotal: 600, discountMonths: 3, discountedServiceCost: 10 },
    pairedCategoryId: 'vve', newPairedCategory: null, separateDeviceCategory: true, devicePairedCategoryId: null, newDevicePairedCategory: { name: 'Phone Device', groupId: 'must', newGroupName: null } } });
});

it('offers the device choice only for contracts that include a device', async () => {
  const user = userEvent.setup();
  show('');
  await user.click(await screen.findByRole('button', { name: 'Add account' }));
  await user.click(within(screen.getByRole('region', { name: 'Mortgages and Loans' })).getByRole('button', { name: 'Contract' }));
  const form = screen.getByRole('dialog', { name: 'Add Unlinked Account' });
  await user.type(within(form).getByLabelText('Give it a nickname'), 'SIM only');
  await user.selectOptions(within(form).getByLabelText('Contract length'), '12');
  await user.click(within(form).getByLabelText('Includes a device'));
  await user.type(within(form).getByLabelText('Monthly contract cost'), '15');
  await user.click(within(form).getByRole('button', { name: 'Next' }));
  const step = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
  expect(within(step).queryByRole('radio', { name: /Use the same category/ })).not.toBeInTheDocument();
  await user.click(within(step).getByRole('button', { name: 'Skip' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ loan: { contract: { includesDevice: false }, pairedCategoryId: null, separateDeviceCategory: false } });
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

const openMonth = (month: string) => ({ month, endBalance: -4804.17, estimatedInterest: 4.17, payments: [] as LoanDetails['activity'][number]['payments'], other: [] as LoanDetails['activity'][number]['other'] });

it('shows a single payment inline, groups several payments by month and opens the matching edit dialogs', async () => {
  const user = userEvent.setup();
  loan.activity = [{ ...openMonth('2026-11'),
    payments: [{ id: 'p1', date: '2026-11-01', amount: 200, kind: 'payment', description: 'Rent', accountName: 'ING', accountId: 'checking' }],
    other: [{ id: 'o1', date: '2026-11-01', amount: -5000, kind: 'opening', description: 'Initial Balance', accountName: null }] }];
  show();
  const activity = within(await screen.findByRole('region', { name: 'Account Activity' }));
  expect(activity.getByText('Payments')).toBeInTheDocument();
  expect(activity.getByText('Other Activity')).toBeInTheDocument();
  expect(activity.getByText(/-€5,000.00 Initial Balance on 1 November/)).toBeInTheDocument();
  expect(activity.getByText('Estimated')).toBeInTheDocument();

  await user.click(activity.getByRole('button', { name: 'Edit charge' }));
  let dialog = within(screen.getByRole('dialog', { name: 'Edit Charge' }));
  expect(dialog.getByLabelText('Amount')).toHaveValue('5000.00');
  await user.clear(dialog.getByLabelText('Amount')); await user.type(dialog.getByLabelText('Amount'), '4900');
  await user.click(dialog.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toMatchObject({ path: '/api/transactions/o1', method: 'PUT', body: { accountId: 'mortgage', amount: -4900, date: '2026-11-01' } });
});

it('records, overrides and deletes the monthly interest, and edits or deletes a payment in both accounts', async () => {
  const user = userEvent.setup();
  loan.activity = [{ ...openMonth('2026-11'), payments: [
    { id: 'p1', date: '2026-11-01', amount: 200, kind: 'payment', description: '', accountName: 'ING', accountId: 'checking' },
    { id: 'p2', date: '2026-11-01', amount: 200, kind: 'payment', description: '', accountName: 'ING', accountId: 'checking' }] }];
  show();
  const activity = within(await screen.findByRole('region', { name: 'Account Activity' }));
  expect(activity.getByText('From ING on 1 November')).toBeInTheDocument();
  expect(activity.getByText('€400.00')).toBeInTheDocument();
  expect(activity.queryByLabelText(/Edit payment of/)).not.toBeInTheDocument();
  await user.click(activity.getByRole('button', { name: 'Payments' }));
  await user.click(activity.getAllByRole('button', { name: /Edit payment of €200.00/ })[0]);
  let dialog = within(screen.getByRole('dialog', { name: 'Edit Payment' }));
  expect(dialog.getByLabelText('Payment Amount')).toHaveValue('200.00');
  expect(dialog.getByLabelText('From Account')).toHaveValue('checking');
  await user.click(dialog.getByRole('button', { name: 'Delete Payment' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toMatchObject({ path: '/api/transactions/p1/delete', method: 'POST' });

  await user.click(activity.getByRole('button', { name: 'Edit interest' }));
  dialog = within(screen.getByRole('dialog', { name: 'Edit Interest' }));
  expect(dialog.getByLabelText('Interest Amount')).toHaveValue('4.17');
  await user.clear(dialog.getByLabelText('Interest Amount')); await user.type(dialog.getByLabelText('Interest Amount'), '5.25');
  await user.click(dialog.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(mutations).toHaveLength(2));
  expect(mutations[1]).toMatchObject({ path: '/api/accounts/mortgage/loan/interest', method: 'POST', body: { month: '2026-11', amount: 5.25 } });

  await user.click(activity.getByRole('button', { name: 'Edit interest' }));
  await user.click(within(screen.getByRole('dialog', { name: 'Edit Interest' })).getByRole('button', { name: 'Delete Interest' }));
  await waitFor(() => expect(mutations).toHaveLength(3));
  expect(mutations[2]).toMatchObject({ path: '/api/accounts/mortgage/loan/interest', body: { month: '2026-11', amount: 0 } });
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
  expect(dialog).toHaveTextContent('Paying +€464.34 extra every month');
  await user.clear(within(dialog).getByLabelText('Monthly Payment')); await user.type(within(dialog).getByLabelText('Monthly Payment'), '1000');
  expect(await within(dialog).findByText('This payment never pays off the loan.')).toBeInTheDocument();
  await user.type(within(dialog).getByLabelText('One time extra payment'), '500');
  await waitFor(() => expect(simulations.some(path => path.includes('extra=500'))).toBe(true));
  await user.click(within(dialog).getByRole('button', { name: 'Done' }));
  expect(mutations).toHaveLength(0);
});

it('styles the simulator chart and inputs like the reference: smooth curves, dotted grid, dark inputs, savings and reset', async () => {
  const user = userEvent.setup();
  const onOpenCategory = show();
  await user.click(await screen.findByRole('button', { name: 'Try It Now' }));
  const dialog = screen.getByRole('dialog', { name: 'Loan Payoff Simulator' });
  await within(dialog).findByText('Paying the minimum every month');
  const chart = dialog.querySelector('.loan-chart') as HTMLElement;
  expect(chart.querySelector('.loan-chart-past')?.getAttribute('d')).toContain('C');
  expect(chart.querySelector('.loan-chart-track')?.getAttribute('d')).toContain('C');
  const grid = chart.querySelectorAll('line.loan-chart-grid');
  expect(grid.length).toBeGreaterThan(2);
  grid.forEach(line => expect(line.getAttribute('class')).toContain('loan-chart-grid'));
  expect(chart.querySelectorAll('.loan-chart-axis').length).toBeGreaterThan(grid.length);
  expect(chart.querySelector('.loan-chart-sim')).toBeNull();
  expect(within(dialog).queryByRole('button', { name: /Reset all changes/ })).not.toBeInTheDocument();
  const payment = within(dialog).getByLabelText('Monthly Payment');
  expect(payment).toHaveClass('loan-sim-input');
  expect(within(dialog).getByLabelText('One time extra payment')).toHaveClass('loan-sim-input');
  expect(within(dialog).getByRole('button', { name: 'Payoff Date' })).toHaveClass('month-year-trigger');
  await user.type(within(dialog).getByLabelText('One time extra payment'), '500');
  await waitFor(() => expect(dialog.querySelector('.loan-chart-sim')).not.toBeNull());
  expect(dialog.querySelector('.loan-chart-sim')?.getAttribute('d')).toContain('C');
  expect(dialog.querySelector('.loan-chart-legend')).toHaveTextContent('Simulation');
  expect(dialog).toHaveTextContent('Paying €500.00 extra in Oct');
  expect(dialog).toHaveTextContent('Interest Savings');
  expect(dialog).toHaveTextContent('Time Savings');
  expect(dialog).toHaveTextContent('Your original payoff date is Jun 2055');
  await user.click(within(dialog).getByRole('button', { name: /Reset all changes/ }));
  await waitFor(() => expect(within(dialog).getByLabelText('Monthly Payment')).toHaveValue('1789.49'));
  expect(await within(dialog).findByText('Paying the minimum every month')).toBeInTheDocument();
  expect(dialog.querySelector('.loan-chart-sim')).toBeNull();
  await user.click(within(dialog).getByRole('button', { name: 'Copy and go to my plan' }));
  expect(onOpenCategory).toHaveBeenCalledWith('loan-mortgage', true);
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
  expect(mutations[0].body).toMatchObject({ accountType: 'personalLoan', workingBalance: -389651.68, loan: { escrowAmount: 150, interestRate: 3.47, pairedCategoryId: null, newPairedCategory: null } });

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

it('pairs an unpaired loan from Edit Account through the same step, then saves with the account update', async () => {
  const user = userEvent.setup();
  loan = { ...loan, pairedCategory: null };
  pairings = [...pairings, { accountId: 'mortgage', accountName: 'Mortgage (400K @ 3.47%)', categoryId: 'income-tax', slot: 'loan' }];
  show();
  await user.click(await screen.findByRole('button', { name: 'Edit account' }));
  let dialog = screen.getByRole('dialog', { name: 'Edit Account' });
  expect(await within(dialog).findByText(/Not paired/)).toBeInTheDocument();
  await user.type(within(dialog).getByLabelText('Account Notes'), 'Fixed rate');
  await user.click(within(dialog).getByRole('button', { name: 'Pair Category' }));
  const step = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
  expect(within(step).getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
  const select = await within(step).findByLabelText('Select a category');
  // The loan's own pairing is not "taken"; another loan's is.
  expect(within(select).getByRole('option', { name: 'Income Tax' })).toBeEnabled();
  expect(within(select).getByRole('option', { name: 'Mortgage (400K @ 3.47%)' })).toBeDisabled();

  // Back returns to Edit Account without changes and keeps edited fields.
  await user.click(within(step).getByRole('button', { name: 'Back' }));
  dialog = screen.getByRole('dialog', { name: 'Edit Account' });
  expect(within(dialog).getByLabelText('Account Notes')).toHaveValue('Fixed rate');
  expect(mutations).toHaveLength(0);

  await user.click(within(dialog).getByRole('button', { name: 'Pair Category' }));
  const again = await screen.findByRole('dialog', { name: 'Optional: Pair a Category' });
  await user.click(within(again).getByRole('radio', { name: /Create a new category/ }));
  expect(within(again).getByLabelText('Give your category a name')).toHaveValue('Mortgage (400K @ 3.47%)');
  await user.selectOptions(within(again).getByLabelText('Add to category group'), 'tax');
  await user.click(within(again).getByRole('button', { name: 'Next' }));
  dialog = screen.getByRole('dialog', { name: 'Edit Account' });
  expect(dialog).toHaveTextContent('Paired with “Mortgage (400K @ 3.47%)” (new)');
  expect(mutations).toHaveLength(0);
  await user.click(within(dialog).getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toMatchObject({ path: '/api/accounts/mortgage', method: 'PUT' });
  expect(mutations[0].body).toMatchObject({ notes: 'Fixed rate', loan: { pairedCategoryId: null, newPairedCategory: { name: 'Mortgage (400K @ 3.47%)', groupId: 'tax', newGroupName: null } } });
});

it('shows and edits separate contract and device categories', async () => {
  const user = userEvent.setup();
  const category = (id: string, name: string, available: number) => ({ id, name, groupName: 'Bills', available, isArchived: false, isAutomatic: false, targetKind: null, targetMoreNeeded: null });
  const fetchMock = vi.mocked(fetch);
  const original = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation(async (path, init) => {
    const result = await original(path, init);
    if (path !== '/api/accounts/phone/loan') return result;
    const body = await result.json();
    return response({ ...body, separateDeviceCategory: true, pairedCategory: category('vve', 'Phone plan', 20), devicePairedCategory: category('ozb', 'Phone device', -5) });
  });
  const onCreateTarget = show('phone');
  const page = await screen.findByRole('region', { name: 'Phone loan' });
  const contractRow = within(page).getByRole('region', { name: 'Contract category' });
  const deviceRow = within(page).getByRole('region', { name: 'Device category' });
  expect(contractRow).toHaveTextContent('Bills: Phone plan');
  expect(contractRow).toHaveTextContent('Available to spend€20.00');
  expect(deviceRow).toHaveTextContent('Bills: Phone device');
  expect(deviceRow).toHaveTextContent('-€5.00');
  await user.click(within(deviceRow).getByRole('button', { name: /Create Debt Payment Target/ }));
  expect(onCreateTarget).toHaveBeenCalledWith('ozb', true);

  await user.click(screen.getByRole('button', { name: 'Edit account' }));
  const dialog = screen.getByRole('dialog', { name: 'Edit Account' });
  expect(await within(dialog).findByText('“Phone plan”')).toBeInTheDocument();
  expect(dialog).toHaveTextContent('Device:“Phone device”');
  await user.click(within(dialog).getByRole('button', { name: 'Unpair device category' }));
  expect(dialog).toHaveTextContent('Device:Not paired');
  await user.click(within(dialog).getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0].body).toMatchObject({ loan: { pairedCategoryId: 'vve', separateDeviceCategory: true, devicePairedCategoryId: null, newDevicePairedCategory: null } });
});
