import { useEffect, useMemo, useRef, useState } from 'react';

type Category = {
  id: string;
  name: string;
  assigned: number;
  activity: number;
  available: number;
  target: number;
  icon?: string;
  snoozed?: boolean;
};

type CategoryGroup = { id: string; name: string; categories: Category[] };
type Filter = 'All' | 'Overspent' | 'Underfunded' | 'Overfunded' | 'Money available' | 'Snoozed';
type BudgetState = { month: string; groups: CategoryGroup[]; note?: string };

const filters: Filter[] = ['All', 'Overspent', 'Underfunded', 'Overfunded', 'Money available', 'Snoozed'];

const initialGroups: CategoryGroup[] = [
  {
    id: 'home', name: 'Home', categories: [
      { id: 'rent', name: 'Rent & mortgage', assigned: 1450, activity: -1450, available: 0, target: 1450, icon: '⌂' },
      { id: 'utilities', name: 'Electricity & water', assigned: 180, activity: -132.45, available: 47.55, target: 180, icon: 'ϟ' },
      { id: 'internet', name: 'Internet', assigned: 55, activity: -55, available: 0, target: 55, icon: '⌁' },
    ],
  },
  {
    id: 'daily', name: 'Everyday', categories: [
      { id: 'groceries', name: 'Groceries', assigned: 520, activity: -376.2, available: 143.8, target: 520, icon: '▤' },
      { id: 'transport', name: 'Transport', assigned: 120, activity: -96.5, available: 23.5, target: 120, icon: '↗' },
      { id: 'coffee', name: 'Coffee & lunch', assigned: 90, activity: -42.75, available: 47.25, target: 90, icon: '☕' },
      { id: 'health', name: 'Health & pharmacy', assigned: 75, activity: -92.4, available: -17.4, target: 75, icon: '✚' },
    ],
  },
  {
    id: 'life', name: 'Life & fun', categories: [
      { id: 'dining', name: 'Dining out', assigned: 160, activity: -185.3, available: -25.3, target: 160, icon: '♡' },
      { id: 'entertainment', name: 'Entertainment', assigned: 80, activity: -34.99, available: 45.01, target: 80, icon: '✦' },
      { id: 'clothing', name: 'Clothing', assigned: 100, activity: 0, available: 100, target: 100, icon: '◌' },
    ],
  },
  {
    id: 'future', name: 'True expenses', categories: [
      { id: 'annual', name: 'Annual bills', assigned: 210, activity: 0, available: 790, target: 1000, icon: '◷' },
      { id: 'home-repair', name: 'Home maintenance', assigned: 125, activity: 0, available: 625, target: 750, icon: '⌁' },
      { id: 'travel', name: 'Next trip', assigned: 200, activity: 0, available: 600, target: 1200, icon: '✈' },
    ],
  },
  {
    id: 'savings', name: 'Savings goals', categories: [
      { id: 'buffer', name: 'Emergency fund', assigned: 300, activity: 0, available: 2400, target: 5000, icon: '◉' },
      { id: 'gifts', name: 'Gifts', assigned: 40, activity: 0, available: 160, target: 300, icon: '◇', snoozed: true },
    ],
  },
];

const currency = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
const money = (value: number) => currency.format(value);
const shortMonthText = (value: string) => new Intl.DateTimeFormat('en-GB', { month: 'short', year: 'numeric' }).format(new Date(`${value}-01T12:00:00`));
const amountFromInput = (value: string) => Number(value.replace(',', '.').replace(/[^\d.-]/g, '')) || 0;

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

