import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { type Account, type CategoryOption, ApiError, requestJson } from './api';
import { focusNextField } from './focusNext';
import { LedgerDialog } from './LedgerDialog';
import { CategoryPicker } from './CategoryPicker';

export type PayeeRule = { operator: 'is' | 'contains'; value: string };
export type SavedPayee = {
  id: string; name: string; isHidden: boolean; categoryMode: 'history' | 'always' | 'never';
  defaultCategoryId: string | null; effectiveCategoryId: string | null; transactionCount: number;
  rules: PayeeRule[]; delete?: boolean;
};
export type PayeeList = { version: number; payees: SavedPayee[] };
type Shared = {
  accounts: Account[]; categories: CategoryOption[]; onChanged: () => void;
  onPendingChange?: (pending: boolean) => void; onShowTransactions?: (payee: SavedPayee) => void;
};

export function PayeePicker({ value, onChange, accountId, disabled, form, categories, accounts, onChanged, onPendingChange, onShowTransactions, paymentsRequest=0, allowTransfers=true, label='Payee', transferLabel=(a: Account) => 'Transfer to ' + a.name }: Shared & {
  value: string; onChange: (name: string, transferId?: string, categoryId?: string | null) => void;
  accountId: string; disabled?: boolean; form: string;paymentsRequest?:number;allowTransfers?:boolean;label?:string;transferLabel?:(account: Account) => string;
}) {
  const [open, setOpen] = useState(false); const [manage, setManage] = useState(false);
  const [paymentsOnly,setPaymentsOnly]=useState(false);
  useEffect(()=>{if(paymentsRequest){setPaymentsOnly(true);setOpen(true);refocus();}},[paymentsRequest]);
  const [data, setData] = useState<PayeeList | null>(null); const [error, setError] = useState(''); const [refresh, setRefresh] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  // Programmatic refocus (after choosing, Escape or the toggle) must not reopen the list.
  const skipFocusOpen=useRef(false);const refocus=()=>{skipFocusOpen.current=true;input.current?.focus();skipFocusOpen.current=false;}; const popup = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ left: 0, top: 0 }); const [active, setActive] = useState(-1);
  useEffect(() => {
    const controller = new AbortController(); setError('');
    requestJson<PayeeList>('/api/payees/manage', { signal: controller.signal }).then(setData)
      .catch(err => { if (!controller.signal.aborted) setError(err.message); });
    return () => controller.abort();
  }, [refresh]);
  useEffect(() => {
    if (!open) return;
    function close(event: MouseEvent) {
      if (!popup.current?.contains(event.target as Node) && !input.current?.parentElement?.contains(event.target as Node)) setOpen(false);
    }
    function positionPopup() {
      const rect = input.current?.getBoundingClientRect();
      if (rect) setPosition({ left: Math.max(8, Math.min(rect.left, window.innerWidth - 330)), top: Math.max(8, Math.min(rect.bottom + 12, window.innerHeight - 325)) });
    }
    positionPopup(); document.addEventListener('mousedown', close); window.addEventListener('resize', positionPopup); window.addEventListener('scroll', positionPopup, true);
    return () => { document.removeEventListener('mousedown', close); window.removeEventListener('resize', positionPopup); window.removeEventListener('scroll', positionPopup, true); };
  }, [open]);
  const needle = value.trim().toLocaleLowerCase(); const selected = data?.payees.find(p => !p.isHidden && p.name.toLocaleLowerCase() === needle);
  const transfers = allowTransfers ? accounts.filter(a => paymentsOnly || !needle || transferLabel(a).toLocaleLowerCase().includes(needle)) : [];
  const saved = (data?.payees ?? []).filter(p => !paymentsOnly&&!p.isHidden && (!needle || p.name.toLocaleLowerCase().includes(needle)));
  const options = [
    ...(selected ? [{ key: 'selected', label: selected.name, choose: () => onChange(selected.name, undefined, selected.effectiveCategoryId), disabled: false }] : []),
    ...transfers.map(a => ({ key: a.id, label: transferLabel(a), choose: () => onChange(transferLabel(a), a.id), disabled: a.isClosed || a.id === accountId })),
    ...saved.filter(p => p.id !== selected?.id).map(p => ({ key: p.id, label: p.name, choose: () => onChange(p.name, undefined, p.effectiveCategoryId), disabled: false }))
  ];
  const selectedCount = selected ? 1 : 0;
  function choose(index: number) { if (options[index] && !options[index].disabled) { options[index].choose(); setOpen(false); refocus(); focusNextField(input.current); } }
  function keyboard(direction: number) {
    setOpen(true); setActive(previous => {
      let next = previous < 0 && direction < 0 ? 0 : previous;
      for (let attempt = 0; attempt < options.length; attempt++) {
        next = (next + direction + options.length) % options.length;
        if (!options[next].disabled) return next;
      }
      return -1;
    });
  }
  const option = (index: number) => <button type="button" role="option" id={form + '-payee-' + index} aria-selected={active === index} key={options[index].key} disabled={options[index].disabled} onClick={() => choose(index)}>{options[index].label}{options[index].disabled ? transfers.find(a => a.id === options[index].key)?.isClosed ? ' (closed)' : ' (this account)' : ''}</button>;
  return <>
    <span className="payee-input-wrap"><input ref={input} form={form} role="combobox" onFocus={()=>{if(skipFocusOpen.current||disabled)return;setActive(-1);setOpen(true);}} aria-label={label} aria-expanded={open} aria-controls={form + '-payee-list'} aria-activedescendant={open && active >= 0 ? form + '-payee-' + active : undefined} aria-autocomplete="list" autoComplete="off" value={value} disabled={disabled} maxLength={160} placeholder="payee"
      onClick={() => { setPaymentsOnly(false);setActive(-1); setOpen(true); }} onChange={e => {setPaymentsOnly(false); onChange(e.target.value); setActive(-1); setOpen(true); }}
      onKeyDown={e => {
        if (e.key === 'Escape' && open) { e.stopPropagation(); setOpen(false); }
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); keyboard(e.key === 'ArrowDown' ? 1 : -1); }
        if (e.key === 'Enter' && open && active >= 0) { e.preventDefault(); choose(active); }
        if (e.key === 'Tab') setOpen(false);
      }} /><button type="button" className="payee-open" aria-label="Choose payee" tabIndex={-1} disabled={disabled} onClick={() => { setOpen(v => !v); refocus(); }}>▾</button></span>
    {open && createPortal(<div ref={popup} className="payee-picker" style={position} onKeyDown={e => { if (e.key === 'Escape') { e.stopPropagation(); setOpen(false); refocus(); } }}>
      <div id={form + '-payee-list'} role="listbox" aria-label="Payees" className="payee-options">
        {!data && !error && <p role="status">Loading payees…</p>}{error && <p role="alert">Could not load saved payees. <button type="button" onClick={() => setRefresh(n => n + 1)}>Retry</button></p>}
        {selected && <><h4>Selected</h4>{option(0)}</>}
        {transfers.length > 0 && <h4>Payments and Transfers</h4>}{transfers.map((_, i) => option(i + selectedCount))}
        {saved.some(p => p.id !== selected?.id) && <h4>Saved Payees</h4>}{saved.filter(p => p.id !== selected?.id).map((_, i) => option(i + transfers.length + selectedCount))}
        {data && options.length === 0 && <p>No saved payees found. Type a name to create one when saving.</p>}
      </div><button type="button" className="manage-payees-link" onClick={() => { setOpen(false); setManage(true); }}>Manage Payees</button>
    </div>, document.body)}
    {manage && createPortal(<ManagePayees initialName={value} onShowTransactions={onShowTransactions} accounts={accounts} categories={categories} onPendingChange={onPendingChange} onClose={() => setManage(false)} onChanged={() => { setRefresh(n => n + 1); onChanged(); }} onSaved={next => {
      const old = data?.payees.find(p => p.name === value); const updated = next.find(p => p.id === old?.id);
      if (updated && !updated.delete) onChange(updated.name, undefined, updated.effectiveCategoryId);
    }} />, document.body)}
  </>;
}

