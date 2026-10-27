import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { ManagePayees, PayeePicker, type PayeeList } from './Payees';
import { type Account } from './api';

const accounts=[{id:'one',name:'Checking',isClosed:false},{id:'two',name:'Savings',isClosed:false},{id:'three',name:'Savings',isClosed:false},{id:'closed',name:'Old account',isClosed:true}] as Account[];
const categories=[{id:'food',name:'Everyday: Food',isArchived:false},{id:'travel',name:'Life: Travel',isArchived:false}];
let data:PayeeList;let writes:string[];let failure:'network'|'conflict'|'read'|null;const changed=vi.fn();const closed=vi.fn();
const response=(body:unknown,status=200)=>new Response(JSON.stringify(body),{status,headers:{'Content-Type':'application/json'}});
beforeEach(()=>{
  data={version:7,payees:[{id:'market',name:'Market',isHidden:false,categoryMode:'history',defaultCategoryId:null,effectiveCategoryId:'food',transactionCount:3,rules:[]},{id:'hidden',name:'Hidden merchant',isHidden:true,categoryMode:'never',defaultCategoryId:null,effectiveCategoryId:null,transactionCount:0,rules:[]}]};writes=[];failure=null;changed.mockClear();closed.mockClear();
  vi.stubGlobal('fetch',vi.fn(async(_path:string,init?:RequestInit)=>{
    if(init?.method==='PUT'){writes.push(String(init.body));if(failure==='network'){failure=null;throw new Error('Connection lost');}if(failure==='conflict'){failure=null;return response({error:'Changed elsewhere'},409);}return response({id:'saved',version:8});}
    if(failure==='read'){failure=null;return response({error:'Unavailable'},503);}return response(data);
  }));
});
function manage(){render(<ManagePayees accounts={accounts} categories={categories} onChanged={changed} onClose={closed}/>);}
async function ready(){await screen.findByRole('textbox',{name:'Payee Name'});}
function Picker(){const [value,setValue]=useState('');return <PayeePicker value={value} onChange={setValue} accounts={accounts} categories={categories} onChanged={changed} accountId="one" form="test"/>;}

it('groups all accounts separately from saved payees, excludes hidden suggestions and selects transfer by id',async()=>{
  const select=vi.fn();render(<PayeePicker value="" onChange={select} accounts={accounts} categories={categories} onChanged={changed} accountId="one" form="test"/>);
  await userEvent.click(screen.getByRole('combobox',{name:'Payee'}));await screen.findByRole('option',{name:'Market'});
  expect(screen.getByText('Payments and Transfers')).toBeInTheDocument();expect(screen.getByText('Saved Payees')).toBeInTheDocument();expect(screen.queryByRole('option',{name:'Hidden merchant'})).not.toBeInTheDocument();
  expect(screen.getByRole('option',{name:'Transfer to Checking (this account)'})).toBeDisabled();expect(screen.getByRole('option',{name:'Transfer to Old account (closed)'})).toBeDisabled();
  await userEvent.click(screen.getAllByRole('option',{name:'Transfer to Savings'})[1]);expect(select).toHaveBeenCalledWith('Transfer to Savings','three');expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
});

it('filters merchant names and supports keyboard selection and Escape without changing the value',async()=>{
  render(<Picker/>);const input=screen.getByRole('combobox',{name:'Payee'});await userEvent.type(input,'Mar');await screen.findByRole('option',{name:'Market'});await userEvent.keyboard('{ArrowDown}{Enter}');expect(input).toHaveValue('Market');
  await userEvent.click(input);expect(screen.getByText('Selected')).toBeInTheDocument();await userEvent.keyboard('{Escape}');expect(input).toHaveValue('Market');expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
});

it('opens Manage Payees from the picker and closes just the dialog on Escape',async()=>{
  const escaped=vi.fn();render(<div onKeyDown={e=>{if(e.key==='Escape')escaped();}}><Picker/></div>);
  await userEvent.click(screen.getByRole('combobox',{name:'Payee'}));await userEvent.click(screen.getByRole('button',{name:'Manage Payees'}));await ready();await userEvent.keyboard('{Escape}');expect(screen.queryByRole('dialog')).not.toBeInTheDocument();expect(screen.getByRole('combobox',{name:'Payee'})).toBeInTheDocument();expect(escaped).not.toHaveBeenCalled();
});

