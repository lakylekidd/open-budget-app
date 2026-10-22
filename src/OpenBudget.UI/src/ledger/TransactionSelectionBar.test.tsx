import { render,screen,within,waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach,expect,it,vi } from 'vitest';
import { type Account,type Entry } from './api';
import { defaultFlags } from './Flags';
import { selectedTransactionsCsv,TransactionSelectionBar } from './TransactionSelectionBar';

const accounts=[{id:'one',name:'Checking',isClosed:false},{id:'two',name:'Savings',isClosed:false},{id:'closed',name:'Closed',isClosed:true}] as Account[];
const entry:Entry={id:'a',accountId:'one',date:'2026-10-21',amount:-12.35,payee:'Cafe',memo:'Lunch',categoryId:'food',categoryName:'Everyday: Food',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:87.65,transferAccountName:null,isLocked:false};
const apply=vi.fn();const clear=vi.fn();
beforeEach(()=>{apply.mockReset().mockResolvedValue(true);clear.mockReset();});
function show(entries=[entry],disabled=false,scheduled=false){render(<TransactionSelectionBar entries={entries} accounts={accounts} flags={defaultFlags} disabled={disabled} scheduled={scheduled} onApply={apply} onClear={clear} onRenameFlags={vi.fn()}/>);}
async function menu(){await userEvent.click(screen.getByRole('button',{name:'More selected transaction actions'}));return screen.getByRole('menu',{name:'Selected transaction actions'});}

