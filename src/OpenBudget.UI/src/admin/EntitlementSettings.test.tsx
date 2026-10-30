import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { EntitlementSettings, UserEntitlementOverrides } from './EntitlementSettings';

afterEach(() => vi.unstubAllGlobals());
const plans = [{ id: 'free', name: 'Free' }, { id: 'plus', name: 'Plus' }];

it('lists the wishlist key per tier and saves a tier default', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === '/api/admin/membership-plans/free/entitlements' && !init) return Response.json([{ entitlementKey: 'wishlist', isEnabled: false }]);
    if (url === '/api/admin/membership-plans/plus/entitlements' && !init) return Response.json([{ entitlementKey: 'wishlist', isEnabled: true }]);
    if (init?.method === 'PUT') return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<EntitlementSettings plans={plans} keys={['wishlist']} />);
  const free = await screen.findByRole('checkbox', { name: 'Wishlist (wishlist) for Free' });
  await waitFor(() => expect(free).toBeEnabled());
  expect(free).not.toBeChecked();
  expect(screen.getByRole('checkbox', { name: 'Wishlist (wishlist) for Plus' })).toBeChecked();
  await userEvent.click(free);
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/membership-plans/free/entitlements', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ entitlements: [{ entitlementKey: 'wishlist', isEnabled: true }] }) }));
  expect(await screen.findByRole('status')).toHaveTextContent('Wishlist (wishlist) enabled for Free.');
});

it('sets and clears a per-user override', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === '/api/admin/users/u1/entitlements') return Response.json([]);
    if (url === '/api/admin/users/u1/entitlements/wishlist' && (init?.method === 'PUT' || init?.method === 'DELETE')) return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<UserEntitlementOverrides userId="u1" userName="alice" keys={['wishlist']} />);
  await userEvent.click(screen.getByText('Feature overrides'));
  const select = await screen.findByRole('combobox', { name: 'Wishlist (wishlist) override for alice' });
  expect(select).toHaveValue('default');
  await userEvent.selectOptions(select, 'allow');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/entitlements/wishlist', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ isEnabled: true }) }));
  await waitFor(() => expect(select).toHaveValue('allow'));
  await userEvent.selectOptions(select, 'default');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/entitlements/wishlist', { method: 'DELETE' });
});
