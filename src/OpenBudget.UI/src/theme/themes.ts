// Colour themes. Each theme recolours the app by shifting the hue and saturation of the blue-violet palette (see themeColors.ts at
// the UI root, which rewrites the stylesheets to read --theme-hue/--theme-sat). The keys must match UserThemes on the API.

export type ThemeKey = 'midnight' | 'ocean' | 'forest' | 'rosewood' | 'graphite';
export type ThemeOption = { key: ThemeKey; label: string; description: string; hue: number; saturation: number };

export const defaultTheme: ThemeKey = 'midnight';
export const themes: readonly ThemeOption[] = [
  { key: 'midnight', label: 'Midnight', description: 'The original deep blue-violet.', hue: 0, saturation: 1 },
  { key: 'ocean', label: 'Ocean', description: 'Cool teal and sea blue.', hue: -45, saturation: 1 },
  { key: 'forest', label: 'Forest', description: 'Calm, muted greens.', hue: -90, saturation: 0.8 },
  { key: 'rosewood', label: 'Rosewood', description: 'Warm plum and rose.', hue: 95, saturation: 0.95 },
  { key: 'graphite', label: 'Graphite', description: 'Neutral greys with a soft accent.', hue: 0, saturation: 0.2 },
];

export const isThemeKey = (value: unknown): value is ThemeKey => themes.some(theme => theme.key === value);
const themeOf = (key: ThemeKey) => themes.find(theme => theme.key === key) ?? themes[0];

// The base palette the swatches are drawn from (the app background, a surface and the primary accent of the original theme).
const baseColors = { background: '#0c0d22', surface: '#15172d', accent: '#5b54ff' } as const;

/** Applies a theme's hue/saturation shift to a hex colour exactly as the stylesheet's relative colours do. */
export function shiftColor(hex: string, hue: number, saturation: number): string {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16) / 255);
  const max = Math.max(r, g, b), min = Math.min(r, g, b), delta = max - min, l = (max + min) / 2;
  let h = 0, s = 0;
  if (delta !== 0) {
    s = delta / (1 - Math.abs(2 * l - 1));
    const sector = max === r ? ((g - b) / delta) % 6 : max === g ? (b - r) / delta + 2 : (r - g) / delta + 4;
    h = (sector * 60 + 360) % 360;
  }
  const nextHue = (((h + hue) % 360) + 360) % 360;
  const nextSaturation = Math.min(1, s * saturation);
  const chroma = (1 - Math.abs(2 * l - 1)) * nextSaturation;
  const channel = (n: number) => {
    const k = (n + nextHue / 30) % 12;
    return Math.round((l - (chroma / 2) * Math.max(-1, Math.min(k - 3, 9 - k, 1))) * 255).toString(16).padStart(2, '0');
  };
  return `#${channel(0)}${channel(8)}${channel(4)}`;
}

export function themeSwatches(key: ThemeKey) {
  const { hue, saturation } = themeOf(key);
  return { background: shiftColor(baseColors.background, hue, saturation), surface: shiftColor(baseColors.surface, hue, saturation), accent: shiftColor(baseColors.accent, hue, saturation) };
}

// The last applied theme is cached in this browser only so the right colours show before the account's saved theme loads
// (and on the sign-in screen). The account's saved theme on the server is the source of truth. Storage can be unavailable.
const cacheKey = 'openbudget.theme';
export function cachedTheme(): ThemeKey | null {
  try { const value = window.localStorage.getItem(cacheKey); return isThemeKey(value) ? value : null; } catch { return null; }
}

export function currentTheme(): ThemeKey {
  const value = document.documentElement.dataset.theme;
  return isThemeKey(value) ? value : defaultTheme;
}

/** Switches the whole app to a theme immediately. */
export function applyTheme(key: ThemeKey) {
  const { hue, saturation } = themeOf(key);
  const root = document.documentElement;
  root.dataset.theme = key;
  root.style.setProperty('--theme-hue', String(hue));
  root.style.setProperty('--theme-sat', String(saturation));
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', shiftColor('#10111f', hue, saturation));
  try { window.localStorage.setItem(cacheKey, key); } catch { /* the theme just is not cached */ }
}

/** Reads the signed-in account's saved theme, or null when it cannot be loaded (the current theme is then kept). */
export async function loadThemePreference(signal?: AbortSignal): Promise<ThemeKey | null> {
  try {
    const response = await fetch('/api/me/preferences', { credentials: 'same-origin', signal });
    if (!response.ok) return null;
    const body = await response.json() as { theme?: unknown };
    return isThemeKey(body.theme) ? body.theme : null;
  } catch { return null; }
}

/** Saves the account's theme; throws when the server rejects or cannot be reached so the caller can revert. */
export async function saveThemePreference(key: ThemeKey): Promise<void> {
  const response = await fetch('/api/me/preferences', { method: 'PUT', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ theme: key }) });
  if (!response.ok) throw new Error('Theme could not be saved.');
}
