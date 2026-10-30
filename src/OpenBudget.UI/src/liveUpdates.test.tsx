import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { reconnectDelay, useBudgetLiveUpdates, type BudgetChange, type LiveUpdateOptions } from './liveUpdates';

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  listeners = new Map<string, ((event: MessageEvent) => void)[]>();
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;
  constructor(public url: string) { FakeEventSource.instances.push(this); }
  addEventListener(type: string, listener: (event: MessageEvent) => void) { this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener]); }
  close() { this.closed = true; }
  emit(change: Partial<BudgetChange>) {
    const data = JSON.stringify({ budgetId: 'b1', kind: 'plan', month: '2026-10', version: 5, at: '2026-10-21T12:00:00Z', ...change });
    for (const listener of this.listeners.get('change') ?? []) listener(new MessageEvent('change', { data }));
  }
}

beforeEach(() => { FakeEventSource.instances = []; vi.useFakeTimers(); });
afterEach(() => vi.useRealTimers());

function setup(overrides: Partial<LiveUpdateOptions> = {}) {
  const onPlanChanged = vi.fn();
  const onLedgerChanged = vi.fn();
  const onBudgetChanged = vi.fn();
  let dirty = false;
  const initial: LiveUpdateOptions = {
    budgetId: 'b1', month: '2026-10', planVersion: 4, isSaving: false, hasUnsavedPlanEdits: () => dirty,
    onPlanChanged, onLedgerChanged, onBudgetChanged, createEventSource: url => new FakeEventSource(url) as unknown as EventSource, ...overrides,
  };
  const hook = renderHook((props: LiveUpdateOptions) => useBudgetLiveUpdates(props), { initialProps: initial });
  return { hook, initial, onPlanChanged, onLedgerChanged, onBudgetChanged, setDirty: (value: boolean) => { dirty = value; }, source: () => FakeEventSource.instances.at(-1)! };
}

it('subscribes to the selected budget and refreshes the Plan when a newer version arrives', () => {
  const { source, onPlanChanged, hook } = setup();
  expect(source().url).toBe('/api/budgets/b1/events');
  act(() => { source().onopen?.(); });
  expect(hook.result.current.connected).toBe(true);
  act(() => { source().emit({ version: 5 }); vi.advanceTimersByTime(300); });
  expect(onPlanChanged).toHaveBeenCalledTimes(1);
  expect(hook.result.current.notice).toBe(false);
});

it('shows the updated-elsewhere notice instead of refreshing when there are unsaved Plan edits', () => {
  const { source, onPlanChanged, hook, setDirty } = setup();
  setDirty(true);
  act(() => { source().emit({ version: 6 }); vi.advanceTimersByTime(300); });
  expect(onPlanChanged).not.toHaveBeenCalled();
  expect(hook.result.current.notice).toBe(true);
  act(() => hook.result.current.dismissNotice());
  expect(hook.result.current.notice).toBe(false);
});

it('ignores own saves, other months and other budgets, and refreshes target changes', () => {
  const { source, onPlanChanged } = setup();
  act(() => {
    source().emit({ version: 4 });
    source().emit({ month: '2026-11', version: 9 });
    source().emit({ budgetId: 'other', version: 9 });
    vi.advanceTimersByTime(300);
  });
  expect(onPlanChanged).not.toHaveBeenCalled();
  act(() => { source().emit({ kind: 'targets', month: null, version: null }); vi.advanceTimersByTime(300); });
  expect(onPlanChanged).toHaveBeenCalledTimes(1);
});

it('re-evaluates an event received during this session’s save once the save settles', () => {
  const { source, onPlanChanged, hook, initial } = setup({ isSaving: true });
  act(() => { source().emit({ version: 5 }); vi.advanceTimersByTime(300); });
  expect(onPlanChanged).not.toHaveBeenCalled();
  // Our save produced version 5, so the event was our own: nothing to do.
  hook.rerender({ ...initial, isSaving: false, planVersion: 5 });
  act(() => { vi.advanceTimersByTime(300); });
  expect(onPlanChanged).not.toHaveBeenCalled();
  hook.rerender({ ...initial, isSaving: true, planVersion: 5 });
  act(() => { source().emit({ version: 7 }); });
  hook.rerender({ ...initial, isSaving: false, planVersion: 6 });
  act(() => { vi.advanceTimersByTime(300); });
  expect(onPlanChanged).toHaveBeenCalledTimes(1);
});

it('coalesces ledger bursts and forwards budget administration changes', () => {
  const { source, onLedgerChanged, onBudgetChanged } = setup();
  act(() => { source().emit({ kind: 'ledger', month: null, version: 10 }); source().emit({ kind: 'ledger', month: null, version: 11 }); vi.advanceTimersByTime(300); });
  expect(onLedgerChanged).toHaveBeenCalledTimes(1);
  act(() => { source().emit({ kind: 'admin', month: null }); });
  expect(onBudgetChanged).toHaveBeenCalledTimes(1);
});

it('reconnects with exponential backoff and closes on unmount', () => {
  const { source, hook } = setup();
  const first = source();
  act(() => { first.onerror?.(); });
  expect(first.closed).toBe(true);
  expect(FakeEventSource.instances).toHaveLength(1);
  act(() => { vi.advanceTimersByTime(1000); });
  expect(FakeEventSource.instances).toHaveLength(2);
  act(() => { source().onerror?.(); vi.advanceTimersByTime(1999); });
  expect(FakeEventSource.instances).toHaveLength(2);
  act(() => { vi.advanceTimersByTime(1); });
  expect(FakeEventSource.instances).toHaveLength(3);
  expect(reconnectDelay(0)).toBe(1000);
  expect(reconnectDelay(10)).toBe(30000);
  hook.unmount();
  expect(source().closed).toBe(true);
});

it('does nothing without a budget or EventSource support', () => {
  setup({ budgetId: null });
  expect(FakeEventSource.instances).toHaveLength(0);
});
