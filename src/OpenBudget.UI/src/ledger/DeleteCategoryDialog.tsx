import { useEffect, useRef, useState } from 'react';
import { ApiError, requestJson } from './api';
import { LedgerDialog } from './LedgerDialog';

export type CategoryDeletionPreview = {
  categoryId: string; name: string; groupName: string; requiresReassignment: boolean;
  transactionCount: number; splitLineCount: number; scheduleCount: number; assignedMonthCount: number; assignedTotal: number;
  remainingAvailable: number; remainingAvailableMonth: string | null; hasTarget: boolean; payeeDefaultCount: number; loanPairingCount: number;
  planningValueCount: number; ledgerVersion: number;
  reassignmentOptions: { id: string; name: string; groupId: string; groupName: string }[];
};

const plural = (count: number, one: string, many = `${one}s`) => `${count} ${count === 1 ? one : many}`;

// Deletes a category from every month. A category with history first reassigns it (transactions, assigned amounts,
// remaining available, schedules, payee defaults, loan pairings) to the chosen category in one server transaction.
export function DeleteCategoryDialog({ budgetId, categoryId, categoryName, onClose, onDeleted }: {
  budgetId: string; categoryId: string; categoryName: string; onClose: () => void; onDeleted: (reassignedTo: string | null) => void;
}) {
  const [preview, setPreview] = useState<CategoryDeletionPreview | null>(null);
  const [target, setTarget] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const [loading, setLoading] = useState(true);
  // Kept until the server answers, so retrying after a lost response replays the same command instead of repeating it.
  const command = useRef<{ id: string; body: string } | null>(null);
  const base = `/api/budgets/${encodeURIComponent(budgetId)}/categories/${encodeURIComponent(categoryId)}`;

  useEffect(() => {
    const controller = new AbortController();
    // A reload after a conflict keeps the current details and the conflict message visible until fresh data arrives.
    setLoading(true);
    requestJson<CategoryDeletionPreview>(`${base}/deletion`, { signal: controller.signal })
      .then(value => { if (controller.signal.aborted) return; setLoading(false); setPreview(value); setTarget(current => value.reassignmentOptions.some(option => option.id === current) ? current : ''); })
      .catch(err => { if (!controller.signal.aborted) { setLoading(false); setPreview(null); } if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Could not load the category.'); });
    return () => controller.abort();
  }, [base, refresh]);

  function close() { if (!busy && !uncertain) onClose(); }

  async function remove() {
    if (!preview || busy) return;
    if (preview.requiresReassignment && !target) { setError('Select a category to reassign this category’s history to.'); return; }
    if (!command.current) command.current = { id: crypto.randomUUID(), body: JSON.stringify({ commandId: '', version: preview.ledgerVersion, reassignToCategoryId: preview.requiresReassignment ? target : null }) };
    const pending = command.current;
    setBusy(true); setError('');
    try {
      await requestJson(`${base}/delete`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...JSON.parse(pending.body), commandId: pending.id }) });
      command.current = null; setUncertain(false);
      onDeleted(preview.requiresReassignment ? target : null);
    } catch (err) {
      if (err instanceof ApiError && err.status < 500) {
        // The server refused the command (stale ledger, invalid target): nothing changed, so reload and choose again.
        command.current = null; setUncertain(false);
        setError(err.status === 409 ? 'This budget changed in another session. The details below were reloaded; review them and try again.' : err.message);
        if (err.status === 409) setRefresh(n => n + 1);
      } else {
        setUncertain(true);
        setError('The delete outcome is unknown. Retry to finish it safely; it will not be applied twice.');
      }
    } finally { setBusy(false); }
  }

  const groups = preview ? [...new Map(preview.reassignmentOptions.map(option => [option.groupId, option.groupName])).entries()] : [];
  const canDelete = Boolean(preview) && !busy && !loading && (!preview!.requiresReassignment || Boolean(target));
  return <LedgerDialog title="Delete Category" onClose={close}>
    <form className="delete-category-form" onSubmit={event => { event.preventDefault(); void remove(); }}>
      {!preview && !error && <p role="status">Loading category…</p>}
      {preview && (preview.requiresReassignment ? <>
        <p>Before you can delete the category <strong>{categoryName}</strong>, you’ll need to reassign your past activity to a new category.</p>
        <label>Select category
          <select aria-label="Select category" value={target} disabled={busy || uncertain} onChange={event => { setTarget(event.target.value); setError(''); }}>
            <option value="">Select category</option>
            {groups.map(([groupId, groupName]) => <optgroup key={groupId} label={groupName}>
              {preview.reassignmentOptions.filter(option => option.groupId === groupId).map(option => <option key={option.id} value={option.id}>{option.name}</option>)}
            </optgroup>)}
          </select>
        </label>
        {preview.reassignmentOptions.length === 0 && <p>There is no other active category to reassign to. Add or restore a category first.</p>}
        <section aria-label="Here's what will be reassigned">
          <h3>Here’s what will be reassigned</h3>
          <ul>
            <li>All transactions <span className="delete-category-count">{preview.transactionCount}</span></li>
            <li>All assigned amounts</li>
            <li>Any remaining available amount</li>
            {preview.scheduleCount > 0 && <li>{plural(preview.scheduleCount, 'scheduled transaction')}</li>}
            {preview.payeeDefaultCount > 0 && <li>{plural(preview.payeeDefaultCount, 'payee default')}</li>}
            {preview.loanPairingCount > 0 && <li>{plural(preview.loanPairingCount, 'loan pairing')}</li>}
            {preview.planningValueCount > 0 && <li>Long-term planning values</li>}
          </ul>
          {preview.hasTarget && <p>The category’s target will be removed.</p>}
        </section>
      </> : <p>Delete the category <strong>{categoryName}</strong>? It has no transactions or money in any month, so nothing needs to be reassigned.{preview.hasTarget ? ' Its target will be removed.' : ''}</p>)}
      {error && <p role="alert">{error}{!preview && <button type="button" onClick={() => { setError(''); setRefresh(n => n + 1); }}>Retry loading</button>}</p>}
      <div className="delete-category-actions">
        <button type="button" disabled={busy || uncertain} onClick={close}>Cancel</button>
        <button type="submit" className="button-danger" disabled={!canDelete}>{busy ? 'Deleting…' : uncertain ? 'Retry delete' : 'Delete'}</button>
      </div>
    </form>
  </LedgerDialog>;
}
