import { useEffect, useRef, useState } from 'react';

export const maxBucketNameLength = 60;

/** What the table is editing inline: a new draft bucket row at the end, or an existing bucket being renamed. */
export type BucketEditing = { kind: 'new' } | { kind: 'rename'; bucketId: string };

type Props = {
  initialValue: string;
  busy: boolean;
  error: string;
  errorId: string;
  /** Resolves true when saved. A false result keeps the input open so the (inline) error can be fixed. */
  onCommit: (name: string) => Promise<boolean>;
  onCancel: () => void;
  onEdit: () => void;
};

// Inline name editor shared by the "Add bucket" draft row and rename. Enter or blur saves, Escape cancels, and an empty
// (or, when renaming, unchanged) name cancels. `settled` stops Escape/Enter/blur from handling the same edit twice.
export function BucketNameInput({ initialValue, busy, error, errorId, onCommit, onCancel, onEdit }: Props) {
  const [value, setValue] = useState(initialValue);
  const settled = useRef(false);
  const field = useRef<HTMLInputElement>(null);

  async function commit() {
    if (settled.current || busy) return;
    const name = value.trim();
    if (!name || name === initialValue) { settled.current = true; onCancel(); return; }
    settled.current = true;
    if (!(await onCommit(name))) { settled.current = false; field.current?.focus(); }
  }

  useEffect(() => { field.current?.select(); }, []);

  return <input
    className="wishlist-name-input"
    autoFocus
    ref={field}
    aria-label="Bucket name"
    aria-invalid={error ? true : undefined}
    aria-describedby={error ? errorId : undefined}
    maxLength={maxBucketNameLength}
    readOnly={busy}
    value={value}
    placeholder="Bucket name"
    onChange={event => { setValue(event.target.value); if (error) onEdit(); }}
    onKeyDown={event => {
      if (event.key === 'Enter') { event.preventDefault(); void commit(); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); settled.current = true; onCancel(); }
    }}
    onBlur={() => void commit()}
  />;
}
