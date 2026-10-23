import { useEffect, useState } from 'react';

type UserProfile = { id: string; userName: string; email: string | null; isAdministrator: boolean };

export function ProfileSettings({ onClose }: { onClose: () => void }) {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [userName, setUserName] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;
    void fetch('/api/auth/profile').then(async response => {
      if (!response.ok) throw new Error('Profile could not be loaded.');
      const value = await response.json() as UserProfile;
      if (active) { setProfile(value); setUserName(value.userName); }
    }).catch(() => { if (active) setError('Profile could not be loaded. Retry by closing and reopening this panel.'); });
    return () => { active = false; };
  }, []);

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

  return <div className="profile-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="profile-dialog" role="dialog" aria-modal="true" aria-labelledby="profile-title">
      <div className="profile-heading"><h2 id="profile-title">Profile and security</h2><button type="button" aria-label="Close profile settings" onClick={onClose}>×</button></div>
      {error && <p className="profile-error" role="alert">{error}</p>}{message && <p className="profile-message" role="status">{message}{message.startsWith('Password changed') && <> <a href="/login">Sign in</a></>}</p>}
      {!profile && !error ? <p aria-busy="true">Loading profile…</p> : <>
        <form className="profile-form" onSubmit={saveProfile}><h3>Profile</h3><label>Username<input aria-label="Username" required maxLength={64} value={userName} onChange={event => setUserName(event.target.value)} /></label><label>Email<input aria-label="Email" value={profile?.email ?? ''} disabled /></label><button type="submit" disabled={!profile || busy !== ''}>Save profile</button></form>
        <form className="profile-form" onSubmit={changePassword}><h3>Change password</h3><label>Current password<input aria-label="Current password" type="password" autoComplete="current-password" required value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label><label>New password<input aria-label="New password" type="password" autoComplete="new-password" required value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label><label>Confirm new password<input aria-label="Confirm new password" type="password" autoComplete="new-password" required value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></label><button type="submit" disabled={busy !== ''}>Change password</button></form>
      </>}
    </section>
  </div>;
}
