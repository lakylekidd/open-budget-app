import { expect, it } from 'vitest';
import { previewPlanningChange, sumNullable, type LongTermPlanning } from './longTermPlanningMath';

function plan(): LongTermPlanning {
  const months = Array.from({ length: 12 }, (_, index) => ({
    month: `2027-${String(index + 1).padStart(2, '0')}`,
    expectedIncome: 3000,
    hasExpectedIncome: true,
    expectedExpenses: 0,
    remaining: 3000,
    remainingToBeAssigned: 2500,
    hasActualData: false,
  }));
  let available = 0;
  const categoryMonths = months.map((month, index) => {
    const plannedBudgetOverride = index === 0 ? 500 : index === 8 ? 700 : null;
    const plannedBudget = plannedBudgetOverride ?? (index < 8 ? 500 : 700);
    available += plannedBudget;
    return {
      month: month.month,
      plannedBudgetOverride,
      plannedBudgetBeforeMonth: index === 0 ? 0 : index < 9 ? 500 : 700,
      plannedBudget,
      plannedExpenses: 0,
      plannedAvailable: available,
      actualAssigned: null,
      actualExpenses: null,
      actualAvailable: null,
    };
  });
  return {
    year: 2027,
    version: 1,
    hasPlanningData: true,
    months,
    groups: [{
      id: 'daily', name: 'Everyday', sortOrder: 0, isArchived: false,
      categories: [{ id: 'groceries', name: 'Groceries', sortOrder: 0, isArchived: false, months: categoryMonths }],
    }],
  };
}

const categoryMonths = (value: LongTermPlanning) => value.groups[0].categories[0].months;

it('previews a mid-year budget override only through the next explicit override', () => {
  const result = previewPlanningChange(plan(), { month: '2027-06', categoryId: 'groceries', field: 'plannedBudgetOverride', amount: 650 });
  const values = categoryMonths(result);

  expect(values[4].plannedBudget).toBe(500);
  expect(values[5].plannedBudget).toBe(650);
  expect(values[7].plannedBudget).toBe(650);
  expect(values[8].plannedBudget).toBe(700);
  expect(values[8].plannedBudgetOverride).toBe(700);
  expect(values[11].plannedBudget).toBe(700);
  expect(result.months[5].remainingToBeAssigned).toBe(2350);
  expect(result.months[8].remainingToBeAssigned).toBe(2300);
});

it('removing an override restores the previous effective budget and recalculates carry-forward', () => {
  const result = previewPlanningChange(plan(), { month: '2027-09', categoryId: 'groceries', field: 'plannedBudgetOverride', amount: null });
  const values = categoryMonths(result);

  expect(values[8].plannedBudgetOverride).toBeNull();
  expect(values[8].plannedBudget).toBe(500);
  expect(values[11].plannedBudget).toBe(500);
  expect(values[11].plannedAvailable).toBe(6000);
  expect(result.months[8].remainingToBeAssigned).toBe(2500);
});

it('keeps scheduled expenses and remaining stable while previewing a budget override', () => {
  const value = plan();
  value.groups[0].categories[0].months[1].plannedExpenses = 0.1;
  value.groups[0].categories[0].months[1].plannedAvailable -= 0.1;
  value.months[1].expectedExpenses = 0.1;
  value.months[1].remaining = 2999.9;
  const result = previewPlanningChange(value, { month: '2027-02', categoryId: 'groceries', field: 'plannedBudgetOverride', amount: 500.2 });

  expect(categoryMonths(result)[1].plannedAvailable).toBe(1000.1);
  expect(result.months[1].expectedExpenses).toBe(0.1);
  expect(result.months[1].remaining).toBe(2999.9);
  expect(result.months[1].remainingToBeAssigned).toBe(2499.8);
  expect(sumNullable([0.1, 0.2, null])).toBe(0.3);
});
