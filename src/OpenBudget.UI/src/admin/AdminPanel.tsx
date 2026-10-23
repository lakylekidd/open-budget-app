import { useCallback, useEffect, useState } from 'react';

type AdminUser = {
  id: string;
  userName: string;
  email: string | null;
  createdAt: string;
  lastLoginAt: string | null;
  status: 'Active' | 'Pending' | 'Locked' | 'Suspended';
  isBootstrapAdministrator: boolean;
  lockoutEnd: string | null;
};
type AdminAudit = { id: string; actorUserId: string; targetUserId: string; action: string; createdAt: string };
type AdminSummary = { totalUsers: number; activeUsers: number; pendingUsers: number; lockedUsers: number; suspendedUsers: number; totalBudgets: number; activeBudgets: number };
const modes = ['Disabled', 'ApprovalRequired', 'Open'] as const;

export function AdminPanel() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [audit, setAudit] = useState<AdminAudit[]>([]);
  const [mode, setMode] = useState('Disabled');
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('All');
  const [summary, setSummary] = useState<AdminSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [forbidden, setForbidden] = useState(false);

  const load = useCallback(async (search: string, filter: string) => {
    setLoading(true); setError('');
    try {
      const params = new URLSearchParams();
      if (search) params.set('q', search);
      if (filter !== 'All') params.set('status', filter);
      const queryString = params.toString();
      const suffix = queryString ? `?${queryString}` : '';
      const [usersResponse, auditResponse, modeResponse, summaryResponse] = await Promise.all([
        fetch(`/api/admin/users${suffix}`),
        fetch('/api/admin/audit'),
        fetch('/api/auth/registration-mode'),
        fetch('/api/admin/dashboard'),
      ]);
      if (usersResponse.status === 403 || usersResponse.status === 401) { setForbidden(true); return; }
      if (!usersResponse.ok || !auditResponse.ok || !modeResponse.ok || !summaryResponse.ok) throw new Error('Could not load administration data.');
      setUsers(await usersResponse.json() as AdminUser[]);
      setAudit(await auditResponse.json() as AdminAudit[]);
      setMode((await modeResponse.json() as { mode: string }).mode);
      setSummary(await summaryResponse.json() as AdminSummary);
      setForbidden(false);
    } catch { setError('Could not load administration data. Retry to reconnect.'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void load('', 'All'); }, [load]);

  async function action(user: AdminUser, operation: 'approve' | 'reject' | 'suspend' | 'reactivate' | 'unlock') {
    const prompts = { approve: `Approve ${user.userName}?`, reject: `Reject the pending registration for ${user.userName}?`, suspend: `Suspend ${user.userName}? Their active session will be invalidated.`, reactivate: `Reactivate ${user.userName}?`, unlock: `Unlock ${user.userName}?` };
    if (!window.confirm(prompts[operation])) return;
    setBusy(user.id); setError('');
    try {
      const response = await fetch(`/api/admin/users/${encodeURIComponent(user.id)}/${operation}`, { method: 'POST' });
      if (!response.ok) throw new Error(response.status === 409 ? 'This account can no longer use that action.' : 'The account update failed.');
      await load(query, status);
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The account update failed.'); }
    finally { setBusy(''); }
  }

  async function saveMode(value: string) {
    setBusy('settings'); setError('');
    try {
      const response = await fetch('/api/admin/settings/registration-mode', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ mode: value }) });
      if (!response.ok) throw new Error('Registration setting could not be saved.');
      setMode(value);
      await load(query, status);
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Registration setting could not be saved.'); }
    finally { setBusy(''); }
  }

  if (forbidden) return <main className="admin-screen"><header><a href="/">← OpenBudget</a><h1>Administration</h1></header><section className="admin-card"><h2>Access denied</h2><p>An administrator account is required to open this page.</p></section></main>;

  return <main className="admin-screen">
    <header><a href="/">← OpenBudget</a><div><span className="eyebrow">OPENBUDGET CONTROL</span><h1>Administration</h1><p>Manage accounts and registration policy.</p></div></header>
    {error && <p className="admin-error" role="alert">{error}<button onClick={() => void load(query, status)}>Retry</button></p>}
    <section className="admin-card"><div className="admin-section-head"><div><h2>Registration</h2><p>New users must verify their email before signing in.</p></div><label>Registration mode<select aria-label="Registration mode" value={mode} disabled={busy === 'settings'} onChange={event => void saveMode(event.target.value)}>{modes.map(item => <option key={item} value={item}>{item === 'ApprovalRequired' ? 'Approval required' : item}</option>)}</select></label></div></section>
    <section className="admin-card"><h2>Dashboard</h2>{loading || !summary ? <p>Loading statistics…</p> : <div className="admin-stats">{[['Registered users', summary.totalUsers], ['Active', summary.activeUsers], ['Pending', summary.pendingUsers], ['Locked', summary.lockedUsers], ['Suspended', summary.suspendedUsers], ['Total budgets', summary.totalBudgets], ['Active budgets', summary.activeBudgets]].map(([label, value]) => <article key={label}><span>{label}</span><strong>{value}</strong></article>)}</div>}</section>
    <section className="admin-card"><div className="admin-section-head"><div><h2>Users</h2><p>{users.length} accounts shown · newest first</p></div><form className="admin-search" onSubmit={event => { event.preventDefault(); void load(query, status); }}><label>Search users<input aria-label="Search users" value={query} onChange={event => setQuery(event.target.value)} placeholder="Name or email" /></label><label>Status<select aria-label="Filter status" value={status} onChange={event => setStatus(event.target.value)}>{['All', 'Active', 'Pending', 'Locked', 'Suspended'].map(item => <option key={item}>{item}</option>)}</select></label><button type="submit">Apply</button></form></div>
      {loading ? <p aria-busy="true">Loading users…</p> : users.length === 0 ? <p>No matching users.</p> : <div className="admin-table-wrap"><table><thead><tr><th>User</th><th>Status</th><th>Created</th><th>Last sign-in</th><th>Actions</th></tr></thead><tbody>{users.map(user => <tr key={user.id}><td><strong>{user.userName}{user.isBootstrapAdministrator ? ' · Permanent admin' : ''}</strong><small>{user.email}</small></td><td><span className={`admin-status status-${user.status.toLowerCase()}`}>{user.status}</span></td><td>{new Date(user.createdAt).toLocaleDateString()}</td><td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleDateString() : 'Never'}</td><td className="admin-actions">{user.status === 'Pending' && <><button disabled={busy === user.id} onClick={() => void action(user, 'approve')}>Approve</button><button disabled={busy === user.id} onClick={() => void action(user, 'reject')}>Reject</button></>}{user.status === 'Active' && !user.isBootstrapAdministrator && <button disabled={busy === user.id} onClick={() => void action(user, 'suspend')}>Suspend</button>}{user.status === 'Suspended' && <button disabled={busy === user.id} onClick={() => void action(user, 'reactivate')}>Reactivate</button>}{user.status === 'Locked' && <><button disabled={busy === user.id} onClick={() => void action(user, 'unlock')}>Unlock</button>{!user.isBootstrapAdministrator && <button disabled={busy === user.id} onClick={() => void action(user, 'suspend')}>Suspend</button>}</>}</td></tr>)}</tbody></table></div>}
    </section>
    <section className="admin-card"><h2>Recent administration activity</h2>{loading ? <p>Loading activity…</p> : audit.length === 0 ? <p>No administration actions recorded.</p> : <ul className="admin-audit">{audit.map(item => <li key={item.id}><span>{item.action}</span><small>Target {item.targetUserId} · {new Date(item.createdAt).toLocaleString()}</small></li>)}</ul>}</section>
    {!loading && <button className="admin-refresh" onClick={() => void load(query, status)}>Refresh</button>}
  </main>;
}
