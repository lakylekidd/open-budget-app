import { useState, type DragEvent, type ReactNode } from 'react';
import type { Account } from './api';

// One sidebar section's accounts, reorderable by drag-and-drop or by the keyboard-accessible move up/down buttons.
// Drops are only accepted from rows of this same list, so accounts never move between sections.
export function SortableAccountList({ accounts, disabled, onMove, renderRow }: {
  accounts: Account[]; disabled: boolean; onMove: (accountId: string, toIndex: number) => void;
  renderRow: (account: Account, orderControls: ReactNode) => ReactNode;
}) {
  const [dragId, setDragId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<{ id: string; position: 'before' | 'after' } | null>(null);
  const position = (event: DragEvent<HTMLElement>) => {
    const bounds = event.currentTarget.getBoundingClientRect();
    return event.clientY > bounds.top + bounds.height / 2 ? 'after' as const : 'before' as const;
  };
  const reset = () => { setDragId(null); setDropTarget(null); };
  const drop = (event: DragEvent<HTMLElement>, targetId: string) => {
    event.preventDefault();
    const source = dragId;
    const where = dropTarget?.id === targetId ? dropTarget.position : position(event);
    reset();
    if (!source || source === targetId || disabled) return;
    const from = accounts.findIndex(account => account.id === source);
    let to = accounts.findIndex(account => account.id === targetId) + (where === 'after' ? 1 : 0);
    if (from < 0 || to < 0) return;
    if (from < to) to -= 1;
    onMove(source, to);
  };
  return <>{accounts.map((account, index) => {
    const controls = <span className="account-order-tools">
      <button type="button" aria-label={`Move ${account.name} up`} title="Move up" disabled={disabled || index === 0} onClick={() => onMove(account.id, index - 1)}>↑</button>
      <button type="button" aria-label={`Move ${account.name} down`} title="Move down" disabled={disabled || index === accounts.length - 1} onClick={() => onMove(account.id, index + 1)}>↓</button>
    </span>;
    const target = dropTarget?.id === account.id && dragId !== account.id ? ` drop-target-${dropTarget.position}` : '';
    return <div key={account.id} className={`sortable-account${dragId === account.id ? ' is-dragging' : ''}${target}`} data-testid={`sortable-account-${account.id}`}
      draggable={!disabled && accounts.length > 1}
      onDragStart={event => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', account.id); setDragId(account.id); }}
      onDragOver={event => { if (!dragId || disabled) return; event.preventDefault(); event.dataTransfer.dropEffect = 'move'; const where = position(event); if (dropTarget?.id !== account.id || dropTarget.position !== where) setDropTarget({ id: account.id, position: where }); }}
      onDrop={event => drop(event, account.id)} onDragEnd={reset}>
      {renderRow(account, controls)}
    </div>;
  })}</>;
}
