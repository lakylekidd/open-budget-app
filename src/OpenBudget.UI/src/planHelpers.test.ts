import { describe, expect, it } from 'vitest';
import { categoryProgress, categoryTransactionAmount, moveItem } from './planHelpers';
import type { Entry } from './ledger/api';

describe('categoryProgress', () => {
  it('shows the unspent portion of a funded category', () => {
    expect(categoryProgress(336, -112, 224).green).toBeCloseTo(66.67, 1);
    expect(categoryProgress(336, -112, 224).red).toBe(0);
  });

  it('shows funded and overspent portions separately', () => {
    expect(categoryProgress(107.8, -153.7, -45.9).green).toBeCloseTo(70, 0);
    expect(categoryProgress(107.8, -153.7, -45.9).red).toBeCloseTo(30, 0);
  });

  it('shows an empty bar when the category has been fully spent', () => {
    expect(categoryProgress(25, -25, 0)).toEqual({ green: 0, red: 0 });
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
