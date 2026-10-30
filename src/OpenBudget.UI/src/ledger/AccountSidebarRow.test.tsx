import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect,it,vi } from 'vitest';
import { AccountSidebarRow } from './AccountSidebarRow';
import { type Account } from './api';
it('marks only the selected account and exposes a separate accessible pencil without bank checkmarks',async()=>{
 const select=vi.fn(),edit=vi.fn();const account:Account={id:'cash',name:'Travel',isClosed:false,balance:-334.62,clearedBalance:0,unclearedBalance:-334.62,accountType:'checking',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0};
 const view=render(<AccountSidebarRow account={account} selected disabled={false} onSelect={select} onEdit={edit}/>);
 expect(screen.getByRole('button',{name:'Travel -€334.62'})).toHaveAttribute('aria-current','page');expect(screen.getByText('-€334.62')).toHaveClass('negative-account-balance');
 await userEvent.click(screen.getByRole('button',{name:'Edit Travel account'}));expect(edit).toHaveBeenCalledOnce();expect(select).not.toHaveBeenCalled();
 await userEvent.click(screen.getByRole('button',{name:'Travel -€334.62'}));expect(select).toHaveBeenCalledOnce();expect(screen.queryByText('✓')).not.toBeInTheDocument();expect(view.container.querySelector('.account-dot')).not.toBeInTheDocument();view.rerender(<AccountSidebarRow account={{...account,unapprovedCount:2}} selected disabled={false} onSelect={select} onEdit={edit}/>);expect(view.container.querySelector('.account-dot')).toBeInTheDocument();
});
it.each(['creditCard','lineOfCredit','mortgage','contract'] as const)('shows the %s account name without an automatic icon',accountType=>{
 const account:Account={id:'debt',name:'Visa',isClosed:false,balance:-50,clearedBalance:0,unclearedBalance:-50,accountType,isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0};
 render(<AccountSidebarRow account={account} selected={false} disabled={false} onSelect={vi.fn()} onEdit={vi.fn()}/>);
 expect(screen.getByRole('button',{name:'Visa -€50.00'}).querySelector('span')).toHaveTextContent(/^Visa$/);
});
