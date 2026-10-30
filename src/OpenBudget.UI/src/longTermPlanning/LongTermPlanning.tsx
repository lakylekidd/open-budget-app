import { useEffect, useMemo, useRef, useState } from 'react';
import { longTermYearStore, useRoutedLongTermYear } from '../routing/longTermYear';
import { parseAssignment } from '../budgetMoney';
import { MoneyInput } from '../MoneyInput';
import { formatMoney, requestJson, type Entry, type Register } from '../ledger/api';
import { expandActivityEntries, PlanTransactionList, type PlanTransactionItem } from '../PlanTransactionList';
import {
  previewPlanningChange,
  sumNullable,
  type LongTermPlanning as LongTermPlanningData,
  type PlanningCategory,
  type PlanningChange,
  type PlanningGroup,
} from './longTermPlanningMath';
import './longTermPlanning.css';

type EditingValue = PlanningChange & { raw: string; initialRaw: string; amount: number | null };
type ConflictState = { current: LongTermPlanningData; change: PlanningChange };
type Metric = 'plannedBudget' | 'plannedExpenses' | 'plannedAvailable' | 'actualAssigned' | 'actualExpenses' | 'actualAvailable';
type ActivityKind = 'income' | 'expenses' | 'plannedExpenses' | 'actualExpenses';
type ActivitySelection = { month: string; kind: ActivityKind; title: string; categoryIds?: string[] };
type ActivityDialog = ActivitySelection & { items: PlanTransactionItem[]; loading: boolean; error: string; requestId: number };
type LongTermPlanningProps = { onPendingChange?: (pending: boolean) => void; accounts?: { id: string; name: string }[]; onSelectTransaction?: (entry: Entry, scheduled: boolean, splitIndex?: number) => void };

const plannedMetrics: Metric[] = ['plannedBudget', 'plannedExpenses', 'plannedAvailable'];
const actualMetrics: Metric[] = ['actualAssigned', 'actualExpenses', 'actualAvailable'];

function monthName(month: string) {
  return new Intl.DateTimeFormat('en-GB', { month: 'long' }).format(new Date(`${month}-01T12:00:00`));
}

function monthText(year: number, month: number) {
  return `${year}-${String(month).padStart(2, '0')}`;
}

function inputAmount(value: number) {
  return value.toFixed(2);
}

function parseDraft(value: string): number | null {
  const parsed = parseAssignment(value);
  return parsed !== null && parsed >= 0 ? parsed : null;
}

function total(group: PlanningGroup, monthIndex: number, metric: Metric): number | null {
  const values = group.categories.map(category => category.months[monthIndex]?.[metric] ?? null);
  if (metric.startsWith('actual')) return sumNullable(values);
  return sumNullable(values.map(value => value ?? 0));
}

function actualLabel(value: number | null | undefined) {
  return value === null || value === undefined ? '—' : formatMoney(value);
}

function availableTone(metric: Metric, value: number | null | undefined) {
  if ((metric !== 'plannedAvailable' && metric !== 'actualAvailable') || value === null || value === undefined) return '';
  return value < 0 ? 'lt-available-negative' : 'lt-available-positive';
}

function selectInputContents(input: HTMLInputElement) {
  window.requestAnimationFrame(() => {
    if (document.activeElement === input) input.select();
  });
}

