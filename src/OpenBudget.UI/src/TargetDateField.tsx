import { useEffect, useId, useRef, useState } from 'react';
import { displayDate, parseRegisterDate } from './ledger/TransactionDatePicker';

const weekdayHeadings = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
const iso = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

// dd-MM-yyyy date input with a month calendar popover. Typed text is accepted when it is a valid date.
export function TargetDateField({ label, value, disabled, onChange }: { label: string; value: string; disabled?: boolean; onChange: (value: string | null) => void }) {
  const id = useId();
  const root = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const [text, setText] = useState(value ? displayDate(value) : '');
  const [open, setOpen] = useState(false);
  const [shown, setShown] = useState((value || iso(new Date())).slice(0, 7));

  useEffect(() => { setText(value ? displayDate(value) : ''); if (value) setShown(value.slice(0, 7)); }, [value]);
  useEffect(() => {
    if (!open) return;
    const close = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, [open]);

  const [year, monthNumber] = shown.split('-').map(Number);
  const first = new Date(year, monthNumber - 1, 1);
  const days = Array.from({ length: 42 }, (_, index) => new Date(year, monthNumber - 1, index + 1 - first.getDay()));
  const lastRowNeeded = days.slice(35).some(day => day.getMonth() === monthNumber - 1);
  const visibleDays = lastRowNeeded ? days : days.slice(0, 35);

  function move(delta: number) {
    const next = new Date(year, monthNumber - 1 + delta, 1);
    if (next.getFullYear() >= 1900 && next.getFullYear() <= 9998) setShown(`${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`);
  }

  function choose(date: string) {
    onChange(date);
    setText(displayDate(date));
    setOpen(false);
    input.current?.focus();
  }

  return <div className="target-field target-date-field" ref={root} onKeyDown={event => {
    if (event.key === 'Escape' && open) { event.stopPropagation(); setOpen(false); input.current?.focus(); }
  }}>
    <label htmlFor={id}>{label}</label>
    <div className="target-date-input">
      <input id={id} ref={input} value={text} disabled={disabled} placeholder="dd-mm-yyyy" inputMode="numeric"
        onChange={event => { setText(event.target.value); onChange(parseRegisterDate(event.target.value)); }}
        onKeyDown={event => { if (event.key === 'ArrowDown' && event.altKey) { event.preventDefault(); setOpen(true); } }} />
      <button type="button" aria-label={`Choose ${label.toLowerCase()} date`} aria-expanded={open} disabled={disabled} onClick={() => setOpen(current => !current)}>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18M8 3v4M16 3v4" /></svg>
      </button>
    </div>
    {open && <div className="target-calendar" role="dialog" aria-label={`${label} calendar`}>
      <div className="target-calendar-heading">
        <button type="button" aria-label="Previous month" onClick={() => move(-1)}>‹</button>
        <strong>{first.toLocaleDateString('en-GB', { month: 'short', year: 'numeric' })}</strong>
        <button type="button" aria-label="Next month" onClick={() => move(1)}>›</button>
      </div>
      <div className="target-calendar-grid">
        {weekdayHeadings.map(day => <span key={day} className="target-calendar-weekday">{day}</span>)}
        {visibleDays.map(day => {
          const key = iso(day);
          return <button type="button" key={key} className={day.getMonth() === monthNumber - 1 ? '' : 'outside'} aria-pressed={key === value}
            aria-label={day.toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' })} onClick={() => choose(key)}>{day.getDate()}</button>;
        })}
      </div>
    </div>}
  </div>;
}
