import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { TransactionForm } from './TransactionForm';
import { type Account } from './api';

const accounts: Account[] = [{ id: 'cash', name: 'Checking', isClosed: false, balance: 100, clearedBalance: 100, unclearedBalance: 0, accountType:'checking', isFavorite:false, connectionStatus:'unlinked', lastReconciledAt:null, needsCategoryCount:0 }];
beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => new Response(JSON.stringify(path === '/api/payees' ? ['Shop'] : { groups: [{ categories: [{ id: 'food', name: 'Food' }, { id: 'rent', name: 'Rent' }] }] }), { status: 200 })));
});

it('submits signed categorized spending and payee fields', async () => {
  const submit = vi.fn(async () => {}); const user = userEvent.setup();
  render(<TransactionForm accounts={accounts} selectedAccount="cash" busy={false} onSubmit={submit} />);
  await screen.findByRole('combobox', { name: 'Category' });
  await user.type(screen.getByLabelText('Amount'), '-12.34');
  await user.type(screen.getByLabelText('Payee'), 'Shop');
  await user.click(screen.getByRole('combobox', { name: 'Category' })); await user.click(screen.getByRole('option', { name: /^Food/ }));
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenCalledWith(expect.objectContaining({ amount: -12.34, categoryId: 'food', accountId: 'cash', payee: 'Shop', splits: [] }));
});

it('rejects excess precision and mismatched split totals then submits valid splits', async () => {
  const submit = vi.fn(async () => {}); const user = userEvent.setup();
  render(<TransactionForm accounts={accounts} selectedAccount="cash" busy={false} onSubmit={submit} />);
  await screen.findByRole('combobox', { name: 'Category' });
  await user.type(screen.getByLabelText('Amount'), '-10.001');
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(screen.getByRole('alert')).toHaveTextContent('two decimal places');
  expect(submit).not.toHaveBeenCalled();
  await user.clear(screen.getByLabelText('Amount')); await user.type(screen.getByLabelText('Amount'), '-10');
  await user.click(screen.getByRole('button', { name: '+ Add split' }));
  await user.click(screen.getByRole('combobox', { name: 'Split 1 category' })); await user.click(screen.getByRole('option', { name: /^Food/ }));
  await user.type(screen.getByLabelText('Split 1 amount'), '-7');
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(screen.getByRole('alert')).toHaveTextContent('must equal');
  await user.click(screen.getByRole('button', { name: '+ Add split' }));
  await user.click(screen.getByRole('combobox', { name: 'Split 2 category' })); await user.click(screen.getByRole('option', { name: /^Rent/ }));
  await user.type(screen.getByLabelText('Split 2 amount'), '-3');
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  await waitFor(() => expect(submit).toHaveBeenCalledWith(expect.objectContaining({ amount: -10, categoryId: null, splits: [{ categoryId: 'food', amount: -7 }, { categoryId: 'rent', amount: -3 }] })));
});

it('disables writes when categories fail to load', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 503 })));
  render(<TransactionForm accounts={accounts} selectedAccount="cash" busy={false} onSubmit={vi.fn()} />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Could not load categories');
  expect(screen.getByRole('button', { name: 'Save transaction' })).toBeDisabled();
});

it('requires a category for credit account transactions and hides card payment categories', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => new Response(JSON.stringify(path === '/api/payees' ? [] : { groups: [{ id: 'credit-card-payments', categories: [{ id: 'ccp-card', name: 'Visa' }] }, { id: 'daily', categories: [{ id: 'food', name: 'Food' }] }] }), { status: 200 })));
  const card: Account = { ...accounts[0], id: 'card', name: 'Visa', balance: -50, accountType: 'creditCard' };
  const submit = vi.fn(async () => {}); const user = userEvent.setup();
  render(<TransactionForm accounts={[card]} selectedAccount="card" busy={false} onSubmit={submit} />);
  await screen.findByRole('combobox', { name: 'Category' });
  await user.type(screen.getByLabelText('Amount'), '-20');
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Credit account transactions require a category.');
  expect(submit).not.toHaveBeenCalled();
  await user.click(screen.getByRole('combobox', { name: 'Category' }));
  expect(within(screen.getByRole('listbox', { name: 'Budget categories' })).queryByRole('option', { name: /^Visa/ })).not.toBeInTheDocument();
  await user.click(screen.getByRole('option', { name: /^Food/ }));
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  await waitFor(() => expect(submit).toHaveBeenCalledWith(expect.objectContaining({ accountId: 'card', amount: -20, categoryId: 'food' })));
});

it('calculates arithmetic typed into an amount field', async () => {
  const submit = vi.fn(async () => {}); const user = userEvent.setup();
  render(<TransactionForm accounts={accounts} selectedAccount="cash" busy={false} onSubmit={submit} />);
  await screen.findByRole('combobox', { name: 'Category' });
  const amount = screen.getByLabelText('Amount');
  expect(amount.closest('.money-input-shell')?.querySelector('.money-input-icon svg')).not.toBeNull();
  await user.type(amount, '-12.50-7.25');
  await user.tab();
  expect(amount).toHaveValue('-19.75');
  await user.click(screen.getByRole('combobox', { name: 'Category' })); await user.click(screen.getByRole('option', { name: /^Food/ }));
  await user.click(screen.getByRole('button', { name: 'Save transaction' }));
  expect(submit).toHaveBeenCalledWith(expect.objectContaining({ amount: -19.75, categoryId: 'food' }));
});

it('offers the same budget categories for any transaction date and only the month balances differ', async () => {
  // Categories are budget-wide: every month returns the same groups and categories (archived ones included and hidden).
  const groups = (available: number) => [{ id: 'daily', name: 'Everyday', categories: [{ id: 'food', name: 'Food', available }, { id: 'pets', name: 'Pets', available: 0 }, { id: 'old', name: 'Old', isArchived: true, available: 0 }] }];
  const requested: string[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    requested.push(path);
    if (path === '/api/payees') return new Response(JSON.stringify([]), { status: 200 });
    return new Response(JSON.stringify({ readyToAssign: 0, groups: groups(path.endsWith('2025-01') ? 5 : 40) }), { status: 200 });
  }));
  const user = userEvent.setup();
  render(<TransactionForm accounts={accounts} selectedAccount="cash" busy={false} onSubmit={vi.fn(async () => {})} />);
  const names = async () => { await user.click(await screen.findByRole('combobox', { name: 'Category' })); const listbox = await screen.findByRole('listbox', { name: 'Budget categories' }); const result = within(listbox).getAllByRole('option').map(option => option.textContent); await user.keyboard('{Escape}'); return result; };
  const current = await names();
  fireDate('2025-01-15');
  await waitFor(() => expect(requested.some(path => path.endsWith('/2025-01'))).toBe(true));
  const past = await names();
  expect(past.map(name => name?.replace(/€.*$/, ''))).toEqual(current.map(name => name?.replace(/€.*$/, '')));
  expect(past.join(' ')).not.toContain('Old');
});

function fireDate(value: string) {
  const input = screen.getByLabelText('Date') as HTMLInputElement;
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
  setter.call(input, value);
  input.dispatchEvent(new Event('input', { bubbles: true }));
}
