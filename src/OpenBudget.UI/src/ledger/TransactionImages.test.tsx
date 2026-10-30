import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { ApiError, type Entry } from './api';
import { TransactionImagesButton } from './TransactionImages';
import { TransactionRow } from './AccountRegister';
import { defaultView } from './ViewOptions';
import type { TransactionImage, TransactionImagesApi } from './transactionImagesApi';

const entry: Entry = { id: 'tx-1', accountId: 'checking', date: '2026-10-21', amount: -12.5, payee: 'Coffee Shop', memo: '', categoryId: null, status: 'uncleared', transferId: null,
  isOpening: false, splits: [], needsAttention: false, runningBalance: 0, transferAccountName: null, categoryName: null, isLocked: false, imageCount: 0 };
const image = (n: number): TransactionImage => ({ id: `img-${n}`, transactionId: 'tx-1', fileName: `receipt-${n}.jpg`, contentType: 'image/jpeg', sizeBytes: 100, width: 10, height: 10, source: 'upload', createdAt: '2026-10-21T12:00:00Z', url: `/api/transaction-images/img-${n}` });
const png = (name = 'scan.png') => new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], name, { type: 'image/png' });

let stored: TransactionImage[];
let api: TransactionImagesApi & { [K in keyof TransactionImagesApi]: ReturnType<typeof vi.fn> };

beforeEach(() => {
  stored = [];
  api = {
    list: vi.fn(async () => ({ transactionId: 'tx-1', maxImages: 20, images: [...stored] })),
    upload: vi.fn(async (_id: string, files: File[], _command: string, onProgress?: (p: number) => void) => { onProgress?.(50); stored = [...stored, ...files.map((_, i) => image(stored.length + i + 1))]; return { transactionId: 'tx-1', maxImages: 20, images: [...stored] }; }),
    importUrl: vi.fn(async () => { stored = [...stored, image(stored.length + 1)]; return { transactionId: 'tx-1', maxImages: 20, images: [...stored] }; }),
    remove: vi.fn(async (id: string) => { stored = stored.filter(item => item.id !== id); }),
  } as never;
});

async function openDialog(count = 0) {
  stored = Array.from({ length: count }, (_, i) => image(i + 1));
  render(<TransactionImagesButton entry={{ ...entry, imageCount: count }} api={api}/>);
  await userEvent.click(screen.getByRole('button', { name: `Images for Coffee Shop 2026-10-21 (${count})` }));
  const dialog = await screen.findByRole('dialog', { name: 'Images for Coffee Shop' });
  await waitFor(() => expect(screen.queryByText('Loading images…')).not.toBeInTheDocument());
  return dialog;
}

