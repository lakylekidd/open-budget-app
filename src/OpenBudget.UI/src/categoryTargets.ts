// Category targets for regular Plan categories. The API owns every calculation; these helpers only shape requests
// and turn the server's progress into bar segments.
export type TargetKind = 'weekly' | 'monthly' | 'yearly' | 'custom';
export type TargetBehavior = 'setAside' | 'refill';
export type RepeatUnit = 'day' | 'week' | 'month' | 'year';

export type CategoryTarget = {
  kind: TargetKind; amount: number; behavior: TargetBehavior; weekday: number | null; dayOfMonth: number | null; dueDate: string | null;
  repeatEvery: number | null; repeatUnit: RepeatUnit | null; startMonth: string; version: number; createdAt: string; updatedAt: string;
};

export type TargetProgress = {
  needed: number; funded: number; moreNeeded: number; segments: number; fundedFraction: number; status: 'funded' | 'underfunded';
  nextDueDate: string | null; monthsRemaining: number | null; isPastDue: boolean; cadence: string;
};

export type CategoryTargetResult = { categoryId: string; month: string; target: CategoryTarget | null; progress: TargetProgress | null };

export type TargetFields = {
  kind: TargetKind; amount: number; behavior: TargetBehavior; weekday: number | null; dayOfMonth: number | null; dueDate: string | null;
  repeatEvery: number | null; repeatUnit: RepeatUnit | null;
};

export const targetKinds: { id: TargetKind; label: string }[] = [
  { id: 'weekly', label: 'Weekly' }, { id: 'monthly', label: 'Monthly' }, { id: 'yearly', label: 'Yearly' }, { id: 'custom', label: 'Custom' },
];
export const weekdays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
export const repeatUnits: RepeatUnit[] = ['day', 'week', 'month', 'year'];

export const ordinal = (day: number) => `${day}${day % 100 >= 11 && day % 100 <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[day % 10] ?? 'th'}`;

export const targetPath = (budgetId: string, categoryId: string) => `/api/budgets/${encodeURIComponent(budgetId)}/categories/${encodeURIComponent(categoryId)}/target`;

// Fill fraction (0-1) of each equal bar segment. Weekly targets have one segment per weekday occurrence.
export function targetSegments(progress: TargetProgress): number[] {
  const count = Math.max(1, progress.segments);
  const filled = Math.min(Math.max(progress.fundedFraction, 0), 1) * count;
  return Array.from({ length: count }, (_, index) => Math.min(Math.max(filled - index, 0), 1));
}

export function isTargetUnderfunded(progress: TargetProgress | null | undefined) {
  return Boolean(progress && progress.moreNeeded > 0);
}

export function nextMonthStart(month: string) {
  const [year, monthNumber] = month.split('-').map(Number);
  const date = new Date(year, monthNumber, 1);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`;
}
