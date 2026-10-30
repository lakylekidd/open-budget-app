import { useCallback, useEffect, useState } from 'react';
import { EntitlementSettings, UserEntitlementOverrides, useEntitlementKeys } from './EntitlementSettings';

type AdminUser = {
  id: string;
  userName: string;
  email: string | null;
  createdAt: string;
  lastLoginAt: string | null;
  status: 'Active' | 'Pending' | 'Locked' | 'Suspended';
  isBootstrapAdministrator: boolean;
  lockoutEnd: string | null;
  membershipPlanId: string;
  budgetLimitOverride: number | null;
  membershipStartsOn?: string | null;
  membershipExpiresOn?: string | null;
  billingProvider?: string | null;
  externalSubscriptionId?: string | null;
};
type MembershipPlan = { id: string; name: string; maxActiveBudgets: number | null };
type MembershipDraft = { membershipPlanId: string; budgetLimitOverride: string; startsOn: string; expiresOn: string; billingProvider: string; externalSubscriptionId: string };
type AdminAudit = { id: string; actorUserId: string; targetUserId: string; action: string; createdAt: string };
type EmailSettings = { host: string; port: number; from: string; username: string | null; enableSsl: boolean; hasPassword: boolean; isConfigured: boolean; source: 'admin' | 'environment' | 'none' };
type AdminSummary = {
  totalUsers: number;
  activeUsers: number;
  pendingUsers: number;
  lockedUsers: number;
  suspendedUsers: number;
  totalBudgets: number;
  activeBudgets: number;
  usersByMembership: { membershipPlanId: string; name: string; userCount: number }[];
  recentRegistrations: { userId: string; userName: string; createdAt: string; status: string }[];
  recentSecurityEvents: AdminAudit[];
};
const modes = ['Disabled', 'ApprovalRequired', 'Open'] as const;

