import { useEffect, useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { type Account, type CategoryOption, type Entry, type Split, isCreditAccount, loadCategoryBudget, parseMoney, requestJson, today } from './api';
import { CategoryPicker, inflowCategoryId } from './CategoryPicker';

export type TransactionFields = { accountId: string; date: string; amount: number; payee: string; memo: string; categoryId: string | null; status: string; splits: Split[] };

export function TransactionForm({ accounts, entry, selectedAccount, busy, onSubmit }: {
  accounts: Account[]; entry?: Entry; selectedAccount: string; busy: boolean; onSubmit: (fields: TransactionFields) => Promise<void>;
}) {
  const [accountId, setAccountId] = useState(entry?.accountId ?? (selectedAccount || accounts.find(a => !a.isClosed)?.id || ''));
  const [date, setDate] = useState(entry?.date ?? today());
  const [amount, setAmount] = useState(String(entry?.amount ?? ''));
  const [payee, setPayee] = useState(entry?.payee ?? '');
  const [memo, setMemo] = useState(entry?.memo ?? '');
  const [categoryId, setCategoryId] = useState(entry?.categoryId ?? (entry && entry.amount > 0 && !entry.transferId && !entry.isOpening && !entry.splits.length ? inflowCategoryId : ''));
  const [status, setStatus] = useState<string>(entry?.status ?? 'uncleared');
  const [splits, setSplits] = useState((entry?.splits ?? []).map(s => ({ categoryId: s.categoryId, amount: String(s.amount) })));
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [readyToAssign, setReadyToAssign] = useState<number | undefined>();
  const [payees, setPayees] = useState<string[]>([]);
  const [categoryState, setCategoryState] = useState('loading');
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setCategoryState('loading');
    loadCategoryBudget(date, controller.signal).then(result => { setCategories(result.categories); setReadyToAssign(result.readyToAssign); setCategoryState('ready'); })
      .catch(error => { if (error.name !== 'AbortError') setCategoryState('error'); });
    return () => controller.abort();
  }, [date]);
  useEffect(() => { requestJson<string[]>('/api/payees').then(setPayees).catch(() => {}); }, []);
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setError('');
    const parsed = parseMoney(amount);
    if (parsed === null || parsed === 0) { setError('Enter a nonzero amount with at most two decimal places.'); return; }
    const parsedSplits = splits.map(s => ({ categoryId: s.categoryId, amount: parseMoney(s.amount) }));
    if (parsedSplits.some(s => !s.categoryId || s.amount === null || s.amount === 0) || (splits.length && Math.round(parsedSplits.reduce((sum, s) => sum + (s.amount ?? 0) * 100, 0)) !== Math.round(parsed * 100))) {
      setError('Choose a category for each split. Split amounts must equal the signed transaction amount.'); return;
    }
    if (credit && !entry?.isOpening && !splits.length && (!categoryId || categoryId === inflowCategoryId)) { setError('Credit account transactions require a category.'); return; }
    await onSubmit({ accountId, date, amount: parsed, payee, memo, categoryId: splits.length || categoryId === inflowCategoryId ? null : categoryId || null, status,
      splits: parsedSplits as Split[] });
  }
  const credit = isCreditAccount(accounts.find(a => a.id === accountId));
  const categoryProps = { categories, readyToAssign, loading: categoryState === 'loading', error: categoryState === 'error' };
  return <form onSubmit={submit} className="ledger-form">
    <p>Use a negative amount for spending and a positive amount for income or refunds.</p>
    <label>Account<select value={accountId} disabled={busy || entry?.isOpening} onChange={e => setAccountId(e.target.value)} required>{accounts.filter(a => !a.isClosed).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
    <label>Date<input type="date" min="1900-01-01" max="9998-12-31" required value={date} onChange={e => setDate(e.target.value)} /></label>
    <label>Amount<MoneyInput required value={amount} onChange={raw => setAmount(raw)} placeholder="-25.00" /></label>
    <label>Payee<input list="ledger-payees" value={payee} maxLength={160} onChange={e => setPayee(e.target.value)} /><datalist id="ledger-payees">{payees.map(p => <option key={p} value={p} />)}</datalist></label>
    {!entry?.isOpening && <>
      {categoryState === 'loading' && <p role="status">Loading categories…</p>}
      {categoryState === 'error' && <p role="alert">Could not load categories. Change the date to retry.</p>}
      {!splits.length && <CategoryPicker {...categoryProps} value={categoryId} onChange={setCategoryId} />}
      <p className="ledger-hint">{credit ? 'Credit spending moves money from the category to this card’s payment category.' : 'Uncategorized income adds to Ready to Assign. Uncategorized spending reduces it until you choose a category.'}</p>
      {splits.map((split, index) => <div className="split-row" key={index}>
        <label>Split {index + 1} category<CategoryPicker {...categoryProps} includeInflow={false} label={`Split ${index + 1} category`} value={split.categoryId} onChange={id => setSplits(items => items.map((item, i) => i === index ? { ...item, categoryId: id } : item))} /></label>
        <label>Split {index + 1} amount<MoneyInput required value={split.amount} onChange={raw => setSplits(items => items.map((item, i) => i === index ? { ...item, amount: raw } : item))} /></label>
        <button type="button" aria-label={`Remove split ${index + 1}`} onClick={() => setSplits(items => items.filter((_, i) => i !== index))}>×</button>
      </div>)}
      <button type="button" disabled={splits.length >= 50} onClick={() => { setCategoryId(''); setSplits(items => [...items, { categoryId: '', amount: '' }]); }}>+ Add split</button>
    </>}
    <label>Memo<input value={memo} maxLength={1000} onChange={e => setMemo(e.target.value)} /></label>
    <label>Status<select value={status} onChange={e => setStatus(e.target.value)}><option value="uncleared">Uncleared</option><option value="cleared">Cleared</option></select></label>
    {error && <p role="alert">{error}</p>}
    <button className="button-primary" disabled={busy || categoryState !== 'ready'} type="submit">{busy ? 'Saving…' : 'Save transaction'}</button>
  </form>;
}
