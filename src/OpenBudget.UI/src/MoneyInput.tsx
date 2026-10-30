import { forwardRef, type InputHTMLAttributes } from 'react';
import { evaluateMoneyExpression, hasMoneyOperator } from './moneyExpression';

// The calculator icon shown in the Plan Assigned editor.
export function CalculatorIcon({ size = 14 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M3 5h6M6 2v6M14 5h7M3 12l6 6M9 12l-6 6M14 15h7M3 22h6M14 22h7" />
  </svg>;
}

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> & {
  value: string;
  onChange: (value: string) => void;
  shellClassName?: string;
};

// Amount field that accepts arithmetic ("12.50+7.25", "100/3"). The expression is replaced by its
// cent-rounded result on blur or Enter; parsers also evaluate it, so submitting directly works too.
export const MoneyInput = forwardRef<HTMLInputElement, Props>(function MoneyInput({ value, onChange, onBlur, onKeyDown, shellClassName = '', className = '', ...props }, ref) {
  const resolve = () => {
    if (!hasMoneyOperator(value)) return;
    const result = evaluateMoneyExpression(value);
    if (result !== null) onChange(result.toFixed(2));
  };
  return <span className={`money-input-shell ${shellClassName}`}>
    <span className="money-input-icon"><CalculatorIcon /></span>
    <input {...props} ref={ref} className={`money-input ${className}`} inputMode="decimal" autoComplete="off" value={value}
      onChange={event => onChange(event.target.value)}
      onBlur={event => { resolve(); onBlur?.(event); }}
      onKeyDown={event => { if (event.key === 'Enter') resolve(); onKeyDown?.(event); }} />
  </span>;
});
