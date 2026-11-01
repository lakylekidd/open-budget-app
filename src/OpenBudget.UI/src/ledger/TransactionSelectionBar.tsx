import { useEffect, useRef, useState } from 'react';
import { type Account, type Entry } from './api';
import { FlagButton, type Flag } from './Flags';
import { RegisterIcon } from './AccountRegister';
import { LedgerDialog } from './LedgerDialog';
import { repeatOptions } from './TransactionDatePicker';

export type SelectionAction={action:string;status?:string;color?:string|null;memo?:string;accountId?:string;repeat?:string;confirmReconciledEdit?:boolean};
function csvText(value:string){return '"'+(/^[\s]*[=+@-]/.test(value)?"'"+value:value).replaceAll('"','""')+'"';}
export function selectedTransactionsCsv(entries:Entry[],accounts:Account[]){return '\uFEFF'+[
  'Date,Account,Payee,Category,Memo,Outflow,Inflow,Status,Flag',
  ...entries.map(t=>[csvText(t.date),csvText(accounts.find(a=>a.id===t.accountId)?.name??''),csvText(t.transferId?t.payee||'Transfer: '+(t.transferAccountName??''):t.payee),csvText(t.categoryName??t.categoryId??''),csvText(t.memo),t.amount<0?Math.abs(t.amount).toFixed(2):'',t.amount>0?t.amount.toFixed(2):'',csvText(t.status),csvText(t.flagColor??'')].join(','))
].join('\r\n')+'\r\n';}

