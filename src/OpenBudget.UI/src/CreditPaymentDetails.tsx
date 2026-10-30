import { useEffect, useRef, useState } from 'react';
import { ApiError, type Account, type CreditSummary, requestJson } from './ledger/api';
import { type CreditPayoff, PayoffTargetEditor, PayoffTargetSummary, type PayoffTargetFields } from './CreditPayoffTarget';

// Payment category details for one credit account: how much more must be assigned to cover the
// card's current balance and how that balance changed during the selected month. A debt payoff target, when
// saved, replaces the Payment card with server-calculated progress for the selected month.
export function CreditPaymentDetails({ account, month, available, money, onClose, onChanged }: {
  account: Account; month: string; available: number; money: (amount: number) => string; onClose: () => void; onChanged: () => void;
}) {
  const [summary, setSummary] = useState<CreditSummary | null>(null);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const [balanceOpen, setBalanceOpen] = useState(true);
  const [whyOpen, setWhyOpen] = useState(false);
  const [payoff, setPayoff] = useState<CreditPayoff | null>(null);
  const [payoffError, setPayoffError] = useState('');
  const [payoffRefresh, setPayoffRefresh] = useState(0);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [conflict, setConflict] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  // Retain the original command across lost-response retries so the server can replay it safely.
  const pending = useRef<{ signature: string; path: string; method: string; fields: object; body: string } | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setSummary(null); setError('');
    requestJson<CreditSummary>(`/api/accounts/${account.id}/credit-summary/${month}`, { signal: controller.signal })
      .then(setSummary).catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Could not load the card balance.'); });
    return () => controller.abort();
  }, [account.id, account.balance, month, retry]);
  useEffect(() => {
    const controller = new AbortController();
    setPayoffError('');
    requestJson<CreditPayoff>(`/api/accounts/${account.id}/payoff-target/${month}`, { signal: controller.signal })
      .then(setPayoff).catch(err => { if (!controller.signal.aborted) setPayoffError(err instanceof Error ? err.message : 'Could not load the debt payoff target.'); });
    return () => controller.abort();
  }, [account.id, account.balance, month, available, payoffRefresh]);
  async function mutate(path: string, method: string, fields: object) {
    if (busy || conflict || !payoff) return;
    const signature = method + path + JSON.stringify(fields);
    if (uncertain && pending.current?.signature !== signature) { setSaveError('Retry the previous save first to resolve its outcome.'); return; }
    if (pending.current?.signature !== signature) pending.current = { signature, path, method, fields, body: JSON.stringify({ ...fields, commandId: crypto.randomUUID(), version: payoff.version }) };
    setBusy(true); setSaveError('');
    try {
      await requestJson(path, { method, headers: { 'Content-Type': 'application/json' }, body: pending.current.body });
      pending.current = null; setUncertain(false); setEditing(false);
      setPayoffRefresh(value => value + 1); onChanged();
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Could not save the target. Retry the same action safely.');
      if (err instanceof ApiError && err.status < 500) { pending.current = null; setUncertain(false); if (err.status === 409) setConflict(true); }
      else setUncertain(true);
    } finally { setBusy(false); }
  }
  const save = (fields: PayoffTargetFields) => mutate(`/api/accounts/${account.id}/payoff-target`, 'PUT', fields);
  const remove = () => mutate(`/api/accounts/${account.id}/payoff-target/delete`, 'POST', {});
  function reloadTarget() { setConflict(false); setSaveError(''); setPayoffRefresh(value => value + 1); }
  function cancelEdit() { if (busy || uncertain) return; setEditing(false); setSaveError(''); setConflict(false); }
  const target = payoff?.target ?? null;
  const feedback = (saveError || uncertain) && <div className="credit-target-feedback" role="alert">
    {saveError} {conflict && <button type="button" onClick={reloadTarget}>Reload target</button>}
    {uncertain && <>The save outcome is unknown. <button type="button" disabled={busy} onClick={() => { const command = pending.current; if (command) void mutate(command.path, command.method, command.fields); }}>Retry save</button></>}
  </div>;
  const owed = Math.max(-account.balance, 0);
  const underfunded = Math.max(Math.round((owed - Math.max(available, 0)) * 100) / 100, 0);
  const funded = underfunded === 0;
  const progress = owed === 0 ? 100 : Math.min(Math.max(available, 0) / owed * 100, 100);
  return <section className="credit-payment-details" aria-label={`${account.name} payment`}>
    <header><strong><span aria-hidden="true">💳</span> {account.name}</strong><button type="button" aria-label="Close category details" onClick={onClose}>×</button></header>
    {editing ? <><PayoffTargetEditor key={target?.updatedAt ?? 'new'} target={target} busy={busy || conflict} onSave={save} onCancel={cancelEdit} onDelete={remove} />{feedback}</>
      : target && payoff?.progress ? <><PayoffTargetSummary target={target} progress={payoff.progress} money={money} onEdit={() => { setSaveError(''); setEditing(true); }} />{feedback}</>
      : <><div className="credit-payment-card">
      <strong>Payment</strong>
      <div className={`credit-payment-status ${funded ? 'funded' : 'underfunded'}`}><span aria-hidden="true">{funded ? '✓' : '⚠'}</span>{funded ? 'Funded' : 'Underfunded'}</div>
      <div className="credit-payment-progress" role="progressbar" aria-label="Payment funded" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress)}><span className={funded ? 'funded' : ''} style={{ width: `${progress}%` }} /></div>
      {funded
        ? <p>{owed === 0 ? 'This card has no balance to pay.' : <>You’ve assigned enough to pay off your current balance.</>}</p>
        : <><p>You need to <b>assign {money(underfunded)} more</b> to pay off your current balance.</p>
          <div className="credit-payment-divider"><span>Don’t have more money to assign?</span></div>
          <button type="button" className="credit-payment-target" disabled={!payoff} onClick={() => { setSaveError(''); setEditing(true); }}>Create a Debt Payoff Target</button>
          {payoffError && <p role="alert" className="credit-target-feedback">{payoffError} <button type="button" onClick={reloadTarget}>Retry</button></p>}</>}
    </div>
    {!funded && <button type="button" className="credit-payment-why" aria-expanded={whyOpen} onClick={() => setWhyOpen(value => !value)}>Why is my payment underfunded?</button>}
    {whyOpen && !funded && <p className="credit-payment-explanation">Card spending moves only the money available in its category into this payment category. Existing debt and spending from unfunded categories are not covered until you assign money here.</p>}
    {!funded && <div className="credit-payment-row"><strong>Total Underfunded</strong><strong>{money(underfunded)}</strong></div>}</>}
    <div className="credit-payment-balance">
      <button type="button" aria-expanded={balanceOpen} onClick={() => setBalanceOpen(value => !value)}><strong>Current Balance <span className={`credit-chevron ${balanceOpen ? 'open' : ''}`} aria-hidden="true">⌄</span></strong><strong>{money(summary?.currentBalance ?? account.balance)}</strong></button>
      {balanceOpen && (error ? <p role="alert">{error} <button type="button" onClick={() => setRetry(value => value + 1)}>Retry</button></p> : !summary ? <p role="status">Loading card balance…</p> : <dl>
        <div><dt>Prior Balance</dt><dd>{money(summary.priorBalance)}</dd></div>
        <div className="credit-activity-heading"><dt>Activity This Month</dt></div>
        <div><dt>Spending and Outflows</dt><dd>{money(summary.spendingAndOutflows)}</dd></div>
        <div><dt>Interest Charges</dt><dd>{money(summary.interestCharges)}</dd></div>
        <div><dt>Payments and Inflows</dt><dd>{money(summary.paymentsAndInflows)}</dd></div>
      </dl>)}
    </div>
  </section>;
}
