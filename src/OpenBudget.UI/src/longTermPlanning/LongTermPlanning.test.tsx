import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { LongTermPlanning } from './LongTermPlanning';
import { previewPlanningChange, type LongTermPlanning as PlanData } from './longTermPlanningMath';
import type { Entry } from '../ledger/api';

function planFor(year: number, hasPlanningData = true): PlanData {
  const months = Array.from({ length: 12 }, (_, index) => ({
    month: `${year}-${String(index + 1).padStart(2, '0')}`,
    expectedIncome: hasPlanningData ? 2000 : 0,
    hasExpectedIncome: hasPlanningData,
    expectedExpenses: 0,
    remaining: hasPlanningData ? 2000 : 0,
    remainingToBeAssigned: hasPlanningData ? 1400 : 0,
    hasActualData: false,
  }));
  const categoryMonths = months.map((month, index) => {
    const override = !hasPlanningData ? null : index === 0 ? 500 : index === 8 ? 700 : null;
    const budget = !hasPlanningData ? 0 : override ?? (index < 8 ? 500 : 700);
    const available = !hasPlanningData ? 0 : index < 8 ? (index + 1) * 500 : 4000 + (index - 7) * 700;
    return {
      month: month.month,
      plannedBudgetOverride: override,
      plannedBudgetBeforeMonth: !hasPlanningData || index === 0 ? 0 : index < 9 ? 500 : 700,
      plannedBudget: budget,
      plannedExpenses: 0,
      plannedAvailable: available,
      actualAssigned: index === 0 && hasPlanningData ? 300 : null,
      actualExpenses: index === 0 && hasPlanningData ? 45 : null,
      actualAvailable: index === 0 && hasPlanningData ? 255 : null,
    };
  });
  const transportMonths = months.map(month => ({
    month: month.month,
    plannedBudgetOverride: null,
    plannedBudgetBeforeMonth: hasPlanningData ? 100 : 0,
    plannedBudget: hasPlanningData ? 100 : 0,
    plannedExpenses: 0,
    plannedAvailable: hasPlanningData ? Number(month.month.slice(5, 7)) * 100 : 0,
    actualAssigned: null,
    actualExpenses: null,
    actualAvailable: null,
  }));
  return {
    year,
    version: 1,
    hasPlanningData,
    months,
    groups: [{
      id: 'daily', name: 'Everyday', sortOrder: 0, isArchived: false,
      categories: [
        { id: 'groceries', name: 'Groceries', sortOrder: 0, isArchived: false, months: categoryMonths },
        { id: 'transport', name: 'Transport', sortOrder: 1, isArchived: false, months: transportMonths },
      ],
    }],
  };
}

