/** Right-pointing chevron that rotates down when its section is open. */
export function DisclosureChevron({ open, className }: { open: boolean; className?: string }) {
  return <svg className={['disclosure-chevron', open ? 'open' : '', className].filter(Boolean).join(' ')} viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
    <path d="m5 3 5 5-5 5" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>;
}
