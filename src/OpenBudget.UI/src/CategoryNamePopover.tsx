type CategoryNamePopoverProps = {
  name: string;
  label: string;
  maxLength: number;
  error: string;
  canDelete: boolean;
  onNameChange: (name: string) => void;
  onSubmit: () => void;
  hideLabel: string;
  onHide: () => void;
  onDelete: () => void;
  onCancel: () => void;
};

export default function CategoryNamePopover({ name, label, maxLength, error, canDelete, hideLabel, onNameChange, onSubmit, onHide, onDelete, onCancel }: CategoryNamePopoverProps) {
  return <form className="category-name-popover" role="dialog" aria-label={`Rename ${label}`} onClick={event => event.stopPropagation()} onSubmit={event => { event.preventDefault(); onSubmit(); }}>
    <label className="sr-only" htmlFor="rename-category-name">Name</label>
    <input id="rename-category-name" autoFocus maxLength={maxLength} value={name} onChange={event => onNameChange(event.target.value)} onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); onCancel(); } }} />
    {error && <span role="alert">{error}</span>}
    <footer>
      <div className="category-name-popover-actions">
        <button type="button" onClick={onHide}>{hideLabel}</button>
        <button type="button" className="danger" disabled={!canDelete} title={canDelete ? undefined : 'Saved categories are retained for budget history.'} onClick={onDelete}>Delete</button>
      </div>
      <div className="category-name-popover-actions">
        <button type="button" onClick={onCancel}>Cancel</button>
        <button type="submit" className="button-primary">OK</button>
      </div>
    </footer>
  </form>;
}
