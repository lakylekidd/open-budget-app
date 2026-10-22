export type PlanningMonth = {
  month: string;
  expectedIncome: number;
  hasExpectedIncome: boolean;
  expectedExpenses: number;
  remaining: number;
  remainingToBeAssigned: number;
  hasActualData: boolean;
};

export type PlanningCategoryMonth = {
  month: string;
  plannedBudgetOverride: number | null;
  plannedBudgetBeforeMonth: number;
  plannedBudget: number;
  plannedExpenses: number;
  plannedAvailable: number;
  actualAssigned: number | null;
  actualExpenses: number | null;
  actualAvailable: number | null;
};

export type PlanningCategory = {
  id: string;
  name: string;
  sortOrder: number;
  isArchived: boolean;
  months: PlanningCategoryMonth[];
};

export type PlanningGroup = {
  id: string;
  name: string;
  sortOrder: number;
  isArchived: boolean;
  categories: PlanningCategory[];
};

export type LongTermPlanning = {
  year: number;
  version: number;
  hasPlanningData: boolean;
  months: PlanningMonth[];
  groups: PlanningGroup[];
};

export type PlanningChange = {
  month: string;
  field: 'plannedBudgetOverride';
  categoryId?: string;
  amount: number | null;
};

export const cents = (value: number) => Math.round(value * 100);
export const amountFromCents = (value: number) => value / 100;

export function previewPlanningChange(plan: LongTermPlanning, change: PlanningChange): LongTermPlanning {
  const monthIndex = plan.months.findIndex(month => month.month === change.month);
  if (monthIndex < 0) return plan;

  const months = plan.months.map(month => ({ ...month }));
  let groups = plan.groups.map(group => ({ ...group, categories: group.categories.map(category => ({ ...category, months: category.months.map(value => ({ ...value })) })) }));
  if (!change.categoryId) return plan;
  groups = groups.map(group => ({
    ...group,
    categories: group.categories.map(category => {
      if (category.id !== change.categoryId) return category;
      const categoryMonths = category.months;
      const first = categoryMonths[monthIndex];
      if (!first) return category;
      let budgetBefore = monthIndex === 0
        ? first.plannedBudgetBeforeMonth
        : categoryMonths[monthIndex - 1].plannedBudget;
      let available = monthIndex === 0
        ? cents(first.plannedAvailable) - cents(first.plannedBudget) + cents(first.plannedExpenses)
        : cents(categoryMonths[monthIndex - 1].plannedAvailable);

      for (let index = monthIndex; index < categoryMonths.length; index++) {
        const current = categoryMonths[index];
        if (index === monthIndex)
          current.plannedBudgetOverride = change.amount;

        current.plannedBudgetBeforeMonth = budgetBefore;
        if (current.plannedBudgetOverride !== null) budgetBefore = current.plannedBudgetOverride;
        current.plannedBudget = budgetBefore;
        available += cents(current.plannedBudget) - cents(current.plannedExpenses);
        current.plannedAvailable = amountFromCents(available);
      }
      return { ...category, months: categoryMonths };
    }),
  }));

  months.forEach((month, index) => {
    const assignedCents = groups.flatMap(group => group.categories)
      .reduce((sum, category) => sum + cents(category.months[index]?.plannedBudget ?? 0), 0);
    month.remainingToBeAssigned = amountFromCents(cents(month.expectedIncome) - assignedCents);
  });

  return { ...plan, months, groups };
}

export function sumNullable(values: Array<number | null>): number | null {
  const present = values.filter((value): value is number => value !== null);
  return present.length === 0 ? null : amountFromCents(present.reduce((sum, value) => sum + cents(value), 0));
}
