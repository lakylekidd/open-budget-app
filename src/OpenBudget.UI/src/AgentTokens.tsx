import { useEffect, useState } from 'react';
import './agentTokens.css';

type Permission = 'Admin' | 'Budget' | 'Reporting';
export type AgentToken = {
  id: string; name: string; prefix: string; permission: Permission; budgets: { id: string; name: string }[];
  createdAt: string; expiresAt: string; lastUsedAt: string | null; revokedAt: string | null; status: 'active' | 'expired' | 'revoked';
};
type Activity = { id: string; tool: string; budgetId: string | null; success: boolean; outcome: string; createdAt: string };
type BudgetOption = { id: string; name: string; isArchived: boolean; accessRole?: string };

const permissionHelp: Record<Permission, string> = {
  Reporting: 'Read-only access to the selected budgets: Plan, accounts, transactions, payees, schedules and planning.',
  Budget: 'Read the selected budgets’ Plan and change category assignments (set assigned, move money). No ledger or admin changes.',
  Admin: 'Everything you can do in OpenBudget, including user administration, across every budget you can access.',
};

const tokensPath = '/api/auth/agent-tokens';
const formatDate = (value: string | null) => value ? new Date(value).toLocaleString('en-IE', { dateStyle: 'medium', timeStyle: 'short' }) : 'Never';

async function readJson<T>(response: Response, fallback: string): Promise<T> {
  const body = await response.json().catch(() => ({})) as T & { error?: string };
  if (!response.ok) throw new Error(body.error ?? fallback);
  return body;
}

