import { useEffect, useId, useMemo, useState } from 'react';
import { creditPaymentGroupId, requestJson, today } from './api';
import { wishlistGroupId } from '../wishlist/wishlistApi';
import { type LoanPairing, type LoanPairingItem, type NewPairedCategory, unpaired } from './loans';
import './loans.css';

// "Optional: Pair a Category" step shared by Add Unlinked Account (after Next) and Edit Account -> Pair Category.
// The parent owns the draft so going back keeps every choice. The server repeats every check (active category,
// protected groups, one-to-one pairing, name rules) inside the same versioned command that creates the account.
export const newGroupOption = '__new-group';
const maxCategoryName = 160, maxGroupName = 120;

type MonthCategory = { id: string; name: string; sortOrder: number; isArchived: boolean };
type MonthGroup = { id: string; name: string; sortOrder: number; isArchived: boolean; categories: MonthCategory[] };
export type SlotDraft = { mode: 'existing' | 'new'; categoryId: string; name: string; nameEdited: boolean; groupId: string; newGroupName: string };
export type PairDraft = { device: 'same' | 'separate'; main: SlotDraft; deviceSlot: SlotDraft };
export type PairResult = { pairing: LoanPairing; labels: { main: string | null; device: string | null } };

const slot = (name: string, categoryId?: string | null): SlotDraft =>
  ({ mode: 'existing', categoryId: categoryId ?? '', name, nameEdited: false, groupId: '', newGroupName: '' });
export const deviceName = (name: string) => `${name.trim()} Device`;
export const pairDraft = (name: string, current?: { main?: string | null; device?: string | null; separate?: boolean }): PairDraft =>
  ({ device: current?.separate ? 'separate' : 'same', main: slot(name.trim(), current?.main), deviceSlot: slot(deviceName(name), current?.device) });
// Keeps the suggested names in step with the nickname until the user types their own.
export const renameDraft = (draft: PairDraft, name: string): PairDraft => ({ ...draft,
  main: draft.main.nameEdited ? draft.main : { ...draft.main, name: name.trim() },
  deviceSlot: draft.deviceSlot.nameEdited ? draft.deviceSlot : { ...draft.deviceSlot, name: deviceName(name) } });

const sameName = (a: string, b: string) => a.trim().toLocaleLowerCase() === b.trim().toLocaleLowerCase();
const isProtected = (groupId: string) => groupId === creditPaymentGroupId || groupId === wishlistGroupId;

type Resolved = { id: string | null; create: NewPairedCategory | null; label: string } | { problem: string };

function resolve(draft: SlotDraft, groups: MonthGroup[], taken: Map<string, string>): Resolved {
  if (draft.mode === 'existing') {
    const group = groups.find(g => g.categories.some(c => c.id === draft.categoryId));
    if (!draft.categoryId || !group) return { problem: '' };
    if (taken.has(draft.categoryId)) return { problem: `That category is already paired with ${taken.get(draft.categoryId)}.` };
    return { id: draft.categoryId, create: null, label: group.categories.find(c => c.id === draft.categoryId)!.name };
  }
  const name = draft.name.trim();
  if (!name || !draft.groupId) return { problem: '' };
  if (name.length > maxCategoryName) return { problem: `Category names can be up to ${maxCategoryName} characters.` };
  if (draft.groupId === newGroupOption) {
    const groupName = draft.newGroupName.trim();
    if (!groupName) return { problem: '' };
    if (groupName.length > maxGroupName) return { problem: `Category group names can be up to ${maxGroupName} characters.` };
    if (groups.some(g => sameName(g.name, groupName))) return { problem: 'A category group with that name already exists. Choose it from the list instead.' };
    return { id: null, create: { name, groupId: null, newGroupName: groupName }, label: name };
  }
  const group = groups.find(g => g.id === draft.groupId);
  if (!group) return { problem: '' };
  if (group.categories.some(c => sameName(c.name, name))) return { problem: 'This category group already has a category with that name.' };
  return { id: null, create: { name, groupId: group.id, newGroupName: null }, label: name };
}

