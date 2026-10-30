import { evaluateMoneyExpression } from '../moneyExpression';
export type Account = { unapprovedCount?:number; attentionCount?:number; notes?:string; id: string; name: string; isClosed: boolean; balance: number; clearedBalance: number; unclearedBalance: number;
  accountType: AccountType; isFavorite: boolean; connectionStatus: 'unlinked' | 'connected'; lastReconciledAt: string | null; needsCategoryCount: number };
export type LoanAccountType = 'mortgage' | 'autoLoan' | 'studentLoan' | 'personalLoan' | 'medicalDebt' | 'otherDebt' | 'contract';
export type AccountType = 'checking' | 'savings' | 'cash' | 'creditCard' | 'lineOfCredit' | LoanAccountType;
export const accountTypeSections: { title: string; description: string; types: { value: AccountType; label: string }[] }[] = [
  { title: 'Cash Accounts', description: 'A cash account holds funds you already own and can spend immediately.', types: [{ value: 'checking', label: 'Checking' }, { value: 'savings', label: 'Savings' }, { value: 'cash', label: 'Cash' }] },
  { title: 'Credit Accounts', description: 'A credit account lets you spend borrowed money that you’ll need to repay later, often with interest.', types: [{ value: 'creditCard', label: 'Credit Card' }, { value: 'lineOfCredit', label: 'Line of Credit' }] },
  { title: 'Mortgages and Loans', description: 'Accounts that have an outstanding balance you’re currently paying off, and aren’t spending from.', types: [{ value: 'mortgage', label: 'Mortgage' }, { value: 'autoLoan', label: 'Auto Loan' }, { value: 'studentLoan', label: 'Student Loan' }, { value: 'personalLoan', label: 'Personal Loan' }, { value: 'medicalDebt', label: 'Medical Debt' }, { value: 'otherDebt', label: 'Other Debt' }, { value: 'contract', label: 'Contract' }] },
];
export const loanAccountTypes = accountTypeSections[2].types;
export const accountTypeLabel = (type: AccountType) => accountTypeSections.flatMap(section => section.types).find(item => item.value === type)?.label ?? type;
export const isCreditAccount = (account: Pick<Account, 'accountType'> | undefined) => account?.accountType === 'creditCard' || account?.accountType === 'lineOfCredit';
export const isLoanAccount = (account: Pick<Account, 'accountType'> | undefined) => loanAccountTypes.some(type => type.value === account?.accountType);
export const accountFamily = (account: Pick<Account, 'accountType'> | undefined) => isLoanAccount(account) ? 'loan' : isCreditAccount(account) ? 'credit' : 'cash';
export const loanCategoryId = (accountId: string) => `loan-${accountId}`;
export const creditPaymentGroupId = 'credit-card-payments';
export const creditPaymentCategoryId = (accountId: string) => `ccp-${accountId}`;
export type CreditSummary = { accountId: string; month: string; priorBalance: number; spendingAndOutflows: number; interestCharges: number; paymentsAndInflows: number; currentBalance: number };
export type Accounts = { version: number; accounts: Account[] };
export type Split = { categoryId: string; amount: number; payee?: string; memo?: string };
export type Entry = { id: string; accountId: string; date: string; amount: number; payee: string; memo: string; categoryId: string | null;
  status: 'uncleared' | 'cleared' | 'reconciled'; transferId: string | null; isOpening: boolean; splits: Split[]; needsAttention: boolean;
  isApproved?:boolean; needsCategory?:boolean; imageCount?:number; canEdit?:boolean; flagColor?:string|null; scheduleId?:string|null; runningBalance: number | null; transferAccountName: string | null; categoryName: string | null; isLocked: boolean };
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

// Accepts plain amounts and arithmetic such as "12.50+7.25" or "100/3" (rounded to cents).
export function parseMoney(value: string): number | null {
  const amount = evaluateMoneyExpression(value);
  return amount !== null && Math.abs(amount) <= 999999999.99 ? amount : null;
}

export async function loadCategories(date: string, signal?: AbortSignal): Promise<CategoryOption[]> {
  return (await loadCategoryBudget(date, signal)).categories;
}
export async function loadCategoryBudget(date: string, signal?: AbortSignal): Promise<{categories:CategoryOption[];readyToAssign?:number}> {
  const month = await requestJson<{ readyToAssign?:number; groups: { id?:string;name?:string;isArchived?: boolean; categories: CategoryOption[] }[] }>(`/api/budget/${date.slice(0, 7)}`, { signal });
  return {readyToAssign:month.readyToAssign,categories:month.groups.filter(group => group.id !== creditPaymentGroupId).flatMap(group => group.categories.map(category => ({ ...category, groupId:group.id,groupName:group.name,isArchived: group.isArchived || category.isArchived })))};
}
export type ScheduledItem = { id:string;occurrenceId:string;repeat:string;startDate:string;isPaused:boolean;endDate:string|null;count:number|null;toAccountId:string|null;transaction:Entry;postingError?:string|null };
export type Schedules = {version:number;page:number;pageSize:number;total:number;items:ScheduledItem[]};
