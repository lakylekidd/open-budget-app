import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { DeleteCategoryDialog, type CategoryDeletionPreview } from './DeleteCategoryDialog';

let preview: CategoryDeletionPreview; let posts: { path: string; body: Record<string, unknown> }[]; let failure: 'read' | 'conflict' | 'lost' | 'invalid' | null;
const close = vi.fn(), deleted = vi.fn();
const response = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  close.mockClear(); deleted.mockClear(); posts = []; failure = null;
  preview = {
    categoryId: 'coffee', name: 'Coffee', groupName: 'Everyday', requiresReassignment: true, transactionCount: 12, splitLineCount: 1, scheduleCount: 0,
    assignedMonthCount: 3, assignedTotal: 90, remainingAvailable: 15, remainingAvailableMonth: '2026-10', hasTarget: true, payeeDefaultCount: 0, loanPairingCount: 0,
    planningValueCount: 0, ledgerVersion: 7,
    reassignmentOptions: [{ id: 'groceries', name: 'Groceries', groupId: 'daily', groupName: 'Everyday' }, { id: 'rent', name: 'Rent', groupId: 'home', groupName: 'Home' }],
  };
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (init?.method === 'POST') {
      posts.push({ path, body: JSON.parse(String(init.body)) });
      if (failure === 'conflict') { failure = null; return response({ error: 'The ledger changed in another session.' }, 409); }
      if (failure === 'invalid') { failure = null; return response({ error: 'Choose an active category to reassign to.' }, 400); }
      if (failure === 'lost') { failure = null; throw new Error('Connection lost'); }
      return response({ id: 'a3d2c4b0-0000-4000-8000-000000000001', version: 8 });
    }
    if (failure === 'read') { failure = null; return response({ error: 'Unavailable' }, 503); }
    return response(preview);
  }));
});

const show = () => render(<DeleteCategoryDialog budgetId="budget-1" categoryId="coffee" categoryName="Coffee" onClose={close} onDeleted={deleted} />);

it('explains the reassignment, lists what moves with the live transaction count and keeps Delete disabled until a category is chosen', async () => {
  show();
  const dialog = await screen.findByRole('dialog', { name: 'Delete Category' });
  expect(await within(dialog).findByText(/Before you can delete the category/)).toHaveTextContent('Before you can delete the category Coffee, you’ll need to reassign your past activity to a new category.');
  const summary = within(dialog).getByRole('region', { name: "Here's what will be reassigned" });
  expect(summary).toHaveTextContent('All transactions');
  expect(within(summary).getByText('12')).toHaveClass('delete-category-count');
  expect(summary).toHaveTextContent('All assigned amounts');
  expect(summary).toHaveTextContent('Any remaining available amount');
  expect(summary).toHaveTextContent('target will be removed');
  const select = within(dialog).getByRole('combobox', { name: 'Select category' });
  expect(within(select).getAllByRole('option').map(option => option.textContent)).toEqual(['Select category', 'Groceries', 'Rent']);
  expect(within(dialog).getByRole('button', { name: 'Delete' })).toBeDisabled();
  await userEvent.selectOptions(select, 'groceries');
  expect(within(dialog).getByRole('button', { name: 'Delete' })).toBeEnabled();
  await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
  await waitFor(() => expect(deleted).toHaveBeenCalledWith('groceries'));
  expect(posts).toHaveLength(1);
  expect(posts[0].path).toBe('/api/budgets/budget-1/categories/coffee/delete');
  expect(posts[0].body).toEqual({ commandId: expect.any(String), version: 7, reassignToCategoryId: 'groceries' });
});

it('cancels and closes without writing', async () => {
  show(); await screen.findByRole('combobox', { name: 'Select category' });
  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
  expect(close).toHaveBeenCalledOnce(); expect(posts).toHaveLength(0);
  await userEvent.click(screen.getByRole('button', { name: 'Close dialog' }));
  expect(close).toHaveBeenCalledTimes(2);
});

it('confirms a category without history without asking for a replacement', async () => {
  preview = { ...preview, requiresReassignment: false, transactionCount: 0, hasTarget: false, reassignmentOptions: [] };
  show();
  expect(await screen.findByText(/nothing needs to be reassigned/)).toBeInTheDocument();
  expect(screen.queryByRole('combobox', { name: 'Select category' })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
  await waitFor(() => expect(deleted).toHaveBeenCalledWith(null));
  expect(posts[0].body.reassignToCategoryId).toBeNull();
});

it('retries a lost response with the same command id so the delete is not applied twice', async () => {
  failure = 'lost'; show();
  await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Select category' }), 'rent');
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('outcome is unknown');
  expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
  await userEvent.click(screen.getByRole('button', { name: 'Retry delete' }));
  await waitFor(() => expect(deleted).toHaveBeenCalledWith('rent'));
  expect(posts).toHaveLength(2); expect(posts[1].body.commandId).toBe(posts[0].body.commandId);
});

it('reloads the details after a conflict and shows refused targets without closing', async () => {
  failure = 'conflict'; show();
  await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Select category' }), 'groceries');
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('changed in another session');
  // The details reload (Delete stays disabled meanwhile) and the chosen category is kept.
  await waitFor(() => expect(screen.getByRole('button', { name: 'Delete' })).toBeEnabled());
  expect(screen.getByRole('combobox', { name: 'Select category' })).toHaveValue('groceries');
  expect(screen.getByRole('alert')).toHaveTextContent('changed in another session');
  failure = 'invalid';
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Choose an active category');
  expect(deleted).not.toHaveBeenCalled(); expect(close).not.toHaveBeenCalled();
  expect(posts[0].body.commandId).not.toBe(posts[1].body.commandId);
});

it('recovers a failed initial load', async () => {
  failure = 'read'; show();
  await userEvent.click(await screen.findByRole('button', { name: 'Retry loading' }));
  expect(await screen.findByRole('combobox', { name: 'Select category' })).toBeInTheDocument();
});
