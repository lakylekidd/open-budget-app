import { afterEach, describe, expect, it } from 'vitest';
import {
  accountSlugs, budgetIdFromLocation, budgetKeyFor, budgetSlugFor, budgetSlugs, findBudgetByKey, findBudgetBySegment, formatRoute, initialPageFromLocation, isSafeReturnPath, monthFromLocation,
  parseRoute, pathForState, rememberReturnPath, resolveAccountSlug, resolveRoute, routeNotices, slugify, takeReturnPath, titleForState,
  type BudgetRoute,
} from './routes';
import { base64Url, sha256 } from './sha256';

const hex = (bytes: Uint8Array) => Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
const budgets = [{ id: 'default', name: 'Household' }, { id: '6f1c2a7e-0d4b-4b7e-9a35-2b6f0c1d9e88', name: 'Travel' }];
const key = budgetKeyFor('default');
const slug = 'household';

describe('sha256', () => {
  it('matches the FIPS 180-4 test vectors', () => {
    expect(hex(sha256(''))).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
    expect(hex(sha256('abc'))).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    expect(hex(sha256('abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq'))).toBe('248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1');
  });

  it('hashes multi-block and non-ASCII input as UTF-8', () => {
    expect(hex(sha256('a'.repeat(1000)))).toBe('41edece42d63e8d9bf515a9ba6932e1c20cbc9f5a5d134645adb5db1b9737ea3');
    expect(hex(sha256('€'))).toBe(hex(sha256('€')));
  });

  it('encodes base64url without padding', () => {
    expect(base64Url(new Uint8Array([0xfb, 0xff, 0xfe]))).toBe('-__-');
    expect(base64Url(new Uint8Array([1]))).toBe('AQ');
  });
});

describe('legacy budget keys', () => {
  it('derives a short, stable, URL-safe key that does not expose the budget id', () => {
    expect(key).toMatch(/^[A-Za-z0-9_-]{11}$/);
    expect(budgetKeyFor('default')).toBe(key);
    expect(budgetKeyFor(budgets[1].id)).not.toBe(key);
    expect(budgetKeyFor(budgets[1].id)).not.toContain('6f1c');
    expect(key).toBe(base64Url(sha256('openbudget:budget:default')).slice(0, 11));
  });

  it('resolves keys only against budgets the user can see', () => {
    expect(findBudgetByKey(budgets, budgetKeyFor(budgets[1].id))).toBe(budgets[1]);
    expect(findBudgetByKey(budgets, budgetKeyFor('someone-elses-budget'))).toBeUndefined();
    expect(findBudgetByKey([], key)).toBeUndefined();
  });

  it('reads the budget from the address bar', () => {
    expect(budgetIdFromLocation(budgets, `/${budgetKeyFor(budgets[1].id)}/account/all`)).toBe(budgets[1].id);
    expect(budgetIdFromLocation(budgets, `/${key}/nonsense`)).toBe('default');
    expect(budgetIdFromLocation(budgets, '/household/plan')).toBe('default');
    expect(budgetIdFromLocation(budgets, '/travel/nonsense')).toBe(budgets[1].id);
    expect(budgetIdFromLocation(budgets, '/AAAAAAAAAAA/plan')).toBeUndefined();
    expect(budgetIdFromLocation(budgets, '/')).toBeUndefined();
  });
});