it('renders the register image cell as a labelled button with the count and opens the dialog', async () => {
  const onSelect = vi.fn(); const onEdit = vi.fn();
  render(<table><tbody><TransactionRow entry={{ ...entry, imageCount: 3 }} view={defaultView} categories={[]} selected={false} disabled={false} onSelect={onSelect} onEdit={onEdit} onToggle={vi.fn()}/></tbody></table>);
  const button = screen.getByRole('button', { name: 'Images for Coffee Shop 2026-10-21 (3)' });
  expect(button).toHaveTextContent('3');
  const fetchMock = vi.fn(async () => new Response(JSON.stringify({ transactionId: 'tx-1', maxImages: 20, images: [image(1), image(2), image(3)] }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetchMock);
  try {
    await userEvent.click(button);
    const dialog = await screen.findByRole('dialog', { name: 'Images for Coffee Shop' });
    expect(await within(dialog).findByText('1 of 3')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/transactions/tx-1/images', expect.anything());
    // The dialog is portalled out of the row: clicks inside it never select or edit the transaction.
    expect(dialog.closest('table')).toBeNull();
    await userEvent.click(within(dialog).getByText('Drop, paste or upload receipts'));
    expect(onSelect).not.toHaveBeenCalled(); expect(onEdit).not.toHaveBeenCalled();
  } finally { vi.unstubAllGlobals(); }
});

it('keeps scheduled rows as a plain image icon', () => {
  render(<table><tbody><TransactionRow entry={entry} scheduled view={defaultView} categories={[]} selected={false} disabled={false} onSelect={vi.fn()} onEdit={vi.fn()} onToggle={vi.fn()}/></tbody></table>);
  expect(screen.queryByRole('button', { name: /Images for/ })).not.toBeInTheDocument();
});

it('shows the empty state, then uploads files chosen with the picker with progress', async () => {
  const dialog = await openDialog();
  expect(within(dialog).getByText('Drop, paste or upload receipts')).toBeInTheDocument();
  await userEvent.upload(within(dialog).getByLabelText('Choose images to upload'), [png('a.png'), png('b.png')]);
  await waitFor(() => expect(within(dialog).getByText('1 of 2')).toBeInTheDocument());
  expect(api.upload).toHaveBeenCalledWith('tx-1', [expect.objectContaining({ name: 'a.png' }), expect.objectContaining({ name: 'b.png' })], expect.any(String), expect.any(Function));
  expect(within(dialog).getByRole('status')).toHaveTextContent('2 images added.');
  // The register button reflects the new count without a reload.
  expect(screen.getByRole('button', { name: 'Images for Coffee Shop 2026-10-21 (2)' })).toBeInTheDocument();
});

it('uploads dropped files and rejects unsupported files before calling the API', async () => {
  const dialog = await openDialog();
  const zone = within(dialog).getByRole('group', { name: 'Image drop zone' });
  fireEvent.drop(zone, { dataTransfer: { files: [new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' })], types: ['Files'] } });
  expect(await within(dialog).findByRole('alert')).toHaveTextContent('logo.svg is not a JPEG, PNG, WebP or GIF image.');
  expect(api.upload).not.toHaveBeenCalled();
  fireEvent.drop(zone, { dataTransfer: { files: [png('dropped.png')], types: ['Files'] } });
  await waitFor(() => expect(api.upload).toHaveBeenCalledWith('tx-1', [expect.objectContaining({ name: 'dropped.png' })], expect.any(String), expect.any(Function)));
  expect(await within(dialog).findByText('1 of 1')).toBeInTheDocument();
});

it('uploads an image pasted from the clipboard while the dialog is open', async () => {
  const dialog = await openDialog();
  fireEvent.paste(document, { clipboardData: { files: [png('clipboard.png')], getData: () => '' } });
  await waitFor(() => expect(api.upload).toHaveBeenCalledWith('tx-1', [expect.objectContaining({ name: 'clipboard.png' })], expect.any(String), expect.any(Function)));
  expect(await within(dialog).findByText('1 of 1')).toBeInTheDocument();
});

it('imports from an https URL and validates the address client-side', async () => {
  const dialog = await openDialog();
  const input = within(dialog).getByLabelText('Image URL');
  await userEvent.type(input, 'http://example.test/a.png');
  await userEvent.click(within(dialog).getByRole('button', { name: 'Add from URL' }));
  expect(within(dialog).getByRole('alert')).toHaveTextContent('Enter a public https:// link to an image.');
  expect(api.importUrl).not.toHaveBeenCalled();
  await userEvent.clear(input);
  await userEvent.type(input, 'https://example.test/a.png');
  await userEvent.click(within(dialog).getByRole('button', { name: 'Add from URL' }));
  await waitFor(() => expect(api.importUrl).toHaveBeenCalledWith('tx-1', 'https://example.test/a.png', expect.any(String)));
  expect(await within(dialog).findByText('1 of 1')).toBeInTheDocument();
  expect(input).toHaveValue('');
});

it('shows the generic server error for a rejected URL and keeps the command for a safe retry', async () => {
  api.importUrl.mockRejectedValueOnce(new Error('Connection lost.'));
  const dialog = await openDialog();
  await userEvent.type(within(dialog).getByLabelText('Image URL'), 'https://example.test/a.png');
  await userEvent.click(within(dialog).getByRole('button', { name: 'Add from URL' }));
  expect(await within(dialog).findByRole('alert')).toHaveTextContent('Connection lost.');
  await userEvent.click(within(dialog).getByRole('button', { name: 'Add from URL' }));
  await waitFor(() => expect(api.importUrl).toHaveBeenCalledTimes(2));
  expect(api.importUrl.mock.calls[0][2]).toBe(api.importUrl.mock.calls[1][2]);
});

it('navigates with buttons and arrow keys and highlights the current thumbnail', async () => {
  const dialog = await openDialog(3);
  const thumbnails = () => within(within(dialog).getByRole('list', { name: 'Image thumbnails' })).getAllByRole('button');
  expect(thumbnails()).toHaveLength(3);
  expect(within(dialog).getByText('1 of 3')).toBeInTheDocument();
  expect(thumbnails()[0]).toHaveAttribute('aria-current', 'true');
  await userEvent.click(within(dialog).getAllByRole('button', { name: 'Next image' })[0]);
  expect(within(dialog).getByText('2 of 3')).toBeInTheDocument();
  expect(thumbnails()[1]).toHaveAttribute('aria-current', 'true');
  expect(thumbnails()[0]).not.toHaveAttribute('aria-current');
  await userEvent.keyboard('{ArrowRight}');
  expect(within(dialog).getByText('3 of 3')).toBeInTheDocument();
  await userEvent.keyboard('{ArrowRight}');
  expect(within(dialog).getByText('1 of 3')).toBeInTheDocument();
  await userEvent.keyboard('{ArrowLeft}');
  expect(within(dialog).getByText('3 of 3')).toBeInTheDocument();
  await userEvent.click(within(dialog).getAllByRole('button', { name: 'Previous image' })[0]);
  expect(within(dialog).getByText('2 of 3')).toBeInTheDocument();
  await userEvent.click(thumbnails()[0]);
  expect(within(dialog).getByRole('img', { name: 'receipt-1.jpg, image 1 of 3' })).toHaveAttribute('src', '/api/transaction-images/img-1');
});

it('opens a full-screen viewer with fit/actual size toggle and exits on Escape without closing the dialog', async () => {
  const dialog = await openDialog(2);
  await userEvent.click(within(dialog).getByRole('button', { name: 'View full screen' }));
  const viewer = screen.getByRole('dialog', { name: 'Full-screen image 1 of 2' });
  expect(within(viewer).getByRole('button', { name: 'Exit full screen' })).toHaveFocus();
  const toggle = within(viewer).getByRole('button', { name: 'Actual size' });
  await userEvent.click(toggle);
  expect(within(viewer).getByRole('button', { name: 'Fit to screen' })).toHaveAttribute('aria-pressed', 'true');
  expect(viewer).toHaveClass('actual-size');
  await userEvent.keyboard('{ArrowRight}');
  expect(screen.getByRole('dialog', { name: 'Full-screen image 2 of 2' })).toBeInTheDocument();
  await userEvent.keyboard('{Escape}');
  expect(screen.queryByRole('dialog', { name: /Full-screen image/ })).not.toBeInTheDocument();
  expect(screen.getByRole('dialog', { name: 'Images for Coffee Shop' })).toBeInTheDocument();
  await waitFor(() => expect(within(dialog).getByRole('button', { name: 'View full screen' })).toHaveFocus());
  await userEvent.keyboard('{Escape}');
  expect(screen.queryByRole('dialog', { name: 'Images for Coffee Shop' })).not.toBeInTheDocument();
});

it('traps focus inside the full-screen viewer', async () => {
  const dialog = await openDialog(2);
  await userEvent.click(within(dialog).getByRole('button', { name: 'View full screen' }));
  const viewer = screen.getByRole('dialog', { name: /Full-screen image/ });
  for (let i = 0; i < 8; i++) { await userEvent.tab(); expect(viewer.contains(document.activeElement)).toBe(true); }
  await userEvent.tab({ shift: true });
  expect(viewer.contains(document.activeElement)).toBe(true);
});

it('deletes the current image only after confirmation', async () => {
  const dialog = await openDialog(2);
  await userEvent.click(within(dialog).getByRole('button', { name: 'Delete image' }));
  const confirm = within(dialog).getByRole('alertdialog', { name: 'Confirm image deletion' });
  expect(confirm).toHaveTextContent('Delete receipt-1.jpg? This cannot be undone.');
  expect(within(confirm).getByRole('button', { name: 'Cancel' })).toHaveFocus();
  await userEvent.keyboard('{Escape}');
  expect(within(dialog).queryByRole('alertdialog')).not.toBeInTheDocument();
  expect(api.remove).not.toHaveBeenCalled();
  await userEvent.click(within(dialog).getByRole('button', { name: 'Delete image' }));
  await userEvent.click(within(within(dialog).getByRole('alertdialog')).getByRole('button', { name: 'Delete' }));
  await waitFor(() => expect(api.remove).toHaveBeenCalledWith('img-1', expect.any(String)));
  expect(await within(dialog).findByText('1 of 1')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Images for Coffee Shop 2026-10-21 (1)' })).toBeInTheDocument();
});

it('shows load errors with a retry and upload errors from the server', async () => {
  api.list.mockRejectedValueOnce(new ApiError('Transaction not found.', 404));
  render(<TransactionImagesButton entry={entry} api={api}/>);
  await userEvent.click(screen.getByRole('button', { name: 'Images for Coffee Shop 2026-10-21 (0)' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Images could not be loaded. Transaction not found.');
  await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
  expect(await screen.findByText('Drop, paste or upload receipts')).toBeInTheDocument();
  api.upload.mockRejectedValueOnce(new ApiError('A transaction can have at most 20 images.', 409));
  await userEvent.upload(screen.getByLabelText('Choose images to upload'), png());
  expect(await screen.findByRole('alert')).toHaveTextContent('at most 20 images');
});

it('shows a loading state until the list arrives', async () => {
  let resolve: (value: unknown) => void = () => {};
  api.list.mockReturnValueOnce(new Promise(r => { resolve = r; }));
  render(<TransactionImagesButton entry={entry} api={api}/>);
  await userEvent.click(screen.getByRole('button', { name: 'Images for Coffee Shop 2026-10-21 (0)' }));
  expect(screen.getByText('Loading images…')).toBeInTheDocument();
  await act(async () => resolve({ transactionId: 'tx-1', maxImages: 20, images: [] }));
  expect(screen.getByText('Drop, paste or upload receipts')).toBeInTheDocument();
});
