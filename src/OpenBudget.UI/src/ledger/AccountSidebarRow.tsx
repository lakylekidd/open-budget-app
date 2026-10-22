import { type Account, formatMoney } from './api';
import { RegisterIcon } from './AccountRegister';
export function AccountSidebarRow({ account, selected, disabled, onSelect, onEdit }: {account:Account;selected:boolean;disabled:boolean;onSelect:()=>void;onEdit:()=>void}) {
  return <div className={`sidebar-account-row ${selected?'is-selected':''}`}>
    <button className="sidebar-account-edit" aria-label={`Edit ${account.name} account`} disabled={disabled} onClick={onEdit}>{(account.unapprovedCount??0)>0&&<span className="account-dot"/>}<RegisterIcon name="edit" size={15}/></button>
    <button className="sidebar-account-select" aria-label={`${account.name}${account.isClosed?' (Closed)':''} ${formatMoney(account.balance)}`} aria-current={selected?'page':undefined} disabled={disabled} onClick={onSelect}><span>{account.name}{account.isClosed?' (Closed)':''}</span><strong className={account.balance<0?'negative-account-balance':''}>{formatMoney(account.balance)}</strong></button>
  </div>;
}
