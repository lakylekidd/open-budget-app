import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Entry } from './ledger/api';
import { expandActivityEntries, PlanTransactionList, type CreditActivitySummary } from './PlanTransactionList';

const entry = (overrides: Partial<Entry> = {}): Entry => ({
  id: 'e1', accountId: 'a1', date: '2026-10-30', amount: -112.01, payee: '🛒 Market', memo: 'Weekly shop', categoryId: 'groceries', status: 'cleared', transferId: null,
  isOpening: false, splits: [], needsAttention: false, runningBalance: null, transferAccountName: null, categoryName: 'Groceries', isLocked: false, ...overrides
});
const accounts = [{ id: 'a1', name: '💶 Checking' }, { id: 'card', name: 'ICS Mastercard' }];
const summary: CreditActivitySummary = { spending: -1577.5, returns: 0, totalSpending: -1577.5, fundedSpending: 1577.5, paymentsAndReturns: -1525.52, totalActivity: 51.98 };

function setup(props: Partial<Parameters<typeof PlanTransactionList>[0]> = {}) {
  const onClose = vi.fn(), onSelect = vi.fn(), onRetry = vi.fn();
  const trigger = document.createElement('button');
  trigger.textContent = '€0.00';
  document.body.append(trigger);
  const view = render(<PlanTransactionList subtitle="Groceries" items={[{ entry: entry(), scheduled: false }]} accounts={accounts} anchor={trigger} onClose={onClose} onSelect={onSelect} onRetry={onRetry} {...props} />);
  return { onClose, onSelect, onRetry, trigger, ...view };
}

describe('PlanTransactionList standard variant', () => {
  it('shows the Activity title, context subtitle and the ACCOUNT DATE PAYEE MEMO AMOUNT columns', () => {
    setup();
    const dialog = screen.getByRole('dialog', { name: 'Activity' });
    expect(within(dialog).getByRole('heading', { name: 'Activity' })).toBeInTheDocument();
    expect(within(dialog).getByText('Groceries')).toBeInTheDocument();
    const headers = Array.from(dialog.querySelectorAll('.activity-head > span')).map(node => node.textContent);
    expect(headers).toEqual(['ACCOUNT', 'DATE', 'PAYEE', 'MEMO', 'AMOUNT']);
    expect(within(dialog).queryByText('CATEGORY')).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('group', { name: 'Card activity summary' })).not.toBeInTheDocument();
  });

  it('formats account (with emoji), date, payee, memo and bold amount', () => {
    setup();
    const row = screen.getByRole('button', { name: 'Open 🛒 Market in 💶 Checking account' });
    expect(row).toHaveTextContent('💶 Checking');
    expect(row).toHaveTextContent('30-10-2026');
    expect(row).toHaveTextContent('🛒 Market');
    expect(row).toHaveTextContent('Weekly shop');
    expect(within(row).getByText('-€112.01')).toHaveClass('activity-amount');
    expect(within(row).getByTitle('🛒 Market')).toBeInTheDocument();
  });

  it('shows the combination subtitle with the included category names', () => {
    setup({ subtitle: 'Combination (Waste Tax, WOZ, Sweage)' });
    expect(screen.getByText('Combination (Waste Tax, WOZ, Sweage)')).toBeInTheDocument();
  });

  it('marks upcoming rows and shows a split row with its own payee and amount', () => {
    const split = entry({ id: 's1', amount: -35, splits: [{ categoryId: 'groceries', amount: -25, payee: 'Internet plan', memo: 'Internet' }, { categoryId: 'mobile', amount: -10 }] });
    setup({ items: expandActivityEntries([split], true, ['groceries'], { groceries: 'Groceries' }) });
    const row = screen.getByRole('button', { name: 'Open Groceries split for Internet plan in 💶 Checking account' });
    expect(row).toHaveTextContent('Upcoming');
    expect(row).toHaveTextContent('-€25.00');
  });

  it('navigates to the transaction when a row is selected', async () => {
    const { onSelect } = setup({ items: [{ entry: entry(), scheduled: true }] });
    await userEvent.click(screen.getByRole('button', { name: /Open 🛒 Market/ }));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), true, undefined);
  });

  it('shows a transfer payee as "Transfer : account"', () => {
    setup({ items: [{ entry: entry({ payee: '', transferId: 't', transferAccountName: 'Savings' }), scheduled: false }] });
    expect(screen.getByText('Transfer : Savings')).toBeInTheDocument();
  });

  it('has a scrolling rows container so the header stays visible', () => {
    setup();
    expect(screen.getByTestId('activity-rows')).toHaveClass('activity-rows');
    expect(screen.getByTestId('activity-rows').previousElementSibling).toHaveClass('activity-head');
  });
});

