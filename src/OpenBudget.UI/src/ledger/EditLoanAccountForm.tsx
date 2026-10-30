import { useEffect, useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { type Account, type LoanAccountType, loanAccountTypes, loanCategoryId, parseMoney, requestJson } from './api';
import { ContractFields, contractDraft, parseContract } from './LoanAccountForm';
import { type LoanDetails, type LoanTerms, parseRate } from './loans';

// Edit Account for mortgages, loans and contracts. Saving sends the account fields, the working balance (an adjustment
// transaction is created for a change) and the loan terms in one versioned account update.
export function EditLoanAccountForm({ account, busy, onSubmit, onCancel, onOpenCategory }: {
  account: Account; busy: boolean; onSubmit: (fields: object) => Promise<void>; onCancel: () => void; onOpenCategory?: (categoryId: string) => void;
}) {
  const [details, setDetails] = useState<LoanDetails | null>(null);
  const [loadError, setLoadError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    requestJson<LoanDetails>(`/api/accounts/${account.id}/loan`, { signal: controller.signal })
      .then(setDetails).catch(error => { if (error.name !== 'AbortError') setLoadError('Could not load the loan details. Close and reopen to retry.'); });
    return () => controller.abort();
  }, [account.id]);
  if (loadError) return <div className="edit-account-form"><p role="alert">{loadError}</p><div className="edit-account-actions"><button type="button" onClick={onCancel}>Cancel</button></div></div>;
  if (!details) return <p role="status">Loading loan details…</p>;
  return <LoanEditor account={account} details={details} busy={busy} onSubmit={onSubmit} onCancel={onCancel} onOpenCategory={onOpenCategory} />;
}

