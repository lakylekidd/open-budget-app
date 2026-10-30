import { useEffect, useId, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import { ApiError, parseMoney, requestJson, today } from './ledger/api';
import { displayDate } from './ledger/TransactionDatePicker';
import { ProgressRing } from './CreditPayoffTarget';
import { TargetDateField } from './TargetDateField';
import { MoneyInput } from './MoneyInput';
import {
  nextMonthStart, ordinal, repeatUnits, targetKinds, targetPath, weekdays,
  type CategoryTarget, type CategoryTargetResult, type RepeatUnit, type TargetBehavior, type TargetFields, type TargetKind, type TargetProgress,
} from './categoryTargets';

type Money = (amount: number) => string;
export type TargetCategory = { id: string; name: string; icon?: string | null; categoryTarget?: CategoryTarget | null; targetProgress?: TargetProgress | null };

const trashIcon = <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M9 2h6l1 2h5v2H3V4h5l1-2Zm-4 6h14l-1.2 13.1A2 2 0 0 1 15.8 23H8.2a2 2 0 0 1-2-1.9L5 8Z" /></svg>;

// Money input that shows the formatted amount (€0.00) until it receives focus.
function MoneyField({ label, value, disabled, money, onChange }: { label: string; value: string; disabled: boolean; money: Money; onChange: (value: string) => void }) {
  const id = useId();
  const [focused, setFocused] = useState(false);
  const parsed = parseMoney(value || '0');
  return <div className="target-field">
    <label htmlFor={id}>{label}</label>
    <MoneyInput id={id} className="target-input" disabled={disabled}
      value={focused ? value : parsed !== null ? money(parsed) : value}
      onFocus={event => { setFocused(true); const input = event.target; requestAnimationFrame(() => input.select()); }}
      onBlur={() => setFocused(false)} onChange={raw => onChange(raw.replace(/[€\s]/g, ''))} />
  </div>;
}

function NativeSelect({ label, value, disabled, options, onChange }: {
  label: string; value: string; disabled: boolean; options: { value: string; label: string }[]; onChange: (value: string) => void;
}) {
  const id = useId();
  return <div className="target-field">
    <label htmlFor={id}>{label}</label>
    <select id={id} className="target-input target-select" value={value} disabled={disabled} onChange={event => onChange(event.target.value)}>
      {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  </div>;
}

// "Next month I want to" choice between setting aside another amount and refilling up to it.
function BehaviorPicker({ label, kind, behavior, amount, disabled, onChange }: {
  label: string; kind: TargetKind; behavior: TargetBehavior; amount: string; disabled: boolean; onChange: (behavior: TargetBehavior) => void;
}) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const listId = useId();
  const labelId = useId();
  const refillLabel = kind === 'custom' ? 'Fill up to' : 'Refill up to';
  const period = kind === 'yearly' ? 'year' : 'month';
  const choices: { id: TargetBehavior; title: string; useFor: string; note?: string; pill?: string }[] = [
    { id: 'setAside', title: `Set aside another ${amount}`, useFor: 'bills, subscriptions, saving over time', pill: 'Most people choose this' },
    { id: 'refill', title: `${refillLabel} ${amount}`, useFor: 'gasoline, fun money, dining out',
      note: kind === 'custom' ? `Sets a Target to have ${amount} on hand by the due date.` : `Sets a Target to have ${amount} on hand each ${period}. Whatever you don't spend will get applied toward next ${period}'s ${amount}.` },
  ];
  const current = choices.find(choice => choice.id === behavior) ?? choices[0];

  useEffect(() => {
    if (!open) return;
    root.current?.querySelector<HTMLElement>('[aria-selected="true"]')?.focus();
    const close = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, [open]);

  function choose(next: TargetBehavior) {
    onChange(next);
    setOpen(false);
    root.current?.querySelector<HTMLButtonElement>('button')?.focus();
  }

  function keys(event: ReactKeyboardEvent<HTMLLIElement>, index: number) {
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose(choices[index].id); }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const items = root.current?.querySelectorAll<HTMLElement>('[role="option"]');
      items?.[(index + (event.key === 'ArrowDown' ? 1 : -1) + choices.length) % choices.length]?.focus();
    }
  }

  return <div className="target-field target-behavior" ref={root} onKeyDown={event => {
    if (event.key === 'Escape' && open) { event.stopPropagation(); setOpen(false); root.current?.querySelector<HTMLButtonElement>('button')?.focus(); }
  }}>
    <span id={labelId} className="target-label">{label}</span>
    <button type="button" className="target-input target-select" aria-labelledby={labelId} aria-haspopup="listbox" aria-expanded={open}
      aria-controls={open ? listId : undefined} disabled={disabled} onClick={() => setOpen(value => !value)}>{current.title}</button>
    {open && <ul className="target-behavior-popover" id={listId} role="listbox" aria-labelledby={labelId}>
      {choices.map((choice, index) => <li key={choice.id} role="option" aria-selected={choice.id === behavior} tabIndex={0}
        onClick={() => choose(choice.id)} onKeyDown={event => keys(event, index)}>
        <strong>{choice.id === behavior && <span className="target-check" aria-hidden="true">✓</span>}{choice.title}</strong>
        <span>Use for: {choice.useFor}</span>
        {choice.pill && <em>{choice.pill}</em>}
        {choice.note && <small>{choice.note}</small>}
      </li>)}
    </ul>}
  </div>;
}

