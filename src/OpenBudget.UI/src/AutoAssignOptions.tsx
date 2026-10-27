import type { AutoAssignOption } from './planAutoAssign';

export function AutoAssignOptions({ options, formatAmount, disabled = false, canApply, onApply }: {
  options: AutoAssignOption[];
  formatAmount: (amount: number) => string;
  disabled?: boolean;
  canApply?: (option: AutoAssignOption) => boolean;
  onApply: (option: AutoAssignOption) => void;
}) {
  return <div className="auto-assign-options">
    {options.map(option => <div className={`auto-assign-option-wrap ${option.id === 'reset-available' ? 'has-separator' : ''}`} key={option.id}>
      <button type="button" className="auto-assign-option" disabled={disabled || option.adjustments.length === 0 || canApply?.(option) === false}
        aria-label={`${option.label} ${formatAmount(option.amountCents / 100)}`} onClick={() => onApply(option)}>
        <span>{option.label}</span><strong>{formatAmount(option.amountCents / 100)}</strong>
      </button>
    </div>)}
  </div>;
}
