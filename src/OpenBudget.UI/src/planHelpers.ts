import type { Entry } from './ledger/api';

export function categoryTransactionAmount(entry: Entry, categoryId: string): number {
  if (entry.categoryId === categoryId) return entry.amount;
  return entry.splits.filter(split => split.categoryId === categoryId).reduce((total, split) => total + split.amount, 0);
}

export function categoryProgress(assigned: number, activity: number, available: number) {
  const fundedBeforeActivity = Math.max(0, assigned, available - activity);
  const scale = available < 0
    ? Math.max(fundedBeforeActivity + Math.abs(available), Math.abs(activity))
    : Math.max(fundedBeforeActivity, Math.abs(activity), available);
  if (scale === 0) return { green: 0, red: 0 };
  return {
    green: Math.min(100, Math.max(0, ((available < 0 ? fundedBeforeActivity : Math.max(available, 0)) / scale) * 100)),
    red: Math.min(100, Math.max(0, (Math.max(-available, 0) / scale) * 100)),
  };
}

export function moveItem<T>(items: T[], from: number, to: number): T[] {
  if (from < 0 || from >= items.length || to < 0 || to >= items.length || from === to) return items;
  const next = [...items];
  const [item] = next.splice(from, 1);
  next.splice(to, 0, item);
  return next;
}
