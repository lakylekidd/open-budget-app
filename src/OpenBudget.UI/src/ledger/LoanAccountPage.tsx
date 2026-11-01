import { useEffect, useRef, useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { type Account, accountTypeLabel, formatMoney, parseMoney, requestJson } from './api';
import { LedgerDialog } from './LedgerDialog';
import { LoanChart } from './LoanChart';
import { ActivityList, type ActivityTarget } from './LoanActivityList';
import { EditChargeDialog, EditInterestDialog, EditPaymentDialog } from './LoanActivityDialogs';
import { RegisterIcon } from './AccountRegister';
import './loans.css';
import { type LoanDetails, type LoanSimulation, type PairedCategory, addMonths, durationLabel, monthShortName, percentLabel, shortMonthLabel } from './loans';

type Mutate = (path: string, method: string, fields: object) => Promise<boolean>;

const bannerKey = 'openbudget.loanSimulatorBanner.dismissed';
const readDismissed = () => { try { return window.localStorage.getItem(bannerKey) === '1'; } catch { return false; } };

// Loan, mortgage and contract account page (replaces the transaction register for these accounts). All numbers come
// from GET /api/accounts/{id}/loan; the simulator only reads; balance updates and edits go through the ledger mutate path.
export function LoanAccountPage({ account, accounts, refresh, disabled, onMenu, onEdit, onAddActivity, onMutate, onOpenCategory, onCreateTarget }: {
  account: Account; accounts: Account[]; refresh: number; disabled: boolean; onMenu: () => void; onEdit: () => void; onAddActivity: () => void;
  onMutate: Mutate; onOpenCategory: (categoryId: string) => void; onCreateTarget: (categoryId: string) => void;
}) {
  const [details, setDetails] = useState<LoanDetails | null>(null);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const [simulatorOpen, setSimulatorOpen] = useState(false);
  const [updateOpen, setUpdateOpen] = useState(false);
  const [bannerDismissed, setBannerDismissed] = useState(readDismissed);
  const [progressOpen, setProgressOpen] = useState(true);
  const [categoryOpen, setCategoryOpen] = useState(true);
  const [editing, setEditing] = useState<ActivityTarget | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setError('');
    requestJson<LoanDetails>(`/api/accounts/${account.id}/loan`, { signal: controller.signal })
      .then(value => { if (!controller.signal.aborted) setDetails(value); })
      .catch(err => { if (err.name !== 'AbortError') setError('Could not load this loan. ' + err.message); });
    return () => controller.abort();
  }, [account.id, refresh, reload, account.balance]);
  if (error) return <section className="loan-page"><div role="alert" className="ledger-error">{error} <button type="button" onClick={() => setReload(n => n + 1)}>Retry</button></div></section>;
  if (!details || details.accountId !== account.id) return <section className="loan-page"><p role="status" className="ledger-empty">Loading loan…</p></section>;
  const contract = details.contract;
  const debt = Math.max(-details.balance, 0);
  const projection = details.projection;
  const updated = details.balanceConfirmedAt ? new Date(details.balanceConfirmedAt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : 'never';
  function dismissBanner() { setBannerDismissed(true); try { window.localStorage.setItem(bannerKey, '1'); } catch { /* per-browser convenience only */ } }
  return <section className="loan-page" aria-label={`${account.name} loan`}>
    <header className="loan-header">
      <div><h1>{account.name}{account.isClosed && <small>Closed</small>}</h1>
        <div className="loan-meta"><span><RegisterIcon name="bank" size={12} />{accountTypeLabel(account.accountType)}</span><span><RegisterIcon name="lock" size={12} />Updated {updated}</span></div></div>
      <div className="loan-header-actions"><button className="ledger-menu" aria-label="Open menu" onClick={onMenu}>☰</button>
        <button className="loan-edit" aria-label="Edit account" title="Edit account" disabled={disabled} onClick={onEdit}><RegisterIcon name="edit" size={18} /></button>
        <button className="loan-secondary" disabled={disabled || account.isClosed} onClick={() => setUpdateOpen(true)}>Update Balance</button>
        <button className="button-primary" disabled={disabled || account.isClosed} onClick={onAddActivity}>Add Activity</button></div>
    </header>
    <dl className="loan-stats" aria-label="Loan summary">
      <div><dd>{formatMoney(debt)}</dd><dt>Remaining Balance</dt></div>
      {!contract && <div><dd>{percentLabel(details.interestRate)}</dd><dt>Interest Rate</dt></div>}
      <div><dd>{formatMoney(details.minimumPayment)}</dd><dt>{contract ? 'Next Payment' : 'Minimum Payment'}</dt></div>
      <div><dd>{projection.neverPaidOff ? 'Never' : projection.payoffMonth ? shortMonthLabel(projection.payoffMonth) : '—'}</dd><dt>Payoff Date</dt></div>
    </dl>
    <div className="loan-body">
      {!contract && !bannerDismissed && <div className="loan-banner"><span className="loan-banner-icon" aria-hidden="true">▦</span><div><strong>Make a plan to crush your debts with the loan payoff simulator</strong><span>Find out how to save money and achieve your goals faster.</span></div>
        <button className="button-primary" onClick={() => setSimulatorOpen(true)}>Try It Now</button><button className="loan-banner-close" aria-label="Dismiss payoff simulator banner" onClick={dismissBanner}>×</button></div>}
      <section className="loan-card"><button className="loan-card-heading" aria-expanded={progressOpen} onClick={() => setProgressOpen(open => !open)}><span>{contract ? 'Your Contract' : 'Your Loan’s Progress'}</span><Chevron open={progressOpen} /></button>
        {progressOpen && <div className="loan-card-body">
          <div className="loan-progress-head"><div><span>Balance Remaining</span><strong>{new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 }).format(debt)}{details.percentRemaining !== null && <small> ({details.percentRemaining.toFixed(1)}%)</small>}</strong></div>
            {!contract && <div className="loan-progress-links"><button type="button" disabled title="Sharing is not available in this local workspace"><RegisterIcon name="import" size={13} />Share Your Progress</button><button type="button" onClick={() => setSimulatorOpen(true)}><RegisterIcon name="calendar" size={13} />Open Payoff Simulator</button></div>}</div>
          <LoanChart past={details.history} track={projection.track} label={`${account.name} balance: past and projected`} />
          {contract ? <ContractSummary contract={contract} /> : <p className="loan-summary-banner">{projection.neverPaidOff
            ? <>The minimum payment of <b>{formatMoney(details.minimumPayment)}</b> does not cover the monthly interest of <b>{formatMoney(details.estimatedInterestThisMonth)}</b>, so this loan will never be paid off.</>
            : <>The minimum payment of <b>{formatMoney(details.minimumPayment)}</b> will pay off this loan in <b>{durationLabel(projection.paymentsRemaining)}</b></>}</p>}
        </div>}
      </section>
      {contract && <ContractSchedule contract={contract} />}
      <section className="loan-card"><button className="loan-card-heading" aria-expanded={categoryOpen} onClick={() => setCategoryOpen(open => !open)}><span>Paired Plan Category</span><Chevron open={categoryOpen} /></button>
        {categoryOpen && (details.separateDeviceCategory ? <div className="loan-card-body loan-category-slots">
          <PairedCategoryRow title="Contract" category={details.pairedCategory ?? null} onOpenCategory={onOpenCategory} onCreateTarget={onCreateTarget} />
          <PairedCategoryRow title="Device" category={details.devicePairedCategory ?? null} onOpenCategory={onOpenCategory} onCreateTarget={onCreateTarget} />
          <p className="loan-help">Each payment is split between these categories in proportion to that month’s scheduled contract and device amounts.</p>
        </div> : <div className="loan-card-body loan-category">{details.pairedCategory ? <PairedCategoryFields category={details.pairedCategory} onOpenCategory={onOpenCategory} onCreateTarget={onCreateTarget} />
          : <p>No category is paired. Payments into this account reduce Ready to Assign. Pair a category from Edit Account.</p>}</div>)}
      </section>
      <ActivityList months={details.activity} contract={Boolean(contract)} editable={!disabled && !account.isClosed} onEdit={setEditing} />
    </div>
    {simulatorOpen && <PayoffSimulator accountId={account.id} details={details} onClose={() => setSimulatorOpen(false)} onCreateTarget={onCreateTarget} />}
    {updateOpen && <UpdateBalanceDialog balance={details.balance} disabled={disabled} onClose={() => setUpdateOpen(false)}
      onSubmit={async fields => { if (await onMutate(`/api/accounts/${account.id}/loan/balance`, 'POST', fields)) { setUpdateOpen(false); setReload(n => n + 1); } }} />}
    {editing && (() => {
      const save = async (path: string, method: string, fields: object) => { if (await onMutate(path, method, fields)) { setEditing(null); setReload(n => n + 1); return true; } return false; };
      if (editing.kind === 'charge') return <EditChargeDialog account={account} item={editing.item} busy={disabled} onClose={() => setEditing(null)} onSave={save} />;
      if (editing.kind === 'payment') return <EditPaymentDialog account={account} accounts={accounts} item={editing.item} available={details.pairedCategory ? details.pairedCategory.available : null} busy={disabled} onClose={() => setEditing(null)} onSave={save} />;
      return <EditInterestDialog account={account} month={editing.month.month} monthName={shortMonthLabel(editing.month.month)} estimate={editing.month.estimatedInterest} recorded={editing.month.interest} busy={disabled} onClose={() => setEditing(null)} onSave={save} />;
    })()}
  </section>;
}

