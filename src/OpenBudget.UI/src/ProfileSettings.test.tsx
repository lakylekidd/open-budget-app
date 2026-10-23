import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { ProfileSettings } from './ProfileSettings';

afterEach(() => vi.unstubAllGlobals());

it('updates the username and validates password confirmation before changing credentials', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === '/api/auth/profile' && !init?.method) return Response.json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: false });
    if (url === '/api/auth/profile' && init?.method === 'PUT') return Response.json({ id: 'u1', userName: 'alice-renamed', email: 'alice@example.test', isAdministrator: false });
    if (url === '/api/auth/change-password') return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<ProfileSettings onClose={vi.fn()} />);
  const user = userEvent.setup();
  await user.clear(await screen.findByLabelText('Username'));
  await user.type(screen.getByLabelText('Username'), 'alice-renamed');
  await user.click(screen.getByRole('button', { name: 'Save profile' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Profile updated');
  expect(fetchMock).toHaveBeenCalledWith('/api/auth/profile', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ userName: 'alice-renamed' }) }));

  await user.type(screen.getByLabelText('Current password'), 'Old-pass1!');
  await user.type(screen.getByLabelText('New password'), 'New-pass1!');
  await user.type(screen.getByLabelText('Confirm new password'), 'Other-pass1!');
  await user.click(screen.getByRole('button', { name: 'Change password' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('do not match');
  expect(fetchMock).not.toHaveBeenCalledWith('/api/auth/change-password', expect.anything());

  await user.clear(screen.getByLabelText('Confirm new password'));
  await user.type(screen.getByLabelText('Confirm new password'), 'New-pass1!');
  await user.click(screen.getByRole('button', { name: 'Change password' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Your session has ended');
  expect(fetchMock).toHaveBeenCalledWith('/api/auth/change-password', expect.objectContaining({ method: 'POST', body: JSON.stringify({ currentPassword: 'Old-pass1!', newPassword: 'New-pass1!' }) }));
  expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login');
});