function Toggle({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (checked: boolean) => void }) {
  return <label className="target-toggle">
    <button type="button" role="switch" aria-checked={checked} disabled={disabled} onClick={() => onChange(!checked)}><span /></button>
    {label}
  </label>;
}

export function CategoryTargetEditor({ target, month, busy, money, onSave, onCancel, onDelete }: {
  target: CategoryTarget | null; month: string; busy: boolean; money: Money;
  onSave: (fields: TargetFields) => void; onCancel: () => void; onDelete: () => void;
}) {
  const [kind, setKind] = useState<TargetKind>(target?.kind ?? 'monthly');
  const [amount, setAmount] = useState(target ? target.amount.toFixed(2) : '');
  const [weekday, setWeekday] = useState(target?.weekday ?? new Date().getDay());
  const [dayOfMonth, setDayOfMonth] = useState(target?.dayOfMonth ?? 0);
  const [behavior, setBehavior] = useState<TargetBehavior>(target?.behavior ?? 'setAside');
  const [dueDate, setDueDate] = useState<string | null>(target?.dueDate ?? nextMonthStart(month));
  const [repeat, setRepeat] = useState(target?.repeatEvery != null);
  const [repeatEvery, setRepeatEvery] = useState(target?.repeatEvery ?? 1);
  const [repeatUnit, setRepeatUnit] = useState<RepeatUnit>(target?.repeatUnit ?? 'month');
  const [error, setError] = useState('');
  const parsed = parseMoney(amount || '0');
  const amountText = money(parsed !== null && parsed > 0 ? parsed : 0);
  const edit = <T,>(setter: (value: T) => void) => (value: T) => { setter(value); setError(''); };

  function submit() {
    const value = parseMoney(amount);
    if (value === null || value <= 0) { setError('Enter an amount greater than zero with at most two decimals.'); return; }
    const base: TargetFields = { kind, amount: value, behavior, weekday: null, dayOfMonth: null, dueDate: null, repeatEvery: null, repeatUnit: null };
    if (kind === 'weekly') { onSave({ ...base, behavior: 'setAside', weekday }); return; }
    if (kind === 'monthly') { onSave({ ...base, dayOfMonth: dayOfMonth || null }); return; }
    if (!dueDate) { setError('Enter a valid date as dd-mm-yyyy.'); return; }
    if (kind === 'yearly') { onSave({ ...base, dueDate }); return; }
    if (!repeat && dueDate < today()) { setError('Choose today or a later date, or turn on Repeat.'); return; }
    onSave({ ...base, dueDate, repeatEvery: repeat ? repeatEvery : null, repeatUnit: repeat ? repeatUnit : null });
  }

  return <form className="credit-target-card credit-target-editor category-target-editor" aria-label="Category target"
    onSubmit={event => { event.preventDefault(); submit(); }}>
    <strong className="credit-target-title">Target</strong>
    <div className="credit-target-fields">
      <div className="target-tabs" role="tablist" aria-label="Target cadence">
        {targetKinds.map(option => <button key={option.id} type="button" role="tab" aria-selected={kind === option.id} disabled={busy}
          onClick={() => { setKind(option.id); setError(''); }}>{option.label}</button>)}
      </div>
      <MoneyField label={kind === 'custom' ? 'Amount' : 'I need'} value={amount} disabled={busy} money={money} onChange={edit(setAmount)} />
      {kind === 'weekly' && <NativeSelect label="Every" value={String(weekday)} disabled={busy} onChange={value => edit(setWeekday)(Number(value))}
        options={weekdays.map((name, index) => ({ value: String(index), label: name }))} />}
      {kind === 'monthly' && <>
        <NativeSelect label="By" value={String(dayOfMonth)} disabled={busy} onChange={value => edit(setDayOfMonth)(Number(value))}
          options={[{ value: '0', label: 'Last Day of Month' }, ...Array.from({ length: 31 }, (_, index) => ({ value: String(index + 1), label: ordinal(index + 1) }))]} />
        <BehaviorPicker label="Next month I want to" kind={kind} behavior={behavior} amount={amountText} disabled={busy} onChange={edit(setBehavior)} />
      </>}
      {kind === 'yearly' && <>
        <TargetDateField label="By" value={dueDate ?? ''} disabled={busy} onChange={edit(setDueDate)} />
        <BehaviorPicker label="Next year I want to" kind={kind} behavior={behavior} amount={amountText} disabled={busy} onChange={edit(setBehavior)} />
      </>}
      {kind === 'custom' && <>
        <BehaviorPicker label="I want to" kind={kind} behavior={behavior} amount={amountText} disabled={busy} onChange={edit(setBehavior)} />
        <TargetDateField label="Due on" value={dueDate ?? ''} disabled={busy} onChange={edit(setDueDate)} />
        <Toggle label="Repeat" checked={repeat} disabled={busy} onChange={edit(setRepeat)} />
        {repeat && <div className="target-repeat" role="group" aria-label="Repeat every">
          <span className="target-label">Every</span>
          <div>
            <select className="target-input target-select" aria-label="Repeat interval" value={repeatEvery} disabled={busy} onChange={event => edit(setRepeatEvery)(Number(event.target.value))}>
              {Array.from({ length: 99 }, (_, index) => <option key={index + 1} value={index + 1}>{index + 1}</option>)}
            </select>
            <select className="target-input target-select" aria-label="Repeat unit" value={repeatUnit} disabled={busy} onChange={event => edit(setRepeatUnit)(event.target.value as RepeatUnit)}>
              {repeatUnits.map(unit => <option key={unit} value={unit}>{unit[0].toUpperCase() + unit.slice(1)}{repeatEvery > 1 ? 's' : ''}</option>)}
            </select>
          </div>
        </div>}
      </>}
      {error && <p role="alert" className="credit-target-error">{error}</p>}
    </div>
    <div className="credit-target-actions">
      {target && <button type="button" className="credit-target-delete" disabled={busy} onClick={onDelete}>{trashIcon} Delete</button>}
      <button type="button" className="credit-target-cancel" disabled={busy} onClick={onCancel}>Cancel</button>
      <button type="submit" className="credit-target-save" disabled={busy}>{busy ? 'Saving…' : 'Save Target'}</button>
    </div>
  </form>;
}

