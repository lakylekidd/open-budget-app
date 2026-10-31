import { useState } from 'react';
import { ApiError, requestJson, type Account, type Accounts } from './api';

// The server stores one budget-wide account order. The sidebar shows it split into sections (CASH, CREDIT, LOANS,
// CLOSED), so a move only reorders accounts inside one section: the section keeps the same slots in the full list and
// every other account keeps its position.
export function moveWithinSection(order: string[], sectionIds: string[], accountId: string, toIndex: number): string[] {
  const from = sectionIds.indexOf(accountId);
  if (from < 0 || toIndex < 0 || toIndex >= sectionIds.length || from === toIndex) return order;
  const reordered = [...sectionIds];
  reordered.splice(from, 1);
  reordered.splice(toIndex, 0, accountId);
  const members = new Set(sectionIds);
  let next = 0;
  return order.map(id => members.has(id) ? reordered[next++] : id);
}

export function saveAccountOrder(version: number, accountIds: string[]) {
  return requestJson<{ id: string; version: number }>('/api/accounts/order', {
    method: 'PUT', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ commandId: crypto.randomUUID(), version, accountIds })
  });
}

// Optimistically shows the new order, then saves it. Any failure restores the previous list, explains why and asks
// the caller to reload, so a lost response or a concurrent change still ends up showing the persisted order.
export function useAccountReorder(accounts: Accounts, setAccounts: (value: Accounts) => void, onSettled: () => void) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  async function move(sectionAccounts: Account[], accountId: string, toIndex: number) {
    if (saving) return;
    const previous = accounts;
    const order = moveWithinSection(previous.accounts.map(account => account.id), sectionAccounts.map(account => account.id), accountId, toIndex);
    if (order.every((id, index) => id === previous.accounts[index].id)) return;
    const byId = new Map(previous.accounts.map(account => [account.id, account]));
    setAccounts({ ...previous, accounts: order.map(id => byId.get(id)!) });
    setSaving(true); setError('');
    try {
      const result = await saveAccountOrder(previous.version, order);
      setAccounts({ version: result.version, accounts: order.map(id => byId.get(id)!) });
    } catch (failure) {
      setAccounts(previous);
      setError(failure instanceof ApiError && failure.status === 409
        ? 'Accounts changed in another session. The latest order has been reloaded; try again.'
        : `Could not save the account order. ${failure instanceof Error ? failure.message : ''}`.trim());
    } finally {
      setSaving(false);
      onSettled();
    }
  }
  return { move, saving, error, clearError: () => setError('') };
}