export function PairCategoryStep({ draft, onChange, accountId, subject, includesDevice, busy, secondaryLabel, onSecondary, onNext }: {
  draft: PairDraft; onChange: (draft: PairDraft) => void; accountId?: string; subject: 'loan' | 'contract'; includesDevice: boolean; busy: boolean;
  secondaryLabel: string; onSecondary: () => void; onNext: (result: PairResult) => void;
}) {
  const [groups, setGroups] = useState<MonthGroup[] | null>(null);
  const [pairings, setPairings] = useState<LoanPairingItem[]>([]);
  const [loadError, setLoadError] = useState('');
  const [reload, setReload] = useState(0);
  const deviceId = useId();
  useEffect(() => {
    const controller = new AbortController();
    setLoadError('');
    Promise.all([
      requestJson<{ groups: MonthGroup[] }>(`/api/budget/${today().slice(0, 7)}`, { signal: controller.signal }),
      requestJson<{ items: LoanPairingItem[] }>('/api/loan-pairings', { signal: controller.signal })
    ]).then(([month, pairs]) => { if (!controller.signal.aborted) { setGroups(month.groups); setPairings(pairs.items); } })
      .catch(error => { if (!controller.signal.aborted) setLoadError(`Could not load your plan categories. ${error.message}`); });
    return () => controller.abort();
  }, [reload]);

  // Active, user-managed groups and categories in plan order; Credit Card Payments, Wishlist and archived rows are hidden.
  const usable = useMemo(() => (groups ?? []).filter(g => !g.isArchived && !isProtected(g.id)).sort((a, b) => a.sortOrder - b.sortOrder)
    .map(g => ({ ...g, categories: g.categories.filter(c => !c.isArchived && !c.id.startsWith('ccp-') && !c.id.startsWith('wl-')).sort((a, b) => a.sortOrder - b.sortOrder) })), [groups]);
  // Categories paired with a different loan are shown but disabled: a category pairs with one loan only.
  const taken = useMemo(() => new Map(pairings.filter(p => p.accountId !== accountId).map(p => [p.categoryId, p.accountName])), [pairings, accountId]);

  const separate = includesDevice && draft.device === 'separate';
  const main = resolve(draft.main, usable, taken);
  const device = separate ? resolve(draft.deviceSlot, usable, taken) : null;
  let conflict = '';
  if (separate && 'label' in main && device && 'label' in device) {
    if (main.id && main.id === device.id) conflict = 'Choose different categories for the contract and the device.';
    else if (main.create && device.create && sameName(main.create.name, device.create.name)
      && (main.create.groupId ? main.create.groupId === device.create.groupId : Boolean(device.create.newGroupName && sameName(main.create.newGroupName!, device.create.newGroupName))))
      conflict = 'The contract and device categories need different names.';
  }
  const valid = groups !== null && 'label' in main && (!device || 'label' in device) && !conflict;

  function next() {
    if (!valid || !('label' in main)) return;
    const deviceSlot = device && 'label' in device ? device : null;
    onNext({
      pairing: { ...unpaired, pairedCategoryId: main.id, newPairedCategory: main.create, separateDeviceCategory: separate,
        devicePairedCategoryId: deviceSlot?.id ?? null, newDevicePairedCategory: deviceSlot?.create ?? null },
      labels: { main: main.label, device: deviceSlot?.label ?? null }
    });
  }

  const setMain = (value: SlotDraft) => onChange({ ...draft, main: value });
  const setDevice = (value: SlotDraft) => onChange({ ...draft, deviceSlot: value });
  return <div className="loan-pair-step">
    <div className="loan-pair-scroll">
      <h3>Pair your {subject} with a plan category</h3>
      <p className="loan-pair-help">Pairing with a plan category helps better show how your payments will affect your debt. Pairing works best when there is a one-to-one relationship between your {subject} account and a plan category.</p>
      {includesDevice && <fieldset className="loan-pair-choice" disabled={busy}>
        <legend>How do you want to budget for the device?</legend>
        <Radio name={deviceId} checked={draft.device === 'same'} onChange={() => onChange({ ...draft, device: 'same' })} title="Use the same category for the device and contract" detail="One category covers the whole payment" />
        <Radio name={deviceId} checked={draft.device === 'separate'} onChange={() => onChange({ ...draft, device: 'separate' })} title="Use separate categories for the device and contract" detail="Each payment is split by that month's scheduled contract and device amounts" />
      </fieldset>}
      {loadError ? <p role="alert" className="loan-error">{loadError} <button type="button" className="loan-link" onClick={() => setReload(n => n + 1)}>Retry</button></p>
        : !groups ? <p role="status">Loading categories…</p>
        : separate ? <>
          <section className="loan-pair-section" aria-label="Contract category"><h4>Contract category</h4>
            <SlotFields slot={draft.main} onChange={setMain} groups={usable} taken={taken} question="Which category do you want to pair your contract with?" problem={'problem' in main ? main.problem : ''} busy={busy} prefix="Contract" />
          </section>
          <section className="loan-pair-section" aria-label="Device category"><h4>Device category</h4>
            <SlotFields slot={draft.deviceSlot} onChange={setDevice} groups={usable} taken={taken} question="Which category do you want to pair your device with?" problem={device && 'problem' in device ? device.problem : ''} busy={busy} prefix="Device" />
          </section>
        </> : <SlotFields slot={draft.main} onChange={setMain} groups={usable} taken={taken} question={`Which category do you want to pair your ${subject} with?`} problem={'problem' in main ? main.problem : ''} busy={busy} />}
      {conflict && <p role="alert" className="loan-error">{conflict}</p>}
    </div>
    <div className="loan-pair-actions">
      <button type="button" className="loan-pair-skip" disabled={busy} onClick={onSecondary}>{secondaryLabel}</button>
      <button type="button" className="loan-pair-next" disabled={busy || !valid} onClick={next}>{busy ? 'Saving…' : 'Next'}</button>
    </div>
  </div>;
}