export function TransactionSelectionBar({entries,accounts,flags,disabled,scheduled,allScheduled,onClear,onApply,onRenameFlags}:{entries:Entry[];accounts:Account[];flags:Flag[];disabled:boolean;scheduled:boolean;allScheduled:boolean;onClear:()=>void;onApply:(action:SelectionAction)=>Promise<boolean>;onRenameFlags:(flags:Flag[])=>Promise<boolean>}){
  const [open,setOpen]=useState(false);const [move,setMove]=useState(false);const [dialog,setDialog]=useState<'memo'|'repeat'|'delete'|null>(null);const [destination,setDestination]=useState<string|null>(null);
  const [memo,setMemo]=useState(entries.length===1?entries[0].memo:'');const [repeat,setRepeat]=useState('monthly');const [confirmed,setConfirmed]=useState(false);const [error,setError]=useState('');
  const root=useRef<HTMLDivElement>(null);const trigger=useRef<HTMLButtonElement>(null);
  const locked=entries.some(t=>t.isLocked||t.status==='reconciled');const opening=entries.some(t=>t.isOpening);const transfer=entries.some(t=>t.transferId);
  const blocked=disabled||scheduled||entries.some(t=>t.canEdit===false||accounts.find(a=>a.id===t.accountId)?.isClosed);
  const canDelete=allScheduled?!disabled&&!entries.some(t=>accounts.find(a=>a.id===t.accountId)?.isClosed):!blocked&&!locked;
  const targetStatus=entries.every(t=>t.status==='cleared')?'uncleared':'cleared';const label=`${entries.length} ${entries.length===1?'Transaction':'Transactions'}`;
  const needsWarning=locked&&(dialog==='memo'||destination!==null);
  useEffect(()=>{if(!open)return;const outside=(event:PointerEvent)=>{if(!root.current?.contains(event.target as Node)){setOpen(false);setMove(false);}};document.addEventListener('pointerdown',outside);return()=>document.removeEventListener('pointerdown',outside);},[open]);
  useEffect(()=>{if(open)root.current?.querySelector<HTMLButtonElement>('[role=menuitem]:not(:disabled)')?.focus();},[open]);
  async function apply(action:SelectionAction){const ok=await onApply(action);if(ok){setOpen(false);setDialog(null);setDestination(null);}return ok;}
  function showDialog(value:'memo'|'repeat'|'delete'){setOpen(false);setMove(false);setConfirmed(false);setError('');setDialog(value);}
  function closeDialog(){if(!disabled){setDialog(null);setDestination(null);setError('');}}
  useEffect(()=>{function shortcut(event:KeyboardEvent){
    if(disabled||dialog||destination||document.querySelector('[role=dialog],.transaction-calendar,.flag-picker,.payee-picker,.category-picker'))return;
    if((event.target as HTMLElement).closest('input,textarea,select,[contenteditable=true]')||event.ctrlKey||event.metaKey||event.altKey)return;
    if(event.key==='Escape'){if(open){setOpen(false);setMove(false);trigger.current?.focus();}else onClear();return;}
    if(event.key==='Delete'&&canDelete){event.preventDefault();showDialog('delete');return;}
    if(blocked)return;
    if(event.key.toLowerCase()==='c'&&!event.shiftKey&&!locked){event.preventDefault();void apply({action:'status',status:targetStatus});}
    if(event.shiftKey&&event.key.toLowerCase()==='d'&&!opening){event.preventDefault();void apply({action:'duplicate'});}
    if(event.shiftKey&&event.key.toLowerCase()==='t'&&!opening){event.preventDefault();showDialog('repeat');}
  }document.addEventListener('keydown',shortcut);return()=>document.removeEventListener('keydown',shortcut);});
  function exportCsv(){const url=URL.createObjectURL(new Blob([selectedTransactionsCsv(entries,accounts)],{type:'text/csv;charset=utf-8'}));const link=document.createElement('a');link.href=url;link.download='openbudget-selected-transactions.csv';document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);setOpen(false);}
  async function submit(){setError('');if(needsWarning&&!confirmed){setError('Confirm the warning before saving reconciled transactions.');return;}await apply(destination?{action:'move',accountId:destination,confirmReconciledEdit:confirmed}:dialog==='memo'?{action:'memo',memo,confirmReconciledEdit:confirmed}:dialog==='repeat'?{action:'repeat',repeat}:{action:'delete'});}
  function menuKey(event:React.KeyboardEvent){
    if(event.key==='Escape'){event.stopPropagation();setOpen(false);setMove(false);trigger.current?.focus();}
    if(!['ArrowDown','ArrowUp','Home','End'].includes(event.key))return;
    event.preventDefault();const buttons=Array.from(event.currentTarget.querySelectorAll<HTMLButtonElement>('[role=menuitem]:not(:disabled)'));const index=buttons.indexOf(document.activeElement as HTMLButtonElement);
    buttons[event.key==='Home'?0:event.key==='End'?buttons.length-1:(index+(event.key==='ArrowUp'?-1:1)+buttons.length)%buttons.length]?.focus();
  }
  return <>
    <div className="transaction-selection-bar" ref={root} role="region" aria-label="Selected transactions">
      <button type="button" className="selection-count" disabled={disabled} aria-label="Clear transaction selection" onClick={onClear}><span aria-hidden="true">×</span> {label}</button><span className="selection-divider"/>
      <span className="selection-flag"><FlagButton label="Flag" flags={flags} color={entries.every(t=>t.flagColor===entries[0].flagColor)?entries[0].flagColor:null} disabled={blocked} onSet={color=>apply({action:'flag',color})} onRename={onRenameFlags}/></span>
      <button type="button" disabled title="Available when imported transaction matching is implemented"><span aria-hidden="true">↔</span> Unmatch</button><span className="selection-divider"/>
      <button ref={trigger} type="button" aria-label="More selected transaction actions" aria-haspopup="menu" aria-expanded={open} disabled={disabled} onClick={()=>{setOpen(!open);setMove(false);}} onKeyDown={e=>{if(e.key==='ArrowDown'){e.preventDefault();setOpen(true);requestAnimationFrame(()=>root.current?.querySelector<HTMLButtonElement>('[role=menuitem]:not(:disabled)')?.focus());}}}><span aria-hidden="true">···</span> More</button>
      {open&&<div className="selection-more-menu" role="menu" aria-label="Selected transaction actions" onKeyDown={menuKey}>
        <button role="menuitem" disabled={blocked||locked} onClick={()=>void apply({action:'status',status:targetStatus})}><span className="selection-menu-icon" aria-hidden="true">ⓒ</span>Mark as {targetStatus==='cleared'?'Cleared':'Uncleared'}<kbd aria-hidden="true">C</kbd></button><div className="selection-menu-rule"/>
        <button role="menuitem" disabled title="Available when imported transaction matching is implemented"><span className="selection-menu-icon" aria-hidden="true">↔</span>Match<kbd aria-hidden="true">M</kbd></button>
        <button role="menuitem" disabled title="These transactions have no imported match"><span className="selection-menu-icon" aria-hidden="true">↔</span>Unmatch<kbd aria-hidden="true">shift U</kbd></button><div className="selection-menu-rule"/>
        <button role="menuitem" disabled={blocked||opening} onClick={()=>void apply({action:'duplicate'})}><span className="selection-menu-icon" aria-hidden="true">▣</span>Duplicate<kbd aria-hidden="true">shift D</kbd></button>
        <button role="menuitem" disabled={blocked||opening} onClick={()=>showDialog('repeat')}><RegisterIcon name="transfer"/>Make Repeating<kbd aria-hidden="true">shift T</kbd></button><div className="selection-menu-rule"/>
        <button role="menuitem" disabled={blocked} onClick={()=>showDialog('memo')}><span className="selection-menu-icon" aria-hidden="true">▰</span>Memo…</button><div className="selection-menu-rule"/>
        <button role="menuitem" disabled={blocked||opening||transfer} title={transfer?'Edit a linked transfer to change both accounts together':undefined} aria-expanded={move} onClick={()=>setMove(!move)}><RegisterIcon name="import"/>Move to Account<span className="selection-menu-arrow">›</span></button>
        {move&&<div className="selection-account-menu" role="group" aria-label="Destination accounts">{accounts.filter(a=>!a.isClosed).map(a=><button role="menuitem" key={a.id} disabled={entries.every(t=>t.accountId===a.id)} onClick={()=>{setDestination(a.id);setConfirmed(false);setOpen(false);}}>{a.name}</button>)}</div>}<div className="selection-menu-rule"/>
        <button role="menuitem" disabled={disabled||scheduled} onClick={exportCsv}><RegisterIcon name="import"/>Export {label}</button><div className="selection-menu-rule"/>
        <button role="menuitem" disabled={!canDelete} onClick={()=>showDialog('delete')}><span className="selection-menu-icon" aria-hidden="true">▥</span>Delete<kbd aria-hidden="true">delete</kbd></button>
      </div>}
    </div>
    {(dialog||destination)&&<LedgerDialog title={destination?'Move to Account':dialog==='memo'?'Edit memo':dialog==='repeat'?'Make Repeating':'Delete selected transactions'} onClose={closeDialog}><form className="selection-action-form" onSubmit={e=>{e.preventDefault();void submit();}}>
      {destination?<p>Move {label.toLowerCase()} to {accounts.find(a=>a.id===destination)?.name}? Amounts, dates and categories are preserved.</p>:dialog==='memo'?<label>Memo<textarea aria-label="Memo for selected transactions" maxLength={1000} disabled={disabled} value={memo} onChange={e=>setMemo(e.target.value)}/></label>:dialog==='repeat'?<label>Repeat<select aria-label="Repeat selected transactions" value={repeat} disabled={disabled} onChange={e=>setRepeat(e.target.value)}>{repeatOptions.filter(([id])=>id!=='never').map(([id,name])=><option key={id} value={id}>{name}</option>)}</select></label>:allScheduled?<p>Delete {label.toLowerCase()}? Scheduled transactions stop repeating and no future occurrences are entered. Entered transactions stay. Audit history is retained.</p>:<p>Delete {label.toLowerCase()}? Both sides of any linked transfer will be removed. Audit history is retained.</p>}
      {needsWarning&&<label className="selection-reconcile-warning"><input type="checkbox" checked={confirmed} disabled={disabled} onChange={e=>setConfirmed(e.target.checked)}/>These transactions were reconciled. Saving marks them cleared and requires reconciling again; the edit and previous reconciliation remain in audit history.</label>}
      {error&&<p role="alert">{error}</p>}<div className="selection-form-actions"><button type="button" disabled={disabled} onClick={closeDialog}>Cancel</button><button className="button-primary" disabled={disabled}>{disabled?'Saving…':dialog==='delete'?'Delete':destination?'Move':'Save'}</button></div>
    </form></LedgerDialog>}
  </>;
}
