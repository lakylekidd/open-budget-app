import { useState } from 'react';
import { ApprovalButton } from './ApprovalButton';
import { FlagButton, defaultFlags, type Flag } from './Flags';
import { defaultView, type ViewOptions } from './ViewOptions';
import { type Account, type CategoryOption, type Entry, accountTypeLabel, formatMoney } from './api';

export function RegisterIcon({ name, size = 16 }: { name: string; size?: number }) {
  const paths: Record<string, React.ReactNode> = {
    lock: <><rect x="6" y="10" width="12" height="11" rx="1.5" fill="currentColor" stroke="none" /><path d="M8 10V7a4 4 0 0 1 8 0v3" /></>,
    edit: <><path d="m5 16-1 4 4-1L20 7l-3-3L5 16Z"/><path d="m15 6 3 3"/></>,
    search: <><circle cx="10" cy="10" r="6.5"/><path d="m15 15 5 5"/></>,
    flag: <path d="M4 21V4h15l-4 5 4 5H4"/>,
    image: <><rect x="3" y="5" width="18" height="14"/><circle cx="9" cy="10" r="2"/><path d="m4 18 5-5 4 3 4-5 4 5"/></>,
    calendar: <><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 2v6M17 2v6M3 11h18"/></>,
    transfer: <><path d="M4 7h15l-4-4M20 17H5l4 4M19 7l-4 4M5 17l4-4"/></>,
    import: <><path d="M5 2h10l4 4v16H5V2Z"/><path d="M15 2v5h4M8 14h8m-3-3 3 3-3 3"/></>,
    check: <path d="m4 12 5 5L20 5"/>,
    bank: <><rect x="3" y="7" width="18" height="10"/><circle cx="12" cy="12" r="3"/></>,
  };
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>;
}

export function balanceSummary(accounts: Account[]) {
  const sum = (field: 'balance' | 'clearedBalance' | 'unclearedBalance') => accounts.reduce((total, a) => total + Math.round(a[field] * 100), 0) / 100;
  return { working: sum('balance'), cleared: sum('clearedBalance'), uncleared: sum('unclearedBalance') };
}

export function reconciliationText(date: string | null, now = Date.now()) {
  if (!date) return 'Not reconciled';
  const days = Math.max(0, Math.floor((now - Date.parse(date)) / 86400000));
  if (days === 0) return 'Reconciled today';
  if (days === 1) return 'Reconciled yesterday';
  if (days < 30) return `Reconciled ${days} days ago`;
  const months = Math.floor(days / 30);
  return `Reconciled ${months === 1 ? 'a month' : `${months} months`} ago`;
}

