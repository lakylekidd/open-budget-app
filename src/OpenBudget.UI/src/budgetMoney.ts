// Matches the budget API's decimal(14,2) assignment range.
export function parseAssignment(value:string):number|null {
  const normalized=value.trim().replace(',','.');
  if(!normalized)return 0;
  if(!/^-?\d+(\.\d{1,2})?$/.test(normalized))return null;
  const amount=Number(normalized);
  return Number.isFinite(amount)&&Math.abs(amount)<=999999999999.99?amount:null;
}
