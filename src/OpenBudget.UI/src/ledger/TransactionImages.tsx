import { useEffect, useRef, useState, type DragEvent, type FormEvent, type KeyboardEvent } from 'react';
import { createPortal } from 'react-dom';
import { ApiError, type Entry } from './api';
import { LedgerDialog } from './LedgerDialog';
import { RegisterIcon } from './AccountRegister';
import { acceptedImageTypes, maxFilesPerUpload, maxImageBytes, transactionImagesApi, type TransactionImage, type TransactionImagesApi } from './transactionImagesApi';
import './transactionImages.css';

const focusableSelector = 'button:not(:disabled), input:not(:disabled), [tabindex="0"]';

export function transactionImageLabel(entry: Pick<Entry, 'payee' | 'date' | 'transferId' | 'transferAccountName' | 'isOpening'>) {
  return entry.transferId ? entry.payee || `Transfer: ${entry.transferAccountName ?? 'Cash account'}` : entry.isOpening ? 'Starting Balance' : entry.payee || 'Transaction';
}

/** Register cell button: image icon plus count, opening the attachments dialog. */
export function TransactionImagesButton({ entry, api = transactionImagesApi }: { entry: Entry; api?: TransactionImagesApi }) {
  const [open, setOpen] = useState(false);
  const [count, setCount] = useState(entry.imageCount ?? 0);
  const button = useRef<HTMLButtonElement>(null);
  useEffect(() => setCount(entry.imageCount ?? 0), [entry.imageCount]);
  const name = transactionImageLabel(entry);
  return <>
    <button ref={button} type="button" className={`transaction-images-button ${count ? 'has-images' : ''}`} aria-label={`Images for ${name} ${entry.date} (${count})`}
      title={count ? `${count} image${count === 1 ? '' : 's'}` : 'Add images'} onClick={() => setOpen(true)}>
      <RegisterIcon name="image" size={15}/>{count > 0 && <span>{count}</span>}
    </button>
    {/* Portal out of the register table so row click handlers and cell styles do not apply inside the dialog. */}
    {open && createPortal(<div className="transaction-images-portal" onClick={event => event.stopPropagation()} onDoubleClick={event => event.stopPropagation()}>
      <TransactionImagesDialog transactionId={entry.id} title={`Images for ${name}`} subtitle={entry.date.split('-').reverse().join('-')} api={api}
        onCountChange={setCount} onClose={() => { setOpen(false); window.setTimeout(() => button.current?.focus(), 0); }}/>
    </div>, document.body)}
  </>;
}

type Pending = { signature: string; commandId: string };