describe('parseRoute / formatRoute', () => {
  const cases: [string, BudgetRoute][] = [
    [`/${key}/plan`, { kind: 'budget', budgetKey: key, page: 'Plan' }],
    [`/${key}/plan/2026-11`, { kind: 'budget', budgetKey: key, page: 'Plan', month: '2026-11' }],
    [`/${key}/long-term`, { kind: 'budget', budgetKey: key, page: 'Long-Term Planning' }],
    [`/${key}/long-term/2027`, { kind: 'budget', budgetKey: key, page: 'Long-Term Planning', year: 2027 }],
    [`/${key}/wishlist`, { kind: 'budget', budgetKey: key, page: 'Wishlist' }],
    [`/${key}/reflect`, { kind: 'budget', budgetKey: key, page: 'Reflect' }],
    [`/${key}/account/all`, { kind: 'budget', budgetKey: key, page: 'Accounts', account: 'all' }],
    [`/${key}/account/mastercard-3f9a`, { kind: 'budget', budgetKey: key, page: 'Accounts', account: 'mastercard-3f9a' }],
    [`/${key}/together`, { kind: 'budget', budgetKey: key, page: 'Budget Together' }],
  ];

  it.each(cases)('round-trips %s', (path, route) => {
    expect(parseRoute(path)).toEqual(route);
    expect(formatRoute(route)).toBe(path);
  });

  it('normalises trailing slashes, page case and the bare budget path', () => {
    expect(parseRoute(`/${key}/PLAN/`)).toEqual({ kind: 'budget', budgetKey: key, page: 'Plan' });
    expect(parseRoute(`/${key}`)).toEqual({ kind: 'budget', budgetKey: key, page: 'Plan' });
    expect(parseRoute(`/${key}/account`)).toEqual({ kind: 'budget', budgetKey: key, page: 'Accounts', account: 'all' });
    expect(parseRoute(`/${key}/account/Visa`)).toMatchObject({ account: 'visa' });
  });

  it('recognises root, admin, public auth pages and legacy query links', () => {
    expect(parseRoute('/')).toEqual({ kind: 'root' });
    expect(parseRoute('/admin')).toEqual({ kind: 'admin' });
    expect(parseRoute('/admin/')).toEqual({ kind: 'admin' });
    for (const path of ['/login', '/register', '/forgot-password', '/reset-password', '/confirm-email']) expect(parseRoute(path)).toEqual({ kind: 'public', path });
    expect(parseRoute('/', '?page=Accounts&account=abc')).toEqual({ kind: 'root', legacy: { page: 'Accounts', account: 'abc' } });
    expect(parseRoute('/', '?page=Bogus')).toEqual({ kind: 'root' });
  });

  it('rejects unknown pages, invalid months/years, reserved segments and extra segments', () => {
    expect(parseRoute(`/${key}/bogus`)).toEqual({ kind: 'unknown', budgetKey: key });
    expect(parseRoute(`/${key}/plan/2026-13`)).toEqual({ kind: 'unknown', budgetKey: key });
    expect(parseRoute(`/${key}/plan/november`)).toEqual({ kind: 'unknown', budgetKey: key });
    expect(parseRoute(`/${key}/long-term/0000`)).toEqual({ kind: 'unknown', budgetKey: key });
    expect(parseRoute(`/${key}/wishlist/extra`)).toEqual({ kind: 'unknown', budgetKey: key });
    expect(parseRoute(`/${key}/account/all/more`)).toEqual({ kind: 'unknown', budgetKey: key });
    for (const path of ['/api/budgets', '/health', '/mcp', '/settings', '/assets/index.js', '/not a key/plan']) expect(parseRoute(path)).toEqual({ kind: 'unknown' });
  });

  it('chooses the first page to paint before budgets load', () => {
    expect(initialPageFromLocation(`/${key}/account/visa`, '')).toBe('Accounts');
    expect(initialPageFromLocation(`/${key}/long-term/2027`, '')).toBe('Long-Term Planning');
    expect(initialPageFromLocation('/', '?page=Reflect')).toBe('Reflect');
    expect(initialPageFromLocation('/whatever/bogus', '')).toBe('Plan');
    // Wishlist and Budget Together need the resolved budget, so they paint Plan until the router resolves.
    expect(initialPageFromLocation(`/${key}/wishlist`, '')).toBe('Plan');
    expect(initialPageFromLocation(`/${key}/together`, '')).toBe('Plan');
  });

  it('reads the requested Plan month for the first month load', () => {
    expect(monthFromLocation(`/${key}/plan/2026-11`)).toBe('2026-11');
    expect(monthFromLocation(`/${key}/plan`)).toBeUndefined();
    expect(monthFromLocation(`/${key}/account/all`)).toBeUndefined();
  });
});

