import type { Entry } from './ledger/api';

export function categoryTransactionAmount(entry: Entry, categoryId: string): number {
  if (entry.categoryId === categoryId) return entry.amount;
  return entry.splits.filter(split => split.categoryId === categoryId).reduce((total, split) => total + split.amount, 0);
}

export function categoryProgress(assigned: number, activity: number, available: number, upcomingOutflow = 0) {
  const upcoming = Math.max(-upcomingOutflow, 0);
  if (available < 0) {
    const funded = Math.max(assigned, available - activity, 0);
    const scale = funded + Math.abs(available);
    if (scale === 0) return { green: 0, spent: 0, orange: 0, red: 100 };
    return { green: (funded / scale) * 100, spent: 0, orange: 0, red: (Math.abs(available) / scale) * 100 };
  }

  if (upcoming > 0) {
    const covered = Math.min(available, upcoming);
    if (covered >= upcoming) return { green: 100, spent: 0, orange: 0, red: 0 };
    if (covered === 0) return { green: 0, spent: 0, orange: 100, red: 0 };
    return { green: (covered / upcoming) * 100, spent: 0, orange: ((upcoming - covered) / upcoming) * 100, red: 0 };
  }

  const fundedBeforeActivity = Math.max(0, assigned, available - activity);
  if (fundedBeforeActivity === 0) return { green: 0, spent: 0, orange: 0, red: 0 };
  return {
    green: Math.min(100, Math.max(0, (available / fundedBeforeActivity) * 100)),
    spent: Math.min(100, Math.max(0, ((fundedBeforeActivity - available) / fundedBeforeActivity) * 100)),
    orange: 0,
    red: 0,
  };
}

export function moveItem<T>(items: T[], from: number, to: number): T[] {
  if (from < 0 || from >= items.length || to < 0 || to >= items.length || from === to) return items;
  const next = [...items];
  const [item] = next.splice(from, 1);
  next.splice(to, 0, item);
  return next;
}
