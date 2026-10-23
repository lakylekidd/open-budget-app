import { useEffect, useMemo, useRef, useState } from 'react';
import { AccountSidebarRow } from './ledger/AccountSidebarRow';
import { AccountsPage } from './ledger/AccountsPage';
import { type Accounts, type Entry, type Register, type ScheduledItem, type Schedules, requestJson } from './ledger/api';
import { parseAssignment } from './budgetMoney';
import { categoryProgress, categoryTransactionAmount, moveItem } from './planHelpers';
import { PlanTransactionList, type PlanTransactionItem } from './PlanTransactionList';
import { LongTermPlanning } from './longTermPlanning/LongTermPlanning';
import { AuthGate } from './AuthGate';

type Category = {
  id: string;
  isArchived?: boolean;
  sortOrder?: number;
  name: string;
  assigned: number;
  activity: number;
  available: number;
  target: number;
  icon?: string;
  snoozed?: boolean;
};

type CategoryGroup = { id: string; name: string; categories: Category[]; isArchived?: boolean; sortOrder?: number };
type Filter = 'All' | 'Overspent' | 'Underfunded' | 'Overfunded' | 'Money available' | 'Snoozed' | 'Archived';
type BudgetState = { month: string; groups: CategoryGroup[]; note?: string; version: number; openingReadyToAssign: number; fundsAdded: number; ledgerIncome: number; readyToAssign: number };
type BudgetPlan = { id: string; name: string; currency: string; version: number };

const filters: Filter[] = ['All', 'Overspent', 'Underfunded', 'Overfunded', 'Money available', 'Snoozed', 'Archived'];

const currency = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
const money = (value: number) => currency.format(value);
const shortMonthText = (value: string) => new Intl.DateTimeFormat('en-GB', { month: 'short', year: 'numeric' }).format(new Date(`${value}-01T12:00:00`));
const monthBounds = (month: string) => {
  const [year, monthNumber] = month.split('-').map(Number);
  const format = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  return { from: `${month}-01`, to: format(new Date(year, monthNumber, 0)) };
};

type PlanFocus = { id: string; accountId: string; date: string; scheduled: boolean; projected?: Entry; source?: 'Plan' | 'Long-Term Planning' };
type CategoryTransactionDialog = { title: string; categoryId: string; kind: 'activity' | 'upcoming'; items: PlanTransactionItem[]; loading: boolean; error: string; requestId: number };

function Icon({ name, size = 18 }: { name: string; size?: number }) {
  if (['plan', 'bank', 'chart'].includes(name)) return <img src={`/icon-${name}.svg`} width={size} height={size} alt="" aria-hidden="true" />;
  const common = { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 1.8, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, 'aria-hidden': true as const };
  const paths: Record<string, React.ReactNode> = {
    plan: <><path d="M4 8h16v13H4zM7 4h10v4M2 21h20"/><path d="m5 13 7 4 7-4"/><circle cx="12" cy="11" r="2.5"/></>,
    bank: <><path d="m2 8 10-6 10 6H2ZM2 21h20M4 18h16M5 9v9M10 9v9M15 9v9M20 9v9"/></>,
    users: <><circle cx="8" cy="7" r="3"/><circle cx="17" cy="8" r="2.5"/><path d="M2 21v-4a6 6 0 0 1 12 0v4H2ZM16 13a5 5 0 0 1 6 5v3h-6"/></>,
    views: <><circle cx="12" cy="12" r="10"/><path d="M6 8h12M8 12h8M10 16h4"/></>,
    bars: <><path strokeWidth="5" d="M4 6h16M4 17h16"/></>,
    lines: <><path d="M4 5h16M4 9h16M4 13h16M4 17h16"/></>,
    calendar: <><rect x="3.5" y="5" width="17" height="15" rx="2.5" /><path d="M7.5 3.5v3M16.5 3.5v3M3.5 9.5h17" /></>,
    chart: <><path d="M4 19V5M4 19h17" /><path d="m7 15 3.4-4 3.1 2.2 5.5-6.2" /></>,
    wallet: <><rect x="3" y="5" width="18" height="15" rx="2.5" /><path d="M3 8h18M15.5 13h2.5" /><path d="M6 5V3h12v2" /></>,
    search: <><circle cx="10.8" cy="10.8" r="6.5" /><path d="m16 16 4.5 4.5" /></>,
    chevron: <path d="m8 10 4 4 4-4" />,
    left: <path d="m14.5 5-7 7 7 7" />,
    right: <path d="m9.5 5 7 7-7 7" />,
    plus: <><path d="M12 5v14M5 12h14" /></>,
    dots: <><circle cx="5" cy="12" r="1" fill="currentColor" /><circle cx="12" cy="12" r="1" fill="currentColor" /><circle cx="19" cy="12" r="1" fill="currentColor" /></>,
    arrow: <><path d="M5 12h13M13 6l6 6-6 6" /></>,
    menu: <><path d="M4 7h16M4 12h16M4 17h16" /></>,
    close: <><path d="m6 6 12 12M18 6 6 18" /></>,
    check: <path d="m5 12 4 4L19 6" />,
    spark: <><path d="m12 3 1.5 6.5L20 12l-6.5 1.5L12 20l-1.5-6.5L4 12l6.5-2.5L12 3Z" /><path d="m19 2 .6 2.4L22 5l-2.4.6L19 8l-.6-2.4L16 5l2.4-.6L19 2Z" /></>,
    transfer: <><path d="M5 8h14M15 4l4 4-4 4M19 16H5m4 4-4-4 4-4" /></>,
  };
  return <svg {...common}>{paths[name] || <circle cx="12" cy="12" r="8" />}</svg>;
}

