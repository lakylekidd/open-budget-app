import { Fragment as ReactFragment, useCallback, useEffect, useRef, useState, type PointerEvent } from 'react';
import { MoneyInput } from '../MoneyInput';
import { DisclosureChevron } from '../DisclosureChevron';
import { type Account, type Accounts, type Audit, type CategoryOption, type Entry, type Register, type Schedules, type ScheduledItem, ApiError, formatMoney, loadCategories, parseMoney, requestJson, today, type AccountType, accountTypeLabel, accountTypeSections, isCreditAccount, isLoanAccount, type LoanAccountType } from './api';
import { LedgerDialog } from './LedgerDialog';
import { TransactionForm } from './TransactionForm';
import { AccountHeader, ClearedSymbol, RegisterIcon, TransactionRow } from './AccountRegister';
import { defaultColumnWidths, defaultView, viewDates, ViewOptionsPanel, type ColumnWidths, type ViewOptions } from './ViewOptions';
import { InlineTransactionEditor, type InlineFields } from './InlineTransactionEditor';
import { defaultFlags, type Flag } from './Flags';
import { EditAccountForm } from './EditAccountForm';
import { EditLoanAccountForm } from './EditLoanAccountForm';
import { LoanAccountForm } from './LoanAccountForm';
import { LoanAccountPage } from './LoanAccountPage';
import { ManagePayees } from './Payees';
import { TransactionSelectionBar } from './TransactionSelectionBar';
import { wishlistPurchasePath, type WishlistContext } from './WishlistPurchase';
import './ledger.css';

type Dialog = { kind: 'account'; account?: Account; accountType?: AccountType } | { kind: 'transaction'; entry?: Entry; wishlistItemId?: string } | { kind: 'transfer'; entry?: Entry; from?: string; to?: string }
  | { kind: 'reconcile'; account: Account } | { kind: 'loanActivity'; account: Account; mode?: 'payment' | 'other' } | { kind: 'delete'; entry: Entry } | { kind: 'history' };