describe('account slugs', () => {
  it('slugifies to lowercase ASCII with hyphens and folds diacritics', () => {
    expect(slugify('Main Checking')).toBe('main-checking');
    expect(slugify('  Épargne & Co.  ')).toBe('epargne-co');
    expect(slugify('Crème brûlée fund')).toBe('creme-brulee-fund');
    expect(slugify('ＶＩＳＡ ４２')).toBe('visa-42');
    expect(slugify('日本の口座')).toBe('account');
    expect(slugify('💳')).toBe('account');
    expect(slugify('x'.repeat(80))).toHaveLength(48);
  });

  it('suffixes duplicate names with the shortest unique account id prefix', () => {
    const accounts = [
      { id: '3f9a1111-0000-4000-8000-000000000001', name: 'Mastercard' },
      { id: '3f9b2222-0000-4000-8000-000000000002', name: 'mastercard' },
      { id: '3f9a1999-0000-4000-8000-000000000003', name: 'MasterCard!' },
      { id: 'c0ffee00-0000-4000-8000-000000000004', name: 'Savings' },
    ];
    const slugs = accountSlugs(accounts);
    expect(slugs.get(accounts[0].id)).toBe('mastercard-3f9a11');
    expect(slugs.get(accounts[1].id)).toBe('mastercard-3f9b');
    expect(slugs.get(accounts[2].id)).toBe('mastercard-3f9a19');
    expect(slugs.get(accounts[3].id)).toBe('savings');
    expect(new Set(slugs.values()).size).toBe(accounts.length);
  });

  it('never lets an account take the "all" path and keeps unicode-only names unique', () => {
    const slugs = accountSlugs([{ id: 'a1b2c3d4', name: 'All' }, { id: 'aaaa0001', name: '日本' }, { id: 'bbbb0002', name: '中国' }]);
    expect(slugs.get('a1b2c3d4')).toBe('all-a1b2');
    expect(slugs.get('aaaa0001')).toBe('account-aaaa');
    expect(slugs.get('bbbb0002')).toBe('account-bbbb');
  });

  it('resolves current slugs, raw ids and suffixed links after a rename', () => {
    const before = [{ id: '3f9a1111', name: 'Visa' }, { id: '7c2e2222', name: 'Visa' }, { id: 'c0ffee00', name: 'Cash', isClosed: true }];
    const oldLink = accountSlugs(before).get('3f9a1111')!;
    expect(oldLink).toBe('visa-3f9a');
    const renamed = [{ id: '3f9a1111', name: 'Travel Visa' }, ...before.slice(1)];
    expect(resolveAccountSlug(renamed, 'travel-visa')).toBe('3f9a1111');
    expect(resolveAccountSlug(renamed, oldLink)).toBe('3f9a1111');
    expect(resolveAccountSlug(renamed, 'cash')).toBe('c0ffee00');
    expect(resolveAccountSlug(renamed, '7C2E2222')).toBe('7c2e2222');
  });

  it('does not resolve stale unsuffixed slugs, ambiguous prefixes or unknown names', () => {
    const accounts = [{ id: '3f9a1111', name: 'Travel Visa' }, { id: '3f9a2222', name: 'Savings' }];
    expect(resolveAccountSlug(accounts, 'visa')).toBeUndefined();
    expect(resolveAccountSlug(accounts, 'visa-3f9a')).toBeUndefined();
    expect(resolveAccountSlug(accounts, 'visa-3f9')).toBeUndefined();
    expect(resolveAccountSlug(accounts, 'nothing-here')).toBeUndefined();
  });
});

