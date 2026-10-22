import type { Entry } from './ledger/api';

export type PlanTransactionItem = { entry: Entry; scheduled: boolean; categoryAmount?: number };

export function PlanTransactionList({ title, items, accounts, loading = false, error = '', emptyMessage = 'No transactions for this category this month.', onClose, onRetry, onSelect }: {
  title: string;
  items: PlanTransactionItem[];
  accounts: { id: string; name: string }[];
  loading?: boolean;
  error?: string;
  emptyMessage?: string;
  onClose: () => void;
  onRetry?: () => void;
  onSelect: (entry: Entry, scheduled: boolean) => void;
}) {
  const money = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
  return <div className="plan-transaction-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="plan-transaction-dialog" role="dialog" aria-modal="true" aria-labelledby="plan-transactions-title">
      <header><div><h2 id="plan-transactions-title">{title}</h2><span>{loading ? 'Loading transactions…' : `${items.length} ${items.length === 1 ? 'transaction' : 'transactions'}`}</span></div><button type="button" onClick={onClose} aria-label="Close transactions">×</button></header>
      {loading ? <p className="plan-transaction-state" role="status">Loading transactions…</p> : error ? <div className="plan-transaction-state" role="alert"><p>{error}</p>{onRetry && <button type="button" onClick={onRetry}>Retry</button>}</div> : items.length === 0 ? <p className="plan-transaction-state">{emptyMessage}</p> : <>
        <div className="plan-transaction-head"><span>ACCOUNT</span><span>DATE</span><span>PAYEE</span><span>MEMO</span><span>AMOUNT</span></div>
        <div className="plan-transaction-items">{items.map(({ entry, scheduled, categoryAmount }) => {
          const account = accounts.find(item => item.id === entry.accountId)?.name ?? 'Account';
          const amount = categoryAmount ?? entry.amount;
          return <button type="button" className="plan-transaction-row" key={`${scheduled ? 'schedule' : 'transaction'}-${entry.id}`} aria-label={`Open ${entry.payee || 'transaction'} in ${account} account`} onClick={() => onSelect(entry, scheduled)}>
            <span title={account}>{account}</span><span>{entry.date.split('-').reverse().join('-')}</span><span title={entry.payee}>{entry.payee || (entry.transferId ? `Transfer to ${entry.transferAccountName ?? 'account'}` : '—')}{scheduled && <small>Upcoming</small>}</span><span title={entry.memo}>{entry.memo || '—'}</span><span className={amount < 0 ? 'negative' : ''}>{money.format(amount)}</span>
          </button>;
        })}</div>
      </>}
      <footer><button type="button" className="button button-primary" onClick={onClose}>Close</button></footer>
    </section>
  </div>;
}
