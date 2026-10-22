import { useEffect, useId, useState } from 'react';
import { createPortal } from 'react-dom';
import { type Account, type CategoryOption, type Entry, type Split, loadCategoryBudget, parseMoney, today } from './api';
import { type ViewOptions } from './ViewOptions';
import { RegisterIcon } from './AccountRegister';
import { TransactionDatePicker } from './TransactionDatePicker';
import { PayeePicker, type SavedPayee } from './Payees';
import { CategoryPicker } from './CategoryPicker';
import { NewCategoryDialog } from './NewCategoryDialog';
export type InlineFields = { accountId:string;date:string;amount:number;payee:string;memo:string;categoryId:string|null;splits:Split[];status:string;repeat:string;toAccountId:string|null;confirmReconciledEdit:boolean;onlyOnce:boolean };
export function parseRegisterMoney(value:string) { return parseMoney(/^\d{1,3}(,\d{3})+(\.\d{1,2})?$/.test(value.trim()) ? value.replaceAll(',','') : value); }
export function InlineTransactionEditor({ entry, selectedAccount, accounts, view, busy, onSave, onCancel, repeat:initialRepeat='never', transferAccountId, scheduled=false, requiresConfirmation=false, postingError, onPayeesChanged=()=>{}, onCategoriesChanged=()=>{}, onPayeesPending,onShowPayeeTransactions }: {
  entry?:Entry;selectedAccount:string;accounts:Account[];view:ViewOptions;busy:boolean;onSave:(fields:InlineFields,another:boolean)=>Promise<boolean>;onCancel:()=>void;repeat?:string;transferAccountId?:string;scheduled?:boolean;requiresConfirmation?:boolean; postingError?:string|null;onPayeesChanged?:()=>void;onCategoriesChanged?:()=>void;onPayeesPending?:(pending:boolean)=>void;onShowPayeeTransactions?:(payee:SavedPayee)=>void;
}) {
  const formId=useId(); const [date,setDate]=useState(entry?.date??today());const [dateValid,setDateValid]=useState(true);const [repeat,setRepeat]=useState(initialRepeat);
  const [accountId,setAccountId]=useState(entry?.accountId??(selectedAccount||accounts.find(a=>!a.isClosed)?.id||''));
  const initialTransfer=entry?.transferId?`${entry.amount<0?'Transfer to':'Transfer from'} ${accounts.find(a=>a.id===transferAccountId)?.name??entry.transferAccountName??''}`:entry?.payee??'';
  const [payee,setPayee]=useState(initialTransfer);const [chosenTransfer,setChosenTransfer]=useState(transferAccountId);const [memo,setMemo]=useState(entry?.memo??'');const [categoryId,setCategory]=useState(entry?.categoryId??'');
  const [outflow,setOutflow]=useState(entry&&entry.amount<0?Math.abs(entry.amount).toFixed(2):'');const [inflow,setInflow]=useState(entry&&entry.amount>0?entry.amount.toFixed(2):'');
  const [status,setStatus]=useState(entry?.status==='cleared'||entry?.status==='reconciled'?'cleared':'uncleared');
  const [splits,setSplits]=useState((entry?.splits??[]).map(s=>({...s,amount:String(s.amount)})));const [categories,setCategories]=useState<CategoryOption[]>([]);
  const [readyToAssign,setReadyToAssign]=useState<number|undefined>();const [categoryRefresh,setCategoryRefresh]=useState(0);const [paymentsRequest,setPaymentsRequest]=useState(0);const [newCategoryTarget,setNewCategoryTarget]=useState<number|'main'|null>(null);
  const [categoryState,setCategoryState]=useState('loading');const [error,setError]=useState('');const [confirmed,setConfirmed]=useState(false);const [onlyOnce,setOnlyOnce]=useState(false);
  useEffect(()=>{const controller=new AbortController();setCategoryState('loading');loadCategoryBudget(date,controller.signal).then(data=>{if(controller.signal.aborted)return;setCategories(data.categories);setReadyToAssign(data.readyToAssign);setCategoryState('ready');}).catch(err=>{if(err.name!=='AbortError')setCategoryState('error');});return()=>controller.abort();},[date,categoryRefresh]);
  const transfer=accounts.find(a=>a.id!==accountId&&a.id===chosenTransfer);
  const columns=9+Number(view.showFlags)+Number(view.showImages)+Number(view.showRunningBalance);
  const categoryProps={categories,readyToAssign,loading:categoryState==='loading',error:categoryState==='error',onOpen:()=>setCategoryRefresh(n=>n+1),onRetry:()=>setCategoryRefresh(n=>n+1)};
  function split(){setCategory('');setSplits([{categoryId:'',amount:''},{categoryId:'',amount:''}]);}
  async function save(another:boolean) {
    setError('');const out=outflow.trim()?parseRegisterMoney(outflow):0;const income=inflow.trim()?parseRegisterMoney(inflow):0;
    if(chosenTransfer===accountId){setError('Choose a different transfer account.');return false;}
    if(!dateValid){setError('Enter a valid date as DD-MM-YYYY.');return false;}
    if(out===null||income===null||out<0||income<0||out>0&&income>0||out===0&&income===0&&!entry?.isOpening){setError('Enter one nonzero outflow or inflow with at most two decimal places.');return false;}
    const amount=income-out;const parsed=splits.map(s=>({categoryId:s.categoryId,amount:parseMoney(s.amount)}));
    if(!transfer&&(parsed.some(s=>!s.categoryId||s.amount===null||s.amount===0)||splits.length&&Math.round(parsed.reduce((sum,s)=>sum+(s.amount??0)*100,0))!==Math.round(amount*100))){setError('Split amounts must equal the signed transaction amount.');return false;}
    if(requiresConfirmation&&!confirmed){setError('Confirm the reconciled transaction warning before saving.');return false;}
    return onSave({accountId,date,amount,payee:transfer?'':payee,memo,categoryId:transfer||splits.length?null:categoryId||null,splits:transfer?[]:parsed as Split[],status,repeat,toAccountId:transfer?.id??null,confirmReconciledEdit:confirmed,onlyOnce},another);
  }
  return <><tr className="register-inline-row" onKeyDown={e=>{if(e.key==='Escape'&&!busy)onCancel();}} aria-label={entry?'Edit transaction inline':'New transaction'}>
    <td className="register-check-cell"><input type="checkbox" aria-label="Editing transaction" checked readOnly/></td><td/>{view.showFlags&&<td className="register-decoration"><RegisterIcon name="flag" size={15}/></td>}{view.showImages&&<td className="register-decoration"><RegisterIcon name="image" size={15}/></td>}
    <td className="inline-date-cell"><TransactionDatePicker date={date} repeat={repeat} onDate={setDate} onRepeat={setRepeat} initiallyOpen={!entry} disabled={busy} onValid={setDateValid}/></td>
    <td><PayeePicker paymentsRequest={paymentsRequest} form={formId} value={payee} disabled={busy||entry?.isOpening} accountId={accountId} accounts={accounts} categories={categories} onShowTransactions={onShowPayeeTransactions} onChanged={onPayeesChanged} onPendingChange={onPayeesPending} onChange={(name,id,suggested)=>{setPayee(name);setChosenTransfer(id);if(!categoryId&&!splits.length&&suggested&&categories.some(c=>c.id===suggested&&!c.isArchived))setCategory(suggested);}}/></td>
    <td><CategoryPicker {...categoryProps} form={formId} value={splits.length?'__split':categoryId} notNeeded={!!transfer||entry?.isOpening} disabled={busy} onChange={id=>{setSplits([]);setCategory(id);}} onSplit={split} onTransfer={()=>setPaymentsRequest(n=>n+1)} onNewCategory={()=>setNewCategoryTarget('main')}/></td>
    <td><input form={formId} aria-label="Memo" disabled={busy} value={memo} maxLength={1000} placeholder="memo" onChange={e=>setMemo(e.target.value)}/></td>
    <td><input form={formId} aria-label="Outflow" disabled={busy} inputMode="decimal" value={outflow} placeholder="outflow" onChange={e=>setOutflow(e.target.value)}/></td><td><input form={formId} aria-label="Inflow" disabled={busy} inputMode="decimal" value={inflow} placeholder="inflow" onChange={e=>setInflow(e.target.value)}/></td>
    {view.showRunningBalance&&<td/>}<td className="register-status-cell"><button type="button" className={'transaction-status status-'+status} disabled={busy||scheduled||requiresConfirmation} aria-label={status==='cleared'?'Cleared: mark uncleared':'Uncleared: mark cleared'} onClick={()=>setStatus(status==='cleared'?'uncleared':'cleared')}><span className={status==='cleared'?'cleared-symbol':'uncleared-symbol'}>c</span></button></td>
  </tr><tr className="register-inline-footer"><td colSpan={columns}>
    <form id={formId} onSubmit={e=>{e.preventDefault();void save(false);}}>
      {!selectedAccount&&!entry&&<label>Account<select aria-label="Transaction account" value={accountId} disabled={busy} onChange={e=>setAccountId(e.target.value)}>{accounts.filter(a=>!a.isClosed).map(a=><option key={a.id} value={a.id}>{a.name}</option>)}</select></label>}
      {scheduled&&<label className="inline-check"><input type="checkbox" checked={onlyOnce} disabled={busy} onChange={e=>setOnlyOnce(e.target.checked)}/>Only this occurrence</label>}
      {requiresConfirmation&&<label className="inline-reconcile-warning"><input type="checkbox" checked={confirmed} disabled={busy} onChange={e=>setConfirmed(e.target.checked)}/>This entry was reconciled. I understand saving marks it cleared and requires reconciling again. The old reconciliation and edit remain in the audit history.</label>}
      {splits.length>0&&!transfer&&<div className="inline-splits">{splits.map((s,i)=><div className="split-row" key={i}><label>Split {i+1} category<CategoryPicker {...categoryProps} label={`Split ${i+1} category`} value={s.categoryId} disabled={busy} onChange={id=>setSplits(items=>items.map((item,index)=>index===i?{...item,categoryId:id}:item))} onNewCategory={()=>setNewCategoryTarget(i)}/></label><label>Split {i+1} amount<input required aria-label={`Split ${i+1} amount`} value={s.amount} disabled={busy} onChange={e=>setSplits(items=>items.map((item,index)=>index===i?{...item,amount:e.target.value}:item))}/></label><button type="button" disabled={busy} aria-label={`Remove split ${i+1}`} onClick={()=>setSplits(items=>items.filter((_,index)=>index!==i))}>×</button></div>)}<button type="button" disabled={busy||splits.length>=50} onClick={()=>setSplits(items=>[...items,{categoryId:'',amount:''}])}>+ Add split</button></div>}
      {categoryState==='loading'&&<span role="status">Loading categories…</span>}{categoryState==='error'&&<span role="alert">Could not load categories. <button type="button" onClick={()=>setCategoryRefresh(n=>n+1)}>Retry category list</button></span>}
      {postingError&&<span role="alert" className="inline-error">{postingError}</span>}
      {error&&<span role="alert" className="inline-error">{error}</span>}
      <div className="inline-save-actions"><button type="button" disabled={busy} onClick={onCancel}>Cancel</button><button className="button-primary" disabled={busy||categoryState!=='ready'}>{busy?'Saving…':'Save'}</button>{!entry&&<button type="button" className="button-primary" disabled={busy||categoryState!=='ready'} onClick={()=>void save(true)}>Save and add another</button>}</div>
    </form>
  </td></tr>{newCategoryTarget!==null&&createPortal(<NewCategoryDialog date={date} onPendingChange={onPayeesPending} onClose={()=>setNewCategoryTarget(null)} onCreated={id=>{if(newCategoryTarget==='main'){setSplits([]);setCategory(id);}else setSplits(items=>items.map((item,index)=>index===newCategoryTarget?{...item,categoryId:id}:item));setNewCategoryTarget(null);setCategoryRefresh(n=>n+1);onCategoriesChanged();}}/>,document.body)}</>;
}