export function ManagePayees({ accounts, categories, onClose, onChanged, onSaved, onShowTransactions, onPendingChange, initialName }: Shared & {
  initialName?: string; onClose: () => void; onSaved?: (payees: SavedPayee[]) => void;
}) {
  const [loaded, setLoaded] = useState<PayeeList | null>(null); const [draft, setDraft] = useState<SavedPayee[]>([]);
  const [past, setPast] = useState<SavedPayee[][]>([]); const [future, setFuture] = useState<SavedPayee[][]>([]);
  const [search, setSearch] = useState(''); const [selected, setSelected] = useState<string[]>([]);
  const [transfersOpen, setTransfersOpen] = useState(true); const [payeesOpen, setPayeesOpen] = useState(true); const [transfer, setTransfer] = useState<string | null>(null);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [conflict, setConflict] = useState(false);
  const [refresh, setRefresh] = useState(0); const [uncertain, setUncertain] = useState(false);
  const pending = useRef<string | null>(null); const saving = useRef(false); const initial = useRef(initialName);
  useEffect(() => {
    const controller = new AbortController(); setLoaded(null);
    requestJson<PayeeList>('/api/payees/manage', { signal: controller.signal }).then(value => {
      if (controller.signal.aborted) return;
      setLoaded(value); setDraft(value.payees); setPast([]); setFuture([]); setTransfer(null);
      setSelected(value.payees.length ? [(value.payees.find(p => p.name === initial.current) ?? value.payees[0]).id] : []); setError(''); setConflict(false);
    }).catch(err => { if (!controller.signal.aborted) setError(err.message); });
    return () => controller.abort();
  }, [refresh]);
  useEffect(() => { onPendingChange?.(busy || uncertain); return () => onPendingChange?.(false); }, [busy, uncertain, onPendingChange]);
  const locked = busy || uncertain || conflict || !loaded;
  const current = draft.find(p => p.id === selected[0] && !p.delete); const target = accounts.find(a => a.id === transfer);
  const filtered = draft.filter(p => !p.delete && p.name.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  const changed = loaded && JSON.stringify(draft) !== JSON.stringify(loaded.payees);
  const unused = draft.filter(p => !p.delete && p.transactionCount === 0);
  function update(next: SavedPayee[]) { setPast(items => [...items, draft]); setFuture([]); setDraft(next); }
  function edit(fields: Partial<SavedPayee>) { update(draft.map(p => selected.includes(p.id) ? { ...p, ...fields } : p)); }
  function rule(index: number, fields: Partial<PayeeRule>) { if (current) edit({ rules: current.rules.map((r, i) => i === index ? { ...r, ...fields } : r) }); }
  function undo() { const previous = past.at(-1); if (previous) { setFuture(items => [draft, ...items]); setDraft(previous); setPast(items => items.slice(0, -1)); } }
  function redo() { const next = future[0]; if (next) { setPast(items => [...items, draft]); setDraft(next); setFuture(items => items.slice(1)); } }
  async function done() {
    if (saving.current || conflict || !loaded) return;
    if (!changed && !pending.current) { onClose(); return; }
    const modified = draft.filter(p => JSON.stringify(p) !== JSON.stringify(loaded.payees.find(old => old.id === p.id)));
    if (!pending.current) {
      if (modified.some(p => !p.name.trim() || p.name.trim().length > 160 || p.categoryMode === 'always' && !p.defaultCategoryId || p.rules.some(r => !r.value.trim()))) {
        setError('Enter a payee name, choose required default categories, and fill in or remove empty renaming rules.'); return;
      }
      pending.current = JSON.stringify({ commandId: crypto.randomUUID(), version: loaded.version, payees: modified });
    }
    saving.current = true; setBusy(true); setError('');
    try {
      await requestJson('/api/payees/manage', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: pending.current });
      pending.current = null; setUncertain(false);
      onSaved?.(draft.map(p => ({ ...p, effectiveCategoryId: p.categoryMode === 'always' ? p.defaultCategoryId : p.categoryMode === 'never' ? null : p.effectiveCategoryId })));
      onChanged(); onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save payees.');
      if (err instanceof ApiError && err.status < 500) { pending.current = null; setUncertain(false); setConflict(err.status === 409); } else setUncertain(true);
    } finally { saving.current = false; setBusy(false); }
  }
  function close() { if (!busy && !uncertain) onClose(); }
  return <LedgerDialog title="Manage Payees" onClose={close}>
    <div className="manage-payees-layout">
      <aside className="payees-sidebar"><input aria-label="Search Payees" placeholder="⌕ Search Payees" value={search} onChange={e => setSearch(e.target.value)} />
        <div className="payees-sidebar-scroll">
          <button type="button" className="payee-group" aria-expanded={payeesOpen} onClick={() => setPayeesOpen(!payeesOpen)}>{payeesOpen ? '⌄' : '›'} ▣ Payees ({selected.length})</button>
          {payeesOpen && filtered.map(p => <label className={'payee-list-item' + (selected.includes(p.id) ? ' selected' : '')} key={p.id}>
            <input type="checkbox" checked={selected.includes(p.id) && !transfer} disabled={locked} onChange={e => { setTransfer(null); setSelected(e.target.checked ? [...selected.filter(id => id !== p.id), p.id] : selected.filter(id => id !== p.id)); }} />
            <button type="button" disabled={locked} onClick={() => { setSelected([p.id]); setTransfer(null); }}>{p.name}{p.isHidden ? ' (hidden)' : ''}</button>
          </label>)}
          <button type="button" className="payee-group" aria-expanded={transfersOpen} onClick={() => setTransfersOpen(!transfersOpen)}>{transfersOpen ? '⌄' : '›'} Transfer Payees</button>
          {transfersOpen && accounts.filter(a => a.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())).map(a => <button type="button" key={a.id} className={'payee-list-item transfer-payee' + (transfer === a.id ? ' selected' : '')} onClick={() => { setTransfer(a.id); setSelected([]); }}>Transfer: {a.name}{a.isClosed ? ' (closed)' : ''}</button>)}
          <button type="button" className="payee-text-link" disabled={locked} onClick={() => { setTransfer(null); setSelected(unused.map(p => p.id)); }}>Select {unused.length} Unused Payees</button>
        </div>
      </aside>
      <div className="payee-detail" aria-busy={busy}>
        {!loaded && !error && <p role="status">Loading payees…</p>}
        {error && <div role="alert" className="payee-error">{error}{!loaded || conflict ? <button type="button" disabled={busy} onClick={() => setRefresh(n => n + 1)}>{conflict ? 'Reload saved payees' : 'Retry loading'}</button> : null}</div>}
        {uncertain && <p role="alert">The save outcome is unknown. Retry the same save to resolve it. <button type="button" disabled={busy} onClick={() => void done()}>Retry save</button></p>}
        {target ? <section className="payee-card"><h3>Payee Name</h3><div><strong>Transfer: {target.name}</strong><p>Transfer payees follow your account names. Rename the account using Edit Account.</p><p>Transfers move money between accounts and do not need a spending category.</p></div></section> : current ? <>
          {selected.length === 1 && <section className="payee-card"><h3>Payee Name</h3><div><input aria-label="Payee Name" value={current.name} maxLength={160} disabled={locked} onChange={e => edit({ name: e.target.value })} />{onShowTransactions && <button type="button" className="payee-text-link" disabled={!!changed || locked} onClick={() => onShowTransactions(current)}>Show {current.transactionCount} Transactions</button>}</div></section>}
          {selected.length > 1 && <p>{selected.length} payees selected. Changes below apply to all selected payees.</p>}
          <section className="payee-card"><h3>Categorization</h3><div className="payee-categorization">
            <label><input type="radio" name="payee-category-mode" checked={current.categoryMode === 'history'} disabled={locked} onChange={() => edit({ categoryMode: 'history', defaultCategoryId: null })} />Auto-categorize based on payee history</label>
            <p>OpenBudget will auto-select the category for new transactions based on how the last 3 transactions for this payee were categorized.</p>
            <p>Current default category:<br /><strong>{categories.find(c => c.id === current.effectiveCategoryId)?.name ?? 'Inflow: Ready to Assign / Needs category'}</strong></p>
            <label><input type="radio" name="payee-category-mode" checked={current.categoryMode === 'always'} disabled={locked} onChange={() => edit({ categoryMode: 'always' })} />Always use my choice</label>
            {current.categoryMode === 'always' && <CategoryPicker label="Default category" includeInflow={false} categories={categories} disabled={locked} value={current.defaultCategoryId ?? ''} onChange={id => edit({ defaultCategoryId: id || null })} />}
            <label><input type="radio" name="payee-category-mode" checked={current.categoryMode === 'never'} disabled={locked} onChange={() => edit({ categoryMode: 'never', defaultCategoryId: null })} />Do not auto-categorize {selected.length === 1 ? current.name : 'these payees'}</label>
          </div></section>
          {selected.length === 1 && <section className="payee-card"><h3>Renaming ⚙</h3><div><p>Imported payees that match these rules will be renamed.</p>
            {current.rules.map((r, i) => <div className="payee-renaming-rule" key={i}><select aria-label={`Rule ${i + 1} operator`} value={r.operator} disabled={locked} onChange={e => rule(i, { operator: e.target.value as PayeeRule['operator'] })}><option value="is">Is</option><option value="contains">Contains</option></select><input aria-label={`Rule ${i + 1} text`} maxLength={160} value={r.value} disabled={locked} onChange={e => rule(i, { value: e.target.value })} /><button type="button" aria-label={`Remove rule ${i + 1}`} disabled={locked} onClick={() => edit({ rules: current.rules.filter((_, index) => index !== i) })}>−</button></div>)}
            <button type="button" className="add-payee-rule" aria-label="Add renaming rule" disabled={locked || current.rules.length >= 50} onClick={() => edit({ rules: [...current.rules, { operator: 'contains', value: '' }] })}>＋</button>
          </div></section>}
          <div className="payee-actions"><button type="button" disabled={locked} onClick={() => edit({ isHidden: !current.isHidden })}>{current.isHidden ? 'Unhide' : 'Hide'}</button><button type="button" className="payee-delete" disabled={locked} onClick={() => {
            if (selected.some(id => (draft.find(p => p.id === id)?.transactionCount ?? 0) > 0)) { setError('A selected payee has transaction history. Hide it to keep that history.'); return; }
            edit({ delete: true }); setSelected([]);
          }}>Delete</button></div>
          <p className="payee-help">Hidden payees will not be suggested as you type or included in the list when adding a transaction. Their renaming rules still apply to imports. Changes take effect when you click Done.</p>
        </> : loaded && <p>{draft.some(p => !p.delete) ? 'Select a payee to edit.' : 'No saved payees yet. Add a named transaction to create one.'}</p>}
      </div>
    </div>
    <footer className="manage-payees-footer"><button type="button" disabled={locked || !past.length} onClick={undo}>↶ Undo</button><button type="button" disabled={locked || !future.length} onClick={redo}>↪ Redo</button><button type="button" className="payee-cancel" disabled={busy || uncertain} onClick={close}>Cancel</button><button type="button" className="button-primary" disabled={busy || conflict || !loaded || uncertain} onClick={() => void done()}>{busy ? 'Saving…' : 'Done'}</button></footer>
  </LedgerDialog>;
}
