// Evaluates simple arithmetic typed into money fields, e.g. "12.50+7.25", "100/3" or "(20-5)*2".
// A small recursive-descent parser: no eval, only numbers, + - * /, parentheses and unary minus.
// Each number may have at most two decimals; the result is rounded to cents. Returns null when invalid.
export function evaluateMoneyExpression(text: string): number | null {
  const source = normalize(text);
  if (!source) return null;
  let position = 0;
  const peek = () => source[position];
  const fail = (): never => { throw new Error('invalid'); };

  function number(): number {
    const match = /^\d+(\.\d{1,2})?/.exec(source.slice(position));
    if (!match) fail();
    position += match![0].length;
    return Number(match![0]);
  }
  function factor(): number {
    // One sign per operand: "--2" is treated as a typo, not as 2.
    if (peek() === '-' || peek() === '+') { const sign = source[position++]; if (peek() === '-' || peek() === '+') fail(); const value = factor(); return sign === '-' ? -value : value; }
    if (peek() === '(') { position++; const value = sum(); if (peek() !== ')') fail(); position++; return value; }
    return number();
  }
  function product(): number {
    let value = factor();
    while (peek() === '*' || peek() === '/') {
      const operator = source[position++];
      const right = factor();
      if (operator === '/' && right === 0) fail();
      value = operator === '*' ? value * right : value / right;
    }
    return value;
  }
  function sum(): number {
    let value = product();
    while (peek() === '+' || peek() === '-') {
      const operator = source[position++];
      const right = product();
      value = operator === '+' ? value + right : value - right;
    }
    return value;
  }

  try {
    const value = sum();
    if (position !== source.length || !Number.isFinite(value)) return null;
    const rounded = Math.round((value + Number.EPSILON * Math.sign(value)) * 100) / 100;
    return Object.is(rounded, -0) ? 0 : rounded;
  } catch {
    return null;
  }
}

export const hasMoneyOperator = (text: string) => /[-+*/()]/.test(normalize(text).replace(/^-/, ''));

// Spaces and the "×"/"÷" symbols are accepted. A comma is a thousands separator in "1,234.56",
// otherwise a decimal comma ("12,50").
function normalize(text: string) {
  const compact = text.replace(/\s+/g, '').replace(/[×xX]/g, '*').replace(/÷/g, '/').replace(/€/g, '');
  return compact.replace(/(?<![\d.,])\d{1,3}(,\d{3})+(?![\d,])/g, match => match.replaceAll(',', '')).replace(/,/g, '.');
}