it('keeps exact merchant matches filtered and restores choices when the query is cleared',async()=>{
  render(<Picker/>);const input=screen.getByRole('combobox',{name:'Payee'});
  await userEvent.type(input,'  MARKET  ');await screen.findByRole('option',{name:'Market'});
  expect(screen.getAllByRole('option')).toHaveLength(1);
  await userEvent.type(input,'new');expect(screen.queryAllByRole('option')).toHaveLength(0);
  expect(screen.getByText('No saved payees found. Type a name to create one when saving.')).toBeInTheDocument();
  expect(writes).toHaveLength(0);
  await userEvent.clear(input);expect(screen.getAllByRole('option')).toHaveLength(5);
});

it('filters full transfer names and prevents keyboard selection of a previous match',async()=>{
  render(<Picker/>);const input=screen.getByRole('combobox',{name:'Payee'});
  await userEvent.type(input,'Transfer to Savings');await screen.findAllByRole('option',{name:'Transfer to Savings'});
  expect(screen.getAllByRole('option')).toHaveLength(2);
  await userEvent.keyboard('{ArrowDown}');await userEvent.type(input,'missing');
  expect(screen.queryAllByRole('option')).toHaveLength(0);
  expect(input).not.toHaveAttribute('aria-activedescendant');
  await userEvent.keyboard('{ArrowDown}{Enter}');expect(input).toHaveValue('Transfer to Savingsmissing');
});

it('saves the name, category policy, and both rule operators in one versioned command',async()=>{
  manage();await ready();const user=userEvent.setup();await user.clear(screen.getByRole('textbox',{name:'Payee Name'}));await user.type(screen.getByRole('textbox',{name:'Payee Name'}),'Local Market');await user.click(screen.getByLabelText('Always use my choice'));await user.click(screen.getByRole('combobox',{name:'Default category'}));await user.click(screen.getByRole('option',{name:/Travel/}));
  await user.click(screen.getByRole('button',{name:'Add renaming rule'}));await user.selectOptions(screen.getByRole('combobox',{name:'Rule 1 operator'}),'is');await user.type(screen.getByRole('textbox',{name:'Rule 1 text'}),'MARKET 001');
  await user.click(screen.getByRole('button',{name:'Add renaming rule'}));await user.type(screen.getByRole('textbox',{name:'Rule 2 text'}),'MARKET-');await user.click(screen.getByRole('button',{name:'Done'}));
  await waitFor(()=>expect(changed).toHaveBeenCalledOnce());expect(closed).toHaveBeenCalledOnce();expect(JSON.parse(writes[0])).toEqual({commandId:expect.any(String),version:7,payees:[expect.objectContaining({name:'Local Market',categoryMode:'always',defaultCategoryId:'travel',rules:[{operator:'is',value:'MARKET 001'},{operator:'contains',value:'MARKET-'}]})]});
});

it('undo and redo change only the draft, and cancel never writes',async()=>{
  manage();await ready();await userEvent.click(screen.getByRole('button',{name:'Hide'}));await userEvent.click(screen.getByRole('button',{name:'↶ Undo'}));expect(screen.getByRole('button',{name:'Hide'})).toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'↪ Redo'}));expect(screen.getByRole('button',{name:'Unhide'})).toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'Cancel'}));expect(closed).toHaveBeenCalledOnce();expect(writes).toHaveLength(0);
});

it('validates empty rules, removes them, and protects used payees from deletion',async()=>{
  manage();await ready();await userEvent.click(screen.getByRole('button',{name:'Delete'}));expect(screen.getByRole('alert')).toHaveTextContent('transaction history');await userEvent.click(screen.getByRole('button',{name:'Add renaming rule'}));await userEvent.click(screen.getByRole('button',{name:'Done'}));expect(screen.getByRole('alert')).toHaveTextContent('empty renaming rules');expect(writes).toHaveLength(0);
  await userEvent.click(screen.getByRole('button',{name:'Remove rule 1'}));await userEvent.click(screen.getByRole('button',{name:'Done'}));expect(closed).toHaveBeenCalledOnce();
});

