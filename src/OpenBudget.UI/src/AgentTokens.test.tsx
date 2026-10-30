import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentTokens, type AgentToken } from './AgentTokens';

afterEach(() => vi.unstubAllGlobals());

const existing: AgentToken = {
  id: 't1', name: 'Laptop agent', prefix: 'obk_AbCdEfGh', permission: 'Reporting', budgets: [{ id: 'b1', name: 'Household' }],
  createdAt: '2026-10-01T10:00:00Z', expiresAt: '2026-12-30T10:00:00Z', lastUsedAt: '2026-10-20T08:30:00Z', revokedAt: null, status: 'active',
};
const synthetic = 'obk_' + 'x'.repeat(43);

function stubApi(overrides: Partial<Record<string, (init?: RequestInit) => Response>> = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const key = `${init?.method ?? 'GET'} ${url}`;
    if (overrides[key]) return overrides[key]!(init);
    if (key === 'GET /api/auth/agent-tokens') return Response.json([existing]);
    if (key === 'GET /api/budgets') return Response.json({ budgets: [{ id: 'b1', name: 'Household', isArchived: false }, { id: 'b2', name: 'Holiday fund', isArchived: false }, { id: 'b3', name: 'Old', isArchived: true }] });
    if (key === 'POST /api/auth/agent-tokens') {
      const body = JSON.parse(String(init?.body));
      return Response.json({ token: synthetic, details: { ...existing, id: 't2', name: body.name, prefix: synthetic.slice(0, 12), permission: body.permission,
        budgets: body.budgetIds.map((id: string) => ({ id, name: id === 'b1' ? 'Household' : 'Holiday fund' })), lastUsedAt: null } }, { status: 201 });
    }
    if (key === 'POST /api/auth/agent-tokens/t1/revoke') return new Response(null, { status: 204 });
    if (key === 'GET /api/auth/agent-tokens/t1/activity?limit=20') return Response.json([
      { id: 'a1', tool: 'plan_get_month', budgetId: 'b1', success: true, outcome: 'ok', createdAt: '2026-10-20T08:30:00Z' },
      { id: 'a2', tool: 'transactions_create', budgetId: 'b1', success: false, outcome: 'denied', createdAt: '2026-10-20T08:31:00Z' },
    ]);
    return new Response(null, { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

it('creates a scoped token, shows the secret once with copy, and hides Admin for non-administrators', async () => {
  const fetchMock = stubApi();
  const user = userEvent.setup();
  // user-event installs a clipboard stub during setup; observe it after.
  const writeText = vi.spyOn(navigator.clipboard, 'writeText');
  render(<AgentTokens isAdministrator={false} />);

  expect(await screen.findByText('Laptop agent')).toBeInTheDocument();
  expect(screen.getByText('obk_AbCdEfGh…')).toBeInTheDocument();
  expect(screen.queryByRole('option', { name: /Admin/ })).not.toBeInTheDocument();
  expect(screen.queryByRole('checkbox', { name: 'Old' })).not.toBeInTheDocument();

  await user.type(screen.getByLabelText('Token name'), 'Planner');
  await user.click(screen.getByRole('button', { name: 'Create token' }));
  expect(screen.getByText('Choose at least one budget for this token.')).toBeInTheDocument();
  expect(fetchMock).not.toHaveBeenCalledWith('/api/auth/agent-tokens', expect.objectContaining({ method: 'POST' }));

  await user.selectOptions(screen.getByLabelText('Permission'), 'Budget');
  await user.click(screen.getByRole('checkbox', { name: 'Holiday fund' }));
  await user.clear(screen.getByLabelText('Expires in (days)'));
  await user.type(screen.getByLabelText('Expires in (days)'), '30');
  await user.click(screen.getByRole('button', { name: 'Create token' }));

  expect(await screen.findByLabelText('New agent token')).toHaveTextContent(synthetic);
  expect(fetchMock).toHaveBeenCalledWith('/api/auth/agent-tokens', expect.objectContaining({
    method: 'POST', body: JSON.stringify({ name: 'Planner', permission: 'Budget', budgetIds: ['b2'], expiresInDays: 30 }),
  }));
  await user.click(screen.getByRole('button', { name: 'Copy token' }));
  expect(writeText).toHaveBeenCalledWith(synthetic);
  expect(await screen.findByText('Copied to clipboard.')).toBeInTheDocument();
  expect(within(screen.getByRole('list', { name: 'Agent tokens' })).getByText('Planner')).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Done' }));
  expect(screen.queryByText(synthetic)).not.toBeInTheDocument();
});

it('offers Admin tokens to administrators without a budget selection', async () => {
  const fetchMock = stubApi();
  const user = userEvent.setup();
  render(<AgentTokens isAdministrator />);
  await screen.findByText('Laptop agent');
  await user.selectOptions(screen.getByLabelText('Permission'), 'Admin');
  expect(screen.queryByRole('group', { name: 'Budgets' })).not.toBeInTheDocument();
  expect(screen.getByText(/Everything you can do in OpenBudget/)).toBeInTheDocument();
  await user.type(screen.getByLabelText('Token name'), 'Ops');
  await user.click(screen.getByRole('button', { name: 'Create token' }));
  await screen.findByLabelText('New agent token');
  expect(fetchMock).toHaveBeenCalledWith('/api/auth/agent-tokens', expect.objectContaining({ body: JSON.stringify({ name: 'Ops', permission: 'Admin', budgetIds: [], expiresInDays: 90 }) }));
});

it('shows server validation errors and keeps the form', async () => {
  stubApi({ 'POST /api/auth/agent-tokens': () => Response.json({ error: 'Only administrators can create Admin agent tokens.' }, { status: 403 }) });
  const user = userEvent.setup();
  render(<AgentTokens isAdministrator={false} />);
  await screen.findByText('Laptop agent');
  await user.type(screen.getByLabelText('Token name'), 'Planner');
  await user.click(screen.getByRole('checkbox', { name: 'Household' }));
  await user.click(screen.getByRole('button', { name: 'Create token' }));
  expect(await screen.findByText('Only administrators can create Admin agent tokens.')).toBeInTheDocument();
  expect(screen.getByLabelText('Token name')).toHaveValue('Planner');
  expect(screen.queryByLabelText('New agent token')).not.toBeInTheDocument();
});

it('revokes only after confirmation and shows recent agent activity', async () => {
  const fetchMock = stubApi();
  const user = userEvent.setup();
  render(<AgentTokens isAdministrator={false} />);
  await screen.findByText('Laptop agent');

  await user.click(screen.getByRole('button', { name: 'Recent activity' }));
  const table = await screen.findByRole('table', { name: 'Recent activity for Laptop agent' });
  expect(within(table).getByText('plan_get_month')).toBeInTheDocument();
  expect(within(table).getByText('Succeeded')).toBeInTheDocument();
  expect(within(table).getByText('denied')).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Revoke' }));
  const confirm = screen.getByRole('group', { name: 'Confirm revoking Laptop agent' });
  await user.click(within(confirm).getByRole('button', { name: 'Cancel' }));
  expect(fetchMock).not.toHaveBeenCalledWith('/api/auth/agent-tokens/t1/revoke', expect.anything());
  await user.click(screen.getByRole('button', { name: 'Revoke' }));
  await user.click(screen.getByRole('button', { name: 'Confirm revoke' }));
  expect(fetchMock).toHaveBeenCalledWith('/api/auth/agent-tokens/t1/revoke', expect.objectContaining({ method: 'POST' }));
  expect(await screen.findByText('Revoked')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Revoke' })).not.toBeInTheDocument();
});

it('shows a recoverable error when tokens cannot load and an empty state', async () => {
  let fail = true;
  stubApi({ 'GET /api/auth/agent-tokens': () => fail ? new Response(null, { status: 500 }) : Response.json([]) });
  const user = userEvent.setup();
  render(<AgentTokens isAdministrator={false} />);
  expect(await screen.findByText('Agent tokens could not be loaded.')).toBeInTheDocument();
  fail = false;
  await user.click(screen.getByRole('button', { name: 'Retry' }));
  expect(await screen.findByText('No agent tokens yet.')).toBeInTheDocument();
});
