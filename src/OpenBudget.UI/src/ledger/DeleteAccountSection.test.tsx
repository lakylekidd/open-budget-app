import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { DeleteAccountSection } from './DeleteAccountSection';
import { type Account } from './api';

const account: Account = { id: 'mistake', name: 'Mistake', isClosed: false, balance: 0, clearedBalance: 0, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0, hasTransactions: false };

it('asks for confirmation, can be cancelled and deletes only on explicit confirmation', async () => {
  const onDelete = vi.fn().mockResolvedValue(undefined);
  render(<DeleteAccountSection account={account} busy={false} onDelete={onDelete} />);
  await userEvent.click(screen.getByRole('button', { name: 'Delete Account' }));
  expect(screen.getByRole('group')).toHaveTextContent('Permanently delete “Mistake”? This can’t be undone.');
  expect(screen.getByRole('button', { name: 'Keep Account' })).toHaveFocus();
  await userEvent.click(screen.getByRole('button', { name: 'Keep Account' }));
  expect(onDelete).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: 'Delete Account' }));
  await userEvent.click(screen.getByRole('button', { name: 'Delete Permanently' }));
  expect(onDelete).toHaveBeenCalledOnce();
});

it('is disabled with an explanation when the account has transactions, and while busy', () => {
  const view = render(<DeleteAccountSection account={{ ...account, hasTransactions: true }} busy={false} onDelete={vi.fn()} />);
  expect(screen.getByRole('button', { name: 'Delete Account' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Delete Account' })).toHaveAccessibleDescription(/Accounts with transactions can’t be deleted/);
  view.rerender(<DeleteAccountSection account={account} busy onDelete={vi.fn()} />);
  expect(screen.getByRole('button', { name: 'Delete Account' })).toBeDisabled();
});
