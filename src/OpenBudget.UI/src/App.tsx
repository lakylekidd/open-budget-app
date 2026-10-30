import { Fragment, useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { MoneyInput } from './MoneyInput';
import { AccountSidebarRow } from './ledger/AccountSidebarRow';
import { AccountsPage } from './ledger/AccountsPage';
import { type Accounts, type Entry, type Register, type ScheduledItem, type Schedules, accountFamily, creditPaymentCategoryId, creditPaymentGroupId, isCreditAccount, requestJson } from './ledger/api';
import { CreditPaymentDetails } from './CreditPaymentDetails';
import { CategoryTargetCard, TargetRingIcon } from './CategoryTarget';
import { isTargetUnderfunded, targetSegments, type CategoryTarget, type CategoryTargetResult, type TargetProgress } from './categoryTargets';

// CASH always shows; CREDIT and LOANS appear only when such accounts exist.
const accountSections = [{ id: 'cash', title: 'CASH' }, { id: 'credit', title: 'CREDIT' }, { id: 'loan', title: 'LOANS' }] as const;
import { parseAssignment } from './budgetMoney';
import CategoryNamePopover from './CategoryNamePopover';

function rememberActiveBudget(id: string | null) {
  if (id) {
    window.localStorage.setItem('openbudget.activeBudgetId', id);
    document.cookie = `openbudget.activeBudgetId=${encodeURIComponent(id)}; Path=/; SameSite=Strict${window.location.protocol === 'https:' ? '; Secure' : ''}`;
  } else {
    window.localStorage.removeItem('openbudget.activeBudgetId');
    document.cookie = `openbudget.activeBudgetId=; Path=/; Max-Age=0; SameSite=Strict${window.location.protocol === 'https:' ? '; Secure' : ''}`;
  }
}
import { categoryProgress, categoryTransactionAmount, moveItem } from './planHelpers';
import { expandActivityEntries, PlanTransactionList, type PlanTransactionItem } from './PlanTransactionList';
import { LongTermPlanning } from './longTermPlanning/LongTermPlanning';
import { AuthGate } from './AuthGate';
import { AdminPanel } from './admin/AdminPanel';
import { ProfileSettings } from './ProfileSettings';
import { CategoryPicker } from './ledger/CategoryPicker';
import { AutoAssignOptions } from './AutoAssignOptions';
import { useBudgetLiveUpdates } from './liveUpdates';
import './agentTokens.css';
import { BudgetTogether } from './BudgetTogether';
import { RecentMoves } from './RecentMoves';
import { buildAutoAssignOptions, previousBudgetMonths, type AssignmentHistory, type AutoAssignOption } from './planAutoAssign';
import type { LongTermPlanning as LongTermPlanningData } from './longTermPlanning/longTermPlanningMath';
import { WishlistPage } from './wishlist/WishlistPage';
import { useWishlistAccess, WishlistCategoryCard, WishlistNavItem, WishlistRowStatus } from './wishlist/WishlistPlanParts';
import { wishlistGroupId, type WishlistProgress } from './wishlist/wishlistApi';
import { budgetIdFromLocation, initialPageFromLocation, isAdminPath, monthFromLocation, type RouteTarget } from './routing/routes';
import { useAppRouter } from './routing/useAppRouter';

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
  // Structured target and its server-calculated progress for this month; read-only in the browser.
  categoryTarget?: CategoryTarget | null;
  targetProgress?: TargetProgress | null;
  // Server-calculated progress toward the bucket's next Wishlist item (Wishlist categories only).
  wishlistProgress?: WishlistProgress | null;
};

type CategoryGroup = { id: string; name: string; categories: Category[]; isArchived?: boolean; sortOrder?: number };
type Filter = 'All' | 'Overspent' | 'Underfunded' | 'Overfunded' | 'Money available' | 'Snoozed' | 'Archived';
type BudgetState = { month: string; groups: CategoryGroup[]; note?: string; version: number; openingReadyToAssign: number; fundsAdded: number; ledgerIncome: number; readyToAssign: number };
type BudgetPlan = { id: string; name: string; currency: string; version: number };
type OwnedBudget = BudgetPlan & { isArchived: boolean; createdAt: string; accessRole?:'owner'|'editor' };

const filters: Filter[] = ['All', 'Overspent', 'Underfunded', 'Overfunded', 'Money available', 'Snoozed', 'Archived'];

const currency = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
const money = (value: number) => currency.format(value);
const shortMonthText = (value: string) => new Intl.DateTimeFormat('en-GB', { month: 'short', year: 'numeric' }).format(new Date(`${value}-01T12:00:00`));
const currentMonthKey = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`; };
const previousMonthKey = (value: string) => { const [year, month] = value.split('-').map(Number); const date = new Date(year, month - 2, 1); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`; };
const monthBounds = (month: string) => {
  const [year, monthNumber] = month.split('-').map(Number);
  const format = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  return { from: `${month}-01`, to: format(new Date(year, monthNumber, 0)) };
};

type PlanFocus = { id: string; accountId: string; date: string; scheduled: boolean; projected?: Entry; splitIndex?: number; source?: 'Plan' | 'Long-Term Planning' };
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
    warning: <><path d="M12 3 22 20H2Z" /><path d="M12 9v4" /><path d="M12 17h.01" /></>,
    spark: <><path d="m12 3 1.5 6.5L20 12l-6.5 1.5L12 20l-1.5-6.5L4 12l6.5-2.5L12 3Z" /><path d="m19 2 .6 2.4L22 5l-2.4.6L19 8l-.6-2.4L16 5l2.4-.6L19 2Z" /></>,
    transfer: <><path d="M5 8h14M15 4l4 4-4 4M19 16H5m4 4-4-4 4-4" /></>,
    assign: <><path d="M3 5h6M6 2v6M14 5h7M3 12l6 6M9 12l-6 6M14 15h7M3 22h6M14 22h7" /></>,
  };
  return <svg {...common}>{paths[name] || <circle cx="12" cy="12" r="8" />}</svg>;
}

