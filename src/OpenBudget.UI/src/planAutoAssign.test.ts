import { describe, expect, it } from 'vitest';
import { buildAutoAssignOptions, capAdjustmentsToReady, previousBudgetMonths, type AssignmentHistory } from './planAutoAssign';

describe('previousBudgetMonths', () => {
  it('returns the prior twelve complete months across a year boundary', () => {
    expect(previousBudgetMonths('2026-01')).toEqual([
      '2025-01', '2025-02', '2025-03', '2025-04', '2025-05', '2025-06',
      '2025-07', '2025-08', '2025-09', '2025-10', '2025-11', '2025-12',
    ]);
  });
});

describe('buildAutoAssignOptions', () => {
  const months = previousBudgetMonths('2026-10');
  const category = { id: 'rent', assigned: 10, activity: 0, available: 500, target: 450 };

  it('calculates last-month and twelve-month averages from actual values', () => {
    const history: AssignmentHistory = {
      '2025-10': { rent: { assigned: 120, spent: 80 } },
      '2025-11': { rent: { assigned: 240, spent: 160 } },
      '2026-09': { rent: { assigned: 360, spent: 240 } },
    };
    const options = buildAutoAssignOptions([category], history, months);

    expect(options.find(item => item.id === 'assigned-last-month')).toMatchObject({ amountCents: 35000, adjustments: [{ categoryId: 'rent', deltaCents: 35000 }] });
    expect(options.find(item => item.id === 'spent-last-month')).toMatchObject({ amountCents: 23000, adjustments: [{ categoryId: 'rent', deltaCents: 23000 }] });
    expect(options.find(item => item.id === 'average-assigned')?.amountCents).toBe(5000);
    expect(options.find(item => item.id === 'average-spent')?.amountCents).toBe(3000);
  });

  it('funds only current-month scheduled outflows that would make a category negative', () => {
    const options = buildAutoAssignOptions([
      { ...category, id: 'rent', assigned: 300, available: 40, target: 450 },
      { ...category, id: 'groceries', assigned: 200, available: -25, target: 100 },
      { ...category, id: 'phone', assigned: 10, available: 80, target: 100 },
    ], {}, months, { rent: -100, groceries: -5, phone: -80 });

    expect(options.find(item => item.id === 'underfunded')?.adjustments).toEqual([
      { categoryId: 'rent', deltaCents: 6000 },
      { categoryId: 'groceries', deltaCents: 3000 },
    ]);
    expect(options.find(item => item.id === 'underfunded')?.requiresUpcoming).toBe(true);
  });

  it('does not recommend target funding when no scheduled outflow will create a shortfall', () => {
    const options = buildAutoAssignOptions([{ ...category, assigned: 0, available: 10, target: 450 }], {}, months);
    expect(options.find(item => item.id === 'underfunded')?.adjustments).toEqual([]);
  });

  it('computes reset actions without negative assignments', () => {
    const options = buildAutoAssignOptions([
      { ...category, assigned: 300, available: 500, target: 450 },
      { ...category, id: 'groceries', assigned: 200, available: -25, target: 100 },
    ], {}, months);

    expect(options.find(item => item.id === 'reset-available')?.adjustments).toEqual([{ categoryId: 'rent', deltaCents: -30000 }]);
    expect(options.find(item => item.id === 'reset-assigned')?.amountCents).toBe(50000);
  });
  it('tops categories up to their Long-Term Planning amount and skips missing plans and snoozed categories', () => {
    const options = buildAutoAssignOptions([
      { ...category, id: 'rent', assigned: 100, planned: 450.5 },
      { ...category, id: 'groceries', assigned: 300, planned: 200 },
      { ...category, id: 'phone', assigned: 0, planned: null },
      { ...category, id: 'gym', assigned: 0 },
      { ...category, id: 'holiday', assigned: 0, planned: 80, snoozed: true },
      { ...category, id: 'gifts', assigned: 0, planned: 0 },
      { ...category, id: 'fun', assigned: -20, planned: 30 },
    ], {}, months);
    const planned = options.find(item => item.id === 'planned');

    expect(planned).toMatchObject({ label: 'Planned', requiresHistory: false, requiresUpcoming: false, amountCents: 38050 });
    expect(planned?.adjustments).toEqual([
      { categoryId: 'rent', deltaCents: 35050 },
      { categoryId: 'fun', deltaCents: 3000 },
    ]);
    expect(options.map(item => item.id).indexOf('planned')).toBe(1);
  });

  it('has nothing to assign when no category has a plan value', () => {
    const planned = buildAutoAssignOptions([{ ...category, planned: 5 }], {}, months).find(item => item.id === 'planned');
    expect(planned).toMatchObject({ amountCents: 0, adjustments: [] });
  });
});

describe('capAdjustmentsToReady', () => {
  it('funds positive adjustments in order until Ready to Assign runs out and keeps reductions', () => {
    expect(capAdjustmentsToReady([
      { categoryId: 'rent', deltaCents: 35050 },
      { categoryId: 'fun', deltaCents: 3000 },
      { categoryId: 'old', deltaCents: -500 },
      { categoryId: 'late', deltaCents: 100 },
    ], 360)).toEqual({
      recommendedCents: 38150,
      adjustments: [
        { categoryId: 'rent', deltaCents: 35050 },
        { categoryId: 'fun', deltaCents: 950 },
        { categoryId: 'old', deltaCents: -500 },
        { categoryId: 'late', deltaCents: 0 },
      ],
    });
    expect(capAdjustmentsToReady([{ categoryId: 'rent', deltaCents: 100 }], -5).adjustments).toEqual([{ categoryId: 'rent', deltaCents: 0 }]);
  });
});