describe('budget slugs', () => {
  it('derives a readable slug from the budget name', () => {
    expect(slugify('My Household Budget', 'budget')).toBe('my-household-budget');
    expect(budgetSlugFor([{ id: 'b1', name: 'My Household Budget' }], 'b1')).toBe('my-household-budget');
  });

  it('shares one slugify implementation between accounts and budgets', () => {
    const items = [{ id: 'a1b2c3d4', name: 'Épargne & Co.' }];
    expect(accountSlugs(items).get('a1b2c3d4')).toBe(budgetSlugs(items).get('a1b2c3d4'));
    expect(budgetSlugs(items).get('a1b2c3d4')).toBe(slugify('Épargne & Co.'));
  });

  it('handles diacritics, punctuation, unicode letters, whitespace and length', () => {
    expect(slugify('Crème Brûlée — Café Münster!', 'budget')).toBe('creme-brulee-cafe-munster');
    expect(slugify('Søren & Æsop: Straße', 'budget')).toBe('soren-aesop-strasse');
    expect(slugify("  --Mom's   (2026)  budget--  ", 'budget')).toBe('mom-s-2026-budget');
    expect(slugify('Ａｂｃ １２３', 'budget')).toBe('abc-123');
    expect(slugify('Budget́ é', 'budget')).toBe('budget-e');
    expect(slugify('2026', 'budget')).toBe('2026');
    expect(slugify(`${'word '.repeat(30)}`, 'budget').length).toBeLessThanOrEqual(48);
    expect(slugify(`${'a'.repeat(47)} b`, 'budget')).toBe('a'.repeat(47));
  });

  it('falls back to "budget" for empty, whitespace, symbol-only and non-Latin names', () => {
    for (const name of ['', '   ', '!!!', '💰💰', '---', '日本の家計', 'Бюджет'])
      expect(budgetSlugs([{ id: 'b1', name }]).get('b1')).toBe('budget');
  });

  it('disambiguates budgets whose names slugify identically with the shortest unique id prefix', () => {
    const list = [
      { id: '3f9a1111-0000-4000-8000-000000000001', name: 'Household' },
      { id: '3f9b2222-0000-4000-8000-000000000002', name: 'household!' },
      { id: '3f9a1999-0000-4000-8000-000000000003', name: 'HOUSEHOLD' },
      { id: 'c0ffee00-0000-4000-8000-000000000004', name: 'Travel' },
    ];
    const slugs = budgetSlugs(list);
    expect(slugs.get(list[0].id)).toBe('household-3f9a11');
    expect(slugs.get(list[1].id)).toBe('household-3f9b');
    expect(slugs.get(list[2].id)).toBe('household-3f9a19');
    expect(slugs.get(list[3].id)).toBe('travel');
    expect(new Set(slugs.values()).size).toBe(list.length);
  });

  it('is deterministic regardless of list order', () => {
    const list = [{ id: 'aaaa0001', name: 'Home' }, { id: 'bbbb0002', name: 'Home' }, { id: 'cccc0003', name: '日本' }, { id: 'dddd0004', name: '中国' }];
    const forward = budgetSlugs(list);
    const reversed = budgetSlugs([...list].reverse());
    list.forEach(item => expect(reversed.get(item.id)).toBe(forward.get(item.id)));
    expect(forward.get('cccc0003')).toBe('budget-cccc');
    expect(forward.get('dddd0004')).toBe('budget-dddd');
  });

  it('never lets a budget take a reserved first segment', () => {
    const slugs = budgetSlugs([{ id: 'a1b2c3d4', name: 'Admin' }, { id: 'e5f6a7b8', name: 'API' }, { id: '0123abcd', name: 'Login' }]);
    expect(slugs.get('a1b2c3d4')).toBe('admin-a1b2');
    expect(slugs.get('e5f6a7b8')).toBe('api-e5f6');
    expect(slugs.get('0123abcd')).toBe('login-0123');
  });

  it('resolves the current slug, the legacy hashed key, and suffixed links after a rename', () => {
    const before = [{ id: '3f9a1111', name: 'Home' }, { id: '7c2e2222', name: 'Home' }];
    const oldLink = budgetSlugs(before).get('3f9a1111')!;
    expect(oldLink).toBe('home-3f9a');
    const renamed = [{ id: '3f9a1111', name: 'Family' }, before[1]];
    expect(findBudgetBySegment(renamed, 'family')?.id).toBe('3f9a1111');
    expect(findBudgetBySegment(renamed, oldLink)?.id).toBe('3f9a1111');
    expect(findBudgetBySegment(renamed, 'HOME')?.id).toBe('7c2e2222');
    expect(findBudgetBySegment(renamed, budgetKeyFor('7c2e2222'))?.id).toBe('7c2e2222');
  });

  it('does not resolve a stale unsuffixed slug, an ambiguous prefix or an unknown segment', () => {
    const list = [{ id: '3f9a1111', name: 'Family' }, { id: '3f9a2222', name: 'Travel' }];
    expect(findBudgetBySegment(list, 'home')).toBeUndefined();
    expect(findBudgetBySegment(list, 'home-3f9a')).toBeUndefined();
    expect(findBudgetBySegment(list, 'nothing-here')).toBeUndefined();
    expect(findBudgetBySegment([], 'family')).toBeUndefined();
  });

  it('never resolves a budget that is not in the visible list (slugs are presentational, not access)', () => {
    const visible = [{ id: 'mine-0001', name: 'Mine' }];
    expect(findBudgetBySegment(visible, 'theirs')).toBeUndefined();
    expect(findBudgetBySegment(visible, budgetKeyFor('theirs-0002'))).toBeUndefined();
    expect(findBudgetBySegment(visible, 'theirs-0002')).toBeUndefined();
    expect(resolveRoute(parseRoute('/theirs/plan'), { budgets: visible, fallbackBudgetId: 'mine-0001', accounts: [] }))
      .toEqual({ target: { budgetId: 'mine-0001', page: 'Plan', accountId: '' }, notice: routeNotices.budgetNotFound });
  });

  it('resolves a slug a budget used earlier in the session through aliases', () => {
    const renamed = [{ id: '3f9a1111', name: 'Family' }];
    expect(findBudgetBySegment(renamed, 'home')).toBeUndefined();
    expect(findBudgetBySegment(renamed, 'home', new Map([['home', '3f9a1111']]))?.id).toBe('3f9a1111');
    expect(findBudgetBySegment(renamed, 'home', new Map([['home', 'not-visible']]))).toBeUndefined();
  });

  it('redirects a legacy hashed key to the slug path', () => {
    const resolution = resolveRoute(parseRoute(`/${budgetKeyFor(budgets[1].id)}/long-term/2030`), { budgets, fallbackBudgetId: 'default', accounts: [] });
    expect(resolution.target).toMatchObject({ budgetId: budgets[1].id, page: 'Long-Term Planning', year: 2030 });
    expect(resolution.notice).toBeUndefined();
    expect(pathForState({ budgetId: budgets[1].id, budgets, page: 'Long-Term Planning', accountId: '', month: '2026-10', defaultMonth: '2026-10', year: 2030, defaultYear: 2026, accounts: [] })).toBe('/travel/long-term/2030');
  });

  it('keeps slugs round-trippable through parseRoute and formatRoute', () => {
    expect(parseRoute('/my-household-budget/plan/2026-11')).toEqual({ kind: 'budget', budgetKey: 'my-household-budget', page: 'Plan', month: '2026-11' });
    expect(formatRoute({ kind: 'budget', budgetKey: 'my-household-budget', page: 'Plan' })).toBe('/my-household-budget/plan');
    expect(parseRoute(`/${'a'.repeat(48)}-3f9a11/plan`)).toMatchObject({ kind: 'budget', page: 'Plan' });
  });
});