function BudgetApplication() {
  const [userEmail, setUserEmail] = useState('');
  const [isAdministrator, setIsAdministrator] = useState(false);
  useEffect(() => {
    let active = true;
    void fetch('/api/auth/session', { credentials: 'same-origin' }).then(async response => {
      if (response.ok && active) {
        const session = await response.json() as { email: string | null; isAdministrator: boolean };
        setUserEmail(session.email ?? ''); setIsAdministrator(session.isAdministrator);
      }
    }).catch(() => undefined);
    return () => { active = false; };
  }, []);
  const [plan, setPlan] = useState<BudgetPlan>({ id: 'default', name: 'My Budget', currency: 'EUR', version: 1 });
  const [ownedBudgets, setOwnedBudgets] = useState<OwnedBudget[]>([]);
  const [selectedBudgetId, setSelectedBudgetId] = useState('');
  const [budgetsLoaded, setBudgetsLoaded] = useState(false);
  const [accounts, setAccounts] = useState<Accounts>({ version: 1, accounts: [] });
  const [accountsLoaded, setAccountsLoaded] = useState(false);
  const [accountsBudgetId, setAccountsBudgetId] = useState<string | null>(null);
  const [accountError, setAccountError] = useState(false);
  const [ledgerPending, setLedgerPending] = useState(false);
  const [selectedAccount, setSelectedAccount] = useState('');
  const [accountEditRequest,setAccountEditRequest]=useState<{id:string;token:number}>();
  const [accountCreateRequest,setAccountCreateRequest]=useState(0);
  const [ledgerRefresh, setLedgerRefresh] = useState(0);
  const [budgetRefresh, setBudgetRefresh] = useState(0);
  const [data, setData] = useState<BudgetState>({ month: '2026-10', groups: [], note: '', version: 1, openingReadyToAssign: 0, fundsAdded: 0, ledgerIncome: 0, readyToAssign: 0 });
  const [saveState, setSaveState] = useState<'loading' | 'saved' | 'saving' | 'offline' | 'conflict'>('loading');
  const [assignmentHistory, setAssignmentHistory] = useState<AssignmentHistory>({});
  const [assignmentHistoryLoading, setAssignmentHistoryLoading] = useState(true);
  const [assignmentHistoryError, setAssignmentHistoryError] = useState('');
  const [assignmentHistoryRetry, setAssignmentHistoryRetry] = useState(0);
  const [autoAssignExpanded, setAutoAssignExpanded] = useState(false);
  const [assignMenuOpen, setAssignMenuOpen] = useState(false);
  const [assignTab, setAssignTab] = useState<'auto' | 'manual'>('auto');
  const [manualAssignAmount, setManualAssignAmount] = useState('');
  const [manualAssignCategory, setManualAssignCategory] = useState('');
  const [manualAssignError, setManualAssignError] = useState('');
  const assignmentHistoryMonths = useMemo(() => previousBudgetMonths(data.month, 12), [data.month]);
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
  const [sidebarWidth, setSidebarWidth] = useState(() => {
    const saved = Number(window.localStorage.getItem('openbudget.sidebarWidth'));
    return Number.isFinite(saved) && saved >= 190 && saved <= 420 ? saved : 260;
  });
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => window.localStorage.getItem('openbudget.sidebarCollapsed') === 'true');
  const resizingSidebar = useRef(false);
  function changeSidebarWidth(width: number) {
    const next = Math.min(420, Math.max(190, Math.round(width)));
    setSidebarWidth(next);
    window.localStorage.setItem('openbudget.sidebarWidth', String(next));
  }
  function toggleSidebar() {
    if (window.innerWidth <= 700) { setMobileMenu(false); return; }
    setBudgetMenuOpen(false);
    setSidebarCollapsed(value => { window.localStorage.setItem('openbudget.sidebarCollapsed', String(!value)); return !value; });
  }
  const [activePage, setActivePage] = useState<string>(() => initialPageFromLocation());
  const [planningPending, setPlanningPending] = useState(false);
  const [toast, setToast] = useState('');
  const [recentMovesOpen,setRecentMovesOpen]=useState(false);
  const [recentMovesRefresh,setRecentMovesRefresh]=useState(0);
  const [profileOpen, setProfileOpen] = useState(false);
  const [budgetMenuOpen, setBudgetMenuOpen] = useState(false);
  const [collapsedAccountSections, setCollapsedAccountSections] = useState<string[]>([]);
  const [budgetMenuSection, setBudgetMenuSection] = useState<'main' | 'recent' | 'all'>('main');
  const closePlansTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  function cancelClosePlans() {
    if (closePlansTimer.current) clearTimeout(closePlansTimer.current);
    closePlansTimer.current = null;
  }
  function scheduleClosePlans() {
    cancelClosePlans();
    closePlansTimer.current = setTimeout(() => setBudgetMenuSection('main'), 120);
  }
  useEffect(() => () => { if (closePlansTimer.current) clearTimeout(closePlansTimer.current); }, []);
  const [showSearch, setShowSearch] = useState(false);
  const [query, setQuery] = useState('');
  const [moveFrom, setMoveFrom] = useState('coffee');
  const [moveTo, setMoveTo] = useState('clothing');
  const [moveAmount, setMoveAmount] = useState('25');
  const [moveCovering, setMoveCovering] = useState(false);
  const [progressOn, setProgressOn] = useState(true);
  const [unassignOpen, setUnassignOpen] = useState(false);
  const [unassignMonth, setUnassignMonth] = useState(data.month);
  const [unassignMonthDraft, setUnassignMonthDraft] = useState(data.month);
  const [unassignMonthMenuOpen, setUnassignMonthMenuOpen] = useState(false);
  const [unassignData, setUnassignData] = useState<BudgetState | null>(null);
  const [unassignLoading, setUnassignLoading] = useState(false);
  const [unassignSaving, setUnassignSaving] = useState(false);
  const [unassignError, setUnassignError] = useState('');
  const [unassignSelectedId, setUnassignSelectedId] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [history, setHistory] = useState<BudgetState[]>([]);
  const [future, setFuture] = useState<BudgetState[]>([]);
  const [upcomingItems, setUpcomingItems] = useState<ScheduledItem[]>([]);
  const [upcomingLoading, setUpcomingLoading] = useState(true);
  const [upcomingError, setUpcomingError] = useState('');
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  // A loan page's "Create Debt Payment Target" opens Plan with that category selected and its target editor open.
  const [targetEditRequest, setTargetEditRequest] = useState<string | null>(null);
  const [categoryDialog, setCategoryDialog] = useState<CategoryTransactionDialog | null>(null);
  const [focusTransaction, setFocusTransaction] = useState<PlanFocus | null>(null);
  const dialogRequestId = useRef(0);
  const [addCategoryGroupId, setAddCategoryGroupId] = useState<string | null>(null);
  const [newCategoryName, setNewCategoryName] = useState('');
  const [categoryNameError, setCategoryNameError] = useState('');
  const [nameEditor, setNameEditor] = useState<{ kind: 'group' | 'category'; id: string } | null>(null);
  const [nameDraft, setNameDraft] = useState('');
  const [nameEditorError, setNameEditorError] = useState('');
  const [dragSource, setDragSource] = useState<{ kind: 'group' | 'category'; id: string } | null>(null);
  const dragSourceRef = useRef<{ kind: 'group' | 'category'; id: string } | null>(null);
  const [dropTarget, setDropTarget] = useState<{ groupId: string; categoryId?: string; position: 'before' | 'after' } | null>(null);
  const [wishlistRefresh, setWishlistRefresh] = useState(0);
  const [wishlistAccess, setWishlistAccess] = useWishlistAccess(selectedBudgetId, `${budgetRefresh}:${data.month}:${data.version}:${wishlistRefresh}`);

  useEffect(() => {
    const controller = new AbortController();
    // Accounts follow the active-budget cookie, so reload them when the selected budget changes.
    const budgetId = selectedBudgetId;
    requestJson<Accounts>('/api/accounts', { signal: controller.signal }).then(value => { setAccounts(value); setAccountError(false); setAccountsLoaded(true); setAccountsBudgetId(budgetId); })
      .catch(error => { if (error.name !== 'AbortError') { setAccountError(true); setAccountsLoaded(true); setAccountsBudgetId(budgetId); } });
    return () => controller.abort();
  }, [ledgerRefresh, selectedBudgetId]);

  function canLeaveCurrentView() {
    if (ledgerPending) { setToast('Resolve the pending ledger save before leaving accounts.'); return false; }
    if (planningPending) { setToast('Resolve the pending planning edit before changing screens.'); return false; }
    if (editingId || saveState === 'saving' || conflictBudget || (loadedMonth.current === data.month && lastSaved.current !== JSON.stringify(data))) {
      setToast('Wait for the plan to save, or resolve its save error before opening accounts.'); return false;
    }
    return true;
  }

  // The address bar follows this state through useAppRouter (src/routing); navigate() only changes state.
  function navigate(page: string, accountId = '', transaction: PlanFocus | null = null) {
    if (!canLeaveCurrentView()) return false;
    setActivePage(page); setSelectedAccount(accountId); setFocusTransaction(transaction); setMobileMenu(false);
    if (page === 'Plan') setBudgetRefresh(value => value + 1);
    return true;
  }

  // Applies a URL (first load, browser back/forward). Back/forward obeys the same leave-guards as navigate().
  function applyRoute(target: RouteTarget, { initial }: { initial: boolean }) {
    if (!initial && !canLeaveCurrentView()) return false;
    if (target.budgetId !== selectedBudgetId) selectBudget(target.budgetId);
    setActivePage(target.page); setSelectedAccount(target.accountId); setFocusTransaction(null); setMobileMenu(false);
    if (target.month && target.month !== data.month) setData(current => ({ ...current, month: target.month! }));
    if (!initial && target.page === 'Plan' && activePage !== 'Plan') setBudgetRefresh(value => value + 1);
    return true;
  }

  useAppRouter({
    budgets: ownedBudgets, budgetsLoaded, selectedBudgetId, budgetName: plan.name,
    accounts: selectedBudgetId && accountsBudgetId === selectedBudgetId ? accounts.accounts : null,
    page: activePage, accountId: selectedAccount, month: data.month, applyRoute, notify: setToast,
  });

  useEffect(() => {
    const controller = new AbortController();
    fetch('/api/budgets?includeArchived=true', { signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(`Budget list load failed (${response.status})`);
        const result = await response.json() as { budgets: OwnedBudget[] };
        const available = result.budgets.filter(budget => !budget.isArchived);
        setOwnedBudgets(available);
        const remembered = budgetIdFromLocation(available) ?? window.localStorage.getItem('openbudget.activeBudgetId');
        const routeMonth = monthFromLocation();
        if (routeMonth) setData(current => ({ ...current, month: routeMonth }));
        const selected = available.find(budget => budget.id === remembered) ?? available[0];
        if (selected) {
          setSelectedBudgetId(selected.id);
          setPlan(selected);
          rememberActiveBudget(selected.id);
        }
        setBudgetsLoaded(true);
      })
      .catch(error => { if (error.name !== 'AbortError') { setBudgetsLoaded(true); setToast('Could not load your budgets.'); } });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!budgetsLoaded || !selectedBudgetId) return;
    const controller = new AbortController();
    loadedMonth.current = null;
    setSaveState('loading');
    setHistory([]); setFuture([]); setEditingId(null); setAssignmentError('');
    fetch(`/api/budgets/${selectedBudgetId}/months/${data.month}`, { signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(`Budget load failed (${response.status})`);
        return await response.json() as BudgetState;
      })
      .then(budget => {
        loadedMonth.current = budget.month;
        lastSaved.current = JSON.stringify(budget);
        setConflictBudget(null);
        setData(budget);
        setHistory([]);setFuture([]);setEditingId(null);setAssignmentError('');
        // Keep the category selection while browsing months; drop categories this month does not have.
        const monthCategoryIds = new Set(budget.groups.flatMap(group => group.categories.map(category => category.id)));
        setSelectedIds(current => current.filter(id => monthCategoryIds.has(id)));
        setSelectedCategoryId(current => current && monthCategoryIds.has(current) ? current : null);
        setSaveState('saved');
      })
      .catch(error => {
        if (error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Could not reach the budget API. Start the backend and reload.');
        }
      });
    return () => controller.abort();
  }, [data.month, budgetRefresh, budgetsLoaded, selectedBudgetId]);

  useEffect(() => {
    if (!unassignOpen || !selectedBudgetId) return;
    if (unassignMonth > currentMonthKey()) {
      setUnassignData(null);
      setUnassignLoading(false);
      return;
    }
    if (unassignMonth === data.month) {
      setUnassignData(data);
      setUnassignLoading(false);
      return;
    }
    const controller = new AbortController();
    setUnassignLoading(true); setUnassignError(''); setUnassignData(null);
    fetch(`/api/budgets/${selectedBudgetId}/months/${unassignMonth}`, { signal: controller.signal })
      .then(async response => { if (!response.ok) throw new Error(`Month load failed (${response.status})`); return await response.json() as BudgetState; })
      .then(snapshot => setUnassignData(snapshot))
      .catch(error => { if (error.name !== 'AbortError') setUnassignError('Could not load categories for this month.'); })
      .finally(() => { if (!controller.signal.aborted) setUnassignLoading(false); });
    return () => controller.abort();
  }, [data, selectedBudgetId, unassignMonth, unassignOpen]);

  useEffect(() => {
    if (!budgetsLoaded || !selectedBudgetId) return;
    const controller = new AbortController();
    const { from, to } = monthBounds(data.month);
    setUpcomingLoading(true); setUpcomingError(''); setUpcomingItems([]);
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
  }, [data.month, budgetRefresh, budgetsLoaded, selectedBudgetId]);

  useEffect(() => {
    if (!budgetsLoaded || !selectedBudgetId) return;
    const controller = new AbortController();
    const years = [...new Set(assignmentHistoryMonths.map(month => Number(month.slice(0, 4))))];
    setAssignmentHistory({});
    setAssignmentHistoryLoading(true);
    setAssignmentHistoryError('');
    setAutoAssignExpanded(false);
    setAssignMenuOpen(false);
    const load = async () => {
      try {
        const plans = await Promise.all(years.map(year => requestJson<LongTermPlanningData>(`/api/long-term-planning/${year}`, { signal: controller.signal })));
        if (controller.signal.aborted) return;
        const monthSet = new Set(assignmentHistoryMonths);
        const history: AssignmentHistory = {};
        for (const plan of plans) {
          for (const group of plan.groups) {
            for (const category of group.categories) {
              for (const actual of category.months) {
                if (!monthSet.has(actual.month)) continue;
                history[actual.month] ??= {};
                history[actual.month][category.id] = { assigned: actual.actualAssigned, spent: actual.actualExpenses };
              }
            }
          }
        }
        setAssignmentHistory(history);
      } catch (error) {
        if (!controller.signal.aborted && error instanceof Error) setAssignmentHistoryError(error.message);
      } finally {
        if (!controller.signal.aborted) setAssignmentHistoryLoading(false);
      }
    };
    void load();
    return () => controller.abort();
  }, [assignmentHistoryMonths, assignmentHistoryRetry, budgetRefresh, budgetsLoaded, selectedBudgetId]);

  useEffect(() => {
    if (!selectedBudgetId || loadedMonth.current !== data.month || conflictBudget) return;
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
        const response = await fetch(`/api/budgets/${selectedBudgetId}/months/${data.month}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request), signal: controller.signal });
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
        setRecentMovesRefresh(value=>value+1);
      } catch (error) {
        if (error instanceof Error && error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Changes could not be saved. Check the backend connection.');
        }
      }
    }, 450);
    return () => window.clearTimeout(timer);
  }, [data, conflictBudget, selectedBudgetId]);

  // Live updates: refresh what another session (browser, agent via MCP) committed, without clobbering local Plan edits.
  const [liveLedgerRefresh, setLiveLedgerRefresh] = useState(0);
  const liveUpdates = useBudgetLiveUpdates({
    budgetId: budgetsLoaded ? selectedBudgetId : null,
    month: data.month,
    planVersion: data.version,
    isSaving: saveState === 'saving',
    hasUnsavedPlanEdits: () => Boolean(editingId) || conflictBudget !== null ||
      (loadedMonth.current === data.month && lastSaved.current !== JSON.stringify(data)),
    onPlanChanged: () => setBudgetRefresh(value => value + 1),
    onLedgerChanged: () => { setLedgerRefresh(value => value + 1); setLiveLedgerRefresh(value => value + 1); },
    onBudgetChanged: () => {
      void requestJson<{ budgets: OwnedBudget[] }>('/api/budgets').then(result => {
        const available = result.budgets.filter(budget => !budget.isArchived);
        setOwnedBudgets(available);
        const current = available.find(budget => budget.id === selectedBudgetId);
        if (current) setPlan(current);
      }).catch(() => undefined);
    },
  });

  async function reviewRemotePlanChange() {
    liveUpdates.dismissNotice();
    try {
      const latest = await requestJson<BudgetState>(`/api/budgets/${selectedBudgetId}/months/${data.month}`);
      setEditingId(null);
      setConflictBudget(latest);
      setSaveState('conflict');
    } catch {
      setToast('Could not load the latest plan. Check the connection and try again.');
    }
  }

  const allCategories = useMemo(() => data.groups.flatMap(group => group.categories), [data.groups]);
  const assignableCategories = useMemo(() => data.groups.filter(group => !group.isArchived).flatMap(group => group.categories.filter(category => !category.isArchived)), [data.groups]);
  const currentMonthUpcomingItems = useMemo(() => upcomingItems.filter(item => item.transaction.date.startsWith(`${data.month}-`)), [upcomingItems, data.month]);
  const autoAssignCategories = useMemo(() => (selectedIds.length > 0 ? assignableCategories.filter(category => selectedIds.includes(category.id)) : assignableCategories)
    .map(category => ({ ...category, targetMoreNeeded: category.snoozed ? 0 : category.targetProgress?.moreNeeded ?? 0 })), [assignableCategories, selectedIds]);
  const scheduledOutflows = useMemo(() => Object.fromEntries(assignableCategories.map(category => {
    const outflowCents = currentMonthUpcomingItems.reduce((total, item) => {
      const amount = categoryTransactionAmount(item.transaction, category.id);
      return amount < 0 ? total + Math.round(amount * 100) : total;
    }, 0);
    return [category.id, outflowCents / 100];
  })), [assignableCategories, currentMonthUpcomingItems]);
  const autoAssignOptions = useMemo(() => buildAutoAssignOptions(autoAssignCategories, assignmentHistory, assignmentHistoryMonths, scheduledOutflows), [autoAssignCategories, assignmentHistory, assignmentHistoryMonths, scheduledOutflows]);
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
  const moveCategoryOptions = data.groups.filter(group => !group.isArchived).flatMap(group => group.categories.filter(category => !category.isArchived).map(category => ({ id: category.id, name: category.name, available: category.available, groupId: group.id, groupName: group.name })));
  const matchingCategoryCount = visibleCategories.filter(matchesFilter).length;
  const selectedCategory = visibleCategories.find(category => category.id === selectedCategoryId);
  const paymentAccountFor = (categoryId: string) => accounts.accounts.find(account => isCreditAccount(account) && creditPaymentCategoryId(account.id) === categoryId);
  const selectedPaymentAccount = selectedCategory ? paymentAccountFor(selectedCategory.id) : undefined;
  const selectedUpcoming = selectedCategory ? currentMonthUpcomingItems.flatMap(item => { const amount = categoryTransactionAmount(item.transaction, selectedCategory.id); return amount < 0 ? [{ item, amount }] : []; }) : [];
  const selectedUpcomingTotal = selectedUpcoming.reduce((sum, item) => sum + item.amount, 0);
  const unassignCategories = unassignData?.groups.filter(group => !group.isArchived).flatMap(group => group.categories.filter(category => !category.isArchived && category.available > 0).map(category => ({ ...category, groupName: group.name }))) ?? [];

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

  function applyAssignmentChanges(adjustments: { categoryId: string; deltaCents: number }[]) {
    const changes = new Map(adjustments.filter(item => item.deltaCents !== 0).map(item => [item.categoryId, item.deltaCents]));
    if (changes.size === 0) return 0;
    setData(current => {
      setHistory(stack => [...stack.slice(-49), current]);
      setFuture([]);
      return { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => {
        const deltaCents = changes.get(category.id) ?? 0;
        return deltaCents === 0 ? category : { ...category, assigned: (Math.round(category.assigned * 100) + deltaCents) / 100 };
      }) })) };
    });
    return [...changes.values()].filter(delta => delta > 0).reduce((total, delta) => total + delta, 0);
  }

  function applyAutoAssignOption(option: AutoAssignOption) {
    if (saveState !== 'saved' || assignmentHistoryLoading || option.requiresHistory && assignmentHistoryError) return;
    let readyCents = Math.max(0, Math.round(data.readyToAssign * 100));
    let recommendedCents = 0;
    const adjustments = option.adjustments.map(item => {
      if (item.deltaCents <= 0) return item;
      recommendedCents += item.deltaCents;
      const deltaCents = Math.min(item.deltaCents, readyCents);
      readyCents -= deltaCents;
      return { ...item, deltaCents };
    });
    const appliedCents = applyAssignmentChanges(adjustments);
    setAssignMenuOpen(false);
    setAutoAssignExpanded(false);
    if (!adjustments.some(item => item.deltaCents !== 0)) {
      setToast(`${option.label}: no changes to apply.`);
    } else if (recommendedCents > appliedCents) {
      setToast(`${money(appliedCents / 100)} assigned. Some categories still need ${money((recommendedCents - appliedCents) / 100)}.`);
    } else {
      setToast(`${option.label} applied`);
    }
    window.setTimeout(() => setToast(''), 3000);
  }

  function openAssignMenu() {
    setAssignTab('auto');
    setManualAssignAmount(Math.max(0, data.readyToAssign).toFixed(2));
    setManualAssignCategory('');
    setManualAssignError('');
    setAssignMenuOpen(current => !current);
  }

  function submitManualAssignment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const amount = parseAssignment(manualAssignAmount);
    if (!manualAssignCategory || !amount || amount <= 0) {
      setManualAssignError('Choose a subcategory and enter an amount greater than zero.');
      return;
    }
    if (saveState !== 'saved' || Math.round(amount * 100) > Math.max(0, Math.round(data.readyToAssign * 100))) {
      setManualAssignError('The amount cannot exceed Ready to Assign.');
      return;
    }
    if (!assignableCategories.some(category => category.id === manualAssignCategory)) {
      setManualAssignError('Choose an active subcategory.');
      return;
    }
    const applied = applyAssignmentChanges([{ categoryId: manualAssignCategory, deltaCents: Math.round(amount * 100) }]);
    if (applied > 0) {
      setAssignMenuOpen(false);
      setToast(`${money(applied / 100)} assigned`);
      window.setTimeout(() => setToast(''), 2400);
    }
  }

  const assignmentActionsDisabled = saveState !== 'saved' || assignmentHistoryLoading;
  const canApplyAutoOption = (option: AutoAssignOption) => {
    if (option.requiresHistory && assignmentHistoryError) return false;
    if (option.requiresUpcoming && (upcomingLoading || upcomingError)) return false;
    return !option.adjustments.some(item => item.deltaCents > 0) || data.readyToAssign > 0;
  };

  function openMoveFrom(category: Category) {
    const covering = category.available < 0;
    const source = moveCategories.find(item => item.id !== category.id && item.available > 0);
    const destination = moveCategories.find(item => item.id !== category.id);
    setMoveCovering(covering);
    setMoveFrom(covering ? source?.id ?? '' : category.id);
    setMoveTo(covering ? category.id : destination?.id ?? '');
    setMoveAmount(covering ? source ? String(Math.min(Math.abs(category.available), source.available)) : '' : String(category.available));
    setMoveOpen(true);
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

  function openNameEditor(kind: 'group' | 'category', id: string, name: string) {
    setNameEditor({ kind, id });
    setNameDraft(name);
    setNameEditorError('');
  }

  function nameEditorCanDelete() {
    if (!nameEditor || loadedMonth.current !== data.month || !lastSaved.current) return false;
    try {
      const saved = JSON.parse(lastSaved.current) as BudgetState;
      if (nameEditor.kind === 'group') return !saved.groups.some(group => group.id === nameEditor.id);
      return !saved.groups.some(group => group.categories.some(category => category.id === nameEditor.id));
    } catch { return false; }
  }

  function saveNameEditor() {
    if (!nameEditor) return;
    const name = nameDraft.trim();
    const maxLength = nameEditor.kind === 'group' ? 120 : 160;
    if (!name) { setNameEditorError('Enter a name.'); return; }
    if (name.length > maxLength) { setNameEditorError(`Names can be up to ${maxLength} characters.`); return; }
    setData(current => {
      setHistory(stack => [...stack.slice(-49), current]);
      setFuture([]);
      return nameEditor.kind === 'group'
        ? { ...current, groups: current.groups.map(group => group.id === nameEditor.id ? { ...group, name } : group) }
        : { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === nameEditor.id ? { ...category, name } : category) })) };
    });
    setNameEditor(null);
  }

  function hideNameEditorTarget() {
    if (!nameEditor) return;
    if (nameEditor.kind === 'group') toggleGroupArchived(nameEditor.id);
    else toggleCategoryArchived(nameEditor.id);
    setSelectedCategoryId(null);
    setSelectedIds(current => nameEditor.kind === 'category' ? current.filter(id => id !== nameEditor.id) : current.filter(id => !data.groups.find(group => group.id === nameEditor.id)?.categories.some(category => category.id === id)));
    setNameEditor(null);
  }

  function deleteUnsavedNameEditorTarget() {
    if (!nameEditor || !nameEditorCanDelete()) return;
    setData(current => {
      setHistory(stack => [...stack.slice(-49), current]);
      setFuture([]);
      return nameEditor.kind === 'group'
        ? { ...current, groups: current.groups.filter(group => group.id !== nameEditor.id) }
        : { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.filter(category => category.id !== nameEditor.id) })) };
    });
    setNameEditor(null);
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

  function dropCategory(sourceId: string, targetGroupId: string, targetCategoryId?: string, position: 'before' | 'after' = 'before') {
    setData(current => {
      const sourceGroup = current.groups.find(group => group.categories.some(category => category.id === sourceId));
      const destinationGroup = current.groups.find(group => group.id === targetGroupId);
      const source = sourceGroup?.categories.find(category => category.id === sourceId);
      if (!sourceGroup || !destinationGroup || !source || sourceGroup.isArchived || destinationGroup.isArchived || source.isArchived || sourceId === targetCategoryId) return current;
      if (sourceGroup.id === wishlistGroupId || destinationGroup.id === wishlistGroupId) return current;
      const groups = current.groups.map(group => group.id === sourceGroup.id ? { ...group, categories: group.categories.filter(category => category.id !== sourceId) } : group);
      const destinationIndex = groups.findIndex(group => group.id === targetGroupId);
      const destination = groups[destinationIndex];
      const categories = [...destination.categories];
      const targetIndex = targetCategoryId ? categories.findIndex(category => category.id === targetCategoryId) : -1;
      const insertAt = targetIndex < 0 ? categories.length : targetIndex + (position === 'after' ? 1 : 0);
      categories.splice(insertAt, 0, source);
      groups[destinationIndex] = { ...destination, categories };
      setHistory(stack => [...stack.slice(-49), current]); setFuture([]);
      return { ...current, groups };
    });
  }

  function dropGroup(sourceId: string, targetGroupId: string) {
    const source = dragSourceRef.current ?? dragSource;
    if (source?.kind === 'group') reorderGroup(sourceId, targetGroupId);
    else if (source?.kind === 'category') dropCategory(sourceId, targetGroupId);
    dragSourceRef.current = null; setDragSource(null); setDropTarget(null);
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
      setCategoryDialog(current => current?.requestId === requestId ? { ...current, items: expandActivityEntries(entries, false, [category.id], { [category.id]: category.name }), loading: false } : current);
    } catch (error) {
      setCategoryDialog(current => current?.requestId === requestId ? { ...current, loading: false, error: error instanceof Error ? error.message : 'Could not load transactions.' } : current);
    }
  }

  function openUpcoming(category: Category) {
    const categoryNames = Object.fromEntries(visibleCategories.map(item => [item.id, item.name]));
    const items = expandActivityEntries(currentMonthUpcomingItems.map(item => item.transaction), true, [category.id], categoryNames)
      .filter(item => (item.categoryAmount ?? item.entry.amount) < 0);
    const requestId = ++dialogRequestId.current;
    setCategoryDialog({ title: `Upcoming activity · ${category.name}`, categoryId: category.id, kind: 'upcoming', items, loading: false, error: upcomingError, requestId });
  }

  function closeCategoryDialog() { dialogRequestId.current++; setCategoryDialog(null); }

  function showTransactionInAccount(entry: Entry, scheduled: boolean, source: 'Plan' | 'Long-Term Planning' = 'Plan', splitIndex?: number) {
    closeCategoryDialog();
    navigate('Accounts', entry.accountId, { id: entry.id, accountId: entry.accountId, date: entry.date, scheduled, projected: scheduled ? entry : undefined, splitIndex, source });
  }

  function addFunds() {
    navigate('Accounts');
    setToast('Record income as a positive cash account transaction without a category.');
  }

  function openUnassign() {
    setUnassignMonth(data.month); setUnassignMonthDraft(data.month); setUnassignMonthMenuOpen(false); setUnassignData(data); setUnassignSelectedId(''); setUnassignError(''); setUnassignOpen(true);
  }

  function openUnassignMonthMenu() {
    setUnassignMonthDraft(unassignMonth);
    setUnassignMonthMenuOpen(true);
  }

  function cancelUnassignMonthChange() {
    setUnassignMonthDraft(unassignMonth);
    setUnassignMonthMenuOpen(false);
  }

  function applyUnassignMonthChange() {
    setUnassignMonth(unassignMonthDraft);
    setUnassignSelectedId('');
    setUnassignError('');
    setUnassignMonthMenuOpen(false);
  }

  async function unassignCategory(categoryId: string) {
    if (!unassignData || unassignMonth > currentMonthKey() || unassignSaving) return;
    const category = unassignCategories.find(item => item.id === categoryId);
    const needed = Math.max(0, -data.readyToAssign);
    if (!category || needed <= 0) return;
    const amount = Math.min(category.available, needed);
    const next = {
      ...unassignData,
      readyToAssign: unassignData.readyToAssign + amount,
      groups: unassignData.groups.map(group => ({ ...group, categories: group.categories.map(item => item.id === categoryId ? { ...item, assigned: item.assigned - amount, available: item.available - amount } : item) })),
    };
    setUnassignSelectedId('');
    if (unassignMonth === data.month) {
      setData(next); setUnassignData(next);
      setToast(`${money(amount)} unassigned from ${category.name}`);
      window.setTimeout(() => setToast(''), 2400);
      return;
    }
    setUnassignSaving(true); setUnassignError('');
    try {
      const request = {
        month: next.month, note: next.note ?? '', version: next.version, fundsAdded: next.fundsAdded,
        groups: next.groups.map((group, groupOrder) => ({ id: group.id, name: group.name, sortOrder: groupOrder, isArchived: Boolean(group.isArchived), categories: group.categories.map((item, categoryOrder) => ({ id: item.id, name: item.name, assigned: item.assigned, target: item.target, icon: item.icon ?? null, snoozed: Boolean(item.snoozed), sortOrder: categoryOrder, isArchived: Boolean(item.isArchived) })) })),
      };
      const response = await fetch(`/api/budgets/${selectedBudgetId}/months/${unassignMonth}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) });
      if (!response.ok) throw new Error(`Month save failed (${response.status})`);
      setUnassignData(await response.json() as BudgetState);
      setBudgetRefresh(value => value + 1);
      setToast(`${money(amount)} unassigned from ${category.name}`);
      window.setTimeout(() => setToast(''), 2400);
    } catch { setUnassignError('Could not unassign money. Try again.'); }
    finally { setUnassignSaving(false); }
  }

  function selectBudget(id: string) {
    if (id === selectedBudgetId) return;
    if (ledgerPending || planningPending || editingId || saveState === 'saving' || conflictBudget || (loadedMonth.current === data.month && lastSaved.current !== JSON.stringify(data))) {
      setToast('Save or resolve the current changes before switching budgets.');
      return;
    }
    const selected = ownedBudgets.find(budget => budget.id === id);
    if (!selected) return;
    setSelectedIds([]); setSelectedCategoryId(null); setSelectedAccount(''); setFocusTransaction(null);
    loadedMonth.current = null;
    lastSaved.current = null;
    setPlan(selected);
    setSelectedBudgetId(selected.id);
    rememberActiveBudget(selected.id);
    setData(current => ({ ...current, groups: [] }));
  }

  async function createBudget() {
    const name = window.prompt('New budget name', 'My Budget')?.trim();
    if (!name) return;
    try {
      const response = await fetch('/api/budgets', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, currency: plan.currency || 'EUR' }) });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? 'Budget could not be created.');
      const created = result as OwnedBudget;
      setOwnedBudgets(current => [...current, created]);
      setPlan(created);
      setSelectedBudgetId(created.id); setSelectedAccount('');
      rememberActiveBudget(created.id);
      loadedMonth.current = null;
      lastSaved.current = null;
      setData(current => ({ ...current, groups: [] }));
      setHistory([]); setFuture([]); setToast('Budget created');
    } catch (error) {
      setToast(error instanceof Error ? error.message : 'Budget could not be created.');
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

  // A target change does not alter the month snapshot, so patch both the current data and the last saved copy
  // without triggering a Plan save.
  function applyTargetResult(result: CategoryTargetResult) {
    if (result.month !== data.month) return;
    const patch = (state: BudgetState): BudgetState => ({ ...state, groups: state.groups.map(group => ({ ...group, categories: group.categories.map(category =>
      category.id === result.categoryId ? { ...category, categoryTarget: result.target, targetProgress: result.progress } : category) })) });
    if (lastSaved.current && loadedMonth.current === result.month) lastSaved.current = JSON.stringify(patch(JSON.parse(lastSaved.current) as BudgetState));
    setData(current => current.month === result.month ? patch(current) : current);
    setToast(result.target ? 'Target saved' : 'Target deleted');
    window.setTimeout(() => setToast(''), 2200);
  }

  function matchesFilter(category: Category) {
    const textMatches = category.name.toLowerCase().includes(query.toLowerCase());
    if (!textMatches) return false;
    switch (filter) {
      case 'Overspent': return category.available < 0;
      // Structured targets use the server-calculated amount still needed this month; the legacy manual target remains a fallback.
      case 'Underfunded': return !category.snoozed && (isTargetUnderfunded(category.targetProgress) || (!category.targetProgress && category.target > category.assigned && category.available >= 0));
      case 'Overfunded': return category.targetProgress ? category.targetProgress.needed > 0 && category.assigned > category.targetProgress.needed : category.target > 0 && category.available > category.target;
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

  <section className={`ready-card ${data.readyToAssign === 0 ? 'all-money-assigned' : data.readyToAssign < 0 ? 'assigned-over' : 'funds-ready'} ${assignMenuOpen ? 'assign-menu-open' : ''}`} aria-label="Ready to assign">
        <div className="ready-copy">
          <strong>{money(data.readyToAssign)}</strong>
          <span className="ready-hint">{data.readyToAssign < 0 ? 'You assigned more than you have' : data.readyToAssign > 0 ? 'Ready to assign' : 'All Money Assigned'}</span>
        </div>
        {data.readyToAssign === 0
          ? <span className="all-assigned-check" aria-hidden="true"><Icon name="check" size={26}/></span>
          : data.readyToAssign > 0
            ? <button className="ready-link assign-ready-button" aria-expanded={assignMenuOpen} aria-haspopup="dialog" onClick={openAssignMenu}>Assign <Icon name="chevron" size={13} /></button>
            : <button className="ready-link" onClick={data.readyToAssign < 0 ? openUnassign : addFunds}>{data.readyToAssign < 0 ? 'Fix This' : 'Record income'}</button>}
        {assignMenuOpen && data.readyToAssign > 0 && <section className="assign-menu-popover" role="dialog" aria-label="Assign money">
          <div className="assign-tabs" role="tablist" aria-label="Assignment method">
            <button type="button" role="tab" aria-selected={assignTab === 'auto'} className={assignTab === 'auto' ? 'active' : ''} onClick={() => setAssignTab('auto')}>Auto</button>
            <button type="button" role="tab" aria-selected={assignTab === 'manual'} className={assignTab === 'manual' ? 'active' : ''} onClick={() => setAssignTab('manual')}>Manually</button>
          </div>
          {assignTab === 'auto' ? <div className="assign-menu-content">
            {assignmentHistoryLoading && <p role="status">Loading the last 12 months…</p>}
            {assignmentHistoryError && <p className="assign-history-error" role="alert">Could not load past assignments. Historical options are unavailable. <button type="button" onClick={() => setAssignmentHistoryRetry(value => value + 1)}>Retry</button></p>}
            {upcomingLoading && <p role="status">Loading this month’s scheduled transactions…</p>}
            {upcomingError && <p className="assign-history-error" role="alert">Could not load scheduled transactions. Underfunded is unavailable. <button type="button" onClick={() => setBudgetRefresh(value => value + 1)}>Retry</button></p>}
            <AutoAssignOptions options={autoAssignOptions} formatAmount={money} disabled={assignmentActionsDisabled} canApply={canApplyAutoOption} onApply={applyAutoAssignOption} />
          </div> : <form className="manual-assign-form" onSubmit={submitManualAssignment}>
            <label>Assign:<MoneyInput aria-label="Amount to assign" value={manualAssignAmount} onChange={raw => { setManualAssignAmount(raw); setManualAssignError(''); }} disabled={saveState !== 'saved'} /></label>
            <CategoryPicker label="To" categories={moveCategoryOptions} value={manualAssignCategory} onChange={value => { setManualAssignCategory(value); setManualAssignError(''); }} includeInflow={false} disabled={saveState !== 'saved'} />
            {manualAssignError && <span role="alert" className="manual-assign-error">{manualAssignError}</span>}
            <footer><button type="button" onClick={() => setAssignMenuOpen(false)}>Cancel</button><button type="submit" className="button-primary" disabled={saveState !== 'saved'}>Assign</button></footer>
          </form>}
        </section>}
        {unassignOpen && <section className="unassign-popover" role="dialog" aria-label="Un-assign money from">
          <header><strong>Un-assign money from</strong><label><button type="button" className="unassign-month-trigger" aria-label="Month to unassign from" aria-expanded={unassignMonthMenuOpen} onClick={openUnassignMonthMenu}>{shortMonthText(unassignMonth)} <Icon name="chevron" size={13} /></button></label><button type="button" aria-label="Close un-assign money" onClick={() => setUnassignOpen(false)}>×</button>
            {unassignMonthMenuOpen && <div className="unassign-month-menu" role="group" aria-label="Select unassign month"><button type="button" className={unassignMonthDraft === data.month ? 'selected' : ''} aria-pressed={unassignMonthDraft === data.month} onClick={() => setUnassignMonthDraft(data.month)}>{shortMonthText(data.month)}{unassignMonthDraft === data.month && <Icon name="check" size={17} />}</button><button type="button" className={unassignMonthDraft === previousMonthKey(data.month) ? 'selected' : ''} aria-pressed={unassignMonthDraft === previousMonthKey(data.month)} onClick={() => setUnassignMonthDraft(previousMonthKey(data.month))}>{shortMonthText(previousMonthKey(data.month))}{unassignMonthDraft === previousMonthKey(data.month) && <Icon name="check" size={17} />}</button><footer><button type="button" onClick={cancelUnassignMonthChange}>Cancel</button><button type="button" className="button-primary" disabled={unassignMonthDraft === unassignMonth} onClick={applyUnassignMonthChange}>OK</button></footer></div>}
          </header>
          <select className="unassign-category-select" value={unassignSelectedId} onChange={event => { setUnassignSelectedId(event.target.value); if (event.target.value) void unassignCategory(event.target.value); }} disabled={unassignLoading || unassignSaving || unassignCategories.length === 0} aria-label="Category to unassign"><option value=""> </option>{unassignCategories.map(category => <option key={category.id} value={category.id}>{category.name} · {money(category.available)}</option>)}</select>
          {unassignLoading ? <p role="status">Loading categories…</p> : unassignError ? <p role="alert">{unassignError}</p> : <div className="unassign-category-list"><strong>Plan Categories</strong>{unassignCategories.length === 0 ? <p>No categories found.</p> : unassignData?.groups.filter(group => !group.isArchived).map(group => { const categories = group.categories.filter(category => !category.isArchived && category.available > 0); if (!categories.length) return null; return <div className="unassign-group" key={group.id}><strong>{group.name}</strong>{categories.map(category => <button type="button" key={category.id} disabled={unassignSaving} onClick={() => void unassignCategory(category.id)}><span>{category.name}</span><span>{money(category.available)}</span></button>)}</div>; })}</div>}
        </section>}
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
              <button className="table-tool recent-button" aria-expanded={recentMovesOpen} onClick={() => setRecentMovesOpen(value=>!value)}><span className="recent-dot">◴</span> Recent Moves</button>
            </div>
            <div className="progress-toggle" aria-label="Progress bars setting"><button title="Progress Bars On" aria-label="Progress Bars On" className={progressOn ? 'active' : ''} onClick={() => setProgressOn(true)}><Icon name="bars" size={16} /></button><button title="Progress Bars Off" aria-label="Progress Bars Off" className={!progressOn ? 'active' : ''} onClick={() => setProgressOn(false)}><Icon name="lines" size={16} /></button></div>
          </div>
          {recentMovesOpen&&selectedBudgetId&&<RecentMoves budgetId={selectedBudgetId} currency={plan.currency} refresh={recentMovesRefresh} onClose={()=>setRecentMovesOpen(false)}/>}

          <div className={`budget-table ${progressOn ? '' : 'compact'}`}>
            <div className="table-head">
              <span className="head-category"><button className="collapse-all" aria-label={data.groups.length > 0 && collapsed.length === data.groups.length ? 'Expand all' : 'Collapse all'} aria-expanded={!(data.groups.length > 0 && collapsed.length === data.groups.length)} onClick={() => setCollapsed(collapsed.length === data.groups.length ? [] : data.groups.map(group => group.id))}><span className={`group-chevron ${data.groups.length > 0 && collapsed.length === data.groups.length ? 'closed' : ''}`}><Icon name="chevron" size={18} /></span></button><input type="checkbox" aria-label="Check all" checked={selectedIds.length === allCategories.length && allCategories.length > 0} onChange={event => setSelectedIds(event.target.checked ? allCategories.map(category => category.id) : [])} /><span>CATEGORY</span></span>
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
              const groupHasUpcomingShortfall = !upcomingLoading && !upcomingError && group.categories.some(category => {
                const scheduledOutflow = scheduledOutflows[category.id] ?? 0;
                return !category.isArchived && scheduledOutflow < 0 && Math.round((category.available + scheduledOutflow) * 100) < 0;
              });
              return <div className={`category-group ${dropTarget?.groupId === group.id && !dropTarget.categoryId ? 'drop-target' : ''}`} key={group.id} onDragOver={event => {
                if (!(dragSourceRef.current ?? dragSource)) return;
                event.preventDefault();
                if (!(event.target instanceof Element) || !event.target.closest('.category-row')) setDropTarget({ groupId: group.id, position: 'after' });
              }} onDrop={event => { event.preventDefault(); const source = dragSourceRef.current ?? dragSource; if (source) dropGroup(source.id, group.id); }}>
                <div className="group-row">
                  <div className="group-name"><button className="group-collapse" aria-label={`Toggle ${group.name}`} aria-expanded={!isCollapsed} onClick={() => toggleGroup(group.id)}><span className={`group-chevron ${isCollapsed ? 'closed' : ''}`}><Icon name="chevron" size={18} /></span></button><input type="checkbox" aria-label={`Select ${group.name}`} checked={group.categories.length > 0 && group.categories.every(category => selectedIds.includes(category.id))} onChange={event => setSelectedIds(current => event.target.checked ? [...new Set([...current, ...group.categories.map(category => category.id)])] : current.filter(id => !group.categories.some(category => category.id === id)))} /><button className="group-label" draggable={!group.isArchived && group.id !== creditPaymentGroupId} title="Drag to reorder this group" onDragStart={event => { const source = { kind: 'group' as const, id: group.id }; event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', group.id); dragSourceRef.current = source; setDragSource(source); }} onDragEnd={() => { dragSourceRef.current = null; setDragSource(null); setDropTarget(null); }} onClick={() => { if (group.id !== creditPaymentGroupId && group.id !== wishlistGroupId) openNameEditor('group', group.id, group.name); }}>{group.name}{isCollapsed && groupHasUpcomingShortfall && <span className="group-upcoming-warning" role="img" aria-label="Upcoming transactions will make a subcategory negative"><Icon name="warning" size={15} /></span>}</button>{!group.isArchived && group.id !== creditPaymentGroupId && group.id !== wishlistGroupId && <button className="group-add" onClick={() => addCategory(group.id)} aria-label={`Add category to ${group.name}`}><Icon name="plus" size={16} /></button>}<div className="group-order-tools"><button aria-label={`Move ${group.name} up`} onClick={() => moveGroup(group.id, -1)}>↑</button><button aria-label={`Move ${group.name} down`} onClick={() => moveGroup(group.id, 1)}>↓</button></div></div>
                  {nameEditor?.kind === 'group' && nameEditor.id === group.id && <CategoryNamePopover name={nameDraft} label="category group" maxLength={120} error={nameEditorError} canDelete={nameEditorCanDelete()} hideLabel={group.isArchived ? 'Restore' : 'Hide'} onNameChange={value => { setNameDraft(value); setNameEditorError(''); }} onSubmit={saveNameEditor} onHide={hideNameEditorTarget} onDelete={deleteUnsavedNameEditorTarget} onCancel={() => setNameEditor(null)} />}
                  <div className="group-assigned">{money(group.categories.reduce((sum, item) => sum + item.assigned, 0))}</div>
                  <div className="group-activity">{money(group.categories.reduce((sum, item) => sum + item.activity, 0))}</div>
                  <div className={`group-available ${groupTotal < 0 ? 'negative' : ''}`}>{group.id === creditPaymentGroupId && <span className="payment-group-label">Payment</span>}{money(groupTotal)}</div>
                  {addCategoryGroupId === group.id && <form className="category-add-popover" onSubmit={submitCategory}><label className="sr-only" htmlFor={`new-category-${group.id}`}>New category</label><input id={`new-category-${group.id}`} autoFocus maxLength={160} value={newCategoryName} placeholder="New Category" onChange={event => { setNewCategoryName(event.target.value); setCategoryNameError(''); }} onKeyDown={event => { if (event.key === 'Escape') setAddCategoryGroupId(null); }} />{categoryNameError && <span role="alert">{categoryNameError}</span>}<div><button type="button" onClick={() => setAddCategoryGroupId(null)}>Cancel</button><button className="button button-primary" type="submit">OK</button></div></form>}<button className="group-archive" aria-label={`${group.isArchived ? 'Restore' : 'Archive'} group ${group.name}`} onClick={() => toggleGroupArchived(group.id)}>{group.isArchived ? 'Restore' : 'Archive'}</button>
                </div>
                {!isCollapsed && shownCategories.map(category => {
                  const upcomingTotal = scheduledOutflows[category.id] ?? 0;
                  const projectedAvailable = category.available + upcomingTotal;
                  const underfundedUpcoming = !upcomingLoading && !upcomingError && upcomingTotal < 0 && Math.round(projectedAvailable * 100) < 0;
                  const progress = categoryProgress(category.assigned, category.activity, category.available, upcomingTotal);
                  const hasProgress = progress.green > 0 || progress.spent > 0 || progress.orange > 0 || progress.red > 0;
                  const paymentAccount = group.id === creditPaymentGroupId ? paymentAccountFor(category.id) : undefined;
                  const paymentUnderfunded = paymentAccount !== undefined && Math.round((-paymentAccount.balance - category.available) * 100) > 0;
                const targetProgress = !paymentAccount && !category.snoozed ? category.targetProgress ?? null : null;
                const targetUnderfunded = category.available >= 0 && isTargetUnderfunded(targetProgress);
                const targetBar = Boolean(targetProgress) && category.available >= 0;
                const wishlistRow = group.id === wishlistGroupId;
                  return <div className={`category-row ${category.available < 0 ? 'is-overspent' : ''} ${selectedCategoryId === category.id ? 'category-selected' : ''} ${dropTarget?.categoryId === category.id ? `drop-target-${dropTarget.position}` : ''}`} key={category.id} draggable={!category.isArchived && !group.isArchived && !paymentAccount && !wishlistRow} onClick={event => {
                    if (event.target instanceof Element && event.target.closest('button, input, select, textarea, a')) return;
                    if (category.isArchived || group.isArchived || saveState === 'loading' || loadedMonth.current !== data.month || conflictBudget) return;
                    if (selectedIds.includes(category.id)) {
                      setSelectedIds(current => current.filter(id => id !== category.id));
                      setSelectedCategoryId(current => current === category.id ? null : current);
                      if (editingId === category.id) { setEditingId(null); setAssignmentError(''); }
                      return;
                    }
                    setSelectedIds([category.id]);
                    setSelectedCategoryId(category.id);
                    startEdit(category);
                  }} onDragStart={event => { const source = { kind: 'category' as const, id: category.id }; event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', category.id); dragSourceRef.current = source; setDragSource(source); }} onDragEnd={() => { dragSourceRef.current = null; setDragSource(null); setDropTarget(null); }} onDragOver={event => {
                    if ((dragSourceRef.current ?? dragSource)?.kind !== 'category') return;
                    event.preventDefault();
                    event.stopPropagation();
                    const bounds = event.currentTarget.getBoundingClientRect();
                    setDropTarget({ groupId: group.id, categoryId: category.id, position: event.clientY > bounds.top + bounds.height / 2 ? 'after' : 'before' });
                  }} onDrop={event => {
                    event.preventDefault(); event.stopPropagation();
                    const source = dragSourceRef.current ?? dragSource;
                    if (source?.kind === 'category') {
                      const bounds = event.currentTarget.getBoundingClientRect();
                      dropCategory(source.id, group.id, category.id, event.clientY > bounds.top + bounds.height / 2 ? 'after' : 'before');
                    }
                    dragSourceRef.current = null; setDragSource(null); setDropTarget(null);
                  }}>
                    <div className="category-name-cell">{underfundedUpcoming && <span className="category-upcoming-warning" role="img" aria-label={`Upcoming transactions will make ${category.name} negative`} />}<input type="checkbox" aria-label={`Select ${category.name}`} checked={selectedIds.includes(category.id)} onChange={event => setSelectedIds(current => event.target.checked ? [...current, category.id] : current.filter(id => id !== category.id))} /><div className="category-copy"><div className="category-label-line"><button className={`category-name ${selectedIds.includes(category.id) ? 'is-selected' : ''}`} title={selectedIds.includes(category.id) ? `Rename ${category.name}` : `Select ${category.name} before renaming`} disabled={category.isArchived || group.isArchived} onClick={() => { if (selectedIds.includes(category.id)) { if (!paymentAccount && !wishlistRow) openNameEditor('category', category.id, category.name); } else { setSelectedIds([category.id]); setSelectedCategoryId(category.id); startEdit(category); } }}>{paymentAccount && <span className="credit-card-icon" aria-hidden="true">💳</span>}{category.name}</button>{progressOn && <span className="category-status">{category.available < 0 ? 'Overspent' : category.snoozed ? 'Snoozed' : category.wishlistProgress ? <WishlistRowStatus progress={category.wishlistProgress} money={money} /> : targetProgress && targetUnderfunded ? <><b>{money(targetProgress.moreNeeded)}</b> more needed this month</> : category.available === 0 && category.activity < 0 ? 'Fully Spent' : targetProgress ? 'Funded' : ''}</span>}</div>{progressOn && targetBar && targetProgress && <div className={`progress-track target-track ${targetUnderfunded ? 'underfunded' : 'funded'}`} data-segments={targetProgress.segments} role="img" aria-label={`Target progress: ${Math.floor(targetProgress.fundedFraction * 100)}% of ${money(targetProgress.needed)} funded`}>{targetSegments(targetProgress).map((fill, index) => <span className="target-segment" key={index}><span style={{ width: `${fill * 100}%` }} /></span>)}</div>}{progressOn && !targetBar && hasProgress && <div className="progress-track" aria-label={`Category progress: ${Math.round(progress.green)}% available${progress.spent ? `, ${Math.round(progress.spent)}% spent` : ''}${progress.orange ? `, ${Math.round(progress.orange)}% upcoming` : ''}${progress.red ? `, ${Math.round(progress.red)}% overspent` : ''}`}><span className="progress-green" style={{ width: `${progress.green}%` }} />{progress.spent > 0 && <span className="progress-spent" style={{ width: `${progress.spent}%` }} />}{progress.orange > 0 && <span className="progress-orange" style={{ width: `${progress.orange}%` }} />}<span className="progress-red" style={{ width: `${progress.red}%` }} /></div>}</div><div className="category-order-tools"><button aria-label={`Move ${category.name} up`} disabled={category.isArchived || group.isArchived || wishlistRow} onClick={() => moveCategory(group.id, category.id, -1)}>↑</button><button aria-label={`Move ${category.name} down`} disabled={category.isArchived || group.isArchived || wishlistRow} onClick={() => moveCategory(group.id, category.id, 1)}>↓</button></div><button className="category-archive" disabled={group.isArchived || wishlistRow} aria-label={`${category.isArchived ? 'Restore' : 'Archive'} category ${category.name}`} onClick={() => toggleCategoryArchived(category.id)}>{category.isArchived ? 'Restore' : 'Archive'}</button></div>
                    {nameEditor?.kind === 'category' && nameEditor.id === category.id && <CategoryNamePopover name={nameDraft} label="subcategory" maxLength={160} error={nameEditorError} canDelete={nameEditorCanDelete()} hideLabel={category.isArchived ? 'Restore' : 'Hide'} onNameChange={value => { setNameDraft(value); setNameEditorError(''); }} onSubmit={saveNameEditor} onHide={hideNameEditorTarget} onDelete={deleteUnsavedNameEditorTarget} onCancel={() => setNameEditor(null)} />}
                    <div className="assigned-cell">{editingId===category.id&&assignmentError&&<span role="alert" className="assignment-error">{assignmentError}</span>}
                      {editingId === category.id ? <div className="amount-input-shell"><span className="assignment-adjust-icon"><Icon name="assign" size={14} /></span><input className="amount-input" autoFocus value={editValue} aria-invalid={Boolean(assignmentError)} onFocus={event => event.currentTarget.select()} onChange={event => {setEditValue(event.target.value);setAssignmentError('');}} onBlur={() => saveEdit(category)} onKeyDown={event => { if (event.key === 'Enter') saveEdit(category); if (event.key === 'Escape') {setEditingId(null);setAssignmentError('');} }} aria-label={`Assigned to ${category.name}`} /></div> : <button className="amount-button" disabled={category.isArchived || group.isArchived || saveState==='loading' || loadedMonth.current!==data.month || !!conflictBudget} onClick={() => { setSelectedIds([category.id]); setSelectedCategoryId(category.id); startEdit(category); }} aria-label={`Edit assigned for ${category.name}`}><span className="assignment-adjust-icon"><Icon name="assign" size={14} /></span><span>{money(category.assigned)}</span></button>}

                    </div>
                    <div className={`activity-cell ${category.activity < 0 ? 'negative' : ''}`}><button className="activity-button" disabled={category.activity === 0} aria-label={`View transactions for ${category.name} activity`} onClick={() => void openActivity(category)}>{money(category.activity)}</button></div>
                    <div className={`available-cell ${category.available < 0 ? 'negative' : underfundedUpcoming || paymentUnderfunded || targetUnderfunded ? 'underfunded' : category.available > 0 || targetProgress ? 'positive' : ''}`}>{category.available !== 0 ? <button className="available-button" aria-label={category.available < 0 ? `Cover overspending for ${category.name}` : `Move available money from ${category.name}`} onClick={() => openMoveFrom(category)}><span>{targetUnderfunded && targetProgress && <TargetRingIcon fraction={targetProgress.fundedFraction} />}{money(category.available)}</span></button> : <span>{targetUnderfunded && targetProgress && <TargetRingIcon fraction={targetProgress.fundedFraction} />}{money(category.available)}</span>}</div>
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
          {selectedCategory && selectedPaymentAccount ? <CreditPaymentDetails account={selectedPaymentAccount} month={data.month} available={selectedCategory.available} money={money} onClose={() => setSelectedCategoryId(null)} onChanged={() => setLedgerRefresh(value => value + 1)} /> : selectedCategory && <section className="category-detail-card"><header><strong>{selectedCategory.name}</strong><button type="button" aria-label="Close category details" onClick={() => setSelectedCategoryId(null)}>×</button></header><div className="summary-body"><div className="summary-line"><span>Available Balance</span><strong>{money(selectedCategory.available)}</strong></div><div className="summary-line"><span>Assigned This Month</span><strong>{money(selectedCategory.assigned)}</strong></div><div className="summary-line"><span>Activity</span><strong>{money(selectedCategory.activity)}</strong></div>{upcomingLoading ? <p role="status">Loading upcoming transactions…</p> : upcomingError ? <div role="alert">{upcomingError} <button onClick={() => setBudgetRefresh(value => value + 1)}>Retry</button></div> : selectedUpcoming.length > 0 && <><button className="category-upcoming-detail" onClick={() => openUpcoming(selectedCategory)}><span>{selectedUpcoming.length} Upcoming {selectedUpcoming.length === 1 ? 'Transaction' : 'Transactions'}</span><strong>{money(selectedUpcomingTotal)}</strong><span>Available After Upcoming</span><strong>{money(selectedCategory.available + selectedUpcomingTotal)}</strong></button>{selectedCategory.available + selectedUpcomingTotal < 0 && <span className="category-underfunded-hint">This category does not have enough available to cover these transactions.</span>}</>}</div></section>}
          {selectedCategory?.wishlistProgress && <WishlistCategoryCard progress={selectedCategory.wishlistProgress} money={money} onOpen={() => navigate('Wishlist')} />}
          {selectedCategory && !selectedPaymentAccount && !selectedCategory.id.startsWith('ccp-') && !selectedCategory.isArchived && <CategoryTargetCard key={`${selectedCategory.id}-${data.month}`} startEditing={targetEditRequest === selectedCategory.id} budgetId={selectedBudgetId} month={data.month} category={selectedCategory} money={money} onChanged={applyTargetResult} />}
          <section className="summary-card"><button className="summary-title" onClick={() => setToast('Monthly summary')}><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}'s Summary</span><Icon name="chevron" size={15} /></button><div className="summary-body">
            <div className="summary-line"><span>Ready to Assign</span><strong>{money(data.readyToAssign)}</strong></div><div className="summary-line"><span>Assigned in {new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}</span><strong>{money(totals.assigned)}</strong></div><div className="summary-line"><span>Activity</span><strong>{money(totals.activity)}</strong></div><div className="summary-line summary-total"><span>Available</span><strong>{money(totals.available)}</strong></div>
            <div className="summary-rule" /><div className="summary-subhead">Cost to Be Me</div><div className="summary-line"><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}’s Targets</span><strong>{money(targetSum)}</strong></div><div className="target-progress"><span style={{width:'14%'}} /></div><div className="summary-line"><span>Expected Income</span><strong>{money(data.ledgerIncome)} <button className="edit-income" aria-label="Edit income" onClick={() => setToast('Income settings')}>↗</button></strong></div><div className="income-progress"/><div className="summary-footnote">Next month's targets could increase your total to {money(targetSum)}</div></div></section>
          <section className={`auto-assign-widget ${autoAssignExpanded ? 'expanded' : ''}`}>
            <button className="auto-assign-card" aria-expanded={autoAssignExpanded} onClick={() => setAutoAssignExpanded(value => !value)}><span>ϟ</span><strong>Auto-Assign</strong><Icon name="chevron" size={15} /></button>
            {autoAssignExpanded && <div className="auto-assign-panel">
              {assignmentHistoryLoading && <p role="status">Loading the last 12 months…</p>}
              {assignmentHistoryError && <p className="assign-history-error" role="alert">Could not load past assignments. Historical options are unavailable. <button type="button" onClick={() => setAssignmentHistoryRetry(value => value + 1)}>Retry</button></p>}
              {upcomingLoading && <p role="status">Loading this month’s scheduled transactions…</p>}
              {upcomingError && <p className="assign-history-error" role="alert">Could not load scheduled transactions. Underfunded is unavailable. <button type="button" onClick={() => setBudgetRefresh(value => value + 1)}>Retry</button></p>}
              <AutoAssignOptions options={autoAssignOptions} formatAmount={money} disabled={assignmentActionsDisabled} canApply={canApplyAutoOption} onApply={applyAutoAssignOption} />
            </div>}
          </section>
          <section className="future-card"><div className="future-header"><strong>Assigned in Future Months</strong><Icon name="chevron" size={15} /></div><button onClick={() => changeMonth(1)}><span className="future-month">◷ &nbsp; {new Date(Number(data.month.slice(0,4)),Number(data.month.slice(5)),1).toLocaleDateString('en-GB',{month:'long'})}</span><span>{money(0)}</span></button></section>
                </aside>
      </div>
    </>
  );

  const otherPages: Record<string, { title: string; subtitle: string; icon: string }> = {
    Reflect: { title: 'Reflect on your spending', subtitle: 'Take a clear look at where your money went this month.', icon: 'chart' },
    Accounts: { title: 'Your accounts', subtitle: 'Keep balances and transactions together in one place.', icon: 'wallet' },
  };

  return <div className={`app-shell ${activePage==='Budget Together'?'together-active':''}`}>
    {mobileMenu && <button className="sidebar-scrim" aria-label="Close navigation" onClick={() => setMobileMenu(false)} />}
    <aside className={`sidebar ${mobileMenu ? 'mobile-open' : ''} ${sidebarCollapsed && !mobileMenu ? 'is-collapsed' : ''}`} style={{'--sidebar-width': `${sidebarCollapsed && !mobileMenu ? 56 : sidebarWidth}px`} as CSSProperties}>
      {!sidebarCollapsed && <div className="sidebar-resizer" role="separator" aria-label="Resize sidebar" aria-orientation="vertical" aria-valuemin={190} aria-valuemax={420} aria-valuenow={sidebarWidth} tabIndex={0} onPointerDown={event => { resizingSidebar.current = true; event.currentTarget.setPointerCapture(event.pointerId); event.preventDefault(); }} onPointerMove={event => { if (resizingSidebar.current) changeSidebarWidth(event.clientX); }} onPointerUp={event => { resizingSidebar.current = false; event.currentTarget.releasePointerCapture(event.pointerId); }} onPointerCancel={() => { resizingSidebar.current = false; }} onKeyDown={event => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); changeSidebarWidth(sidebarWidth + (event.key === 'ArrowRight' ? 10 : -10)); } }} />}
      <div className="budget-menu-anchor" onKeyDown={event => { if (event.key === 'Escape') setBudgetMenuOpen(false); }}>
        <button type="button" className="budget-picker" aria-label="Budget menu" aria-haspopup="menu" aria-expanded={budgetMenuOpen} onClick={() => { cancelClosePlans(); setBudgetMenuSection('main'); setBudgetMenuOpen(value => !value); }}><img src="/openbudget-logo.svg" alt="" /><span className="budget-picker-copy"><strong>{plan.name}</strong><small title={userEmail}>{userEmail}</small></span><span className="budget-picker-chevron" aria-hidden="true" /></button>
        {budgetMenuOpen && <><button className="budget-menu-dismiss" aria-label="Close budget menu" onClick={() => setBudgetMenuOpen(false)} /><div className="budget-dropdown" role="menu" aria-label="Budget menu options">
            <button role="menuitem" onClick={() => { setBudgetMenuOpen(false); void createBudget(); }}>✚ <span>New Plan</span></button>
            <button role="menuitem" aria-haspopup="menu" aria-expanded={budgetMenuSection !== 'main'} onMouseEnter={() => { cancelClosePlans(); setBudgetMenuSection('recent'); }} onMouseLeave={scheduleClosePlans} onClick={() => { cancelClosePlans(); setBudgetMenuSection('recent'); }}>▰ <span>Open Plan</span><span className="menu-right">›</span></button>
            <p>Current Plan</p>
            <button role="menuitem" onClick={() => setToast('Plan settings are not available yet')}>⚒ <span>Plan Settings</span></button>
            <button role="menuitem" onClick={() => { setBudgetMenuOpen(false); navigate('Accounts'); }}>◉ <span>Manage Payees</span></button>
            <button role="menuitem" onClick={() => setToast('Flag editing is not available yet')}>⚑ <span>Edit Flags</span></button>
            <button role="menuitem" onClick={() => setToast('Display options are not available yet')}>⚑ <span>Display Options</span></button>
            <button role="menuitem" onClick={() => setToast('Plan export is not available yet')}>▣ <span>Export Plan</span></button>
            <button role="menuitem" onClick={() => setToast('Fresh start is not available yet')}>◒ <span>Make a Fresh Start</span></button>
            <button role="menuitem" onClick={() => { setBudgetMenuOpen(false); navigate('Budget Together'); }}>♧ <span>Budget Together</span></button>
            <p>Account</p>
            <button role="menuitem" onClick={() => { setBudgetMenuOpen(false); setProfileOpen(true); }}>♟ <span>Account Settings</span></button>
            <button role="menuitem" onClick={() => setToast('Bank connections are not available yet')}>▥ <span>Manage Bank Connections</span></button>
            {isAdministrator && <a className="budget-menuitem" role="menuitem" href="/admin">♟ <span>Admin</span></a>}
            <button role="menuitem" onClick={() => { setBudgetMenuOpen(false); window.dispatchEvent(new Event('openbudget:signout')); }}>⇥ <span>Sign Out</span></button>
        </div><div className={`budget-plan-flyout ${budgetMenuSection !== 'main' ? 'is-open' : ''}`} role="menu" aria-label={budgetMenuSection === 'all' ? 'All Plans' : 'Recent Plans'} aria-hidden={budgetMenuSection === 'main'} onMouseEnter={cancelClosePlans} onMouseLeave={scheduleClosePlans}>
          <p>{budgetMenuSection === 'recent' ? 'Recent Plans' : 'All Plans'}</p>
          {(budgetMenuSection === 'recent' ? ownedBudgets.slice(0, 5) : ownedBudgets).map(budget => <button key={budget.id} role="menuitem" aria-current={budget.id === selectedBudgetId ? 'true' : undefined} onClick={() => { selectBudget(budget.id); setBudgetMenuOpen(false); }}>▣ <span>{budget.name}</span></button>)}
          {budgetMenuSection === 'recent' && <button className="view-all-plans" role="menuitem" onClick={() => setBudgetMenuSection('all')}>▰ <span>View All Plans</span></button>}
        </div></>}
      </div>
      <div className="side-nav" role="navigation" aria-label="Main navigation">
        <button aria-label="Plan" title="Plan" className={`nav-item ${activePage === 'Plan' ? 'active' : ''}`} onClick={() => navigate('Plan')}><Icon name="plan" size={22} /><span>Plan</span></button>
        <button aria-label="Long-Term Planning" title="Long-Term Planning" className={`nav-item ${activePage === 'Long-Term Planning' ? 'active' : ''}`} onClick={() => navigate('Long-Term Planning')}><Icon name="calendar" size={22} /><span>Long-Term Planning</span></button>
        <WishlistNavItem access={wishlistAccess} active={activePage === 'Wishlist'} onClick={() => navigate('Wishlist')} />
        <button aria-label="Reflect" title="Reflect" className={`nav-item ${activePage === 'Reflect' ? 'active' : ''}`} onClick={() => navigate('Reflect')}><Icon name="chart" /><span>Reflect</span></button>
        <button aria-label="All Accounts" title="All Accounts" className={`nav-item ${activePage === 'Accounts' && !selectedAccount ? 'active' : ''}`} onClick={() => navigate('Accounts')}><Icon name="bank" size={22} /><span>All Accounts</span></button>
      </div>
      <div className="account-section">
        {accountsLoaded && !accountError && accounts.accounts.length === 0 ? <section className="no-accounts-card"><strong>No Accounts</strong><p>You can't plan without adding accounts to Open Budget first. How about adding one now?</p></section> : null}
        {accountsLoaded && !accountError && accounts.accounts.length > 0 && <>
        {accountSections.map(section => { const sectionAccounts = accounts.accounts.filter(account => accountFamily(account) === section.id); if (section.id !== 'cash' && sectionAccounts.length === 0) return null; const total = sectionAccounts.reduce((sum, account) => sum + account.balance, 0); const expanded = !collapsedAccountSections.includes(section.id); return <Fragment key={section.id}>
        <button type="button" className="account-type" aria-expanded={expanded} aria-controls={`${section.id}-account-list`} onClick={() => setCollapsedAccountSections(current => expanded ? [...current, section.id] : current.filter(id => id !== section.id))}><svg className="cash-chevron" viewBox="0 0 16 16" width="12" height="12" aria-hidden="true"><path d="m5 3 5 5-5 5" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg><span>{section.title}</span><strong className={total < 0 ? 'negative-account-balance' : ''}>{money(total)}</strong></button>
        <div id={`${section.id}-account-list`} hidden={!expanded}>
          {sectionAccounts.map(account => <AccountSidebarRow key={account.id} account={account} selected={activePage==='Accounts'&&selectedAccount===account.id} disabled={ledgerPending} onSelect={()=>navigate('Accounts',account.id)} onEdit={()=>{if(!ledgerPending){navigate('Accounts',account.id);setAccountEditRequest({id:account.id,token:Date.now()});}}}/>)}
        </div></Fragment>; })}
        </>}
        {accountError && <button className="account-row" onClick={() => setLedgerRefresh(value => value + 1)}>Retry loading accounts</button>}
        {accountsLoaded && !accountError && <button type="button" className="add-cash-account" onClick={() => { if (navigate('Accounts')) setAccountCreateRequest(value => value + 1); }}><span aria-hidden="true">✚</span> Add Account</button>}
      </div>
      <div className="sidebar-bottom"><div className="referral-banner"><span>Give your friends 6 free weeks to try YNAB!</span><img src="/referral-heart.svg" alt="" /><button aria-label="Dismiss referral" onClick={event => {event.currentTarget.parentElement?.classList.add('dismissed');}}>×</button></div><div className="sidebar-footer"><button onClick={() => setToast('Sharing is not connected in this demo')}>▦ &nbsp; Share & Earn</button><button aria-label={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'} onClick={toggleSidebar}>{sidebarCollapsed ? '›' : '‹'}</button></div></div>
    </aside>

    <main className="main-area">
      <header className="topbar"><div className="breadcrumbs"><span>{plan.name}</span><Icon name="right" size={13} /><strong>{activePage}</strong></div><div className="topbar-right"><button className="share-button" onClick={() => setToast('Sharing is not connected in this local demo')}>Share</button><button className="top-avatar" aria-label="Profile" onClick={() => setProfileOpen(true)}>A</button></div></header>
      {activePage==='Budget Together'&&selectedBudgetId?<BudgetTogether budgetId={selectedBudgetId} budgetName={plan.name} currentUserEmail={userEmail} onBack={()=>navigate('Plan')}/>:activePage === 'Accounts' ? <AccountsPage liveRefresh={liveLedgerRefresh} onOpenCategory={(categoryId, editTarget) => { if (navigate('Plan')) { setSelectedCategoryId(categoryId); setSelectedIds([]); setTargetEditRequest(editTarget ? categoryId : null); } }} createAccountRequest={accountCreateRequest} editAccountRequest={accountEditRequest} focusTransaction={focusTransaction} onPendingChange={setLedgerPending} selectedAccount={selectedAccount} onSelectAccount={setSelectedAccount} onChanged={() => setLedgerRefresh(value => value + 1)} onMenu={() => setMobileMenu(true)} /> : activePage === 'Plan' ? <div className="page-content"><div className="page-heading"><div><span className="eyebrow">YOUR MONEY, WITH A PLAN</span><h1>Plan</h1></div></div>{budgetsLoaded && ownedBudgets.length === 0 ? <section className="empty-state"><p>You do not have an active budget yet.</p><button type="button" onClick={createBudget}>Create your first budget</button></section> : budgetPage}<div className="ledger-plan-status" role="status">{saveState === 'saved' ? 'Plan saved' : saveState === 'saving' ? 'Saving plan…' : saveState === 'loading' ? 'Loading plan…' : saveState === 'conflict' ? 'Resolve the conflicting plan edits' : 'Plan changes could not be saved'} {saveState === 'offline' && <button onClick={() => setBudgetRefresh(value => value + 1)}>Reload saved plan</button>}</div></div> : activePage === 'Wishlist' && selectedBudgetId ? <div className="page-content"><WishlistPage budgetId={selectedBudgetId} currency={plan.currency} access={wishlistAccess} onAccessChange={setWishlistAccess} onRetry={() => setWishlistRefresh(value => value + 1)} onNavigate={page => navigate(page)} /></div> : activePage === 'Long-Term Planning' ? <div className="page-content"><LongTermPlanning accounts={accounts.accounts} onSelectTransaction={(entry, scheduled, splitIndex) => showTransactionInAccount(entry, scheduled, 'Long-Term Planning', splitIndex)} onPendingChange={setPlanningPending} /></div> : <div className="page-content other-page"><div className="page-heading"><div><span className="eyebrow">MY BUDGET</span><h1>{activePage}</h1></div></div><section className="coming-card"><div className="coming-icon"><Icon name={otherPages[activePage].icon} size={24} /></div><h2>{otherPages[activePage].title}</h2><p>{otherPages[activePage].subtitle}</p><div className="coming-preview"><span /><span /><span /></div><button className="button button-primary" onClick={() => setActivePage('Plan')}>Back to your plan <Icon name="right" size={15} /></button><small>This view is queued for the next build slice.</small></section></div>}
      <footer className="footer-note"><span>{saveState === 'loading' ? 'Loading budget…' : saveState === 'saving' ? 'Saving changes…' : saveState === 'conflict' ? 'Budget changed elsewhere' : saveState === 'offline' ? 'Changes not saved' : 'Changes saved to PostgreSQL'}</span><span>OpenBudget <span className="footer-separator">·</span> Local workspace</span></footer>
    </main>

    <button className="help-bubble" aria-label="Help" onClick={() => setToast('Local demo help: edit Assigned values, move money with Fix This, or add a Category Group.')}>?</button>
    {profileOpen && <ProfileSettings onClose={() => setProfileOpen(false)} />}
    {liveUpdates.notice && <div className="live-update-notice" aria-live="polite"><span>Updated elsewhere — reload to see the latest plan.</span><button type="button" onClick={() => void reviewRemotePlanChange()}>Reload</button><button type="button" className="secondary" onClick={liveUpdates.dismissNotice}>Dismiss</button></div>}
    {conflictBudget && <div className="modal-backdrop" role="presentation"><section className="move-modal conflict-modal" role="dialog" aria-modal="true" aria-labelledby="conflict-title"><span className="eyebrow">BUDGET UPDATED ELSEWHERE</span><h2 id="conflict-title">Choose which plan to keep</h2><p className="modal-copy">Another session saved this month while you were editing. Load that version or reapply your edits over it.</p><div className="conflict-actions"><button className="button" onClick={() => resolveConflict(false)}>Load latest</button><button className="button button-primary" onClick={() => resolveConflict(true)}>Reapply my changes</button></div></section></div>}
    {moveOpen && <div className="modal-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) setMoveOpen(false); }}><section className="move-modal move-available-modal" role="dialog" aria-modal="true" aria-labelledby="move-title"><h2 id="move-title">{moveCovering ? 'Cover overspending' : 'Move'}</h2><form onSubmit={applyMove}>{moveCovering && <CategoryPicker label="Move from" categories={moveCategoryOptions.filter(category => category.available > 0 && category.id !== moveTo)} value={moveFrom} onChange={setMoveFrom} includeInflow={false} /> }<label>Amount<MoneyInput autoFocus value={moveAmount} onChange={raw => setMoveAmount(raw)} aria-label="Amount to move" /></label><CategoryPicker label="Move to" categories={moveCategoryOptions.filter(category => category.id !== moveFrom)} value={moveTo} onChange={setMoveTo} includeInflow={false} disabled={moveCovering} /><div className="move-available-actions"><button type="button" onClick={() => setMoveOpen(false)}>Cancel</button><button className="button-primary" type="submit" disabled={!moveFrom || !moveTo || moveTo === moveFrom}>OK</button></div></form></section></div>}
    {categoryDialog && <PlanTransactionList title={categoryDialog.title} items={categoryDialog.items} accounts={accounts.accounts} loading={categoryDialog.loading} error={categoryDialog.error} onClose={closeCategoryDialog} onRetry={categoryDialog.kind === 'activity' ? () => { const category = visibleCategories.find(item => item.id === categoryDialog.categoryId); if (category) void openActivity(category); } : undefined} onSelect={(entry, scheduled, splitIndex) => showTransactionInAccount(entry, scheduled, 'Plan', splitIndex)} />}
    {toast && <div className="toast" role="status"><span className="toast-check"><Icon name="check" size={14} /></span>{toast}<button aria-label="Dismiss notification" onClick={() => setToast('')}><Icon name="close" size={14} /></button></div>}
  </div>;
}

// Chooses the page when it mounts, so a deep link restored after sign-in renders the right screen.
function RoutedPage() {
  return isAdminPath(window.location.pathname) ? <AdminPanel /> : <BudgetApplication />;
}

export default function App() {
  return import.meta.env.MODE === 'test' ? <RoutedPage /> : <AuthGate><RoutedPage /></AuthGate>;
}
