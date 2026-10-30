import { useCallback, useEffect, useRef, useState } from 'react';

// Lightweight change notification from GET /api/budgets/{id}/events. Never carries financial values.
export type BudgetChange = {
  budgetId: string;
  kind: 'plan' | 'ledger' | 'targets' | 'planning' | 'admin';
  month: string | null;
  version: number | null;
  at: string;
};

export type LiveUpdateOptions = {
  budgetId: string | null | undefined;
  /** Plan month currently on screen and the version the UI last loaded or saved. */
  month: string;
  planVersion: number;
  /** True while this session's own Plan save is in flight; events are re-evaluated once it settles. */
  isSaving: boolean;
  /** True when re-fetching would discard local Plan edits (pending input, unsaved changes or an open conflict). */
  hasUnsavedPlanEdits: () => boolean;
  onPlanChanged: () => void;
  onLedgerChanged: () => void;
  onPlanningChanged?: () => void;
  onBudgetChanged?: () => void;
  /** Test seam; defaults to the browser EventSource. */
  createEventSource?: (url: string) => EventSource;
};

const coalesceMs = 250;
const maxBackoffMs = 30_000;

export function reconnectDelay(attempt: number) {
  return Math.min(maxBackoffMs, 1000 * 2 ** Math.min(attempt, 5));
}

/**
 * Subscribes to a budget's server-sent change events and refreshes affected data. Plan changes refresh the month
 * unless the user has unsaved Plan edits, in which case a "Updated elsewhere" notice is raised instead. Reconnects
 * with exponential backoff when the stream drops.
 */
export function useBudgetLiveUpdates(options: LiveUpdateOptions) {
  const [notice, setNotice] = useState(false);
  const [connected, setConnected] = useState(false);
  const latest = useRef(options);
  latest.current = options;
  const pendingPlan = useRef<BudgetChange | null>(null);
  const timers = useRef<{ plan?: number; ledger?: number; planning?: number }>({});

  const schedule = useCallback((key: 'plan' | 'ledger' | 'planning', action: () => void) => {
    window.clearTimeout(timers.current[key]);
    timers.current[key] = window.setTimeout(action, coalesceMs);
  }, []);

  const evaluatePlan = useCallback((change: BudgetChange) => {
    const current = latest.current;
    if (change.month !== null && change.month !== current.month) return;
    // The session's own saves arrive as events too; anything at or below the loaded version is already shown.
    if (change.kind === 'plan' && change.version !== null && change.version <= current.planVersion) return;
    if (current.isSaving) { pendingPlan.current = change; return; }
    if (current.hasUnsavedPlanEdits()) { setNotice(true); return; }
    schedule('plan', () => latest.current.onPlanChanged());
  }, [schedule]);

  const handle = useCallback((change: BudgetChange) => {
    const current = latest.current;
    if (change.budgetId !== current.budgetId) return;
    switch (change.kind) {
      case 'plan': case 'targets': evaluatePlan(change); break;
      case 'ledger': schedule('ledger', () => latest.current.onLedgerChanged()); break;
      case 'planning': schedule('planning', () => latest.current.onPlanningChanged?.()); break;
      case 'admin': current.onBudgetChanged?.(); break;
    }
  }, [evaluatePlan, schedule]);

  // Re-check an event that arrived during this session's own save once the save settles.
  useEffect(() => {
    if (options.isSaving || !pendingPlan.current) return;
    const change = pendingPlan.current;
    pendingPlan.current = null;
    evaluatePlan(change);
  }, [options.isSaving, options.planVersion, options.month, evaluatePlan]);

  useEffect(() => { setNotice(false); pendingPlan.current = null; }, [options.budgetId, options.month]);

  useEffect(() => {
    const budgetId = options.budgetId;
    const factory = latest.current.createEventSource ?? (typeof EventSource === 'undefined' ? undefined : (url: string) => new EventSource(url));
    if (!budgetId || !factory) return;
    let source: EventSource | null = null;
    let attempt = 0;
    let retry: number | undefined;
    let stopped = false;
    const onChange = (event: MessageEvent) => {
      try { handle(JSON.parse(String(event.data)) as BudgetChange); } catch { /* ignore malformed events */ }
    };
    const connect = () => {
      if (stopped) return;
      source = factory(`/api/budgets/${encodeURIComponent(budgetId)}/events`);
      source.addEventListener('change', onChange as EventListener);
      source.onopen = () => { attempt = 0; setConnected(true); };
      source.onerror = () => {
        setConnected(false);
        source?.close();
        source = null;
        if (stopped) return;
        retry = window.setTimeout(connect, reconnectDelay(attempt++));
      };
    };
    connect();
    return () => {
      stopped = true;
      window.clearTimeout(retry);
      source?.close();
      setConnected(false);
      const pending = timers.current;
      window.clearTimeout(pending.plan); window.clearTimeout(pending.ledger); window.clearTimeout(pending.planning);
    };
  }, [options.budgetId, handle]);

  const dismissNotice = useCallback(() => setNotice(false), []);
  return { notice, dismissNotice, connected };
}
