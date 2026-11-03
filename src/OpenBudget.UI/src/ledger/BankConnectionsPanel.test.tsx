import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { BankConnectionsPanel } from './BankConnectionsPanel';

const overview = { connections: [], pendingImportCount: 2, providerConfigured: true };
const imports = [
  { id:'match',linkedAccountId:'linked',accountName:'Checking',date:'2026-10-20',amount:-18.4,currency:'EUR',payee:'ALBERTHEIJN NL',memo:'Groceries',providerStatus:'booked',reviewStatus:'pending',candidate:{id:'existing',date:'2026-10-20',amount:-18.4,payee:'Albert Heijn',score:0.94},suggestedPayee:'Albert Heijn',payeeSimilarity:0.88 },
  { id:'new',linkedAccountId:'linked',accountName:'Checking',date:'2026-10-19',amount:-12,currency:'EUR',payee:'Albert Heijn Leiden',memo:'Groceries',providerStatus:'booked',reviewStatus:'pending',candidate:null,suggestedPayee:'Albert Heijn',payeeSimilarity:0.77 }
];

afterEach(() => vi.unstubAllGlobals());

it('shows possible matches and sends user-selected matching actions', async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    void init;
    const path = String(input);
    if (path.endsWith('/overview')) return Response.json(overview);
    if (path.endsWith('/imports')) return Response.json(imports);
    return new Response(null, { status:204 });
  });
  vi.stubGlobal('fetch', fetchMock);
  render(<BankConnectionsPanel accounts={[]} onChanged={vi.fn()}/>);
  await userEvent.click(screen.getByRole('button', { name:/Bank connections/ }));
  expect(await screen.findByText(/Possible match: Albert Heijn/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name:'Verify match' }));
  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/banking/imports/match/review', expect.objectContaining({ method:'POST' })));

  const payeeSelect = screen.getByRole('combobox', { name:'Payee for Albert Heijn Leiden' });
  await userEvent.selectOptions(payeeSelect, 'suggested');
  await userEvent.click(screen.getByRole('button', { name:'Add transaction' }));
  await waitFor(() => {
    const calls = fetchMock.mock.calls.map(([path, options]) => [String(path), options] as const);
    const accept = calls.find(([path]) => path === '/api/banking/imports/new/review');
    expect(accept?.[1]?.body).toBe(JSON.stringify({ action:'accept', useSuggestedPayee:true }));
  });
});
