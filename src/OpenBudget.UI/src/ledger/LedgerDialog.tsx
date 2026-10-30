import { useEffect, useId, useRef } from 'react';

export function LedgerDialog({ title, onClose, onBack, children }: { title: string; onClose: () => void; onBack?: () => void; children: React.ReactNode }) {
  const id = useId();
  const ref = useRef<HTMLElement>(null);
  const close = useRef(onClose);
  close.current = onClose;
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    ref.current?.focus();
    const keydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close.current();
      if (event.key !== 'Tab') return;
      const focusable = Array.from(ref.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex="0"]') ?? []);
      const first = focusable[0]; const last = focusable.at(-1);
      if (event.shiftKey && (document.activeElement === first || document.activeElement === ref.current)) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', keydown);
    return () => { document.removeEventListener('keydown', keydown); previous?.focus(); };
  }, []);
  return <div className="modal-backdrop"><section ref={ref} tabIndex={-1} className="ledger-dialog" role="dialog" aria-modal="true" aria-labelledby={id} onKeyDown={event=>{if(event.key==='Escape'){event.stopPropagation();close.current();}}}>
    <div className="ledger-dialog-heading">{onBack && <button type="button" className="ledger-dialog-back" aria-label="Back" onClick={onBack}>‹</button>}<h2 id={id}>{title}</h2><button type="button" aria-label="Close dialog" onClick={onClose}>×</button></div>{children}
  </section></div>;
}
