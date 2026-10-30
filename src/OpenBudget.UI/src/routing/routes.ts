// URL routing for the budget application: pure parse/format/resolve helpers (no React, no browser history).
// See docs/architecture.md "URL routing" for the route table and the budget-key / account-slug rules.
import { base64Url, sha256 } from './sha256';

/** Page names used by App.tsx `activePage`. */
export type AppPage = 'Plan' | 'Long-Term Planning' | 'Wishlist' | 'Reflect' | 'Accounts' | 'Budget Together';

export type BudgetRoute = {
  kind: 'budget';
  budgetKey: string;
  page: AppPage;
  /** Plan only: viewed month (YYYY-MM). Omitted = the app's default month. */
  month?: string;
  /** Long-Term Planning only: viewed year. Omitted = the current year. */
  year?: number;
  /** Accounts only: 'all' or an account slug (legacy routes may carry a raw account id). */
  account?: string;
};

export type AppRoute =
  | BudgetRoute
  | { kind: 'root'; legacy?: { page: AppPage; account?: string } }
  | { kind: 'admin' }
  | { kind: 'public'; path: string }
  | { kind: 'unknown'; budgetKey?: string };

const pageSegments: Record<AppPage, string> = {
  Plan: 'plan',
  'Long-Term Planning': 'long-term',
  Wishlist: 'wishlist',
  Reflect: 'reflect',
  Accounts: 'account',
  'Budget Together': 'together',
};
const pagesBySegment = new Map(Object.entries(pageSegments).map(([page, segment]) => [segment, page as AppPage]));

/** Signed-out pages served by AuthGate (email links point at /confirm-email and /reset-password). */
export const publicPaths = ['/login', '/register', '/forgot-password', '/reset-password', '/confirm-email'];
/** First path segments that can never be a budget key. /api, /health and /mcp belong to the API, never the SPA. */
const reservedSegments = new Set(['admin', 'settings', 'api', 'health', 'mcp', 'assets', ...publicPaths.map(path => path.slice(1))]);

const budgetKeyPattern = /^[A-Za-z0-9_-]{1,64}$/;
const monthPattern = /^\d{4}-(0[1-9]|1[0-2])$/;
const yearPattern = /^\d{4}$/;

export const isAdminPath = (pathname: string) => pathname.replace(/\/+$/, '') === '/admin';

function decodeSegment(segment: string) {
  try { return decodeURIComponent(segment); } catch { return segment; }
}

function legacyPage(value: string | null): AppPage | undefined {
  return value && value in pageSegments ? value as AppPage : undefined;
}

/** Parses a browser path (and the legacy `?page=&account=` query on `/`) into a route. */
export function parseRoute(pathname: string, search = ''): AppRoute {
  const trimmed = pathname.length > 1 ? pathname.replace(/\/+$/, '') : pathname;
  if (trimmed === '' || trimmed === '/') {
    const query = new URLSearchParams(search);
    const page = legacyPage(query.get('page'));
    return page ? { kind: 'root', legacy: { page, account: query.get('account') || undefined } } : { kind: 'root' };
  }
  if (isAdminPath(trimmed)) return { kind: 'admin' };
  if (publicPaths.includes(trimmed)) return { kind: 'public', path: trimmed };
  const segments = trimmed.slice(1).split('/').map(decodeSegment);
  const [budgetKey, pageSegment, detail, ...rest] = segments;
  if (!budgetKeyPattern.test(budgetKey) || reservedSegments.has(budgetKey.toLowerCase())) return { kind: 'unknown' };
  if (pageSegment === undefined) return { kind: 'budget', budgetKey, page: 'Plan' };
  const page = pagesBySegment.get(pageSegment.toLowerCase());
  if (!page || rest.length > 0) return { kind: 'unknown', budgetKey };
  if (detail === undefined) return page === 'Accounts' ? { kind: 'budget', budgetKey, page, account: 'all' } : { kind: 'budget', budgetKey, page };
  if (page === 'Plan' && monthPattern.test(detail)) return { kind: 'budget', budgetKey, page, month: detail };
  if (page === 'Long-Term Planning' && yearPattern.test(detail) && Number(detail) >= 1) return { kind: 'budget', budgetKey, page, year: Number(detail) };
  if (page === 'Accounts' && detail.trim()) return { kind: 'budget', budgetKey, page, account: detail.toLowerCase() };
  return { kind: 'unknown', budgetKey };
}

/** Formats a budget route as its canonical lowercase path. */
export function formatRoute(route: BudgetRoute): string {
  const base = `/${route.budgetKey}/${pageSegments[route.page]}`;
  if (route.page === 'Plan' && route.month) return `${base}/${route.month}`;
  if (route.page === 'Long-Term Planning' && route.year) return `${base}/${route.year}`;
  if (route.page === 'Accounts') return `${base}/${encodeURIComponent(route.account || 'all')}`;
  return base;
}

