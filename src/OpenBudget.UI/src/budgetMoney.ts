import { evaluateMoneyExpression } from './moneyExpression';

// Matches the budget API's decimal(14,2) assignment range. Accepts arithmetic such as "50+25".
export function parseAssignment(value:string):number|null {
  if(!value.trim())return 0;
  const amount=evaluateMoneyExpression(value);
  return amount!==null&&Math.abs(amount)<=999999999999.99?amount:null;
}
