import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { useState } from 'react';
import { defaultFlags, FlagButton } from './Flags';
it('selects and clears a named colored flag and supports numeric shortcuts',async()=>{
 const save=vi.fn();function Test(){const [color,setColor]=useState<string|null>(null);return <FlagButton color={color} flags={defaultFlags} disabled={false} onSet={async c=>{save(c);setColor(c);return true;}} onRename={vi.fn()}/>;}
 render(<Test/>);await userEvent.click(screen.getByRole('button',{name:'Flag transaction'}));await userEvent.click(screen.getByRole('button',{name:'Use Red flag'}));
 expect(save).toHaveBeenLastCalledWith('red');expect(screen.getByRole('button',{name:'Flag transaction: Red'})).toHaveClass('flag-red');
 await userEvent.click(screen.getByRole('button',{name:'Flag transaction: Red'}));await userEvent.keyboard('5');expect(save).toHaveBeenLastCalledWith('blue');
 await userEvent.click(screen.getByRole('button',{name:'Flag transaction: Blue'}));await userEvent.click(screen.getByRole('button',{name:'Clear flag'}));expect(save).toHaveBeenLastCalledWith(null);
});
it('edits six flag names with Cancel/Done and keeps failed drafts recoverable',async()=>{
 const rename=vi.fn().mockResolvedValue(false);render(<FlagButton flags={defaultFlags} disabled={false} onSet={vi.fn()} onRename={rename}/>);
 await userEvent.click(screen.getByRole('button',{name:'Flag transaction'}));await userEvent.click(screen.getByRole('button',{name:'Edit Flags'}));
 const dialog=screen.getByRole('dialog',{name:'Edit Flags'});const blue=within(dialog).getByRole('textbox',{name:'blue flag name'});await userEvent.clear(blue);await userEvent.type(blue,'Household pays');
 await userEvent.click(within(dialog).getByRole('button',{name:'Done'}));expect(rename).toHaveBeenCalledWith(defaultFlags.map(f=>f.color==='blue'?{...f,name:'Household pays'}:f));expect(await screen.findByRole('alert')).toHaveTextContent('could not be saved');
 await userEvent.click(within(dialog).getByRole('button',{name:'Cancel'}));expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});
