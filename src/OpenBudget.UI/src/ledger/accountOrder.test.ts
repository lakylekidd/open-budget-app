import { expect, it } from 'vitest';
import { moveWithinSection } from './accountOrder';

const order = ['cash-a', 'card-a', 'cash-b', 'loan-a', 'cash-c', 'card-b'];
const cash = ['cash-a', 'cash-b', 'cash-c'];

it('moves an account within its section and keeps the other sections in place', () => {
  expect(moveWithinSection(order, cash, 'cash-c', 0)).toEqual(['cash-c', 'card-a', 'cash-a', 'loan-a', 'cash-b', 'card-b']);
  expect(moveWithinSection(order, cash, 'cash-a', 2)).toEqual(['cash-b', 'card-a', 'cash-c', 'loan-a', 'cash-a', 'card-b']);
  expect(moveWithinSection(order, ['card-a', 'card-b'], 'card-b', 0)).toEqual(['cash-a', 'card-b', 'cash-b', 'loan-a', 'cash-c', 'card-a']);
});

it('returns the same order for no-op, out-of-range or foreign moves', () => {
  expect(moveWithinSection(order, cash, 'cash-b', 1)).toBe(order);
  expect(moveWithinSection(order, cash, 'cash-a', -1)).toBe(order);
  expect(moveWithinSection(order, cash, 'cash-c', 3)).toBe(order);
  expect(moveWithinSection(order, cash, 'card-a', 0)).toBe(order);
});