type CategoryActions = { onOpenCategory: (categoryId: string) => void; onCreateTarget: (categoryId: string) => void };

function PairedCategoryFields({ category, onOpenCategory, onCreateTarget }: CategoryActions & { category: PairedCategory }) {
  return <>
    <div><span>Category</span><button className="loan-link" onClick={() => onOpenCategory(category.id)}>{category.groupName}: {category.name}</button></div>
    <div><span>Available to spend</span><b className={`loan-pill ${category.available < 0 ? 'negative' : category.available > 0 ? 'positive' : ''}`}>{formatMoney(category.available)}</b></div>
    <div><span>Target</span>{category.targetKind ? <button className="loan-link" onClick={() => onCreateTarget(category.id)}>{category.targetMoreNeeded ? `${formatMoney(category.targetMoreNeeded)} more needed` : 'Target funded'}</button>
      : <button className="loan-link" disabled={category.isArchived} onClick={() => onCreateTarget(category.id)}><span className="loan-plus" aria-hidden="true">+</span>Create Debt Payment Target</button>}</div>
  </>;
}

// Contracts with a separate device category show one row per slot (contract, device).
function PairedCategoryRow({ title, category, ...actions }: CategoryActions & { title: string; category: PairedCategory | null }) {
  return <section className="loan-category loan-category-slot" aria-label={`${title} category`}>
    <h3>{title}</h3>
    {category ? <PairedCategoryFields category={category} {...actions} /> : <p>Not paired. This share of each payment reduces Ready to Assign.</p>}
  </section>;
}

