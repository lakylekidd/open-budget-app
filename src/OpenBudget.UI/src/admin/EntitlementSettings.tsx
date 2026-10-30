import { useEffect, useState } from 'react';

type Plan = { id: string; name: string };
type Entitlement = { entitlementKey: string; isEnabled: boolean };

const featureLabels: Record<string, string> = { wishlist: 'Wishlist' };
export const featureLabel = (key: string) => featureLabels[key] ? `${featureLabels[key]} (${key})` : key;

async function readJson<T>(path: string): Promise<T> {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`Request failed (${response.status}).`);
  return await response.json() as T;
}

export function useEntitlementKeys() {
  const [keys, setKeys] = useState<string[]>([]);
  useEffect(() => { void readJson<string[]>('/api/admin/entitlement-keys').then(setKeys).catch(() => setKeys([])); }, []);
  return keys;
}

// Tier defaults for every known feature key. Saving replaces that tier's entitlement list (unknown keys are preserved).
export function EntitlementSettings({ plans, keys }: { plans: Plan[]; keys: string[] }) {
  const [byPlan, setByPlan] = useState<Record<string, Entitlement[]>>({});
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const [loadError, setLoadError] = useState('');
  useEffect(() => {
    let active = true;
    void Promise.all(plans.map(async plan => [plan.id, await readJson<Entitlement[]>(`/api/admin/membership-plans/${plan.id}/entitlements`)] as const))
      .then(rows => { if (active) setByPlan(Object.fromEntries(rows)); })
      .catch(() => { if (active) setLoadError('Feature entitlements could not be loaded. Refresh to retry.'); });
    return () => { active = false; };
  }, [plans]);

  async function toggle(planId: string, key: string, enabled: boolean) {
    const current = byPlan[planId] ?? [];
    const next = current.some(item => item.entitlementKey === key)
      ? current.map(item => item.entitlementKey === key ? { ...item, isEnabled: enabled } : item)
      : [...current, { entitlementKey: key, isEnabled: enabled }];
    setBusy(`${planId}:${key}`); setMessage('');
    try {
      const response = await fetch(`/api/admin/membership-plans/${planId}/entitlements`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ entitlements: next }) });
      if (!response.ok) throw new Error();
      setByPlan(value => ({ ...value, [planId]: next }));
      setMessage(`${featureLabel(key)} ${enabled ? 'enabled' : 'disabled'} for ${plans.find(plan => plan.id === planId)?.name ?? planId}.`);
    } catch { setMessage('The feature entitlement could not be saved.'); }
    finally { setBusy(''); }
  }

  return <section className="admin-card" aria-label="Feature entitlements"><h2>Feature entitlements</h2>
    <p>Choose which membership tiers include each feature. Per-user overrides in the Users table win over these defaults.</p>
    {keys.length === 0 ? <p>No feature keys configured.</p> : <div className="admin-table-wrap"><table><thead><tr><th>Feature</th>{plans.map(plan => <th key={plan.id}>{plan.name}</th>)}</tr></thead>
      <tbody>{keys.map(key => <tr key={key}><td>{featureLabel(key)}</td>{plans.map(plan => <td key={plan.id}><input type="checkbox" aria-label={`${featureLabel(key)} for ${plan.name}`}
        checked={byPlan[plan.id]?.some(item => item.entitlementKey === key && item.isEnabled) ?? false} disabled={!byPlan[plan.id] || busy !== ''}
        onChange={event => void toggle(plan.id, key, event.target.checked)} /></td>)}</tr>)}</tbody></table></div>}
    {loadError && <p>{loadError}</p>}
    {message && <p role="status">{message}</p>}
  </section>;
}

// Per-user allow/deny overrides, loaded when the administrator opens the disclosure.
export function UserEntitlementOverrides({ userId, userName, keys }: { userId: string; userName: string; keys: string[] }) {
  const [overrides, setOverrides] = useState<Record<string, boolean> | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function load() {
    try { setOverrides(Object.fromEntries((await readJson<Entitlement[]>(`/api/admin/users/${userId}/entitlements`)).map(item => [item.entitlementKey, item.isEnabled]))); }
    catch { setError('Overrides could not be loaded.'); }
  }
  async function change(key: string, value: string) {
    setBusy(true); setError('');
    try {
      const path = `/api/admin/users/${userId}/entitlements/${encodeURIComponent(key)}`;
      const response = value === 'default' ? await fetch(path, { method: 'DELETE' })
        : await fetch(path, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isEnabled: value === 'allow' }) });
      if (!response.ok) throw new Error();
      setOverrides(current => { const next = { ...(current ?? {}) }; if (value === 'default') delete next[key]; else next[key] = value === 'allow'; return next; });
    } catch { setError('The override could not be saved.'); }
    finally { setBusy(false); }
  }
  return <details onToggle={event => { if (event.currentTarget.open && overrides === null) void load(); }}><summary>Feature overrides</summary>
    {error && <small role="alert">{error}</small>}
    {overrides !== null && keys.map(key => <label key={key}>{featureLabel(key)}<select aria-label={`${featureLabel(key)} override for ${userName}`} disabled={busy}
      value={key in overrides ? (overrides[key] ? 'allow' : 'deny') : 'default'} onChange={event => void change(key, event.target.value)}>
      <option value="default">Tier default</option><option value="allow">Allow</option><option value="deny">Deny</option></select></label>)}
  </details>;
}