const describe = (target: CategoryTarget, money: Money) => {
  const amount = money(target.amount);
  if (target.kind === 'weekly') return `Set aside ${amount} every ${weekdays[target.weekday ?? 0]}`;
  const verb = target.behavior === 'refill' ? (target.kind === 'custom' ? 'Fill up to' : 'Refill up to') : 'Set aside another';
  if (target.kind === 'monthly') return `${verb} ${amount} each month`;
  if (target.kind === 'yearly') return `${verb} ${amount} each year`;
  return `${verb} ${amount}`;
};

// Target card in the Plan category details. Saves and deletes keep their command for lost-response retries; a version
// conflict asks for a reload instead of overwriting another session's change.
export function CategoryTargetCard({ budgetId, month, category, money, onChanged, startEditing = false }: {
  budgetId: string; month: string; category: TargetCategory; money: Money; onChanged: (result: CategoryTargetResult) => void; startEditing?: boolean;
}) {
  const target = category.categoryTarget ?? null;
  const progress = category.targetProgress ?? null;
  const [open, setOpen] = useState(true);
  const [editing, setEditing] = useState(startEditing);
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [conflict, setConflict] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const pending = useRef<{ path: string; body: string } | null>(null);
  const path = targetPath(budgetId, category.id);

  async function send(command: { path: string; body: string }) {
    pending.current = command;
    setBusy(true); setSaveError(''); setConflict(false);
    try {
      const result = await requestJson<CategoryTargetResult>(command.path, { method: command.path.endsWith('/delete') ? 'POST' : 'PUT', headers: { 'Content-Type': 'application/json' }, body: command.body });
      pending.current = null; setUncertain(false); setEditing(false);
      onChanged(result);
    } catch (error) {
      if (error instanceof ApiError && error.status < 500) {
        pending.current = null; setUncertain(false);
        setConflict(error.status === 409);
        setSaveError(error.status === 409 ? 'This target changed in another session. Reload it before saving again.' : error.message);
      } else {
        setUncertain(true);
        setSaveError('The target could not be confirmed.');
      }
    } finally { setBusy(false); }
  }

  const save = (fields: TargetFields) => void send({ path, body: JSON.stringify({ commandId: crypto.randomUUID(), version: target?.version ?? 0, month, ...fields }) });
  const remove = () => target && void send({ path: `${path}/delete`, body: JSON.stringify({ commandId: crypto.randomUUID(), version: target.version, month }) });

  async function reload() {
    setBusy(true); setSaveError(''); setConflict(false);
    try {
      onChanged(await requestJson<CategoryTargetResult>(`${path}/${month}`));
      setEditing(false);
    } catch (error) { setSaveError(error instanceof Error ? error.message : 'Could not reload the target.'); setConflict(true); }
    finally { setBusy(false); }
  }

  function cancel() {
    if (busy || uncertain) return;
    setEditing(false); setSaveError(''); setConflict(false);
  }

  const feedback = (saveError || uncertain) && <div className="credit-target-feedback category-target-feedback" role="alert">
    {saveError}{' '}
    {conflict && <button type="button" disabled={busy} onClick={() => void reload()}>Reload target</button>}
    {uncertain && <>Retrying uses the same request, so it cannot be applied twice. <button type="button" disabled={busy} onClick={() => { if (pending.current) void send(pending.current); }}>Retry save</button></>}
  </div>;

  if (editing) return <div className="category-target">
    <CategoryTargetEditor target={target} month={month} busy={busy} money={money} onSave={save} onCancel={cancel} onDelete={remove} />
    {feedback}
  </div>;

  const underfunded = Boolean(progress && progress.moreNeeded > 0);
  return <section className="credit-target-card credit-target-summary category-target" aria-label={`${category.name} target`}>
    <button type="button" className="credit-target-toggle" aria-expanded={open} onClick={() => setOpen(value => !value)}>
      Target <span className={`credit-chevron ${open ? 'open' : ''}`} aria-hidden="true">⌄</span>
    </button>
    {open && (!target ? <div className="credit-target-body category-target-empty">
      <strong>How much do you need for {category.icon && <span className="category-target-icon" aria-hidden="true">{category.icon}</span>}{category.name}?</strong>
      <p>When you create a target, we’ll let you know how much money to set aside to stay on track over time.</p>
      <button type="button" className="credit-target-edit category-target-create" onClick={() => { setEditing(true); setSaveError(''); }}>Create Target</button>
      {feedback}
    </div> : <div className="credit-target-body">
      <div className="credit-target-goal">
        <b>{describe(target, money)}</b>
        <span>{progress?.cadence ?? 'Restore this category to track its target.'}</span>
      </div>
      {progress && <>
        <ProgressRing percent={Math.floor(progress.fundedFraction * 100)} tone={underfunded ? 'behind' : 'good'} />
        <p className={`credit-target-banner ${underfunded ? 'behind' : 'good'}`} role="status">
          {underfunded ? <><mark>{money(progress.moreNeeded)}</mark> more needed this month{progress.isPastDue ? ' — this target is past due' : ''}</>
            : 'Funded — you have assigned enough for this month.'}
        </p>
        <dl>
          <div><dt>Needed This Month</dt><dd>{money(progress.needed)}</dd></div>
          <div><dt>Assigned This Month</dt><dd>{money(progress.funded)}</dd></div>
          {progress.nextDueDate && target.kind !== 'weekly' && <div><dt>Due</dt><dd>{displayDate(progress.nextDueDate)}</dd></div>}
          <div className="credit-target-total"><dt>To Go</dt><dd>{money(progress.moreNeeded)}</dd></div>
        </dl>
      </>}
      <button type="button" className="credit-target-edit" onClick={() => { setEditing(true); setSaveError(''); }}>Edit Target</button>
      {feedback}
    </div>)}
  </section>;
}

// Small ring drawn before an underfunded Available amount; the filled arc is the funded share.
export function TargetRingIcon({ fraction }: { fraction: number }) {
  const clamped = Math.min(Math.max(fraction, 0), 1);
  const radius = 4.5;
  const circumference = 2 * Math.PI * radius;
  return <svg className="target-ring-icon" width="13" height="13" viewBox="0 0 13 13" aria-hidden="true">
    <circle cx="6.5" cy="6.5" r={radius} fill="none" stroke="currentColor" strokeWidth="1.6" />
    {clamped > 0 && <circle cx="6.5" cy="6.5" r={radius / 2} fill="none" stroke="currentColor" strokeWidth={radius}
      strokeDasharray={`${(Math.PI * radius) * clamped} ${circumference}`} transform="rotate(-90 6.5 6.5)" />}
  </svg>;
}