it('shows the count, clear, flag and More controls with the reference menu options',async()=>{
  show();expect(screen.getByText('1 Transaction')).toBeInTheDocument();await menu();
  for(const name of [/Mark as Cleared/,/^Match/,/^Unmatch/,/Duplicate/,/Make Repeating/,/Memo/,/Move to Account/,/Export 1 Transaction/,/^Delete/])expect(screen.getByRole('menuitem',{name})).toBeInTheDocument();
  expect(screen.getByRole('menuitem',{name:/^Match/})).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Clear transaction selection'}));expect(clear).toHaveBeenCalledOnce();
});
it('clears or unclears all selected entries using a single action',async()=>{
  show([{...entry,status:'cleared'},{...entry,id:'b',status:'cleared'}]);expect(screen.getByText('2 Transactions')).toBeInTheDocument();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/Mark as Uncleared/}));expect(apply).toHaveBeenCalledWith({action:'status',status:'uncleared'});
});
it('duplicates and selects a recurring frequency without accidental submission',async()=>{
  show();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/Duplicate/}));expect(apply).toHaveBeenCalledWith({action:'duplicate'});apply.mockClear();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/Make Repeating/}));await userEvent.selectOptions(screen.getByRole('combobox',{name:'Repeat selected transactions'}),'weekly');expect(apply).not.toHaveBeenCalled();await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(apply).toHaveBeenCalledWith({action:'repeat',repeat:'weekly'});
});
it('retains a memo after a rejected save and requires confirmation for reconciled edits',async()=>{
  apply.mockResolvedValue(false);show([{...entry,status:'reconciled',isLocked:true}]);await menu();expect(screen.getByRole('menuitem',{name:/^Delete/})).toBeDisabled();await userEvent.click(screen.getByRole('menuitem',{name:/Memo/}));await userEvent.clear(screen.getByRole('textbox',{name:'Memo for selected transactions'}));await userEvent.type(screen.getByRole('textbox',{name:'Memo for selected transactions'}),'Corrected');await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(screen.getByRole('alert')).toHaveTextContent('Confirm the warning');expect(apply).not.toHaveBeenCalled();await userEvent.click(screen.getByRole('checkbox'));await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(apply).toHaveBeenCalledWith({action:'memo',memo:'Corrected',confirmReconciledEdit:true});expect(screen.getByRole('textbox')).toHaveValue('Corrected');
});
it('offers open destination accounts and confirms moving the selection',async()=>{
  show();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/Move to Account/}));const choices=within(screen.getByRole('group',{name:'Destination accounts'}));expect(choices.getByRole('menuitem',{name:'Checking'})).toBeDisabled();expect(choices.queryByRole('menuitem',{name:'Closed'})).not.toBeInTheDocument();await userEvent.click(choices.getByRole('menuitem',{name:'Savings'}));await userEvent.click(screen.getByRole('button',{name:/^Move$/}));expect(apply).toHaveBeenCalledWith({action:'move',accountId:'two',confirmReconciledEdit:false});
});
it('confirms deletion and cancels without a write',async()=>{
  show();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/^Delete/}));await userEvent.click(screen.getByRole('button',{name:'Cancel'}));expect(apply).not.toHaveBeenCalled();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/^Delete/}));await userEvent.click(screen.getByRole('button',{name:'Delete'}));expect(apply).toHaveBeenCalledWith({action:'delete'});
});
it('flags the whole selection with the existing named flag picker',async()=>{
  show();await userEvent.click(screen.getByRole('button',{name:'Flag transaction'}));await userEvent.click(screen.getByRole('button',{name:'Use Green flag'}));expect(apply).toHaveBeenCalledWith({action:'flag',color:'green'});
});
it('keeps destructive actions disabled for opening balances, transfers and scheduled forecasts',async()=>{
  show([{...entry,isOpening:true,transferId:'pair'}]);await menu();for(const name of [/Duplicate/,/Make Repeating/,/Move to Account/])expect(screen.getByRole('menuitem',{name})).toBeDisabled();
});
it('supports menu keyboard navigation and shortcuts without hijacking memo input',async()=>{
  show();await menu();await userEvent.keyboard('{Home}');expect(screen.getByRole('menuitem',{name:/Mark as Cleared/})).toHaveFocus();await userEvent.keyboard('{ArrowDown}');expect(screen.getByRole('menuitem',{name:/Duplicate/})).toHaveFocus();await userEvent.keyboard('{Escape}');expect(screen.queryByRole('menu')).not.toBeInTheDocument();expect(screen.getByRole('button',{name:'More selected transaction actions'})).toHaveFocus();await userEvent.keyboard('C');expect(apply).toHaveBeenCalledWith({action:'status',status:'cleared'});apply.mockClear();await menu();await userEvent.click(screen.getByRole('menuitem',{name:/Memo/}));await userEvent.type(screen.getByRole('textbox'),'C');expect(apply).not.toHaveBeenCalled();
});
it('blocks actions and closing selection while an uncertain request is pending',async()=>{
  show([entry],true);expect(screen.getByRole('button',{name:'More selected transaction actions'})).toBeDisabled();expect(screen.getByRole('button',{name:'Clear transaction selection'})).toBeDisabled();await userEvent.keyboard('C{Delete}');expect(apply).not.toHaveBeenCalled();
});
it('exports only selected transactions with escaped names, precise money and safe spreadsheet cells',()=>{
  const csv=selectedTransactionsCsv([{...entry,payee:'Cafe, "Local"',memo:'=SUM(1,2)\nnext line'},{...entry,id:'b',amount:25,memo:'Refund',payee:'Coffee ☕'}],accounts);
  expect(csv).toContain('"Cafe, ""Local"""');expect(csv).toContain('"\'=SUM(1,2)\nnext line"');expect(csv).toContain(',12.35,,');expect(csv).toContain(',,25.00,');expect(csv).toContain('Coffee ☕');expect(csv.startsWith('\uFEFFDate,')).toBe(true);expect(csv).not.toContain('87.65');
});
it('downloads CSV without changing the ledger',async()=>{
  const create=vi.fn(()=> 'blob:test');const revoke=vi.fn();vi.stubGlobal('URL',Object.assign(URL,{createObjectURL:create,revokeObjectURL:revoke}));const click=vi.spyOn(HTMLAnchorElement.prototype,'click').mockImplementation(()=>{});
  show();await menu();await userEvent.click(screen.getByRole('menuitem',{name:'Export 1 Transaction'}));expect(create).toHaveBeenCalledOnce();expect(click).toHaveBeenCalledOnce();expect(apply).not.toHaveBeenCalled();await waitFor(()=>expect(screen.queryByRole('menu')).not.toBeInTheDocument());click.mockRestore();
});
