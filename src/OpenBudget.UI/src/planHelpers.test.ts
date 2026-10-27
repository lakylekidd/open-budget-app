import { describe, expect, it } from 'vitest';
import { categoryProgress, categoryTransactionAmount, moveItem } from './planHelpers';
import type { Entry } from './ledger/api';

describe('categoryProgress', () => {
  it('shows the unspent portion of a funded category', () => {
    expect(categoryProgress(336, -112, 224).green).toBeCloseTo(66.67, 1);
    expect(categoryProgress(336, -112, 224).spent).toBeCloseTo(33.33, 1);
    expect(categoryProgress(336, -112, 224).orange).toBe(0);
    expect(categoryProgress(336, -112, 224).red).toBe(0);
  });

  it('shows funded and overspent portions separately', () => {
    expect(categoryProgress(107.8, -153.7, -45.9).green).toBeCloseTo(70, 0);
    expect(categoryProgress(107.8, -153.7, -45.9).red).toBeCloseTo(30, 0);
  });

  it('outlines the spent portion of a fully spent assignment', () => {
    expect(categoryProgress(25, -25, 0)).toEqual({ green: 0, spent: 100, orange: 0, red: 0 });
  });

  it('uses orange only for an uncovered scheduled outflow portion', () => {
    expect(categoryProgress(0, 0, 0, -70)).toEqual({ green: 0, spent: 0, orange: 100, red: 0 });
    expect(categoryProgress(40, 0, 40, -70)).toEqual({ green: 40 / 70 * 100, spent: 0, orange: 30 / 70 * 100, red: 0 });
    expect(categoryProgress(70, 0, 70, -70)).toEqual({ green: 100, spent: 0, orange: 0, red: 0 });
  });

  it('keeps actual unassigned overspending fully red', () => {
    expect(categoryProgress(0, -35, -35)).toEqual({ green: 0, spent: 0, orange: 0, red: 100 });
  });
});

describe('plan category transactions', () => {
  it('counts only the split amount assigned to a category', () => {
    const entry = { id: 'tx', accountId: 'account', date: '2026-10-01', amount: -30, payee: 'Shop', memo: '', categoryId: null,
      status: 'uncleared', transferId: null, isOpening: false, splits: [{ categoryId: 'one', amount: -12 }, { categoryId: 'two', amount: -18 }],
      needsAttention: false, runningBalance: null, transferAccountName: null, categoryName: null, isLocked: false } satisfies Entry;
    expect(categoryTransactionAmount(entry, 'one')).toBe(-12);
    expect(categoryTransactionAmount(entry, 'two')).toBe(-18);
  });

  it('moves a dragged item to the target position', () => {
    expect(moveItem(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
    expect(moveItem(['a', 'b'], -1, 1)).toEqual(['a', 'b']);
  });
});