describe('resolveRoute', () => {
  const accounts = [{ id: 'acc-visa-0001', name: 'Visa' }];
  const context = { budgets, fallbackBudgetId: 'default', accounts };

  it('resolves budget pages, months, years and accounts', () => {
    const travelKey = budgetKeyFor(budgets[1].id);
    expect(resolveRoute(parseRoute(`/${travelKey}/plan/2026-11`), context)).toEqual({ target: { budgetId: budgets[1].id, page: 'Plan', accountId: '', month: '2026-11', year: undefined } });
    expect(resolveRoute(parseRoute(`/${key}/long-term/2030`), context).target).toMatchObject({ page: 'Long-Term Planning', year: 2030 });
    expect(resolveRoute(parseRoute(`/${key}/account/visa`), context).target).toMatchObject({ page: 'Accounts', accountId: 'acc-visa-0001' });
    expect(resolveRoute(parseRoute(`/${key}/account/all`), context).target).toMatchObject({ page: 'Accounts', accountId: '' });
  });

  it('falls back to the plan with a notice for unknown budgets, pages and accounts', () => {
    expect(resolveRoute(parseRoute('/AAAAAAAAAAA/wishlist'), context)).toEqual({ target: { budgetId: 'default', page: 'Plan', accountId: '' }, notice: routeNotices.budgetNotFound });
    expect(resolveRoute(parseRoute(`/${budgetKeyFor(budgets[1].id)}/bogus`), context)).toEqual({ target: { budgetId: budgets[1].id, page: 'Plan', accountId: '' }, notice: routeNotices.unknownPage });
    expect(resolveRoute(parseRoute('/totally/unknown/path/here'), context).notice).toBe(routeNotices.unknownPage);
    expect(resolveRoute(parseRoute(`/${key}/account/gone`), context)).toMatchObject({ target: { page: 'Accounts', accountId: '' }, notice: routeNotices.accountNotFound });
    expect(resolveRoute(parseRoute('/'), context)).toEqual({ target: { budgetId: 'default', page: 'Plan', accountId: '' }, notice: undefined });
  });

  it('defers account slugs until that budget\'s accounts are loaded', () => {
    expect(resolveRoute(parseRoute(`/${key}/account/visa`), { ...context, accounts: null })).toMatchObject({ target: { page: 'Accounts', accountId: '' }, pendingAccount: 'visa' });
  });

  it('maps legacy ?page=&account= links', () => {
    expect(resolveRoute(parseRoute('/', '?page=Accounts&account=acc-visa-0001'), context).target).toMatchObject({ page: 'Accounts', accountId: 'acc-visa-0001' });
    expect(resolveRoute(parseRoute('/', '?page=Long-Term Planning'), context).target).toMatchObject({ page: 'Long-Term Planning' });
  });
});

