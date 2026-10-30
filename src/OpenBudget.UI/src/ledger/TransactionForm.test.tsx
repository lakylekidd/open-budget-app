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
