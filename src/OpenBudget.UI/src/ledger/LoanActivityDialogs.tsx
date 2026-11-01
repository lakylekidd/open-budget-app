import { useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { type Account, formatMoney, parseMoney } from './api';
import { LedgerDialog } from './LedgerDialog';
import { type LoanActivityItem } from './loans';

type Save = (path: string, method: string, fields: object) => Promise<boolean>;

const asInput = (amount: number) => Math.abs(amount).toFixed(2);

function Footer({ remove, removeLabel, busy, onCancel, saveLabel = 'Save' }: { remove?: () => void; removeLabel: string; busy: boolean; onCancel: () => void; saveLabel?: string }) {
  return <div className="loan-dialog-footer">
    {remove && <button type="button" className="loan-danger" disabled={busy} onClick={remove}>{removeLabel}</button>}
    <span className="loan-dialog-spacer" />
    <button type="button" className="loan-secondary" onClick={onCancel}>Cancel</button>
    <button className="button-primary" disabled={busy}>{saveLabel}</button>
  </div>;
}

// Edit Charge: a charge against the loan other than interest (withdrawal, fee, initial balance). Keeps its sign.
export function EditChargeDialog({ account, item, busy, onClose, onSave }: { account: Account; item: LoanActivityItem; busy: boolean; onClose: () => void; onSave: Save }) {
  const [amount, setAmount] = useState(asInput(item.amount));
  const [date, setDate] = useState(item.date);
  const [memo, setMemo] = useState(item.kind === 'opening' ? '' : item.description ?? '');
  const [error, setError] = useState('');
  const sign = item.amount < 0 ? -1 : 1;
  return <LedgerDialog title="Edit Charge" onClose={onClose}>
    <form className="loan-dialog" onSubmit={async event => {
      event.preventDefault(); const parsed = parseMoney(amount);
      if (parsed === null || parsed <= 0) { setError('Enter a positive amount with at most two decimal places.'); return; }
      await onSave(`/api/transactions/${item.id}`, 'PUT', { accountId: account.id, date, amount: sign * parsed, payee: '', memo, categoryId: null, status: 'cleared', splits: [], repeat: 'never', confirmReconciledEdit: false });
    }}>
      <p>This is for charges against your account other than interest or escrow: new withdrawals, late fees, annual fees, etc.</p>
      <div className="loan-dialog-note">Remember to set additional money aside in your plan for payment if you do not want these new charges to carry forward as increased debt.</div>
      <div className="loan-dialog-row">
        <label>Amount<MoneyInput autoFocus aria-label="Amount" value={amount} onChange={setAmount} /></label>
        <label>Date<input type="date" aria-label="Date" required min="1900-01-01" max="9998-12-31" value={date} onChange={event => setDate(event.target.value)} /></label>
      </div>
      <label>Memo<input aria-label="Memo" maxLength={1000} value={memo} onChange={event => setMemo(event.target.value)} /></label>
      {error && <p role="alert">{error}</p>}
      <Footer busy={busy} removeLabel="Delete Charge" remove={() => void onSave(`/api/transactions/${item.id}/delete`, 'POST', {})} onCancel={onClose} />
    </form>
  </LedgerDialog>;
}

// Edit Interest: the month's total interest charge. Saving records it in place of the estimate; Delete returns to the estimate.
export function EditInterestDialog({ account, month, monthName, estimate, recorded, busy, onClose, onSave }: { account: Account; month: string; monthName: string; estimate: number; recorded: LoanActivityItem | null | undefined; busy: boolean; onClose: () => void; onSave: Save }) {
  const [amount, setAmount] = useState(recorded ? asInput(recorded.amount) : estimate.toFixed(2));
  const [error, setError] = useState('');
  const path = `/api/accounts/${account.id}/loan/interest`;
  return <LedgerDialog title="Edit Interest" onClose={onClose}>
    <form className="loan-dialog" onSubmit={async event => {
      event.preventDefault(); const parsed = parseMoney(amount);
      if (parsed === null || parsed < 0) { setError('Enter the interest with at most two decimal places.'); return; }
      await onSave(path, 'POST', { month, amount: parsed });
    }}>
      <p>This is for the total interest charge for {monthName}. Any existing estimate or recorded interest charge will override when you save the changes.</p>
      <label>Interest Amount<MoneyInput autoFocus aria-label="Interest Amount" value={amount} onChange={setAmount} /></label>
      {error && <p role="alert">{error}</p>}
      <Footer busy={busy} removeLabel="Delete Interest" remove={() => void onSave(path, 'POST', { month, amount: 0 })} onCancel={onClose} />
    </form>
  </LedgerDialog>;
}

// Edit Payment: one transfer into the loan. Editing changes both legs; the source account's register shows the same change.
export function EditPaymentDialog({ account, accounts, item, available, busy, onClose, onSave }: { account: Account; accounts: Account[]; item: LoanActivityItem; available: number | null; busy: boolean; onClose: () => void; onSave: Save }) {
  const sources = accounts.filter(a => a.id !== account.id && !a.isClosed || a.id === item.accountId);
  const [from, setFrom] = useState(item.accountId ?? sources[0]?.id ?? '');
  const [amount, setAmount] = useState(asInput(item.amount));
  const [date, setDate] = useState(item.date);
  const [memo, setMemo] = useState(item.description ?? '');
  const [error, setError] = useState('');
  return <LedgerDialog title="Edit Payment" onClose={onClose}>
    <form className="loan-dialog" onSubmit={async event => {
      event.preventDefault(); const parsed = parseMoney(amount);
      if (parsed === null || parsed <= 0) { setError('Enter a positive payment with at most two decimal places.'); return; }
      await onSave(`/api/transfers/${item.id}`, 'PUT', { fromAccountId: from, toAccountId: account.id, date, amount: parsed, memo, repeat: 'never', confirmReconciledEdit: false });
    }}>
      <p>This payment will appear here, and as a transaction in the account your payment is coming from. Editing it here will change it in both places.</p>
      {available !== null && <div className="loan-dialog-note">You have <strong>{formatMoney(available)}</strong> available for payment in <strong>{account.name}</strong>.</div>}
      <label>Payment Amount<MoneyInput autoFocus aria-label="Payment Amount" value={amount} onChange={setAmount} /></label>
      <small>Enter the total amount you are paying. OpenBudget will estimate the portion of each payment that is principal and interest.</small>
      <div className="loan-dialog-row">
        <label>From Account<select aria-label="From Account" value={from} onChange={event => setFrom(event.target.value)}>{sources.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
        <label>Date<input type="date" aria-label="Date" required min="1900-01-01" max="9998-12-31" value={date} onChange={event => setDate(event.target.value)} /></label>
      </div>
      <label>Memo<input aria-label="Memo" maxLength={1000} value={memo} onChange={event => setMemo(event.target.value)} /></label>
      {error && <p role="alert">{error}</p>}
      <Footer busy={busy} removeLabel="Delete Payment" remove={() => void onSave(`/api/transactions/${item.id}/delete`, 'POST', {})} onCancel={onClose} />
    </form>
  </LedgerDialog>;
}
