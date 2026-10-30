import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties } from 'react';
import type { Entry, Split } from './ledger/api';
import './planTransactionList.css';

export type PlanTransactionItem = { entry: Entry; scheduled: boolean; categoryAmount?: number; splitIndex?: number; split?: Split; splitCategoryName?: string };

// Month breakdown for one credit card; mirrors GET /api/accounts/{id}/credit-activity/{month}.
export type CreditActivitySummary = { spending: number; returns: number; totalSpending: number; fundedSpending: number; paymentsAndReturns: number; totalActivity: number };

export function expandActivityEntries(entries: Entry[], scheduled: boolean, categoryIds?: string[], categoryNames: Record<string, string> = {}): PlanTransactionItem[] {
  return entries.flatMap(entry => {
    if (entry.splits.length) return entry.splits.flatMap((split, splitIndex) => {
      if (categoryIds && !categoryIds.includes(split.categoryId)) return [];
      return [{ entry, scheduled, categoryAmount: split.amount, splitIndex, split, splitCategoryName: categoryNames[split.categoryId] ?? split.categoryId }];
    });
    if (categoryIds && (!entry.categoryId || !categoryIds.includes(entry.categoryId))) return [];
    return [{ entry, scheduled }];
  });
}

const money = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
const signedMoney = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR', signDisplay: 'exceptZero' });
const POPOVER_WIDTH = 500;
const MARGIN = 8;
const CARET = 12;

type Placement = { left: number; top: number; caret: number; side: 'below' | 'above' | 'centered' };

// Anchors under (or above, when there is no room) the clicked amount cell and clamps inside the viewport.
function place(anchor: HTMLElement | null | undefined, width: number, height: number): Placement {
  const viewportWidth = window.innerWidth, viewportHeight = window.innerHeight;
  const rect = anchor?.getBoundingClientRect();
  if (!rect || (rect.width === 0 && rect.height === 0)) {
    return { left: Math.max(MARGIN, (viewportWidth - width) / 2), top: Math.max(MARGIN, (viewportHeight - height) / 3), caret: 0, side: 'centered' };
  }
  const center = rect.left + rect.width / 2;
  const left = Math.max(MARGIN, Math.min(center - width / 2, viewportWidth - width - MARGIN));
  const below = viewportHeight - rect.bottom - CARET - MARGIN, above = rect.top - CARET - MARGIN;
  const side = height <= below || below >= above ? 'below' : 'above';
  const top = side === 'below' ? rect.bottom + CARET : Math.max(MARGIN, rect.top - CARET - height);
  return { left, top, caret: Math.max(18, Math.min(center - left, width - 18)), side };
}

