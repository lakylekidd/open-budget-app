import { useId, useState } from 'react';
import { type Account } from './api';

/** Delete action for the edit-account forms. The server refuses accounts with transactions; this mirrors that up front when known. */
export function DeleteAccountSection({ account, busy, onDelete }: { account: Account; busy: boolean; onDelete: () => Promise<void> }) {
  const [confirming, setConfirming] = useState(false);
  const hintId = useId();
  if (account.hasTransactions) return <div className="delete-account-section">
    <button type="button" className="delete-account-button" disabled aria-describedby={hintId}>Delete Account</button>
    <p id={hintId}>Accounts with transactions can’t be deleted. Delete its transactions first, or close the account instead.</p>
  </div>;
  if (!confirming) return <div className="delete-account-section">
    <button type="button" className="delete-account-button" disabled={busy} onClick={() => setConfirming(true)}>Delete Account</button>
  </div>;
  return <div className="delete-account-section delete-account-confirm" role="group" aria-labelledby={hintId}>
    <p id={hintId}>Permanently delete “{account.name}”? This can’t be undone. The audit history is retained.</p>
    <div className="edit-account-actions">
      <button type="button" autoFocus disabled={busy} onClick={() => setConfirming(false)}>Keep Account</button>
      <button type="button" className="delete-account-button" disabled={busy} onClick={() => void onDelete()}>Delete Permanently</button>
    </div>
  </div>;
}