export function AccountHeader({ current, accounts, disabled, loading = false, onEdit, onReconcile, onFavorite, onAddAccount, onMenu }: {
  current?: Account; accounts: Account[]; disabled: boolean; onEdit: () => void; onReconcile: () => void;
  onFavorite: () => void; onAddAccount: () => void; onMenu: () => void; loading?: boolean;
}) {
  const totals = balanceSummary(current ? [current] : accounts);
  return <>
    <header className="account-register-header"><div className="account-register-identity">
      <h1>{loading ? 'Loading account…' : current ? current.name : 'All Accounts'}{current && <button className={`account-favorite ${current.isFavorite ? 'is-favorite' : ''}`} disabled={disabled} aria-label={current.isFavorite ? 'Remove favorite account' : 'Favorite account'} aria-pressed={current.isFavorite} onClick={onFavorite}>★</button>}{current?.isClosed && <small>Closed</small>}</h1>
      <div className="account-register-meta"><span><RegisterIcon name="bank" size={12}/>{current ? accountTypeLabel(current.accountType) : 'Cash accounts'}</span>
        {current && <><span title={current.connectionStatus === 'connected' ? 'Automatically connected and synced' : 'Transactions are entered manually; automatic bank sync is not connected.'}><RegisterIcon name="check" size={13}/>{current.connectionStatus === 'connected' ? 'Connected' : 'Unlinked'}</span>
          <span><RegisterIcon name="lock" size={12}/>{reconciliationText(current.lastReconciledAt)}</span></>}
      </div></div>
      <div className="account-register-controls"><button className="ledger-menu" aria-label="Open menu" onClick={onMenu}>☰</button>
        {current ? <><button className="account-edit" aria-label="Edit account" title="Edit account" disabled={disabled} onClick={onEdit}><RegisterIcon name="edit" size={22}/></button><button className="button-primary" disabled={disabled || current.isClosed} onClick={onReconcile}>Reconcile</button></> : <button className="button-primary" disabled={disabled} onClick={onAddAccount}>Add account</button>}
      </div>
    </header>
    <section className="account-balance-equation" aria-label="Account balance summary">
      <div className="balance-term" data-testid="cleared-balance"><strong className={totals.cleared < 0 ? 'negative-balance' : 'cleared-total'}>{loading ? '—' : formatMoney(totals.cleared)}</strong><span><span aria-hidden="true" className="cleared-symbol small-symbol">c</span> Cleared Balance</span></div><b className="balance-operator">+</b>
      <div className="balance-term" data-testid="uncleared-balance"><strong className={totals.uncleared < 0 ? 'negative-balance' : ''}>{loading ? '—' : formatMoney(totals.uncleared)}</strong><span><span aria-hidden="true" className="uncleared-symbol small-symbol">c</span> Uncleared Balance</span></div><b className="balance-operator">=</b>
      <div className="balance-term" data-testid="working-balance"><strong className={totals.working < 0 ? 'negative-balance' : ''}>{loading ? '—' : formatMoney(totals.working)}</strong><span>Working Balance</span></div>
    </section>
  </>;
}

export function ClearedIndicator({ entry, disabled, onToggle }: { entry: Entry; disabled: boolean; onToggle: () => void }) {
  const label = entry.status === 'reconciled' ? 'Reconciled transaction (locked)' : entry.status === 'cleared' ? 'Cleared transaction: mark uncleared' : 'Uncleared transaction: mark cleared';
  return <button className={`transaction-status status-${entry.status}`} aria-label={label} title={label} disabled={disabled || entry.status === 'reconciled'} onClick={onToggle}>
    {entry.status === 'reconciled' ? <RegisterIcon name="lock" size={13}/> : <span aria-hidden="true" className={entry.status === 'cleared' ? 'cleared-symbol' : 'uncleared-symbol'}>c</span>}
  </button>;
}