describe('PlanTransactionList credit variant', () => {
  const cardItems = [
    { entry: entry({ accountId: 'card', id: 'c1' }), scheduled: false, splitCategoryName: 'Home: Electricity' },
    { entry: entry({ accountId: 'card', id: 'c2', payee: '', amount: 1525.52, transferId: 'x', transferAccountName: 'Checking', categoryId: null, categoryName: null }), scheduled: false, splitCategoryName: 'Credit Card Payments: ICS Mastercard' }
  ];

  it('shows the card name, summary box and DATE PAYEE CATEGORY MEMO AMOUNT columns without ACCOUNT', () => {
    setup({ variant: 'credit', subtitle: 'ICS Mastercard', summary, items: cardItems });
    const dialog = screen.getByRole('dialog', { name: 'Activity' });
    expect(within(dialog).getByText('ICS Mastercard')).toBeInTheDocument();
    const headers = Array.from(dialog.querySelectorAll('.activity-head > span')).map(node => node.textContent);
    expect(headers).toEqual(['DATE', 'PAYEE', 'CATEGORY', 'MEMO', 'AMOUNT']);
    const box = within(dialog).getByRole('group', { name: 'Card activity summary' });
    const columns = box.querySelectorAll('dl');
    expect(columns).toHaveLength(2);
    expect(columns[0]).toHaveTextContent('Spending-€1,577.50');
    expect(columns[0]).toHaveTextContent('Returns€0.00');
    expect(columns[0]).toHaveTextContent('Total Spending-€1,577.50');
    expect(columns[1]).toHaveTextContent('Funded Spending+€1,577.50');
    expect(columns[1]).toHaveTextContent('Payments & Returns-€1,525.52');
    expect(columns[1]).toHaveTextContent('Total Activity+€51.98');
    expect(within(box).getByText('Total Activity').closest('div')).toHaveClass('activity-summary-total');
  });

  it('shows Group: Category and transfer payees, and scrolls the table body separately from the summary', () => {
    setup({ variant: 'credit', subtitle: 'ICS Mastercard', summary, items: cardItems });
    expect(screen.getByText('Home: Electricity')).toBeInTheDocument();
    expect(screen.getByText('Transfer : Checking')).toBeInTheDocument();
    expect(screen.getByText('Credit Card Payments: ICS Mastercard')).toBeInTheDocument();
    const rows = screen.getByTestId('activity-rows');
    expect(rows).toHaveAttribute('data-variant', 'credit');
    expect(rows.closest('.activity-popover-credit')).not.toBeNull();
    expect(rows.parentElement?.querySelector('.activity-summary')).not.toBeNull();
    expect(rows.contains(screen.getByRole('group', { name: 'Card activity summary' }))).toBe(false);
  });
});

describe('PlanTransactionList behaviour', () => {
  it('closes with the Close button', async () => {
    const { onClose } = setup();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes on Escape and returns focus to the trigger', async () => {
    const onClose = vi.fn();
    const trigger = document.createElement('button');
    document.body.append(trigger);
    const { rerender } = render(<PlanTransactionList items={[]} accounts={accounts} anchor={trigger} onClose={onClose} onSelect={vi.fn()} />);
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
    rerender(<div />);
    expect(trigger).toHaveFocus();
  });

  it('closes on an outside click but not on a click inside or on the anchor', async () => {
    const { onClose, trigger } = setup();
    await userEvent.click(screen.getByRole('heading', { name: 'Activity' }));
    await userEvent.click(trigger);
    expect(onClose).not.toHaveBeenCalled();
    await userEvent.click(document.body);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows a loading state without the table', () => {
    setup({ loading: true, items: [] });
    expect(screen.getByRole('status')).toHaveTextContent('Loading transactions');
    expect(screen.queryByText('ACCOUNT')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();
  });

  it('shows an error with a retry action', async () => {
    const { onRetry } = setup({ error: 'Could not load transactions.', items: [] });
    expect(screen.getByRole('alert')).toHaveTextContent('Could not load transactions.');
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('shows the empty message, and the credit summary stays visible when the card has no rows', () => {
    setup({ items: [], emptyMessage: 'No transactions for this month.' });
    expect(screen.getByText('No transactions for this month.')).toBeInTheDocument();
    expect(screen.queryByTestId('activity-rows')).not.toBeInTheDocument();
  });

  it('shows the credit summary even when the card has no transactions', () => {
    setup({ variant: 'credit', summary, items: [] });
    expect(screen.getByRole('group', { name: 'Card activity summary' })).toBeInTheDocument();
    expect(screen.getByText('No transactions for this category this month.')).toBeInTheDocument();
  });
});
