import { useEffect, useRef, useState } from 'react';
import { AgentTokens } from './AgentTokens';
import { ThemePicker } from './theme/ThemePicker';
import { applyTheme, currentTheme, saveThemePreference, type ThemeKey } from './theme/themes';
import { credentialToJson, parseCreationOptions } from './passkeys';

type UserProfile = { id: string; userName: string; email: string | null; isAdministrator: boolean };
type Passkey = { id: string; name: string; createdAt: string };

export function ProfileSettings({ onClose }: { onClose: () => void }) {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [userName, setUserName] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [theme, setTheme] = useState<ThemeKey>(currentTheme);
  const [themeError, setThemeError] = useState('');
  const [passkeys, setPasskeys] = useState<Passkey[]>([]);
  const [passkeyName, setPasskeyName] = useState('This device');
  const themeRequest = useRef(0);

  useEffect(() => {
    let active = true;
    void fetch('/api/auth/profile').then(async response => {
      if (!response.ok) throw new Error('Profile could not be loaded.');
      const value = await response.json() as UserProfile;
      if (active) { setProfile(value); setUserName(value.userName); }
    }).catch(() => { if (active) setError('Profile could not be loaded. Retry by closing and reopening this panel.'); });
    void fetch('/api/auth/passkeys').then(async response => {
      if (!response.ok) throw new Error('Passkeys could not be loaded.');
      const value = await response.json() as Passkey[];
      if (active) setPasskeys(value);
    }).catch(() => { if (active) setError('Passkeys could not be loaded. Retry by closing and reopening this panel.'); });
    return () => { active = false; };
  }, []);

  // The theme switches immediately and is then saved to the account; if saving fails the previous theme is restored.
  async function chooseTheme(next: ThemeKey) {
    const previous = theme;
    const request = ++themeRequest.current;
    setThemeError(''); setTheme(next); applyTheme(next);
    try { await saveThemePreference(next); }
    catch {
      if (request !== themeRequest.current) return;
      setTheme(previous); applyTheme(previous);
      setThemeError('Your theme could not be saved, so the previous theme was restored. Try again.');
    }
  }

  async function saveProfile(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy('profile'); setError(''); setMessage('');
    try {
      const response = await fetch('/api/auth/profile', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName }) });
      const result = await response.json().catch(() => ({})) as UserProfile & { error?: string; errors?: string[] };
      if (!response.ok) throw new Error(result.error ?? result.errors?.join(', ') ?? 'Profile could not be updated.');
      setProfile(result); setUserName(result.userName); setMessage('Profile updated.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Profile could not be updated.'); }
    finally { setBusy(''); }
  }

  async function changePassword(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setMessage('');
    if (newPassword !== confirmPassword) { setError('New password and confirmation do not match.'); return; }
    setBusy('password');
    try {
      const response = await fetch('/api/auth/change-password', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ currentPassword, newPassword }) });
      if (!response.ok) throw new Error(response.status === 400 ? 'Current password is incorrect or the new password does not meet the password policy.' : 'Password could not be changed.');
      setCurrentPassword(''); setNewPassword(''); setConfirmPassword('');
      setMessage('Password changed. Your session has ended; sign in again to continue.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Password could not be changed.'); }
    finally { setBusy(''); }
  }

  async function addPasskey() {
    setBusy('passkey'); setError(''); setMessage('');
    try {
      if (!window.PublicKeyCredential || !navigator.credentials) throw new Error('Passkeys are not supported by this browser.');
      const optionsResponse = await fetch('/api/auth/passkeys/registration-options', { method: 'POST', credentials: 'same-origin' });
      const options = await optionsResponse.json().catch(() => ({})) as { error?: string };
      if (!optionsResponse.ok) throw new Error(options.error ?? 'Passkey setup could not be started.');
      const credential = await navigator.credentials.create({ publicKey: parseCreationOptions(options as Parameters<typeof parseCreationOptions>[0]) });
      if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey setup was canceled.');
      const response = await fetch('/api/auth/passkeys', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ credentialJson: credentialToJson(credential), name: passkeyName }) });
      const result = await response.json().catch(() => ({})) as Passkey & { error?: string };
      if (!response.ok) throw new Error(result.error ?? 'Passkey could not be saved.');
      setPasskeys(current => [...current, result]); setMessage('Passkey added. You can still sign in with your password.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Passkey could not be added.'); }
    finally { setBusy(''); }
  }

  async function removePasskey(passkey: Passkey) {
    setBusy('passkey'); setError(''); setMessage('');
    try {
      const response = await fetch(`/api/auth/passkeys/${encodeURIComponent(passkey.id)}`, { method: 'DELETE', credentials: 'same-origin' });
      if (!response.ok) throw new Error('Passkey could not be removed.');
      setPasskeys(current => current.filter(item => item.id !== passkey.id)); setMessage('Passkey removed. Password sign-in remains available.');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Passkey could not be removed.'); }
    finally { setBusy(''); }
  }

  return <div className="profile-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="profile-dialog" role="dialog" aria-modal="true" aria-labelledby="profile-title">
      <div className="profile-heading"><h2 id="profile-title">Profile and security</h2><button type="button" aria-label="Close profile settings" onClick={onClose}>×</button></div>
      {error && <p className="profile-error" role="alert">{error}</p>}{message && <p className="profile-message" role="status">{message}{message.startsWith('Password changed') && <> <a href="/login">Sign in</a></>}</p>}
      <section className="profile-form" aria-labelledby="appearance-title"><h3 id="appearance-title">Appearance</h3><ThemePicker value={theme} onChange={next => void chooseTheme(next)} />{themeError && <p className="profile-error" role="alert">{themeError}</p>}</section>
      {!profile && !error ? <p aria-busy="true">Loading profile…</p> : <>
        <form className="profile-form" onSubmit={saveProfile}><h3>Profile</h3><label>Username<input aria-label="Username" required maxLength={64} value={userName} onChange={event => setUserName(event.target.value)} /></label><label>Email<input aria-label="Email" value={profile?.email ?? ''} disabled /></label><button type="submit" disabled={!profile || busy !== ''}>Save profile</button></form>
        <form className="profile-form" onSubmit={changePassword}><h3>Change password</h3><label>Current password<input aria-label="Current password" type="password" autoComplete="current-password" required value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label><label>New password<input aria-label="New password" type="password" autoComplete="new-password" required value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label><label>Confirm new password<input aria-label="Confirm new password" type="password" autoComplete="new-password" required value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></label><button type="submit" disabled={busy !== ''}>Change password</button></form>
        <section className="profile-form" aria-labelledby="passkeys-title"><h3 id="passkeys-title">Passkeys</h3><p>Add an optional passkey for passwordless sign-in. Your password and recovery options continue to work.</p>{passkeys.length > 0 ? <ul>{passkeys.map(passkey => <li key={passkey.id}>{passkey.name} <button type="button" onClick={() => void removePasskey(passkey)} disabled={busy !== ''}>Remove</button></li>)}</ul> : <p>No passkeys added.</p>}<label>Passkey name<input aria-label="Passkey name" maxLength={60} value={passkeyName} onChange={event => setPasskeyName(event.target.value)} /></label><button type="button" onClick={() => void addPasskey()} disabled={!profile || busy !== '' || passkeys.length >= 10}>Add passkey</button></section>
        {profile && <AgentTokens isAdministrator={profile.isAdministrator} />}
      </>}
    </section>
  </div>;
}
