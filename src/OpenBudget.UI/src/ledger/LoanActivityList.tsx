import { useState } from 'react';
import { formatMoney } from './api';
import { RegisterIcon } from './AccountRegister';
import { type LoanActivityItem, type LoanActivityMonth, dayLabel, monthLabel } from './loans';

export type ActivityTarget = { kind: 'payment'; item: LoanActivityItem } | { kind: 'charge'; item: LoanActivityItem } | { kind: 'interest'; month: LoanActivityMonth };

function Chevron({ open }: { open: boolean }) {
  return <svg className={`loan-chevron ${open ? 'open' : ''}`} viewBox="0 0 16 16" width="14" height="14" aria-hidden="true"><path d="m4 6 4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

const fromLabel = (items: LoanActivityItem[]) => {
  const names = [...new Set(items.map(item => item.accountName ?? 'another account'))];
  return names.length === 1 ? `From ${names[0]}` : 'From multiple accounts';
};
const chargeText = (item: LoanActivityItem) => `${formatMoney(item.amount)} ${item.kind === 'draw' ? `Transfer to ${item.accountName ?? 'another account'}` : item.description || 'Loan activity'} on ${dayLabel(item.date)}`;
const paymentText = (item: LoanActivityItem) => `⇄ ${formatMoney(item.amount)} from ${item.accountName ?? 'another account'} on ${dayLabel(item.date)}`;
const pencil = <span className="loan-row-edit" aria-hidden="true"><RegisterIcon name="edit" size={14} /></span>;

// Account Activity: months with grouped payments, the (estimated or recorded) interest charge and other activity.
// Rows open the matching edit dialog; payments and other activity group when a month has more than one.
export function ActivityList({ months, contract, onEdit, editable }: { months: LoanActivityMonth[]; contract: boolean; onEdit: (target: ActivityTarget) => void; editable: boolean }) {
  const currentYear = months[0]?.month.slice(0, 4);
  const [open, setOpen] = useState<string[]>(() => months.length ? [months[0].month] : []);
  const [years, setYears] = useState<string[]>([]);
  const [groupsOpen, setGroupsOpen] = useState<string[]>([]);
  const groups = months.reduce<{ year: string; months: LoanActivityMonth[] }[]>((list, month) => {
    const year = month.month.slice(0, 4);
    if (list.at(-1)?.year === year) list.at(-1)!.months.push(month); else list.push({ year, months: [month] });
    return list;
  }, []);
  const toggle = (month: string) => setOpen(current => current.includes(month) ? current.filter(m => m !== month) : [...current, month]);
  const toggleGroup = (key: string) => setGroupsOpen(current => current.includes(key) ? current.filter(k => k !== key) : [...current, key]);
  return <section className="loan-activity" aria-label="Account Activity"><h2>Account Activity</h2>
    {groups.map(group => {
      const expanded = group.year === currentYear || years.includes(group.year);
      const past = group.year !== currentYear;
      const monthCards = group.months.map(month => {
          const paymentsKey = `${month.month}:payments`, otherKey = `${month.month}:other`;
          const paymentTotal = month.payments.reduce((sum, item) => sum + item.amount, 0);
          const single = month.payments.length === 1 ? month.payments[0] : null;
          const interest = month.interest;
          const only = month.other.length === 1 ? month.other[0] : null;
          return <article key={month.month} className="loan-month">
            <button className="loan-month-heading" aria-expanded={open.includes(month.month)} onClick={() => toggle(month.month)}><span>{monthLabel(month.month)}<Chevron open={open.includes(month.month)} /></span><b>{formatMoney(month.endBalance)}</b></button>
            {open.includes(month.month) && <div className="loan-month-body">
              {single && <div className="loan-month-row loan-row-editable"><button type="button" className="loan-row-hit" aria-label={`Edit payment of ${formatMoney(single.amount)}`} disabled={!editable} onClick={() => onEdit({ kind: 'payment', item: single })} />
                <span className="loan-tick" aria-hidden="true">✓</span><div><strong>Payments</strong><small>{paymentText(single)}</small></div><b className="positive">{formatMoney(paymentTotal)}</b>{editable && pencil}</div>}
              {month.payments.length > 1 && <>
                <div className="loan-month-row loan-row-group"><button type="button" className="loan-row-hit" aria-expanded={groupsOpen.includes(paymentsKey)} aria-label="Payments" onClick={() => toggleGroup(paymentsKey)} />
                  <span className="loan-tick" aria-hidden="true">✓</span><div><strong>Payments <Chevron open={groupsOpen.includes(paymentsKey)} /></strong><small>{fromLabel(month.payments)} on {dayLabel(month.payments[0].date)}</small></div><b className="positive">{formatMoney(paymentTotal)}</b></div>
                {groupsOpen.includes(paymentsKey) && month.payments.map(item => <div key={item.id} className="loan-month-row loan-row-sub loan-row-editable"><button type="button" className="loan-row-hit" aria-label={`Edit payment of ${formatMoney(item.amount)} on ${dayLabel(item.date)}`} disabled={!editable} onClick={() => onEdit({ kind: 'payment', item })} />
                  <span />{editable && pencil}<div><small><span className="loan-transfer-icon" aria-hidden="true">⇄</span>{paymentText(item).slice(2)}</small></div><b /></div>)}
              </>}
              {!contract && <div className="loan-month-row loan-row-editable"><button type="button" className="loan-row-hit" aria-label="Edit interest" disabled={!editable} onClick={() => onEdit({ kind: 'interest', month })} />
                <span /><div><strong>Interest Charges This Month</strong><small>{interest ? 'Recorded' : 'Estimated'}</small></div><b>{formatMoney(interest ? interest.amount : -month.estimatedInterest)}</b>{editable && pencil}</div>}
              {only && <div className="loan-month-row loan-row-editable"><button type="button" className="loan-row-hit" aria-label="Edit charge" disabled={!editable} onClick={() => onEdit({ kind: 'charge', item: only })} />
                <span className="loan-count" aria-hidden="true">1</span><div><strong>Other Activity</strong><small>{chargeText(only)}</small></div><b>{formatMoney(only.amount)}</b>{editable && pencil}</div>}
              {month.other.length > 1 && <>
                <div className="loan-month-row loan-row-group"><button type="button" className="loan-row-hit" aria-expanded={groupsOpen.includes(otherKey)} aria-label="Other Activity" onClick={() => toggleGroup(otherKey)} />
                  <span className="loan-count" aria-hidden="true">{month.other.length}</span><div><strong>Other Activity <Chevron open={groupsOpen.includes(otherKey)} /></strong><small>{month.other.length} transactions</small></div><b>{formatMoney(month.other.reduce((sum, item) => sum + item.amount, 0))}</b></div>
                {groupsOpen.includes(otherKey) && month.other.map(item => <div key={item.id} className="loan-month-row loan-row-sub loan-row-editable"><button type="button" className="loan-row-hit" aria-label={`Edit charge of ${formatMoney(item.amount)} on ${dayLabel(item.date)}`} disabled={!editable} onClick={() => onEdit({ kind: 'charge', item })} />
                  <span />{editable && pencil}<div><small>{chargeText(item)}</small></div><b /></div>)}
              </>}
              {!month.payments.length && !month.other.length && contract && <p className="loan-help">No activity recorded this month.</p>}
            </div>}
          </article>;
      });
      if (!past) return <div key={group.year} className="loan-year-group">{monthCards}</div>;
      return <article key={group.year} className="loan-month loan-year-card">
        <button className="loan-month-heading" aria-expanded={expanded} onClick={() => setYears(current => expanded ? current.filter(y => y !== group.year) : [...current, group.year])}><span>{group.year}<Chevron open={expanded} /></span><b>{formatMoney(group.months[0].endBalance)}</b></button>
        {expanded && <div className="loan-year-months">{monthCards}</div>}
      </article>;
    })}
  </section>;
}
