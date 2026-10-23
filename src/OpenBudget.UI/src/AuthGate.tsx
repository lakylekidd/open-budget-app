import { useEffect, useState, type FormEvent, type ReactNode } from 'react';

type Session = { id: string; userName: string; email: string | null; isAdministrator: boolean };

export function AuthGate({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function refreshSession() {
    try {
      const response = await fetch('/api/auth/session', { credentials: 'same-origin' });
      setSession(response.ok ? await response.json() as Session : null);
      if (!response.ok && window.location.pathname !== '/login') window.history.replaceState(null, '', `/login${window.location.search}`);
    } catch { setSession(null); }
    finally { setLoading(false); }
  }

  useEffect(() => { void refreshSession(); }, []);

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    try {
      const response = await fetch('/api/auth/login', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userNameOrEmail: form.get('username'), password: form.get('password') }) });
      if (!response.ok) throw new Error(response.status === 429 ? 'Too many attempts. Try again later.' : 'Sign-in failed. Check your username or email and password.');
      setSession(await response.json() as Session);
      if (window.location.pathname === '/login') window.history.replaceState(null, '', '/');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Could not sign in.'); }
    finally { setBusy(false); }
  }

  async function logout() {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' }).catch(() => undefined);
    setSession(null);
    window.history.replaceState(null, '', `/login${window.location.search}`);
  }

  if (loading) return <main className="auth-screen" aria-busy="true"><p>Loading your session…</p></main>;
  if (!session) return <main className="auth-screen"><form className="auth-card" onSubmit={login}>
    <span className="eyebrow">OPENBUDGET</span><h1>Sign in</h1><p>Enter your username or email and password.</p>
    <label>Username or email<input name="username" autoComplete="username" required /></label>
    <label>Password<input name="password" type="password" autoComplete="current-password" required /></label>
    {error && <p role="alert" className="auth-error">{error}</p>}
    <button className="button button-primary" type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
  </form></main>;
  return <><div className="session-bar"><span>{session.userName}{session.isAdministrator ? ' · Administrator' : ''}</span><button onClick={() => void logout()}>Sign out</button></div>{children}</>;
}