function Chevron({ open }: { open: boolean }) {
  return <svg className={`loan-chevron ${open ? 'open' : ''}`} viewBox="0 0 16 16" width="14" height="14" aria-hidden="true"><path d="m4 6 4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function ContractSummary({ contract }: { contract: NonNullable<LoanDetails['contract']> }) {
  return <dl className="loan-contract-summary">
    <div><dt>Remaining device cost</dt><dd>{formatMoney(contract.remainingDevice)}</dd></div>
    <div><dt>Remaining contract cost</dt><dd>{formatMoney(contract.remainingService)}</dd></div>
    <div><dt>Months remaining</dt><dd>{contract.remainingMonths}</dd></div>
    <div><dt>Ends</dt><dd>{new Date(`${contract.endDate}T12:00:00`).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}</dd></div>
    <div><dt>Total cost of contract</dt><dd>{formatMoney(contract.totalCost)}</dd></div>
  </dl>;
}

function ContractSchedule({ contract }: { contract: NonNullable<LoanDetails['contract']> }) {
  return <section className="loan-card"><h2 className="loan-card-title">Contract Schedule</h2>
    <div className="loan-schedule-scroll"><table className="loan-schedule" aria-label="Contract schedule">
      <thead><tr><th>#</th><th>Due</th><th>Contract</th>{contract.includesDevice && <th>Device</th>}<th>Total</th></tr></thead>
      <tbody>{contract.upfrontCost > 0 && <tr className={contract.schedule[0]?.isPast ? 'is-past' : ''}><td>0</td><td>Upfront</td><td colSpan={contract.includesDevice ? 2 : 1} /><td>{formatMoney(contract.upfrontCost)}</td></tr>}
        {contract.schedule.map(item => <tr key={item.number} className={`${item.isDiscounted ? 'is-discounted' : ''} ${item.isPast ? 'is-past' : ''}`}>
          <td>{item.number}</td><td>{new Date(`${item.dueDate}T12:00:00`).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}{item.isDiscounted && <small className="loan-discount-tag">Discount</small>}</td>
          <td>{formatMoney(item.service)}</td>{contract.includesDevice && <td>{formatMoney(item.device)}</td>}<td>{formatMoney(item.total)}</td></tr>)}</tbody>
    </table></div></section>;
}

