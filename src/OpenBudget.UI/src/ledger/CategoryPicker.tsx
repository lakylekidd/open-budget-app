import { useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { type CategoryOption, formatMoney } from './api';
import { focusNextField } from './focusNext';
// Picker value for an uncategorized inflow; callers send it as a null category.
export const inflowCategoryId='__inflow';

export function categoryGroups(categories:CategoryOption[], search:string) {
  const query=search.trim().toLocaleLowerCase();
  const groups=new Map<string,{id:string;name:string;categories:CategoryOption[]}>();
  for(const category of categories){
    if(category.isArchived||query&&!`${category.groupName??''} ${category.name}`.toLocaleLowerCase().includes(query))continue;
    const id=category.groupId??category.groupName??'categories';
    if(!groups.has(id))groups.set(id,{id,name:category.groupName??'Categories',categories:[]});
    groups.get(id)!.categories.push(category);
  }
  return [...groups.values()];
}

export function CategoryPicker({value,categories,readyToAssign,includeInflow=true,disabled,notNeeded=false,form,label='Category',loading=false,error=false,onRetry,onOpen,onChange,onSplit,onTransfer,onNewCategory}: {
  value:string;categories:CategoryOption[];readyToAssign?:number;includeInflow?:boolean;disabled?:boolean;notNeeded?:boolean;form?:string;label?:string;loading?:boolean;error?:boolean;
  onRetry?:()=>void;onOpen?:()=>void;onChange:(id:string)=>void;onSplit?:()=>void;onTransfer?:()=>void;onNewCategory?:()=>void;
}){
  const id=useId();const input=useRef<HTMLInputElement>(null);
  // Programmatic refocus (after choosing, Escape or the toggle) must not reopen the list.
  const skipFocusOpen=useRef(false);const refocus=()=>{skipFocusOpen.current=true;input.current?.focus();skipFocusOpen.current=false;};const popup=useRef<HTMLDivElement>(null);
  const [open,setOpen]=useState(false);const [query,setQuery]=useState<string|null>(null);const [active,setActive]=useState(-1);
  const [position,setPosition]=useState({left:0,top:0,maxHeight:320});
  useEffect(()=>{if(disabled||notNeeded){setOpen(false);setQuery(null);}},[disabled,notNeeded]);
  useEffect(()=>{if(loading||error)setActive(-1);},[loading,error]);
  const selected=categories.find(c=>c.id===value);
  const title=notNeeded?'Category not needed':value==='__split'?'Split (Multiple Categories)...':value===inflowCategoryId?'Inflow: Ready to Assign':selected?`${selected.groupName?selected.groupName+': ':''}${selected.name}`:'';
  const groups=categoryGroups(categories,query??'');const showSelected=!!selected&&!query;
  const showInflow=includeInflow&&(!query||`inflow ready to assign`.includes(query.trim().toLocaleLowerCase()));
  const choices=[...(showSelected?[{id:selected.id,name:selected.name,group:selected.groupName,available:selected.available,selected:true}]:[]),...(showInflow?[{id:inflowCategoryId,name:'Ready to Assign',group:undefined,available:readyToAssign,selected:false}]:[]),...groups.flatMap(g=>g.categories.map(c=>({id:c.id,name:c.name,group:c.groupName,available:c.available,selected:false})))];
  function close(){setOpen(false);setQuery(null);setActive(-1);}
  function show(){if(!open)onOpen?.();setOpen(true);}
  function choose(index:number){if(!loading&&!error&&choices[index]){onChange(choices[index].id);close();refocus();focusNextField(input.current);}}
  useEffect(()=>{if(!open)return;
    function positionPopup(){const rect=input.current?.getBoundingClientRect();if(!rect)return;const height=Math.min(360,window.innerHeight-24);const below=window.innerHeight-rect.bottom-12;const top=below>=height||below>=rect.top?rect.bottom+10:Math.max(8,rect.top-height-10);setPosition({left:Math.max(8,Math.min(rect.left-30,window.innerWidth-408)),top,maxHeight:Math.max(160,Math.min(height,window.innerHeight-top-8))});}
    function outside(event:MouseEvent){if(!popup.current?.contains(event.target as Node)&&!input.current?.parentElement?.contains(event.target as Node))close();}
    positionPopup();document.addEventListener('mousedown',outside);window.addEventListener('resize',positionPopup);window.addEventListener('scroll',positionPopup,true);
    return()=>{document.removeEventListener('mousedown',outside);window.removeEventListener('resize',positionPopup);window.removeEventListener('scroll',positionPopup,true);};
  },[open]);
  useEffect(()=>{if(active>=0)popup.current?.querySelector('[data-active="true"]')?.scrollIntoView?.({block:'nearest'});},[active]);
  function option(index:number){const item=choices[index];return <button type="button" role="option" id={`${id}-${index}`} key={`${item.selected?'selected':index}-${item.id}`} aria-selected={item.id===value} data-active={index===active} className={'category-choice'+(item.selected?' current-category':'')} onClick={()=>choose(index)}><span className="category-choice-name">{item.selected&&<span aria-hidden="true">✓ </span>}{item.name}{item.selected&&item.group&&<span className="category-parent"> ({item.group})</span>}</span><span className={'category-available '+(typeof item.available!=='number'?'':item.available<0?'negative':item.available>0?'positive':'')} aria-label={`${item.name} available`}>{typeof item.available==='number'?formatMoney(item.available):'—'}</span></button>;}
  let index=Number(showSelected)+Number(showInflow);
  return <><span className="category-input-wrap"><input ref={input} form={form} role="combobox" onFocus={()=>{if(skipFocusOpen.current||disabled)return;if(!open){setQuery(null);setActive(-1);}show();}} aria-label={label} aria-expanded={open} aria-controls={`${id}-list`} aria-autocomplete="list" aria-activedescendant={open&&active>=0?`${id}-${active}`:undefined} autoComplete="off" disabled={disabled||notNeeded} value={query??title} placeholder="category" onClick={()=>{if(!open){setQuery(null);setActive(-1);input.current?.select();}show();}} onChange={e=>{setQuery(e.target.value);show();setActive(-1);}} onKeyDown={e=>{
    if(e.key==='Escape'&&open){e.stopPropagation();close();}
    if(e.key==='Tab')close();
    if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();show();const direction=e.key==='ArrowDown'?1:-1;setActive(n=>choices.length?((n<0?(direction>0?-1:0):n)+direction+choices.length)%choices.length:-1);}
    if(e.key==='Enter'&&open){e.preventDefault();if(active>=0)choose(active);}
  }}/><button type="button" className="category-open" aria-label={`Choose ${label.toLowerCase()}`} tabIndex={-1} disabled={disabled||notNeeded} onClick={()=>{if(open)close();else show();setQuery(null);setActive(-1);refocus();}}>▾</button></span>
    {open&&createPortal(<div ref={popup} className="category-picker" style={position} onKeyDown={e=>{if(e.key==='Escape'){e.stopPropagation();close();refocus();}}}>
      {onNewCategory&&<button type="button" className="new-category-link" onClick={()=>{close();onNewCategory();}}>⊕ New Category</button>}
      <div className="category-options" role="listbox" id={`${id}-list`} aria-label="Budget categories">
        {loading?<p role="status">Loading categories…</p>:error?<p role="alert">Could not load categories. <button type="button" onClick={onRetry}>Retry categories</button></p>:<>
          {showSelected&&<><h4>Selected</h4>{option(0)}</>}{showInflow&&<><h4>Inflow</h4>{option(Number(showSelected))}</>}
          {groups.map(group=><section role="group" aria-label={group.name} key={group.id}><h4>{group.name}</h4>{group.categories.map(()=>option(index++))}</section>)}
          {!choices.length&&<p>No matching categories.</p>}
        </>}
      </div>{(onTransfer||onSplit)&&<footer>{onTransfer&&<button type="button" onClick={()=>{close();onTransfer();}}>Payment/Transfer</button>}{onSplit&&<button type="button" onClick={()=>{close();onSplit();}}>Split</button>}</footer>}
    </div>,document.body)}
  </>;
}
