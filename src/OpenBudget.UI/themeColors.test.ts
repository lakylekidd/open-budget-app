import { describe, expect, it } from 'vitest';
import { isThemedHex, rewriteThemeColors } from './themeColors';

const themed = (hex: string) => `hsl(from #${hex} calc(h + var(--theme-hue)) calc(s * var(--theme-sat)) l)`;

describe('rewriteThemeColors', () => {
  it('rewrites blue-violet colours in declarations, including custom properties and shorthands', () => {
    const css = ':root{--surface:#15172d}.a{background:#0c0d22;border:1px solid #2c2e43;box-shadow:0 1px #5b54ff,inset 0 0 #aaa6ff}';
    expect(rewriteThemeColors(css)).toBe(`:root{--surface:${themed('15172d')}}.a{background:${themed('0c0d22')};border:1px solid ${themed('2c2e43')};box-shadow:0 1px ${themed('5b54ff')},inset 0 0 ${themed('aaa6ff')}}`);
  });

  it('keeps white, greys, status colours and alpha hex values unchanged', () => {
    const css = '.a{color:#fff;background:#3c3c3c;border-color:#a1cc6e;outline-color:#bd263e;fill:#ffdc00;box-shadow:0 0 #0006,0 0 #00000080}';
    expect(rewriteThemeColors(css)).toBe(css);
  });

  it('expands 3-digit values and leaves selectors and url() references alone', () => {
    const css = '#abc{color:#55f}.b{background:url(data:image/svg+xml,%3Csvg fill=%22%235b54ff%22/%3E) #0c0d22;mask:url(#a1b2c3)}';
    expect(rewriteThemeColors(css)).toBe(`#abc{color:${themed('55f')}}.b{background:url(data:image/svg+xml,%3Csvg fill=%22%235b54ff%22/%3E) ${themed('0c0d22')};mask:url(#a1b2c3)}`);
  });

  it('handles declarations after a semicolon, in media queries and in the last declaration without a semicolon', () => {
    const css = '@media(max-width:600px){.a{margin:0;color:#a3acff}.b{background:linear-gradient(#0c0d22,#15172d)}}';
    expect(rewriteThemeColors(css)).toBe(`@media(max-width:600px){.a{margin:0;color:${themed('a3acff')}}.b{background:linear-gradient(${themed('0c0d22')},${themed('15172d')})}}`);
  });

  it('is a no-op for a stylesheet without themed colours', () => {
    expect(rewriteThemeColors('.a{display:flex;margin:0 auto}')).toBe('.a{display:flex;margin:0 auto}');
  });
});

describe('isThemedHex', () => {
  it('selects the blue-violet palette only', () => {
    ['0c0d22', '15172d', '5b54ff', 'a3acff', 'c4c5cf', 'f1f1ff'].forEach(hex => expect(isThemedHex(hex), hex).toBe(true));
    ['ffffff', '000000', '777777', 'a1cc6e', '75bd22', 'bd263e', 'ffdc00', 'f0a331', 'ffb3c1'].forEach(hex => expect(isThemedHex(hex), hex).toBe(false));
  });
});