export function UpdateBalanceDialog({ balance, disabled, onClose, onSubmit }: { balance: number; disabled: boolean; onClose: () => void; onSubmit: (fields: object) => Promise<void> }) {
  const [correcting, setCorrecting] = useState(false);
  const [value, setValue] = useState(balance.toFixed(2));
  const [error, setError] = useState('');
  return <LedgerDialog title="Update Balance" onClose={onClose}><div className="loan-update-balance">
    {!correcting ? <><p>Is your current balance at your lender</p><strong>{formatMoney(balance)} ?</strong>
      <div className="loan-update-actions"><button className="button-primary" disabled={disabled} onClick={() => setCorrecting(true)}>No</button><button className="button-primary" disabled={disabled} onClick={() => void onSubmit({})}>Yes</button></div>
      <details><summary>How do I find my account balance?</summary><p>Look for the outstanding principal or payoff balance on your latest statement or in your lender’s online banking. Enter it as a negative amount because it is money you owe.</p></details></>
      : <form onSubmit={event => { event.preventDefault(); const parsed = parseMoney(value); if (parsed === null) { setError('Enter the balance with at most two decimal places.'); return; } void onSubmit({ balance: parsed }); }}>
        <label>What is the balance at your lender?<MoneyInput autoFocus aria-label="Lender balance" value={value} onChange={setValue} /></label>
        <p className="loan-help">An adjustment transaction records the difference. It changes the debt only, not Ready to Assign.</p>
        {error && <p role="alert">{error}</p>}
        <div className="loan-update-actions"><button type="button" onClick={() => setCorrecting(false)}>Back</button><button className="button-primary" disabled={disabled}>Save Balance</button></div>
      </form>}
  </div></LedgerDialog>;
}

