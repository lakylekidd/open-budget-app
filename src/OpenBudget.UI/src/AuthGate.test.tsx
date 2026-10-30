import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthGate } from './AuthGate';

afterEach(() => {
  window.history.replaceState(null, '', '/');
  vi.unstubAllGlobals();
});

it('shows sign in when no session exists and authenticates with username or email', async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(Response.json({ mode: 'Disabled' }))
    .mockResolvedValueOnce(Response.json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: true }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  expect(await screen.findByRole('img', { name: 'OpenBudget' })).toBeInTheDocument();
  expect(screen.getByText('Make every euro feel intentional.')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Forgot password?' })).toHaveClass('auth-recovery-link');
  await user.type(await screen.findByLabelText('Username or email'), 'alice@example.test');
  await user.type(screen.getByLabelText('Password'), 'Secret-pass1!');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Budget application')).toBeInTheDocument();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/auth/login', expect.objectContaining({ method: 'POST', credentials: 'same-origin' }));
});

it('shows a recoverable error when login fails', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(Response.json({ mode: 'Disabled' })).mockResolvedValueOnce(new Response(null, { status: 401 })));
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Username or email'), 'alice');
  await user.type(screen.getByLabelText('Password'), 'wrong');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Sign-in failed');
});

it('keeps authenticated users out of the shared legacy budget until ownership exists', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(Response.json({ id: 'u2', userName: 'bob', email: 'bob@example.test', isAdministrator: false })));
  render(<AuthGate><div>Shared financial data</div></AuthGate>);
  expect(await screen.findByRole('heading', { name: 'Budget access is being set up' })).toBeInTheDocument();
  expect(screen.queryByText('Shared financial data')).not.toBeInTheDocument();
});

it('offers registration only when enabled and submits the account details', async () => {
  window.history.replaceState(null, '', '/register');
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(Response.json({ mode: 'Open' }))
    .mockResolvedValueOnce(Response.json({ message: 'Verification email sent.' }, { status: 202 }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Username'), 'alice');
  await user.type(screen.getByLabelText('Email'), 'alice@example.test');
  await user.type(screen.getByLabelText('Password'), 'Strong-pass1!');
  await user.click(screen.getByRole('button', { name: 'Create account' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Verification email sent.');
  expect(fetchMock).toHaveBeenLastCalledWith('/api/auth/register', expect.objectContaining({ method: 'POST' }));
});

it('returns to the requested deep link after sign-in', async () => {
  window.history.replaceState(null, '', '/hB2YaG5AyLF/account/visa-3f9a');
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(Response.json({ mode: 'Disabled' }))
    .mockResolvedValueOnce(Response.json({ id: 'u1', userName: 'alice', email: 'alice@example.test', isAdministrator: true }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthGate><div>Budget application</div></AuthGate>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Username or email'), 'alice');
  expect(window.location.pathname).toBe('/login');
  await user.type(screen.getByLabelText('Password'), 'Secret-pass1!');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Budget application')).toBeInTheDocument();
  expect(window.location.pathname).toBe('/hB2YaG5AyLF/account/visa-3f9a');
  expect(window.sessionStorage.getItem('openbudget.returnTo')).toBeNull();
});

it('keeps emailed reset-password links reachable before sign-in', async () => {
  window.history.replaceState(null, '', '/reset-password?userId=u1&token=abc');
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(Response.json({ mode: 'Disabled' })));
  render(<AuthGate><div>Budget application</div></AuthGate>);
  expect(await screen.findByRole('heading', { name: 'Choose a new password' })).toBeInTheDocument();
  expect(window.location.pathname + window.location.search).toBe('/reset-password?userId=u1&token=abc');
  expect(window.sessionStorage.getItem('openbudget.returnTo')).toBeNull();
});
