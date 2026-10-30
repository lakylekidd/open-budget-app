import { useEffect, useState } from 'react';
import { loadWishlist, type WishlistAccess, type WishlistProgress } from './wishlistApi';
import './wishlist.css';

type Money = (amount: number) => string;

export function WishlistIcon({ size = 22 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <rect x="3.5" y="8" width="17" height="4.5" rx="1.2" /><path d="M5.5 12.5V20h13v-7.5M12 8v12" /><path d="M12 8c-1.2-3.4-5.6-4.3-5.6-1.7C6.4 7.6 9 8 12 8Zm0 0c1.2-3.4 5.6-4.3 5.6-1.7 0 1.3-2.6 1.7-5.6 1.7Z" />
  </svg>;
}

// Loads entitlement/enabled state and the ready-to-buy count for the sidebar. Errors keep the nav item hidden.
export function useWishlistAccess(budgetId: string, refreshKey: unknown) {
  const [access, setAccess] = useState<WishlistAccess>({ status: 'loading' });
  useEffect(() => {
    if (!budgetId) return;
    const controller = new AbortController();
    loadWishlist(budgetId, controller.signal)
      .then(result => { if (!controller.signal.aborted) setAccess(result); })
      .catch(error => { if (!controller.signal.aborted) setAccess({ status: 'error', message: error instanceof Error ? error.message : 'Wishlist could not be loaded.' }); });
    return () => controller.abort();
  }, [budgetId, refreshKey]);
  return [access, setAccess] as const;
}

// Shown only to entitled users (directly below Long-Term Planning); highlights how many buckets can buy their next item.
export function WishlistNavItem({ access, active, onClick }: { access: WishlistAccess; active: boolean; onClick: () => void }) {
  if (access.status !== 'ready') return null;
  const ready = access.overview.isEnabled ? access.overview.readyCount : 0;
  const label = ready > 0 ? `Wishlist, ${ready} ready to buy` : 'Wishlist';
  return <button aria-label={label} title={label} className={`nav-item wishlist-nav-item ${active ? 'active' : ''} ${ready > 0 ? 'has-ready' : ''}`} onClick={onClick}>
    <WishlistIcon /><span>Wishlist</span>{ready > 0 && <b className="wishlist-nav-count" aria-hidden="true">{ready}</b>}
    {!access.overview.isEnabled && <small className="wishlist-nav-off" aria-hidden="true">Off</small>}
  </button>;
}

// Target-style status text for a Wishlist category row in Plan.
export function WishlistRowStatus({ progress, money }: { progress: WishlistProgress; money: Money }) {
  return progress.canPurchase
    ? <span className="wishlist-row-ready">Ready to buy <b>{progress.nextItemName}</b></span>
    : <><b>{money(progress.remaining)}</b> to go for {progress.nextItemName}</>;
}

export function WishlistProgressBar({ progress, money }: { progress: WishlistProgress; money: Money }) {
  const percent = Math.round(Math.min(1, Math.max(0, progress.fundedFraction)) * 100);
  return <div className={`wishlist-progress ${progress.canPurchase ? 'ready' : ''}`} role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}
    aria-label={`${money(Math.max(progress.available, 0))} of ${money(progress.cost)} saved for ${progress.nextItemName}`}><span style={{ width: `${percent}%` }} /></div>;
}

// Category details panel card in Plan for a Wishlist bucket category.
export function WishlistCategoryCard({ progress, money, onOpen }: { progress: WishlistProgress; money: Money; onOpen: () => void }) {
  return <section className="wishlist-category-card" aria-label="Wishlist progress">
    <header><strong>Wishlist</strong>{progress.canPurchase && <span className="wishlist-ready-badge">Ready to buy</span>}</header>
    <p>Next: <b>{progress.nextItemName}</b></p>
    <p className="wishlist-amounts">{money(Math.max(progress.available, 0))} of {money(progress.cost)}{progress.canPurchase ? '' : ` — ${money(progress.remaining)} to go`}</p>
    <WishlistProgressBar progress={progress} money={money} />
    {progress.canPurchase && <p className="wishlist-can-buy">You can buy {progress.nextItemName} now.</p>}
    <button type="button" onClick={onOpen}>Open Wishlist</button>
  </section>;
}
