const focusable = 'input:not([disabled]):not([tabindex="-1"]), select:not([disabled]):not([tabindex="-1"]), textarea:not([disabled]):not([tabindex="-1"]), button:not([disabled]):not([tabindex="-1"])';

// After a choice is made, move focus to the next field in tab order. Runs after React re-renders so
// fields disabled by the choice (e.g. category after picking a transfer payee) are skipped.
export function focusNextField(from: HTMLElement | null) {
  if (!from) return;
  setTimeout(() => {
    const scope = from.closest('tr, form') ?? document.body;
    const fields = Array.from(scope.querySelectorAll<HTMLElement>(focusable)).filter(field => !field.closest('[hidden]'));
    const index = fields.indexOf(from);
    (index >= 0 ? fields[index + 1] : undefined)?.focus();
  });
}