export function LongTermPlanning({ onPendingChange, accounts = [], onSelectTransaction }: LongTermPlanningProps) {
  // Under the app router the viewed year follows /{budgetKey}/long-term/{year} (browser back/forward included).
  const routedYear = useRoutedLongTermYear();
  const [year, setYear] = useState(() => routedYear ?? new Date().getFullYear());
  useEffect(() => { if (routedYear !== null) setYear(routedYear); }, [routedYear]);
  useEffect(() => { if (longTermYearStore.get() !== null) longTermYearStore.set(year); }, [year]);
  const [plan, setPlan] = useState<LongTermPlanningData | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [reloadToken, setReloadToken] = useState(0);
  const [collapsed, setCollapsed] = useState<string[]>([]);
  const [editing, setEditing] = useState<EditingValue | null>(null);
  const [pendingChange, setPendingChange] = useState<PlanningChange | null>(null);
  const [conflict, setConflict] = useState<ConflictState | null>(null);
  const [status, setStatus] = useState<'saved' | 'saving' | 'error' | 'conflict'>('saved');
  const [message, setMessage] = useState('');
  const savingRef = useRef(false);
  const cancelEditRef = useRef(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const activityRequestId = useRef(0);
  const [activityDialog, setActivityDialog] = useState<ActivityDialog | null>(null);

  useEffect(() => () => onPendingChange?.(false), [onPendingChange]);

  const now = new Date();
  const currentMonth = monthText(now.getFullYear(), now.getMonth() + 1);
  const displayedPlan = useMemo(() => {
    if (!plan) return null;
    if (editing?.raw !== editing?.initialRaw && editing?.amount !== null && editing?.amount !== undefined)
      return previewPlanningChange(plan, editing);
    if (pendingChange) return previewPlanningChange(plan, pendingChange);
    return plan;
  }, [plan, editing, pendingChange]);

  useEffect(() => {
    if (!plan || year !== now.getFullYear()) return;
    const scroller = scrollRef.current;
    const header = scroller?.querySelector<HTMLElement>(`[data-month="${currentMonth}"]`);
    if (scroller && header) scroller.scrollLeft = Math.max(0, header.offsetLeft - 248);
  }, [plan, year, currentMonth]);

  useEffect(() => {
    const controller = new AbortController();
    onPendingChange?.(false);
    setLoading(true);
    setLoadError('');
    setPlan(null);
    setEditing(null);
    setPendingChange(null);
    setConflict(null);
    setStatus('saved');
    setMessage('');
    void fetch(`/api/long-term-planning/${year}`, { signal: controller.signal })
      .then(async response => {
        const value = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(value.error ?? `Planning data could not be loaded (${response.status}).`);
        return value as LongTermPlanningData;
      })
      .then(value => { if (!controller.signal.aborted) setPlan(value); })
      .catch(error => { if (!controller.signal.aborted) setLoadError(error instanceof Error ? error.message : 'Planning data could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [year, reloadToken]);

  async function saveChange(change: PlanningChange, version = plan?.version) {
    if (!version || savingRef.current) return;
    savingRef.current = true;
    setStatus('saving');
    setMessage('Saving planning changes…');
    setPendingChange(change);
    setEditing(null);
    try {
      const response = await fetch(`/api/long-term-planning/${year}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ version, month: change.month, field: change.field, categoryId: change.categoryId ?? null, amount: change.amount }),
      });
      const result = await response.json().catch(() => ({}));
      if (response.status === 409) {
        setConflict({ current: result.current as LongTermPlanningData, change });
        setStatus('conflict');
        setMessage(result.error ?? 'Planning changed in another session.');
        return;
      }
      if (!response.ok) throw new Error(result.error ?? `Planning changes could not be saved (${response.status}).`);
      setPlan(result as LongTermPlanningData);
      setPendingChange(null);
      setConflict(null);
      setStatus('saved');
      setMessage('Planning changes saved.');
      onPendingChange?.(false);
    } catch (error) {
      setStatus('error');
      setMessage(error instanceof Error ? error.message : 'Planning changes could not be saved.');
      onPendingChange?.(true);
    } finally {
      savingRef.current = false;
    }
  }

  function beginEdit(change: Omit<PlanningChange, 'amount'>, value: number) {
    onPendingChange?.(true);
    const raw = inputAmount(value);
    setMessage('');
    setEditing({ ...change, amount: value, raw, initialRaw: raw });
  }

  function updateDraft(raw: string) {
    if (!editing) return;
    setEditing({ ...editing, raw, amount: parseDraft(raw) });
    setMessage('');
  }

  function commitDraft() {
    if (cancelEditRef.current) {
      cancelEditRef.current = false;
      return;
    }
    if (!editing || savingRef.current || status === 'error' || status === 'conflict') return;
    const parsed = parseDraft(editing.raw);
    if (parsed === null) {
      setMessage('Enter a valid non-negative amount with at most two decimal places.');
      return;
    }
    if (editing.raw === editing.initialRaw) {
      setEditing(null);
      onPendingChange?.(false);
      return;
    }
    const change: PlanningChange = { month: editing.month, field: editing.field, categoryId: editing.categoryId, amount: parsed };
    setEditing(null);
    void saveChange(change);
  }

  function changeYear(delta: number) {
    if (savingRef.current || status === 'saving' || status === 'error' || status === 'conflict' || editing) {
      setMessage('Save or resolve the current planning change before changing years.');
      return;
    }
    setYear(current => Math.min(9999, Math.max(1, current + delta)));
  }

  function metricValue(category: PlanningCategory, monthIndex: number, metric: Metric) {
    const value = category.months[monthIndex]?.[metric];
    return metric.startsWith('actual') ? actualLabel(value) : formatMoney(value ?? 0);
  }

  function renderMetric(category: PlanningCategory, monthIndex: number, metric: Metric) {
    const value = category.months[monthIndex];
    const month = displayedPlan!.months[monthIndex];
    const editable = month.month >= currentMonth && !category.isArchived;
    const locked = !editable || status === 'saving' || status === 'error' || status === 'conflict';
    if (metric === 'plannedBudget' && editable) {
      const field = 'plannedBudgetOverride';
      const change: Omit<PlanningChange, 'amount'> = { month: month.month, field, categoryId: category.id };
      const isEditing = editing?.month === month.month && editing.categoryId === category.id && editing.field === field;
      const display = value.plannedBudget;
      return <div className="lt-value-line" key={metric}>
        {isEditing ? <MoneyInput
          aria-label={`${category.name} Assigned ${monthName(month.month)} ${year}`}
          className="lt-money-input"
          autoFocus
          value={editing.raw}
          disabled={locked}
          aria-invalid={Boolean(editing.amount === null)}
          onFocus={event => selectInputContents(event.currentTarget)}
          onChange={updateDraft}
          onBlur={commitDraft}
          onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); commitDraft(); event.currentTarget.blur(); } if (event.key === 'Escape') { cancelEditRef.current = true; setEditing(null); setMessage(''); onPendingChange?.(false); event.currentTarget.blur(); } }}
        /> : <button
          type="button"
          className="lt-money-display"
          aria-label={`${category.name} Assigned ${monthName(month.month)} ${year}`}
          title="Click to edit the planned assignment"
          disabled={locked}
          onClick={() => beginEdit(change, display)}
        >{formatMoney(display)}</button>}
      </div>;
    }
    if (metric === 'plannedExpenses' || metric === 'actualExpenses') {
      const kind = metric === 'plannedExpenses' ? 'plannedExpenses' : 'actualExpenses';
      const label = kind === 'plannedExpenses' ? 'planned' : 'actual';
      return <div className={`lt-value-line ${kind === 'actualExpenses' ? 'lt-actual-value' : ''}`} key={metric}>
        <button type="button" className="lt-activity-value" aria-label={`View ${category.name} ${label} expenses in ${monthName(month.month)} ${year}`}
          onClick={() => void openActivity({ month: month.month, kind, title: `Activity · ${category.name} · ${label} expenses · ${monthName(month.month)} ${year}`, categoryIds: [category.id] })}>
          {metricValue(category, monthIndex, metric)}
        </button>
      </div>;
    }
    return <div className={`lt-value-line ${metric.startsWith('actual') ? 'lt-actual-value' : ''} ${availableTone(metric, value?.[metric])}`} key={metric}>
      <span className="lt-readonly-value">{metricValue(category, monthIndex, metric)}</span>
    </div>;
  }

  function renderGroupMetric(group: PlanningGroup, monthIndex: number, metric: Metric) {
    if (metric === 'plannedExpenses') {
      const month = displayedPlan!.months[monthIndex].month;
      return <div className="lt-value-line" key={metric}><button type="button" className="lt-activity-value"
        aria-label={`View ${group.name} planned expenses in ${monthName(month)} ${year}`}
        onClick={() => void openActivity({ month, kind: 'plannedExpenses', title: `Activity · ${group.name} · planned expenses · ${monthName(month)} ${year}`, categoryIds: group.categories.map(category => category.id) })}>
        {groupValues(group, monthIndex, metric)}
      </button></div>;
    }
    const availableValue = metric === 'plannedAvailable' || metric === 'actualAvailable' ? total(group, monthIndex, metric) : null;
    return <div className={`lt-value-line ${metric.startsWith('actual') ? 'lt-actual-value' : ''} ${availableTone(metric, availableValue)}`} key={metric}>
      <span className="lt-readonly-value">{groupValues(group, monthIndex, metric)}</span>
    </div>;
  }

  function groupValues(group: PlanningGroup, monthIndex: number, metric: Metric) {
    const value = total(group, monthIndex, metric);
    if (metric.startsWith('actual')) return actualLabel(value);
    return formatMoney(value ?? 0);
  }

  async function openActivity(selection: ActivitySelection) {
    const requestId = ++activityRequestId.current;
    setActivityDialog({ ...selection, items: [], loading: true, error: '', requestId });
    try {
      let entries: Entry[];
      const scheduled = selection.kind !== 'actualExpenses';
      if (scheduled) {
        const result = await requestJson<{ scheduled: Entry[] }>(`/api/long-term-planning/${year}/activity/${selection.month}`);
        entries = result.scheduled;
      } else {
        const [yearNumber, monthNumber] = selection.month.split('-').map(Number);
        const to = `${selection.month}-${String(new Date(yearNumber, monthNumber, 0).getDate()).padStart(2, '0')}`;
        const loadPage = (page: number) => requestJson<Register>(`/api/transactions?${new URLSearchParams({ from: `${selection.month}-01`, to, page: String(page), pageSize: '100', includeReconciled: 'true' })}`);
        const first = await loadPage(1);
        entries = [...first.items];
        for (let page = 2; (page - 1) * first.pageSize < first.total; page++) entries.push(...(await loadPage(page)).items);
      }
      const categoryNames = Object.fromEntries((displayedPlan?.groups.flatMap(group => group.categories) ?? []).map(category => [category.id, category.name]));
      const expanded = expandActivityEntries(entries, scheduled, selection.categoryIds, categoryNames);
      const items = expanded.flatMap(item => {
        const amount = item.categoryAmount ?? item.entry.amount;
        if (selection.kind === 'income') return amount > 0 ? [item] : [];
        if (selection.kind === 'expenses') return amount < 0 ? [item] : [];
        if (selection.kind === 'plannedExpenses') return amount < 0 ? [{ ...item, categoryAmount: amount }] : [];
        return amount !== 0 ? [{ ...item, categoryAmount: amount }] : [];
      });
      setActivityDialog(current => current?.requestId === requestId ? { ...current, items, loading: false } : current);
    } catch (error) {
      setActivityDialog(current => current?.requestId === requestId ? { ...current, loading: false, error: error instanceof Error ? error.message : 'Could not load activity.' } : current);
    }
  }

  function closeActivity() { activityRequestId.current++; setActivityDialog(null); }

  return <section className="long-term-planning" aria-label="Long-Term Planning">
    <header className="lt-toolbar">
      <div><span className="eyebrow">PLAN AHEAD</span><h1>Long-Term Planning</h1><p>Map expected income and expenses across the year, then compare each category with your budget.</p></div>
      <div className="lt-year-controls" aria-label="Year navigation">
        <button type="button" aria-label="Previous year" disabled={year <= 1 || status === 'saving'} onClick={() => changeYear(-1)}>‹</button>
        <strong>{year}</strong>
        <button type="button" aria-label="Next year" disabled={year >= 9999 || status === 'saving'} onClick={() => changeYear(1)}>›</button>
      </div>
    </header>

    {loading ? <div className="lt-state" role="status">Loading planning data for {year}…</div>
      : loadError ? <div className="lt-state lt-error" role="alert"><strong>Planning data could not be loaded.</strong><span>{loadError}</span><button type="button" onClick={() => setReloadToken(value => value + 1)}>Retry</button></div>
      : displayedPlan && <>
        {!displayedPlan.hasPlanningData && <div className="lt-empty-note" role="status">No planning values saved yet. Forecast amounts start at €0.00; enter estimates in any current or future month.</div>}
        {displayedPlan.groups.length === 0 ? <div className="lt-state">Add budget categories in Plan to start long-term planning.</div> : <div className="lt-scroll" ref={scrollRef} aria-label={`Annual budget planning grid for ${year}`}>
          <div className="lt-grid" role="table" aria-label={`Long-term plan ${year}`}>
            <div className="lt-month-head lt-grid-row" role="row">
              <div className="lt-label-header" role="columnheader"><strong>Category</strong><span>Planned vs actual</span></div>
              {displayedPlan.months.map(month => {
                const state = month.month < currentMonth ? 'past' : month.month === currentMonth ? 'current' : 'future';
                const monthIndex = displayedPlan.months.indexOf(month);
                return <div className={`lt-month-header ${state}`} role="columnheader" data-month={month.month} key={month.month}>
                  <h2>{monthName(month.month)}</h2>
                  <button type="button" className="lt-summary-line lt-summary-action" aria-label={`View expected income in ${monthName(month.month)} ${year}`}
                    onClick={() => void openActivity({ month: month.month, kind: 'income', title: `Activity · Expected Income · ${monthName(month.month)} ${year}` })}><span>Expected Income</span><strong>{formatMoney(month.expectedIncome)}</strong></button>
                  <button type="button" className="lt-summary-line lt-summary-action" aria-label={`View expected expenses in ${monthName(month.month)} ${year}`}
                    onClick={() => void openActivity({ month: month.month, kind: 'expenses', title: `Activity · Expected Expenses · ${monthName(month.month)} ${year}` })}><span>Expected Expenses</span><strong>{formatMoney(displayedPlan.months[monthIndex].expectedExpenses)}</strong></button>
                  <div className="lt-summary-line"><span>Remaining to Be Assigned</span><strong>{formatMoney(displayedPlan.months[monthIndex].remainingToBeAssigned)}</strong></div>
                  <div className="lt-summary-line lt-summary-remaining"><span>Remaining</span><strong>{formatMoney(displayedPlan.months[monthIndex].remaining)}</strong></div>
                  <div className="lt-measure-head"><span>Assigned</span><span>Expenses</span><span>Available</span></div>
                </div>;
              })}
            </div>

            {displayedPlan.groups.map(group => {
              const isCollapsed = collapsed.includes(group.id);
              return <div className="lt-group-block" role="rowgroup" key={group.id}>
                <div className="lt-grid-row lt-group-row" role="row">
                  <div className="lt-label-cell lt-sticky-label lt-group-label" role="rowheader">
                    <button type="button" aria-expanded={!isCollapsed} aria-label={`${isCollapsed ? 'Expand' : 'Collapse'} ${group.name}`} onClick={() => setCollapsed(values => isCollapsed ? values.filter(id => id !== group.id) : [...values, group.id])}>
                      <span className={`lt-disclosure ${isCollapsed ? 'closed' : ''}`}>⌄</span><strong>{group.name}</strong>
                    </button>
                  </div>
                  {displayedPlan.months.map((month, monthIndex) => <div className={`lt-month-values ${month.month < currentMonth ? 'past' : month.month === currentMonth ? 'current' : 'future'}`} role="cell" key={month.month}>
                    <div className="lt-subrow lt-planned-subrow" data-subrow="planned">{plannedMetrics.map(metric => renderGroupMetric(group, monthIndex, metric))}</div>
                  </div>)}
                </div>
                {!isCollapsed && group.categories.map(category => <div className="lt-grid-row lt-category-row" role="row" key={category.id}>
                  <div className="lt-label-cell lt-sticky-label" role="rowheader">
                    <strong title={category.name}>{category.name}</strong>
                    <div className="lt-pair-labels"><span>Planned</span><span>Actual</span></div>
                  </div>
                  {displayedPlan.months.map((month, monthIndex) => <div className={`lt-month-values ${month.month < currentMonth ? 'past' : month.month === currentMonth ? 'current' : 'future'}`} role="cell" key={month.month}>
                    <div className="lt-subrow lt-planned-subrow" data-subrow="planned">{plannedMetrics.map(metric => renderMetric(category, monthIndex, metric))}</div>
                    <div className="lt-subrow lt-actual-subrow" data-subrow="actual">{actualMetrics.map(metric => renderMetric(category, monthIndex, metric))}</div>
                  </div>)}
                </div>)}
              </div>;
            })}
          </div>
        </div>}

        <div className={`lt-save-state ${status}`} role="status">
          <span>{message || (status === 'saved' ? 'Planning changes saved.' : status === 'saving' ? 'Saving planning changes…' : '')}</span>
          {status === 'error' && pendingChange && <><button type="button" onClick={() => void saveChange(pendingChange)}>Retry save</button><button type="button" onClick={() => setReloadToken(value => value + 1)}>Reload saved plan</button></>}
          {status === 'conflict' && conflict && <><button type="button" onClick={() => { setPlan(conflict.current); setPendingChange(null); setEditing(null); setConflict(null); setStatus('saved'); setMessage('Loaded the latest saved plan.'); onPendingChange?.(false); }}>Load latest</button><button type="button" onClick={() => { const current = conflict.current; const change = conflict.change; setPlan(current); setConflict(null); onPendingChange?.(true); void saveChange(change, current.version); }}>Reapply my value</button></>}
        </div>
        {activityDialog && <PlanTransactionList title={activityDialog.title} items={activityDialog.items} accounts={accounts}
          loading={activityDialog.loading} error={activityDialog.error} emptyMessage="No transactions for this month."
          onClose={closeActivity} onRetry={() => void openActivity(activityDialog)}
          onSelect={(entry, scheduled, splitIndex) => { closeActivity(); onSelectTransaction?.(entry, scheduled, splitIndex); }} />}
      </>}
  </section>;
}

export default LongTermPlanning;