function Radio({ name, checked, onChange, title, detail }: { name: string; checked: boolean; onChange: () => void; title: string; detail: string }) {
  return <label className="loan-pair-radio"><input type="radio" name={name} checked={checked} onChange={onChange} /><span><b>{title}</b><small>{detail}</small></span></label>;
}

function SlotFields({ slot, onChange, groups, taken, question, problem, busy, prefix }: {
  slot: SlotDraft; onChange: (slot: SlotDraft) => void; groups: MonthGroup[]; taken: Map<string, string>; question: string; problem: string; busy: boolean; prefix?: string;
}) {
  const name = useId();
  const label = (text: string) => prefix ? `${prefix}: ${text}` : text;
  const choices = groups.filter(g => g.categories.length > 0);
  return <>
    <fieldset className="loan-pair-choice" disabled={busy}>
      <legend>{question}</legend>
      <Radio name={name} checked={slot.mode === 'existing'} onChange={() => onChange({ ...slot, mode: 'existing' })} title="Select an existing category" detail="Pair with a category already in your plan" />
      <Radio name={name} checked={slot.mode === 'new'} onChange={() => onChange({ ...slot, mode: 'new' })} title="Create a new category" detail="Pair with a brand new category" />
    </fieldset>
    {slot.mode === 'existing' ? <label className="loan-pair-field">Select a category
      <select aria-label={label('Select a category')} value={slot.categoryId} disabled={busy} onChange={e => onChange({ ...slot, categoryId: e.target.value })}>
        <option value="" disabled hidden></option>
        {choices.map(group => <optgroup key={group.id} label={group.name}>
          {group.categories.map(category => <option key={category.id} value={category.id} disabled={taken.has(category.id)}
            title={taken.has(category.id) ? `Already paired with ${taken.get(category.id)}` : undefined}>{category.name}</option>)}
        </optgroup>)}
      </select>
      {!choices.length && <small>No categories are available. Create a new category instead.</small>}
    </label> : <>
      <label className="loan-pair-field">Give your category a name
        <input aria-label={label('Give your category a name')} value={slot.name} maxLength={maxCategoryName} disabled={busy} onChange={e => onChange({ ...slot, name: e.target.value, nameEdited: true })} />
      </label>
      <label className="loan-pair-field">Add to category group
        <select aria-label={label('Add to category group')} value={slot.groupId} disabled={busy} onChange={e => onChange({ ...slot, groupId: e.target.value })}>
          <option value="" disabled hidden></option>
          <option value={newGroupOption}>New Category Group</option>
          {groups.map(group => <option key={group.id} value={group.id}>{group.name}</option>)}
        </select>
      </label>
      {slot.groupId === newGroupOption && <label className="loan-pair-field">Category group name
        <input aria-label={label('Category group name')} value={slot.newGroupName} maxLength={maxGroupName} disabled={busy} onChange={e => onChange({ ...slot, newGroupName: e.target.value })} />
      </label>}
    </>}
    {problem && <p role="alert" className="loan-error">{problem}</p>}
  </>;
}
