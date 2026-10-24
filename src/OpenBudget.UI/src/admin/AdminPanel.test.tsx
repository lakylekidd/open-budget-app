import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { AdminPanel } from './AdminPanel';

afterEach(() => vi.unstubAllGlobals());

it('shows pending users and requires confirmation before approval', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.startsWith('/api/admin/users')) return Response.json([{ id: 'u1', userName: 'alice', email: 'alice@example.test', createdAt: '2026-10-01T00:00:00Z', lastLoginAt: null, status: 'Pending', isBootstrapAdministrator: false, lockoutEnd: null, membershipPlanId: 'free', budgetLimitOverride: null }]);
    if (url === '/api/admin/audit') return Response.json([]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'ApprovalRequired' });
    if (url === '/api/admin/settings/email') return Response.json({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 1, activeUsers: 0, pendingUsers: 1, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1, usersByMembership: [{ membershipPlanId: 'free', name: 'Free', userCount: 1 }], recentRegistrations: [{ userId: 'u1', userName: 'alice', createdAt: '2026-10-01T00:00:00Z', status: 'Pending' }], recentSecurityEvents: [{ id: 'e1', actorUserId: 'admin', targetUserId: 'u1', action: 'user.register', createdAt: '2026-10-01T00:00:00Z' }] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }, { id: 'plus', name: 'Plus', maxActiveBudgets: 3 }, { id: 'pro', name: 'Pro', maxActiveBudgets: null }]);
    if (url === '/api/admin/users/u1/approve' && init?.method === 'POST') return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  const confirmMock = vi.fn(() => true);
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', confirmMock);
  render(<AdminPanel />);
  expect(await screen.findByRole('heading', { name: 'Users by membership' })).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Users by membership' }).parentElement).toHaveTextContent('Free');
  expect(screen.getByRole('heading', { name: 'Recent registrations' })).toBeInTheDocument();
  expect(screen.getByText('user.register')).toBeInTheDocument();
  const user = userEvent.setup();
  const approve = await screen.findByRole('button', { name: 'Approve' });
  await user.click(approve);
  expect(confirmMock).toHaveBeenCalledWith('Approve alice?');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/approve', expect.objectContaining({ method: 'POST' }));
  await user.selectOptions(screen.getByLabelText('Filter status'), 'Suspended');
  await user.click(screen.getByRole('button', { name: 'Apply' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users?status=Suspended');
});

it('lets administrators assign a membership tier and a budget-limit override', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/admin/users')) return Response.json([{ id: 'u1', userName: 'alice', email: 'alice@example.test', createdAt: '2026-10-01T00:00:00Z', lastLoginAt: null, status: 'Active', isBootstrapAdministrator: false, lockoutEnd: null, membershipPlanId: 'free', budgetLimitOverride: null }]);
    if (url === '/api/admin/audit') return Response.json([]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'Open' });
    if (url === '/api/admin/settings/email') return Response.json({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 1, activeUsers: 1, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 0, activeBudgets: 0, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }, { id: 'plus', name: 'Plus', maxActiveBudgets: 3 }]);
    if (url === '/api/admin/users/u1/membership') return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', vi.fn(() => true));
  render(<AdminPanel />);
  const user = userEvent.setup();
  await user.selectOptions(await screen.findByLabelText('Membership for alice'), 'plus');
  await user.type(screen.getByLabelText('Budget limit override for alice'), '2');
  await user.click(screen.getByText('Subscription metadata'));
  fireEvent.change(screen.getByLabelText('Membership starts for alice'), { target: { value: '2026-10-01' } });
  fireEvent.change(screen.getByLabelText('Membership expires for alice'), { target: { value: '2027-10-01' } });
  await user.type(screen.getByLabelText('Billing provider for alice'), 'stripe');
  await user.type(screen.getByLabelText('External subscription ID for alice'), 'sub_test_123');
  await user.click(screen.getByRole('button', { name: 'Save membership' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/membership', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ membershipPlanId: 'plus', budgetLimitOverride: 2, startsOn: '2026-10-01', expiresOn: '2027-10-01', billingProvider: 'stripe', externalSubscriptionId: 'sub_test_123' }) }));
});

it('requires confirmation and requests a password reset link without handling a password', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/admin/users')) return Response.json([{ id: 'u1', userName: 'alice', email: 'alice@example.test', createdAt: '2026-10-01T00:00:00Z', lastLoginAt: null, status: 'Active', isBootstrapAdministrator: false, lockoutEnd: null, membershipPlanId: 'free', budgetLimitOverride: null }]);
    if (url === '/api/admin/settings/email') return Response.json({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'Open' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 1, activeUsers: 1, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }]);
    if (url === '/api/admin/users/u1/password-reset') return Response.json({ message: 'A password reset link has been sent.' }, { status: 202 });
    return new Response(null, { status: 404 });
  });
  const confirmMock = vi.fn(() => true);
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', confirmMock);
  render(<AdminPanel />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Send reset link' }));
  expect(confirmMock).toHaveBeenCalledWith('Send a secure password reset link to alice@example.test?');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/password-reset', expect.objectContaining({ method: 'POST' }));
});