export function AgentTokens({ isAdministrator }: { isAdministrator: boolean }) {
  const [tokens, setTokens] = useState<AgentToken[] | null>(null);
  const [budgets, setBudgets] = useState<BudgetOption[]>([]);
  const [loadError, setLoadError] = useState('');
  const [reload, setReload] = useState(0);
  const [name, setName] = useState('');
  const [permission, setPermission] = useState<Permission>('Reporting');
  const [selectedBudgets, setSelectedBudgets] = useState<string[]>([]);
  const [expiresInDays, setExpiresInDays] = useState('90');
  const [formError, setFormError] = useState('');
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<{ token: string; name: string } | null>(null);
  const [copied, setCopied] = useState('');
  const [confirmRevoke, setConfirmRevoke] = useState<string | null>(null);
  const [activity, setActivity] = useState<Record<string, Activity[] | 'loading' | 'error'>>({});

  useEffect(() => {
    const controller = new AbortController();
    setLoadError('');
    Promise.all([
      fetch(tokensPath, { signal: controller.signal }).then(response => readJson<AgentToken[]>(response, 'Agent tokens could not be loaded.')),
      fetch('/api/budgets', { signal: controller.signal }).then(response => readJson<{ budgets: BudgetOption[] }>(response, 'Budgets could not be loaded.')),
    ]).then(([tokenList, budgetList]) => {
      setTokens(tokenList);
      setBudgets(budgetList.budgets.filter(budget => !budget.isArchived));
    }).catch(error => { if (!controller.signal.aborted) setLoadError(error instanceof Error ? error.message : 'Agent tokens could not be loaded.'); });
    return () => controller.abort();
  }, [reload]);

  function toggleBudget(id: string) {
    setSelectedBudgets(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id]);
  }

  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(''); setCopied('');
    const days = Number(expiresInDays);
    if (!name.trim()) { setFormError('Enter a token name.'); return; }
    if (!Number.isInteger(days) || days < 1 || days > 365) { setFormError('Expiry must be between 1 and 365 days.'); return; }
    if (permission !== 'Admin' && selectedBudgets.length === 0) { setFormError('Choose at least one budget for this token.'); return; }
    setBusy(true);
    try {
      const response = await fetch(tokensPath, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: name.trim(), permission, budgetIds: permission === 'Admin' ? [] : selectedBudgets, expiresInDays: days }),
      });
      const result = await readJson<{ token: string; details: AgentToken }>(response, 'The token could not be created.');
      setCreated({ token: result.token, name: result.details.name });
      setTokens(current => [result.details, ...(current ?? [])]);
      setName(''); setSelectedBudgets([]); setExpiresInDays('90'); setPermission('Reporting');
    } catch (error) {
      setFormError(error instanceof Error ? error.message : 'The token could not be created.');
    } finally { setBusy(false); }
  }

  async function copyToken() {
    if (!created) return;
    try {
      await navigator.clipboard.writeText(created.token);
      setCopied('Copied to clipboard.');
    } catch { setCopied('Copy failed. Select the token and copy it manually.'); }
  }

  async function revoke(token: AgentToken) {
    setBusy(true); setFormError('');
    try {
      const response = await fetch(`${tokensPath}/${token.id}/revoke`, { method: 'POST' });
      if (!response.ok) throw new Error('The token could not be revoked. Reload and try again.');
      setTokens(current => (current ?? []).map(item => item.id === token.id ? { ...item, status: 'revoked', revokedAt: new Date().toISOString() } : item));
      setConfirmRevoke(null);
    } catch (error) { setFormError(error instanceof Error ? error.message : 'The token could not be revoked.'); }
    finally { setBusy(false); }
  }

  async function toggleActivity(id: string) {
    if (activity[id] && activity[id] !== 'error') { setActivity(current => { const next = { ...current }; delete next[id]; return next; }); return; }
    setActivity(current => ({ ...current, [id]: 'loading' }));
    try {
      const response = await fetch(`${tokensPath}/${id}/activity?limit=20`);
      const rows = await readJson<Activity[]>(response, 'Activity could not be loaded.');
      setActivity(current => ({ ...current, [id]: rows }));
    } catch { setActivity(current => ({ ...current, [id]: 'error' })); }
  }

  const budgetName = (id: string | null) => id ? budgets.find(budget => budget.id === id)?.name ?? 'Budget' : '—';

  return <section className="agent-tokens" aria-labelledby="agent-tokens-title">
    <h3 id="agent-tokens-title">Agent tokens</h3>
    <p className="agent-tokens-intro">Personal access tokens let your own AI agents use OpenBudget through the MCP server at <code>/mcp</code>. A token acts as you, limited to its permission and budgets.</p>

    {created && <div className="agent-token-created" aria-live="polite">
      <strong>Copy “{created.name}” now — it will not be shown again.</strong>
      <code aria-label="New agent token">{created.token}</code>
      <div className="agent-token-actions">
        <button type="button" onClick={() => void copyToken()}>Copy token</button>
        <button type="button" onClick={() => { setCreated(null); setCopied(''); }}>Done</button>
      </div>
      {copied && <span className="agent-token-hint">{copied}</span>}
    </div>}

    <form className="agent-token-form" onSubmit={create} aria-label="Create agent token">
      <label>Token name<input aria-label="Token name" maxLength={80} value={name} onChange={event => setName(event.target.value)} placeholder="e.g. Laptop assistant" /></label>
      <label>Permission<select aria-label="Permission" value={permission} onChange={event => setPermission(event.target.value as Permission)}>
        <option value="Reporting">Reporting (read-only)</option>
        <option value="Budget">Budget (Plan assignments)</option>
        {isAdministrator && <option value="Admin">Admin (full access)</option>}
      </select></label>
      <p className="agent-token-hint">{permissionHelp[permission]}</p>
      {permission !== 'Admin' && <fieldset className="agent-token-budgets">
        <legend>Budgets</legend>
        {budgets.length === 0 ? <span className="agent-token-hint">You have no active budgets to share with an agent.</span>
          : budgets.map(budget => <label key={budget.id} className="agent-token-budget"><input type="checkbox" checked={selectedBudgets.includes(budget.id)} onChange={() => toggleBudget(budget.id)} />{budget.name}</label>)}
      </fieldset>}
      <label>Expires in (days)<input aria-label="Expires in (days)" type="number" min={1} max={365} value={expiresInDays} onChange={event => setExpiresInDays(event.target.value)} /></label>
      {formError && <p className="agent-token-error" aria-live="assertive">{formError}</p>}
      <button type="submit" className="agent-token-primary" disabled={busy}>Create token</button>
    </form>

    {loadError ? <p className="agent-token-error">{loadError} <button type="button" onClick={() => setReload(value => value + 1)}>Retry</button></p>
      : tokens === null ? <p aria-busy="true" className="agent-token-hint">Loading agent tokens…</p>
      : tokens.length === 0 ? <p className="agent-token-hint">No agent tokens yet.</p>
      : <ul className="agent-token-list" aria-label="Agent tokens">
        {tokens.map(token => <li key={token.id} className={`agent-token-row ${token.status}`}>
          <div className="agent-token-summary">
            <strong>{token.name}</strong>
            <span className={`agent-token-badge ${token.permission.toLowerCase()}`}>{token.permission}</span>
            <span className={`agent-token-status ${token.status}`}>{token.status === 'active' ? 'Active' : token.status === 'expired' ? 'Expired' : 'Revoked'}</span>
          </div>
          <div className="agent-token-meta">
            <code>{token.prefix}…</code>
            <span>{token.permission === 'Admin' ? 'All your budgets' : token.budgets.map(budget => budget.name).join(', ')}</span>
            <span>Created {formatDate(token.createdAt)}</span>
            <span>Expires {formatDate(token.expiresAt)}</span>
            <span>Last used {formatDate(token.lastUsedAt)}</span>
          </div>
          <div className="agent-token-actions">
            <button type="button" aria-expanded={Boolean(activity[token.id])} onClick={() => void toggleActivity(token.id)}>Recent activity</button>
            {token.status === 'active' && confirmRevoke !== token.id && <button type="button" className="agent-token-danger" onClick={() => setConfirmRevoke(token.id)}>Revoke</button>}
          </div>
          {confirmRevoke === token.id && <div className="agent-token-confirm" role="group" aria-label={`Confirm revoking ${token.name}`}>
            <span>Revoke “{token.name}”? Agents using it lose access immediately. This cannot be undone.</span>
            <button type="button" onClick={() => setConfirmRevoke(null)}>Cancel</button>
            <button type="button" className="agent-token-danger" disabled={busy} onClick={() => void revoke(token)}>Confirm revoke</button>
          </div>}
          {activity[token.id] === 'loading' && <p className="agent-token-hint" aria-busy="true">Loading activity…</p>}
          {activity[token.id] === 'error' && <p className="agent-token-error">Activity could not be loaded. <button type="button" onClick={() => void toggleActivity(token.id)}>Retry</button></p>}
          {Array.isArray(activity[token.id]) && ((activity[token.id] as Activity[]).length === 0
            ? <p className="agent-token-hint">No agent activity yet.</p>
            : <table className="agent-token-activity" aria-label={`Recent activity for ${token.name}`}>
              <thead><tr><th>Tool</th><th>Budget</th><th>Result</th><th>When</th></tr></thead>
              <tbody>{(activity[token.id] as Activity[]).map(row => <tr key={row.id}>
                <td><code>{row.tool}</code></td><td>{budgetName(row.budgetId)}</td>
                <td className={row.success ? 'ok' : 'failed'}>{row.success ? 'Succeeded' : row.outcome.replace('_', ' ')}</td>
                <td>{formatDate(row.createdAt)}</td>
              </tr>)}</tbody>
            </table>)}
        </li>)}
      </ul>}
  </section>;
}
