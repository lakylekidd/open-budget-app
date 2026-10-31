import type { Plugin } from 'vite';

// Themes recolour the app by shifting the hue and saturation of its blue-violet palette. Rather than hand-maintaining a token per
// colour, this build step rewrites each blue-violet hex literal in a declaration value into
//   hsl(from #15172d calc(h + var(--theme-hue)) calc(s * var(--theme-sat)) l)
// The source stylesheets keep their plain hex values; `--theme-hue: 0` and `--theme-sat: 1` (src/theme/themes.css) reproduce the
// original colour, and each theme only sets those two variables. Neutrals, white and the green/red/yellow/orange status colours are
// left alone, so money and warning colours keep their meaning in every theme.

const hueRange = { min: 190, max: 300 };
const minSaturation = 8;

function hslOf(hex: string): { h: number; s: number } {
  const digits = hex.length === 3 ? [...hex].map(c => c + c).join('') : hex;
  const [r, g, b] = [0, 2, 4].map(i => parseInt(digits.slice(i, i + 2), 16) / 255);
  const max = Math.max(r, g, b), min = Math.min(r, g, b), delta = max - min, l = (max + min) / 2;
  if (delta === 0) return { h: 0, s: 0 };
  const s = delta / (1 - Math.abs(2 * l - 1)) * 100;
  const sector = max === r ? ((g - b) / delta) % 6 : max === g ? (b - r) / delta + 2 : (r - g) / delta + 4;
  return { h: (sector * 60 + 360) % 360, s };
}

/** True for the blue-violet family that themes recolour (not white, greys or status colours). */
export function isThemedHex(hex: string): boolean {
  const { h, s } = hslOf(hex);
  return s >= minSaturation && h >= hueRange.min && h <= hueRange.max;
}

const themed = (hex: string) => `hsl(from #${hex} calc(h + var(--theme-hue)) calc(s * var(--theme-sat)) l)`;

// Only 3- and 6-digit hex values are rewritten (4- and 8-digit values carry alpha and are left as they are).
const hexLiteral = /#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})(?![0-9a-zA-Z_-])/g;
// A declaration value: after `{` or `;`, `property:` up to the next `;`, `{` or `}`. Selectors such as `#abc` never match.
const declaration = /([{;]\s*[-\w]+\s*:)([^;{}]*)/g;
const urlCall = /(url\([^)]*\))/g;

function rewriteValue(value: string): string {
  return value.split(urlCall).map((part, index) => index % 2 === 1 ? part : part.replace(hexLiteral, (match, hex: string) => isThemedHex(hex) ? themed(hex) : match)).join('');
}

/** Rewrites the blue-violet hex colours in a stylesheet into theme-aware relative colours. */
export function rewriteThemeColors(css: string): string {
  return css.replace(declaration, (_match, head: string, value: string) => head + rewriteValue(value));
}

export function themeColors(): Plugin {
  return {
    name: 'openbudget-theme-colors',
    enforce: 'pre',
    transform(code, id) {
      const path = id.split('?')[0];
      if (!path.endsWith('.css') || path.includes('node_modules')) return null;
      return { code: rewriteThemeColors(code), map: null };
    },
  };
}
