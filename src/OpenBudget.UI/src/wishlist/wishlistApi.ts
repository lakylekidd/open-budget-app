// Typed client for the budget-scoped Wishlist API. Progress values are server-calculated; the browser only renders them.
export const wishlistGroupId = 'wishlist';

export type WishlistProgress = {
  nextItemId: string;
  nextItemName: string;
  cost: number;
  available: number;
  remaining: number;
  canPurchase: boolean;
  fundedFraction: number;
};

export type WishlistItemStatus = 'active' | 'purchased' | 'removed';
export type WishlistItem = {
  id: string;
  bucketId: string;
  name: string;
  url: string | null;
  cost: number;
  description: string;
  sortOrder: number;
  status: WishlistItemStatus;
  createdAt: string;
  purchasedAt: string | null;
  removedAt: string | null;
  // Set when the purchase was recorded as a ledger transaction ("Record purchase"); amount is the positive spending.
  purchase?: WishlistPurchase | null;
};

export type WishlistPurchase = { transactionId: string; accountId: string; date: string; amount: number };

export type WishlistBucket = {
  id: string;
  name: string;
  sortOrder: number;
  categoryId: string;
  available: number;
  minCost: number | null;
  maxCost: number | null;
  progress: WishlistProgress | null;
  items: WishlistItem[];
  purchased: WishlistItem[];
  removed: WishlistItem[];
};

export type WishlistOverview = { isEnabled: boolean; version: number; month: string; readyCount: number; buckets: WishlistBucket[] };

// "forbidden" means the signed-in user's membership does not include the wishlist entitlement.
export type WishlistAccess =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'forbidden' }
  | { status: 'ready'; overview: WishlistOverview };

export class WishlistConflictError extends Error {
  constructor(message: string, readonly current: WishlistOverview | null) { super(message); this.name = 'WishlistConflictError'; }
}

export const wishlistPath = (budgetId: string) => `/api/budgets/${encodeURIComponent(budgetId)}/wishlist`;

export async function loadWishlist(budgetId: string, signal?: AbortSignal): Promise<WishlistAccess> {
  const response = await fetch(wishlistPath(budgetId), { signal });
  if (response.status === 403) return { status: 'forbidden' };
  if (!response.ok) return { status: 'error', message: `Wishlist could not be loaded (${response.status}).` };
  return { status: 'ready', overview: await response.json() as WishlistOverview };
}

export async function sendWishlistCommand(budgetId: string, path: string, method: 'POST' | 'PUT', body: unknown): Promise<WishlistOverview> {
  const response = await fetch(`${wishlistPath(budgetId)}${path}`, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  if (response.ok) return await response.json() as WishlistOverview;
  const problem = await response.json().catch(() => ({})) as { error?: string; current?: WishlistOverview };
  if (response.status === 409) throw new WishlistConflictError(problem.error ?? 'Wishlist changed in another session.', problem.current ?? null);
  if (response.status === 403) throw new Error('Your membership does not include Wishlist.');
  throw new Error(problem.error ?? `Wishlist request failed (${response.status}).`);
}

// "added 3 months ago" for the do-I-still-want-this review.
export function addedAgo(createdAt: string, now = Date.now()): string {
  const days = Math.max(0, Math.floor((now - new Date(createdAt).getTime()) / 86_400_000));
  if (days === 0) return 'added today';
  if (days === 1) return 'added yesterday';
  if (days < 14) return `added ${days} days ago`;
  if (days < 60) return `added ${Math.floor(days / 7)} weeks ago`;
  if (days < 365) return `added ${Math.floor(days / 30)} months ago`;
  const years = Math.floor(days / 365);
  return `added ${years} year${years === 1 ? '' : 's'} ago`;
}

export function isWebLink(value: string): boolean {
  if (!value.trim()) return true;
  if (/\s/.test(value.trim())) return false;
  try {
    const url = new URL(value.trim());
    return (url.protocol === 'http:' || url.protocol === 'https:') && Boolean(url.hostname) && !url.username && !url.password;
  } catch { return false; }
}

// Moves one id to the position of another, returning a new array (used by drag-and-drop and keyboard moves).
export function reorder<T>(items: T[], from: number, to: number): T[] {
  if (from < 0 || to < 0 || from >= items.length || to >= items.length || from === to) return items;
  const next = [...items];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}