function LoanEditor({ account, details, busy, onSubmit, onCancel, onOpenCategory }: {
  account: Account; details: LoanDetails; busy: boolean; onSubmit: (fields: object) => Promise<void>; onCancel: () => void; onOpenCategory?: (categoryId: string) => void;
}) {
  const isContract = account.accountType === 'contract';
  const [name, setName] = useState(account.name);
  const [notes, setNotes] = useState(account.notes ?? '');
  const [balance, setBalance] = useState(account.balance.toFixed(2));
  const [type, setType] = useState(account.accountType as LoanAccountType);
  const [payment, setPayment] = useState(details.minimumPayment.toFixed(2));
  const [rate, setRate] = useState(String(details.interestRate));
  const [escrow, setEscrow] = useState(details.escrowAmount ? details.escrowAmount.toFixed(2) : '');
  const [contract, setContract] = useState(contractDraft(details.contract));
  const [paired, setPaired] = useState(details.pairedCategory);
  const [unpaired, setUnpaired] = useState(false);
  const [error, setError] = useState('');
  const pairedCategory = unpaired ? null : paired;

  async function save() {
    setError('');
    const workingBalance = parseMoney(balance);
    if (workingBalance === null) { setError('Enter today’s balance with at most two decimal places.'); return; }
    const pairing = pairedCategory ? pairedCategory.isAutomatic ? { pairedCategoryId: null, createPairedCategory: true } : { pairedCategoryId: pairedCategory.id, createPairedCategory: false }
      : { pairedCategoryId: null, createPairedCategory: false };
    let loan: LoanTerms;
    if (isContract) {
      const parsed = parseContract(contract);
      if (typeof parsed === 'string') { setError(parsed); return; }
      loan = { interestRate: 0, minimumPayment: 0, escrowAmount: 0, contract: parsed, ...pairing };
    } else {
      const minimum = parseMoney(payment), interest = parseRate(rate), fee = escrow.trim() === '' ? 0 : parseMoney(escrow);
      if (minimum === null || minimum <= 0) { setError('Enter the minimum monthly payment with at most two decimal places.'); return; }
      if (interest === null) { setError('Enter an interest rate between 0 and 100 with at most three decimals.'); return; }
      if (fee === null || fee < 0 || fee >= minimum) { setError('The escrow or fee amount must be zero or more and less than the monthly payment.'); return; }
      loan = { interestRate: interest, minimumPayment: minimum, escrowAmount: fee, contract: null, ...pairing };
    }
    await onSubmit({ name, notes, isClosed: account.isClosed, accountType: type, workingBalance, loan });
  }

  return <form className="edit-account-form edit-loan-form" onSubmit={e => { e.preventDefault(); void save(); }}>
    <div className="edit-loan-scroll">
      <h3>Account Information</h3>
      <label>Account Nickname<input required maxLength={120} value={name} disabled={busy} onChange={e => setName(e.target.value)} /></label>
      <label>Account Notes<textarea maxLength={1000} value={notes} disabled={busy} onChange={e => setNotes(e.target.value)} /></label>
      <section className="edit-account-balance"><h3>Today’s Balance</h3>
        <MoneyInput aria-label="Today's Balance" required value={balance} disabled={busy || account.isClosed} onChange={setBalance} />
        <p>An adjustment transaction will be created automatically if you change this amount.</p></section>
      <section className="edit-account-balance"><h3>{isContract ? 'Contract Details' : 'Loan Details'}</h3>
        {isContract ? <ContractFields draft={contract} onChange={setContract} disabled={busy || account.isClosed} /> : <>
          <label>Loan Type<select value={type} disabled={busy} onChange={e => setType(e.target.value as LoanAccountType)}>{loanAccountTypes.filter(item => item.value !== 'contract').map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
          <div className="loan-field-row">
            <label>Minimum Monthly Payment<MoneyInput value={payment} disabled={busy || account.isClosed} onChange={setPayment} /></label>
            <label className="loan-rate">Interest Rate (%)<input inputMode="decimal" value={rate} disabled={busy || account.isClosed} onChange={e => setRate(e.target.value)} /></label>
          </div>
          <label>Monthly Escrow or Fee Amount<MoneyInput value={escrow} placeholder="0.00" disabled={busy || account.isClosed} onChange={setEscrow} /></label>
          <p>This would only apply if your bank collects your property taxes on behalf of your city or if it collects other fees as part of your payment.</p>
        </>}
      </section>
      <section className="edit-account-balance"><h3>Paired Plan Category</h3>
        <p>Pairing a category helps you create realistic payoff plans, and keep track of your progress.</p>
        <button type="button" className="loan-pair-button" disabled={busy || account.isClosed} onClick={() => {
          if (pairedCategory) setUnpaired(true);
          else { setUnpaired(false); if (!paired) setPaired({ id: loanCategoryId(account.id), name, groupName: 'Loans', available: 0, isArchived: false, isAutomatic: true, targetKind: null, targetMoreNeeded: null }); }
        }}>{pairedCategory ? 'Unpair Category' : 'Pair Category'}</button>
        {pairedCategory ? <p>Paired with <button type="button" className="loan-link" onClick={() => onOpenCategory?.(pairedCategory.id)}>“{pairedCategory.name}”</button></p>
          : <p>Not paired. Payments to this account reduce Ready to Assign until you pair a category.</p>}
      </section>
      <section className="edit-account-balance"><h3>Bank Connection</h3>
        <p>New transactions are unable to import into loan accounts.</p>
        <button type="button" className="button-primary loan-learn-more" onClick={() => setError('Loan accounts are tracked manually: record payments as transfers and use Update Balance to match your lender.')}>Learn More</button>
      </section>
      {error && <p role="alert">{error}</p>}
    </div>
    <div className="edit-account-actions"><button className="account-close-button" type="button" disabled={busy} onClick={() => void onSubmit({ name, notes, isClosed: !account.isClosed })}>{account.isClosed ? 'Reopen Account' : 'Close Account'}</button><button type="button" disabled={busy} onClick={onCancel}>Cancel</button><button className="button-primary" disabled={busy}>Save</button></div>
  </form>;
}