export function TransactionImagesDialog({ transactionId, title, subtitle, api = transactionImagesApi, onClose, onCountChange }: {
  transactionId: string; title: string; subtitle?: string; api?: TransactionImagesApi; onClose: () => void; onCountChange?: (count: number) => void;
}) {
  const [images, setImages] = useState<TransactionImage[] | null>(null);
  const [maxImages, setMaxImages] = useState(20);
  const [loadError, setLoadError] = useState('');
  const [reloadToken, setReloadToken] = useState(0);
  const [index, setIndex] = useState(0);
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [url, setUrl] = useState('');
  const [dragging, setDragging] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);
  const [actualSize, setActualSize] = useState(false);
  const [brokenIds, setBrokenIds] = useState<string[]>([]);
  const pending = useRef<Pending | null>(null);
  const busyRef = useRef(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const fullscreenRef = useRef<HTMLDivElement>(null);
  const maximizeButton = useRef<HTMLButtonElement>(null);
  const cancelDeleteButton = useRef<HTMLButtonElement>(null);
  const countChanged = useRef(onCountChange); countChanged.current = onCountChange;

  useEffect(() => {
    const controller = new AbortController();
    setLoadError(''); setImages(null);
    api.list(transactionId, controller.signal)
      .then(result => { if (controller.signal.aborted) return; setImages(result.images); setMaxImages(result.maxImages); setIndex(0); countChanged.current?.(result.images.length); })
      .catch(err => { if (!controller.signal.aborted) setLoadError(err instanceof Error ? err.message : 'Images could not be loaded.'); });
    return () => controller.abort();
  }, [api, transactionId, reloadToken]);

  const count = images?.length ?? 0;
  const current = images?.[Math.min(index, count - 1)];
  const full = count >= maxImages;

  function commandFor(signature: string) {
    // Reuse the command after a lost response so a retry replays instead of duplicating.
    if (pending.current?.signature !== signature) pending.current = { signature, commandId: crypto.randomUUID() };
    return pending.current.commandId;
  }

  async function run(signature: string, action: (commandId: string) => Promise<TransactionImage[] | null>, success: string, select?: (before: number) => number) {
    if (busyRef.current) return false;
    busyRef.current = true; setBusy(true); setError(''); setNotice('');
    const before = count;
    try {
      const next = await action(commandFor(signature));
      pending.current = null;
      if (next) { setImages(next); countChanged.current?.(next.length); if (select) setIndex(Math.max(0, Math.min(select(before), next.length - 1))); }
      setNotice(success);
      return true;
    } catch (err) {
      if (err instanceof ApiError && err.status >= 400 && err.status < 500) pending.current = null;
      setError(err instanceof Error ? err.message : 'The change could not be saved. Retry safely.');
      return false;
    } finally { busyRef.current = false; setBusy(false); setProgress(null); }
  }

  async function addFiles(files: File[]) {
    if (!files.length) return;
    const invalid = files.find(file => !acceptedImageTypes.includes(file.type));
    if (invalid) { setError(`${invalid.name || 'That file'} is not a JPEG, PNG, WebP or GIF image.`); return; }
    const large = files.find(file => file.size > maxImageBytes);
    if (large) { setError(`${large.name || 'That image'} is larger than 10 MB.`); return; }
    if (files.length > maxFilesPerUpload) { setError(`Add at most ${maxFilesPerUpload} images at a time.`); return; }
    if (count + files.length > maxImages) { setError(`A transaction can have at most ${maxImages} images.`); return; }
    setProgress(0);
    const signature = 'upload:' + files.map(file => `${file.name}:${file.size}:${file.lastModified}`).join('|');
    await run(signature, async commandId => (await api.upload(transactionId, files, commandId, setProgress)).images,
      files.length === 1 ? 'Image added.' : `${files.length} images added.`, before => before);
  }

  async function importUrl(event: FormEvent) {
    event.preventDefault();
    const value = url.trim();
    if (!/^https:\/\//i.test(value)) { setError('Enter a public https:// link to an image.'); return; }
    if (await run('url:' + value, async commandId => (await api.importUrl(transactionId, value, commandId)).images, 'Image added from link.', before => before)) setUrl('');
  }

  async function remove() {
    if (!current) return;
    const id = current.id;
    const deleted = await run('delete:' + id, async commandId => { await api.remove(id, commandId); return (images ?? []).filter(image => image.id !== id); }, 'Image deleted.', () => index);
    if (deleted) { setConfirmDelete(false); if (count <= 1) setFullscreen(false); }
  }

  function go(step: number) { if (count > 1) { setIndex(value => (value + step + count) % count); setConfirmDelete(false); } }

  useEffect(() => {
    // Paste is dialog-wide: any image on the clipboard while the dialog is open is uploaded.
    const paste = (event: ClipboardEvent) => {
      const files = Array.from(event.clipboardData?.files ?? []).filter(file => file.type.startsWith('image/'));
      if (!files.length) return;
      event.preventDefault();
      void addFiles(files);
    };
    document.addEventListener('paste', paste);
    return () => document.removeEventListener('paste', paste);
  });

  useEffect(() => { if (fullscreen) fullscreenRef.current?.querySelector<HTMLElement>('.transaction-image-exit')?.focus(); }, [fullscreen]);
  useEffect(() => { if (confirmDelete) cancelDeleteButton.current?.focus(); }, [confirmDelete]);

  function keyDown(event: KeyboardEvent<HTMLDivElement>) {
    const typing = (event.target as HTMLElement).closest('input,textarea,select');
    if (event.key === 'Escape' && (fullscreen || confirmDelete)) {
      event.stopPropagation(); event.preventDefault();
      if (confirmDelete) setConfirmDelete(false);
      else { setFullscreen(false); window.setTimeout(() => maximizeButton.current?.focus(), 0); }
      return;
    }
    if (!typing && (event.key === 'ArrowLeft' || event.key === 'ArrowRight')) { event.preventDefault(); go(event.key === 'ArrowLeft' ? -1 : 1); return; }
    if (event.key === 'Tab' && fullscreen && fullscreenRef.current) {
      // The full-screen viewer is its own focus trap layered over the dialog trap.
      event.stopPropagation();
      const focusable = Array.from(fullscreenRef.current.querySelectorAll<HTMLElement>(focusableSelector));
      const first = focusable[0]; const last = focusable.at(-1);
      if (!focusable.includes(document.activeElement as HTMLElement)) { event.preventDefault(); first?.focus(); }
      else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  }

  function drop(event: DragEvent<HTMLElement>) {
    event.preventDefault(); setDragging(false);
    void addFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  const image = (item: TransactionImage, className: string, alt: string) => brokenIds.includes(item.id)
    ? <div className={`${className} transaction-image-broken`} role="img" aria-label={alt}>Image could not be displayed.</div>
    : <img className={className} src={item.url} alt={alt} onError={() => setBrokenIds(ids => [...ids, item.id])}/>;
  const position = current ? `${Math.min(index, count - 1) + 1} of ${count}` : '';

  return <LedgerDialog title={title} onClose={() => { if (!busyRef.current) onClose(); }}>
    <div className="transaction-images" onKeyDown={keyDown}>
      {subtitle && <p className="transaction-images-subtitle">{subtitle}</p>}
      {loadError ? <div className="transaction-images-state" role="alert"><p>Images could not be loaded. {loadError}</p><button type="button" onClick={() => setReloadToken(value => value + 1)}>Retry</button></div>
        : images === null ? <p className="transaction-images-state" role="status">Loading images…</p>
        : current && <>
          <div className="transaction-image-stage">
            <button type="button" className="transaction-image-nav" aria-label="Previous image" disabled={count < 2} onClick={() => go(-1)}>‹</button>
            <figure>{image(current, 'transaction-image-current', `${current.fileName}, image ${position}`)}<figcaption>{current.fileName}</figcaption></figure>
            <button type="button" className="transaction-image-nav" aria-label="Next image" disabled={count < 2} onClick={() => go(1)}>›</button>
          </div>
          <div className="transaction-image-toolbar">
            <span className="transaction-image-position" aria-live="polite">{position}</span>
            <button ref={maximizeButton} type="button" aria-label="View full screen" onClick={() => { setActualSize(false); setFullscreen(true); }}>⤢ Full screen</button>
            <button type="button" className="transaction-image-delete" disabled={busy} onClick={() => setConfirmDelete(true)}>Delete image</button>
          </div>
          {confirmDelete && <div className="transaction-image-confirm" role="alertdialog" aria-label="Confirm image deletion">
            <p>Delete {current.fileName}? This cannot be undone.</p>
            <div><button ref={cancelDeleteButton} type="button" disabled={busy} onClick={() => setConfirmDelete(false)}>Cancel</button><button type="button" className="button-danger" disabled={busy} onClick={() => void remove()}>Delete</button></div>
          </div>}
          <ul className="transaction-image-thumbnails" aria-label="Image thumbnails">
            {images.map((item, i) => <li key={item.id}><button type="button" aria-label={`Show image ${i + 1} of ${count}: ${item.fileName}`} aria-current={i === index ? 'true' : undefined} className={i === index ? 'current' : ''} onClick={() => { setIndex(i); setConfirmDelete(false); }}>{image(item, 'transaction-image-thumbnail', '')}</button></li>)}
          </ul>
        </>}
      {images !== null && !loadError && <>
        <div className={`transaction-image-dropzone ${dragging ? 'dragging' : ''} ${count ? 'compact' : ''}`} onDragEnter={event => { event.preventDefault(); setDragging(true); }}
          onDragOver={event => { event.preventDefault(); if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy'; }} onDragLeave={() => setDragging(false)} onDrop={drop} aria-label="Image drop zone" role="group">
          <RegisterIcon name="image" size={count ? 18 : 30}/>
          <p>{full ? `This transaction has the maximum of ${maxImages} images.` : 'Drop, paste or upload receipts'}</p>
          {!full && <small>JPEG, PNG, WebP or GIF · up to 10 MB each · Ctrl/Cmd+V to paste</small>}
          <button type="button" className="button-primary" disabled={busy || full} onClick={() => fileInput.current?.click()}>Upload images</button>
          <input ref={fileInput} type="file" multiple accept={acceptedImageTypes.join(',')} hidden aria-label="Choose images to upload" tabIndex={-1}
            onChange={event => { const files = Array.from(event.target.files ?? []); event.target.value = ''; void addFiles(files); }}/>
        </div>
        <form className="transaction-image-url" onSubmit={event => void importUrl(event)}>
          <input type="url" inputMode="url" placeholder="https://…/receipt.jpg" aria-label="Image URL" value={url} maxLength={2048} disabled={busy || full} onChange={event => setUrl(event.target.value)}/>
          <button type="submit" disabled={busy || full || !url.trim()}>Add from URL</button>
        </form>
      </>}
      {progress !== null && <div className="transaction-image-progress" role="status"><progress aria-label="Upload progress" max={100} value={progress}/><span>Uploading… {progress}%</span></div>}
      {busy && progress === null && <p className="transaction-images-status" role="status">Saving…</p>}
      {error && <p className="transaction-images-error" role="alert">{error}</p>}
      {notice && !error && <p className="transaction-images-status" role="status">{notice}</p>}
      {fullscreen && current && <div ref={fullscreenRef} className={`transaction-image-fullscreen ${actualSize ? 'actual-size' : ''}`} role="dialog" aria-modal="true" aria-label={`Full-screen image ${position}`}>
        <div className="transaction-image-fullscreen-toolbar">
          <span>{current.fileName} · {position}</span>
          <button type="button" aria-label="Previous image" disabled={count < 2} onClick={() => go(-1)}>‹</button>
          <button type="button" aria-label="Next image" disabled={count < 2} onClick={() => go(1)}>›</button>
          <button type="button" aria-pressed={actualSize} onClick={() => setActualSize(value => !value)}>{actualSize ? 'Fit to screen' : 'Actual size'}</button>
          <button type="button" className="transaction-image-exit" aria-label="Exit full screen" onClick={() => { setFullscreen(false); window.setTimeout(() => maximizeButton.current?.focus(), 0); }}>×</button>
        </div>
        <div className="transaction-image-fullscreen-canvas" tabIndex={0} aria-label="Image canvas, scroll to pan">{image(current, 'transaction-image-full', `${current.fileName}, image ${position}`)}</div>
      </div>}
    </div>
  </LedgerDialog>;
}