/**
 * Opaque, stable, URL-safe budget key: the first 11 base64url characters (66 bits) of SHA-256("openbudget:budget:" + id).
 * It is obfuscation for clean URLs, not a security boundary: the API authorizes every budget request.
 */
export function budgetKeyFor(budgetId: string): string {
  return base64Url(sha256(`openbudget:budget:${budgetId}`)).slice(0, 11);
}

/** Resolves a key against the budgets the signed-in user can already see. */
export function findBudgetByKey<T extends { id: string }>(budgets: readonly T[], budgetKey: string): T | undefined {
  return budgets.find(budget => budgetKeyFor(budget.id) === budgetKey);
}

/** Budget id named by the current address-bar path, if it resolves within the given list. */
export function budgetIdFromLocation(budgets: readonly { id: string }[], pathname = window.location.pathname): string | undefined {
  const route = parseRoute(pathname);
  return route.kind === 'budget' || (route.kind === 'unknown' && route.budgetKey) ? findBudgetByKey(budgets, route.budgetKey!)?.id : undefined;
}

/**
 * Page to render before the routing hook has resolved the location (keeps the first paint on the right page).
 * Wishlist and Budget Together need a resolved budget id to render, so they start on Plan until resolution.
 */
export function initialPageFromLocation(pathname = window.location.pathname, search = window.location.search): AppPage {
  const route = parseRoute(pathname, search);
  const page = route.kind === 'budget' ? route.page : route.kind === 'root' && route.legacy ? route.legacy.page : 'Plan';
  return page === 'Wishlist' || page === 'Budget Together' ? 'Plan' : page;
}

/** Plan month requested by the address bar, so the first month load already asks for it. */
export function monthFromLocation(pathname = window.location.pathname): string | undefined {
  const route = parseRoute(pathname);
  return route.kind === 'budget' && route.page === 'Plan' ? route.month : undefined;
}

/** Lowercase ASCII words joined by hyphens; diacritics are folded ("Épargne" -> "epargne"), other scripts are dropped. */
export function slugify(name: string): string {
  const slug = name.normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase()
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 48).replace(/-+$/g, '');
  return slug || 'account';
}

const idToken = (id: string) => id.toLowerCase().replace(/[^a-z0-9]/g, '');

/**
 * Account slugs, unique within a budget. A name shared by several accounts (or one whose slug would be "all")
 * gets "-" plus the shortest unique prefix (min. 4 chars) of its account id, e.g. "mastercard-3f9a".
 */
export function accountSlugs(accounts: readonly { id: string; name: string }[]): Map<string, string> {
  const bases = accounts.map(account => ({ account, base: slugify(account.name) }));
  const counts = new Map<string, number>();
  bases.forEach(({ base }) => counts.set(base, (counts.get(base) ?? 0) + 1));
  const tokens = accounts.map(account => idToken(account.id));
  const slugs = new Map<string, string>();
  bases.forEach(({ account, base }, index) => {
    if (counts.get(base) === 1 && base !== 'all') { slugs.set(account.id, base); return; }
    const token = tokens[index];
    let length = Math.min(4, token.length);
    while (length < token.length && tokens.some((other, otherIndex) => otherIndex !== index && other.startsWith(token.slice(0, length)))) length++;
    slugs.set(account.id, `${base}-${token.slice(0, length) || 'x'}`);
  });
  return slugs;
}

export function accountSlugFor(accounts: readonly { id: string; name: string }[], accountId: string): string | undefined {
  return accountSlugs(accounts).get(accountId);
}

/**
 * Finds the account a slug names: its current slug, its raw id (legacy links), or any "<words>-<id prefix>" whose
 * prefix (4+ chars) matches exactly one account id, so suffixed links keep working after a rename. Closed accounts resolve.
 */
export function resolveAccountSlug(accounts: readonly { id: string; name: string }[], slug: string): string | undefined {
  const wanted = slug.toLowerCase();
  for (const [id, current] of accountSlugs(accounts)) if (current === wanted) return id;
  const byId = accounts.find(account => account.id.toLowerCase() === wanted);
  if (byId) return byId.id;
  const suffix = /-([a-z0-9]{4,})$/.exec(wanted)?.[1];
  if (!suffix) return undefined;
  const matches = accounts.filter(account => idToken(account.id).startsWith(suffix));
  return matches.length === 1 ? matches[0].id : undefined;
}

/** What the app should show for a route. Month/year are left undefined when the route uses the default. */
export type RouteTarget = { budgetId: string; page: AppPage; accountId: string; month?: string; year?: number };
export type RouteResolution = { target: RouteTarget; notice?: string; pendingAccount?: string };