export function AccountsPage({ selectedAccount, onSelectAccount, onChanged, onMenu, onPendingChange, createAccountRequest, editAccountRequest, onEditAccountRequestHandled, focusTransaction, onOpenCategory, liveRefresh, wishlist, wishlistPurchaseRequest }: {
  selectedAccount: string; onSelectAccount: (id: string) => void; onChanged: () => void; onMenu: () => void; onPendingChange?: (pending: boolean) => void; createAccountRequest?: number; editAccountRequest?: {id:string;token:number}; onEditAccountRequestHandled?: () => void;
  onOpenCategory?: (categoryId: string, editTarget: boolean) => void;
  /** Bumped when another session changes this budget's ledger; refreshes unless the user is mid-edit (the 30s poll retries). */
  liveRefresh?: number;
  focusTransaction?: { id: string; accountId: string; date: string; scheduled: boolean; projected?: Entry; splitIndex?: number; source?: 'Plan' | 'Long-Term Planning' | 'Wishlist' } | null;
  /** Entitled + enabled Wishlist for this budget (null otherwise): enables Wishlist purchases when adding transactions. */
  wishlist?: WishlistContext | null;
  /** "Record purchase" from the Wishlist screen: opens the add-transaction dialog prefilled for that item. */
  wishlistPurchaseRequest?: { itemId: string; token: number } | null;
}) {
  const [managePayees,setManagePayees]=useState(false);
  const [payeePending,setPayeePending]=useState(false);const [payeeFilter,setPayeeFilter]=useState<string|null>(null);
  const [inline, setInline] = useState<{key:string;entry?:Entry;schedule?:ScheduledItem;transferAccountId?:string} | null>(null);
  const [flags,setFlags]=useState(defaultFlags);const [flagVersion,setFlagVersion]=useState(1);
  const [schedules, setSchedules] = useState<Schedules | null>(null);
  const [schedulePage, setSchedulePage] = useState(1);
  const handledEdit = useRef<number | null>(null);
  const handledPurchase = useRef<number | null>(null);
  const handledCreate = useRef(0);
  useEffect(() => {
    if (createAccountRequest && createAccountRequest !== handledCreate.current) {
      handledCreate.current = createAccountRequest;
      setDialog({ kind: 'account' });
    }
  }, [createAccountRequest]);
  const [accounts, setAccounts] = useState<Accounts>({ version: 1, accounts: [] });
  const [register, setRegister] = useState<Register | null>(null);
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [search, setSearch] = useState('');
  const [reviewFilter,setReviewFilter]=useState(false);
  const [category, setCategory] = useState(''); const [status, setStatus] = useState('');
  const [page, setPage] = useState(1); const [refresh, setRefresh] = useState(0);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [conflict, setConflict] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [busy, setBusy] = useState(false); const [dialog, setDialog] = useState<Dialog | null>(null);
  // The loan add/edit forms' optional "Pair a Category" step; it changes the dialog title and back button.
  const [pairingStep, setPairingStep] = useState(false);
  useEffect(() => setPairingStep(false), [dialog]);
  const [notice, setNotice] = useState(''); const [audit, setAudit] = useState<Audit[] | null>(null);
  const [viewOpen, setViewOpen] = useState(false); const [moreOpen, setMoreOpen] = useState(false);
  const [viewSaving, setViewSaving] = useState(false);
  const [savedView, setSavedView] = useState<{ scope: string; value: ViewOptions } | null>(null);
  const scope = selectedAccount || 'all';
  const view = savedView?.scope === scope ? savedView.value : defaultView;
  const scheduledOpen = view.showScheduledTransactions;
  const [resizing, setResizing] = useState<{ scope: string; widths: ColumnWidths } | null>(null);
  const resizeRef = useRef<{ key: string; pointerId: number; startX: number; startWidths: ColumnWidths; scale: number } | null>(null);
  const tableRef = useRef<HTMLTableElement>(null);
  const latestWidths = useRef<ColumnWidths>({});
  const columnWidths = { ...defaultColumnWidths, ...(view.columnWidths ?? {}), ...(resizing?.scope === scope ? resizing.widths : {}) };
  const columnOrder = registerColumnOrder(view);
  const { from, to } = viewDates(view);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [auditPage, setAuditPage] = useState(1); const [auditTotal, setAuditTotal] = useState(0);
  const pending = useRef<{ signature: string; body: string; path: string; method: string; fields: object } | null>(null);
  const inFlight = useRef(false); const viewInFlight = useRef(false);
  function beginColumnResize(key: string, event: PointerEvent<HTMLSpanElement>) {
    if (disabled || viewSaving) return;
    event.preventDefault(); event.stopPropagation();
    const widths = { ...columnWidths };
    latestWidths.current = widths;
    setResizing({ scope, widths });
    // The table may be stretched to fill the page; convert screen pixels into stored width units.
    const total = columnOrder.reduce((sum, column) => sum + (widths[column] ?? 0), 0);
    const rendered = tableRef.current?.getBoundingClientRect().width || total;
    resizeRef.current = { key, pointerId: event.pointerId, startX: event.clientX, startWidths: widths, scale: total / rendered };
    event.currentTarget.setPointerCapture?.(event.pointerId);
  }
  function moveColumnResize(event: PointerEvent<HTMLSpanElement>) {
    const active = resizeRef.current;
    if (!active || active.pointerId !== event.pointerId) return;
    const widths = resizeColumnPair(active.startWidths, columnOrder, active.key, Math.round((event.clientX - active.startX) * active.scale));
    latestWidths.current = widths;
    setResizing({ scope, widths });
  }
  async function persistColumnWidths(widths: ColumnWidths) {
    if (viewInFlight.current) return;
    viewInFlight.current = true; setViewSaving(true);
    try {
      const next = await requestJson<ViewOptions>(`/api/register-views/${scope}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...view, columnWidths: widths }) });
      setSavedView({ scope, value: next });
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not save column widths.'); }
    finally { viewInFlight.current = false; setViewSaving(false); setResizing(current => current?.scope === scope ? null : current); }
  }
  async function persistScheduledVisibility(show: boolean) {
    if (viewInFlight.current) return;
    viewInFlight.current = true; setViewSaving(true);
    try {
      const next = await requestJson<ViewOptions>(`/api/register-views/${scope}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...view, showScheduledTransactions: show }) });
      setSavedView({ scope, value: next });
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not save scheduled transaction visibility.'); }
    finally { viewInFlight.current = false; setViewSaving(false); }
  }
  async function finishColumnResize(event: PointerEvent<HTMLSpanElement>) {
    if (!resizeRef.current || resizeRef.current.pointerId !== event.pointerId) return;
    resizeRef.current = null;
    const widths = { ...latestWidths.current };
    setResizing(null);
    await persistColumnWidths(widths);
  }
  function resizeHandle(key: string, label: string) {
    // The last column has no right-hand neighbour to trade width with.
    if (columnOrder.at(-1) === key) return null;
    return <span className="register-resize-handle" role="separator" aria-label={`Resize ${label} column`} aria-orientation="vertical" aria-valuemin={24} aria-valuemax={1200} aria-valuenow={columnWidths[key]} tabIndex={0}
      onPointerDown={event => beginColumnResize(key, event)} onPointerMove={moveColumnResize} onPointerUp={event => void finishColumnResize(event)}
      onPointerCancel={() => { resizeRef.current = null; setResizing(null); }}
      onKeyDown={event => { if (!viewSaving && (event.key === 'ArrowLeft' || event.key === 'ArrowRight')) { event.preventDefault(); const widths = resizeColumnPair(columnWidths, columnOrder, key, event.key === 'ArrowRight' ? 10 : -10); latestWidths.current = widths; setResizing({ scope, widths }); void persistColumnWidths(widths); } }} />;
  }
  async function loadView() {
    const value = await requestJson<ViewOptions>(`/api/register-views/${scope}`);
    setSavedView({ scope, value });
  }
  async function applyView(value: ViewOptions) {
    if (viewInFlight.current) return;
    viewInFlight.current = true; setViewSaving(true);
    try {
      const next = await requestJson<ViewOptions>(`/api/register-views/${scope}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value) });
      setSavedView({ scope, value: next }); setPage(1); setSelectedIds([]); setViewOpen(false);
    } finally { viewInFlight.current = false; setViewSaving(false); }
  }
  useEffect(() => {
    const controller = new AbortController(); setViewOpen(false); setMoreOpen(false); setSavedView(null); setSchedules(null);
    requestJson<ViewOptions>(`/api/register-views/${scope}`, { signal: controller.signal })
      .then(value => { if (!controller.signal.aborted) setSavedView({ scope, value }); })
      .catch(err => { if (!controller.signal.aborted) { setLoading(false); setRegister(null); setError('Could not load saved view options. Reload to retry. ' + err.message); } });
    return () => controller.abort();
  }, [scope, refresh]);
  useEffect(() => { setInline(null); setSchedulePage(1); }, [scope]);
  useEffect(() => { if (focusTransaction) { setPage(1); setSchedulePage(1); setSelectedIds([focusTransaction.id]); } }, [focusTransaction?.id]);
  useEffect(() => {
    onPendingChange?.(busy || uncertain || viewSaving || payeePending);
    return () => onPendingChange?.(false);
  }, [busy, uncertain, viewSaving, payeePending, onPendingChange]);
  useEffect(() => { setPage(1); setSelectedIds(focusTransaction?.accountId === selectedAccount ? [focusTransaction.id] : []); }, [selectedAccount, search, from, to, category, status, reviewFilter, view.showReconciled, focusTransaction?.id]);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true);
    if (savedView?.scope !== scope) return () => controller.abort();
    const focused = focusTransaction?.accountId === selectedAccount ? focusTransaction : null;
    const query = new URLSearchParams({ page: String(page), pageSize: focused ? '100' : '25', includeReconciled: String(focused ? true : view.showReconciled) });
    if (selectedAccount) query.set('accountId', selectedAccount);
    if (focused) { query.set('from', focused.date); query.set('to', focused.date); }
    else {
      if(reviewFilter)query.set('needsAttention','true');
      if (search) query.set('search', search);
      if(payeeFilter)query.set('payeeId',payeeFilter);
      if (from) query.set('from', from); if (to) query.set('to', to);
      if (category) query.set('categoryId', category); if (status) query.set('status', status);
    }
    requestJson<Register>(`/api/transactions?${query}`, { signal: controller.signal })
      .then(nextRegister => { if (!controller.signal.aborted) { setAccounts(nextRegister.accounts); setRegister(nextRegister); if (focused && !focused.scheduled && nextRegister.items.some(item => item.id === focused.id)) setSelectedIds([focused.id]); setLoading(false); } })
      .catch(err => { if (!controller.signal.aborted) { setError(err.message); setLoading(false); setRegister(null); } });
    return () => controller.abort();
  }, [selectedAccount, search, from, to, category, status, page, refresh, savedView, scope, reviewFilter, view.showReconciled, payeeFilter, focusTransaction?.id]);
  useEffect(() => {
    const controller = new AbortController();
    loadCategories(today(), controller.signal).then(setCategories).catch(err => { if (err.name !== 'AbortError') setError('Could not load category filters. Reload to retry.'); });
    return () => controller.abort();
  }, [refresh]);
  useEffect(() => {
    if (dialog?.kind !== 'history') return;
    const controller = new AbortController(); setAudit(null);
    requestJson<{ items: Audit[]; total: number }>(`/api/ledger/audit?page=${auditPage}&pageSize=25`, { signal: controller.signal })
      .then(result => { setAudit(result.items); setAuditTotal(result.total); })
      .catch(err => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [dialog?.kind, auditPage]);
  useEffect(() => {
    if (savedView?.scope !== scope) return;
    const controller = new AbortController();
    const focused = focusTransaction?.scheduled && focusTransaction.accountId === selectedAccount ? focusTransaction : null;
    const query = new URLSearchParams({page:String(schedulePage),pageSize:focused?'100':'25'});
    if(selectedAccount) query.set('accountId',selectedAccount);
    if(focused){query.set('from',focused.date);query.set('to',focused.date);}
    else {if(from) query.set('from',from); if(to) query.set('to',to);}
    requestJson<Schedules>(`/api/schedules?${query}`,{signal:controller.signal}).then(value=>{if(!controller.signal.aborted){setSchedules(value);if(focused&&value.items.some(item=>item.occurrenceId===focused.id))setSelectedIds([focused.id]);}})
      .catch(err=>{if(!controller.signal.aborted)setError('Could not load scheduled transactions. Reload to retry. '+err.message);});
    return ()=>controller.abort();
  },[scope,selectedAccount,from,to,schedulePage,refresh,savedView,focusTransaction?.id]);
  useEffect(() => {
    if (!focusTransaction || focusTransaction.accountId !== selectedAccount) return;
    const timer = window.setTimeout(() => {
      const selector = focusTransaction.splitIndex === undefined
        ? `[data-transaction-id="${focusTransaction.id}"]`
        : `[data-transaction-id="${focusTransaction.id}"][data-split-index="${focusTransaction.splitIndex}"]`;
      document.querySelector(selector)?.scrollIntoView?.({ block: 'center' });
    }, 0);
    return () => window.clearTimeout(timer);
  }, [focusTransaction?.id, focusTransaction?.splitIndex, selectedAccount, loading, register, schedules]);
  useEffect(() => {
    if(editAccountRequest && editAccountRequest.token !== handledEdit.current && selectedAccount === editAccountRequest.id && !loading) {
      const account=accounts.accounts.find(a=>a.id===editAccountRequest.id);
      if(account){handledEdit.current=editAccountRequest.token;setDialog({kind:'account',account});onEditAccountRequestHandled?.();}
    }
  },[editAccountRequest,selectedAccount,loading,accounts,onEditAccountRequestHandled]);
  useEffect(()=>{const controller=new AbortController();requestJson<{version:number;flags:Flag[]}>('/api/flags',{signal:controller.signal}).then(value=>{setFlags(value.flags);setFlagVersion(value.version);}).catch(err=>{if(!controller.signal.aborted)setError('Could not load flag names. Reload to retry. '+err.message);});return()=>controller.abort();},[refresh]);
  const changed=useRef(onChanged);changed.current=onChanged;
  useEffect(()=>{const timer=window.setInterval(()=>{if(inline||dialog||viewOpen||moreOpen||busy||uncertain||conflict||document.querySelector('.ledger-dialog,.flag-picker,.approval-picker,.transaction-calendar'))return;setRefresh(value=>value+1);changed.current();},30000);return()=>window.clearInterval(timer);},[inline,dialog,viewOpen,moreOpen,busy,uncertain,conflict]);
  useEffect(()=>{if(!liveRefresh||inline||dialog||viewOpen||moreOpen||busy||uncertain||conflict||document.querySelector('.ledger-dialog,.flag-picker,.approval-picker,.transaction-calendar'))return;setRefresh(value=>value+1);},[liveRefresh]);
  useEffect(() => {
    if (!wishlistPurchaseRequest || wishlistPurchaseRequest.token === handledPurchase.current || loading || !wishlist) return;
    handledPurchase.current = wishlistPurchaseRequest.token;
    setInline(null); setDialog({ kind: 'transaction', wishlistItemId: wishlistPurchaseRequest.itemId });
  }, [wishlistPurchaseRequest, loading, wishlist]);
  const reload = useCallback(() => { setError(''); setConflict(false); setRefresh(value => value + 1); }, []);
  async function mutate(path: string, method: string, fields: object) {
    if (inFlight.current || conflict || loading || viewInFlight.current) return false;
    const signature = path + JSON.stringify(fields);
    if (uncertain && pending.current?.signature !== signature) { setError('Retry the previous save first to resolve its outcome.'); return false; }
    inFlight.current = true; setBusy(true); setError('');
    // Retain the original command across lost-response retries.
    if (pending.current?.signature !== signature) pending.current = { signature, path, method, fields, body: JSON.stringify({ ...fields, commandId: crypto.randomUUID(), version: Math.max(accounts.version, register?.version ?? 1, schedules?.version ?? 1, flagVersion) }) };
    try {
      await requestJson(path, { method, headers: { 'Content-Type': 'application/json' }, body: pending.current.body });
      pending.current = null; setUncertain(false); setDialog(null); if(path!=='/api/flags'&&!path.endsWith('/flag'))setInline(null); setSelectedIds([]); setNotice(path==='/api/flags'?'Flag names saved.':path.endsWith('/flag')?'Flag saved.':path.startsWith('/api/schedules')&&!path.endsWith('/actions')?'Scheduled transaction saved. Actual balances change when you choose Enter Now.':'Ledger saved. Balances and budget activity updated.');
      setRefresh(value => value + 1); onChanged(); return true;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save. Retry the same action safely.');
      if (err instanceof ApiError && err.status < 500) { pending.current = null; setUncertain(false); if (err.status === 409) setConflict(true); }
      else setUncertain(true);
      return false;
    } finally { inFlight.current = false; setBusy(false); }
  }
  function open(next: Dialog) { setError(''); setNotice(''); setViewOpen(false); setMoreOpen(false); setDialog(next); }
  function close() { if (!inFlight.current) setDialog(null); }
  async function edit(entry: Entry) {
    setSelectedIds([entry.id]);
    setViewOpen(false);setMoreOpen(false);
    const scheduled=schedules?.items.find(s=>s.occurrenceId===entry.id);
    if(scheduled){setInline({key:entry.id,entry,schedule:scheduled,transferAccountId:entry.amount<0?scheduled.toAccountId??undefined:accounts.accounts.find(a=>a.name===entry.transferAccountName)?.id});return;}
    if (!entry.transferId) { setInline({key:entry.id,entry}); return; }
    try {
      const pair = await requestJson<Register>(`/api/transactions?transferId=${entry.transferId}`);
      setInline({key:entry.id,entry,transferAccountId:pair.items.find(t=>t.id!==entry.id)?.accountId});
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not load transfer.'); }
  }
  const current = accounts.accounts.find(a => a.id === selectedAccount);
  const openAccounts = accounts.accounts.filter(a => !a.isClosed);
  const disabled = busy || conflict || loading || uncertain || viewSaving;
  const feedback = <>{error && <div role="alert" className="ledger-error">{error} <button type="button" disabled={busy} onClick={reload}>{conflict ? 'Reload ledger' : 'Reload'}</button></div>}
    {conflict && <p>Review the refreshed ledger before submitting your changes again. Your form remains open.</p>}
    {uncertain && <p>The save outcome is unknown. Retry the original command safely before making another change. <button type="button" disabled={busy || loading} onClick={() => { const command = pending.current; if (command) void mutate(command.path, command.method, command.fields); }}>Retry save</button></p>}</>;
  const filtered = Boolean(payeeFilter || search || from || to || category || status || reviewFilter || !view.showReconciled);
  const shownAccounts = current ? [current] : accounts.accounts;
  const needsCategory = shownAccounts.reduce((sum, account) => sum + (account.attentionCount??account.needsCategoryCount), 0);
  const selected = [...(register?.items??[]),...(schedules?.items.map(s=>s.transaction)??[])].filter(entry=>selectedIds.includes(entry.id));
  const chosen = selected.length === 1 ? selected[0] : undefined;
  const chosenLocked = !chosen || chosen.isLocked || chosen.status === 'reconciled' || accounts.accounts.find(a => a.id === chosen.accountId)?.isClosed;
  const chosenSchedule = schedules?.items.find(s=>s.occurrenceId===chosen?.id);
  const chosenEditLocked = !chosen || chosen.canEdit === false || accounts.accounts.find(a=>a.id===chosen.accountId)?.isClosed;
  async function scheduleAction(item:ScheduledItem,action:string,date?:string) {
    await mutate(`/api/schedules/${item.id}/actions`,'POST',{action,occurrenceId:item.occurrenceId,date});
  }
  async function saveInline(fields:InlineFields,another:boolean) {
    let path='/api/transactions';let method='POST';let payload:object=fields;
    const transfer=fields.toAccountId;
    if(fields.wishlistItemId&&wishlist&&!inline?.entry) {path=wishlistPurchasePath(wishlist.budgetId,fields.wishlistItemId);payload={accountId:fields.accountId,date:fields.date,amount:fields.amount,payee:fields.payee,memo:fields.memo,categoryId:fields.categoryId,status:fields.status,splits:[]};}
    else if(inline?.schedule) {
      const item=inline.schedule;
      const incoming=Boolean(item.toAccountId&&fields.accountId===item.toAccountId);
      const scheduleFields={...fields,accountId:incoming?fields.toAccountId:fields.accountId,toAccountId:incoming?fields.accountId:fields.toAccountId,amount:incoming?-fields.amount:fields.amount};
      if(fields.onlyOnce){path=`/api/schedules/${item.id}/actions`;payload={action:'editOnce',occurrenceId:item.occurrenceId,date:fields.date,template:{amount:scheduleFields.amount,payee:fields.payee,memo:fields.memo,categoryId:fields.categoryId,splits:fields.splits}};}
      else {path=`/api/schedules/${item.id}`;method='PUT';payload={...scheduleFields,isPaused:item.isPaused,endDate:item.endDate,count:item.count,flagColor:item.transaction.flagColor};}
    } else if(!inline?.entry&&fields.date>today()) {path='/api/schedules';payload={...fields,accountId:transfer&&fields.amount>0?transfer:fields.accountId,toAccountId:transfer?(fields.amount>0?fields.accountId:transfer):null,amount:transfer?-Math.abs(fields.amount):fields.amount};}
    else if(transfer){path=inline?.entry?`/api/transfers/${inline.entry.id}`:'/api/transfers';method=inline?.entry?'PUT':'POST';payload={fromAccountId:fields.amount<0?fields.accountId:transfer,toAccountId:fields.amount<0?transfer:fields.accountId,date:fields.date,amount:Math.abs(fields.amount),memo:fields.memo,repeat:fields.repeat,confirmReconciledEdit:fields.confirmReconciledEdit};}
    else {if(inline?.entry){path+=`/${inline.entry.id}`;method='PUT';}}
    const saved=await mutate(path,method,payload);
    if(saved)setInline(another?{key:crypto.randomUUID()}:null);
    return saved;
  }
  async function renameFlags(next:Flag[]){return mutate('/api/flags','PUT',{flags:next});}
  function editor() {return inline&&<InlineTransactionEditor key={inline.key} entry={inline.entry} selectedAccount={selectedAccount} accounts={accounts.accounts} view={view} busy={disabled} onSave={saveInline} onCancel={()=>setInline(null)} repeat={inline.schedule?.repeat} transferAccountId={inline.transferAccountId} scheduled={Boolean(inline.schedule)} onShowPayeeTransactions={payee=>{setPayeeFilter(payee.id);setSearch('');setCategory('');setStatus('');setReviewFilter(false);setInline(null);setPage(1);}} onCategoriesChanged={()=>{void loadCategories(today()).then(setCategories).catch(()=>setError('Could not refresh category filters. Reload to retry.'));onChanged();}} onPayeesPending={setPayeePending} onPayeesChanged={()=>{setRefresh(n=>n+1);onChanged();}} postingError={inline.schedule?.postingError} requiresConfirmation={!inline.schedule&&Boolean(inline.entry?.isLocked||inline.entry?.status==='reconciled')} wishlist={wishlist}/>;}
  function clearFilters() {
    setPayeeFilter(null);setReviewFilter(false);setSearch(''); setCategory(''); setStatus(''); setPage(1);
    if (from || to || !view.showReconciled) void applyView({ ...view, datePreset:'allDates', showReconciled:true }).catch(err => setError(err.message));
  }
  const selectedActions = selectedIds.length > 0 && <div className="register-selected-actions"><strong>Selected transaction</strong><button disabled={disabled || chosenEditLocked} onClick={() => chosen && void edit(chosen)}>Edit selected</button><button disabled={disabled || chosenLocked || !!chosenSchedule} onClick={() => chosen && open({kind:'delete',entry:chosen})}>Delete selected</button>{chosenSchedule&&<><button disabled={disabled||chosenSchedule.isPaused} onClick={()=>void scheduleAction(chosenSchedule,'post',chosenSchedule.transaction.date>today()?today():undefined)}>Enter Now</button><button disabled={disabled||chosenSchedule.isPaused} onClick={()=>void scheduleAction(chosenSchedule,'skip')}>Skip</button><button disabled={disabled||chosenSchedule.isPaused} onClick={()=>{const d=new Date(chosenSchedule.transaction.date+'T12:00:00');d.setDate(d.getDate()+1);void scheduleAction(chosenSchedule,'snooze',`${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`);}}>Snooze one day</button><button disabled={disabled} onClick={()=>void scheduleAction(chosenSchedule,chosenSchedule.isPaused?'resume':'pause')}>{chosenSchedule.isPaused?'Resume':'Pause'}</button><button disabled={disabled} onClick={()=>void scheduleAction(chosenSchedule,'end')}>End schedule</button></>}<button onClick={() => setSelectedIds([])}>Clear selection</button></div>;
  const loanPage = current && isLoanAccount(current);
  return <section className="ledger-page account-register-page" aria-label="Accounts and transactions">
    {loanPage ? <>{!dialog && feedback}<LoanAccountPage account={current} refresh={refresh} disabled={disabled} onMenu={onMenu}
      onEdit={() => open({ kind: 'account', account: current })} onAddActivity={() => open({ kind: 'loanActivity', account: current })}
      onMutate={mutate} onOpenCategory={id => onOpenCategory?.(id, false)} onCreateTarget={id => onOpenCategory?.(id, true)} /></> : <>
      {focusTransaction && focusTransaction.accountId === selectedAccount && <div className="account-focus-banner" role="status">Showing the transaction selected from the {focusTransaction.source ?? 'Plan'} screen. <button onClick={() => setSelectedIds([focusTransaction.id])}>Highlight transaction</button></div>}
      {needsCategory > 0 && <div className="account-attention-banner"><span><span className="transaction-info">i</span> {needsCategory} new {needsCategory === 1 ? 'transaction' : 'transactions'} to approve or categorize.</span><button className="button-primary" onClick={() => { setReviewFilter(true);setCategory(''); setPage(1); }}>View</button></div>}
    <AccountHeader current={current} accounts={accounts.accounts} disabled={disabled} loading={loading && !register} onMenu={onMenu}
      onAddAccount={() => open({ kind: 'account' })} onEdit={() => current && open({ kind: 'account', account: current })}
      onReconcile={() => current && open({ kind: 'reconcile', account: current })}
      onFavorite={() => current && void mutate('/api/accounts/' + current.id, 'PUT', { name: current.name, isClosed: current.isClosed, isFavorite: !current.isFavorite })}/>
    {!dialog && feedback}{notice && <p className="sr-only" role="status">{notice}</p>}
    <div className="register-toolbar">
      <button aria-label="Add Transaction" className="register-add" disabled={disabled || !openAccounts.length} onClick={() => {setInline({key:crypto.randomUUID()});setSelectedIds([]);setViewOpen(false);setMoreOpen(false);}}><span className="register-plus">+</span>Add Transaction</button>
      <button disabled title="File import is planned for a later milestone"><RegisterIcon name="import"/>File Import</button>
      <span className="register-toolbar-divider"/>
      <button disabled title="Ledger changes are recorded in Audit history"><span className="register-history-icon">↶</span>Undo</button>
      <button disabled title="Ledger changes are recorded in Audit history"><span className="register-history-icon">↷</span>Redo</button>
      <div className="register-toolbar-right"><div className="register-view-control">
        <button disabled={disabled || savedView?.scope !== scope} aria-label="View options" aria-expanded={viewOpen} onClick={() => { setMoreOpen(false); setViewOpen(value => !value); }}>View <span className="register-caret">▾</span></button>
        {viewOpen && <ViewOptionsPanel value={view} busy={viewSaving} onApply={applyView} onCancel={() => setViewOpen(false)} onReload={loadView}/>}
        <button disabled={viewSaving} aria-label="More account actions" aria-expanded={moreOpen} onClick={() => { setViewOpen(false); setMoreOpen(value => !value); }}>More ▾</button>
        {moreOpen && <section className="register-view-panel" aria-label="Account actions and filters" onKeyDown={e => { if(e.key === 'Escape') setMoreOpen(false); }}>
          <div className="register-filter-fields">
            <label>Account<select value={selectedAccount} onChange={e => onSelectAccount(e.target.value)}><option value="">All accounts</option>{accounts.accounts.map(a => <option key={a.id} value={a.id}>{a.name}{a.isClosed ? ' (Closed)' : ''}</option>)}</select></label>
            <label>Category<select value={category} onChange={e => setCategory(e.target.value)}><option value="">All categories</option><option value="uncategorized">Needs category</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
            <label>Status<select value={status} onChange={e => setStatus(e.target.value)}><option value="">All statuses</option><option value="uncleared">Uncleared</option><option value="cleared">Cleared</option><option value="reconciled">Reconciled</option></select></label>
          </div>
          <div className="register-view-actions"><button disabled={disabled} onClick={() => open({kind:'account'})}>Add account</button><button disabled={disabled || openAccounts.length < 2} onClick={() => open({kind:'transfer'})}>Transfer</button>
            <button disabled={disabled} onClick={()=>{setMoreOpen(false);setManagePayees(true);}}>Manage Payees</button><button onClick={() => { setAuditPage(1); open({kind:'history'}); }}>Audit history</button><button disabled={busy} onClick={reload}>Refresh ledger</button><button disabled={disabled} onClick={clearFilters}>Reset filters</button></div>
          {selectedActions}
        </section>}
      </div><label className="register-search"><RegisterIcon name="search" size={15}/><input type="search" aria-label="Search" maxLength={160} placeholder={'Search ' + (current?.name ?? 'All Accounts')} value={search} onChange={e => setSearch(e.target.value)}/></label></div>
    </div>

    <div className="register-scroll">
      <table ref={tableRef} onKeyDown={e=>{if(e.key==='Escape'&&inline&&!disabled)setInline(null);}} className="register-table" aria-label="Transaction register"><colgroup>
        <col style={{width:columnWidths.check}}/><col style={{width:columnWidths.info}}/>{view.showFlags && <col style={{width:columnWidths.flag}}/>}{view.showImages && <col style={{width:columnWidths.image}}/>}<col style={{width:columnWidths.date}}/><col style={{width:columnWidths.payee}}/><col style={{width:columnWidths.category}}/><col style={{width:columnWidths.memo}}/><col style={{width:columnWidths.outflow}}/><col style={{width:columnWidths.inflow}}/>{view.showRunningBalance && <col style={{width:columnWidths.balance}}/>}<col style={{width:columnWidths.status}}/></colgroup>
        <thead><tr><th aria-label="Select transactions">{resizeHandle('check','selection')}<input type="checkbox" aria-label="Select visible transactions" checked={Boolean(register?.items.length && register.items.every(t => selectedIds.includes(t.id)))} onChange={e => setSelectedIds(e.target.checked ? register?.items.map(t => t.id) ?? [] : [])}/></th>
          <th aria-label="Attention">{resizeHandle('info','attention')}<span className="transaction-info muted-info" aria-hidden="true">i</span><span className="sr-only">Attention</span></th>{view.showFlags && <th className="register-decoration-heading" aria-label="Flag">{resizeHandle('flag','flag')}<RegisterIcon name="flag" size={15}/><span className="sr-only">Flag</span></th>}{view.showImages && <th className="register-decoration-heading" aria-label="Attachment">{resizeHandle('image','attachment')}<RegisterIcon name="image" size={15}/><span className="sr-only">Attachment</span></th>}
          <th aria-label="DATE" aria-sort="descending">{resizeHandle('date','date')}DATE <span className="register-sort-arrow">▾</span></th><th aria-label="PAYEE">{resizeHandle('payee','payee')}PAYEE</th><th aria-label="CATEGORY">{resizeHandle('category','category')}CATEGORY</th><th aria-label="MEMO">{resizeHandle('memo','memo')}MEMO</th><th aria-label="OUTFLOW" className="register-money">{resizeHandle('outflow','outflow')}OUTFLOW</th><th aria-label="INFLOW" className="register-money">{resizeHandle('inflow','inflow')}INFLOW</th>{view.showRunningBalance && <th aria-label="BALANCE" className="register-money">{resizeHandle('balance','balance')}BALANCE</th>}<th aria-label="Cleared status">{resizeHandle('status','cleared status')}<ClearedSymbol cleared title="Cleared status" /><span className="sr-only">Cleared status</span></th></tr></thead>
        <tbody>
          {inline&&(!inline.entry||(!loading&&(inline.schedule?!schedules?.items.some(s=>s.occurrenceId===inline.entry!.id):!register?.items.some(t=>t.id===inline.entry!.id))))&&editor()}
          {Boolean(schedules?.total) && <tr className="scheduled-transactions-row"><td colSpan={9+Number(view.showFlags)+Number(view.showImages)+Number(view.showRunningBalance)}><button disabled={viewSaving} aria-expanded={scheduledOpen} onClick={()=>void persistScheduledVisibility(!scheduledOpen)}><RegisterIcon name="calendar"/>Scheduled Transactions <DisclosureChevron open={scheduledOpen} className="scheduled-chevron" /> ({schedules?.total})</button></td></tr>}
          {scheduledOpen&&!loading&&schedules?.items.map(item=>inline?.entry?.id===item.occurrenceId? <ReactFragment key={item.occurrenceId}>{editor()}</ReactFragment> : <TransactionRow key={item.occurrenceId} entry={{...item.transaction,memo:item.postingError?`Needs review: ${item.postingError}`:item.transaction.memo}} onRowSelect={()=>setSelectedIds([item.occurrenceId])} flags={flags} onFlag={color=>mutate(`/api/schedules/${item.id}/flag`,'POST',{color})} onRenameFlags={renameFlags} view={view} scheduled paused={item.isPaused} focusedSplitIndex={focusTransaction?.id===item.occurrenceId?focusTransaction.splitIndex:undefined} account={accounts.accounts.find(a=>a.id===item.transaction.accountId)} categories={categories} selected={selectedIds.includes(item.occurrenceId)} disabled={disabled} onSelect={()=>setSelectedIds(ids=>ids.includes(item.occurrenceId)?ids.filter(id=>id!==item.occurrenceId):[...ids,item.occurrenceId])} onEdit={()=>void edit(item.transaction)} onToggle={()=>{}}/>)}
          {scheduledOpen&&!loading&&focusTransaction?.scheduled&&focusTransaction.accountId===selectedAccount&&focusTransaction.projected&&!schedules?.items.some(item=>item.occurrenceId===focusTransaction.id)&&<TransactionRow key={focusTransaction.id} entry={focusTransaction.projected} view={view} focusedSplitIndex={focusTransaction.splitIndex} scheduled account={accounts.accounts.find(a=>a.id===focusTransaction.accountId)} categories={categories} selected={selectedIds.includes(focusTransaction.id)} disabled onSelect={()=>setSelectedIds([focusTransaction.id])} onEdit={()=>{}} onToggle={()=>{}}/>}
          {scheduledOpen&&schedules&&schedules.total>25&&<tr><td colSpan={12}><nav className="register-pagination" aria-label="Scheduled transaction pages"><button disabled={schedulePage===1||loading} onClick={()=>setSchedulePage(p=>p-1)}>Previous scheduled</button><span>Page {schedulePage}</span><button disabled={schedulePage*25>=schedules.total||loading} onClick={()=>setSchedulePage(p=>p+1)}>Next scheduled</button></nav></td></tr>}
          {!loading && register?.items.map(entry => inline?.entry?.id===entry.id&&!inline.schedule ? <ReactFragment key={entry.id}>{editor()}</ReactFragment> : <TransactionRow key={entry.id} view={view} entry={entry} focusedSplitIndex={focusTransaction?.id===entry.id?focusTransaction.splitIndex:undefined} onRowSelect={()=>setSelectedIds([entry.id])} onReview={action=>mutate(`/api/transactions/${entry.id}/review`,'POST',{action})} flags={flags} onFlag={color=>mutate(`/api/transactions/${entry.id}/flag`,'POST',{color})} onRenameFlags={renameFlags} account={accounts.accounts.find(a => a.id === entry.accountId)} categories={categories}
            selected={selectedIds.includes(entry.id)} disabled={disabled} onSelect={() => setSelectedIds(ids => ids.includes(entry.id) ? ids.filter(id => id !== entry.id) : [...ids, entry.id])}
            onEdit={() => void edit(entry)} onToggle={() => void mutate('/api/transactions/' + entry.id + '/status', 'POST', {status:entry.status === 'uncleared' ? 'cleared' : 'uncleared'})}/>)}
        </tbody>
      </table>
      {loading ? <p className="ledger-empty" role="status">Loading transactions…</p> : !accounts.accounts.length ? <div className="ledger-empty"><h2>Give your money a home</h2><p>Add a cash account with its current balance to begin your register.</p></div>
        : register && !register.items.length && <p className="ledger-empty">No transactions match these filters.</p>}
      {filtered && <div className="register-hidden-message"><p>Some transactions are hidden by filters.</p><button disabled={disabled} onClick={clearFilters}>View All</button></div>}
      {register && (register.total > 25 || page > 1) && <nav className="register-pagination" aria-label="Transaction pages"><button disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Previous</button><span>Page {page} · {register.total} transactions</span><button disabled={page * 25 >= register.total || loading} onClick={() => setPage(p => p + 1)}>Next</button></nav>}
    </div></>}
    {managePayees&&<ManagePayees accounts={accounts.accounts} categories={categories} onPendingChange={setPayeePending} onClose={()=>setManagePayees(false)} onChanged={()=>{setRefresh(n=>n+1);onChanged();}} onShowTransactions={payee=>{setPayeeFilter(payee.id);setSearch('');setCategory('');setStatus('');setReviewFilter(false);setManagePayees(false);setPage(1);}}/>}
    {selected.length>0&&!inline&&!dialog&&<TransactionSelectionBar key={selected.map(t=>t.id).join(',')} entries={selected} accounts={accounts.accounts} flags={flags} disabled={disabled} scheduled={selected.some(t=>schedules?.items.some(s=>s.occurrenceId===t.id))} onClear={()=>setSelectedIds([])} onRenameFlags={renameFlags} onApply={action=>mutate('/api/transactions/selection','POST',{ids:selected.map(t=>t.id),...action})}/>}
    {dialog && <LedgerDialog onBack={dialog.kind === 'account' && pairingStep ? () => { if (!inFlight.current) setPairingStep(false); } : dialog.kind === 'account' && !dialog.account && dialog.accountType ? () => setDialog({ kind: 'account' }) : undefined} title={dialog.kind === 'loanActivity' ? 'Add Activity' : dialog.kind === 'account' && pairingStep ? 'Optional: Pair a Category' : dialog.kind === 'account' ? dialog.account ? 'Edit Account' : dialog.accountType ? isLoanAccount({ accountType: dialog.accountType }) ? 'Add Unlinked Account' : `Add ${accountTypeLabel(dialog.accountType)} account` : 'Select Account Type' : dialog.kind === 'transaction' ? dialog.entry ? 'Edit transaction' : 'Add transaction' : dialog.kind === 'transfer' ? dialog.entry ? 'Edit transfer' : 'Transfer money' : dialog.kind === 'reconcile' ? 'Reconcile account' : dialog.kind === 'history' ? 'Audit history' : 'Delete transaction'} onClose={close}>
      {feedback}
      {dialog.kind === 'transaction' && <TransactionForm accounts={accounts.accounts} selectedAccount={selectedAccount} entry={dialog.entry} busy={disabled} wishlist={wishlist} initialWishlistItemId={dialog.wishlistItemId} onSubmit={async ({ wishlistItemId, ...fields }) => { await mutate(wishlistItemId && wishlist && !dialog.entry ? wishlistPurchasePath(wishlist.budgetId, wishlistItemId) : `/api/transactions${dialog.entry ? `/${dialog.entry.id}` : ''}`, dialog.entry ? 'PUT' : 'POST', fields); }} />}
      {dialog.kind === 'account' && (dialog.account ? isLoanAccount(dialog.account) ? <EditLoanAccountForm account={dialog.account} busy={disabled} onCancel={close} pairing={pairingStep} onPairingChange={setPairingStep} onOpenCategory={id => { setDialog(null); onOpenCategory?.(id, false); }} onSubmit={async fields => { await mutate(`/api/accounts/${dialog.account!.id}`, 'PUT', fields); }}/>
        : <EditAccountForm account={dialog.account} busy={disabled} onCancel={close} onSubmit={async fields => { await mutate(`/api/accounts/${dialog.account!.id}`, 'PUT', fields); }}/>
        : dialog.accountType && isLoanAccount({ accountType: dialog.accountType }) ? <LoanAccountForm key={dialog.accountType} accountType={dialog.accountType as LoanAccountType} busy={disabled} pairing={pairingStep} onPairingChange={setPairingStep} onSubmit={async fields => { await mutate('/api/accounts', 'POST', fields); }}/>
        : dialog.accountType ? <AccountForm key={dialog.accountType} accountType={dialog.accountType} busy={disabled} onSubmit={async fields => { await mutate('/api/accounts', 'POST', fields); }}/> : <AccountTypeList onSelect={accountType => setDialog({ kind: 'account', accountType })}/>) }
      {dialog.kind === 'loanActivity' && <div className="loan-activity-dialog">
        <div className="loan-activity-tabs" role="tablist" aria-label="Activity type">
          <button type="button" role="tab" aria-selected={dialog.mode !== 'other'} onClick={() => setDialog({ ...dialog, mode: 'payment' })}>Payment</button>
          <button type="button" role="tab" aria-selected={dialog.mode === 'other'} onClick={() => setDialog({ ...dialog, mode: 'other' })}>Interest, fee or correction</button></div>
        {dialog.mode === 'other' ? <TransactionForm accounts={[dialog.account]} selectedAccount={dialog.account.id} busy={disabled} onSubmit={async fields => { await mutate('/api/transactions', 'POST', fields); }} />
          : <TransferForm key="loan-payment" accounts={openAccounts} selectedAccount="" dialog={{ kind: 'transfer', from: openAccounts.find(a => !isLoanAccount(a) && !isCreditAccount(a))?.id ?? openAccounts.find(a => a.id !== dialog.account.id)?.id, to: dialog.account.id }} busy={disabled} onSubmit={async fields => { await mutate('/api/transfers', 'POST', fields); }} />}
      </div>}
      {dialog.kind === 'transfer' && <TransferForm accounts={openAccounts} selectedAccount={selectedAccount} dialog={dialog} busy={disabled} onSubmit={async fields => { await mutate(`/api/transfers${dialog.entry ? `/${dialog.entry.id}` : ''}`, dialog.entry ? 'PUT' : 'POST', fields); }} />}
      {dialog.kind === 'reconcile' && <ReconcileForm account={accounts.accounts.find(a => a.id === dialog.account.id) ?? dialog.account} busy={disabled} onSubmit={async fields => { await mutate(`/api/accounts/${dialog.account.id}/reconcile`, 'POST', fields); }} />}
      {dialog.kind === 'delete' && <><p>Delete this entry{dialog.entry.transferId ? ' and its linked transfer entry' : ''}? Balances and budget activity will be recalculated. The audit history is retained.</p><button disabled={disabled} className="button-primary" onClick={() => mutate(`/api/transactions/${dialog.entry.id}/delete`, 'POST', {})}>Confirm deletion</button></>}
      {dialog.kind === 'history' && <>{audit === null ? <p role="status">Loading audit history…</p> : !audit.length ? <p>No ledger changes yet.</p> : audit.map(item => <article className="audit-entry" key={item.id}><strong>{item.kind.split('/')[0]}</strong><small>{new Date(item.createdAt).toLocaleString()} · Revision {item.version}</small><details><summary>View before and after</summary><h3>Before</h3><pre>{JSON.stringify(JSON.parse(item.beforeJson), null, 2)}</pre><h3>After</h3><pre>{JSON.stringify(JSON.parse(item.afterJson), null, 2)}</pre></details></article>)}
        <nav className="register-pagination"><button disabled={auditPage === 1} onClick={() => setAuditPage(p => p - 1)}>Previous changes</button><span>Page {auditPage}</span><button disabled={auditPage * 25 >= auditTotal} onClick={() => setAuditPage(p => p + 1)}>Next changes</button></nav></>}
    </LedgerDialog>}
  </section>;
}

function AccountTypeList({ onSelect }: { onSelect: (type: AccountType) => void }) {
  return <div className="account-type-list">{accountTypeSections.map(section => <section key={section.title} aria-label={section.title}>
    <h3>{section.title}</h3><p>{section.description}</p>
    {section.types.map(type => <button type="button" key={type.value} onClick={() => onSelect(type.value)}>{type.label}</button>)}
  </section>)}</div>;
}

function AccountForm({ accountType, busy, onSubmit }: { accountType: AccountType; busy: boolean; onSubmit: (fields: object) => Promise<void> }) {
  const credit = isCreditAccount({ accountType });
  const [name, setName] = useState(''); const [date, setDate] = useState(today());
  const [amount, setAmount] = useState('0'); const [error, setError] = useState('');
  return <form className="ledger-form" onSubmit={async e => { e.preventDefault(); const parsed = parseMoney(amount); if (parsed === null || credit && parsed < 0) { setError(credit ? 'Enter the amount you owe as a positive balance with at most two decimal places.' : 'Enter a balance with at most two decimal places.'); return; } await onSubmit({ name, accountType, date, openingBalance: credit && parsed !== 0 ? -parsed : parsed }); }}>
    <label>Account name<input required maxLength={120} value={name} onChange={e => setName(e.target.value)} /></label>
    <label>Opening date<input required min="1900-01-01" max="9998-12-31" type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
    <label>{credit ? 'Current balance owed' : 'Opening balance'}<MoneyInput required value={amount} onChange={raw => setAmount(raw)} /></label>
    <p>{credit ? 'The balance owed is recorded once as existing debt. It does not change Ready to Assign; assign money to this account’s payment category to pay it off.' : 'The opening balance is recorded once as a cleared ledger entry and contributes to Ready to Assign.'}</p>
    {error && <p role="alert">{error}</p>}<button className="button-primary" disabled={busy}>Save account</button>
  </form>;
}

function TransferForm({ accounts, selectedAccount, dialog, busy, onSubmit }: { accounts: Account[]; selectedAccount: string; dialog: Extract<Dialog, { kind: 'transfer' }>; busy: boolean; onSubmit: (fields: object) => Promise<void> }) {
  const [from, setFrom] = useState(dialog.from ?? (selectedAccount || accounts[0]?.id || ''));
  const [to, setTo] = useState(dialog.to ?? accounts.find(a => a.id !== from)?.id ?? '');
  const [amount, setAmount] = useState(dialog.entry ? String(Math.abs(dialog.entry.amount)) : '');
  const [date, setDate] = useState(dialog.entry?.date ?? today()); const [memo, setMemo] = useState(dialog.entry?.memo ?? ''); const [error, setError] = useState('');
  return <form className="ledger-form" onSubmit={async e => { e.preventDefault(); const parsed = parseMoney(amount); if (parsed === null || parsed <= 0 || from === to) { setError('Choose different accounts and a positive amount with at most two decimal places.'); return; } await onSubmit({ fromAccountId: from, toAccountId: to, amount: parsed, date, memo }); }}>
    <p>{isLoanAccount(accounts.find(a => a.id === to)) || isLoanAccount(accounts.find(a => a.id === from)) ? 'A payment into a loan spends from its paired Plan category (or Ready to Assign when unpaired) and reduces the debt.' : 'Both account entries are saved together. Transfers do not change budget income or spending.'}</p>
    <label>From account<select required value={from} onChange={e => setFrom(e.target.value)}>{accounts.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
    <label>To account<select required value={to} onChange={e => setTo(e.target.value)}>{accounts.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
    <label>Date<input type="date" required min="1900-01-01" max="9998-12-31" value={date} onChange={e => setDate(e.target.value)} /></label><label>Amount<MoneyInput required value={amount} onChange={raw => setAmount(raw)} /></label>
    <label>Memo<input maxLength={1000} value={memo} onChange={e => setMemo(e.target.value)} /></label>{error && <p role="alert">{error}</p>}<button disabled={busy} className="button-primary">Save transfer</button>
  </form>;
}

function ReconcileForm({ account, busy, onSubmit }: { account: Account; busy: boolean; onSubmit: (fields: object) => Promise<void> }) {
  const [statement, setStatement] = useState(''); const amount = parseMoney(statement);
  const difference = amount === null ? null : Math.round((amount - account.clearedBalance) * 100) / 100;
  return <form className="ledger-form" onSubmit={async e => { e.preventDefault(); if (amount !== null && difference === 0) await onSubmit({ statementBalance: amount }); }}>
    <p>{account.name} · Cleared balance: {formatMoney(account.clearedBalance)}</p>
    <label>Statement balance<MoneyInput required value={statement} onChange={raw => setStatement(raw)} /></label>
    <p role="status">Difference: {difference === null ? 'Enter your statement balance' : formatMoney(difference)}</p>
    <p>Clear or correct entries until the difference is zero. Confirming records a snapshot and locks cleared entries; use a new correcting entry for later corrections.</p>
    <button disabled={busy || difference !== 0} className="button-primary">Confirm reconciliation</button>
  </form>;
}

export function registerColumnOrder(view: Pick<ViewOptions, 'showFlags' | 'showImages' | 'showRunningBalance'>) {
  return ['check', 'info', ...(view.showFlags ? ['flag'] : []), ...(view.showImages ? ['image'] : []), 'date', 'payee', 'category', 'memo', 'outflow', 'inflow', ...(view.showRunningBalance ? ['balance'] : []), 'status'];
}

// Dragging a column's right edge trades width only with the column to its right, so the table's
// total width and every other column stay unchanged. Both columns keep at least 24px.
export function resizeColumnPair(widths: ColumnWidths, order: string[], key: string, delta: number): ColumnWidths {
  const neighbour = order[order.indexOf(key) + 1];
  if (!neighbour) return widths;
  const current = widths[key] ?? 0, next = widths[neighbour] ?? 0;
  const applied = Math.max(24 - current, Math.min(next - 24, delta));
  return { ...widths, [key]: current + applied, [neighbour]: next - applied };
}
