import { useEffect, useState, type FormEvent, type ReactNode } from 'react';

type Session = { id: string; userName: string; email: string | null; isAdministrator: boolean };

function AuthBrand() {
  return <header className="auth-brand">
    <img className="auth-logo" src="/openbudget-logo.svg" alt="OpenBudget" />
    <div>
      <span>OPENBUDGET</span>
      <p>Make every euro feel intentional.</p>
    </div>
  </header>;
}

export function AuthGate({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [registrationMode, setRegistrationMode] = useState('Disabled');

  async function refreshSession() {
    try {
      const response = await fetch('/api/auth/session', { credentials: 'same-origin' });
      setSession(response.ok ? await response.json() as Session : null);
      if (!response.ok) {
        const modeResponse = await fetch('/api/auth/registration-mode');
        if (modeResponse.ok) setRegistrationMode((await modeResponse.json() as { mode: string }).mode);
        if (window.location.pathname === '/confirm-email') {
          const query = new URLSearchParams(window.location.search);
          const confirmation = await fetch('/api/auth/confirm-email', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userId: query.get('userId'), token: query.get('token') }) });
          setNotice(confirmation.ok ? 'Email verified. You can sign in now.' : 'This verification link is invalid or expired.');
        } else if (!['/login', '/register', '/forgot-password', '/reset-password'].includes(window.location.pathname)) {
          window.history.replaceState(null, '', `/login${window.location.search}`);
        }
      }
    } catch { setSession(null); }
    finally { setLoading(false); }
  }

  useEffect(() => { void refreshSession(); }, []);
  useEffect(() => {
    const signOut = () => { void logout(); };
    window.addEventListener('openbudget:signout', signOut);
    return () => window.removeEventListener('openbudget:signout', signOut);
  }, []);

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

  async function submitPublicForm(event: FormEvent<HTMLFormElement>, endpoint: string, body: Record<string, string>) {
    event.preventDefault(); setBusy(true); setError(''); setNotice('');
    try {
      const response = await fetch(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.error ?? result.errors?.join(', ') ?? 'Request could not be completed.');
      setNotice(result.message ?? 'Request completed.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Request could not be completed.'); }
    finally { setBusy(false); }
  }

  async function logout() {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' }).catch(() => undefined);
    setSession(null);
    window.history.replaceState(null, '', `/login${window.location.search}`);
  }

  if (loading) return <main className="auth-screen" aria-busy="true"><p>Loading your session…</p></main>;
  if (session && !session.isAdministrator && window.location.pathname !== '/admin') return <main className="auth-screen"><section className="auth-card"><AuthBrand /><h1>Budget access is being set up</h1><p>Your account is signed in, but no personal budget has been assigned yet. Contact an administrator to continue.</p><button className="button button-primary" onClick={() => void logout()}>Sign out</button></section></main>;
  if (!session && window.location.pathname === '/register' && registrationMode !== 'Disabled') return <main className="auth-screen"><form className="auth-card" onSubmit={event => { const form = new FormData(event.currentTarget); void submitPublicForm(event, '/api/auth/register', { userName: String(form.get('username')), email: String(form.get('email')), password: String(form.get('password')) }); }}>
    <AuthBrand /><h1>Create account</h1><label>Username<input name="username" autoComplete="username" required /></label><label>Email<input name="email" type="email" autoComplete="email" required /></label><label>Password<input name="password" type="password" autoComplete="new-password" required /></label>{error && <p role="alert" className="auth-error">{error}</p>}{notice && <p role="status">{notice}</p>}<button className="button button-primary" disabled={busy}>Create account</button><a className="auth-secondary-link" href="/login">Back to sign in</a>
  </form></main>;
  if (!session && window.location.pathname === '/forgot-password') return <main className="auth-screen"><form className="auth-card" onSubmit={event => { const form = new FormData(event.currentTarget); void submitPublicForm(event, '/api/auth/forgot-password', { email: String(form.get('email')) }); }}><AuthBrand /><h1>Reset password</h1><label>Email<input name="email" type="email" required /></label>{error && <p role="alert" className="auth-error">{error}</p>}{notice && <p role="status">{notice}</p>}<button className="button button-primary" disabled={busy}>Send reset link</button><a className="auth-secondary-link" href="/login">Back to sign in</a></form></main>;
  if (!session && window.location.pathname === '/reset-password') return <main className="auth-screen"><form className="auth-card" onSubmit={event => { const form = new FormData(event.currentTarget); const query = new URLSearchParams(window.location.search); void submitPublicForm(event, '/api/auth/reset-password', { userId: query.get('userId') ?? '', token: query.get('token') ?? '', newPassword: String(form.get('password')) }); }}><AuthBrand /><h1>Choose a new password</h1><label>New password<input name="password" type="password" autoComplete="new-password" required /></label>{error && <p role="alert" className="auth-error">{error}</p>}{notice && <p role="status">{notice}</p>}<button className="button button-primary" disabled={busy}>Update password</button><a className="auth-secondary-link" href="/login">Back to sign in</a></form></main>;
  if (!session) return <main className="auth-screen"><form className="auth-card" onSubmit={login}>
    <AuthBrand /><div className="auth-introduction"><h1>Welcome back</h1><p>Sign in to give every euro a job.</p></div>
    <label>Username or email<input name="username" autoComplete="username" required /></label>
    <label>Password<input name="password" type="password" autoComplete="current-password" required /></label>
    {error && <p role="alert" className="auth-error">{error}</p>}
    {notice && <p role="status">{notice}</p>}<button className="button button-primary" type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button><div className="auth-links"><a className="auth-recovery-link" href="/forgot-password">Forgot password?</a>{registrationMode !== 'Disabled' && <a className="auth-secondary-link" href="/register">Create an account</a>}</div>
  </form></main>;
  return <>{children}</>;
}