function BudgetApplication() {
  const [plan, setPlan] = useState<BudgetPlan>({ id: 'default', name: 'My Budget', currency: 'EUR', version: 1 });
  const [accounts, setAccounts] = useState<Accounts>({ version: 1, accounts: [] });
  const [accountError, setAccountError] = useState(false);
  const [ledgerPending, setLedgerPending] = useState(false);
  const [selectedAccount, setSelectedAccount] = useState(() => new URLSearchParams(window.location.search).get('account') || '');
  const [accountEditRequest,setAccountEditRequest]=useState<{id:string;token:number}>();
  const [ledgerRefresh, setLedgerRefresh] = useState(0);
  const [budgetRefresh, setBudgetRefresh] = useState(0);
  const [data, setData] = useState<BudgetState>({ month: '2026-10', groups: [], note: '', version: 1, openingReadyToAssign: 0, fundsAdded: 0, ledgerIncome: 0, readyToAssign: 0 });
  const [saveState, setSaveState] = useState<'loading' | 'saved' | 'saving' | 'offline' | 'conflict'>('loading');
  const loadedMonth = useRef<string | null>(null);
  const lastSaved = useRef<string | null>(null);
  const saveController = useRef<AbortController | null>(null);
  const [filter, setFilter] = useState<Filter>('All');
  const [collapsed, setCollapsed] = useState<string[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const [assignmentError,setAssignmentError]=useState('');
  const [moveOpen, setMoveOpen] = useState(false);
  const [conflictBudget, setConflictBudget] = useState<BudgetState | null>(null);
  const [mobileMenu, setMobileMenu] = useState(false);
  const [activePage, setActivePage] = useState(() => new URLSearchParams(window.location.search).get('page') || 'Plan');
  const [planningPending, setPlanningPending] = useState(false);
  const [toast, setToast] = useState('');
  const [showSearch, setShowSearch] = useState(false);
  const [query, setQuery] = useState('');
  const [moveFrom, setMoveFrom] = useState('coffee');
  const [moveTo, setMoveTo] = useState('clothing');
  const [moveAmount, setMoveAmount] = useState('25');
  const [progressOn, setProgressOn] = useState(true);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [history, setHistory] = useState<BudgetState[]>([]);
  const [future, setFuture] = useState<BudgetState[]>([]);
  const [upcomingItems, setUpcomingItems] = useState<ScheduledItem[]>([]);
  const [upcomingLoading, setUpcomingLoading] = useState(true);
  const [upcomingError, setUpcomingError] = useState('');
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [categoryDialog, setCategoryDialog] = useState<CategoryTransactionDialog | null>(null);
  const [focusTransaction, setFocusTransaction] = useState<PlanFocus | null>(null);
  const dialogRequestId = useRef(0);
  const [addCategoryGroupId, setAddCategoryGroupId] = useState<string | null>(null);
  const [newCategoryName, setNewCategoryName] = useState('');
  const [categoryNameError, setCategoryNameError] = useState('');
  const [dragSource, setDragSource] = useState<{ kind: 'group' | 'category'; id: string } | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    requestJson<Accounts>('/api/accounts', { signal: controller.signal }).then(value => { setAccounts(value); setAccountError(false); })
      .catch(error => { if (error.name !== 'AbortError') setAccountError(true); });
    return () => controller.abort();
  }, [ledgerRefresh]);

  function navigate(page: string, accountId = '', transaction: PlanFocus | null = null) {
    if (ledgerPending) { setToast('Resolve the pending ledger save before leaving accounts.'); return; }
    if (planningPending) { setToast('Resolve the pending planning edit before changing screens.'); return; }
    if (editingId || saveState === 'saving' || conflictBudget || (loadedMonth.current === data.month && lastSaved.current !== JSON.stringify(data))) {
      setToast('Wait for the plan to save, or resolve its save error before opening accounts.'); return;
    }
    const params = new URLSearchParams(); params.set('page', page); if (accountId) params.set('account', accountId);
    window.history.pushState(null, '', `${window.location.pathname}?${params}`);
    setActivePage(page); setSelectedAccount(accountId); setFocusTransaction(transaction); setMobileMenu(false);
    if (page === 'Plan') setBudgetRefresh(value => value + 1);
  }

  useEffect(() => {
    const restore = () => { const params = new URLSearchParams(window.location.search); setActivePage(params.get('page') || 'Plan'); setSelectedAccount(params.get('account') || ''); setFocusTransaction(null); };
    window.addEventListener('popstate', restore); return () => window.removeEventListener('popstate', restore);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    fetch('/api/budget', { signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(`Budget plan load failed (${response.status})`);
        setPlan(await response.json() as BudgetPlan);
      })
      .catch(error => { if (error.name !== 'AbortError') setToast('Could not load budget details.'); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadedMonth.current = null;
    setSaveState('loading');
    fetch(`/api/budget/${data.month}`, { signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(`Budget load failed (${response.status})`);
        return await response.json() as BudgetState;
      })
      .then(budget => {
        loadedMonth.current = budget.month;
        lastSaved.current = JSON.stringify(budget);
        setConflictBudget(null);
        setData(budget);
        setHistory([]);setFuture([]);setEditingId(null);setAssignmentError('');setSelectedIds([]);
        setSaveState('saved');
      })
      .catch(error => {
        if (error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Could not reach the budget API. Start the backend and reload.');
        }
      });
    return () => controller.abort();
  }, [data.month, budgetRefresh]);

  useEffect(() => {
    const controller = new AbortController();
    const { from, to } = monthBounds(data.month);
    setUpcomingLoading(true); setUpcomingError(''); setUpcomingItems([]); setSelectedCategoryId(null);
    const load = async () => {
      try {
        const first = await requestJson<Schedules>(`/api/schedules?${new URLSearchParams({ from, to, page: '1', pageSize: '100' })}`, { signal: controller.signal });
        const items = [...first.items];
        for (let page = 2; (page - 1) * first.pageSize < first.total; page++) {
          const next = await requestJson<Schedules>(`/api/schedules?${new URLSearchParams({ from, to, page: String(page), pageSize: '100' })}`, { signal: controller.signal });
          items.push(...next.items);
        }
        if (!controller.signal.aborted) setUpcomingItems(items.filter(item => !item.isPaused));
      } catch (error) {
        if (!controller.signal.aborted && error instanceof Error) setUpcomingError(error.message);
      } finally { if (!controller.signal.aborted) setUpcomingLoading(false); }
    };
    void load();
    return () => controller.abort();
  }, [data.month, budgetRefresh]);

  useEffect(() => {
    if (loadedMonth.current !== data.month || conflictBudget) return;
    if (lastSaved.current === JSON.stringify(data)) return;
    setSaveState('saving');
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      saveController.current?.abort();
      saveController.current = controller;
      setSaveState('saving');
      const request = {
        month: data.month,
        note: data.note ?? '',
        version: data.version,
        fundsAdded: data.fundsAdded,
        groups: data.groups.map((group, groupOrder) => ({
          id: group.id, name: group.name, sortOrder: groupOrder, isArchived: Boolean(group.isArchived),
          categories: group.categories.map((category, categoryOrder) => ({
            id: category.id, name: category.name, assigned: category.assigned, target: category.target,
            icon: category.icon ?? null, snoozed: Boolean(category.snoozed), sortOrder: categoryOrder,
            isArchived: Boolean(category.isArchived),
          })),
        })),
      };
      try {
        const response = await fetch(`/api/budget/${data.month}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request), signal: controller.signal });
        if (response.status === 409) {
          const problem = await response.json();
          setConflictBudget(problem.current as BudgetState);
          setSaveState('conflict');
          setToast('This month changed in another session. Choose which version to keep.');
          return;
        }
        if (!response.ok) throw new Error(`Budget save failed (${response.status})`);
        const saved = await response.json() as BudgetState;
        lastSaved.current = JSON.stringify(saved);
        setData(saved);
        setSaveState('saved');
      } catch (error) {
        if (error instanceof Error && error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Changes could not be saved. Check the backend connection.');
        }
      }
    }, 450);
    return () => window.clearTimeout(timer);
  }, [data, conflictBudget]);

  const allCategories = useMemo(() => data.groups.flatMap(group => group.categories), [data.groups]);
  const totals = useMemo(() => {
    const assigned = allCategories.reduce((sum, item) => sum + item.assigned, 0);
    const activity = allCategories.reduce((sum, item) => sum + item.activity, 0);
    const available = allCategories.reduce((sum, item) => sum + item.available, 0);
    return { assigned, activity, available };
  }, [allCategories]);
  const targetSum = allCategories.reduce((sum, item) => sum + item.target, 0);
  const overspentCount = allCategories.filter(item => item.available < 0).length;
  const visibleCategories = data.groups.flatMap(group => group.categories.map(category => ({ ...category, isArchived: Boolean(category.isArchived || group.isArchived) })));
  const moveCategories = data.groups.filter(group => !group.isArchived).flatMap(group => group.categories.filter(category => !category.isArchived));
  const matchingCategoryCount = visibleCategories.filter(matchesFilter).length;
  const selectedCategory = visibleCategories.find(category => category.id === selectedCategoryId);
  const selectedUpcoming = selectedCategory ? upcomingItems.flatMap(item => { const amount = categoryTransactionAmount(item.transaction, selectedCategory.id); return amount < 0 ? [{ item, amount }] : []; }) : [];
  const selectedUpcomingTotal = selectedUpcoming.reduce((sum, item) => sum + item.amount, 0);

  function updateCategory(id: string, updater: (category: Category) => Category) {
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === id ? updater(category) : category) })) }; });
  }

  function changeMonth(delta: number) {
    if(editingId || loadedMonth.current!==data.month || saveState==='saving' || conflictBudget || lastSaved.current!==JSON.stringify(data)){
      setToast('Finish the assignment and wait for this month to save before changing months.');return;
    }
    const [year, month] = data.month.split('-').map(Number);
    const date = new Date(year, month - 1 + delta, 1);
    setData(current => ({ ...current, month: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}` }));
  }

  function toggleGroup(id: string) {
    setCollapsed(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id]);
  }

  function startEdit(category: Category) {
    setAssignmentError('');
    setEditingId(category.id);
    setEditValue(String(category.assigned));
  }

  function saveEdit(category: Category) {
    const next = parseAssignment(editValue);
    if(next===null){setAssignmentError('Enter a valid amount with at most two decimal places.');return;}
    setAssignmentError('');
    updateCategory(category.id, item => ({ ...item, assigned: next }));
    setEditingId(null);
    setToast('Assignment updated');
    window.setTimeout(() => setToast(''), 2200);
  }

  function addCategory(groupId: string) {
    setAddCategoryGroupId(groupId); setNewCategoryName(''); setCategoryNameError('');
  }

  function submitCategory(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = newCategoryName.trim();
    const groupId = addCategoryGroupId;
    const group = data.groups.find(item => item.id === groupId);
    if (!group || !name) { setCategoryNameError('Enter a category name.'); return; }
    if (name.length > 160) { setCategoryNameError('Category names can be up to 160 characters.'); return; }
    if (group.categories.some(item => item.name.trim().toLocaleLowerCase() === name.toLocaleLowerCase())) { setCategoryNameError('This group already has a category with that name.'); return; }
    const id = `category-${crypto.randomUUID()}`;
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(item => item.id === groupId ? { ...item, categories: [...item.categories, { id, name, assigned: 0, activity: 0, available: 0, target: 0, icon: '＋', isArchived: false }] } : item) }; });
    setAddCategoryGroupId(null);
    setToast('Category added');
    window.setTimeout(() => setToast(''), 2200);
  }

  function applyMove(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const amount = parseAssignment(moveAmount);
    if (amount === null || moveFrom === moveTo || amount <= 0) return;
    const source = moveCategories.find(category => category.id === moveFrom);
    if (!source || amount > source.available) {
      setToast('Choose an amount available to move');
      window.setTimeout(() => setToast(''), 2400);
      return;
    }
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === moveFrom ? { ...category, assigned: category.assigned - amount } : category.id === moveTo ? { ...category, assigned: category.assigned + amount } : category) })) }; });
    setMoveOpen(false);
    setToast(`${money(amount)} moved`);
    window.setTimeout(() => setToast(''), 2400);
  }

  function undo() {
    const previous = history.at(-1);
    if (!previous) return;
    setFuture(stack => [...stack, data]); setHistory(stack => stack.slice(0, -1)); setData(previous);
  }

  function redo() {
    const next = future.at(-1);
    if (!next) return;
    setHistory(stack => [...stack, data]); setFuture(stack => stack.slice(0, -1)); setData(next);
  }

  function addGroup() {
    const name = window.prompt('Category group name')?.trim();
    if (!name) return;
    const id = `group-${crypto.randomUUID()}`;
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: [...current.groups, { id, name, categories: [], isArchived: false }] }; });
  }

  function toggleCategoryArchived(id: string) {
    setData(current => ({ ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === id ? { ...category, isArchived: !category.isArchived } : category) })) }));
  }

  function toggleGroupArchived(id: string) {
    setData(current => ({ ...current, groups: current.groups.map(group => group.id === id ? { ...group, isArchived: !group.isArchived } : group) }));
  }

  function moveGroup(id: string, direction: -1 | 1) {
    setData(current => { const from = current.groups.findIndex(group => group.id === id); const to = from + direction; if (from < 0 || to < 0 || to >= current.groups.length) return current; setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: moveItem(current.groups, from, to) }; });
  }

  function moveCategory(groupId: string, id: string, direction: -1 | 1) {
    setData(current => { const group = current.groups.find(item => item.id === groupId); if (!group) return current; const from = group.categories.findIndex(category => category.id === id); const to = from + direction; const next = moveItem(group.categories, from, to); if (next === group.categories) return current; setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(item => item.id === groupId ? { ...item, categories: next } : item) }; });
  }

  function reorderGroup(sourceId: string, targetId: string) {
    setData(current => { const from = current.groups.findIndex(group => group.id === sourceId); const to = current.groups.findIndex(group => group.id === targetId); const next = moveItem(current.groups, from, to); if (next === current.groups) return current; setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: next }; });
  }

  function dropCategory(sourceId: string, targetGroupId: string, targetCategoryId?: string) {
    setData(current => {
      const sourceGroup = current.groups.find(group => group.categories.some(category => category.id === sourceId));
      const destinationGroup = current.groups.find(group => group.id === targetGroupId);
      const source = sourceGroup?.categories.find(category => category.id === sourceId);
      if (!sourceGroup || !destinationGroup || !source || sourceGroup.isArchived || destinationGroup.isArchived || source.isArchived || sourceId === targetCategoryId) return current;
      const groups = current.groups.map(group => group.id === sourceGroup.id ? { ...group, categories: group.categories.filter(category => category.id !== sourceId) } : group);
      const destinationIndex = groups.findIndex(group => group.id === targetGroupId);
      const destination = groups[destinationIndex];
      const categories = [...destination.categories];
      const targetIndex = targetCategoryId ? categories.findIndex(category => category.id === targetCategoryId) : -1;
      categories.splice(targetIndex < 0 ? categories.length : targetIndex, 0, source);
      groups[destinationIndex] = { ...destination, categories };
      setHistory(stack => [...stack.slice(-49), current]); setFuture([]);
      return { ...current, groups };
    });
  }

  function dropGroup(sourceId: string, targetGroupId: string) {
    if (dragSource?.kind === 'group') reorderGroup(sourceId, targetGroupId);
    else if (dragSource?.kind === 'category') dropCategory(sourceId, targetGroupId);
    setDragSource(null); setDropTargetId(null);
  }

  async function openActivity(category: Category) {
    const requestId = ++dialogRequestId.current;
    setCategoryDialog({ title: `Activity · ${category.name}`, categoryId: category.id, kind: 'activity', items: [], loading: true, error: '', requestId });
    const { from, to } = monthBounds(data.month);
    try {
      const loadPage = (page: number) => {
        const query = new URLSearchParams({ categoryId: category.id, from, to, page: String(page), pageSize: '100', includeReconciled: 'true' });
        return requestJson<Register>(`/api/transactions?${query}`);
      };
      const first = await loadPage(1);
      const entries = [...first.items];
      for (let page = 2; (page - 1) * first.pageSize < first.total; page++) entries.push(...(await loadPage(page)).items);
      setCategoryDialog(current => current?.requestId === requestId ? { ...current, items: entries.map(entry => ({ entry, scheduled: false })), loading: false } : current);
    } catch (error) {
      setCategoryDialog(current => current?.requestId === requestId ? { ...current, loading: false, error: error instanceof Error ? error.message : 'Could not load transactions.' } : current);
    }
  }

  function openUpcoming(category: Category) {
    const items = upcomingItems.flatMap(item => {
      const amount = categoryTransactionAmount(item.transaction, category.id);
      return amount < 0 ? [{ entry: item.transaction, scheduled: true, categoryAmount: amount }] : [];
    });
    const requestId = ++dialogRequestId.current;
    setCategoryDialog({ title: `Upcoming activity · ${category.name}`, categoryId: category.id, kind: 'upcoming', items, loading: false, error: upcomingError, requestId });
  }

  function closeCategoryDialog() { dialogRequestId.current++; setCategoryDialog(null); }

  function showTransactionInAccount(entry: Entry, scheduled: boolean, source: 'Plan' | 'Long-Term Planning' = 'Plan') {
    closeCategoryDialog();
    navigate('Accounts', entry.accountId, { id: entry.id, accountId: entry.accountId, date: entry.date, scheduled, projected: scheduled ? entry : undefined, source });
  }

  function addFunds() {
    navigate('Accounts');
    setToast('Record income as a positive cash account transaction without a category.');
  }

  async function renamePlan() {
    const name = window.prompt('Budget name', plan.name)?.trim();
    if (!name || name === plan.name) return;
    try {
      const response = await fetch('/api/budget', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, version: plan.version }) });
      const result = await response.json();
      if (response.status === 409) {
        setPlan(result.current as BudgetPlan);
        setToast('Budget name changed in another session. Please try again.');
        return;
      }
      if (!response.ok) throw new Error(result.error ?? 'Budget name could not be saved.');
      setPlan(result as BudgetPlan);
      setToast('Budget name saved');
    } catch (error) {
      setToast(error instanceof Error ? error.message : 'Budget name could not be saved.');
    }
  }

  function resolveConflict(reapply: boolean) {
    if (!conflictBudget) return;
    const latest = conflictBudget;
    lastSaved.current = JSON.stringify(latest);
    if (reapply) setData(current => ({ ...current, version: latest.version }));
    else { setData(latest); setHistory([]); setFuture([]); }
    setConflictBudget(null);
    setSaveState('saved');
    setToast(reapply ? 'Your changes will be reapplied to the latest version.' : 'Loaded the latest saved plan.');
  }

  function matchesFilter(category: Category) {
    const textMatches = category.name.toLowerCase().includes(query.toLowerCase());
    if (!textMatches) return false;
    switch (filter) {
      case 'Overspent': return category.available < 0;
      case 'Underfunded': return category.target > category.assigned && category.available >= 0 && !category.snoozed;
      case 'Overfunded': return category.target > 0 && category.available > category.target;
      case 'Money available': return category.available > 0;
      case 'Snoozed': return Boolean(category.snoozed);
      case 'Archived': return Boolean(category.isArchived);
      default: return true;
    }
  }

  const budgetPage = (
    <>
      <div className="month-toolbar">
        <div className="month-controls">
          <button className="icon-button month-arrow" aria-label="Previous month" onClick={() => changeMonth(-1)}><Icon name="left" /></button>
          <div className="month-date"><button className="month-select" onClick={() => setToast('Use the arrows to browse months')}>
            <span>{shortMonthText(data.month)}</span><Icon name="chevron" size={15} />
          </button><input className="month-note" aria-label="Month note" placeholder="Enter a note..." value={data.note ?? ''} onChange={event => setData(current => ({ ...current, note: event.target.value }))} /></div>
          <button className="icon-button month-arrow" aria-label="Next month" onClick={() => changeMonth(1)}><Icon name="right" /></button>
        </div>
        <div className="toolbar-actions"><button className="shared-button" onClick={() => setToast('Sharing is not connected in this local demo')}><Icon name="users" size={18} /> Shared</button><button className="icon-button mobile-menu-trigger" aria-label="Open menu" onClick={() => setMobileMenu(value => !value)}><Icon name="menu" /></button></div>
      </div>

      <section className={`ready-card ${data.readyToAssign === 0 ? 'all-money-assigned' : data.readyToAssign < 0 ? 'assigned-over' : 'funds-ready'}`} aria-label="Ready to assign">
        <div className="ready-copy">
          <strong>{money(data.readyToAssign)}</strong>
          <span className="ready-hint">{data.readyToAssign < 0 ? 'Assigned more than available' : data.readyToAssign > 0 ? 'Ready to assign' : 'All Money Assigned'}</span>
        </div>
        {data.readyToAssign === 0
          ? <span className="all-assigned-check" aria-hidden="true"><Icon name="check" size={26}/></span>
          : <button className="ready-link" onClick={addFunds}>Record income</button>}
      </section>

      <div className="filter-bar">
        <div className="filter-scroll" role="tablist" aria-label="Category filters">
          {filters.map(item => <button key={item} className={`filter-chip ${filter === item ? 'selected' : ''} ${item === 'Overspent' && overspentCount > 0 ? 'has-overspent' : ''}`} role="tab" aria-selected={filter === item} onClick={() => setFilter(item)}>
            {item === 'Overspent' && <><span aria-hidden="true">⚠</span><span>{overspentCount}</span></>}{item === 'Money available' ? 'Money Available' : item}
          </button>)}
        </div>
        <button className="icon-button filter-settings" aria-label="Category view options" onClick={() => setToast('View options')}><Icon name="views" size={17} /></button>
      </div>

      {showSearch && <div className="search-row"><Icon name="search" size={17} /><input autoFocus value={query} onChange={event => setQuery(event.target.value)} placeholder="Find a category" aria-label="Find a category" /><button className="icon-button" aria-label="Close search" onClick={() => { setShowSearch(false); setQuery(''); }}><Icon name="close" size={16} /></button></div>}

      <div className="budget-layout">
        <section className="budget-table-wrap" aria-label="Monthly budget categories">
          <div className="table-tools">
            <button className="table-tool" onClick={addGroup}><span className="circle-plus">+</span> Category Group</button>
            <div className="table-tool-actions">
              <button className={`table-tool ${history.length ? '' : 'disabled'}`} onClick={undo} disabled={!history.length}><span className="undo-icon">↶</span> Undo</button>
              <button className={`table-tool ${future.length ? '' : 'disabled'}`} onClick={redo} disabled={!future.length}><span className="undo-icon">↷</span> Redo</button>
              <button className="table-tool recent-button" onClick={() => setToast('Recent moves will appear here')}><span className="recent-dot">◴</span> Recent Moves</button>
            </div>
            <div className="progress-toggle" aria-label="Progress bars setting"><button title="Progress Bars On" aria-label="Progress Bars On" className={progressOn ? 'active' : ''} onClick={() => setProgressOn(true)}><Icon name="bars" size={16} /></button><button title="Progress Bars Off" aria-label="Progress Bars Off" className={!progressOn ? 'active' : ''} onClick={() => setProgressOn(false)}><Icon name="lines" size={16} /></button></div>
          </div>

          <div className="budget-table">
            <div className="table-head">
              <span className="head-category"><button className="collapse-all" aria-label="Collapse all" onClick={() => setCollapsed(collapsed.length === data.groups.length ? [] : data.groups.map(group => group.id))}><Icon name="chevron" size={12} /></button><input type="checkbox" aria-label="Check all" checked={selectedIds.length === allCategories.length && allCategories.length > 0} onChange={event => setSelectedIds(event.target.checked ? allCategories.map(category => category.id) : [])} /><span>CATEGORY</span></span>
              <span className="head-assigned">ASSIGNED</span>
              <span className="head-activity">ACTIVITY</span>
              <span className="head-available">AVAILABLE</span>
            </div>
            {data.groups.map(group => {
              const groupTotal = group.categories.reduce((sum, category) => sum + category.available, 0);
              const groupVisible = filter === 'Archived' ? Boolean(group.isArchived || group.categories.some(category => category.isArchived)) : !group.isArchived;
              const shownCategories = group.categories.filter(category => filter === 'Archived' ? Boolean(group.isArchived || category.isArchived) : !category.isArchived && matchesFilter(category));
              if (!groupVisible || (filter !== 'All' && shownCategories.length === 0 && !group.isArchived)) return null;
              const isCollapsed = collapsed.includes(group.id);
              return <div className={`category-group ${dropTargetId === group.id ? 'drop-target' : ''}`} key={group.id} onDragOver={event => { if (dragSource) { event.preventDefault(); setDropTargetId(group.id); } }} onDrop={event => { event.preventDefault(); if (dragSource) dropGroup(dragSource.id, group.id); }}>
                <div className="group-row">
                  <div className="group-name"><button className="group-collapse" aria-label={`Toggle ${group.name}`} aria-expanded={!isCollapsed} onClick={() => toggleGroup(group.id)}><span className={`group-chevron ${isCollapsed ? 'closed' : ''}`}><Icon name="chevron" size={12} /></span></button><input type="checkbox" aria-label={`Select ${group.name}`} checked={group.categories.length > 0 && group.categories.every(category => selectedIds.includes(category.id))} onChange={event => setSelectedIds(current => event.target.checked ? [...new Set([...current, ...group.categories.map(category => category.id)])] : current.filter(id => !group.categories.some(category => category.id === id)))} /><button className="group-label" draggable={!group.isArchived} title="Drag to reorder this group" onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', group.id); setDragSource({ kind: 'group', id: group.id }); }} onDragEnd={() => { setDragSource(null); setDropTargetId(null); }} onDoubleClick={() => { const name = window.prompt('Rename category group', group.name)?.trim(); if(name) setData(current => ({...current,groups:current.groups.map(item => item.id === group.id ? {...item,name} : item)})); }}>{group.name}</button>{!group.isArchived && <button className="group-add" onClick={() => addCategory(group.id)} aria-label={`Add category to ${group.name}`}><Icon name="plus" size={16} /></button>}<div className="group-order-tools"><button aria-label={`Move ${group.name} up`} onClick={() => moveGroup(group.id, -1)}>↑</button><button aria-label={`Move ${group.name} down`} onClick={() => moveGroup(group.id, 1)}>↓</button></div></div>
                  <div className="group-assigned">{money(group.categories.reduce((sum, item) => sum + item.assigned, 0))}</div>
                  <div className="group-activity">{money(group.categories.reduce((sum, item) => sum + item.activity, 0))}</div>
                  <div className={`group-available ${groupTotal < 0 ? 'negative' : ''}`}>{money(groupTotal)}</div>
                  {addCategoryGroupId === group.id && <form className="category-add-popover" onSubmit={submitCategory}><label className="sr-only" htmlFor={`new-category-${group.id}`}>New category</label><input id={`new-category-${group.id}`} autoFocus maxLength={160} value={newCategoryName} placeholder="New Category" onChange={event => { setNewCategoryName(event.target.value); setCategoryNameError(''); }} onKeyDown={event => { if (event.key === 'Escape') setAddCategoryGroupId(null); }} />{categoryNameError && <span role="alert">{categoryNameError}</span>}<div><button type="button" onClick={() => setAddCategoryGroupId(null)}>Cancel</button><button className="button button-primary" type="submit">OK</button></div></form>}<button className="group-archive" aria-label={`${group.isArchived ? 'Restore' : 'Archive'} group ${group.name}`} onClick={() => toggleGroupArchived(group.id)}>{group.isArchived ? 'Restore' : 'Archive'}</button>
                </div>
                {!isCollapsed && shownCategories.map(category => {
                  const progress = categoryProgress(category.assigned, category.activity, category.available);
                  const categoryUpcoming = upcomingItems.flatMap(item => { const amount = categoryTransactionAmount(item.transaction, category.id); return amount < 0 ? [{ item, amount }] : []; });
                  const upcomingTotal = categoryUpcoming.reduce((sum, item) => sum + item.amount, 0);
                  const hasUpcomingTransactions = categoryUpcoming.length > 0;
                  const underfundedUpcoming = hasUpcomingTransactions && category.available + upcomingTotal < 0;
                  return <div className={`category-row ${category.available < 0 ? 'is-overspent' : ''} ${selectedCategoryId === category.id ? 'category-selected' : ''}`} key={category.id} draggable={!category.isArchived && !group.isArchived} onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', category.id); setDragSource({ kind: 'category', id: category.id }); }} onDragEnd={() => { setDragSource(null); setDropTargetId(null); }} onDragOver={event => { if (dragSource?.kind === 'category') { event.preventDefault(); setDropTargetId(category.id); } }} onDrop={event => { event.preventDefault(); event.stopPropagation(); if (dragSource?.kind === 'category') dropCategory(dragSource.id, group.id, category.id); setDragSource(null); setDropTargetId(null); }}>
                    <div className="category-name-cell"><input type="checkbox" aria-label={`Select ${category.name}`} checked={selectedIds.includes(category.id)} onChange={event => setSelectedIds(current => event.target.checked ? [...current, category.id] : current.filter(id => id !== category.id))} /><div className="category-copy"><div className="category-label-line"><button className="category-name" title="Click to see category details; double-click to rename" disabled={category.isArchived || group.isArchived} onClick={() => setSelectedCategoryId(category.id)} onDoubleClick={() => { const name = window.prompt('Rename category', category.name)?.trim(); if(name) updateCategory(category.id, item => ({...item,name})); }}>{category.name}</button>{progressOn && <span className="category-status">{category.available < 0 ? 'Overspent' : category.snoozed ? 'Snoozed' : category.available === 0 && category.activity < 0 ? 'Fully Spent' : ''}</span>}</div>{progressOn && (category.assigned !== 0 || category.available !== 0) && <div className={`progress-track ${category.available < 0 ? 'over' : ''}`} aria-label={`Category progress: ${Math.round(progress.green)}% available${progress.red ? `, ${Math.round(progress.red)}% overspent` : ''}`}><span className="progress-green" style={{ width: `${progress.green}%` }} /><span className="progress-red" style={{ width: `${progress.red}%` }} /></div>}{underfundedUpcoming && <button className="category-upcoming" onClick={() => openUpcoming(category)} aria-label={`${categoryUpcoming.length} upcoming transactions for ${category.name}, ${money(upcomingTotal)}`}>{categoryUpcoming.length} Upcoming {categoryUpcoming.length === 1 ? 'Transaction' : 'Transactions'} <span>{money(upcomingTotal)}</span></button>}</div><div className="category-order-tools"><button aria-label={`Move ${category.name} up`} disabled={category.isArchived || group.isArchived} onClick={() => moveCategory(group.id, category.id, -1)}>↑</button><button aria-label={`Move ${category.name} down`} disabled={category.isArchived || group.isArchived} onClick={() => moveCategory(group.id, category.id, 1)}>↓</button></div><button className="category-archive" disabled={group.isArchived} aria-label={`${category.isArchived ? 'Restore' : 'Archive'} category ${category.name}`} onClick={() => toggleCategoryArchived(category.id)}>{category.isArchived ? 'Restore' : 'Archive'}</button></div>
                    <div className="assigned-cell">{editingId===category.id&&assignmentError&&<span role="alert" className="assignment-error">{assignmentError}</span>}
                      {editingId === category.id ? <input className="amount-input" autoFocus value={editValue} aria-invalid={Boolean(assignmentError)} onFocus={event => event.currentTarget.select()} onChange={event => {setEditValue(event.target.value);setAssignmentError('');}} onBlur={() => saveEdit(category)} onKeyDown={event => { if (event.key === 'Enter') saveEdit(category); if (event.key === 'Escape') {setEditingId(null);setAssignmentError('');} }} aria-label={`Assigned to ${category.name}`} /> : <button className="amount-button" disabled={category.isArchived || group.isArchived || saveState==='loading' || loadedMonth.current!==data.month || !!conflictBudget} onClick={() => startEdit(category)} aria-label={`Edit assigned for ${category.name}`}>{money(category.assigned)}</button>}

                    </div>
                    <div className={`activity-cell ${category.activity < 0 ? 'negative' : ''}`}><button className="activity-button" aria-label={`View transactions for ${category.name} activity`} onClick={() => void openActivity(category)}>{money(category.activity)}</button></div>
                    <div className={`available-cell ${category.available < 0 ? 'negative' : hasUpcomingTransactions ? 'underfunded' : category.available > 0 ? 'positive' : ''}`}><span>{money(category.available)}</span></div>
                  </div>;
                })}
                {!isCollapsed && shownCategories.length === 0 && <div className="empty-group">No categories match this filter.</div>}
              </div>;
            })}
            {filter !== 'All' && matchingCategoryCount === 0 && <div className="empty-state"><div className="empty-state-icon"><Icon name="check" /></div><strong>All clear</strong><span>There are no categories in this view.</span><button onClick={() => setFilter('All')}>Show all categories</button></div>}
            <button className="add-group" onClick={addGroup}><span><Icon name="plus" size={16} /></span> Add category group</button>
          </div>
        </section>

        <aside className="summary-column">
          {selectedCategory && <section className="category-detail-card"><header><strong>{selectedCategory.name}</strong><button type="button" aria-label="Close category details" onClick={() => setSelectedCategoryId(null)}>×</button></header><div className="summary-body"><div className="summary-line"><span>Available Balance</span><strong>{money(selectedCategory.available)}</strong></div><div className="summary-line"><span>Assigned This Month</span><strong>{money(selectedCategory.assigned)}</strong></div><div className="summary-line"><span>Activity</span><strong>{money(selectedCategory.activity)}</strong></div>{upcomingLoading ? <p role="status">Loading upcoming transactions…</p> : upcomingError ? <div role="alert">{upcomingError} <button onClick={() => setBudgetRefresh(value => value + 1)}>Retry</button></div> : selectedUpcoming.length > 0 && <><button className="category-upcoming-detail" onClick={() => openUpcoming(selectedCategory)}><span>{selectedUpcoming.length} Upcoming {selectedUpcoming.length === 1 ? 'Transaction' : 'Transactions'}</span><strong>{money(selectedUpcomingTotal)}</strong><span>Available After Upcoming</span><strong>{money(selectedCategory.available + selectedUpcomingTotal)}</strong></button>{selectedCategory.available + selectedUpcomingTotal < 0 && <span className="category-underfunded-hint">This category does not have enough available to cover these transactions.</span>}</>}</div></section>}
          <section className="summary-card"><button className="summary-title" onClick={() => setToast('Monthly summary')}><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}'s Summary</span><Icon name="chevron" size={15} /></button><div className="summary-body">
            <div className="summary-line"><span>Ready to Assign</span><strong>{money(data.readyToAssign)}</strong></div><div className="summary-line"><span>Assigned in {new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}</span><strong>{money(totals.assigned)}</strong></div><div className="summary-line"><span>Activity</span><strong>{money(totals.activity)}</strong></div><div className="summary-line summary-total"><span>Available</span><strong>{money(totals.available)}</strong></div>
            <div className="summary-rule" /><div className="summary-subhead">Cost to Be Me</div><div className="summary-line"><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}’s Targets</span><strong>{money(targetSum)}</strong></div><div className="target-progress"><span style={{width:'14%'}} /></div><div className="summary-line"><span>Expected Income</span><strong>{money(data.ledgerIncome)} <button className="edit-income" aria-label="Edit income" onClick={() => setToast('Income settings')}>↗</button></strong></div><div className="income-progress"/><div className="summary-footnote">Next month's targets could increase your total to {money(targetSum)}</div></div></section>
          <button className="auto-assign-card" onClick={() => setToast('Auto-Assign options')}><span>ϟ</span><strong>Auto-Assign</strong><Icon name="right" size={15} /></button>
          <section className="future-card"><div className="future-header"><strong>Assigned in Future Months</strong><Icon name="chevron" size={15} /></div><button onClick={() => changeMonth(1)}><span className="future-month">◷ &nbsp; {new Date(Number(data.month.slice(0,4)),Number(data.month.slice(5)),1).toLocaleDateString('en-GB',{month:'long'})}</span><span>{money(0)}</span></button></section>
          <section className="for-you-card"><strong>For You <Icon name="chevron" size={15} /></strong><h3>You’re all caught up!</h3><p>Want to keep going? There’s plenty more where that came from.</p><button onClick={() => setToast('This local demo has no connected blog')}>Read the Blog</button></section>
                </aside>
      </div>
    </>
  );

  const otherPages: Record<string, { title: string; subtitle: string; icon: string }> = {
    Reflect: { title: 'Reflect on your spending', subtitle: 'Take a clear look at where your money went this month.', icon: 'chart' },
    Accounts: { title: 'Your accounts', subtitle: 'Keep balances and transactions together in one place.', icon: 'wallet' },
  };

  return <div className="app-shell">
    {mobileMenu && <button className="sidebar-scrim" aria-label="Close navigation" onClick={() => setMobileMenu(false)} />}
    <aside className={`sidebar ${mobileMenu ? 'mobile-open' : ''}`}>
      <button className="budget-picker" onClick={renamePlan}><img src="/openbudget-logo.svg" alt="" /><span className="budget-picker-copy"><strong>{plan.name}</strong><small>{plan.currency} · Rename budget</small></span><Icon name="chevron" size={15} /></button>
      <div className="side-nav" role="navigation" aria-label="Main navigation">
        <button className={`nav-item ${activePage === 'Plan' ? 'active' : ''}`} onClick={() => navigate('Plan')}><Icon name="plan" size={22} /><span>Plan</span></button>
        <button className={`nav-item ${activePage === 'Long-Term Planning' ? 'active' : ''}`} onClick={() => navigate('Long-Term Planning')}><Icon name="calendar" size={22} /><span>Long-Term Planning</span></button>
        <button className={`nav-item ${activePage === 'Reflect' ? 'active' : ''}`} onClick={() => setToast('Reflect is outside this demo’s scope')}><Icon name="chart" /><span>Reflect</span></button>
        <button className={`nav-item ${activePage === 'Accounts' && !selectedAccount ? 'active' : ''}`} onClick={() => navigate('Accounts')}><Icon name="bank" size={22} /><span>All Accounts</span></button>
      </div>
      <div className="account-section">
        
        <div className="account-type"><span>CASH</span><strong>{money(accounts.accounts.reduce((sum, account) => sum + account.balance, 0))}</strong></div>
        {accounts.accounts.map(account => <AccountSidebarRow key={account.id} account={account} selected={activePage==='Accounts'&&selectedAccount===account.id} disabled={ledgerPending} onSelect={()=>navigate('Accounts',account.id)} onEdit={()=>{if(!ledgerPending){navigate('Accounts',account.id);setAccountEditRequest({id:account.id,token:Date.now()});}}}/>)}
        {accountError ? <button className="account-row" onClick={() => setLedgerRefresh(value => value + 1)}>Retry loading accounts</button> : !accounts.accounts.length && <button className="account-row" onClick={() => navigate('Accounts')}>Add your first cash account</button>}
      </div>
      <div className="sidebar-bottom"><div className="referral-banner"><span>Give your friends 6 free weeks to try YNAB!</span><img src="/referral-heart.svg" alt="" /><button aria-label="Dismiss referral" onClick={event => {event.currentTarget.parentElement?.classList.add('dismissed');}}>×</button></div><div className="sidebar-footer"><button onClick={() => setToast('Sharing is not connected in this demo')}>▦ &nbsp; Share & Earn</button><button aria-label="Collapse sidebar" onClick={() => setMobileMenu(value => !value)}>◧</button></div></div>
    </aside>

    <main className="main-area">
      <header className="topbar"><div className="breadcrumbs"><span>{plan.name}</span><Icon name="right" size={13} /><strong>{activePage}</strong></div><div className="topbar-right"><button className="share-button" onClick={() => setToast('Sharing is not connected in this local demo')}>Share</button><button className="top-avatar" aria-label="Profile">A</button></div></header>
      {activePage === 'Accounts' ? <AccountsPage editAccountRequest={accountEditRequest} focusTransaction={focusTransaction} onPendingChange={setLedgerPending} selectedAccount={selectedAccount} onSelectAccount={setSelectedAccount} onChanged={() => setLedgerRefresh(value => value + 1)} onMenu={() => setMobileMenu(true)} /> : activePage === 'Plan' ? <div className="page-content"><div className="page-heading"><div><span className="eyebrow">YOUR MONEY, WITH A PLAN</span><h1>Plan</h1></div></div>{budgetPage}<div className="ledger-plan-status" role="status">{saveState === 'saved' ? 'Plan saved' : saveState === 'saving' ? 'Saving plan…' : saveState === 'loading' ? 'Loading plan…' : saveState === 'conflict' ? 'Resolve the conflicting plan edits' : 'Plan changes could not be saved'} {saveState === 'offline' && <button onClick={() => setBudgetRefresh(value => value + 1)}>Reload saved plan</button>}</div></div> : activePage === 'Long-Term Planning' ? <div className="page-content"><LongTermPlanning accounts={accounts.accounts} onSelectTransaction={(entry, scheduled) => showTransactionInAccount(entry, scheduled, 'Long-Term Planning')} onPendingChange={setPlanningPending} /></div> : <div className="page-content other-page"><div className="page-heading"><div><span className="eyebrow">MY BUDGET</span><h1>{activePage}</h1></div></div><section className="coming-card"><div className="coming-icon"><Icon name={otherPages[activePage].icon} size={24} /></div><h2>{otherPages[activePage].title}</h2><p>{otherPages[activePage].subtitle}</p><div className="coming-preview"><span /><span /><span /></div><button className="button button-primary" onClick={() => setActivePage('Plan')}>Back to your plan <Icon name="right" size={15} /></button><small>This view is queued for the next build slice.</small></section></div>}
      <footer className="footer-note"><span>{saveState === 'loading' ? 'Loading budget…' : saveState === 'saving' ? 'Saving changes…' : saveState === 'conflict' ? 'Budget changed elsewhere' : saveState === 'offline' ? 'Changes not saved' : 'Changes saved to PostgreSQL'}</span><span>OpenBudget <span className="footer-separator">·</span> Local workspace</span></footer>
    </main>

    <button className="help-bubble" aria-label="Help" onClick={() => setToast('Local demo help: edit Assigned values, move money with Fix This, or add a Category Group.')}>?</button>
    {conflictBudget && <div className="modal-backdrop" role="presentation"><section className="move-modal conflict-modal" role="dialog" aria-modal="true" aria-labelledby="conflict-title"><span className="eyebrow">BUDGET UPDATED ELSEWHERE</span><h2 id="conflict-title">Choose which plan to keep</h2><p className="modal-copy">Another session saved this month while you were editing. Load that version or reapply your edits over it.</p><div className="conflict-actions"><button className="button" onClick={() => resolveConflict(false)}>Load latest</button><button className="button button-primary" onClick={() => resolveConflict(true)}>Reapply my changes</button></div></section></div>}
    {moveOpen && <div className="modal-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) setMoveOpen(false); }}><section className="move-modal" role="dialog" aria-modal="true" aria-labelledby="move-title"><div className="modal-heading"><div className="modal-icon"><Icon name="transfer" /></div><button className="icon-button" aria-label="Close move money dialog" onClick={() => setMoveOpen(false)}><Icon name="close" /></button></div><span className="eyebrow">GIVE YOUR MONEY A NEW JOB</span><h2 id="move-title">Move money</h2><p className="modal-copy">Shift money between categories. Your total plan stays the same.</p><form onSubmit={applyMove}><label>Move from<select value={moveFrom} onChange={event => setMoveFrom(event.target.value)}>{moveCategories.map(category => <option key={category.id} value={category.id}>{category.name} · {money(category.available)}</option>)}</select></label><div className="modal-arrow"><Icon name="arrow" /></div><label>Move to<select value={moveTo} onChange={event => setMoveTo(event.target.value)}>{moveCategories.map(category => <option key={category.id} value={category.id}>{category.name} · {money(category.available)}</option>)}</select></label><label className="amount-label">Amount<div className="modal-amount"><span>€</span><input autoFocus inputMode="decimal" value={moveAmount} onChange={event => setMoveAmount(event.target.value)} aria-label="Amount to move" /></div></label><button className="button button-primary modal-submit" type="submit">Move {money(parseAssignment(moveAmount)??0)}</button></form></section></div>}
    {categoryDialog && <PlanTransactionList title={categoryDialog.title} items={categoryDialog.items} accounts={accounts.accounts} loading={categoryDialog.loading} error={categoryDialog.error} onClose={closeCategoryDialog} onRetry={categoryDialog.kind === 'activity' ? () => { const category = visibleCategories.find(item => item.id === categoryDialog.categoryId); if (category) void openActivity(category); } : undefined} onSelect={showTransactionInAccount} />}
    {toast && <div className="toast" role="status"><span className="toast-check"><Icon name="check" size={14} /></span>{toast}<button aria-label="Dismiss notification" onClick={() => setToast('')}><Icon name="close" size={14} /></button></div>}
  </div>;
}

export default function App() { return import.meta.env.MODE === 'test' ? <BudgetApplication /> : <AuthGate><BudgetApplication /></AuthGate>; }
