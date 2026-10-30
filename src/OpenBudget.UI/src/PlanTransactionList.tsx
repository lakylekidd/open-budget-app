import type { Entry, Split } from './ledger/api';

export type PlanTransactionItem = { entry: Entry; scheduled: boolean; categoryAmount?: number; splitIndex?: number; split?: Split; splitCategoryName?: string };

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

export function PlanTransactionList({ title, items, accounts, loading = false, error = '', emptyMessage = 'No transactions for this category this month.', onClose, onRetry, onSelect }: {
  title: string;
  items: PlanTransactionItem[];
  accounts: { id: string; name: string }[];
  loading?: boolean;
  error?: string;
  emptyMessage?: string;
  onClose: () => void;
  onRetry?: () => void;
  onSelect: (entry: Entry, scheduled: boolean, splitIndex?: number) => void;
}) {
  const money = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
  return <div className="plan-transaction-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="plan-transaction-dialog" role="dialog" aria-modal="true" aria-labelledby="plan-transactions-title">
      <header><div><h2 id="plan-transactions-title">{title}</h2><span>{loading ? 'Loading transactions…' : `${items.length} ${items.length === 1 ? 'transaction' : 'transactions'}`}</span></div><button type="button" onClick={onClose} aria-label="Close transactions">×</button></header>
      {loading ? <p className="plan-transaction-state" role="status">Loading transactions…</p> : error ? <div className="plan-transaction-state" role="alert"><p>{error}</p>{onRetry && <button type="button" onClick={onRetry}>Retry</button>}</div> : items.length === 0 ? <p className="plan-transaction-state">{emptyMessage}</p> : <>
        <div className="plan-transaction-head"><span>ACCOUNT</span><span>DATE</span><span>PAYEE</span><span>CATEGORY</span><span>MEMO</span><span>AMOUNT</span></div>
        <div className="plan-transaction-items">{items.map(({ entry, scheduled, categoryAmount, splitIndex, split, splitCategoryName }) => {
          const account = accounts.find(item => item.id === entry.accountId)?.name ?? 'Account';
          const amount = categoryAmount ?? entry.amount;
          const payee = split?.payee || entry.payee || (entry.transferId ? `Transfer to ${entry.transferAccountName ?? 'account'}` : '—');
          const memo = split?.memo || entry.memo;
          return <button type="button" className="plan-transaction-row" key={`${scheduled ? 'schedule' : 'transaction'}-${entry.id}-${splitIndex ?? 'whole'}`} aria-label={`Open ${split ? `${splitCategoryName} split for ${payee}` : payee} in ${account} account`} onClick={() => onSelect(entry, scheduled, splitIndex)}>
            <span title={account}>{account}</span><span>{entry.date.split('-').reverse().join('-')}</span><span title={payee}>{payee}{scheduled && <small>Upcoming</small>}</span><span title={splitCategoryName}>{splitCategoryName ?? entry.categoryName ?? '—'}</span><span title={memo}>{memo || '—'}</span><span className={amount < 0 ? 'negative' : ''}>{money.format(amount)}</span>
          </button>;
        })}</div>
      </>}
      <footer><button type="button" className="button button-primary" onClick={onClose}>Close</button></footer>
    </section>
  </div>;
}
