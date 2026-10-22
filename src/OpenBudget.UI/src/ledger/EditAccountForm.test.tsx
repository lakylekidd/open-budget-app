import { render,screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect,it,vi } from 'vitest';
import { EditAccountForm } from './EditAccountForm';
import { type Account } from './api';
const account:Account={id:'cash',name:'Cash',notes:'Original note',isClosed:false,balance:10,clearedBalance:10,unclearedBalance:0,accountType:'cash',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0};
it('edits nickname notes and balance, explains adjustment and omits bank connection controls for unlinked accounts',async()=>{
 const save=vi.fn().mockResolvedValue(undefined);render(<EditAccountForm account={account} busy={false} onSubmit={save} onCancel={vi.fn()}/>);
 expect(screen.queryByText('Bank Connection')).not.toBeInTheDocument();expect(screen.getByText(/adjustment transaction/)).toBeInTheDocument();
 await userEvent.clear(screen.getByLabelText('Account Nickname'));await userEvent.type(screen.getByLabelText('Account Nickname'),'Household cash');
 await userEvent.clear(screen.getByLabelText('Account Notes'));await userEvent.type(screen.getByLabelText('Account Notes'),'Updated note');await userEvent.clear(screen.getByLabelText('Working Balance'));await userEvent.type(screen.getByLabelText('Working Balance'),'12.50');
 await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(save).toHaveBeenCalledWith({name:'Household cash',notes:'Updated note',isClosed:false,workingBalance:12.5});
});
it('validates money and supports explicit close/reopen and Cancel',async()=>{
 const save=vi.fn().mockResolvedValue(undefined),cancel=vi.fn();const view=render(<EditAccountForm account={account} busy={false} onSubmit={save} onCancel={cancel}/>);
 await userEvent.clear(screen.getByLabelText('Working Balance'));await userEvent.type(screen.getByLabelText('Working Balance'),'1.001');await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(screen.getByRole('alert')).toHaveTextContent('two decimal');expect(save).not.toHaveBeenCalled();
 await userEvent.click(screen.getByRole('button',{name:'Close Account'}));expect(save).toHaveBeenLastCalledWith({name:'Cash',notes:'Original note',isClosed:true});await userEvent.click(screen.getByRole('button',{name:'Cancel'}));expect(cancel).toHaveBeenCalledOnce();
 view.rerender(<EditAccountForm account={{...account,isClosed:true}} busy={false} onSubmit={save} onCancel={cancel}/>);expect(screen.getByLabelText('Working Balance')).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Reopen Account'}));expect(save).toHaveBeenLastCalledWith({name:'Cash',notes:'Original note',isClosed:false});
});