function App() {
  const [data, setData] = useState<BudgetState>({ month: '2026-10', groups: initialGroups, note: '' });
  const [saveState, setSaveState] = useState<'loading' | 'saved' | 'saving' | 'offline'>('loading');
  const loadedMonth = useRef<string | null>(null);
  const saveController = useRef<AbortController | null>(null);
  const [filter, setFilter] = useState<Filter>('All');
  const [collapsed, setCollapsed] = useState<string[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const [moveOpen, setMoveOpen] = useState(false);
  const [mobileMenu, setMobileMenu] = useState(false);
  const [activePage, setActivePage] = useState('Plan');
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
        setData(budget);
        setSaveState('saved');
      })
      .catch(error => {
        if (error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Could not reach the budget API. Start the backend and reload.');
        }
      });
    return () => controller.abort();
  }, [data.month]);

  useEffect(() => {
    if (loadedMonth.current !== data.month) return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      saveController.current?.abort();
      saveController.current = controller;
      setSaveState('saving');
      fetch(`/api/budget/${data.month}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...data, groups: data.groups.map((group, sortOrder) => ({
          ...group, sortOrder,
          categories: group.categories.map((category, categoryOrder) => ({ ...category, sortOrder: categoryOrder })),
        })) }), signal: controller.signal,
      }).then(response => {
        if (!response.ok) throw new Error(`Budget save failed (${response.status})`);
        setSaveState('saved');
      }).catch(error => {
        if (error.name !== 'AbortError') {
          setSaveState('offline');
          setToast('Changes could not be saved. Check the backend connection.');
        }
      });
    }, 450);
    return () => window.clearTimeout(timer);
  }, [data]);

  const allCategories = useMemo(() => data.groups.flatMap(group => group.categories), [data.groups]);
  const totals = useMemo(() => {
    const assigned = allCategories.reduce((sum, item) => sum + item.assigned, 0);
    const activity = allCategories.reduce((sum, item) => sum + item.activity, 0);
    const available = allCategories.reduce((sum, item) => sum + item.available, 0);
    return { assigned, activity, available };
  }, [allCategories]);
  const targetSum = allCategories.reduce((sum, item) => sum + item.target, 0);
  const leftOver = totals.available - totals.assigned - totals.activity;
  const overspentCount = allCategories.filter(item => item.available < 0).length;

  function updateCategory(id: string, updater: (category: Category) => Category) {
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === id ? updater(category) : category) })) }; });
  }

  function changeMonth(delta: number) {
    const [year, month] = data.month.split('-').map(Number);
    const date = new Date(year, month - 1 + delta, 1);
    setData(current => ({ ...current, month: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}` }));
  }

  function toggleGroup(id: string) {
    setCollapsed(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id]);
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setEditValue(String(category.assigned));
  }

  function saveEdit(category: Category) {
    const next = amountFromInput(editValue);
    const delta = next - category.assigned;
    updateCategory(category.id, item => ({ ...item, assigned: next, available: item.available + delta }));
    setEditingId(null);
    setToast('Assignment updated');
    window.setTimeout(() => setToast(''), 2200);
  }

  function addCategory(groupId: string) {
    const id = `new-${Date.now()}`;
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(group => group.id === groupId ? { ...group, categories: [...group.categories, { id, name: 'New category', assigned: 0, activity: 0, available: 0, target: 0, icon: '＋' }] } : group) }; });
    setToast('Category added');
    window.setTimeout(() => setToast(''), 2200);
  }

  function applyMove(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const amount = amountFromInput(moveAmount);
    if (moveFrom === moveTo || amount <= 0) return;
    const source = allCategories.find(category => category.id === moveFrom);
    if (!source || amount > source.available) {
      setToast('Choose an amount available to move');
      window.setTimeout(() => setToast(''), 2400);
      return;
    }
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: current.groups.map(group => ({ ...group, categories: group.categories.map(category => category.id === moveFrom ? { ...category, assigned: category.assigned - amount, available: category.available - amount } : category.id === moveTo ? { ...category, assigned: category.assigned + amount, available: category.available + amount } : category) })) }; });
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
    const id = `group-${Date.now()}`;
    setData(current => { setHistory(stack => [...stack.slice(-49), current]); setFuture([]); return { ...current, groups: [...current.groups, { id, name: 'New group', categories: [] }] }; });
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

      <section className="ready-card" aria-label="Ready to assign">
        <div className="ready-copy">
          <strong>−{money(Math.abs(3670 - totals.assigned))}</strong>
          <span className="ready-hint">You assigned more than you have</span>
        </div>
        <button className="ready-link" onClick={() => setMoveOpen(true)}>Fix This</button>
      </section>

      <div className="filter-bar">
        <div className="filter-scroll" role="tablist" aria-label="Category filters">
          {filters.map(item => <button key={item} className={`filter-chip ${filter === item ? 'selected' : ''}`} role="tab" aria-selected={filter === item} onClick={() => setFilter(item)}>
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
              const shownCategories = group.categories.filter(matchesFilter);
              if (filter !== 'All' && shownCategories.length === 0) return null;
              const isCollapsed = collapsed.includes(group.id);
              return <div className="category-group" key={group.id}>
                <div className="group-row">
                  <div className="group-name"><button className="group-collapse" aria-label={`Toggle ${group.name}`} aria-expanded={!isCollapsed} onClick={() => toggleGroup(group.id)}><span className={`group-chevron ${isCollapsed ? 'closed' : ''}`}><Icon name="chevron" size={12} /></span></button><input type="checkbox" aria-label={`Select ${group.name}`} checked={group.categories.length > 0 && group.categories.every(category => selectedIds.includes(category.id))} onChange={event => setSelectedIds(current => event.target.checked ? [...new Set([...current, ...group.categories.map(category => category.id)])] : current.filter(id => !group.categories.some(category => category.id === id)))} /><button className="group-label" onDoubleClick={() => { const name = window.prompt('Rename category group', group.name)?.trim(); if(name) setData(current => ({...current,groups:current.groups.map(item => item.id === group.id ? {...item,name} : item)})); }}>{group.name}</button></div>
                  <div className="group-assigned">{money(group.categories.reduce((sum, item) => sum + item.assigned, 0))}</div>
                  <div className="group-activity">{money(group.categories.reduce((sum, item) => sum + item.activity, 0))}</div>
                  <div className={`group-available ${groupTotal < 0 ? 'negative' : ''}`}>{money(groupTotal)}</div>
                  <button className="group-add" onClick={() => addCategory(group.id)} aria-label={`Add category to ${group.name}`}><Icon name="plus" size={16} /></button>
                </div>
                {!isCollapsed && shownCategories.map(category => {
                  const progress = category.target > 0 ? Math.min(100, Math.max(0, Math.round(category.available / category.target * 100))) : 0;
                  return <div className={`category-row ${category.available < 0 ? 'is-overspent' : ''}`} key={category.id}>
                    <div className="category-name-cell"><input type="checkbox" aria-label={`Select ${category.name}`} checked={selectedIds.includes(category.id)} onChange={event => setSelectedIds(current => event.target.checked ? [...current, category.id] : current.filter(id => id !== category.id))} /><div className="category-copy"><div className="category-label-line"><button className="category-name" title="Double-click to rename" onDoubleClick={() => { const name = window.prompt('Rename category', category.name)?.trim(); if(name) updateCategory(category.id, item => ({...item,name})); }}>{category.name}</button>{progressOn && <span className="category-status">{category.available < 0 ? 'Overspent' : category.snoozed ? 'Snoozed' : category.available === 0 && category.activity < 0 ? 'Fully Spent' : ''}</span>}</div>{progressOn && category.target > 0 && <div className={`progress-track ${category.available < 0 ? 'over' : category.assigned >= category.target ? 'funded' : ''}`}><span style={{width: `${category.available < 0 ? 100 : category.assigned >= category.target ? 100 : progress}%`}} /></div>}</div></div>
                    <div className="assigned-cell">
                      {editingId === category.id ? <input className="amount-input" autoFocus value={editValue} onChange={event => setEditValue(event.target.value)} onBlur={() => saveEdit(category)} onKeyDown={event => { if (event.key === 'Enter') saveEdit(category); if (event.key === 'Escape') setEditingId(null); }} aria-label={`Assigned to ${category.name}`} /> : <button className="amount-button" onClick={() => startEdit(category)} aria-label={`Edit assigned for ${category.name}`}>{money(category.assigned)}</button>}

                    </div>
                    <div className={`activity-cell ${category.activity < 0 ? 'negative' : ''}`}>{money(category.activity)}</div>
                    <div className={`available-cell ${category.available < 0 ? 'negative' : category.available > 0 ? 'positive' : ''}`}><span>{money(category.available)}</span></div>
                  </div>;
                })}
                {!isCollapsed && shownCategories.length === 0 && <div className="empty-group">No categories match this filter.</div>}
              </div>;
            })}
            {filter !== 'All' && allCategories.filter(matchesFilter).length === 0 && <div className="empty-state"><div className="empty-state-icon"><Icon name="check" /></div><strong>All clear</strong><span>There are no categories in this view.</span><button onClick={() => setFilter('All')}>Show all categories</button></div>}
            <button className="add-group" onClick={addGroup}><span><Icon name="plus" size={16} /></span> Add category group</button>
          </div>
        </section>

        <aside className="summary-column">
          <section className="summary-card"><button className="summary-title" onClick={() => setToast('Monthly summary')}><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}'s Summary</span><Icon name="chevron" size={15} /></button><div className="summary-body">
            <div className="summary-line"><span>Left Over from Last Month</span><strong>{money(leftOver)}</strong></div><div className="summary-line"><span>Assigned in {new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}</span><strong>{money(totals.assigned)}</strong></div><div className="summary-line"><span>Activity</span><strong>{money(totals.activity)}</strong></div><div className="summary-line summary-total"><span>Available</span><strong>{money(totals.available)}</strong></div>
            <div className="summary-rule" /><div className="summary-subhead">Cost to Be Me</div><div className="summary-line"><span>{new Date(data.month+'-01T12:00:00').toLocaleDateString('en-GB',{month:'long'})}’s Targets</span><strong>{money(targetSum)}</strong></div><div className="target-progress"><span style={{width:'14%'}} /></div><div className="summary-line"><span>Expected Income</span><strong>{money(12500)} <button className="edit-income" aria-label="Edit income" onClick={() => setToast('Income settings')}>↗</button></strong></div><div className="income-progress"/><div className="summary-footnote">Next month's targets could increase your total to {money(targetSum)}</div></div></section>
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
      <button className="budget-picker"><img src="/ynab-logo.svg" alt="" /><span className="budget-picker-copy"><strong>🏡 My Budget</strong><small>alex@example.com</small></span><Icon name="chevron" size={15} /></button>
      <div className="side-nav" role="navigation" aria-label="Main navigation">
        <button className={`nav-item ${activePage === 'Plan' ? 'active' : ''}`} onClick={() => { setActivePage('Plan'); setMobileMenu(false); }}><Icon name="plan" size={22} /><span>Plan</span></button>
        <button className={`nav-item ${activePage === 'Reflect' ? 'active' : ''}`} onClick={() => setToast('Reflect is outside this demo’s scope')}><Icon name="chart" /><span>Reflect</span></button>
        <button className={`nav-item ${activePage === 'Accounts' ? 'active' : ''}`} onClick={() => { setActivePage('Accounts'); setMobileMenu(false); }}><Icon name="bank" size={22} /><span>All Accounts</span></button>
      </div>
      <div className="account-section">
        
        <div className="account-type"><span className="account-type-icon cash-icon">$</span><span>CASH</span><strong>{money(3640.82)}</strong></div>
        <button className="account-row" onClick={() => setActivePage('Accounts')}><span className="account-dot checking" /><span>Everyday checking</span><strong>{money(2812.4)}</strong></button>
        <button className="account-row" onClick={() => setActivePage('Accounts')}><span className="account-dot savings" /><span>Rainy day savings</span><strong>{money(828.42)}</strong></button>
        <div className="account-type"><span className="account-type-icon credit-icon">↗</span><span>CREDIT</span><strong className="debt-amount">−{money(684.2)}</strong></div>
        <button className="account-row" onClick={() => setActivePage('Accounts')}><span className="account-dot card" /><span>Everyday card</span><strong>−{money(684.2)}</strong></button>
        <div className="account-type"><span className="account-type-icon tracking-icon">↗</span><span>LOANS</span><strong>{money(0)}</strong></div>
        <button className="account-row" onClick={() => setActivePage('Accounts')}><span className="account-dot tracking" /><span>Long-term loan</span><strong>−{money(8400)}</strong></button>
      </div>
      <div className="sidebar-bottom"><div className="referral-banner"><span>Give your friends 6 free weeks to try YNAB!</span><img src="/referral-heart.svg" alt="" /><button aria-label="Dismiss referral" onClick={event => {event.currentTarget.parentElement?.classList.add('dismissed');}}>×</button></div><div className="sidebar-footer"><button onClick={() => setToast('Sharing is not connected in this demo')}>▦ &nbsp; Share & Earn</button><button aria-label="Collapse sidebar" onClick={() => setMobileMenu(value => !value)}>◧</button></div></div>
    </aside>

    <main className="main-area">
      <header className="topbar"><div className="breadcrumbs"><span>My Budget</span><Icon name="right" size={13} /><strong>{activePage}</strong></div><div className="topbar-right"><button className="share-button" onClick={() => setToast('Sharing is not connected in this local demo')}>Share</button><button className="top-avatar" aria-label="Profile">A</button></div></header>
      {activePage === 'Plan' ? <div className="page-content"><div className="page-heading"><div><span className="eyebrow">YOUR MONEY, WITH A PLAN</span><h1>Plan</h1></div></div>{budgetPage}</div> : <div className="page-content other-page"><div className="page-heading"><div><span className="eyebrow">MY BUDGET</span><h1>{activePage}</h1></div></div><section className="coming-card"><div className="coming-icon"><Icon name={otherPages[activePage].icon} size={24} /></div><h2>{otherPages[activePage].title}</h2><p>{otherPages[activePage].subtitle}</p><div className="coming-preview"><span /><span /><span /></div><button className="button button-primary" onClick={() => setActivePage('Plan')}>Back to your plan <Icon name="right" size={15} /></button><small>This view is queued for the next build slice.</small></section></div>}
      <footer className="footer-note"><span>{saveState === 'loading' ? 'Loading budget…' : saveState === 'saving' ? 'Saving changes…' : saveState === 'offline' ? 'Backend unavailable' : 'Changes saved to PostgreSQL'}</span><span>OpenBudget <span className="footer-separator">·</span> Local workspace</span></footer>
    </main>

    <button className="help-bubble" aria-label="Help" onClick={() => setToast('Local demo help: edit Assigned values, move money with Fix This, or add a Category Group.')}>?</button>
    {moveOpen && <div className="modal-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) setMoveOpen(false); }}><section className="move-modal" role="dialog" aria-modal="true" aria-labelledby="move-title"><div className="modal-heading"><div className="modal-icon"><Icon name="transfer" /></div><button className="icon-button" aria-label="Close move money dialog" onClick={() => setMoveOpen(false)}><Icon name="close" /></button></div><span className="eyebrow">GIVE YOUR MONEY A NEW JOB</span><h2 id="move-title">Move money</h2><p className="modal-copy">Shift money between categories. Your total plan stays the same.</p><form onSubmit={applyMove}><label>Move from<select value={moveFrom} onChange={event => setMoveFrom(event.target.value)}>{allCategories.map(category => <option key={category.id} value={category.id}>{category.name} · {money(category.available)}</option>)}</select></label><div className="modal-arrow"><Icon name="arrow" /></div><label>Move to<select value={moveTo} onChange={event => setMoveTo(event.target.value)}>{allCategories.map(category => <option key={category.id} value={category.id}>{category.name} · {money(category.available)}</option>)}</select></label><label className="amount-label">Amount<div className="modal-amount"><span>€</span><input autoFocus inputMode="decimal" value={moveAmount} onChange={event => setMoveAmount(event.target.value)} aria-label="Amount to move" /></div></label><button className="button button-primary modal-submit" type="submit">Move {money(amountFromInput(moveAmount))}</button></form></section></div>}
    {toast && <div className="toast" role="status"><span className="toast-check"><Icon name="check" size={14} /></span>{toast}<button aria-label="Dismiss notification" onClick={() => setToast('')}><Icon name="close" size={14} /></button></div>}
  </div>;
}

export default App;
