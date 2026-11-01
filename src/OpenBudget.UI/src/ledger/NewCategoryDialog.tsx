import { useEffect, useRef, useState } from 'react';
import { ApiError, budgetMonthPath, requestJson } from './api';
import { LedgerDialog } from './LedgerDialog';

type Category = {id:string;name:string;assigned:number;target:number;icon:string|null;snoozed:boolean;sortOrder:number;isArchived:boolean};
type Month = {month:string;note:string;version:number;fundsAdded:number;groups:{id:string;name:string;sortOrder:number;isArchived:boolean;categories:Category[]}[]};

export function NewCategoryDialog({date,onClose,onCreated,onPendingChange}: {date:string;onClose:()=>void;onCreated:(id:string)=>void;onPendingChange?:(value:boolean)=>void}) {
  const month=date.slice(0,7);const [snapshot,setSnapshot]=useState<Month|null>(null);const [name,setName]=useState('');const [group,setGroup]=useState('');
  const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [uncertain,setUncertain]=useState(false);const [conflict,setConflict]=useState(false);const [refresh,setRefresh]=useState(0);
  const pending=useRef<{id:string;group:string;name:string;body:string;version:number}|null>(null);const inFlight=useRef(false);
  useEffect(()=>{const controller=new AbortController();setSnapshot(null);requestJson<Month>(budgetMonthPath(month),{signal:controller.signal}).then(value=>{if(controller.signal.aborted)return;setSnapshot(value);setGroup(current=>value.groups.some(g=>g.id===current&&!g.isArchived)?current:value.groups.find(g=>!g.isArchived)?.id??'');setConflict(false);setError('');}).catch(err=>{if(!controller.signal.aborted)setError(err.message);});return()=>controller.abort();},[month,refresh]);
  useEffect(()=>{onPendingChange?.(busy||uncertain);return()=>onPendingChange?.(false);},[busy,uncertain,onPendingChange]);
  function close(){if(!busy&&!uncertain)onClose();}
  async function save(){
    if(inFlight.current||!snapshot||conflict)return;
    if(!pending.current){
      if(!name.trim()||name.trim().length>160||!group){setError('Enter a category name and choose a category group.');return;}
      const target=snapshot.groups.find(g=>g.id===group&&!g.isArchived);if(!target)return;
      if(target.categories.some(c=>!c.isArchived&&c.name.toLocaleLowerCase()===name.trim().toLocaleLowerCase())){setError('This group already has a category with that name.');return;}
      const id=crypto.randomUUID();
      const groups=snapshot.groups.map(g=>({...g,categories:g.categories.map(({id,name,assigned,target,icon,snoozed,sortOrder,isArchived})=>({id,name,assigned,target,icon,snoozed,sortOrder,isArchived}))}));
      groups.find(g=>g.id===group)!.categories.push({id,name:name.trim(),assigned:0,target:0,icon:null,snoozed:false,isArchived:false,sortOrder:Math.max(-1,...target.categories.map(c=>c.sortOrder))+1});
      pending.current={id,group,name:name.trim(),version:snapshot.version,body:JSON.stringify({month:snapshot.month,note:snapshot.note,version:snapshot.version,fundsAdded:snapshot.fundsAdded,groups})};
    }
    inFlight.current=true;setBusy(true);setError('');
    try{
      const command=pending.current;
      // A monthly snapshot has no command receipt: recover a lost response by the stable new category ID.
      if(uncertain){
        const latest=await requestJson<Month>(budgetMonthPath(month));
        const saved=latest.groups.find(g=>g.id===command.group)?.categories.find(c=>c.id===command.id);
        if(saved&&saved.name===command.name){pending.current=null;setUncertain(false);onCreated(command.id);return;}
        if(latest.version!==command.version)throw new ApiError('The budget changed. Reload it before creating this category.',409);
      }
      await requestJson<Month>(budgetMonthPath(month),{method:'PUT',headers:{'Content-Type':'application/json'},body:command.body});
      pending.current=null;setUncertain(false);onCreated(command.id);
    }catch(err){setError(err instanceof Error?err.message:'Could not create category.');if(err instanceof ApiError&&err.status<500){pending.current=null;setUncertain(false);setConflict(err.status===409);}else setUncertain(true);}
    finally{inFlight.current=false;setBusy(false);}
  }
  const locked=busy||uncertain||conflict||!snapshot;
  return <LedgerDialog title="New Category" onClose={close}><form className="new-category-form" onSubmit={e=>{e.preventDefault();void save();}}>
    {!snapshot&&!error&&<p role="status">Loading budget…</p>}
    <label>Category Name<input aria-label="Category Name" value={name} maxLength={160} disabled={locked} onChange={e=>setName(e.target.value)}/></label>
    <label>Category Group<select aria-label="Category Group" value={group} disabled={locked} onChange={e=>setGroup(e.target.value)}><option value="">Choose category group</option>{snapshot?.groups.filter(g=>!g.isArchived).map(g=><option value={g.id} key={g.id}>{g.name}</option>)}</select></label>
    {snapshot&&!snapshot.groups.some(g=>!g.isArchived)&&<p>No active category groups. Add a group in Plan first.</p>}
    {error&&<p role="alert">{error}{!snapshot||conflict?<button type="button" disabled={busy} onClick={()=>setRefresh(n=>n+1)}>{conflict?'Reload budget':'Retry loading'}</button>:null}</p>}
    {uncertain&&<p role="alert">The save outcome is unknown. <button type="button" disabled={busy} onClick={()=>void save()}>Retry save</button></p>}
    <div className="new-category-actions"><button type="button" disabled={busy||uncertain} onClick={close}>Cancel</button><button className="button-primary" disabled={locked||!group}>{busy?'Saving…':'Save category'}</button></div>
  </form></LedgerDialog>;
}