const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
const currentMonth = () => `${new Date().getFullYear()}-${String(new Date().getMonth() + 1).padStart(2, '0')}`;
const futureMonth = () => { const date = new Date(); date.setMonth(date.getMonth() + 1); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`; };
const monthName = (month: string) => new Intl.DateTimeFormat('en-GB', { month: 'long' }).format(new Date(`${month}-01T12:00:00`));
const annualGrid = (year: number) => screen.findByRole('table', { name: `Long-term plan ${year}` });
const input = (category: string, metric: string, month: string) => screen.getByRole('textbox', { name: `${category} ${metric} ${monthName(month)} ${month.slice(0, 4)}` });

let plans: Record<number, PlanData>;
let scheduledActivity: Entry[];
let actualActivity: Entry[];

beforeEach(() => {
  plans = {};
  scheduledActivity = [];
  actualActivity = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path.includes('/activity/')) return response({ scheduled: scheduledActivity });
    if (path.startsWith('/api/transactions?')) return response({ page: 1, pageSize: 100, total: actualActivity.length, items: actualActivity, accounts: { version: 1, accounts: [] } });
    const year = Number(path.split('/').at(-1));
    const current = plans[year] ?? planFor(year);
    if (init?.method === 'PUT') {
      const request = JSON.parse(String(init.body)) as { version: number; month: string; field: 'plannedBudgetOverride'; categoryId?: string; amount: number | null };
      if (request.version !== current.version) return response({ error: 'Changed elsewhere', current }, 409);
      const next = previewPlanningChange(current, request);
      next.version += 1;
      next.hasPlanningData = true;
      plans[year] = next;
      return response(next);
    }
    plans[year] = current;
    return response(current);
  }));
});

function activityEntry(id: string, amount: number, categoryId: string | null, year: number): Entry {
  return { id, accountId: 'main', date: `${year}-06-01`, amount, payee: id, memo: '', categoryId,
    status: 'uncleared', transferId: null, isOpening: false, splits: [], needsAttention: false,
    runningBalance: null, transferAccountName: null, categoryName: null, isLocked: false };
}

it('opens forecast and actual expense activity and passes selected transactions to account navigation', async () => {
  const user = userEvent.setup();
  const year = new Date().getFullYear() + 1;
  scheduledActivity = [activityEntry('Salary', 2000, null, year), activityEntry('Grocer', -25, 'groceries', year)];
  actualActivity = [activityEntry('Refund', 5, 'groceries', year), activityEntry('Market', -10, 'groceries', year)];
  const selectTransaction = vi.fn();
  render(<LongTermPlanning accounts={[{ id: 'main', name: 'Main' }]} onSelectTransaction={selectTransaction} />);
  await annualGrid(new Date().getFullYear());
  await user.click(screen.getByRole('button', { name: 'Next year' }));
  await annualGrid(year);

  await user.click(screen.getByRole('button', { name: `View expected income in June ${year}` }));
  expect(await screen.findByRole('button', { name: 'Open Salary in Main account' })).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Open Grocer in Main account' })).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Close transactions' }));

  await user.click(screen.getByRole('button', { name: `View expected expenses in June ${year}` }));
  expect(await screen.findByRole('button', { name: 'Open Grocer in Main account' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Close transactions' }));

  await user.click(screen.getByRole('button', { name: `View Everyday planned expenses in June ${year}` }));
  expect(await screen.findByRole('button', { name: 'Open Grocer in Main account' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Close transactions' }));

  await user.click(screen.getByRole('button', { name: `View Groceries planned expenses in June ${year}` }));
  await user.click(await screen.findByRole('button', { name: 'Open Grocer in Main account' }));
  expect(selectTransaction).toHaveBeenCalledWith(scheduledActivity[1], true, undefined);

  await user.click(screen.getByRole('button', { name: `View Groceries actual expenses in June ${year}` }));
  expect(await screen.findByRole('button', { name: 'Open Refund in Main account' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Open Market in Main account' }));
  expect(selectTransaction).toHaveBeenCalledWith(actualActivity[1], false, undefined);
});

it('defaults to the current year and uses distinct past, current, and future month states', async () => {
  const user = userEvent.setup();
  const year = new Date().getFullYear();
  const now = currentMonth();
  const future = futureMonth();
  const pastDate = new Date(`${now}-01T12:00:00`);
  pastDate.setMonth(pastDate.getMonth() - 1);
  const past = `${pastDate.getFullYear()}-${String(pastDate.getMonth() + 1).padStart(2, '0')}`;
  const { container } = render(<LongTermPlanning />);
  const grid = await annualGrid(year);

  expect(grid).toBeInTheDocument();
  expect(screen.queryByRole('textbox', { name: `Groceries Assigned ${monthName(past)} ${year}` })).not.toBeInTheDocument();
  expect(screen.queryByRole('textbox', { name: `Expected Income ${monthName(past)} ${year}` })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: `Groceries Assigned ${monthName(now)} ${year}` })).toBeEnabled();
  expect(screen.getByRole('button', { name: `Groceries Assigned ${monthName(future)} ${year}` })).toBeEnabled();
  const groceriesRow = Array.from(grid.querySelectorAll('.lt-category-row')).find(row => row.textContent?.includes('Groceries'))!;
  const januaryCell = groceriesRow.querySelector('.lt-month-values')!;
  expect(Array.from(groceriesRow.querySelectorAll('.lt-pair-labels span'), label => label.textContent)).toEqual(['Planned', 'Actual']);
  expect(Array.from(januaryCell.querySelectorAll('.lt-subrow'), row => row.getAttribute('data-subrow'))).toEqual(['planned', 'actual']);
  expect(januaryCell.querySelector('[data-subrow="planned"]')).toHaveTextContent('500.00');
  expect(januaryCell.querySelector('[data-subrow="actual"]')).toHaveTextContent('300.00');
  await user.click(screen.getByRole('button', { name: `Groceries Assigned ${monthName(now)} ${year}` }));
  const assignedInput = input('Groceries', 'Assigned', now) as HTMLInputElement;
  await new Promise(resolve => requestAnimationFrame(() => resolve(undefined)));
  expect(assignedInput.selectionStart).toBe(0);
  expect(assignedInput.selectionEnd).toBe(assignedInput.value.length);
  const monthHeaders = container.querySelectorAll('.lt-month-header');
  expect(Array.from(monthHeaders).find(header => header.classList.contains('past'))).toBeDefined();
  expect(Array.from(monthHeaders).find(header => header.classList.contains('current'))).toBeDefined();
  expect(Array.from(monthHeaders).find(header => header.classList.contains('future'))).toBeDefined();
  await userEvent.click(screen.getByRole('button', { name: 'Previous year' }));
  await annualGrid(year - 1);
  expect(screen.queryByRole('textbox', { name: `Groceries Assigned January ${year - 1}` })).not.toBeInTheDocument();
  expect(screen.queryByRole('textbox', { name: `Expected Income January ${year - 1}` })).not.toBeInTheDocument();
});

it('colors negative Available red and zero-or-positive Available green for planned and actual values', async () => {
  const year = new Date().getFullYear();
  const plan = planFor(year);
  plan.groups[0].categories[0].months[0].plannedAvailable = -25;
  plan.groups[0].categories[0].months[0].actualAvailable = -5;
  plan.groups[0].categories[1].months[0].plannedAvailable = 0;
  plan.groups[0].categories[1].months[0].actualAvailable = 0;
  plans[year] = plan;
  const { container } = render(<LongTermPlanning />);
  await annualGrid(year);

  const categoryRows = Array.from(container.querySelectorAll('.lt-category-row'));
  const groceries = categoryRows.find(row => row.textContent?.includes('Groceries'))!;
  const transport = categoryRows.find(row => row.textContent?.includes('Transport'))!;
  const groceriesJanuary = groceries.querySelector('.lt-month-values')!;
  const transportJanuary = transport.querySelector('.lt-month-values')!;
  const availableLine = (cell: Element, kind: 'planned' | 'actual') => cell.querySelector(`[data-subrow="${kind}"] .lt-value-line:nth-child(3)`);

  expect(availableLine(groceriesJanuary, 'planned')).toHaveClass('lt-available-negative');
  expect(availableLine(groceriesJanuary, 'actual')).toHaveClass('lt-available-negative');
  expect(availableLine(transportJanuary, 'planned')).toHaveClass('lt-available-positive');
  expect(availableLine(transportJanuary, 'actual')).toHaveClass('lt-available-positive');
  expect(availableLine(container.querySelector('.lt-group-row')!, 'planned')).toHaveClass('lt-available-negative');
});

it('colors planned expenses orange only when they are above zero', async () => {
  const year = new Date().getFullYear();
  const plan = planFor(year);
  plan.groups[0].categories[0].months[0].plannedExpenses = 80;
  plans[year] = plan;
  const { container } = render(<LongTermPlanning />);
  await annualGrid(year);

  const categoryRows = Array.from(container.querySelectorAll('.lt-category-row'));
  const groceries = categoryRows.find(row => row.textContent?.includes('Groceries'))!;
  const transport = categoryRows.find(row => row.textContent?.includes('Transport'))!;
  const expenseLine = (cell: Element) => cell.querySelector('[data-subrow="planned"] .lt-value-line:nth-child(2)');

  expect(expenseLine(groceries.querySelector('.lt-month-values')!)).toHaveClass('lt-planned-expense-positive');
  expect(expenseLine(groceries.querySelectorAll('.lt-month-values')[1])).not.toHaveClass('lt-planned-expense-positive');
  expect(expenseLine(transport.querySelector('.lt-month-values')!)).not.toHaveClass('lt-planned-expense-positive');
  expect(expenseLine(container.querySelector('.lt-group-row .lt-month-values')!)).toHaveClass('lt-planned-expense-positive');
});

it('navigates years, previews remaining to be assigned, and preserves later overrides', async () => {
  const user = userEvent.setup();
  render(<LongTermPlanning />);
  await annualGrid(new Date().getFullYear());
  await user.click(screen.getByRole('button', { name: 'Next year' }));
  const year = new Date().getFullYear() + 1;
  await annualGrid(year);

  const june = `${year}-06`;
  await user.click(screen.getByRole('button', { name: `Groceries Assigned ${monthName(june)} ${year}` }));
  const budgetInput = input('Groceries', 'Assigned', june);
  await user.clear(budgetInput);
  await user.type(budgetInput, '650');
  const juneCell = budgetInput.closest('.lt-month-values')!;
  expect(juneCell).toHaveTextContent('€3,150.00');
  const juneHeader = budgetInput.closest('.lt-grid')!.querySelectorAll('.lt-month-header')[5];
  expect(juneHeader).toHaveTextContent('Remaining to Be Assigned');
  expect(juneHeader).toHaveTextContent('€1,250.00');
  await user.keyboard('{Enter}');
  expect(await screen.findByText('Planning changes saved.')).toBeInTheDocument();
  expect(screen.getByText('Planning changes saved.').closest('.lt-save-state')).toHaveClass('sr-only');
  expect(screen.getByRole('button', { name: `Groceries Assigned August ${year}` })).toHaveTextContent('€650.00');
  expect(screen.getByRole('button', { name: `Groceries Assigned September ${year}` })).toHaveTextContent('€700.00');
  expect(juneHeader).toHaveTextContent('€1,250.00');
  expect(screen.queryByRole('button', { name: `Remove Groceries override for June ${year}` })).not.toBeInTheDocument();

  expect(screen.queryByRole('textbox', { name: `Groceries Planned Expenses June ${year}` })).not.toBeInTheDocument();
  expect(juneHeader).toHaveTextContent('Expected Expenses');
  expect(juneHeader).toHaveTextContent('€0.00');
});

it('keeps group totals for collapsed categories and aggregates every subcategory', async () => {
  const user = userEvent.setup();
  const year = new Date().getFullYear() + 1;
  const { container } = render(<LongTermPlanning />);
  await annualGrid(new Date().getFullYear());
  await user.click(screen.getByRole('button', { name: 'Next year' }));
  await annualGrid(year);

  const groupRow = container.querySelector('.lt-group-row')!;
  const januaryGroupCell = groupRow.querySelectorAll('.lt-month-values')[0];
  expect(januaryGroupCell).toHaveTextContent('€600.00');
  expect(groupRow.querySelector('svg.disclosure-chevron')).toHaveClass('open');
  await user.click(screen.getByRole('button', { name: 'Collapse Everyday' }));
  expect(groupRow.querySelector('svg.disclosure-chevron')).not.toHaveClass('open');
  expect(container.querySelector('.lt-category-row')).toHaveClass('lt-row-exit');
  await waitFor(() => expect(container.querySelector('.lt-category-row')).not.toBeInTheDocument());
  expect(groupRow.querySelectorAll('.lt-month-values')[0]).toHaveTextContent('€600.00');

  await user.click(screen.getByRole('button', { name: 'Expand Everyday' }));
  const expandedRows = container.querySelectorAll('.lt-category-row');
  expect(expandedRows[0]).toHaveClass('lt-row-enter');
  expect((expandedRows[1] as HTMLElement).style.getPropertyValue('--row-delay')).toBe('25ms');
  await waitFor(() => expect(container.querySelector('.lt-category-row')).not.toHaveClass('lt-row-enter'));
});

it('shows loading failures and offers a retry instead of showing sample forecasts', async () => {
  const fetchMock = vi.mocked(fetch);
  plans[new Date().getFullYear()] = planFor(new Date().getFullYear(), false);
  fetchMock.mockRejectedValueOnce(new Error('offline'));
  render(<LongTermPlanning />);
  expect(await screen.findByRole('alert')).toHaveTextContent('offline');
  await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
  expect(await annualGrid(new Date().getFullYear())).toBeInTheDocument();
  expect(screen.getByText(/No planning values saved yet/)).toBeInTheDocument();
});
