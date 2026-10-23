import { render, screen } from '@testing-library/react';
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
  expect(screen.getByText('Free')).toBeInTheDocument();
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
  await user.click(screen.getByRole('button', { name: 'Save membership' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/membership', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ membershipPlanId: 'plus', budgetLimitOverride: 2 }) }));
});

it('denies access when the API does not grant the administrator role', async () => {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).startsWith('/api/admin/users')
    ? new Response(null, { status: 403 })
    : String(input) === '/api/admin/dashboard' ? Response.json({ totalUsers: 0, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1, usersByMembership: [], recentRegistrations: [], recentSecurityEvents: [] }) : Response.json({ mode: 'Disabled' })));
  render(<AdminPanel />);
  expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
});