export function AdminPanel() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [membershipPlans, setMembershipPlans] = useState<MembershipPlan[]>([]);
  const [membershipDrafts, setMembershipDrafts] = useState<Record<string, MembershipDraft>>({});
  const [historyByUser, setHistoryByUser] = useState<Record<string, AdminAudit[]>>({});
  const [historyLoaded, setHistoryLoaded] = useState<Record<string, boolean>>({});
  const [historyLoading, setHistoryLoading] = useState('');
  const [mode, setMode] = useState('Disabled');
  const [emailSettings, setEmailSettings] = useState<EmailSettings>({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
  const [smtpPassword, setSmtpPassword] = useState('');
  const [testEmail, setTestEmail] = useState('');
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('All');
  const [inviteUserName, setInviteUserName] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteMembership, setInviteMembership] = useState('free');
  const [summary, setSummary] = useState<AdminSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [forbidden, setForbidden] = useState(false);
  const entitlementKeys = useEntitlementKeys();

  const load = useCallback(async (search: string, filter: string) => {
    setLoading(true); setError('');
    try {
      const params = new URLSearchParams();
      if (search) params.set('q', search);
      if (filter !== 'All') params.set('status', filter);
      const queryString = params.toString();
      const suffix = queryString ? `?${queryString}` : '';
      const [usersResponse, modeResponse, summaryResponse, plansResponse, emailResponse] = await Promise.all([
        fetch(`/api/admin/users${suffix}`),
        fetch('/api/auth/registration-mode'),
        fetch('/api/admin/dashboard'),
        fetch('/api/admin/membership-plans'),
        fetch('/api/admin/settings/email'),
      ]);
      if (usersResponse.status === 403 || usersResponse.status === 401) { setForbidden(true); return; }
      if (!usersResponse.ok || !modeResponse.ok || !summaryResponse.ok || !plansResponse.ok || !emailResponse.ok) throw new Error('Could not load administration data.');
      const userRows = await usersResponse.json() as AdminUser[];
      const dashboard = await summaryResponse.json() as AdminSummary;
      setUsers(userRows);
      setMembershipDrafts(Object.fromEntries(userRows.map(user => [user.id, { membershipPlanId: user.membershipPlanId, budgetLimitOverride: user.budgetLimitOverride?.toString() ?? '', startsOn: user.membershipStartsOn ?? '', expiresOn: user.membershipExpiresOn ?? '', billingProvider: user.billingProvider ?? '', externalSubscriptionId: user.externalSubscriptionId ?? '' }])));
      setMembershipPlans(await plansResponse.json() as MembershipPlan[]);
      setMode((await modeResponse.json() as { mode: string }).mode);
      setEmailSettings(await emailResponse.json() as EmailSettings);
      setSummary(dashboard);
      setForbidden(false);
    } catch { setError('Could not load administration data. Retry to reconnect.'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void load('', 'All'); }, [load]);

  async function action(user: AdminUser, operation: 'approve' | 'reject' | 'suspend' | 'reactivate' | 'unlock' | 'reset-password') {
    const prompts = { approve: `Approve ${user.userName}?`, reject: `Reject the pending registration for ${user.userName}?`, suspend: `Suspend ${user.userName}? Their active session will be invalidated.`, reactivate: `Reactivate ${user.userName}?`, unlock: `Unlock ${user.userName}?`, 'reset-password': `Send a secure password reset link to ${user.email}?` };
    if (!window.confirm(prompts[operation])) return;
    setBusy(user.id); setError('');
    try {
      const endpoint = operation === 'reset-password' ? 'password-reset' : operation;
      const response = await fetch(`/api/admin/users/${encodeURIComponent(user.id)}/${endpoint}`, { method: 'POST' });
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

  async function saveEmailSettings(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const port = Number(emailSettings.port);
    if (!Number.isInteger(port) || port < 1 || port > 65535) { setError('SMTP port must be a whole number between 1 and 65535.'); return; }
    setBusy('email-settings'); setError(''); setNotice('');
    try {
      const response = await fetch('/api/admin/settings/email', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ host: emailSettings.host, port, from: emailSettings.from, username: emailSettings.username, password: smtpPassword || null, enableSsl: emailSettings.enableSsl }) });
      const result = await response.json().catch(() => null) as (EmailSettings & { error?: string }) | null;
      if (!response.ok || !result) throw new Error(result?.error ?? 'Email settings could not be saved.');
      setEmailSettings(result); setSmtpPassword(''); setNotice('Email delivery settings saved.');
      await load(query, status);
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Email settings could not be saved.'); }
    finally { setBusy(''); }
  }

  async function sendTestEmail(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy('email-test'); setError(''); setNotice('');
    try {
      const response = await fetch('/api/admin/settings/email/test', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ to: testEmail.trim() }) });
      const result = await response.json().catch(() => null) as { message?: string; error?: string } | null;
      if (!response.ok) throw new Error(result?.error ?? 'Test email could not be sent.');
      setNotice(result?.message ?? 'Test email sent.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Test email could not be sent.'); }
    finally { setBusy(''); }
  }

  async function saveMembership(user: AdminUser) {
    const draft = membershipDrafts[user.id] ?? { membershipPlanId: user.membershipPlanId, budgetLimitOverride: user.budgetLimitOverride?.toString() ?? '', startsOn: user.membershipStartsOn ?? '', expiresOn: user.membershipExpiresOn ?? '', billingProvider: user.billingProvider ?? '', externalSubscriptionId: user.externalSubscriptionId ?? '' };
    const override = draft.budgetLimitOverride.trim() === '' ? null : Number(draft.budgetLimitOverride);
    if (override !== null && (!Number.isInteger(override) || override < 1 || override > 1000)) { setError('Budget limit override must be a whole number from 1 to 1000.'); return; }
    if (!window.confirm(`Update membership and budget limit for ${user.userName}?`)) return;
    setBusy(user.id); setError('');
    try {
      if (draft.startsOn && draft.expiresOn && draft.expiresOn <= draft.startsOn) { setError('Membership expiration must be after its start date.'); return; }
      const response = await fetch(`/api/admin/users/${encodeURIComponent(user.id)}/membership`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ membershipPlanId: draft.membershipPlanId, budgetLimitOverride: override, startsOn: draft.startsOn || null, expiresOn: draft.expiresOn || null, billingProvider: draft.billingProvider.trim() || null, externalSubscriptionId: draft.externalSubscriptionId.trim() || null }) });
      if (!response.ok) throw new Error('Membership could not be updated.');
      await load(query, status);
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Membership could not be updated.'); }
    finally { setBusy(''); }
  }

  async function inviteUser(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!window.confirm(`Invite ${inviteUserName.trim()} at ${inviteEmail.trim()}?`)) return;
    setBusy('invite'); setError(''); setNotice('');
    try {
      const response = await fetch('/api/admin/users/invite', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userName: inviteUserName.trim(), email: inviteEmail.trim(), membershipPlanId: inviteMembership }),
      });
      if (!response.ok) {
        const result = await response.json().catch(() => null) as { error?: string } | null;
        throw new Error(result?.error ?? (response.status === 503 ? 'Email delivery is not configured.' : 'The invitation could not be sent.'));
      }
      setInviteUserName(''); setInviteEmail('');
      setNotice('Verification link sent. The account will be active after the user verifies their email.');
      await load(query, status);
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The invitation could not be sent.'); }
    finally { setBusy(''); }
  }

  async function loadHistory(user: AdminUser) {
    if (historyLoaded[user.id]) return;
    setHistoryLoading(user.id);
    try {
      const response = await fetch(`/api/admin/users/${encodeURIComponent(user.id)}/history`);
      if (!response.ok) throw new Error('Account history could not be loaded.');
      const history = await response.json() as AdminAudit[];
      setHistoryByUser(current => ({ ...current, [user.id]: history }));
      setHistoryLoaded(current => ({ ...current, [user.id]: true }));
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Account history could not be loaded.'); }
    finally { setHistoryLoading(''); }
  }

  if (forbidden) return <main className="admin-screen"><header><a href="/">← OpenBudget</a><h1>Administration</h1></header><section className="admin-card"><h2>Access denied</h2><p>An administrator account is required to open this page.</p></section></main>;

  return <main className="admin-screen">
    <header><a href="/">← OpenBudget</a><div><span className="eyebrow">OPENBUDGET CONTROL</span><h1>Administration</h1><p>Manage accounts and registration policy.</p></div></header>
    {error && <p className="admin-error" role="alert">{error}<button onClick={() => void load(query, status)}>Retry</button></p>}
    {notice && <p className="admin-notice" role="status">{notice}</p>}
    <section className="admin-card"><div className="admin-section-head"><div><h2>Registration</h2><p>New users must verify their email before signing in.</p></div><label>Registration mode<select aria-label="Registration mode" value={mode} disabled={busy === 'settings'} onChange={event => void saveMode(event.target.value)}>{modes.map(item => <option key={item} value={item}>{item === 'ApprovalRequired' ? 'Approval required' : item}</option>)}</select></label></div></section>
    <section className="admin-card"><div className="admin-section-head"><div><h2>Email delivery</h2><p>{emailSettings.source === 'environment' ? 'Using runtime SMTP configuration. Save a complete configuration here to manage it in OpenBudget.' : 'Configure the account verification, invitation, and password reset sender.'}</p></div><span className={`admin-status ${emailSettings.isConfigured ? 'status-active' : 'status-pending'}`}>{emailSettings.isConfigured ? 'Configured' : 'Not configured'}</span></div>
      <form className="admin-email-settings" onSubmit={event => void saveEmailSettings(event)}>
        <label>SMTP host<input aria-label="SMTP host" autoComplete="off" maxLength={255} required value={emailSettings.host} onChange={event => setEmailSettings(current => ({ ...current, host: event.target.value }))} placeholder="smtp.example.com" /></label>
        <label>Port<input aria-label="SMTP port" type="number" min="1" max="65535" required value={emailSettings.port} onChange={event => setEmailSettings(current => ({ ...current, port: Number(event.target.value) }))} /></label>
        <label>From email<input aria-label="SMTP from email" type="email" autoComplete="email" maxLength={256} required value={emailSettings.from} onChange={event => setEmailSettings(current => ({ ...current, from: event.target.value }))} placeholder="hello@example.com" /></label>
        <label>Username <span>(optional)</span><input aria-label="SMTP username" autoComplete="username" maxLength={256} value={emailSettings.username ?? ''} onChange={event => setEmailSettings(current => ({ ...current, username: event.target.value || null }))} /></label>
        <label>Password <span>{emailSettings.source === 'admin' && emailSettings.hasPassword ? '(leave blank to keep current)' : '(required with a username)'}</span><input aria-label="SMTP password" type="password" autoComplete="new-password" maxLength={512} value={smtpPassword} onChange={event => setSmtpPassword(event.target.value)} /></label>
        <label className="admin-checkbox"><input aria-label="Enable SMTP TLS" type="checkbox" checked={emailSettings.enableSsl} onChange={event => setEmailSettings(current => ({ ...current, enableSsl: event.target.checked }))} />Use TLS</label>
        <button type="submit" disabled={busy === 'email-settings' || loading}>{busy === 'email-settings' ? 'Saving…' : 'Save email settings'}</button>
      </form>
      <form className="admin-email-test" onSubmit={event => void sendTestEmail(event)}><label>Test recipient<input aria-label="Test email recipient" type="email" autoComplete="email" required value={testEmail} onChange={event => setTestEmail(event.target.value)} placeholder="you@example.com" /></label><button type="submit" disabled={!emailSettings.isConfigured || busy === 'email-test'}>{busy === 'email-test' ? 'Sending…' : 'Send test email'}</button></form>
    </section>
    <section className="admin-card"><h2>Dashboard</h2>{loading || !summary ? <p>Loading statistics…</p> : <>
      <div className="admin-stats">{[['Registered users', summary.totalUsers], ['Active', summary.activeUsers], ['Pending', summary.pendingUsers], ['Locked', summary.lockedUsers], ['Suspended', summary.suspendedUsers], ['Total budgets', summary.totalBudgets], ['Active budgets', summary.activeBudgets]].map(([label, value]) => <article key={label}><span>{label}</span><strong>{value}</strong></article>)}</div>
      <div className="admin-dashboard-details">
        <section><h3>Users by membership</h3>{summary.usersByMembership.length === 0 ? <p>No membership tiers configured.</p> : <ul>{summary.usersByMembership.map(item => <li key={item.membershipPlanId}><span>{item.name}</span><strong>{item.userCount}</strong></li>)}</ul>}</section>
        <section><h3>Recent registrations</h3>{summary.recentRegistrations.length === 0 ? <p>No registrations yet.</p> : <ul>{summary.recentRegistrations.map(item => <li key={item.userId}><span>{item.userName}<small>{new Date(item.createdAt).toLocaleString()}</small></span><span className={`admin-status status-${item.status.toLowerCase()}`}>{item.status}</span></li>)}</ul>}</section>
      </div>
    </>}</section>
    <section className="admin-card"><h2>Invite a user</h2><p>They will receive an expiring email verification link. No password is shared.</p><form className="admin-search" onSubmit={event => void inviteUser(event)}>
      <label>Username<input aria-label="Invitation username" autoComplete="off" maxLength={64} required value={inviteUserName} onChange={event => setInviteUserName(event.target.value)} /></label>
      <label>Email<input aria-label="Invitation email" type="email" autoComplete="email" maxLength={256} required value={inviteEmail} onChange={event => setInviteEmail(event.target.value)} /></label>
      <label>Membership<select aria-label="Invitation membership" value={inviteMembership} onChange={event => setInviteMembership(event.target.value)}>{membershipPlans.map(plan => <option key={plan.id} value={plan.id}>{plan.name}</option>)}</select></label>
      <button type="submit" disabled={busy === 'invite' || loading}>Send invitation</button>
    </form></section>
    {membershipPlans.length > 0 && <EntitlementSettings plans={membershipPlans} keys={entitlementKeys} />}
    <section className="admin-card"><div className="admin-section-head"><div><h2>Users</h2><p>{users.length} accounts shown · newest first</p></div><form className="admin-search" onSubmit={event => { event.preventDefault(); void load(query, status); }}><label>Search users<input aria-label="Search users" value={query} onChange={event => setQuery(event.target.value)} placeholder="Name or email" /></label><label>Status<select aria-label="Filter status" value={status} onChange={event => setStatus(event.target.value)}>{['All', 'Active', 'Pending', 'Locked', 'Suspended'].map(item => <option key={item}>{item}</option>)}</select></label><button type="submit">Apply</button></form></div>
      {loading ? <p aria-busy="true">Loading users…</p> : users.length === 0 ? <p>No matching users.</p> : <div className="admin-table-wrap"><table><thead><tr><th>User</th><th>Status</th><th>Membership</th><th>Created</th><th>Last sign-in</th><th>Actions</th></tr></thead><tbody>{users.map(user => { const draft = membershipDrafts[user.id] ?? { membershipPlanId: user.membershipPlanId, budgetLimitOverride: user.budgetLimitOverride?.toString() ?? '', startsOn: user.membershipStartsOn ?? '', expiresOn: user.membershipExpiresOn ?? '', billingProvider: user.billingProvider ?? '', externalSubscriptionId: user.externalSubscriptionId ?? '' }; return <tr key={user.id}><td><strong>{user.userName}{user.isBootstrapAdministrator ? ' · Permanent admin' : ''}</strong><small>{user.email}</small><details onToggle={event => { if (event.currentTarget.open) void loadHistory(user); }}><summary>View account history</summary>{historyLoading === user.id ? <small aria-busy="true">Loading history…</small> : !historyLoaded[user.id] ? null : historyByUser[user.id]?.length ? <ul className="admin-account-history">{historyByUser[user.id].map(entry => <li key={entry.id}><span>{entry.action}</span><small>{new Date(entry.createdAt).toLocaleString()}</small></li>)}</ul> : <small>No account events recorded.</small>}</details></td><td><span className={`admin-status status-${user.status.toLowerCase()}`}>{user.status}</span></td><td className="admin-membership"><label>Tier<select aria-label={`Membership for ${user.userName}`} value={draft.membershipPlanId} disabled={busy === user.id} onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, membershipPlanId: event.target.value } }))}>{membershipPlans.map(plan => <option key={plan.id} value={plan.id}>{plan.name} · {plan.maxActiveBudgets ?? 'Unlimited'} budgets</option>)}</select></label><label>Limit override<input aria-label={`Budget limit override for ${user.userName}`} type="number" min="1" max="1000" value={draft.budgetLimitOverride} disabled={busy === user.id} placeholder="Tier default" onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, budgetLimitOverride: event.target.value } }))} /></label><details><summary>Subscription metadata</summary><label>Starts on<input aria-label={`Membership starts for ${user.userName}`} type="date" value={draft.startsOn} onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, startsOn: event.target.value } }))} /></label><label>Expires on<input aria-label={`Membership expires for ${user.userName}`} type="date" value={draft.expiresOn} onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, expiresOn: event.target.value } }))} /></label><label>Billing provider<input aria-label={`Billing provider for ${user.userName}`} maxLength={80} value={draft.billingProvider} onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, billingProvider: event.target.value } }))} /></label><label>External subscription ID<input aria-label={`External subscription ID for ${user.userName}`} maxLength={160} value={draft.externalSubscriptionId} onChange={event => setMembershipDrafts(current => ({ ...current, [user.id]: { ...draft, externalSubscriptionId: event.target.value } }))} /></label></details><button disabled={busy === user.id} onClick={() => void saveMembership(user)}>Save membership</button><UserEntitlementOverrides userId={user.id} userName={user.userName} keys={entitlementKeys} /></td><td>{new Date(user.createdAt).toLocaleDateString()}</td><td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleDateString() : 'Never'}</td><td className="admin-actions">{user.email && user.status !== 'Pending' && <button disabled={busy === user.id} onClick={() => void action(user, 'reset-password')}>Send reset link</button>}{user.status === 'Pending' && <><button disabled={busy === user.id} onClick={() => void action(user, 'approve')}>Approve</button><button disabled={busy === user.id} onClick={() => void action(user, 'reject')}>Reject</button></>}{user.status === 'Active' && !user.isBootstrapAdministrator && <button disabled={busy === user.id} onClick={() => void action(user, 'suspend')}>Suspend</button>}{user.status === 'Suspended' && <button disabled={busy === user.id} onClick={() => void action(user, 'reactivate')}>Reactivate</button>}{user.status === 'Locked' && <><button disabled={busy === user.id} onClick={() => void action(user, 'unlock')}>Unlock</button>{!user.isBootstrapAdministrator && <button disabled={busy === user.id} onClick={() => void action(user, 'suspend')}>Suspend</button>}</>}</td></tr>; })}</tbody></table></div>}
    </section>
    <section className="admin-card"><h2>Recent security events</h2>{loading || !summary ? <p>Loading activity…</p> : summary.recentSecurityEvents.length === 0 ? <p>No security events recorded.</p> : <ul className="admin-audit">{summary.recentSecurityEvents.map(item => <li key={item.id}><span>{item.action}</span><small>Target {item.targetUserId} · {new Date(item.createdAt).toLocaleString()}</small></li>)}</ul>}</section>
    {!loading && <button className="admin-refresh" onClick={() => void load(query, status)}>Refresh</button>}
  </main>;
}
