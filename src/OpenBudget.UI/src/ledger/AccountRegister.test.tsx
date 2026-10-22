import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { AccountHeader, ClearedIndicator, balanceSummary, reconciliationText } from './AccountRegister';
import { type Account, type Entry } from './api';

const account: Account = { id:'cash', name:'Salary', isClosed:false, balance:-500, clearedBalance:0, unclearedBalance:-500,
  accountType:'checking', isFavorite:false, connectionStatus:'unlinked', lastReconciledAt:null, needsCategoryCount:0 };
const entry: Entry = { id:'one',accountId:'cash',date:'2026-10-21',amount:-500,payee:'Shop',memo:'',categoryId:null,status:'cleared',transferId:null,isOpening:false,splits:[],needsAttention:true,runningBalance:-500,transferAccountName:null,categoryName:null,isLocked:false };

it('shows cleared plus uncleared equals working and truthful connection status', () => {
  render(<AccountHeader current={account} accounts={[account]} disabled={false} onEdit={vi.fn()} onReconcile={vi.fn()} onFavorite={vi.fn()} onAddAccount={vi.fn()} onMenu={vi.fn()}/>);
  expect(screen.getByTestId('cleared-balance')).toHaveTextContent('€0.00');
  expect(screen.getByTestId('uncleared-balance')).toHaveTextContent('-€500.00');
  expect(screen.getByTestId('working-balance')).toHaveTextContent('-€500.00');
  expect(screen.getByText('Unlinked')).toBeInTheDocument(); expect(screen.queryByText('Connected')).not.toBeInTheDocument();
  expect(screen.getByText('Not reconciled')).toBeInTheDocument();
});

it('only shows connected when the account reports automatic sync and uses actual reconciliation time', () => {
  render(<AccountHeader current={{...account,connectionStatus:'connected',lastReconciledAt:new Date().toISOString()}} accounts={[]} disabled={false} onEdit={vi.fn()} onReconcile={vi.fn()} onFavorite={vi.fn()} onAddAccount={vi.fn()} onMenu={vi.fn()}/>);
  expect(screen.getByText('Connected')).toBeInTheDocument(); expect(screen.getByText('Reconciled today')).toBeInTheDocument();
});

it('sums displayed account totals in integer cents', () => {
  const totals = balanceSummary([{...account,balance:0.1,clearedBalance:0.1,unclearedBalance:0},{...account,balance:0.2,clearedBalance:0,unclearedBalance:0.2}]);
  expect(totals).toEqual({working:0.3,cleared:0.1,uncleared:0.2});
  expect(Math.round((totals.cleared + totals.uncleared)*100)).toBe(Math.round(totals.working*100));
});

it('uses a green c for cleared and a disabled lock for reconciled', async () => {
  const onToggle = vi.fn(); const view = render(<ClearedIndicator entry={entry} disabled={false} onToggle={onToggle}/>);
  const cleared = screen.getByRole('button',{name:'Cleared transaction: mark uncleared'});
  expect(cleared).toHaveClass('status-cleared'); await userEvent.click(cleared); expect(onToggle).toHaveBeenCalledOnce();
  view.rerender(<ClearedIndicator entry={{...entry,status:'reconciled'}} disabled={false} onToggle={onToggle}/>);
  expect(screen.getByRole('button',{name:'Reconciled transaction (locked)'})).toBeDisabled();
  expect(screen.getByRole('button',{name:'Reconciled transaction (locked)'}).querySelector('svg')).not.toBeNull();
});

it('describes reconciliation age without inventing a date', () => {
  const now=Date.parse('2026-10-21T12:00:00Z');
  expect(reconciliationText(null,now)).toBe('Not reconciled');
  expect(reconciliationText('2026-10-20T12:00:00Z',now)).toBe('Reconciled yesterday');
  expect(reconciliationText('2026-09-20T12:00:00Z',now)).toBe('Reconciled a month ago');
});
