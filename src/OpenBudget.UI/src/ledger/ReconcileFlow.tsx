import { useEffect, useRef, useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { formatMoney, parseMoney } from './api';

export type ReconcileStep = { step: 'ask' } | { step: 'input' } | { step: 'verify'; statement: number };

// Reconcile starts from the cleared balance: "Is your current balance X?". Yes finishes; No asks for the bank balance.
export function ReconcilePopover({ clearedBalance, step, busy, onYes, onNo, onNext, onClose }: {
  clearedBalance: number; step: 'ask' | 'input'; busy: boolean;
  onYes: () => void; onNo: () => void; onNext: (statement: number) => void; onClose: () => void;
}) {
  const root = useRef<HTMLDivElement>(null);
  const [raw, setRaw] = useState('0.00'); const [help, setHelp] = useState(false);
  const amount = parseMoney(raw);
  useEffect(() => {
    function outside(event: MouseEvent) { if (!root.current?.contains(event.target as Node) && !(event.target as Element).closest?.('.reconcile-anchor')) onClose(); }
    document.addEventListener('mousedown', outside);
    return () => document.removeEventListener('mousedown', outside);
  }, [onClose]);
  return <div ref={root} className="reconcile-popover" role="dialog" aria-label="Reconcile account" onKeyDown={event => { if (event.key === 'Escape') { event.stopPropagation(); onClose(); } }}>
    {step === 'ask' ? <>
      <div className="reconcile-balance-card"><p>Is your current balance</p><strong>{formatMoney(clearedBalance)}?</strong></div>
      <button type="button" className="reconcile-help" aria-expanded={help} onClick={() => setHelp(value => !value)}>How do I find my account balance?</button>
      {help && <p className="reconcile-help-text">Open your bank's app or website, or your latest statement, and look at the balance it shows right now. Compare it with the cleared balance above.</p>}
      <div className="reconcile-actions">
        <button type="button" className="button-primary" disabled={busy} onClick={onNo}>No</button>
        <button type="button" className="button-primary" disabled={busy} autoFocus onClick={onYes}>Yes</button>
      </div>
    </> : <form onSubmit={event => { event.preventDefault(); if (amount !== null) onNext(amount); }}>
      <label className="reconcile-label">Current balance at your bank
        <MoneyInput autoFocus required value={raw} onChange={setRaw} />
      </label>
      <button type="button" className="reconcile-help" aria-expanded={help} onClick={() => setHelp(value => !value)}>How do I find my account balance?</button>
      {help && <p className="reconcile-help-text">Open your bank's app or website, or your latest statement, and look at the balance it shows right now.</p>}
      <div className="reconcile-actions reconcile-actions-end"><button type="submit" className="button-primary" disabled={busy || amount === null}>Next</button></div>
    </form>}
  </div>;
}

// Shown after the bank balance is entered and it differs from the cleared balance: confirms the adjustment that will be created.
export function ReconcileAdjustmentBanner({ difference, busy, onCancel, onConfirm }: { difference: number; busy: boolean; onCancel: () => void; onConfirm: () => void }) {
  return <div className="reconcile-banner" role="status">
    <span>This account's cleared balance in OpenBudget is <strong>{formatMoney(Math.abs(difference))}</strong> {difference > 0 ? 'lower' : 'higher'} than your actual account.</span>
    <span className="reconcile-banner-actions">
      <button type="button" disabled={busy} onClick={onCancel}>Cancel</button>
      <button type="button" className="button-primary" disabled={busy} onClick={onConfirm}>Create Adjustment &amp; Finish</button>
    </span>
  </div>;
}
