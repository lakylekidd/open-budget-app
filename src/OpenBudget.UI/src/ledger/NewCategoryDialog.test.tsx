import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { NewCategoryDialog } from './NewCategoryDialog';

let month:Record<string,unknown>;let writes:string[];let failure:'read'|'conflict'|'lost'|'not-saved'|null;
const close=vi.fn(),created=vi.fn();const response=(data:unknown,status=200)=>new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});
beforeEach(()=>{
  close.mockClear();created.mockClear();writes=[];failure=null;
  month={month:'2026-10',note:'Preserve me',version:5,fundsAdded:0,readyToAssign:75,groups:[{id:'daily',name:'Everyday',sortOrder:0,isArchived:false,categories:[{id:'food',name:'Food',assigned:25,target:10,available:20,activity:-5,icon:'x',snoozed:false,sortOrder:3,isArchived:false}]}]};
  vi.stubGlobal('fetch',vi.fn(async(_path:string,init?:RequestInit)=>{
    if(init?.method==='PUT'){
      writes.push(String(init.body));if(failure==='conflict'){failure=null;return response({error:'Budget changed elsewhere'},409);}if(failure==='not-saved'){failure=null;throw new Error('Connection lost');}
      month={...JSON.parse(String(init.body)),version:6};if(failure==='lost'){failure=null;throw new Error('Response lost');}return response(month);
    }
    if(failure==='read'){failure=null;return response({error:'Unavailable'},503);}return response(month);
  }));
});
function show(){render(<NewCategoryDialog date="2026-10-12" onClose={close} onCreated={created}/>);}
async function ready(){await waitFor(()=>expect(screen.getByRole('button',{name:'Save category'})).toBeEnabled());}

it('creates a zero-assigned category while preserving existing assignments and sends no calculated values',async()=>{
  show();await ready();await userEvent.type(screen.getByRole('textbox',{name:'Category Name'}),'Household');await userEvent.click(screen.getByRole('button',{name:'Save category'}));await waitFor(()=>expect(created).toHaveBeenCalledOnce());
  const saved=JSON.parse(writes[0]);expect(saved).toEqual({month:'2026-10',note:'Preserve me',version:5,fundsAdded:0,groups:[{id:'daily',name:'Everyday',sortOrder:0,isArchived:false,categories:[{id:'food',name:'Food',assigned:25,target:10,icon:'x',snoozed:false,sortOrder:3,isArchived:false},{id:expect.any(String),name:'Household',assigned:0,target:0,icon:null,snoozed:false,sortOrder:4,isArchived:false}]}]});expect(created).toHaveBeenCalledWith(saved.groups[0].categories[1].id);
});

it('validates empty and duplicate names and cancels without writes',async()=>{
  show();await ready();await userEvent.click(screen.getByRole('button',{name:'Save category'}));expect(screen.getByRole('alert')).toHaveTextContent('Enter a category name');await userEvent.type(screen.getByRole('textbox',{name:'Category Name'}),'food');await userEvent.click(screen.getByRole('button',{name:'Save category'}));expect(screen.getByRole('alert')).toHaveTextContent('already has a category');await userEvent.click(screen.getByRole('button',{name:'Cancel'}));expect(writes).toHaveLength(0);expect(close).toHaveBeenCalledOnce();
});

it('keeps a conflicted name and requires reload before retrying with a fresh month version',async()=>{
  failure='conflict';show();await ready();await userEvent.type(screen.getByRole('textbox',{name:'Category Name'}),'Household');await userEvent.click(screen.getByRole('button',{name:'Save category'}));await screen.findByRole('button',{name:'Reload budget'});expect(screen.getByRole('textbox',{name:'Category Name'})).toHaveValue('Household');expect(screen.getByRole('button',{name:'Save category'})).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Reload budget'}));await ready();expect(screen.getByRole('textbox',{name:'Category Name'})).toHaveValue('Household');
});

it.each(['lost','not-saved'] as const)('recovers %s outcome by stable category id without duplicating the category',async mode=>{
  failure=mode;show();await ready();await userEvent.type(screen.getByRole('textbox',{name:'Category Name'}),'Household');await userEvent.click(screen.getByRole('button',{name:'Save category'}));await screen.findByRole('button',{name:'Retry save'});await userEvent.keyboard('{Escape}');expect(close).not.toHaveBeenCalled();expect(screen.getByRole('textbox',{name:'Category Name'})).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Retry save'}));await waitFor(()=>expect(created).toHaveBeenCalledOnce());expect(writes).toHaveLength(mode==='lost'?1:2);if(mode==='not-saved')expect(writes[0]).toBe(writes[1]);
});

it('recovers a failed initial read',async()=>{
  failure='read';show();await userEvent.click(await screen.findByRole('button',{name:'Retry loading'}));await ready();
});

it('explains when no active group is available and prevents a write',async()=>{
  month={...month,groups:[]};show();expect(await screen.findByText('No active category groups. Add a group in Plan first.')).toBeInTheDocument();expect(screen.getByRole('button',{name:'Save category'})).toBeDisabled();expect(writes).toHaveLength(0);
});
