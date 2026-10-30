import { useState } from 'react';
import { MoneyInput } from '../MoneyInput';
import { type LoanAccountType, loanAccountTypes, parseMoney, today } from './api';
import './loans.css';
import { type ContractTerms, type LoanPairing, type LoanTerms, parseRate, unpaired } from './loans';
import { type PairDraft, PairCategoryStep, pairDraft, renameDraft } from './PairCategoryStep';

export type ContractDraft = { months: string; customMonths: string; startDate: string; includesDevice: boolean; upfront: string; service: string;
  device: string; discount: boolean; discountMonths: string; discounted: string };

export const contractDraft = (terms?: ContractTerms | null): ContractDraft => terms ? {
  months: [12, 24].includes(terms.months) ? String(terms.months) : 'custom', customMonths: String(terms.months), startDate: terms.startDate,
  includesDevice: terms.includesDevice, upfront: terms.upfrontCost.toFixed(2), service: terms.monthlyServiceCost.toFixed(2), device: terms.deviceTotal.toFixed(2),
  discount: terms.discountMonths > 0, discountMonths: String(terms.discountMonths || 6), discounted: terms.discountedServiceCost?.toFixed(2) ?? ''
} : { months: '24', customMonths: '24', startDate: today(), includesDevice: true, upfront: '', service: '', device: '', discount: false, discountMonths: '6', discounted: '' };

// Returns contract terms or a validation message. Blank money fields count as zero.
export function parseContract(draft: ContractDraft): ContractTerms | string {
  const months = Number(draft.months === 'custom' ? draft.customMonths : draft.months);
  if (!Number.isInteger(months) || months < 1 || months > 60) return 'Contract length must be 1-60 months.';
  if (!/^\d{4}-\d{2}-\d{2}$/.test(draft.startDate)) return 'Choose the contract start date.';
  const money = (value: string) => value.trim() === '' ? 0 : parseMoney(value);
  const upfront = money(draft.upfront), service = money(draft.service), device = draft.includesDevice ? money(draft.device) : 0;
  if ([upfront, service, device].some(value => value === null || value < 0)) return 'Contract amounts must be non-negative with at most two decimal places.';
  let discountMonths = 0, discounted: number | null = null;
  if (draft.discount) {
    discountMonths = Number(draft.discountMonths);
    discounted = money(draft.discounted);
    if (!Number.isInteger(discountMonths) || discountMonths < 1 || discountMonths > months) return 'Discount months must be between 1 and the contract length.';
    if (discounted === null || discounted < 0 || discounted > (service ?? 0)) return 'The discounted amount must be between 0 and the regular monthly contract cost.';
  }
  if ((upfront ?? 0) + (service ?? 0) + (device ?? 0) + (discounted ?? 0) <= 0) return 'Enter the contract costs.';
  return { months, startDate: draft.startDate, includesDevice: draft.includesDevice, upfrontCost: upfront!, monthlyServiceCost: service!, deviceTotal: device!, discountMonths, discountedServiceCost: discounted };
}

export function ContractFields({ draft, onChange, disabled }: { draft: ContractDraft; onChange: (draft: ContractDraft) => void; disabled?: boolean }) {
  const set = (patch: Partial<ContractDraft>) => onChange({ ...draft, ...patch });
  return <fieldset className="loan-contract-fields" disabled={disabled}>
    <legend>Contract Details</legend>
    <div className="loan-field-row">
      <label>Contract length<select value={draft.months} onChange={e => set({ months: e.target.value })}><option value="12">12 months</option><option value="24">24 months</option><option value="custom">Custom…</option></select></label>
      {draft.months === 'custom' && <label>Months<input type="number" min={1} max={60} value={draft.customMonths} onChange={e => set({ customMonths: e.target.value })} /></label>}
      <label>Start date<input type="date" required min="1900-01-01" max="9990-12-31" value={draft.startDate} onChange={e => set({ startDate: e.target.value })} /></label>
    </div>
    <label className="loan-check"><input type="checkbox" checked={draft.includesDevice} onChange={e => set({ includesDevice: e.target.checked })} />Includes a device</label>
    <div className="loan-field-row">
      <label>Upfront cost<MoneyInput value={draft.upfront} placeholder="0.00" onChange={upfront => set({ upfront })} /></label>
      <label>Monthly contract cost<MoneyInput value={draft.service} placeholder="0.00" onChange={service => set({ service })} /></label>
    </div>
    {draft.includesDevice && <label>Device total (spread over the contract)<MoneyInput value={draft.device} placeholder="0.00" onChange={device => set({ device })} /></label>}
    <label className="loan-check"><input type="checkbox" checked={draft.discount} onChange={e => set({ discount: e.target.checked })} />Discounted for the first months</label>
    {draft.discount && <div className="loan-field-row">
      <label>Discount months<input type="number" min={1} max={60} value={draft.discountMonths} onChange={e => set({ discountMonths: e.target.value })} /></label>
      <label>Discounted monthly cost<MoneyInput value={draft.discounted} placeholder="0.00" onChange={discounted => set({ discounted })} /></label>
    </div>}
    <p className="loan-help">The balance is the remaining device cost plus the remaining contract commitment. Contracts are interest-free.</p>
  </fieldset>;
}

