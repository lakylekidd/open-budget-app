import { ApiError, requestJson } from './api';

export type TransactionImage = { id: string; transactionId: string; fileName: string; contentType: string; sizeBytes: number; width: number; height: number; source: 'upload' | 'url'; createdAt: string; url: string };
export type TransactionImageList = { transactionId: string; maxImages: number; images: TransactionImage[] };
export type TransactionImagesApi = {
  list: (transactionId: string, signal?: AbortSignal) => Promise<TransactionImageList>;
  upload: (transactionId: string, files: File[], commandId: string, onProgress?: (percent: number) => void) => Promise<TransactionImageList>;
  importUrl: (transactionId: string, url: string, commandId: string) => Promise<TransactionImageList>;
  remove: (imageId: string, commandId: string) => Promise<void>;
};

export const acceptedImageTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
export const maxImageBytes = 10 * 1024 * 1024;
export const maxFilesPerUpload = 10;

const json = (body: unknown): RequestInit => ({ method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });

// XMLHttpRequest is used for uploads because fetch exposes no upload progress events.
function uploadWithProgress(transactionId: string, files: File[], commandId: string, onProgress?: (percent: number) => void) {
  return new Promise<TransactionImageList>((resolve, reject) => {
    const form = new FormData();
    form.append('commandId', commandId);
    files.forEach(file => form.append('files', file, file.name));
    const request = new XMLHttpRequest();
    request.open('POST', `/api/transactions/${encodeURIComponent(transactionId)}/images`);
    request.responseType = 'json';
    request.upload.onprogress = event => { if (event.lengthComputable) onProgress?.(Math.round(event.loaded / event.total * 100)); };
    request.onload = () => {
      if (request.status >= 200 && request.status < 300) resolve(request.response as TransactionImageList);
      else reject(new ApiError((request.response as { error?: string } | null)?.error ?? `Upload failed (${request.status}).`, request.status));
    };
    request.onerror = () => reject(new ApiError('Connection lost while uploading. Retry to finish safely.', 0));
    request.send(form);
  });
}

export const transactionImagesApi: TransactionImagesApi = {
  list: (transactionId, signal) => requestJson(`/api/transactions/${encodeURIComponent(transactionId)}/images`, { signal }),
  upload: uploadWithProgress,
  importUrl: (transactionId, url, commandId) => requestJson(`/api/transactions/${encodeURIComponent(transactionId)}/images/from-url`, json({ commandId, url })),
  remove: async (imageId, commandId) => { await requestJson(`/api/transaction-images/${encodeURIComponent(imageId)}/delete`, json({ commandId })); },
};
