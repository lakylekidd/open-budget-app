import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { today } from './api';
export const repeatOptions = [['never','Never'],['daily','Daily'],['weekly','Weekly'],['everyOtherWeek','Every other week'],['twiceAMonth','Twice a month'],['every4Weeks','Every 4 weeks'],['monthly','Monthly'],['everyOtherMonth','Every other month'],['every3Months','Every 3 months'],['every4Months','Every 4 months'],['twiceAYear','Twice a year'],['yearly','Yearly'],['everyOtherYear','Every other year']] as const;
export const displayDate = (date: string) => date.split('-').reverse().join('-');
export function parseRegisterDate(text: string): string | null {
  const parts = /^(\d{2})-(\d{2})-(\d{4})$/.exec(text);
  const iso = parts ? `${parts[3]}-${parts[2]}-${parts[1]}` : text;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) return null;
  const date = new Date(iso+'T12:00:00');
  return Number(iso.slice(0,4)) >= 1900 && Number(iso.slice(0,4)) <= 9998 && !isNaN(date.valueOf()) && date.getFullYear() === Number(iso.slice(0,4)) && date.getMonth()+1 === Number(iso.slice(5,7)) && date.getDate() === Number(iso.slice(8)) ? iso : null;
}
export function TransactionDatePicker({ date, repeat, onDate, onRepeat, initiallyOpen=false, disabled=false, onValid }: {
  date:string; repeat:string; onDate:(date:string)=>void; onRepeat:(repeat:string)=>void; initiallyOpen?:boolean; disabled?:boolean; onValid:(valid:boolean)=>void;
}) {
  const [position,setPosition]=useState({left:12,top:100});const [open,setOpen]=useState(initiallyOpen); const [text,setText]=useState(displayDate(date));
  const [month,setMonth]=useState(date.slice(0,7)); const root=useRef<HTMLDivElement>(null); const input=useRef<HTMLInputElement>(null);
  useEffect(()=>{setText(displayDate(date));setMonth(date.slice(0,7));},[date]);
  useEffect(()=>{const close=(event:PointerEvent)=>{if(!root.current?.contains(event.target as Node)) setOpen(false);};document.addEventListener('pointerdown',close);return()=>document.removeEventListener('pointerdown',close);},[]);
  useEffect(()=>{if(initiallyOpen){input.current?.focus();input.current?.select();}},[initiallyOpen]);
  useLayoutEffect(()=>{if(!open)return;const place=()=>{const rect=input.current?.getBoundingClientRect();if(rect)setPosition({left:Math.max(12,Math.min(rect.left-20,window.innerWidth-242)),top:Math.max(12,Math.min(rect.bottom+16,window.innerHeight-355))});};place();window.addEventListener('resize',place);window.addEventListener('scroll',place,true);return()=>{window.removeEventListener('resize',place);window.removeEventListener('scroll',place,true);};},[open]);
  const [year,n]=month.split('-').map(Number);const first=new Date(year,n-1,1);const days=new Date(year,n,0).getDate();
  function move(delta:number){const value=new Date(year,n-1+delta,1);if(value.getFullYear()>=1900&&value.getFullYear()<=9998)setMonth(`${value.getFullYear()}-${String(value.getMonth()+1).padStart(2,'0')}`);}
  return <div ref={root} className="transaction-date-picker" onKeyDown={e=>{if(e.key==='Escape'&&open){e.stopPropagation();setOpen(false);input.current?.focus();}}}>
    <div className="transaction-date-input"><input ref={input} aria-label="Date" disabled={disabled} value={text} onFocus={()=>setOpen(true)} onChange={e=>{setText(e.target.value);const parsed=parseRegisterDate(e.target.value);onValid(Boolean(parsed));if(parsed)onDate(parsed);}}/><button type="button" disabled={disabled} aria-label="Choose date and repeat" aria-expanded={open} onClick={()=>setOpen(!open)}>▾</button></div>
    {open&&<div className="transaction-calendar" style={position} aria-label="Transaction calendar"><div className="calendar-heading"><button type="button" aria-label="Previous calendar month" disabled={disabled} onClick={()=>move(-1)}>‹</button><strong>{first.toLocaleDateString('en-GB',{month:'short',year:'numeric'})}</strong><button type="button" aria-label="Next calendar month" disabled={disabled} onClick={()=>move(1)}>›</button></div>
      <div className="calendar-grid">{['Su','Mo','Tu','We','Th','Fr','Sa'].map(day=><span className="calendar-weekday" key={day}>{day}</span>)}{Array.from({length:first.getDay()},(_,i)=><span key={'blank'+i}/>)}{Array.from({length:days},(_,i)=>{const iso=`${month}-${String(i+1).padStart(2,'0')}`;return <button key={iso} type="button" disabled={disabled} aria-label={new Date(iso+'T12:00:00').toLocaleDateString('en-GB',{day:'numeric',month:'long',year:'numeric'})} aria-pressed={date===iso} className={iso===today()?'calendar-today':''} onClick={()=>{onDate(iso);onValid(true);}}>{i+1}</button>;})}</div>
      <label className="calendar-repeat">Repeat:<select aria-label="Repeat" disabled={disabled} value={repeat} onChange={e=>onRepeat(e.target.value)}>{repeatOptions.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></label>
    </div>}
  </div>;
}
