export type AssignmentHistory = Record<string, Record<string, { assigned: number | null; spent: number | null }>>;

export type AutoAssignCategory = {
  id: string;
  assigned: number;
  activity: number;
  available: number;
  target: number;
};

export type AssignmentAdjustment = { categoryId: string; deltaCents: number };

export type AutoAssignOption = {
  id: 'underfunded' | 'assigned-last-month' | 'spent-last-month' | 'average-assigned' | 'average-spent' | 'reset-available' | 'reset-assigned';
  label: string;
  amountCents: number;
  requiresHistory: boolean;
  requiresUpcoming: boolean;
  adjustments: AssignmentAdjustment[];
};

const cents = (amount: number) => Math.round(amount * 100);

export function previousBudgetMonths(month: string, count = 12): string[] {
  const [year, monthNumber] = month.split('-').map(Number);
  return Array.from({ length: count }, (_, index) => {
    const date = new Date(year, monthNumber - count - 1 + index, 1);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
  });
}

export function buildAutoAssignOptions(categories: AutoAssignCategory[], history: AssignmentHistory, months: string[], scheduledOutflows: Record<string, number> = {}): AutoAssignOption[] {
  const lastMonth = months.at(-1);
  const makeOption = (id: AutoAssignOption['id'], label: string, requiresHistory: boolean, adjustments: AssignmentAdjustment[], requiresUpcoming = false): AutoAssignOption => {
    const active = adjustments.filter(item => item.deltaCents !== 0);
    return { id, label, requiresHistory, requiresUpcoming, adjustments: active, amountCents: active.reduce((total, item) => total + Math.abs(item.deltaCents), 0) };
  };
  const recommendationAdjustments = (targetFor: (category: AutoAssignCategory) => number) => categories.map(category => ({
    categoryId: category.id,
    deltaCents: Math.max(0, cents(targetFor(category)) - Math.max(0, cents(category.assigned))),
  }));
  const historicalAmount = (month: string | undefined, categoryId: string, field: 'assigned' | 'spent') => {
    const amount = month ? history[month]?.[categoryId]?.[field] : null;
    return typeof amount === 'number' ? cents(amount) : 0;
  };
  const average = (category: AutoAssignCategory, field: 'assigned' | 'spent') => {
    const total = months.reduce((sum, month) => sum + historicalAmount(month, category.id, field), 0);
    return Math.max(0, Math.round(total / Math.max(1, months.length)));
  };
  const scheduledShortfalls = categories.map(category => {
    const projectedAvailableCents = cents(category.available) + cents(scheduledOutflows[category.id] ?? 0);
    return { categoryId: category.id, deltaCents: projectedAvailableCents < 0 ? -projectedAvailableCents : 0 };
  });

  return [
    makeOption('underfunded', 'Underfunded', false, scheduledShortfalls, true),
    makeOption('assigned-last-month', 'Assigned Last Month', true, recommendationAdjustments(category => historicalAmount(lastMonth, category.id, 'assigned') / 100)),
    makeOption('spent-last-month', 'Spent Last Month', true, recommendationAdjustments(category => historicalAmount(lastMonth, category.id, 'spent') / 100)),
    makeOption('average-assigned', 'Average Assigned', true, recommendationAdjustments(category => average(category, 'assigned') / 100)),
    makeOption('average-spent', 'Average Spent', true, recommendationAdjustments(category => average(category, 'spent') / 100)),
    makeOption('reset-available', 'Reset Available Amounts', false, categories.map(category => ({
      categoryId: category.id,
      deltaCents: -Math.min(Math.max(0, cents(category.assigned)), Math.max(0, cents(category.available))),
    }))),
    makeOption('reset-assigned', 'Reset Assigned Amounts', false, categories.map(category => ({ categoryId: category.id, deltaCents: -Math.max(0, cents(category.assigned)) }))),
  ];
}
