import { useEffect, useId, useRef, useState } from 'react';
import { MoneyInput } from './MoneyInput';
import { parseMoney } from './ledger/api';

export type PayoffKind = 'payByDate' | 'monthlyAmount';
export type PayoffTarget = { kind: PayoffKind; targetMonth: string | null; monthlyAmount: number | null; payoffAmount: number; createdAt: string; updatedAt: string };
export type PayoffProgress = { neededForPayoff: number; availableForPayment: number; assignedThisMonth: number; toGo: number; percent: number;
  assignThisMonth: number; monthsRemaining: number | null; isPastDue: boolean; status: 'funded' | 'onTrack' | 'behind' };
export type CreditPayoff = { accountId: string; month: string; version: number; target: PayoffTarget | null; progress: PayoffProgress | null };
export type PayoffTargetFields = { kind: PayoffKind; targetMonth: string | null; monthlyAmount: number | null };

const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const kindLabels: Record<PayoffKind, string> = { payByDate: 'Pay Off Balance by Date', monthlyAmount: 'Pay Specific Amount Monthly' };
const yearsShown = 10;

export const currentMonth = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`; };
export const shortMonth = (month: string) => `${monthNames[Number(month.slice(5, 7)) - 1].slice(0, 3)} ${month.slice(0, 4)}`;
const monthKey = (year: number, monthNumber: number) => `${year}-${String(monthNumber).padStart(2, '0')}`;

// Editor for a credit card payment category's debt payoff target. Months before the current calendar month
// cannot be chosen; the server repeats every check.
export function PayoffTargetEditor({ target, busy, onSave, onCancel, onDelete }: {
  target: PayoffTarget | null; busy: boolean; onSave: (fields: PayoffTargetFields) => void; onCancel: () => void; onDelete: () => void;
}) {
  const minimum = currentMonth();
  const minimumYear = Number(minimum.slice(0, 4));
  const initial = target?.targetMonth ?? monthKey(minimumYear + 1, Number(minimum.slice(5, 7)));
  const [kind, setKind] = useState<PayoffKind>(target?.kind ?? 'payByDate');
  const [year, setYear] = useState(Number(initial.slice(0, 4)));
  const [monthNumber, setMonthNumber] = useState(Number(initial.slice(5, 7)));
  const [amount, setAmount] = useState(target?.monthlyAmount ? target.monthlyAmount.toFixed(2) : '');
  const [typeOpen, setTypeOpen] = useState(false);
  const [minimumNote, setMinimumNote] = useState(false);
  const [error, setError] = useState('');
  const typeRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const years = Array.from({ length: Math.max(yearsShown, year - minimumYear + 1) }, (_, index) => minimumYear + index);
  useEffect(() => {
    if (!typeOpen) return;
    const close = (event: MouseEvent) => { if (!typeRef.current?.contains(event.target as Node)) setTypeOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [typeOpen]);
  function choose(next: PayoffKind) { setKind(next); setTypeOpen(false); setError(''); }
  function submit() {
    if (kind === 'payByDate') {
      const targetMonth = monthKey(year, monthNumber);
      if (targetMonth < minimum) { setError('Choose this month or a later month.'); return; }
      onSave({ kind, targetMonth, monthlyAmount: null });
      return;
    }
    const parsed = parseMoney(amount);
    if (parsed === null || parsed <= 0) { setError('Enter a positive monthly amount with at most two decimals.'); return; }
    onSave({ kind, targetMonth: null, monthlyAmount: parsed });
  }
  return <form className="credit-target-card credit-target-editor" aria-label="Debt payoff target" onSubmit={event => { event.preventDefault(); submit(); }}
    onKeyDown={event => { if (event.key === 'Escape' && typeOpen) { event.stopPropagation(); setTypeOpen(false); } }}>
    <strong className="credit-target-title">Target</strong>
    <div className="credit-target-fields">
      <div className="credit-target-type" ref={typeRef}>
        <button type="button" className="credit-target-select" aria-label="Target type" aria-haspopup="listbox" aria-expanded={typeOpen} aria-controls={typeOpen ? listId : undefined}
          disabled={busy} onClick={() => setTypeOpen(value => !value)}>{kindLabels[kind]}</button>
        {typeOpen && <div className="credit-target-popover">
          <ul id={listId} role="listbox" aria-label="Target type">
            {(Object.keys(kindLabels) as PayoffKind[]).map(option => <li key={option} role="option" aria-selected={kind === option} tabIndex={0}
              onClick={() => choose(option)} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose(option); } }}>
              <span className={`credit-target-option-icon ${option}`} aria-hidden="true">{kind === option ? '✓' : option === 'payByDate' ? '◷' : '▦'}</span>{kindLabels[option]}
            </li>)}
          </ul>
          <button type="button" className="credit-target-info" aria-expanded={minimumNote} onClick={() => setMinimumNote(value => !value)}><span aria-hidden="true">i</span>Making minimum payments?</button>
          {minimumNote && <p className="credit-target-note">Assign at least your card’s minimum payment each month. Choose Pay Specific Amount Monthly to plan a fixed payment, or Pay Off Balance by Date to clear the debt by a deadline.</p>}
        </div>}
      </div>
      {kind === 'payByDate'
        ? <div className="credit-target-date">
          <select aria-label="Target month" value={monthNumber} disabled={busy} onChange={event => { setMonthNumber(Number(event.target.value)); setError(''); }}>
            {monthNames.map((name, index) => <option key={name} value={index + 1} disabled={monthKey(year, index + 1) < minimum}>{name}</option>)}
          </select>
          <select aria-label="Target year" value={year} disabled={busy} onChange={event => { setYear(Number(event.target.value)); setError(''); }}>
            {years.map(value => <option key={value} value={value}>{value}</option>)}
          </select>
        </div>
        : <label className="credit-target-amount">Amount per month<MoneyInput value={amount} disabled={busy} placeholder="0.00" onChange={raw => { setAmount(raw); setError(''); }} /></label>}
      {error && <p role="alert" className="credit-target-error">{error}</p>}
    </div>
    <div className="credit-target-actions">
      {target && <button type="button" className="credit-target-delete" disabled={busy} onClick={onDelete}><svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M9 2h6l1 2h5v2H3V4h5l1-2Zm-4 6h14l-1.2 13.1A2 2 0 0 1 15.8 23H8.2a2 2 0 0 1-2-1.9L5 8Z"/></svg> Delete</button>}
      <button type="button" className="credit-target-cancel" disabled={busy} onClick={onCancel}>Cancel</button>
      <button type="submit" className="credit-target-save" disabled={busy}>Save Target</button>
    </div>
  </form>;
}

export function ProgressRing({ percent, tone, size = 84 }: { percent: number; tone: 'behind' | 'good'; size?: number }) {
  const radius = size / 2 - 6;
  const circumference = 2 * Math.PI * radius;
  const clamped = Math.min(Math.max(percent, 0), 100);
  return <div className={`credit-target-ring ${tone}`} role="progressbar" aria-label="Target progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={clamped} style={{ width: size, height: size }}>
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true">
      <circle className="track" cx={size / 2} cy={size / 2} r={radius} />
      <circle className="arc" cx={size / 2} cy={size / 2} r={radius} strokeDasharray={`${circumference * clamped / 100} ${circumference}`} transform={`rotate(-90 ${size / 2} ${size / 2})`} />
    </svg>
    <span>{clamped}%</span>
  </div>;
}

// Saved target view: server-calculated progress for the Plan month being viewed.
export function PayoffTargetSummary({ target, progress, money, onEdit }: {
  target: PayoffTarget; progress: PayoffProgress; money: (amount: number) => string; onEdit: () => void;
}) {
  const [open, setOpen] = useState(true);
  const good = progress.status !== 'behind';
  return <section className="credit-target-card credit-target-summary" aria-label="Debt payoff target">
    <button type="button" className="credit-target-toggle" aria-expanded={open} onClick={() => setOpen(value => !value)}>
      <span aria-hidden="true">◷</span> Target <span className={`credit-chevron ${open ? 'open' : ''}`} aria-hidden="true">⌄</span>
    </button>
    {open && <div className="credit-target-body">
      <strong>{kindLabels[target.kind]}</strong>
      <div className="credit-target-goal">
        {target.kind === 'payByDate'
          ? <><b>Pay off {money(target.payoffAmount)}</b><span>By {shortMonth(target.targetMonth ?? '')}</span></>
          : <><b>Pay {money(target.monthlyAmount ?? 0)} monthly</b><span>Until the balance is paid off</span></>}
      </div>
      <ProgressRing percent={progress.percent} tone={good ? 'good' : 'behind'} />
      <p className={`credit-target-banner ${good ? 'good' : 'behind'}`} role="status">
        {progress.status === 'funded' ? 'Funded — you have enough available to pay off this card.'
          : progress.status === 'onTrack' ? 'On track — you have assigned enough this month.'
          : <>Assign <mark>{money(progress.assignThisMonth)}</mark> this month {progress.isPastDue ? 'to catch up on your overdue target' : target.kind === 'payByDate' ? 'to stay on track' : 'to meet your target'}</>}
      </p>
      <dl>
        <div><dt>Needed for Payoff</dt><dd>{money(progress.neededForPayoff)}</dd></div>
        <div><dt>Available for Payment</dt><dd>{money(progress.availableForPayment)}</dd></div>
        <div className="credit-target-total"><dt>To Go</dt><dd>{money(progress.toGo)}</dd></div>
      </dl>
      <button type="button" className="credit-target-edit" onClick={onEdit}>Edit Target</button>
    </div>}
  </section>;
}
