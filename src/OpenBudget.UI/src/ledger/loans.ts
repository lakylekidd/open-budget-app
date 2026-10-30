import type { AccountType } from './api';

export type ContractTerms = { months: number; startDate: string; includesDevice: boolean; upfrontCost: number; monthlyServiceCost: number;
  deviceTotal: number; discountMonths: number; discountedServiceCost: number | null };
export type LoanTerms = { interestRate: number; minimumPayment: number; escrowAmount: number; pairedCategoryId: string | null;
  createPairedCategory: boolean; contract: ContractTerms | null };
export type BalancePoint = { month: string; balance: number };
export type LoanProjection = { monthlyPayment: number; paymentsRemaining: number | null; payoffMonth: string | null; interestRemaining: number;
  principalRemaining: number; remainingToPay: number; neverPaidOff: boolean; track: BalancePoint[] };
export type LoanActivityItem = { id: string; date: string; amount: number; kind: 'payment' | 'draw' | 'opening' | 'transaction'; description: string; accountName: string | null };
export type LoanActivityMonth = { month: string; endBalance: number; estimatedInterest: number; payments: LoanActivityItem[]; other: LoanActivityItem[] };
export type PairedCategory = { id: string; name: string; groupName: string; available: number; isArchived: boolean; isAutomatic: boolean;
  targetKind: string | null; targetMoreNeeded: number | null };
export type ContractScheduleItem = { number: number; dueDate: string; service: number; device: number; total: number; isDiscounted: boolean; isPast: boolean };
export type ContractDetails = ContractTerms & { endDate: string; totalCost: number; remainingDevice: number; remainingService: number;
  remainingCommitment: number; remainingMonths: number; schedule: ContractScheduleItem[] };
export type LoanDetails = { accountId: string; accountType: AccountType; version: number; currentMonth: string; balance: number; interestRate: number;
  minimumPayment: number; escrowAmount: number; originalBalance: number; percentRemaining: number | null; balanceConfirmedAt: string | null;
  estimatedInterestThisMonth: number; pairedCategory: PairedCategory | null; projection: LoanProjection; history: BalancePoint[];
  activity: LoanActivityMonth[]; contract: ContractDetails | null };
export type LoanSimulation = { accountId: string; currentMonth: string; balance: number; minimumPayment: number; extra: number; projection: LoanProjection };

const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
export const monthLabel = (month: string) => `${monthNames[Number(month.slice(5, 7)) - 1]} ${month.slice(0, 4)}`;
export const shortMonthLabel = (month: string) => `${monthNames[Number(month.slice(5, 7)) - 1].slice(0, 3)} ${month.slice(0, 4)}`;
export const monthShortName = (index: number) => monthNames[index].slice(0, 3);
export const addMonths = (month: string, count: number) => {
  const index = Number(month.slice(0, 4)) * 12 + Number(month.slice(5, 7)) - 1 + count;
  return `${Math.floor(index / 12)}-${String(index % 12 + 1).padStart(2, '0')}`;
};
export const dayLabel = (date: string) => new Date(`${date}T12:00:00`).toLocaleDateString('en-GB', { month: 'long', day: 'numeric' });

// "28 yrs, 8 mos" from a month count; null means never paid off.
export function durationLabel(months: number | null) {
  if (months === null) return 'Never';
  const years = Math.floor(months / 12), rest = months % 12;
  const parts = [years ? `${years} ${years === 1 ? 'yr' : 'yrs'}` : '', rest || !years ? `${rest} ${rest === 1 ? 'mo' : 'mos'}` : ''].filter(Boolean);
  return parts.join(', ');
}

export const percentLabel = (rate: number) => `${Number(rate.toFixed(3))}%`;

// Annual percentage 0-100 with at most three decimals; the server repeats every check.
export function parseRate(value: string): number | null {
  const trimmed = value.trim().replace(/%$/, '').trim();
  if (!/^\d{1,3}(\.\d{1,3})?$/.test(trimmed)) return null;
  const rate = Number(trimmed);
  return rate >= 0 && rate <= 100 ? rate : null;
}
