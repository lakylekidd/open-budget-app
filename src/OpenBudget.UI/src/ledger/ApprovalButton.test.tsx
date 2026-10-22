import { render,screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect,it,vi } from 'vitest';
import { ApprovalButton } from './ApprovalButton';
it('opens an explicit Approve/Reject menu and submits only the chosen review',async()=>{
 const review=vi.fn().mockResolvedValue(true);render(<ApprovalButton disabled={false} onReview={review}/>);
 await userEvent.click(screen.getByRole('button',{name:'Review unapproved transaction'}));expect(review).not.toHaveBeenCalled();await userEvent.click(screen.getByRole('button',{name:'Approve Transaction'}));expect(review).toHaveBeenLastCalledWith('approve');
 await userEvent.click(screen.getByRole('button',{name:'Review unapproved transaction'}));await userEvent.click(screen.getByRole('button',{name:'Reject Transaction'}));expect(review).toHaveBeenLastCalledWith('reject');
});
