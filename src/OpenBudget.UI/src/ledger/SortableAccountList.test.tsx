import { createEvent, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { SortableAccountList } from './SortableAccountList';
import type { Account } from './api';

const account = (id: string, name: string): Account => ({ id, name, isClosed: false, balance: 0, clearedBalance: 0, unclearedBalance: 0, accountType: 'checking', isFavorite: false, connectionStatus: 'unlinked', lastReconciledAt: null, needsCategoryCount: 0 });
const accounts = [account('a', 'Alpha'), account('b', 'Beta'), account('c', 'Gamma')];
const renderList = (onMove = vi.fn(), disabled = false) => {
  render(<SortableAccountList accounts={accounts} disabled={disabled} onMove={onMove} renderRow={(row, controls) => <div>{row.name}{controls}</div>} />);
  return onMove;
};
const dataTransfer = () => { const data: Record<string, string> = {}; return { effectAllowed: '', dropEffect: '', setData: (k: string, v: string) => { data[k] = v; }, getData: (k: string) => data[k] }; };
const dropAt = (target: HTMLElement, clientY: number, transfer: ReturnType<typeof dataTransfer>) => {
  target.getBoundingClientRect = () => ({ top: 0, height: 20, bottom: 20, left: 0, right: 100, width: 100, x: 0, y: 0, toJSON: () => ({}) });
  const over = createEvent.dragOver(target, { dataTransfer: transfer }); Object.defineProperty(over, 'clientY', { value: clientY }); fireEvent(target, over);
  const drop = createEvent.drop(target, { dataTransfer: transfer }); Object.defineProperty(drop, 'clientY', { value: clientY }); fireEvent(target, drop);
};

it('moves accounts up and down with labelled buttons that are disabled at the ends', async () => {
  const onMove = renderList();
  expect(screen.getByRole('button', { name: 'Move Alpha up' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Move Gamma down' })).toBeDisabled();
  await userEvent.click(screen.getByRole('button', { name: 'Move Beta up' }));
  expect(onMove).toHaveBeenLastCalledWith('b', 0);
  screen.getByRole('button', { name: 'Move Alpha down' }).focus();
  await userEvent.keyboard('{Enter}');
  expect(onMove).toHaveBeenLastCalledWith('a', 1);
});

it('drops an account before or after another row in the same list', () => {
  const onMove = renderList();
  let transfer = dataTransfer();
  fireEvent.dragStart(screen.getByTestId('sortable-account-a'), { dataTransfer: transfer });
  dropAt(screen.getByTestId('sortable-account-c'), 15, transfer);
  expect(onMove).toHaveBeenLastCalledWith('a', 2);
  transfer = dataTransfer();
  fireEvent.dragStart(screen.getByTestId('sortable-account-c'), { dataTransfer: transfer });
  dropAt(screen.getByTestId('sortable-account-b'), 5, transfer);
  expect(onMove).toHaveBeenLastCalledWith('c', 1);
});

it('ignores drops that did not start in this list and every move while disabled', async () => {
  const onMove = renderList();
  dropAt(screen.getByTestId('sortable-account-a'), 5, dataTransfer());
  expect(onMove).not.toHaveBeenCalled();
  document.body.innerHTML = '';
  const disabledMove = renderList(vi.fn(), true);
  expect(screen.getByTestId('sortable-account-a')).toHaveAttribute('draggable', 'false');
  expect(screen.getByRole('button', { name: 'Move Beta up' })).toBeDisabled();
  await userEvent.click(screen.getByRole('button', { name: 'Move Beta up' }));
  expect(disabledMove).not.toHaveBeenCalled();
});
