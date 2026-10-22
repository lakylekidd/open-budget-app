import { useState } from 'react';
import { type Account, parseMoney } from './api';
export function EditAccountForm({ account,busy,onSubmit,onCancel }:{account:Account;busy:boolean;onSubmit:(fields:object)=>Promise<void>;onCancel:()=>void}) {
 const [name,setName]=useState(account.name);const [notes,setNotes]=useState(account.notes??'');const [balance,setBalance]=useState(account.balance.toFixed(2));const [error,setError]=useState('');
 async function save(){setError('');const workingBalance=parseMoney(balance);if(workingBalance===null){setError('Enter a working balance with at most two decimal places.');return;}await onSubmit({name,notes,isClosed:account.isClosed,workingBalance});}
 return <form className="edit-account-form" onSubmit={e=>{e.preventDefault();void save();}}>
  <h3>Account Information</h3><label>Account Nickname<input required maxLength={120} value={name} disabled={busy} onChange={e=>setName(e.target.value)}/></label>
  <label>Account Notes<textarea maxLength={1000} value={notes} disabled={busy} onChange={e=>setNotes(e.target.value)}/></label>
  <div className="edit-account-balance"><label>Working Balance<input required inputMode="decimal" value={balance} disabled={busy||account.isClosed} onChange={e=>setBalance(e.target.value)}/></label><p>An adjustment transaction will be created automatically if you change this amount.</p></div>
  {error&&<p role="alert">{error}</p>}
  <div className="edit-account-actions"><button className="account-close-button" type="button" disabled={busy} onClick={()=>void onSubmit({name,notes,isClosed:!account.isClosed})}>{account.isClosed?'Reopen Account':'Close Account'}</button><button type="button" disabled={busy} onClick={onCancel}>Cancel</button><button className="button-primary" disabled={busy}>Save</button></div>
 </form>;
}
