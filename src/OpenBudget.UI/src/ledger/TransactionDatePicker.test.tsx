import { render,screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect,it,vi } from 'vitest';
import { useState } from 'react';
import { parseRegisterDate,repeatOptions,TransactionDatePicker } from './TransactionDatePicker';
import { parseRegisterMoney } from './InlineTransactionEditor';
it.each([['29-02-2024','2024-02-29'],['29-02-2025',null],['31-04-2026',null],['2026-10-21','2026-10-21'],['21-10-1899',null],['garbage',null]])('validates register date %s',(input,expected)=>expect(parseRegisterDate(input)).toBe(expected));
it.each([['2,005.18',2005.18],['20,50',20.5],['1,00.00',null],['20.001',null]])('parses register money %s without losing precision',(input,expected)=>expect(parseRegisterMoney(input)).toBe(expected));
it('opens a matching calendar, offers every repeat choice, and selects a valid day across months',async()=>{
 const changed=vi.fn();function Test(){const [date,setDate]=useState('2024-02-29');const [repeat,setRepeat]=useState('never');return <TransactionDatePicker date={date} repeat={repeat} onDate={d=>{setDate(d);changed(d);}} onRepeat={setRepeat} onValid={vi.fn()} initiallyOpen/>;}
 render(<Test/>);expect(screen.getByText('Feb 2024')).toBeInTheDocument();expect(screen.getAllByRole('option')).toHaveLength(repeatOptions.length);
 await userEvent.selectOptions(screen.getByRole('combobox',{name:'Repeat'}),'monthly');expect(screen.getByRole('combobox',{name:'Repeat'})).toHaveValue('monthly');
 await userEvent.click(screen.getByRole('button',{name:'Next calendar month'}));await userEvent.click(screen.getByRole('button',{name:'31 March 2024'}));expect(changed).toHaveBeenLastCalledWith('2024-03-31');
});