// Month/year grid popover: year arrows and a 4x3 month grid; months before `minimum` are disabled.
export function MonthYearPicker({ value, minimum, onChange, label }: { value: string; minimum: string; onChange: (month: string) => void; label: string }) {
  const [open, setOpen] = useState(false);
  const [year, setYear] = useState(Number(value.slice(0, 4)));
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (event: MouseEvent) => { if (!ref.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);
  return <div className="month-year-picker" ref={ref} onKeyDown={event => { if (event.key === 'Escape' && open) { event.stopPropagation(); setOpen(false); } }}>
    <button type="button" className="month-year-trigger target-input target-select" aria-label={label} aria-haspopup="dialog" aria-expanded={open} onClick={() => { setYear(Number(value.slice(0, 4))); setOpen(o => !o); }}>{shortMonthLabel(value)}</button>
    {open && <div className="month-year-popover" role="dialog" aria-label={`${label} picker`}>
      <div className="month-year-head"><button type="button" aria-label="Previous year" disabled={year <= Number(minimum.slice(0, 4))} onClick={() => setYear(y => y - 1)}>‹</button><strong>{year}</strong><button type="button" aria-label="Next year" disabled={year >= Number(minimum.slice(0, 4)) + 100} onClick={() => setYear(y => y + 1)}>›</button></div>
      <div className="month-year-grid">{Array.from({ length: 12 }, (_, index) => {
        const month = `${year}-${String(index + 1).padStart(2, '0')}`;
        return <button type="button" key={month} aria-pressed={month === value} className={month === value ? 'selected' : ''} disabled={month < minimum} onClick={() => { onChange(month); setOpen(false); }}>{monthShortName(index)}</button>;
      })}</div>
    </div>}
  </div>;
}

// Read-only payoff simulator. Editing the payment recomputes the payoff date; choosing a date solves the payment.
export function PayoffSimulator({ accountId, details, onClose, onCreateTarget }: { accountId: string; details: LoanDetails; onClose: () => void; onCreateTarget?: (categoryId: string) => void }) {
  const minimumMonth = addMonths(details.currentMonth, 1);
  const [payment, setPayment] = useState(details.minimumPayment.toFixed(2));
  const [extra, setExtra] = useState('');
  const [query, setQuery] = useState<{ payment?: number; payoffMonth?: string; extra: number }>({ payment: details.minimumPayment, extra: 0 });
  const [result, setResult] = useState<LoanSimulation | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const params = new URLSearchParams();
    if (query.payoffMonth) params.set('payoffMonth', query.payoffMonth); else params.set('payment', String(query.payment ?? 0));
    if (query.extra) params.set('extra', String(query.extra));
    const timer = window.setTimeout(() => requestJson<LoanSimulation>(`/api/accounts/${accountId}/loan/simulation?${params}`, { signal: controller.signal })
      .then(value => { setResult(value); setError(''); if (query.payoffMonth) setPayment(value.projection.monthlyPayment.toFixed(2)); })
      .catch(err => { if (err.name !== 'AbortError') setError(err.message); }), 250);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [accountId, query]);
  function changePayment(raw: string) {
    setPayment(raw);
    const parsed = parseMoney(raw);
    if (parsed === null || parsed < 0) { setError('Enter a monthly payment with at most two decimal places.'); return; }
    setQuery(current => ({ payment: parsed, extra: current.extra }));
  }
  function changeExtra(raw: string) {
    setExtra(raw);
    const parsed = raw.trim() === '' ? 0 : parseMoney(raw);
    if (parsed === null || parsed < 0) { setError('Enter an extra payment with at most two decimal places.'); return; }
    setQuery(current => ({ ...current, extra: parsed }));
  }
  const projection = result?.projection;
  const baseline = details.projection;
  const atMinimum = !!projection && Math.abs(projection.monthlyPayment - details.minimumPayment) < 0.005 && !query.extra && !query.payoffMonth;
  const changed = !!projection && !atMinimum;
  const extraPerMonth = projection ? Math.max(0, projection.monthlyPayment - details.minimumPayment) : 0;
  const currentMonthName = monthShortName(Number(details.currentMonth.slice(5, 7)) - 1);
  const interestSaved = projection && !projection.neverPaidOff && !baseline.neverPaidOff ? baseline.interestRemaining - projection.interestRemaining : null;
  const monthsSaved = projection && projection.paymentsRemaining !== null && baseline.paymentsRemaining !== null ? baseline.paymentsRemaining - projection.paymentsRemaining : null;
  const savingsTitle = !projection ? '' : extraPerMonth > 0.005 && query.extra ? `Paying +${formatMoney(extraPerMonth)} every month and ${formatMoney(query.extra)} extra in ${currentMonthName}`
    : extraPerMonth > 0.005 ? `Paying +${formatMoney(extraPerMonth)} extra every month` : query.extra ? `Paying ${formatMoney(query.extra)} extra in ${currentMonthName}` : `Paying ${formatMoney(projection.monthlyPayment)} every month`;
  function reset() {
    setPayment(details.minimumPayment.toFixed(2)); setExtra(''); setError('');
    setQuery({ payment: details.minimumPayment, extra: 0 });
  }
  function copyToPlan() {
    const id = details.pairedCategory?.id;
    if (!id || !onCreateTarget) return;
    onClose(); onCreateTarget(id);
  }
  return <LedgerDialog title="Loan Payoff Simulator" onClose={onClose}><div className="loan-simulator">
    <div className="loan-simulator-inputs">
      <div className="loan-simulator-minimum"><strong>{formatMoney(details.minimumPayment)}</strong><span>Required Minimum Payment</span></div>
      <label>Monthly Payment<MoneyInput value={payment} onChange={changePayment} aria-label="Monthly Payment" shellClassName="loan-sim-money" className="loan-sim-input" /></label>
      {details.pairedCategory && onCreateTarget && <button type="button" className="loan-link loan-sim-copy" onClick={copyToPlan}>Copy and go to my plan</button>}
      <div className="loan-simulator-field"><span>Payoff Date</span>
        <MonthYearPicker label="Payoff Date" minimum={minimumMonth} value={projection?.payoffMonth ?? query.payoffMonth ?? minimumMonth}
          onChange={month => setQuery(current => ({ payoffMonth: month, extra: current.extra }))} /></div>
      <p className="loan-help">{projection?.neverPaidOff ? 'This payment never pays off the loan.' : !projection ? '' : changed && baseline.payoffMonth ? `Your original payoff date is ${shortMonthLabel(baseline.payoffMonth)}` : `You have ${projection.paymentsRemaining} payments remaining on your loan.`}</p>
      <label>One time extra in {currentMonthName}<MoneyInput value={extra} placeholder="0.00" onChange={changeExtra} aria-label="One time extra payment" shellClassName="loan-sim-money" className="loan-sim-input" /></label>
      <p className="loan-help">Extra one-off payments can have a large impact on your loan.</p>
      {changed && <button type="button" className="loan-link loan-sim-reset" onClick={reset}><span aria-hidden="true">✕</span> Reset all changes</button>}
    </div>
    <div className="loan-simulator-results">
      {error && <p role="alert" className="loan-error">{error}</p>}
      {!projection ? <p role="status">Calculating…</p> : <>
        <LoanChart past={details.history} track={baseline.track} simulation={changed ? projection.track : null} label="Simulated loan balance" />
        <div className={`loan-simulator-summary${changed ? ' saving' : ''}`}>
          {changed ? <>
            <h3>{savingsTitle}</h3>
            <div><span><strong>{interestSaved === null ? '—' : formatMoney(interestSaved)}</strong>Interest Savings</span><span><strong>{monthsSaved === null ? '—' : durationLabel(Math.max(monthsSaved, 0))}</strong>Time Savings</span></div></> : <>
            <h3>Paying the minimum every month</h3>
            <div><span><strong>{projection.neverPaidOff ? '—' : formatMoney(projection.interestRemaining)}</strong>Interest Remaining</span><span><strong>{projection.neverPaidOff ? 'Never' : durationLabel(projection.paymentsRemaining)}</strong>Time Remaining</span></div></>}
        </div>
        <dl className="loan-simulator-totals"><div className="total"><dt>Remaining to Pay</dt><dd>{projection.neverPaidOff ? '—' : formatMoney(projection.remainingToPay)}</dd></div>
          <div><dt>Principal</dt><dd>{formatMoney(projection.principalRemaining)}</dd></div><div><dt>Interest</dt><dd>{projection.neverPaidOff ? '—' : formatMoney(projection.interestRemaining)}</dd></div></dl>
        <p className="loan-help">Estimates use the current balance and rate, monthly interest rounded to the cent, and escrow/fees excluded from principal. Nothing is saved.</p>
      </>}
    </div>
    <div className="loan-simulator-footer"><button className="button-primary" onClick={onClose}>Done</button></div>
  </div></LedgerDialog>;
}