export const routeNotices = {
  unknownPage: 'That page does not exist. Showing your plan.',
  budgetNotFound: 'That budget was not found or you no longer have access. Showing your current plan.',
  accountNotFound: 'That account was not found. Showing all accounts.',
};

/**
 * Resolves a route against what the user can access. Unknown budgets/pages fall back to `fallbackBudgetId`'s Plan.
 * `accounts` is null while the target budget's accounts are not loaded; an account slug is then returned as `pendingAccount`.
 */
export function resolveRoute(route: AppRoute, context: { budgets: readonly { id: string }[]; fallbackBudgetId: string; accounts: readonly { id: string; name: string }[] | null }): RouteResolution {
  const fallback = (notice?: string, budgetId = context.fallbackBudgetId): RouteResolution => ({ target: { budgetId, page: 'Plan', accountId: '' }, notice });
  if (route.kind === 'root') {
    if (!route.legacy) return fallback();
    return withAccount({ budgetId: context.fallbackBudgetId, page: route.legacy.page, accountId: '' }, route.legacy.account, context.accounts);
  }
  if (route.kind === 'admin' || route.kind === 'public') return fallback();
  const budget = route.budgetKey ? findBudgetByKey(context.budgets, route.budgetKey) : undefined;
  if (route.kind === 'unknown') return fallback(routeNotices.unknownPage, budget?.id);
  if (!budget) return fallback(routeNotices.budgetNotFound);
  const target: RouteTarget = { budgetId: budget.id, page: route.page, accountId: '', month: route.month, year: route.year };
  return route.page === 'Accounts' && route.account !== 'all' ? withAccount(target, route.account, context.accounts) : { target };
}

function withAccount(target: RouteTarget, account: string | undefined, accounts: readonly { id: string; name: string }[] | null): RouteResolution {
  if (!account || target.page !== 'Accounts') return { target };
  if (!accounts) return { target, pendingAccount: account };
  const accountId = resolveAccountSlug(accounts, account);
  return accountId ? { target: { ...target, accountId } } : { target, notice: routeNotices.accountNotFound };
}

/** Canonical path for the app state, or null while it cannot be expressed yet (e.g. the account list is still loading). */
export function pathForState(state: { budgetId: string; page: string; accountId: string; month: string; defaultMonth: string; year: number; defaultYear: number; accounts: readonly { id: string; name: string }[] | null }): string | null {
  if (!state.budgetId || !(state.page in pageSegments)) return null;
  const route: BudgetRoute = { kind: 'budget', budgetKey: budgetKeyFor(state.budgetId), page: state.page as AppPage };
  if (route.page === 'Plan' && state.month !== state.defaultMonth) route.month = state.month;
  if (route.page === 'Long-Term Planning' && state.year !== state.defaultYear) route.year = state.year;
  if (route.page === 'Accounts') {
    if (!state.accountId) route.account = 'all';
    else {
      const slug = state.accounts ? accountSlugFor(state.accounts, state.accountId) : undefined;
      if (!slug) return null;
      route.account = slug;
    }
  }
  return formatRoute(route);
}

/** Browser tab title for the app state, e.g. "Plan · Household · OpenBudget". */
export function titleForState(state: { page: string; budgetName: string; accountName?: string }): string {
  const page = state.page === 'Accounts' ? state.accountName || 'All Accounts' : state.page === 'Long-Term Planning' ? 'Long-Term' : state.page;
  return [page, state.budgetName, 'OpenBudget'].filter(Boolean).join(' · ');
}

const returnPathKey = 'openbudget.returnTo';

/** Only same-origin app paths may be used as a post-sign-in destination (no protocol-relative/backslash/API/auth paths). */
export function isSafeReturnPath(value: string | null | undefined): value is string {
  if (!value || value.length > 512 || !value.startsWith('/') || value.startsWith('//') || /[\\\u0000-\u001f]/.test(value)) return false;
  const pathname = value.split(/[?#]/, 1)[0];
  if (pathname === '/' || publicPaths.includes(pathname.replace(/\/+$/, ''))) return false;
  return !/^\/(api|health|mcp)(\/|$)/i.test(pathname);
}

/** Remembers the deep link a signed-out visitor requested so AuthGate can return there after sign-in. */
export function rememberReturnPath(path: string) {
  if (!isSafeReturnPath(path)) return;
  try { window.sessionStorage.setItem(returnPathKey, path); } catch { /* storage unavailable: sign-in lands on / */ }
}

export function takeReturnPath(): string | undefined {
  try {
    const value = window.sessionStorage.getItem(returnPathKey);
    window.sessionStorage.removeItem(returnPathKey);
    return isSafeReturnPath(value) ? value : undefined;
  } catch { return undefined; }
}
