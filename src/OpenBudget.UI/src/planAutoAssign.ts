export type AssignmentHistory = Record<string, Record<string, { assigned: number | null; spent: number | null }>>;

export type AutoAssignCategory = {
  id: string;
  assigned: number;
  activity: number;
  available: number;
  target: number;
  // Server-calculated amount a category target still needs this month (TargetProgress.moreNeeded).
  targetMoreNeeded?: number;
  // Server-calculated Long-Term Planning budget effective for this month (CategoryDto.planned); null/undefined when no plan exists.
  planned?: number | null;
  // Snoozed categories are protected from the Planned recommendation (archived ones never reach Auto-Assign).
  snoozed?: boolean;
};

export type AssignmentAdjustment = { categoryId: string; deltaCents: number };

export type AutoAssignOption = {
  id: 'underfunded' | 'planned' | 'assigned-last-month' | 'spent-last-month' | 'average-assigned' | 'average-spent' | 'reset-available' | 'reset-assigned';
  label: string;
  amountCents: number;
  requiresHistory: boolean;
  requiresUpcoming: boolean;
  adjustments: AssignmentAdjustment[];
};

const cents = (amount: number) => Math.round(amount * 100);

// Positive adjustments are funded in list (category) order until Ready to Assign runs out; reductions pass through unchanged.
export function capAdjustmentsToReady(adjustments: AssignmentAdjustment[], readyToAssign: number) {
  let readyCents = Math.max(0, cents(readyToAssign));
  let recommendedCents = 0;
  const capped = adjustments.map(item => {
    if (item.deltaCents <= 0) return item;
    recommendedCents += item.deltaCents;
    const deltaCents = Math.min(item.deltaCents, readyCents);
    readyCents -= deltaCents;
    return { ...item, deltaCents };
  });
  return { adjustments: capped, recommendedCents };
}

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
  // Underfunded covers the larger of a scheduled-outflow shortfall and the server-calculated amount a category
  // target still needs this month. The legacy manual `target` amount is not a recommendation input.
  const scheduledShortfalls = categories.map(category => {
    const projectedAvailableCents = cents(category.available) + cents(scheduledOutflows[category.id] ?? 0);
    const shortfall = projectedAvailableCents < 0 ? -projectedAvailableCents : 0;
    return { categoryId: category.id, deltaCents: Math.max(shortfall, Math.max(0, cents(category.targetMoreNeeded ?? 0))) };
  });

  return [
    makeOption('underfunded', 'Underfunded', false, scheduledShortfalls, true),
    // Planned tops each category up to its Long-Term Planning amount for this month, like Assigned Last Month: it never
    // lowers an existing assignment and skips snoozed categories and categories without a plan value.
    makeOption('planned', 'Planned', false, recommendationAdjustments(category => !category.snoozed && typeof category.planned === 'number' ? category.planned : 0)),
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