it('denies access when the API does not grant the administrator role', async () => {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).startsWith('/api/admin/users')
    ? new Response(null, { status: 403 })
    : String(input) === '/api/admin/dashboard' ? Response.json({ totalUsers: 0, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] }) : Response.json({ mode: 'Disabled' })));
  render(<AdminPanel />);
  expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
});

it('saves SMTP settings without rendering the saved password and sends a delivery test', async () => {
  const emailSettings = { host: 'smtp.example.test', port: 587, from: 'hello@example.test', username: 'mailer', enableSsl: true, hasPassword: true, isConfigured: true, source: 'admin' };
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.startsWith('/api/admin/users')) return Response.json([]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'Disabled' });
    if (url === '/api/admin/settings/email' && init?.method === 'PUT') return Response.json(emailSettings);
    if (url === '/api/admin/settings/email') return Response.json(emailSettings);
    if (url === '/api/admin/settings/email/test') return Response.json({ message: 'Test email sent.' }, { status: 202 });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 0, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 0, activeBudgets: 0, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }]);
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<AdminPanel />);
  const user = userEvent.setup();
  expect(await screen.findByText('Configured')).toBeInTheDocument();
  expect(screen.getByLabelText('SMTP password')).toHaveValue('');
  await user.type(screen.getByLabelText('SMTP password'), 'new-secret');
  await user.click(screen.getByRole('button', { name: 'Save email settings' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/settings/email', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ host: 'smtp.example.test', port: 587, from: 'hello@example.test', username: 'mailer', password: 'new-secret', enableSsl: true }) }));
  await user.type(screen.getByLabelText('Test email recipient'), 'admin@example.test');
  await user.click(screen.getByRole('button', { name: 'Send test email' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/settings/email/test', expect.objectContaining({ method: 'POST', body: JSON.stringify({ to: 'admin@example.test' }) }));
  expect(await screen.findByRole('status')).toHaveTextContent('Test email sent.');
});

it('invites a user by email without entering or returning a password', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url === '/api/admin/users/invite') return Response.json({ message: 'An account verification link has been sent.' }, { status: 202 });
    if (url.startsWith('/api/admin/users')) return Response.json([]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'Disabled' });
    if (url === '/api/admin/settings/email') return Response.json({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 0, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 0, activeBudgets: 0, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }]);
    return new Response(null, { status: 404 });
  });
  const confirmMock = vi.fn(() => true);
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', confirmMock);
  render(<AdminPanel />);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Invitation username'), 'new-user');
  await user.type(screen.getByLabelText('Invitation email'), 'new@example.test');
  await user.click(screen.getByRole('button', { name: 'Send invitation' }));
  expect(confirmMock).toHaveBeenCalledWith('Invite new-user at new@example.test?');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/invite', expect.objectContaining({
    method: 'POST',
    body: JSON.stringify({ userName: 'new-user', email: 'new@example.test', membershipPlanId: 'free' }),
  }));
  expect(await screen.findByRole('status')).toHaveTextContent('Verification link sent');
  expect(screen.queryByLabelText('Invitation password')).not.toBeInTheDocument();
});

it('loads a user-specific status history when the administrator expands it', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url === '/api/admin/users/u1/history') return Response.json([
      { id: 'e1', actorUserId: 'root', targetUserId: 'u1', action: 'user.suspend', createdAt: '2026-10-02T00:00:00Z' },
    ]);
    if (url.startsWith('/api/admin/users')) return Response.json([{ id: 'u1', userName: 'alice', email: 'alice@example.test', createdAt: '2026-10-01T00:00:00Z', lastLoginAt: null, status: 'Suspended', isBootstrapAdministrator: false, lockoutEnd: null, membershipPlanId: 'free', budgetLimitOverride: null }]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'Disabled' });
    if (url === '/api/admin/settings/email') return Response.json({ host: '', port: 587, from: '', username: null, enableSsl: true, hasPassword: false, isConfigured: false, source: 'none' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 1, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 1, totalBudgets: 0, activeBudgets: 0, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] });
    if (url === '/api/admin/membership-plans') return Response.json([{ id: 'free', name: 'Free', maxActiveBudgets: 1 }]);
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<AdminPanel />);
  const user = userEvent.setup();
  await user.click(await screen.findByText('View account history'));
  expect(await screen.findByText('user.suspend')).toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/history');
});