// "Add Unlinked Account" for mortgages, loans and contracts. Next stays disabled until the required fields are valid and
// opens the optional pairing step (the parent switches the dialog title/back button through `pairing`). The account,
// any new category/group and the pairing are sent as one create command; Skip creates the account unpaired.
export function LoanAccountForm({ accountType, busy, pairing = false, onPairingChange, onSubmit }: {
  accountType: LoanAccountType; busy: boolean; pairing?: boolean; onPairingChange?: (open: boolean) => void; onSubmit: (fields: object) => Promise<void>;
}) {
  const [type, setType] = useState<LoanAccountType>(accountType);
  const [name, setName] = useState('');
  const [balance, setBalance] = useState('');
  const [rate, setRate] = useState('');
  const [payment, setPayment] = useState('');
  const [contract, setContract] = useState(contractDraft());
  const [pair, setPair] = useState<PairDraft | null>(null);
  const [error, setError] = useState('');
  const isContract = type === 'contract';
  const parsedBalance = parseMoney(balance), parsedRate = parseRate(rate), parsedPayment = parseMoney(payment);
  const parsedContract = isContract ? parseContract(contract) : null;
  const valid = name.trim().length > 0 && (isContract ? typeof parsedContract === 'object'
    : parsedBalance !== null && parsedBalance > 0 && parsedRate !== null && parsedPayment !== null && parsedPayment > 0);
  const includesDevice = isContract && contract.includesDevice;
  function next(event: React.FormEvent) {
    event.preventDefault(); setError('');
    if (!valid) { setError(typeof parsedContract === 'string' ? parsedContract : 'Enter a nickname, the balance you owe, the interest rate and the monthly payment.'); return; }
    setPair(current => current ? renameDraft(current, name) : pairDraft(name));
    onPairingChange?.(true);
  }
  async function create(choice: LoanPairing) {
    if (!valid) return;
    const pairing = includesDevice ? choice : { ...choice, separateDeviceCategory: false, devicePairedCategoryId: null, newDevicePairedCategory: null };
    const loan: LoanTerms = isContract
      ? { interestRate: 0, minimumPayment: 0, escrowAmount: 0, contract: parsedContract as ContractTerms, ...pairing }
      : { interestRate: parsedRate!, minimumPayment: parsedPayment!, escrowAmount: 0, contract: null, ...pairing };
    await onSubmit({ name: name.trim(), accountType: type, date: today(), openingBalance: isContract ? 0 : -parsedBalance!, loan });
  }
  if (pairing && pair) return <PairCategoryStep draft={pair} onChange={setPair} subject={isContract ? 'contract' : 'loan'} includesDevice={includesDevice} busy={busy}
    secondaryLabel="Skip" onSecondary={() => void create(unpaired)} onNext={result => void create(result.pairing)} />;
  return <form className="loan-account-form" onSubmit={next}>
    <p className="loan-intro">Let’s go! And don’t worry—if you change your mind, you can link your account at any time.</p>
    <label>Give it a nickname<input autoFocus maxLength={120} value={name} disabled={busy} onChange={e => setName(e.target.value)} /></label>
    <label>What type of account are you adding?<select value={type} disabled={busy} onChange={e => setType(e.target.value as LoanAccountType)}>{loanAccountTypes.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
    {isContract ? <ContractFields draft={contract} onChange={setContract} disabled={busy} /> : <>
      <div className="loan-field-row loan-balance-row">
        <label>Current account balance<MoneyInput value={balance} disabled={busy} placeholder="0.00" onChange={setBalance} /></label>
        <label className="loan-rate">Interest rate<span className="loan-percent-input"><input inputMode="decimal" aria-label="Interest rate" value={rate} disabled={busy} onChange={e => setRate(e.target.value)} /><span aria-hidden="true">%</span></span></label>
      </div>
      <label>Monthly payment required by your lender<MoneyInput value={payment} disabled={busy} placeholder="0.00" onChange={setPayment} /></label>
      <p className="loan-help">Enter the total amount you are required to pay your lender each month, including principal, interest, escrow, fees, etc.</p>
      <p className="loan-help">You can add extra payments as a target in your plan later.</p>
    </>}
    {error && <p role="alert" className="loan-error">{error}</p>}
    <button className="loan-next" type="submit" disabled={busy || !valid}>{busy ? 'Saving…' : 'Next'}</button>
  </form>;
}
