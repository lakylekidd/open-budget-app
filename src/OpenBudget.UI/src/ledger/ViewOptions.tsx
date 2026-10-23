import { useEffect, useRef, useState } from 'react';
import { today } from './api';

export type ColumnWidths = Record<string, number>;
export type ViewOptions = { version: number; datePreset: string; fromMonth: string | null; toMonth: string | null; showReconciled: boolean; showRunningBalance: boolean; showFlags: boolean; showImages: boolean; columnWidths: ColumnWidths };
export const defaultColumnWidths: ColumnWidths = { check: 36, info: 32, flag: 32, image: 44, date: 130, payee: 200, category: 230, memo: 180, outflow: 120, inflow: 120, balance: 120, status: 40 };
export const defaultView: ViewOptions = { version: 0, datePreset: 'allDates', fromMonth: null, toMonth: null, showReconciled: true, showRunningBalance: true, showFlags: true, showImages: true, columnWidths: {} };
export const presets = [['thisMonth','This Month'],['lastMonth','Last Month'],['latest3Months','Latest 3 Months'],['thisYear','This Year'],['lastYear','Last Year'],['allDates','All Dates']] as const;
const month = (year: number, index: number) => `${year}-${String(index + 1).padStart(2, '0')}`;
export function presetMonths(preset: string, now = today()) {
  const [year, n] = now.split('-').map(Number); const index = n - 1;
  const offset = (delta: number) => { const date = new Date(year, index + delta, 1); return month(date.getFullYear(), date.getMonth()); };
  switch (preset) {
    case 'thisMonth': return [offset(0), offset(0)];
    case 'lastMonth': return [offset(-1), offset(-1)];
    case 'latest3Months': return [offset(-2), offset(0)];
    case 'thisYear': return [month(year, 0), month(year, 11)];
    case 'lastYear': return [month(year - 1, 0), month(year - 1, 11)];
    default: return [null, null];
  }
}
export function viewDates(value: ViewOptions, now = today()) {
  const [from, to] = value.datePreset === 'custom' ? [value.fromMonth, value.toMonth] : presetMonths(value.datePreset, now);
  if (!from || !to) return { from: '', to: '' };
  const [year, n] = to.split('-').map(Number);
  return { from: `${from}-01`, to: `${to}-${new Date(year, n, 0).getDate()}` };
}
export function ViewOptionsPanel({ value, busy, onApply, onCancel, onReload }: { value: ViewOptions; busy: boolean; onApply: (next: ViewOptions) => Promise<void>; onCancel: () => void; onReload: () => Promise<void> }) {
  const panel = useRef<HTMLElement>(null);
  useEffect(() => { const previous = document.activeElement as HTMLElement | null; panel.current?.focus(); return () => previous?.focus(); }, []);
  const [draft, setDraft] = useState(value); const [error, setError] = useState('');
  const months = ['January','February','March','April','May','June','July','August','September','October','November','December'];
  function selectPreset(datePreset: string) { const [fromMonth, toMonth] = presetMonths(datePreset); setDraft({ ...draft, datePreset, fromMonth: fromMonth ?? draft.fromMonth, toMonth: toMonth ?? draft.toMonth }); }
  function range(side: 'fromMonth' | 'toMonth') {
    const [year, n] = (draft[side] ?? today().slice(0,7)).split('-');
    return <><select aria-label={side === 'fromMonth' ? 'From month' : 'To month'} value={n} onChange={e => setDraft({ ...draft, fromMonth: draft.fromMonth ?? today().slice(0,7), toMonth: draft.toMonth ?? today().slice(0,7), [side]: `${year}-${e.target.value}`, datePreset: 'custom' })}>{months.map((label, i) => <option key={label} value={String(i+1).padStart(2,'0')}>{label}</option>)}</select>
      <select aria-label={side === 'fromMonth' ? 'From year' : 'To year'} value={year} onChange={e => setDraft({ ...draft, fromMonth: draft.fromMonth ?? today().slice(0,7), toMonth: draft.toMonth ?? today().slice(0,7), [side]: `${e.target.value}-${n}`, datePreset:'custom' })}>{[...new Set([...Array.from({length:Math.max(2100, new Date().getFullYear()+20)-1899},(_,i)=>String(1900+i)), year])].sort().map(y => <option key={y} value={y}>{y}</option>)}</select></>;
  }
  return <section ref={panel} tabIndex={-1} className="register-view-panel view-options-panel" aria-label="Register view options" onKeyDown={e => { if (e.key === 'Escape' && !busy) onCancel(); }}>
    <form onSubmit={async e => { e.preventDefault(); if (draft.datePreset === 'custom' && String(draft.fromMonth) > String(draft.toMonth)) { setError('Choose a From month on or before the To month.'); return; } setError(''); try { await onApply(draft); } catch (err) { setError(err instanceof Error ? err.message : 'Could not save view options.'); } }}>
      <h2>View Options</h2><fieldset disabled={busy}><div className="view-presets">{presets.map(([id,label]) => <button key={id} type="button" aria-pressed={draft.datePreset === id} onClick={() => selectPreset(id)}>{label}</button>)}</div>
      <div className="view-month-range"><span className="view-month-bound"><strong>From:</strong>{range('fromMonth')}</span><span className="view-month-bound"><strong>To:</strong>{range('toMonth')}</span></div>
      <div className="view-checkboxes">{([['showReconciled','Show Reconciled Transactions'],['showRunningBalance','Show Running Balance'],['showFlags','Show Flags'],['showImages','Show Images']] as const).map(([key,label]) => <label key={key}><input type="checkbox" checked={draft[key]} onChange={e => setDraft({ ...draft, [key]:e.target.checked })}/>{label}</label>)}</div></fieldset>
      {error && <div role="alert" className="ledger-error">{error}<button type="button" disabled={busy} onClick={async () => { try { await onReload(); onCancel(); } catch { setError('Could not reload saved options. Try again.'); } }}>Reload saved options</button></div>}
      <div className="view-options-footer"><button type="button" disabled={busy} onClick={onCancel}>Cancel</button><button className="button-primary" disabled={busy}>{busy ? 'Saving…' : 'Apply'}</button></div>
    </form>
  </section>;
}
