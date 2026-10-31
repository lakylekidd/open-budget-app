import { render, renderHook, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ProfileSettings } from '../ProfileSettings';
import { ThemePicker } from './ThemePicker';
import { applyTheme, cachedTheme, currentTheme, defaultTheme, isThemeKey, loadThemePreference, shiftColor, themeSwatches, themes } from './themes';
import { useSavedTheme } from './useSavedTheme';

const root = () => document.documentElement;
beforeEach(() => {
  window.localStorage.clear();
  root().removeAttribute('data-theme'); root().removeAttribute('style');
  document.head.innerHTML = '<meta name="theme-color" content="#10111f" />';
});
afterEach(() => vi.unstubAllGlobals());

describe('themes', () => {
  it('offers the five themes the API accepts, with the original look as the default', () => {
    expect(themes.map(theme => theme.key)).toEqual(['midnight', 'ocean', 'forest', 'rosewood', 'graphite']);
    expect(defaultTheme).toBe('midnight');
    expect(themes.find(theme => theme.key === 'midnight')).toMatchObject({ hue: 0, saturation: 1 });
    expect(isThemeKey('ocean')).toBe(true);
    expect(isThemeKey('Ocean')).toBe(false);
    expect(isThemeKey(undefined)).toBe(false);
  });

  it('leaves the original colours untouched for Midnight and shifts them for the others', () => {
    for (const hex of ['#0c0d22', '#15172d', '#5b54ff', '#a3acff']) expect(shiftColor(hex, 0, 1)).toBe(hex);
    expect(themeSwatches('midnight')).toEqual({ background: '#0c0d22', surface: '#15172d', accent: '#5b54ff' });
    const ocean = themeSwatches('ocean');
    expect(ocean.accent).not.toBe('#5b54ff');
    expect(shiftColor('#777777', 90, 0.5)).toBe('#777777');
    // Graphite keeps only a fifth of the saturation, so its colours are close to grey.
    const graphite = themeSwatches('graphite').surface;
    const [r, g, b] = [1, 3, 5].map(i => parseInt(graphite.slice(i, i + 2), 16));
    expect(Math.max(r, g, b) - Math.min(r, g, b)).toBeLessThan(12);
  });

  it('applies a theme to the document and remembers it in this browser', () => {
    applyTheme('forest');
    expect(root().dataset.theme).toBe('forest');
    expect(root().style.getPropertyValue('--theme-hue')).toBe('-90');
    expect(root().style.getPropertyValue('--theme-sat')).toBe('0.8');
    expect(document.querySelector('meta[name="theme-color"]')?.getAttribute('content')).toBe(shiftColor('#10111f', -90, 0.8));
    expect(cachedTheme()).toBe('forest');
    expect(currentTheme()).toBe('forest');
  });

  it('ignores an unknown cached value and survives unavailable browser storage', () => {
    window.localStorage.setItem('openbudget.theme', 'neon');
    expect(cachedTheme()).toBeNull();
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    expect(cachedTheme()).toBeNull();
    expect(() => applyTheme('ocean')).not.toThrow();
    expect(root().dataset.theme).toBe('ocean');
  });
});

describe('loading the saved theme', () => {
  it('applies the account theme once the app is shown', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ theme: 'rosewood' })));
    renderHook(() => useSavedTheme());
    await waitFor(() => expect(root().dataset.theme).toBe('rosewood'));
  });

  it.each([
    ['a server error', async () => new Response(null, { status: 500 })],
    ['an unknown theme key', async () => Response.json({ theme: 'neon' })],
    ['a network failure', async () => { throw new Error('offline'); }],
  ])('keeps the current theme after %s', async (_name, respond) => {
    applyTheme('ocean');
    vi.stubGlobal('fetch', vi.fn(respond));
    expect(await loadThemePreference()).toBeNull();
    renderHook(() => useSavedTheme());
    await new Promise(resolve => setTimeout(resolve, 20));
    expect(root().dataset.theme).toBe('ocean');
  });
});

describe('ThemePicker', () => {
  it('is a radio group with the selected theme checked', async () => {
    const onChange = vi.fn();
    render(<ThemePicker value="ocean" onChange={onChange} />);
    expect(screen.getByRole('radiogroup', { name: 'Theme' })).toBeInTheDocument();
    expect(screen.getAllByRole('radio')).toHaveLength(5);
    expect(screen.getByRole('radio', { name: /Ocean/ })).toBeChecked();
    await userEvent.click(screen.getByRole('radio', { name: /Rosewood/ }));
    expect(onChange).toHaveBeenCalledWith('rosewood');
  });
});

describe('Account Settings appearance', () => {
  function stubApi(putStatus: number) {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/auth/profile') return Response.json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: false });
      if (url === '/api/me/preferences' && init?.method === 'PUT') return putStatus === 200 ? Response.json(JSON.parse(String(init.body))) : new Response(null, { status: putStatus });
      return new Response(null, { status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);
    return fetchMock;
  }

  it('switches the theme immediately and saves it to the account', async () => {
    const fetchMock = stubApi(200);
    applyTheme('midnight');
    render(<ProfileSettings onClose={vi.fn()} />);
    expect(screen.getByRole('radio', { name: /Midnight/ })).toBeChecked();
    await userEvent.click(screen.getByRole('radio', { name: /Forest/ }));
    expect(root().dataset.theme).toBe('forest');
    expect(screen.getByRole('radio', { name: /Forest/ })).toBeChecked();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/me/preferences', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ theme: 'forest' }) })));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(cachedTheme()).toBe('forest');
  });

  it('restores the previous theme and explains when the save fails', async () => {
    stubApi(500);
    applyTheme('ocean');
    render(<ProfileSettings onClose={vi.fn()} />);
    await userEvent.click(screen.getByRole('radio', { name: /Rosewood/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent('previous theme was restored');
    expect(root().dataset.theme).toBe('ocean');
    expect(screen.getByRole('radio', { name: /Ocean/ })).toBeChecked();
    expect(cachedTheme()).toBe('ocean');
  });
});
