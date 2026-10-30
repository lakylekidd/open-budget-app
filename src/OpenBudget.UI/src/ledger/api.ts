export type Account = { unapprovedCount?:number; attentionCount?:number; notes?:string; id: string; name: string; isClosed: boolean; balance: number; clearedBalance: number; unclearedBalance: number;
  accountType: 'checking' | 'savings' | 'cash'; isFavorite: boolean; connectionStatus: 'unlinked' | 'connected'; lastReconciledAt: string | null; needsCategoryCount: number };
export type Accounts = { version: number; accounts: Account[] };
export type Split = { categoryId: string; amount: number; payee?: string; memo?: string };
export type Entry = { id: string; accountId: string; date: string; amount: number; payee: string; memo: string; categoryId: string | null;
  status: 'uncleared' | 'cleared' | 'reconciled'; transferId: string | null; isOpening: boolean; splits: Split[]; needsAttention: boolean;
  isApproved?:boolean; needsCategory?:boolean; canEdit?:boolean; flagColor?:string|null; scheduleId?:string|null; runningBalance: number | null; transferAccountName: string | null; categoryName: string | null; isLocked: boolean };
export type Register = { version: number; page: number; pageSize: number; total: number; items: Entry[]; accounts: Accounts };
export type CategoryOption = { id: string; name: string; isArchived?: boolean; groupId?: string; groupName?: string; available?: number };
export type Audit = { id: string; kind: string; version: number; createdAt: string; beforeJson: string; afterJson: string };
export const formatMoney = (amount: number) => new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' }).format(amount);
export const today = () => { const date = new Date(); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; };

export class ApiError extends Error {
  constructor(message: string, public status: number) { super(message); }
}

export async function requestJson<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, options);
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(problem.error ?? `Request failed (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}

export function parseMoney(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^-?\d+(\.\d{1,2})?$/.test(normalized)) return null;
  const amount = Number(normalized);
  return Number.isFinite(amount) && Math.abs(amount) <= 999999999.99 ? amount : null;
}

export async function loadCategories(date: string, signal?: AbortSignal): Promise<CategoryOption[]> {
  return (await loadCategoryBudget(date, signal)).categories;
}
export async function loadCategoryBudget(date: string, signal?: AbortSignal): Promise<{categories:CategoryOption[];readyToAssign?:number}> {
  const month = await requestJson<{ readyToAssign?:number; groups: { id?:string;name?:string;isArchived?: boolean; categories: CategoryOption[] }[] }>(`/api/budget/${date.slice(0, 7)}`, { signal });
  return {readyToAssign:month.readyToAssign,categories:month.groups.flatMap(group => group.categories.map(category => ({ ...category, groupId:group.id,groupName:group.name,isArchived: group.isArchived || category.isArchived })))};
}
export type ScheduledItem = { id:string;occurrenceId:string;repeat:string;startDate:string;isPaused:boolean;endDate:string|null;count:number|null;toAccountId:string|null;transaction:Entry;postingError?:string|null };
export type Schedules = {version:number;page:number;pageSize:number;total:number;items:ScheduledItem[]};