export function TransactionRow({ view = defaultView, entry, account, categories, selected, disabled, onSelect, onEdit, onToggle, scheduled = false, paused = false, flags = defaultFlags, onFlag, onRenameFlags, onRowSelect, onReview, focusedSplitIndex }: {
  view?: ViewOptions; entry: Entry; account?: Account; categories: CategoryOption[]; selected: boolean; disabled: boolean;
  onSelect: () => void; onEdit: () => void; onToggle: () => void; scheduled?: boolean; paused?: boolean; onRowSelect?:()=>void; onReview?:(action:'approve'|'reject')=>Promise<boolean>; flags?:Flag[]; onFlag?:(color:string|null)=>Promise<boolean>; onRenameFlags?:(flags:Flag[])=>Promise<boolean>; focusedSplitIndex?:number;
}) {
  const needsCategory = entry.needsCategory ?? (entry.needsAttention && entry.amount < 0 && !entry.isOpening && !entry.transferId);
  const locked = disabled || entry.canEdit === false || account?.isClosed;
  const name = entry.transferId ? `Transfer: ${entry.transferAccountName ?? 'Cash account'}` : entry.isOpening ? 'Starting Balance' : entry.payee || '—';
  const [splitsExpanded, setSplitsExpanded] = useState(true);
  return <><tr data-transaction-id={entry.id} tabIndex={0} aria-selected={selected} onClick={event => { if (!(event.target as HTMLElement).closest('button,input,select,a')) { if (selected && !locked) onEdit(); else (onRowSelect??onSelect)(); } }} onKeyDown={event => { if (event.target === event.currentTarget && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); if (selected && !locked) onEdit(); else (onRowSelect??onSelect)(); } }} className={`${scheduled ? 'register-scheduled-row' : ''} ${selected ? 'register-row-selected' : ''} ${entry.status === 'reconciled' ? 'register-row-reconciled' : ''}`}>
    <td className="register-check-cell"><input type="checkbox" checked={selected} aria-label={`Select transaction ${entry.id}`} onChange={onSelect}/></td>
    <td className="register-info-cell">{entry.isApproved === false && onReview && <ApprovalButton disabled={disabled} onReview={onReview}/>}</td>
    {view.showFlags && <td className="register-decoration">{onFlag&&onRenameFlags?<FlagButton color={entry.flagColor} flags={flags} disabled={disabled} onSet={onFlag} onRename={onRenameFlags}/>:<RegisterIcon name="flag" size={15}/>}</td>}{view.showImages && <td className="register-decoration"><RegisterIcon name="image" size={15}/></td>}
    <td className="register-date">{entry.date.split('-').reverse().join('-')}{scheduled && <span className="schedule-repeat-icon" title="Scheduled transaction">↻</span>}</td>
    <td className="register-payee"><button disabled={locked} aria-label={`Edit ${entry.payee || (entry.transferId ? 'transfer' : 'transaction')} ${entry.date}`} onClick={() => selected ? onEdit() : (onRowSelect??onSelect)()} title={account?.name}>{name}{paused && <small className="schedule-paused">Paused</small>}</button></td>
    <td className="register-category">{entry.transferId ? <span className="transfer-category"><RegisterIcon name="transfer" size={13}/>Category not needed</span> : entry.splits.length ? <span className="split-summary"><button type="button" className="split-toggle" aria-label={`${splitsExpanded?'Collapse':'Expand'} split categories`} aria-expanded={splitsExpanded} onClick={()=>setSplitsExpanded(expanded=>!expanded)}><svg className={splitsExpanded?'split-chevron is-expanded':'split-chevron'} viewBox="0 0 20 20" aria-hidden="true"><path d="m7 4 6 6-6 6"/></svg></button><span>Split (Multiple Categories)...</span></span> : entry.categoryId ? entry.categoryName ?? categories.find(c => c.id === entry.categoryId)?.name ?? entry.categoryId : needsCategory ? <span className="needs-category">Needs category</span> : 'Inflow: Ready to Assign'}</td>
    <td className="register-memo" title={entry.memo}>{entry.memo}</td>
    <td className="register-money">{entry.amount < 0 ? formatMoney(Math.abs(entry.amount)) : ''}</td><td className="register-money">{entry.amount > 0 ? formatMoney(entry.amount) : ''}</td>
    {view.showRunningBalance && <td className={`register-money register-running-balance ${(entry.runningBalance ?? 0) < 0 ? 'negative-balance' : ''}`}>{entry.runningBalance === null ? '—' : formatMoney(entry.runningBalance)}</td>}
    <td className="register-status-cell">{!scheduled && <ClearedIndicator entry={entry} disabled={disabled || Boolean(account?.isClosed)} onToggle={onToggle}/>}</td>
  </tr>{splitsExpanded&&entry.splits.map((split,index)=>{const category=categories.find(c=>c.id===split.categoryId);return <tr key={`${entry.id}-split-${index}`} data-transaction-id={entry.id} data-split-index={index} className={`register-split-detail-row ${selected?'register-row-selected':''} ${focusedSplitIndex===index?'register-split-focused':''}`} aria-label={`Split ${index+1}`} onDoubleClick={()=>{if(!locked)onEdit();}}>
    <td/><td/>{view.showFlags&&<td/>}{view.showImages&&<td/>}<td/>
    <td/><td className="register-category split-detail-label"><span>{category?.groupName?`${category.groupName}: `:''}{category?.name??split.categoryId}{split.payee?` · ${split.payee}`:''}</span></td><td className="register-memo" title={split.memo??''}>{split.memo}</td>
    <td className="register-money">{split.amount<0?formatMoney(Math.abs(split.amount)):''}</td><td className="register-money">{split.amount>0?formatMoney(split.amount):''}</td>{view.showRunningBalance&&<td/>}<td/>
  </tr>;})}</>;
}