describe('pathForState and titles', () => {
  const base = { budgetId: 'default', budgets, page: 'Plan', accountId: '', month: '2026-10', defaultMonth: '2026-10', year: 2026, defaultYear: 2026, accounts: [{ id: 'acc-1', name: 'Main Checking' }] };

  it('formats canonical paths, omitting default month and year', () => {
    expect(pathForState(base)).toBe(`/${slug}/plan`);
    expect(pathForState({ ...base, month: '2026-11' })).toBe(`/${slug}/plan/2026-11`);
    expect(pathForState({ ...base, page: 'Long-Term Planning' })).toBe(`/${slug}/long-term`);
    expect(pathForState({ ...base, page: 'Long-Term Planning', year: 2027, month: '2026-11' })).toBe(`/${slug}/long-term/2027`);
    expect(pathForState({ ...base, page: 'Accounts' })).toBe(`/${slug}/account/all`);
    expect(pathForState({ ...base, page: 'Accounts', accountId: 'acc-1' })).toBe(`/${slug}/account/main-checking`);
    expect(pathForState({ ...base, page: 'Budget Together' })).toBe(`/${slug}/together`);
    expect(pathForState({ ...base, page: 'Reflect' })).toBe(`/${slug}/reflect`);
    expect(pathForState({ ...base, page: 'Wishlist' })).toBe(`/${slug}/wishlist`);
  });

  it('waits while the budget or the selected account is not known yet', () => {
    expect(pathForState({ ...base, budgetId: '' })).toBeNull();
    expect(pathForState({ ...base, page: 'Accounts', accountId: 'acc-1', accounts: null })).toBeNull();
    expect(pathForState({ ...base, page: 'Accounts', accountId: 'acc-new' })).toBeNull();
  });

  it('builds tab titles', () => {
    expect(titleForState({ page: 'Plan', budgetName: 'Household' })).toBe('Plan · Household · OpenBudget');
    expect(titleForState({ page: 'Accounts', budgetName: 'Household' })).toBe('All Accounts · Household · OpenBudget');
    expect(titleForState({ page: 'Accounts', budgetName: 'Household', accountName: 'Visa' })).toBe('Visa · Household · OpenBudget');
    expect(titleForState({ page: 'Plan', budgetName: '' })).toBe('Plan · OpenBudget');
  });
});

describe('post-sign-in return path', () => {
  afterEach(() => window.sessionStorage.clear());

  it('accepts only same-origin app paths', () => {
    expect(isSafeReturnPath(`/${key}/account/visa`)).toBe(true);
    expect(isSafeReturnPath('/admin')).toBe(true);
    for (const value of ['', '/', '//evil.example/x', '/\\evil.example', 'https://evil.example/', 'javascript:alert(1)', '/login', '/reset-password', '/api/budgets', '/mcp', '/health', `/${'a'.repeat(600)}`, '/x\n'])
      expect(isSafeReturnPath(value)).toBe(false);
  });

  it('remembers once and ignores unsafe or tampered values', () => {
    rememberReturnPath(`/${key}/plan/2026-11`);
    expect(takeReturnPath()).toBe(`/${key}/plan/2026-11`);
    expect(takeReturnPath()).toBeUndefined();
    rememberReturnPath('//evil.example');
    expect(takeReturnPath()).toBeUndefined();
    window.sessionStorage.setItem('openbudget.returnTo', 'https://evil.example');
    expect(takeReturnPath()).toBeUndefined();
  });
});
