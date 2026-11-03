import { useEffect, useState } from 'react';
import type { Account } from './api';
import { RegisterIcon } from './AccountRegister';
import { requestJson } from './api';

type BankOverview = { connections: Connection[]; pendingImportCount: number; providerConfigured: boolean };
type Connection = { id: string; bankName: string; country: string; status: string; accessValidUntil: string; syncIntervalMinutes: number; lastSyncedAt: string | null; nextSyncAt: string; lastErrorCode: string | null; accounts: LinkedAccount[] };
type LinkedAccount = { id: string; name: string; currency: string; accountId: string | null; isAvailable: boolean };
type Bank = { name: string; country: string; bic?: string; logo?: string; maximumConsentValidity: number };
type ImportRow = { id: string; linkedAccountId: string; accountName: string; date: string; amount: number; currency: string; payee: string; memo: string; providerStatus: string; reviewStatus: string; candidate: { id: string; date: string; amount: number; payee: string; score: number } | null; suggestedPayee: string | null; payeeSimilarity: number };

async function requestAction(path: string, options: RequestInit) {
  const response = await fetch(path, options);
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.error ?? `Request failed (${response.status}).`);
  }
}

export function BankConnectionsPanel({ accounts, onChanged }: { accounts: Account[]; onChanged: () => void }) {
  const [open, setOpen] = useState(false);
  const [overview, setOverview] = useState<BankOverview | null>(null);
  const [imports, setImports] = useState<ImportRow[]>([]);
  const [banks, setBanks] = useState<Bank[]>([]);
  const [country, setCountry] = useState('NL');
  const [bankName, setBankName] = useState('');
  const [useSuggestedPayee, setUseSuggestedPayee] = useState<Record<string, boolean>>({});
  const [interval, setInterval] = useState(60);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function refresh() {
    const [nextOverview, nextImports] = await Promise.all([
      requestJson<BankOverview>('/api/banking/overview'), requestJson<ImportRow[]>('/api/banking/imports')
    ]);
    setOverview(nextOverview); setImports(nextImports);
  }
  useEffect(() => { if (open) void refresh().catch(e => setError(e instanceof Error ? e.message : 'Could not load bank connections.')); }, [open]);
  async function loadBanks(nextCountry = country) {
    setError('');
    try { const result = await requestJson<Bank[]>(`/api/banking/banks?country=${encodeURIComponent(nextCountry)}`); setBanks(result); setBankName(''); }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not load banks.'); }
  }
  async function action(work: () => Promise<unknown>) {
    setBusy(true); setError('');
    try { await work(); await refresh(); onChanged(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Bank action failed.'); }
    finally { setBusy(false); }
  }
  async function start() {
    setBusy(true); setError('');
    try {
      const result = await requestJson<{ authorizationUrl: string }>('/api/banking/connections/start', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ country, bankName, syncIntervalMinutes: interval }) });
      window.location.assign(result.authorizationUrl);
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not start bank authorization.'); setBusy(false); }
  }
  async function reauthorize(connection: Connection) {
    setBusy(true); setError('');
    try {
      const result = await requestJson<{ authorizationUrl: string }>(`/api/banking/connections/${connection.id}/reauthorize`, { method: 'POST' });
      window.location.assign(result.authorizationUrl);
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not restart bank authorization.'); setBusy(false); }
  }

  return <>
    <button className="bank-connect-open" onClick={() => { setOpen(true); setError(''); }}><RegisterIcon name="bank"/> Bank connections{overview?.pendingImportCount ? ` (${overview.pendingImportCount})` : ''}</button>
    {open && <div className="bank-panel-backdrop" role="presentation" onMouseDown={e => { if (e.target === e.currentTarget) setOpen(false); }}>
      <section className="bank-panel" role="dialog" aria-modal="true" aria-labelledby="bank-panel-title">
        <header><h2 id="bank-panel-title">Bank connections</h2><button aria-label="Close bank connections" onClick={() => setOpen(false)}>×</button></header>
        {error && <p role="alert" className="bank-error">{error}</p>}
        {!overview?.providerConfigured && <p className="bank-note">Enable Banking is not configured on this server. Add its application ID, registered RSA private key path, and approved callback URL to the server configuration.</p>}
        <section className="bank-connect-form" aria-label="Link another bank account">
          <h3>Link another bank account</h3>
          <label>Country code <input aria-label="Country code" value={country} maxLength={2} onChange={e => { setCountry(e.target.value.toUpperCase().replace(/[^A-Z]/g, '')); setBanks([]); }}/></label>
          <button onClick={() => void loadBanks()} disabled={busy || !overview?.providerConfigured}>Find supported banks</button>
          {banks.length > 0 && <><label>Bank <select value={bankName} onChange={e => setBankName(e.target.value)}><option value="">Choose a bank</option>{banks.map(bank => <option key={`${bank.country}:${bank.name}`} value={bank.name}>{bank.name}</option>)}</select></label><label>Sync every <select value={interval} onChange={e => setInterval(Number(e.target.value))}><option value={15}>15 minutes</option><option value={30}>30 minutes</option><option value={60}>1 hour</option><option value={180}>3 hours</option><option value={360}>6 hours</option><option value={720}>12 hours</option><option value={1440}>24 hours</option></select></label><button className="button-primary" disabled={busy || !bankName} onClick={() => void start()}>Continue to bank</button></>}
        </section>
        <section><h3>Connected accounts</h3>{!overview?.connections.length && <p>No bank connections yet.</p>}{overview?.connections.map(connection => <article className="bank-connection" key={connection.id}>
          <div className="bank-connection-heading"><strong>{connection.bankName} · {connection.country}</strong><span>{connection.status.replaceAll('_', ' ')}</span></div>
          <p>Syncs every {connection.syncIntervalMinutes} minutes · {connection.lastSyncedAt ? `Last synced ${new Date(connection.lastSyncedAt).toLocaleString()}` : 'Not synced yet'} · Consent expires {new Date(connection.accessValidUntil).toLocaleDateString()}</p>
          {connection.lastErrorCode && <p className="bank-error">Sync issue: {connection.lastErrorCode.replaceAll('_', ' ')}</p>}
          {connection.accounts.map(linked => <div className="bank-linked-account" key={linked.id}><span>{linked.name} ({linked.currency}){!linked.isAvailable ? ' · not provided by the bank after reconnect' : ''}</span><select aria-label={`Map ${linked.name} to account`} disabled={!linked.isAvailable || busy} value={linked.accountId ?? ''} onChange={e => e.target.value && void action(() => requestJson(`/api/banking/accounts/${linked.id}/map`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ accountId: e.target.value }) }))}><option value="">Choose OpenBudget account</option>{accounts.filter(a => !a.isClosed).map(account => <option key={account.id} value={account.id}>{account.name}</option>)}</select></div>)}
          <label>Sync every <select value={connection.syncIntervalMinutes} onChange={e => void action(() => requestJson(`/api/banking/connections/${connection.id}/interval?minutes=${e.target.value}`, { method: 'PUT' }))}><option value={15}>15 minutes</option><option value={30}>30 minutes</option><option value={60}>1 hour</option><option value={180}>3 hours</option><option value={360}>6 hours</option><option value={720}>12 hours</option><option value={1440}>24 hours</option></select></label>
          <div className="bank-actions">{connection.status === 'reauthorization_required' && <button className="button-primary" disabled={busy} onClick={() => void reauthorize(connection)}>Reconnect</button>}<button disabled={busy || connection.status !== 'active'} onClick={() => void action(() => requestJson(`/api/banking/connections/${connection.id}/sync`, { method: 'POST' }))}>Sync now</button><button disabled={busy} onClick={() => { if (window.confirm(`Disconnect ${connection.bankName}?`)) void action(() => requestAction(`/api/banking/connections/${connection.id}`, { method: 'DELETE' })); }}>Disconnect</button></div>
        </article>)}</section>
        <section><h3>Imports to review ({imports.length})</h3>{imports.length === 0 && <p>No bank transactions need review.</p>}{imports.map(row => <article className="bank-import-row" key={row.id}>
          <div className="bank-import-main"><span>{row.date} · {row.payee || 'Unknown payee'}</span><strong>{row.amount.toLocaleString(undefined, { style: 'currency', currency: row.currency || 'EUR' })}</strong></div>
          <small>{row.accountName}{row.memo ? ` · ${row.memo}` : ''}</small>
          {row.suggestedPayee && row.payeeSimilarity >= 0.72 && <label className="bank-payee-choice">Payee <select aria-label={`Payee for ${row.payee || 'bank import'}`} value={useSuggestedPayee[row.id] === false ? 'bank' : 'suggested'} onChange={e => setUseSuggestedPayee(current => ({ ...current, [row.id]: e.target.value === 'suggested' }))}><option value="suggested">Use {row.suggestedPayee} ({Math.round(row.payeeSimilarity * 100)}% similar)</option><option value="bank">Keep bank payee: {row.payee || 'Unknown payee'}</option></select></label>}
          {row.candidate ? <div className="bank-candidate"><RegisterIcon name="link" size={14}/><span>Possible match: {row.candidate.payee} · {row.candidate.date} · {row.candidate.amount.toLocaleString(undefined, { style: 'currency', currency: row.currency || 'EUR' })} ({Math.round(row.candidate.score * 100)}% match)</span><button disabled={busy} onClick={() => void action(() => requestAction(`/api/banking/imports/${row.id}/review`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action: 'verify-match', matchedTransactionId: row.candidate!.id }) }))}>Verify match</button><button disabled={busy} onClick={() => void action(() => requestAction(`/api/banking/imports/${row.id}/review`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action: 'accept', useSuggestedPayee: Boolean(row.suggestedPayee) && useSuggestedPayee[row.id] !== false }) }))}>Add separately</button></div> : <div className="bank-import-actions"><span>{row.suggestedPayee ? `Payee suggestion ${Math.round(row.payeeSimilarity * 100)}% similar` : 'No similar payee found'}</span><button disabled={busy} onClick={() => void action(() => requestAction(`/api/banking/imports/${row.id}/review`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action: 'accept', useSuggestedPayee: Boolean(row.suggestedPayee) && useSuggestedPayee[row.id] !== false }) }))}>Add transaction</button></div>}
          <button className="bank-dismiss" disabled={busy} onClick={() => void action(() => requestAction(`/api/banking/imports/${row.id}/review`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action: 'dismiss' }) }))}>Dismiss</button>
        </article>)}</section>
      </section>
    </div>}
  </>;
}
