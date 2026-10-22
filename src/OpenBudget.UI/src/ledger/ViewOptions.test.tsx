import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { defaultView, presetMonths, viewDates, ViewOptionsPanel } from './ViewOptions';

it.each([
  ['thisMonth','2026-10-21','2026-10-01','2026-10-31'],
  ['lastMonth','2026-01-15','2025-12-01','2025-12-31'],
  ['latest3Months','2026-01-15','2025-11-01','2026-01-31'],
  ['thisYear','2026-10-21','2026-01-01','2026-12-31'],
  ['lastYear','2026-10-21','2025-01-01','2025-12-31'],
  ['allDates','2026-10-21','',''],
])('resolves %s to inclusive calendar boundaries', (datePreset, now, from, to) => {
  expect(viewDates({...defaultView,datePreset},now)).toEqual({from,to});
});

it('handles leap February and a persisted rolling preset on another date', () => {
  expect(viewDates({...defaultView,datePreset:'custom',fromMonth:'2024-02',toMonth:'2024-02'})).toEqual({from:'2024-02-01',to:'2024-02-29'});
  expect(presetMonths('thisMonth','2027-02-01')).toEqual(['2027-02','2027-02']);
});

it('keeps changes as a draft, cancels without saving, and submits all selections on Apply', async () => {
  const apply = vi.fn().mockResolvedValue(undefined); const cancel = vi.fn();
  render(<ViewOptionsPanel value={defaultView} busy={false} onApply={apply} onCancel={cancel} onReload={vi.fn()}/>);
  await userEvent.click(screen.getByRole('button',{name:'Last Year'}));
  for (const name of ['Show Reconciled Transactions','Show Running Balance','Show Flags','Show Images']) await userEvent.click(screen.getByRole('checkbox',{name}));
  expect(apply).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button',{name:'Cancel'})); expect(cancel).toHaveBeenCalledOnce(); expect(apply).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button',{name:'Apply'}));
  expect(apply).toHaveBeenCalledWith(expect.objectContaining({datePreset:'lastYear',showReconciled:false,showRunningBalance:false,showFlags:false,showImages:false}));
});

it('switches edited month ranges to custom and rejects reversed months before saving', async () => {
  const apply = vi.fn();
  render(<ViewOptionsPanel value={{...defaultView,fromMonth:'2026-10',toMonth:'2026-10'}} busy={false} onApply={apply} onCancel={vi.fn()} onReload={vi.fn()}/>);
  await userEvent.selectOptions(screen.getByRole('combobox',{name:'From month'}),'12');
  await userEvent.click(screen.getByRole('button',{name:'Apply'}));
  expect(screen.getByRole('alert')).toHaveTextContent('From month on or before'); expect(apply).not.toHaveBeenCalled();
  await userEvent.selectOptions(screen.getByRole('combobox',{name:'To year'}),'2036');
  await userEvent.click(screen.getByRole('button',{name:'Apply'}));
  expect(apply).toHaveBeenCalledWith(expect.objectContaining({datePreset:'custom',fromMonth:'2026-12',toMonth:'2036-10'}));
});

it('retains failed drafts with reload recovery and supports Escape', async () => {
  const reload = vi.fn().mockResolvedValue(undefined); const cancel = vi.fn();
  render(<ViewOptionsPanel value={defaultView} busy={false} onApply={vi.fn().mockRejectedValue(new Error('Changed in another session'))} onCancel={cancel} onReload={reload}/>);
  await userEvent.click(screen.getByRole('checkbox',{name:'Show Flags'}));
  await userEvent.click(screen.getByRole('button',{name:'Apply'}));
  expect(await screen.findByRole('alert')).toHaveTextContent('Changed in another session');
  expect(screen.getByRole('checkbox',{name:'Show Flags'})).not.toBeChecked();
  await userEvent.click(screen.getByRole('button',{name:'Reload saved options'})); expect(reload).toHaveBeenCalledOnce(); expect(cancel).toHaveBeenCalledOnce();
  await userEvent.keyboard('{Escape}'); expect(cancel).toHaveBeenCalledTimes(2);
});
