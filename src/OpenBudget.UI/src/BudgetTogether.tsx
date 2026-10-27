import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { requestJson } from './ledger/api';

type Person = { id:string;name:string;email:string|null;role:string;joinedAt:string };
type Invitation = { id:string;email:string;createdAt:string };
type Sharing = { manager:Person;members:Person[];invitations:Invitation[];canManage:boolean;invitesRemaining:number };

export function BudgetTogether({budgetId,budgetName,currentUserEmail,onBack}:{budgetId:string;budgetName:string;currentUserEmail:string;onBack:()=>void}){
  const [sharing,setSharing]=useState<Sharing|null>(null);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const [email,setEmail]=useState('');
  const [inviting,setInviting]=useState(false);
  const [message,setMessage]=useState('');
  const load=useCallback(async()=>{
    setLoading(true);setError('');
    try{setSharing(await requestJson<Sharing>(`/api/budgets/${budgetId}/sharing`));}
    catch(reason){setError(reason instanceof Error?reason.message:'Could not load budget members.');}
    finally{setLoading(false);}
  },[budgetId]);
  useEffect(()=>{void load();},[load]);
  async function invite(event:FormEvent){
    event.preventDefault();if(!email.trim())return;
    setInviting(true);setError('');setMessage('');
    try{
      const result=await requestJson<{message:string}>(`/api/budgets/${budgetId}/invitations`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:email.trim()})});
      setEmail('');setMessage(result.message);await load();
    }catch(reason){setError(reason instanceof Error?reason.message:'Could not invite this member.');}
    finally{setInviting(false);}
  }
  async function remove(path:string){
    setError('');
    try{const response=await fetch(path,{method:'DELETE'});if(!response.ok)throw new Error(`Request failed (${response.status}).`);await load();}
    catch(reason){setError(reason instanceof Error?reason.message:'Could not update budget access.');}
  }
  const avatar=(person:Person)=><span className="together-avatar" aria-hidden="true">{(person.name||person.email||'?').slice(0,1).toUpperCase()}</span>;
  return <section className="together-page" aria-label="OpenBudget Together">
    <header className="together-topbar"><button onClick={onBack}>‹ &nbsp; Back to Plan</button><strong>OpenBudget Together</strong><span /></header>
    <main className="together-content">
      <p className="eyebrow">PLAN BETTER, TOGETHER</p><h1>OpenBudget Together</h1>
      <div className="together-hero" aria-hidden="true"><span>✋</span><span>✋</span><i /><i /></div>
      <p>Share <strong>{budgetName}</strong> with the people you plan with. Everyone sees the same categories, balances, and activity, while Recent Moves shows who changed the plan.</p>
      {loading&&<p role="status">Loading members…</p>}{error&&<p className="together-error" role="alert">{error}</p>}{message&&<p className="together-success" role="status">{message}</p>}
      {sharing&&<>
        <h2>Group Manager</h2><article className="together-person">{avatar(sharing.manager)}<div><strong>{sharing.manager.name}{sharing.manager.email===currentUserEmail?' (you)':''}</strong><span>{sharing.manager.email}</span></div><span className="together-role">Manager</span></article>
        <h2>Members</h2>
        {sharing.members.length===0?<p className="together-empty">No one else has access yet.</p>:sharing.members.map(member=><article className="together-person" key={member.id}>{avatar(member)}<div><strong>{member.name}{member.email===currentUserEmail?' (you)':''}</strong><span>{member.email}</span></div><span className="together-role">Can edit</span>{sharing.canManage&&<button aria-label={`Remove ${member.name}`} onClick={()=>void remove(`/api/budgets/${budgetId}/members/${member.id}`)}>Remove</button>}</article>)}
        {sharing.canManage&&sharing.invitations.length>0&&<><h2>Pending Invitations</h2>{sharing.invitations.map(invitation=><article className="together-person pending" key={invitation.id}><span className="together-avatar" aria-hidden="true">✉</span><div><strong>{invitation.email}</strong><span>Waiting for this email to sign in</span></div><button aria-label={`Cancel invitation for ${invitation.email}`} onClick={()=>void remove(`/api/budgets/${budgetId}/invitations/${invitation.id}`)}>Cancel</button></article>)}</>}
        {sharing.canManage&&<section className="together-invite"><div><h2>Invite</h2><span>{sharing.invitesRemaining} {sharing.invitesRemaining===1?'invite':'invites'} remaining</span></div><p>Add someone by the email address they use—or will use—for OpenBudget.</p><form onSubmit={invite}><label htmlFor="together-email">Email address</label><div><input id="together-email" type="email" required value={email} onChange={event=>setEmail(event.target.value)} placeholder="friend@example.com"/><button className="button-primary" disabled={inviting||sharing.invitesRemaining===0}>{inviting?'Inviting…':'Invite Member'}</button></div></form></section>}
      </>}
    </main>
  </section>;
}