// The one popup used by every action that lists transactions (Plan activity, upcoming, Long-Term, credit card payment).
// The `credit` variant adds the card summary box and swaps ACCOUNT for CATEGORY.
export function PlanTransactionList({ title = 'Activity', subtitle, variant = 'standard', items, accounts, summary = null, loading = false, error = '', emptyMessage = 'No transactions for this category this month.', anchor, onClose, onRetry, onSelect }: {
  title?: string;
  subtitle?: string;
  variant?: 'standard' | 'credit';
  items: PlanTransactionItem[];
  accounts: { id: string; name: string }[];
  summary?: CreditActivitySummary | null;
  loading?: boolean;
  error?: string;
  emptyMessage?: string;
  anchor?: HTMLElement | null;
  onClose: () => void;
  onRetry?: () => void;
  onSelect: (entry: Entry, scheduled: boolean, splitIndex?: number) => void;
}) {
  const titleId = useId();
  const popover = useRef<HTMLElement>(null);
  const returnFocus = useRef<HTMLElement | null>(anchor ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null));
  const close = useRef(onClose);
  close.current = onClose;
  const [placement, setPlacement] = useState<Placement>({ left: MARGIN, top: MARGIN, caret: 0, side: 'centered' });
  const credit = variant === 'credit';
  const width = Math.min(POPOVER_WIDTH, window.innerWidth - 2 * MARGIN);

  const reposition = useCallback(() => setPlacement(place(anchor, width, popover.current?.offsetHeight ?? 0)), [anchor, width]);
  useLayoutEffect(() => { reposition(); }, [reposition, loading, error, items.length, summary]);
  useEffect(() => {
    window.addEventListener('resize', reposition);
    window.addEventListener('scroll', reposition, true);
    return () => { window.removeEventListener('resize', reposition); window.removeEventListener('scroll', reposition, true); };
  }, [reposition]);

  useEffect(() => {
    const trigger = returnFocus.current;
    popover.current?.focus({ preventScroll: true });
    const outside = (event: MouseEvent) => {
      const target = event.target as Node;
      if (!popover.current?.contains(target) && !anchor?.contains(target)) close.current();
    };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { event.stopPropagation(); close.current(); } };
    document.addEventListener('mousedown', outside);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('mousedown', outside);
      document.removeEventListener('keydown', escape);
      if (trigger?.isConnected) trigger.focus({ preventScroll: true });
    };
  }, [anchor]);

  const columns = credit ? ['DATE', 'PAYEE', 'CATEGORY', 'MEMO', 'AMOUNT'] : ['ACCOUNT', 'DATE', 'PAYEE', 'MEMO', 'AMOUNT'];
  const style = { left: placement.left, top: placement.top, width, '--caret-left': `${placement.caret}px` } as CSSProperties;
  const summaryRow = (label: string, value: number, total = false) => <div className={total ? 'activity-summary-total' : undefined}><dt>{label}</dt><dd>{signedMoney.format(value)}</dd></div>;

  return <section ref={popover} tabIndex={-1} className={`activity-popover activity-popover-${variant} activity-popover-${placement.side}`} style={style} role="dialog" aria-labelledby={titleId}>
    <header><h2 id={titleId}>{title}</h2>{subtitle && <p>{subtitle}</p>}</header>
    {loading ? <p className="activity-state" role="status">Loading transactions…</p> : error ? <div className="activity-state" role="alert"><p>{error}</p>{onRetry && <button type="button" onClick={onRetry}>Retry</button>}</div> : <>
      {credit && summary && <div className="activity-summary" role="group" aria-label="Card activity summary">
        <dl>{summaryRow('Spending', summary.spending)}{summaryRow('Returns', summary.returns)}{summaryRow('Total Spending', summary.totalSpending, true)}</dl>
        <dl>{summaryRow('Funded Spending', summary.fundedSpending)}{summaryRow('Payments & Returns', summary.paymentsAndReturns)}{summaryRow('Total Activity', summary.totalActivity, true)}</dl>
      </div>}
      {items.length === 0 ? <p className="activity-state">{emptyMessage}</p> : <>
        <div className="activity-head" data-variant={variant}>{columns.map(column => <span key={column}>{column}</span>)}</div>
        <div className="activity-rows" data-variant={variant} data-testid="activity-rows">{items.map(({ entry, scheduled, categoryAmount, splitIndex, split, splitCategoryName }) => {
          const account = accounts.find(item => item.id === entry.accountId)?.name ?? 'Account';
          const amount = categoryAmount ?? entry.amount;
          const payee = split?.payee || entry.payee || (entry.transferId ? `Transfer : ${entry.transferAccountName ?? 'account'}` : '—');
          const memo = split?.memo || entry.memo;
          const category = splitCategoryName ?? entry.categoryName ?? '—';
          return <button type="button" className="activity-row" key={`${scheduled ? 'schedule' : 'transaction'}-${entry.id}-${splitIndex ?? 'whole'}`} aria-label={`Open ${split ? `${splitCategoryName} split for ${payee}` : payee} in ${account} account`} onClick={() => onSelect(entry, scheduled, splitIndex)}>
            {!credit && <span title={account}>{account}</span>}
            <span>{entry.date.split('-').reverse().join('-')}</span>
            <span title={payee}>{payee}{scheduled && <small>Upcoming</small>}</span>
            {credit && <span title={category}>{category}</span>}
            <span title={memo}>{memo || '—'}</span>
            <span className="activity-amount">{money.format(amount)}</span>
          </button>;
        })}</div>
      </>}
    </>}
    <footer><button type="button" className="button button-primary" onClick={onClose}>Close</button></footer>
  </section>;
}
