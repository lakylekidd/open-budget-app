import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { useState } from 'react';
import { CategoryPicker, categoryGroups } from './CategoryPicker';
import { type CategoryOption } from './api';

const categories:CategoryOption[]=[
  {id:'stream',name:'HBO Max',groupId:'subscriptions',groupName:'Streaming Services',available:0},
  {id:'food',name:'Groceries',groupId:'everyday',groupName:'Everyday',available:123.45},
  {id:'travel',name:'Travel',groupId:'life',groupName:'Life & Fun',available:-27.35},
  {id:'other',name:'Travel',groupId:'work',groupName:'Work',available:10},
  {id:'old',name:'Old category',groupId:'life',groupName:'Life & Fun',available:999,isArchived:true}
];
function Picker(){const [value,setValue]=useState('stream');return <CategoryPicker value={value} categories={categories} readyToAssign={-35.14} onChange={setValue}/>;}

it('groups active categories in budget order and searches both group and category names',()=>{
  expect(categoryGroups(categories,'').map(g=>g.name)).toEqual(['Streaming Services','Everyday','Life & Fun','Work']);
  expect(categoryGroups(categories,' STREAMING ')[0].categories[0].id).toBe('stream');
  expect(categoryGroups(categories,'travel').flatMap(g=>g.categories).map(c=>c.id)).toEqual(['travel','other']);
  expect(categoryGroups(categories,'Old category')).toEqual([]);
});

it('shows the selected category with its parent and the actual available amounts with sign colors',async()=>{
  render(<Picker/>);await userEvent.click(screen.getByRole('combobox',{name:'Category'}));
  expect(screen.getByRole('heading',{name:'Selected'})).toBeInTheDocument();
  expect(screen.getByText('(Streaming Services)')).toBeInTheDocument();
  expect(within(screen.getByRole('group',{name:'Everyday'})).getByText('Groceries')).toBeInTheDocument();
  expect(screen.getByLabelText('Groceries available')).toHaveTextContent('€123.45');expect(screen.getByLabelText('Groceries available')).toHaveClass('positive');
  expect(screen.getByLabelText('Ready to Assign available')).toHaveTextContent('-€35.14');expect(screen.getByLabelText('Ready to Assign available')).toHaveClass('negative');
  expect(screen.queryByText('Old category')).not.toBeInTheDocument();
});

it('selects by category id when names repeat, closes and displays the parent in the editor',async()=>{
  const select=vi.fn();render(<CategoryPicker value="" categories={categories} onChange={select}/>);await userEvent.click(screen.getByRole('combobox',{name:'Category'}));
  await userEvent.click(within(screen.getByRole('group',{name:'Work'})).getByRole('option'));expect(select).toHaveBeenCalledWith('other');expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
});

it('searches, navigates with the keyboard and Escape discards search without cancelling the editor',async()=>{
  const escape=vi.fn();render(<div onKeyDown={e=>{if(e.key==='Escape')escape();}}><Picker/></div>);const input=screen.getByRole('combobox',{name:'Category'});
  await userEvent.click(input);await userEvent.clear(input);await userEvent.type(input,'grocer');await userEvent.keyboard('{ArrowDown}{Enter}');expect(input).toHaveValue('Everyday: Groceries');
  await userEvent.click(input);await userEvent.clear(input);await userEvent.type(input,'missing');expect(screen.getByText('No matching categories.')).toBeInTheDocument();await userEvent.keyboard('{Escape}');expect(input).toHaveValue('Everyday: Groceries');expect(escape).not.toHaveBeenCalled();
});

it('Ready to Assign clears a category and unknown balances display a dash rather than invented zero',async()=>{
  const change=vi.fn();render(<CategoryPicker value="food" categories={[{id:'food',name:'Food'}]} onChange={change}/>);await userEvent.click(screen.getByRole('combobox',{name:'Category'}));
  expect(screen.getByLabelText('Ready to Assign available')).toHaveTextContent('—');await userEvent.click(screen.getByRole('option',{name:/Ready to Assign/}));expect(change).toHaveBeenCalledWith('');
});

it('opens New Category, Split and Payment/Transfer without submitting the transaction form',async()=>{
  const split=vi.fn(),transfer=vi.fn(),create=vi.fn(),submit=vi.fn();render(<form onSubmit={submit}><CategoryPicker value="" categories={categories} onChange={vi.fn()} onNewCategory={create} onSplit={split} onTransfer={transfer}/></form>);
  for(const [name,callback] of [['New Category',create],['Split',split],['Payment/Transfer',transfer]] as const){await userEvent.click(screen.getByRole('combobox',{name:'Category'}));await userEvent.click(screen.getByRole('button',{name:new RegExp(name)}));expect(callback).toHaveBeenCalledOnce();expect(screen.queryByRole('listbox')).not.toBeInTheDocument();}
  expect(submit).not.toHaveBeenCalled();
});

it('has loading, failed-read recovery, and disabled transfer/opening states',async()=>{
  const retry=vi.fn();const {rerender}=render(<CategoryPicker value="" categories={[]} loading onChange={vi.fn()}/>);await userEvent.click(screen.getByRole('combobox',{name:'Category'}));expect(screen.getByRole('status')).toHaveTextContent('Loading categories');
  rerender(<CategoryPicker value="" categories={[]} error onRetry={retry} onChange={vi.fn()}/>);await userEvent.click(screen.getByRole('button',{name:'Retry categories'}));expect(retry).toHaveBeenCalledOnce();
  rerender(<CategoryPicker value="" categories={[]} notNeeded onChange={vi.fn()}/>);expect(screen.getByRole('combobox',{name:'Category'})).toBeDisabled();
});

it('requests fresh balances each time the dropdown opens, preserving the current query while open',async()=>{
  const reload=vi.fn();render(<CategoryPicker value="" categories={categories} onChange={vi.fn()} onOpen={reload}/>);const input=screen.getByRole('combobox',{name:'Category'});await userEvent.click(input);await userEvent.type(input,'grocer');expect(reload).toHaveBeenCalledOnce();expect(input).toHaveValue('grocer');await userEvent.keyboard('{Escape}');await userEvent.click(input);expect(reload).toHaveBeenCalledTimes(2);
});

it('cannot select stale hidden options with the keyboard during loading or a failed read',async()=>{
  const change=vi.fn();const {rerender}=render(<CategoryPicker value="food" categories={categories} loading onChange={change}/>);await userEvent.click(screen.getByRole('combobox',{name:'Category'}));await userEvent.keyboard('{ArrowDown}{Enter}');expect(change).not.toHaveBeenCalled();rerender(<CategoryPicker value="food" categories={categories} error onChange={change}/>);await userEvent.keyboard('{ArrowDown}{Enter}');expect(change).not.toHaveBeenCalled();
});
