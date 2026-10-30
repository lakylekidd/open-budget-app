// Keeps the address bar and the budget application's page state in sync (history.pushState/replaceState + popstate).
// App.tsx stays the owner of page/account/month/budget state; this hook resolves URLs into that state and back.
import { useEffect, useRef, useState } from 'react';
import { longTermYearStore, useRoutedLongTermYear } from './longTermYear';
import { budgetSlugs, findBudgetBySegment, parseRoute, pathForState, resolveAccountSlug, resolveRoute, routeNotices, titleForState, type RouteTarget } from './routes';

type Named = { id: string; name: string };

export type AppRouterOptions = {
  budgets: readonly Named[];
  budgetsLoaded: boolean;
  selectedBudgetId: string;
  budgetName: string;
  /** Accounts of the selected budget, or null while they are loading. */
  accounts: readonly Named[] | null;
  page: string;
  accountId: string;
  month: string;
  /** Applies a resolved URL to the app. `initial` skips leave-guards; return false when a guard blocks the change. */
  applyRoute: (target: RouteTarget, options: { initial: boolean }) => boolean;
  notify: (message: string) => void;
};

const currentPath = () => `${window.location.pathname}${window.location.search}`;

export function useAppRouter(options: AppRouterOptions) {
  // `/{key}/plan` without a month means the month the app opens on; `/{key}/long-term` the current year.
  const defaultMonth = useRef(options.month).current;
  const defaultYear = useRef(new Date().getFullYear()).current;
  // The Long-Term Planning year lives in a tiny shared store so LongTermPlanning can follow it without new props.
  const yearFromUrl = () => { const route = parseRoute(window.location.pathname); return route.kind === 'budget' && route.year ? route.year : defaultYear; };
  // Seed before children render so a deep-linked year is the first one Long-Term Planning loads.
  useState(() => { if (longTermYearStore.get() === null) longTermYearStore.set(yearFromUrl()); });
  const routedYear = useRoutedLongTermYear();
  const longTermYear = routedYear ?? defaultYear;
  const setLongTermYear = longTermYearStore.set;
  useEffect(() => {
    if (longTermYearStore.get() === null) longTermYearStore.set(yearFromUrl());
    return () => longTermYearStore.set(null);
  }, []);
  const [resolved, setResolved] = useState(false);
  const resolvedRef = useRef(resolved);
  resolvedRef.current = resolved;
  const [location, setLocation] = useState(currentPath);
  // An account slug waiting for its budget's account list (deep link or back/forward into another budget).
  const [pendingAccount, setPendingAccount] = useState<string | undefined>();
  const replaceNext = useRef(true);
  const syncedPath = useRef<string | null>(null);
  const latest = useRef(options);
  latest.current = options;

  // Budget slugs follow budget names, so a rename (or a new budget with the same name) changes them. Remember the slugs a
  // visible budget used earlier in this session so history entries from before the rename still resolve on back/forward.
  const budgetSlugById = budgetSlugs(options.budgets);
  const previousSlugs = useRef(budgetSlugById);
  const budgetAliases = useRef(new Map<string, string>());
  if (previousSlugs.current !== budgetSlugById) {
    previousSlugs.current.forEach((slug, id) => { const current = budgetSlugById.get(id); if (current && current !== slug) budgetAliases.current.set(slug, id); });
    previousSlugs.current = budgetSlugById;
  }
  const selectedBudgetSlug = budgetSlugById.get(options.selectedBudgetId);
  // The slug this hook last wrote for the selected budget; a change while the budget stays selected is a rename.
  const syncedBudget = useRef<{ id: string; slug: string | undefined } | null>(null);

  const withDefaults = (target: RouteTarget): RouteTarget => ({
    ...target,
    month: target.page === 'Plan' ? target.month ?? defaultMonth : undefined,
    year: target.page === 'Long-Term Planning' ? target.year ?? defaultYear : undefined,
  });

  function apply(target: RouteTarget, initial: boolean) {
    const complete = withDefaults(target);
    if (!latest.current.applyRoute(complete, { initial })) return false;
    if (complete.year !== undefined) setLongTermYear(complete.year);
    return true;
  }

  // First resolution once the budget list (and, for account links, the account list) is available.
  useEffect(() => {
    if (resolved || !options.budgetsLoaded || !options.selectedBudgetId) return;
    const route = parseRoute(window.location.pathname, window.location.search);
    const resolution = resolveRoute(route, { budgets: options.budgets, fallbackBudgetId: options.selectedBudgetId, accounts: options.accounts, budgetAliases: budgetAliases.current });
    if (resolution.pendingAccount) return;
    replaceNext.current = true;
    apply(resolution.target, true);
    if (resolution.notice) options.notify(resolution.notice);
    setResolved(true);
  });

  // Completes a back/forward navigation into an account of another budget once that budget's accounts load.
  useEffect(() => {
    if (!pendingAccount || !options.accounts) return;
    const accountId = resolveAccountSlug(options.accounts, pendingAccount);
    setPendingAccount(undefined);
    replaceNext.current = true;
    apply({ budgetId: options.selectedBudgetId, page: 'Accounts', accountId: accountId ?? '' }, true);
    if (!accountId) options.notify(routeNotices.accountNotFound);
  });

  useEffect(() => {
    const onPopState = () => {
      const route = parseRoute(window.location.pathname, window.location.search);
      if (route.kind === 'admin') { window.location.reload(); return; }
      if (!resolvedRef.current) return;
      const state = latest.current;
      const sameBudget = route.kind === 'budget' && findBudgetBySegment(state.budgets, route.budgetKey, budgetAliases.current)?.id === state.selectedBudgetId;
      const resolution = resolveRoute(route, { budgets: state.budgets, fallbackBudgetId: state.selectedBudgetId, accounts: sameBudget ? state.accounts : null, budgetAliases: budgetAliases.current });
      if (!apply(resolution.target, false)) {
        // A leave-guard blocked the change: put the address bar back to what is still on screen.
        const restore = syncedPath.current ?? '/';
        window.history.pushState(null, '', restore);
        setLocation(restore);
        return;
      }
      replaceNext.current = true;
      setPendingAccount(resolution.pendingAccount);
      setLocation(currentPath());
      if (resolution.notice) state.notify(resolution.notice);
    };
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  const statePath = pathForState({
    budgetId: options.selectedBudgetId, budgets: options.budgets, page: options.page, accountId: options.accountId, month: options.month, defaultMonth,
    year: longTermYear, defaultYear, accounts: options.accounts,
  });

  // State -> URL: push a history entry for user navigation; replace after initial resolution and back/forward.
  useEffect(() => {
    if (!resolved || pendingAccount || statePath === null) return;
    syncedPath.current = statePath;
    // Renaming the open budget changes its slug: rewrite the URL in place so the user stays on the same page.
    const renamed = syncedBudget.current?.id === options.selectedBudgetId && syncedBudget.current.slug !== selectedBudgetSlug;
    syncedBudget.current = { id: options.selectedBudgetId, slug: selectedBudgetSlug };
    if (renamed) replaceNext.current = true;
    if (statePath === currentPath()) { replaceNext.current = false; return; }
    if (replaceNext.current) window.history.replaceState(null, '', statePath);
    else window.history.pushState(null, '', statePath);
    replaceNext.current = false;
    setLocation(statePath);
  }, [resolved, pendingAccount, statePath, location]);

  const accountName = options.accounts?.find(account => account.id === options.accountId)?.name;
  useEffect(() => {
    document.title = titleForState({ page: options.page, budgetName: options.budgetsLoaded ? options.budgetName : '', accountName });
  }, [options.page, options.budgetName, options.budgetsLoaded, accountName]);
}