it('selects unused payees, stages deletion and retains the ability to undo',async()=>{
  manage();await ready();await userEvent.click(screen.getByRole('button',{name:'Select 1 Unused Payees'}));expect(screen.getByRole('textbox',{name:'Payee Name'})).toHaveValue('Hidden merchant');await userEvent.click(screen.getByRole('button',{name:'Delete'}));expect(screen.queryByRole('textbox',{name:'Payee Name'})).not.toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'↶ Undo'}));await userEvent.click(screen.getByRole('button',{name:'Hidden merchant (hidden)'}));expect(screen.getByRole('textbox',{name:'Payee Name'})).toHaveValue('Hidden merchant');
});

it('keeps transfer payees tied to account names and supports searching',async()=>{
  manage();await ready();await userEvent.type(screen.getByRole('textbox',{name:'Search Payees'}),'Checking');expect(screen.queryByRole('button',{name:'Market'})).not.toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'Transfer: Checking'}));expect(screen.getByText('Transfer payees follow your account names. Rename the account using Edit Account.')).toBeInTheDocument();expect(screen.queryByRole('textbox',{name:'Payee Name'})).not.toBeInTheDocument();
});

it('retries a lost response with the identical command and blocks closing until resolved',async()=>{
  manage();await ready();failure='network';await userEvent.click(screen.getByRole('button',{name:'Hide'}));await userEvent.click(screen.getByRole('button',{name:'Done'}));await screen.findByRole('button',{name:'Retry save'});await userEvent.keyboard('{Escape}');expect(closed).not.toHaveBeenCalled();expect(screen.getByRole('textbox',{name:'Payee Name'})).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Retry save'}));await waitFor(()=>expect(closed).toHaveBeenCalledOnce());expect(writes).toHaveLength(2);expect(writes[0]).toBe(writes[1]);
});

it('retains a conflicted draft until explicit reload and recovers a failed initial read',async()=>{
  failure='read';manage();await userEvent.click(await screen.findByRole('button',{name:'Retry loading'}));await ready();failure='conflict';await userEvent.click(screen.getByRole('button',{name:'Hide'}));await userEvent.click(screen.getByRole('button',{name:'Done'}));await screen.findByRole('button',{name:'Reload saved payees'});expect(screen.getByRole('button',{name:'Done'})).toBeDisabled();expect(screen.getByRole('button',{name:'Unhide'})).toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'Reload saved payees'}));await ready();expect(screen.getByRole('button',{name:'Hide'})).toBeEnabled();
});

it('shows an empty list and provides retry when suggestions fail',async()=>{
  data={version:7,payees:[]};manage();expect(await screen.findByText('No saved payees yet. Add a named transaction to create one.')).toBeInTheDocument();
});

it('recovers failed suggestions and supplies the saved category on merchant selection',async()=>{
  failure='read';const select=vi.fn();render(<PayeePicker value="" onChange={select} accounts={accounts} categories={categories} onChanged={changed} accountId="one" form="test"/>);
  await userEvent.click(screen.getByRole('combobox',{name:'Payee'}));await userEvent.click(await screen.findByRole('button',{name:'Retry'}));await userEvent.click(await screen.findByRole('option',{name:'Market'}));expect(select).toHaveBeenCalledWith('Market',undefined,'food');
});

it('applies a bulk hidden setting to the selected saved payees in one command',async()=>{
  manage();await ready();await userEvent.click(screen.getByRole('checkbox',{name:'Hidden merchant (hidden)'}));expect(screen.getByText('2 payees selected. Changes below apply to all selected payees.')).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:'Hide'}));await userEvent.click(screen.getByRole('button',{name:'Done'}));await waitFor(()=>expect(changed).toHaveBeenCalledOnce());expect(JSON.parse(writes[0]).payees).toEqual([expect.objectContaining({id:'market',isHidden:true})]);
});
