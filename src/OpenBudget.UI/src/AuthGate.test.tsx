import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthGate } from './AuthGate';

afterEach(() => vi.unstubAllGlobals());

it('shows sign in when no session exists and authenticates with username or email', async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(Response.json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: false }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Username or email'), 'alice@example.test');
  await user.type(screen.getByLabelText('Password'), 'Secret-pass1!');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Budget application')).toBeInTheDocument();
  expect(screen.getByText('alice')).toBeInTheDocument();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/auth/login', expect.objectContaining({ method: 'POST', credentials: 'same-origin' }));
});

it('shows a recoverable error when login fails', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(new Response(null, { status: 401 })));
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Username or email'), 'alice');
  await user.type(screen.getByLabelText('Password'), 'wrong');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Sign-in failed');
});
