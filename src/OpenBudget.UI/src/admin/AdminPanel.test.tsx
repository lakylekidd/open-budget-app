import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { AdminPanel } from './AdminPanel';

afterEach(() => vi.unstubAllGlobals());

it('shows pending users and requires confirmation before approval', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.startsWith('/api/admin/users')) return Response.json([{ id: 'u1', userName: 'alice', email: 'alice@example.test', createdAt: '2026-10-01T00:00:00Z', lastLoginAt: null, status: 'Pending', isBootstrapAdministrator: false, lockoutEnd: null }]);
    if (url === '/api/admin/audit') return Response.json([]);
    if (url === '/api/auth/registration-mode') return Response.json({ mode: 'ApprovalRequired' });
    if (url === '/api/admin/dashboard') return Response.json({ totalUsers: 1, activeUsers: 0, pendingUsers: 1, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1 });
    if (url === '/api/admin/users/u1/approve' && init?.method === 'POST') return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
  const confirmMock = vi.fn(() => true);
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', confirmMock);
  render(<AdminPanel />);
  const user = userEvent.setup();
  const approve = await screen.findByRole('button', { name: 'Approve' });
  await user.click(approve);
  expect(confirmMock).toHaveBeenCalledWith('Approve alice?');
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users/u1/approve', expect.objectContaining({ method: 'POST' }));
  await user.selectOptions(screen.getByLabelText('Filter status'), 'Suspended');
  await user.click(screen.getByRole('button', { name: 'Apply' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/admin/users?status=Suspended');
});

it('denies access when the API does not grant the administrator role', async () => {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).startsWith('/api/admin/users')
    ? new Response(null, { status: 403 })
    : String(input) === '/api/admin/dashboard' ? Response.json({ totalUsers: 0, activeUsers: 0, pendingUsers: 0, lockedUsers: 0, suspendedUsers: 0, totalBudgets: 1, activeBudgets: 1 }) : Response.json(String(input) === '/api/admin/audit' ? [] : { mode: 'Disabled' })));
  render(<AdminPanel />);
  expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
});
