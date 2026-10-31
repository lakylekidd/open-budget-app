import { defaultColumnWidths } from './ViewOptions';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { defaultFlags } from './Flags';
import { defaultView, type ViewOptions } from './ViewOptions';
import { AccountsPage, registerColumnOrder, resizeColumnPair } from './AccountsPage';
import { type Account, type Register } from './api';

const checking: Account = { id: 'checking', name: 'Checking', isClosed: false, balance: 90, clearedBalance: 100, unclearedBalance: -10, accountType:'checking', isFavorite:false, connectionStatus:'unlinked', lastReconciledAt:null, needsCategoryCount:1 };
const savings: Account = { id: 'savings', name: 'Savings', isClosed: false, balance: 0, clearedBalance: 0, unclearedBalance: 0, accountType:'savings', isFavorite:false, connectionStatus:'unlinked', lastReconciledAt:null, needsCategoryCount:0 };
let register: Register;
let failMutation: 'network' | 'conflict' | null;
let mutations: { path: string; body: Record<string, unknown> }[];
let scheduledItems: import("./api").ScheduledItem[];
let failRead: boolean;
let views: Record<string, ViewOptions>;
let failViewRead: boolean;
let failViewSave: boolean;
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  register = { version: 3, page: 1, pageSize: 25, total: 1, accounts: { version: 3, accounts: [checking, savings] }, items: [
    { id: 'transaction', accountId: checking.id, date: '2026-10-21', amount: -10, payee: 'Shop', memo: 'Lunch', categoryId: null,
      status: 'uncleared', transferId: null, isOpening: false, splits: [], needsAttention: true, runningBalance:90, transferAccountName:null, categoryName:null, isLocked:false },
  ] };
  scheduledItems=[];
  views = {}; failViewRead = false; failViewSave = false;
  failMutation = null; failRead = false; mutations = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path.startsWith('/api/register-views/')) {
      if (init?.method === 'PUT') {
        if (failViewSave) return response({error:'View options changed in another session.'},409);
        views[path] = {...JSON.parse(String(init.body)),version:(views[path]?.version ?? 0)+1};
      } else if (failViewRead) return response({error:'Database unavailable'},503);
      return response(views[path] ?? defaultView);
    }
    if (init?.method && init.method !== 'GET') {
      mutations.push({ path, body: JSON.parse(String(init.body)) });
      if (failMutation === 'network') { failMutation = null; throw new Error('Connection lost. Retry the same action.'); }
      if (failMutation === 'conflict') { failMutation = null; return response({ error: 'The ledger changed in another session.' }, 409); }
      return response({ id: 'saved', version: 4 });
    }
    if (path.startsWith('/api/schedules')) {const accountId=new URL(path,'http://localhost').searchParams.get('accountId');const items=scheduledItems.filter(item=>!accountId||item.transaction.accountId===accountId);return response({version:3,page:1,pageSize:25,total:items.length,items});}
    if (path === '/api/flags') return response({version:3,flags:defaultFlags});
    if (path.startsWith('/api/transactions')) return failRead ? response({ error: 'Database unavailable' }, 503) : response(register);
    if (path.startsWith('/api/budget/')) return response({ groups: [{ categories: [{ id: 'food', name: 'Food' }] }] });
    if (path === '/api/payees/manage') return response({version:3,payees:[{id:'shop',name:'Shop',isHidden:false,categoryMode:'history',defaultCategoryId:null,effectiveCategoryId:null,transactionCount:1,rules:[]}]});
    if (path === '/api/payees') return response(['Shop']);
    if (path.startsWith('/api/ledger/audit')) return response({ items: [], total: 0 });
    throw new Error(`Unexpected request ${path}`);
  }));
});

function show(selectedAccount = '') { const onChanged = vi.fn(); render(<AccountsPage selectedAccount={selectedAccount} onSelectAccount={vi.fn()} onChanged={onChanged} onMenu={vi.fn()} />); return onChanged; }
async function ready() { await screen.findByRole('button', { name: 'Edit Shop 2026-10-21' }); }

it('renders each saved split as a category detail row beneath its parent transaction',async()=>{
 register.items[0]={...register.items[0],categoryId:null,needsAttention:false,splits:[{categoryId:'food',amount:-7,payee:'Market',memo:'Fruit'},{categoryId:'food',amount:-3,payee:'Cafe',memo:'Coffee'}]};
 show('checking');await ready();
 expect(screen.getByRole('row',{name:'Split 1'})).toHaveTextContent('Food · Market');expect(screen.getByRole('row',{name:'Split 1'})).toHaveTextContent('Fruit');
 expect(screen.getByRole('row',{name:'Split 1'})).toHaveTextContent('€7.00');expect(screen.getByRole('row',{name:'Split 2'})).toHaveTextContent('Cafe');
 const disclosure=screen.getByRole('button',{name:'Collapse split categories'});expect(disclosure).toHaveAttribute('aria-expanded','true');await userEvent.click(disclosure);
 expect(screen.queryByRole('row',{name:'Split 1'})).not.toBeInTheDocument();expect(screen.queryByRole('row',{name:'Split 2'})).not.toBeInTheDocument();
 const expand=screen.getByRole('button',{name:'Expand split categories'});expect(expand).toHaveAttribute('aria-expanded','false');await userEvent.click(expand);expect(screen.getByRole('row',{name:'Split 1'})).toBeInTheDocument();
 await userEvent.dblClick(screen.getByRole('row',{name:'Split 2'}));expect(screen.getByRole('row',{name:'Edit transaction inline'})).toBeInTheDocument();
});

it('resizes every visible register column and persists the selected column width', async () => {
  show('checking'); await ready();
  const handle = screen.getByRole('separator', { name: 'Resize payee column' });
  const payeeColumn = handle.closest('th')?.cellIndex;
  expect(screen.getAllByRole('separator', { name: /^Resize .* column$/ })).toHaveLength(11);
  fireEvent.pointerDown(handle, { pointerId: 1, clientX: 200 });
  fireEvent.pointerMove(handle, { pointerId: 1, clientX: 260 });
  fireEvent.pointerUp(handle, { pointerId: 1, clientX: 260 });
  await waitFor(() => expect(views['/api/register-views/checking']?.columnWidths.payee).toBe(260));
  // Only the right-hand neighbour gives up the width; every other column is unchanged.
  expect(views['/api/register-views/checking']?.columnWidths).toEqual({ ...defaultColumnWidths, payee: 260, category: 170 });
  expect(screen.getAllByRole('columnheader')[payeeColumn!].querySelector('.register-resize-handle')).toBeInTheDocument();
});

it('shows and selects a later projected occurrence opened from annual planning', async () => {
  const projected = { ...register.items[0], id: 'future-occurrence', date: '2027-06-01', amount: -25, payee: 'Rent', categoryId: 'food', needsAttention: false };
  render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()}
    focusTransaction={{ id: projected.id, accountId: 'checking', date: projected.date, scheduled: true, projected }} />);
  const row = await screen.findByRole('row', { name: /Rent/ });
  expect(row).toHaveAttribute('aria-selected', 'true');
  expect(row).toHaveAttribute('data-transaction-id', 'future-occurrence');
});

it('shows the bottom bar on selection and applies one action to the selected ids',async()=>{
  register.items.push({...register.items[0],id:'second',payee:'Cafe'});register.total=2;
  show('checking');await ready();expect(screen.queryByRole('region',{name:'Selected transactions'})).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole('checkbox',{name:'Select visible transactions'}));
  expect(screen.getByRole('region',{name:'Selected transactions'})).toHaveTextContent('2 Transactions');
  await userEvent.click(screen.getByRole('button',{name:'More selected transaction actions'}));await userEvent.click(screen.getByRole('menuitem',{name:'Mark as Cleared'}));
  await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0]).toMatchObject({path:'/api/transactions/selection',body:{ids:['transaction','second'],action:'status',status:'cleared'}});
  await waitFor(()=>expect(screen.queryByRole('region',{name:'Selected transactions'})).not.toBeInTheDocument());
});

it('retains an uncertain selection command for identical retry and hides the bar while editing inline',async()=>{
  show('checking');await ready();await userEvent.click(screen.getByRole('checkbox',{name:'Select transaction transaction'}));
  await userEvent.click(screen.getByRole('button',{name:'More selected transaction actions'}));failMutation='network';await userEvent.click(screen.getByRole('menuitem',{name:'Duplicate'}));
  await screen.findByRole('button',{name:'Retry save'});expect(screen.getByRole('button',{name:'Clear transaction selection'})).toBeDisabled();
  await userEvent.click(screen.getByRole('button',{name:'Retry save'}));await waitFor(()=>expect(mutations).toHaveLength(2));expect(mutations[1]).toEqual(mutations[0]);
  await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));
  expect(screen.getByRole('row',{name:'Edit transaction inline'})).toBeInTheDocument();expect(screen.queryByRole('region',{name:'Selected transactions'})).not.toBeInTheDocument();
});

it('saves an unmatched payee name and offers the saved payee in the next transaction',async()=>{
  const original=vi.mocked(fetch).getMockImplementation()!;let created=false;
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>{
    if(path==='/api/transactions'&&init?.method==='POST')created=true;
    if(path==='/api/payees/manage'&&created)return response({version:4,payees:[{id:'cafe',name:'New Cafe',isHidden:false,categoryMode:'history',defaultCategoryId:null,effectiveCategoryId:null,transactionCount:1,rules:[]}]});
    return original(path,init);
  }));
  show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Add Transaction'}));
  await userEvent.type(screen.getByRole('combobox',{name:'Payee'}),'New Cafe');
  await screen.findByText('No saved payees found. Type a name to create one when saving.');
  await userEvent.type(screen.getByRole('textbox',{name:'Outflow'}),'5');await userEvent.click(screen.getByRole('button',{name:'Save and add another'}));
  await waitFor(()=>expect(screen.getByRole('combobox',{name:'Payee'})).toHaveValue(''));
  expect(mutations[0]).toMatchObject({path:'/api/transactions',body:{payee:'New Cafe',amount:-5}});
  await waitFor(()=>expect(screen.getByRole('button',{name:/^Save$/})).toBeEnabled());
  await userEvent.type(screen.getByRole('combobox',{name:'Payee'}),'cAf');
  await userEvent.click(await screen.findByRole('option',{name:'New Cafe'}));
  expect(screen.getByRole('combobox',{name:'Payee'})).toHaveValue('New Cafe');
});

it('creates and selects a category without losing an existing transaction draft, then saves its category id',async()=>{
  const original=vi.mocked(fetch).getMockImplementation()!;
  let month={month:'2026-10',note:'',version:1,fundsAdded:0,readyToAssign:25,groups:[{id:'daily',name:'Everyday',sortOrder:0,isArchived:false,categories:[{id:'food',name:'Food',assigned:25,target:0,icon:null,snoozed:false,sortOrder:0,isArchived:false,available:25}]}]};
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>{if(path.startsWith('/api/budget/')){if(init?.method==='PUT'){const saved=JSON.parse(String(init.body));month={...saved,groups:saved.groups.map((g:typeof month.groups[0])=>({...g,categories:g.categories.map(c=>({...c,available:0}))}))};}return response(month);}return original(path,init);}));
  show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.clear(screen.getByRole('textbox',{name:'Memo'}));await userEvent.type(screen.getByRole('textbox',{name:'Memo'}),'Unsaved draft note');
  await userEvent.click(screen.getByRole('combobox',{name:'Category'}));await userEvent.click(screen.getByRole('button',{name:/New Category/}));await userEvent.type(await screen.findByRole('textbox',{name:'Category Name'}),'Household');await userEvent.click(screen.getByRole('button',{name:'Save category'}));
  await waitFor(()=>expect(screen.getByRole('combobox',{name:'Category'})).toHaveValue('Everyday: Household'));expect(screen.getByRole('textbox',{name:'Memo'})).toHaveValue('Unsaved draft note');
  await userEvent.click(screen.getByRole('button',{name:'Save'}));await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0].body).toMatchObject({categoryId:month.groups[0].categories[1].id,memo:'Unsaved draft note',amount:-10});
});

it('uses the transaction month for category balances and reloads them when its date changes',async()=>{
  const original=vi.mocked(fetch).getMockImplementation()!;
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>path.startsWith('/api/budget/')?response({readyToAssign:path.endsWith('2026-11')?10:20,groups:[{id:'daily',name:'Everyday',categories:[{id:'food',name:'Food',available:path.endsWith('2026-11')?12.5:50}]}]}):original(path,init)));
  show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('combobox',{name:'Category'}));expect(screen.getByLabelText('Food available')).toHaveTextContent('€50.00');await userEvent.keyboard('{Escape}');
  await userEvent.clear(screen.getByRole('textbox',{name:'Date'}));await userEvent.type(screen.getByRole('textbox',{name:'Date'}),'01-11-2026');await waitFor(()=>expect(vi.mocked(fetch).mock.calls.some(([path])=>path==='/api/budget/2026-11')).toBe(true));await userEvent.click(screen.getByRole('combobox',{name:'Category'}));expect(screen.getByLabelText('Food available')).toHaveTextContent('€12.50');expect(screen.getByLabelText('Ready to Assign available')).toHaveTextContent('€10.00');
});

it('Payment/Transfer opens account choices and saves a new paired transfer',async()=>{
  show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Add Transaction'}));await userEvent.type(screen.getByRole('textbox',{name:'Outflow'}),'15');await userEvent.click(screen.getByRole('combobox',{name:'Category'}));await userEvent.click(screen.getByRole('button',{name:'Payment/Transfer'}));await userEvent.click(await screen.findByRole('option',{name:'Transfer to Savings'}));await userEvent.click(screen.getByRole('button',{name:'Save'}));await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0]).toMatchObject({path:'/api/transfers',body:{fromAccountId:'checking',toAccountId:'savings',amount:15}});
});

it('opens Manage Payees and filters the register by the exact saved payee id',async()=>{
  show();await ready();await userEvent.click(screen.getByRole('button',{name:'More account actions'}));await userEvent.click(screen.getByRole('button',{name:'Manage Payees'}));
  await userEvent.click(await screen.findByRole('button',{name:'Show 1 Transactions'}));
  await waitFor(()=>expect(vi.mocked(fetch).mock.calls.some(([path])=>String(path).includes('payeeId=shop'))).toBe(true));expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

it('shows loading followed by balances and uncategorized spending', async () => {
  show(); expect(screen.getByRole('status')).toHaveTextContent('Loading transactions');
  expect(screen.getByTestId('working-balance')).toHaveTextContent('—'); await ready();
  expect(screen.getByText('Needs category', { selector: 'span' })).toBeInTheDocument();
  expect(screen.getByTestId('working-balance')).toHaveTextContent('€90.00');
  expect(screen.getByTestId('cleared-balance')).toHaveTextContent('€100.00');
});

it('keeps the register on screen while a live refresh re-fetches it, then swaps in the new rows', async () => {
  // Regression: every background refresh (30-second poll, live ledger event) used to drop the saved view and show
  // "Loading transactions…" in place of the rows, which flashed the whole register.
  const props = { onSelectAccount: vi.fn(), onChanged: vi.fn(), onMenu: vi.fn() };
  const page = render(<AccountsPage selectedAccount="checking" liveRefresh={0} {...props}/>); await ready();
  const row = screen.getByRole('button', { name: 'Edit Shop 2026-10-21' }).closest('tr');
  const baseFetch = vi.mocked(fetch).getMockImplementation()!;
  let release!: () => void; const held = new Promise<void>(resolve => { release = resolve; });
  vi.mocked(fetch).mockImplementation(async (input, init) => {
    if (String(input).startsWith('/api/transactions')) await held;
    return baseFetch(input, init);
  });
  register = { ...register, version: 4, items: [{ ...register.items[0], id: 'later', payee: 'Cafe' }, ...register.items] };
  page.rerender(<AccountsPage selectedAccount="checking" liveRefresh={1} {...props}/>);
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.filter(([path]) => String(path).startsWith('/api/transactions'))).toHaveLength(2));
  expect(screen.queryByText('Loading transactions…')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Edit Shop 2026-10-21' }).closest('tr')).toBe(row);
  expect(screen.getByRole('button', { name: 'Add Transaction' })).toBeEnabled();
  release();
  expect(await screen.findByRole('button', { name: 'Edit Cafe 2026-10-21' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Edit Shop 2026-10-21' }).closest('tr')).toBe(row);
  expect(screen.queryByText('Loading transactions…')).not.toBeInTheDocument();
  // The unchanged saved view is revalidated without triggering a second register fetch.
  expect(vi.mocked(fetch).mock.calls.filter(([path]) => String(path).startsWith('/api/transactions'))).toHaveLength(2);
});

it('distinguishes an empty account list, empty filters, and a failed load with retry', async () => {
  register = { ...register, items: [], total: 0, accounts: { version: 1, accounts: [] } };
  failRead = true; show();
  expect(await screen.findByRole('alert')).toHaveTextContent('Database unavailable');
  failRead = false; await userEvent.click(screen.getByRole('button', { name: 'Reload' }));
  expect(await screen.findByText('Give your money a home')).toBeInTheDocument();
  register = { ...register, accounts: { version: 1, accounts: [checking] } };
  await userEvent.click(screen.getByRole('button', { name: 'More account actions' })); await userEvent.click(screen.getByRole('button', { name: 'Refresh ledger' }));
  expect(await screen.findByText('No transactions match these filters.')).toBeInTheDocument();
});

it('creates an account with opening balance and notifies the Plan sidebar', async () => {
  const onChanged = show(); const user = userEvent.setup(); await ready();
  await user.click(screen.getByRole('button', { name: 'Add account' }));
  await user.click(within(screen.getByRole('dialog', { name: 'Select Account Type' })).getByRole('button', { name: 'Checking' }));
  const dialog = screen.getByRole('dialog');
  await user.type(within(dialog).getByLabelText('Account name'), 'Wallet');
  await user.clear(within(dialog).getByLabelText('Opening balance'));
  await user.type(within(dialog).getByLabelText('Opening balance'), '12.34');
  await user.click(within(dialog).getByRole('button', { name: 'Save account' }));
  await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
  expect(mutations[0]).toEqual({ path: '/api/accounts', body: expect.objectContaining({ name: 'Wallet', openingBalance: 12.34, version: 3, commandId: expect.any(String) }) });
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

it('offers cash and credit account types and records a credit balance owed as debt', async () => {
  show(); const user = userEvent.setup(); await ready();
  await user.click(screen.getByRole('button', { name: 'Add account' }));
  const picker = screen.getByRole('dialog', { name: 'Select Account Type' });
  expect(within(picker).getByRole('region', { name: 'Cash Accounts' })).toHaveTextContent('A cash account holds funds you already own and can spend immediately.');
  const credit = within(picker).getByRole('region', { name: 'Credit Accounts' });
  expect(within(credit).getAllByRole('button').map(button => button.textContent)).toEqual(['Credit Card', 'Line of Credit']);
  await user.click(within(credit).getByRole('button', { name: 'Credit Card' }));
  const form = screen.getByRole('dialog', { name: 'Add Credit Card account' });
  await user.click(within(form).getByRole('button', { name: 'Back' }));
  await user.click(within(screen.getByRole('dialog', { name: 'Select Account Type' })).getByRole('button', { name: 'Credit Card' }));
  await user.type(screen.getByLabelText('Account name'), 'Mastercard');
  await user.clear(screen.getByLabelText('Current balance owed'));
  await user.type(screen.getByLabelText('Current balance owed'), '250');
  await user.click(screen.getByRole('button', { name: 'Save account' }));
  await waitFor(() => expect(mutations[0]).toEqual({ path: '/api/accounts', body: expect.objectContaining({ name: 'Mastercard', accountType: 'creditCard', openingBalance: -250 }) }));
});

it('retries a lost response with the identical command id and body', async () => {
  const onPendingChange = vi.fn();
  render(<AccountsPage selectedAccount="" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()} onPendingChange={onPendingChange} />);
  const user = userEvent.setup(); await ready(); failMutation = 'network';
  await user.click(screen.getByRole('button', { name: 'Add account' }));
  await user.click(screen.getByRole('button', { name: 'Checking' }));
  await user.type(screen.getByLabelText('Account name'), 'Wallet');
  await user.click(screen.getByRole('button', { name: 'Save account' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Connection lost');
  expect(onPendingChange).toHaveBeenLastCalledWith(true);
  expect(screen.getByRole('button', { name: 'Save account' })).toBeDisabled();
  await user.click(screen.getByRole('button', { name: 'Reload' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry save' })).toBeEnabled());
  await user.click(screen.getByRole('button', { name: 'Retry save' }));
  await waitFor(() => expect(mutations).toHaveLength(2));
  expect(mutations[1].body).toEqual(mutations[0].body);
  await waitFor(() => expect(onPendingChange).toHaveBeenLastCalledWith(false));
});

it('keeps edits on conflict and requires a reload before resubmitting', async () => {
  show(); const user = userEvent.setup(); await ready(); failMutation = 'conflict';
  await user.click(screen.getByRole('button', { name: 'Add account' }));
  await user.click(screen.getByRole('button', { name: 'Checking' }));
  await user.type(screen.getByLabelText('Account name'), 'Wallet');
  await user.click(screen.getByRole('button', { name: 'Save account' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('changed in another session');
  expect(screen.getByRole('button', { name: 'Save account' })).toBeDisabled();
  register = { ...register, version: 9, accounts: { ...register.accounts, version: 9 } };
  await user.click(screen.getByRole('button', { name: 'Reload ledger' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save account' })).toBeEnabled());
  expect(screen.getByLabelText('Account name')).toHaveValue('Wallet');
  await user.click(screen.getByRole('button', { name: 'Save account' }));
  await waitFor(() => expect(mutations).toHaveLength(2));
  expect(mutations[1].body.version).toBe(9);
  expect(mutations[1].body.commandId).not.toEqual(mutations[0].body.commandId);
});

it('only confirms reconciliation at zero difference', async () => {
  show('checking'); const user = userEvent.setup(); await ready();
  await user.click(screen.getByRole('button', { name: 'Reconcile' }));
  const statement = screen.getByLabelText('Statement balance');
  await user.type(statement, '90');
  expect(screen.getByRole('button', { name: 'Confirm reconciliation' })).toBeDisabled();
  expect(within(screen.getByRole('dialog')).getByRole('status')).toHaveTextContent('-€10.00');
  await user.clear(statement); await user.type(statement, '100');
  await user.click(screen.getByRole('button', { name: 'Confirm reconciliation' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toEqual({ path: '/api/accounts/checking/reconcile', body: expect.objectContaining({ statementBalance: 100 }) });
});

it('validates transfers and sends a paired transfer command', async () => {
  show(); const user = userEvent.setup(); await ready();
  await user.click(screen.getByRole('button', { name: 'More account actions' })); await user.click(screen.getByRole('button', { name: 'Transfer' }));
  await user.selectOptions(screen.getByLabelText('To account'), checking.id);
  await user.type(screen.getByLabelText('Amount'), '20');
  await user.click(screen.getByRole('button', { name: 'Save transfer' }));
  expect(screen.getByRole('alert')).toHaveTextContent('different accounts'); expect(mutations).toHaveLength(0);
  await user.selectOptions(screen.getByLabelText('To account'), savings.id);
  await user.click(screen.getByRole('button', { name: 'Save transfer' }));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toEqual({ path: '/api/transfers', body: expect.objectContaining({ fromAccountId: checking.id, toAccountId: savings.id, amount: 20 }) });
});

it('requires confirmation before deleting and retains an audit history entry point', async () => {
  show(); const user = userEvent.setup(); await ready();
  await user.click(screen.getByRole('button', { name: 'More account actions' })); await user.click(screen.getByRole('button', { name: 'Audit history' }));
  expect(await screen.findByText('No ledger changes yet.')).toBeInTheDocument();
  await user.keyboard('{Escape}'); expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  await user.click(screen.getByRole('checkbox', { name: 'Select transaction transaction' })); await user.click(screen.getByRole('button',{name:'More account actions'})); await user.click(screen.getByRole('button', { name: 'Delete selected' }));
  expect(mutations).toHaveLength(0); await user.click(screen.getByRole('button', { name: 'Confirm deletion' }));
  await waitFor(() => expect(mutations).toHaveLength(1)); expect(mutations[0].path).toBe('/api/transactions/transaction/delete');
});

it('sends combined server filters and pagination and locks reconciled rows', async () => {
  register = { ...register, total: 26, items: [{ ...register.items[0], status: 'reconciled' }] };
  show(); const user = userEvent.setup(); await ready();
  expect(screen.getByRole('button', { name: 'Edit Shop 2026-10-21' })).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Reconciled transaction (locked)' })).toBeDisabled();
  await user.type(screen.getByLabelText('Search'), 'Shop');
  await user.click(screen.getByRole('button', { name: 'More account actions' })); await user.selectOptions(screen.getByLabelText('Category'), 'uncategorized');
  await user.selectOptions(screen.getByLabelText('Status'), 'reconciled');
  await waitFor(() => expect(screen.queryByText('Loading transactions…')).not.toBeInTheDocument());
  await user.click(screen.getByRole('button', { name: 'Next' }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([path]) => String(path).includes('page=2') && String(path).includes('search=Shop') && String(path).includes('categoryId=uncategorized') && String(path).includes('status=reconciled'))).toBe(true));
});

it('keeps full account totals when filtering rows and offers View All to reset filters', async () => {
  show('checking'); const user=userEvent.setup(); await ready();
  await user.type(screen.getByLabelText('Search'),'missing');
  expect(await screen.findByText('Some transactions are hidden by filters.')).toBeInTheDocument();
  expect(screen.getByTestId('cleared-balance')).toHaveTextContent('€100.00');
  expect(screen.getByTestId('uncleared-balance')).toHaveTextContent('-€10.00');
  expect(screen.getByTestId('working-balance')).toHaveTextContent('€90.00');
  await user.click(screen.getByRole('button',{name:'View All'}));
  expect(screen.getByLabelText('Search')).toHaveValue('');
});

it('saves the account favorite through the versioned account command', async () => {
  show('checking'); const user=userEvent.setup(); await ready();
  await user.click(screen.getByRole('button',{name:'Favorite account'}));
  await waitFor(() => expect(mutations).toHaveLength(1));
  expect(mutations[0]).toEqual({path:'/api/accounts/checking',body:expect.objectContaining({isFavorite:true,name:'Checking',version:3})});
});

it('applies and reloads server options per account while matching header and row visibility', async () => {
  const props = {onSelectAccount:vi.fn(),onChanged:vi.fn(),onMenu:vi.fn()};
  const page = render(<AccountsPage selectedAccount="checking" {...props}/>); await ready();
  await userEvent.click(screen.getByRole('button',{name:'View options'}));
  await userEvent.click(screen.getByRole('button',{name:'Last Year'}));
  for (const name of ['Show Reconciled Transactions','Show Running Balance','Show Flags','Show Images']) await userEvent.click(screen.getByRole('checkbox',{name}));
  expect(screen.getByRole('columnheader',{name:'BALANCE'})).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:'Apply'})); await ready();
  expect(screen.queryByRole('columnheader',{name:'BALANCE'})).not.toBeInTheDocument();
  expect(screen.queryByRole('columnheader',{name:'Flag'})).not.toBeInTheDocument();
  expect(screen.queryByRole('columnheader',{name:'Attachment'})).not.toBeInTheDocument();
  expect(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}).closest('tr')?.children).toHaveLength(9);
  const requests = vi.mocked(fetch).mock.calls.map(([path])=>String(path));
  expect(requests.some(path=>path.includes('includeReconciled=false') && path.includes('from=') && path.includes('to='))).toBe(true);
  expect(screen.getByTestId('working-balance')).toHaveTextContent('€90.00');
  page.rerender(<AccountsPage selectedAccount="savings" {...props}/>); await ready();
  expect(screen.getByRole('columnheader',{name:'BALANCE'})).toBeInTheDocument();
  page.rerender(<AccountsPage selectedAccount="checking" {...props}/>); await ready();
  await waitFor(()=>expect(screen.queryByRole('columnheader',{name:'BALANCE'})).not.toBeInTheDocument());
  await userEvent.click(screen.getByRole('button',{name:'View options'}));
  expect(screen.getByRole('button',{name:'Last Year'})).toHaveAttribute('aria-pressed','true');
  expect(screen.getByRole('checkbox',{name:'Show Images'})).not.toBeChecked();
  await userEvent.click(screen.getByRole('button',{name:'Cancel'}));
  await userEvent.click(screen.getByRole('button',{name:'View All'})); await ready();
  expect(views['/api/register-views/checking']).toMatchObject({datePreset:'allDates',showReconciled:true,showImages:false});
});

it('does not apply failed saves and reloads conflicting settings before a new save', async () => {
  show('checking'); await ready();
  await userEvent.click(screen.getByRole('button',{name:'View options'}));
  await userEvent.click(screen.getByRole('checkbox',{name:'Show Running Balance'}));
  failViewSave = true;
  await userEvent.click(screen.getByRole('button',{name:'Apply'}));
  expect(await screen.findByRole('alert')).toHaveTextContent('changed in another session');
  expect(screen.getByRole('columnheader',{name:'BALANCE'})).toBeInTheDocument();
  views['/api/register-views/checking'] = {...defaultView,version:5,showFlags:false}; failViewSave=false;
  await userEvent.click(screen.getByRole('button',{name:'Reload saved options'})); await ready();
  await waitFor(()=>expect(screen.queryByRole('columnheader',{name:'Flag'})).not.toBeInTheDocument());
  await userEvent.click(screen.getByRole('button',{name:'View options'}));
  await userEvent.click(screen.getByRole('checkbox',{name:'Show Images'}));
  await userEvent.click(screen.getByRole('button',{name:'Apply'})); await ready();
  expect(views['/api/register-views/checking']).toMatchObject({version:6,showFlags:false,showImages:false});
});

it('blocks default options after a failed preference load and recovers through Reload', async () => {
  failViewRead = true; show('checking');
  expect(await screen.findByRole('alert')).toHaveTextContent('Could not load saved view options');
  expect(screen.getByRole('button',{name:'View options'})).toBeDisabled();
  failViewRead=false;
  await userEvent.click(screen.getByRole('button',{name:'Reload'})); await ready();
  expect(screen.getByRole('button',{name:'View options'})).toBeEnabled();
});

it('inserts Add Transaction directly in the table, cancels without saving, and supports Save and add another',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Add Transaction'}));
 expect(screen.queryByRole('dialog')).not.toBeInTheDocument();expect(screen.getByRole('row',{name:'New transaction'})).toBeInTheDocument();
 expect(screen.getByRole('combobox',{name:'Repeat'})).toHaveValue('never');
 await userEvent.type(screen.getByRole('combobox',{name:'Payee'}),'Cafe');await userEvent.type(screen.getByRole('textbox',{name:'Outflow'}),'12.50');
 await userEvent.click(screen.getByRole('button',{name:'Save and add another'}));
 await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0]).toMatchObject({path:'/api/transactions',body:{amount:-12.5,payee:'Cafe',accountId:'checking'}});
 await waitFor(()=>expect(screen.getByRole('combobox',{name:'Payee'})).toHaveValue(''));
 await userEvent.click(screen.getByRole('button',{name:'Cancel'}));expect(screen.queryByRole('row',{name:'New transaction'})).not.toBeInTheDocument();expect(mutations).toHaveLength(1);
});

it('selects on first row click and edits on the second, retaining failed saves and replaying exactly once',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByText('Lunch'));expect(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}).closest('tr')).toHaveAttribute('aria-selected','true');
 expect(screen.queryByRole('textbox',{name:'Outflow'})).not.toBeInTheDocument();expect(screen.queryByRole('button',{name:'Edit selected'})).not.toBeInTheDocument();expect(screen.queryByRole('button',{name:'Delete selected'})).not.toBeInTheDocument();await userEvent.click(screen.getByText('Lunch'));
 await userEvent.clear(screen.getByRole('textbox',{name:'Outflow'}));await userEvent.type(screen.getByRole('textbox',{name:'Outflow'}),'15');
 failMutation='network';await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(await screen.findByRole('alert')).toHaveTextContent('Connection lost');expect(screen.getByRole('textbox',{name:'Outflow'})).toHaveValue('15');
 await userEvent.click(screen.getByRole('button',{name:'Retry save'}));await waitFor(()=>expect(mutations).toHaveLength(2));expect(mutations[1]).toEqual(mutations[0]);
 await waitFor(()=>expect(screen.queryByRole('row',{name:'Edit transaction inline'})).not.toBeInTheDocument());
});

it('saves future entries as schedules with Repeat and leaves current account totals unchanged',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Add Transaction'}));
 const date=screen.getByRole('textbox',{name:'Date'});await userEvent.clear(date);await userEvent.type(date,'25-10-2036');
 await userEvent.selectOptions(screen.getByRole('combobox',{name:'Repeat'}),'monthly');await userEvent.type(screen.getByRole('combobox',{name:'Payee'}),'Employer');await userEvent.type(screen.getByRole('textbox',{name:'Inflow'}),'2500');
 await userEvent.click(screen.getByRole('button',{name:'Save'}));await waitFor(()=>expect(mutations).toHaveLength(1));
 expect(mutations[0]).toMatchObject({path:'/api/schedules',body:{date:'2036-10-25',repeat:'monthly',amount:2500}});expect(screen.getByTestId('working-balance')).toHaveTextContent('€90.00');
});

it('requires confirmation when editing a reconciled row and sends the explicit acknowledgement',async()=>{
 register={...register,items:[{...register.items[0],status:'reconciled',isLocked:true}]};show('checking');await ready();
 await userEvent.click(screen.getByText('Lunch'));await userEvent.click(screen.getByText('Lunch'));
 await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(screen.getByRole('alert')).toHaveTextContent('Confirm the reconciled');expect(mutations).toHaveLength(0);
 await userEvent.click(screen.getByRole('checkbox',{name:/This entry was reconciled/}));await userEvent.click(screen.getByRole('button',{name:'Save'}));
 await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0].body).toMatchObject({confirmReconciledEdit:true,status:'cleared'});
});

it('tabs from date to payee to category and opens each dropdown on focus',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));
 await userEvent.click(screen.getByRole('textbox',{name:'Date'}));
 expect(screen.getByLabelText('Transaction calendar')).toBeInTheDocument();
 await userEvent.tab();
 expect(screen.getByRole('combobox',{name:'Payee'})).toHaveFocus();
 expect(screen.queryByLabelText('Transaction calendar')).not.toBeInTheDocument();
 expect(screen.getByRole('combobox',{name:'Payee'})).toHaveAttribute('aria-expanded','true');
 await userEvent.tab();
 const category=screen.getByRole('combobox',{name:'Category'});
 expect(category).toHaveFocus();expect(category).toHaveAttribute('aria-expanded','true');
 expect(screen.getByRole('combobox',{name:'Payee'})).toHaveAttribute('aria-expanded','false');
 await userEvent.keyboard('{Escape}');
 expect(category).toHaveFocus();expect(category).toHaveAttribute('aria-expanded','false');
});

it('moves to the next field after choosing a payee and a category',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));
 await userEvent.click(screen.getByRole('combobox',{name:'Payee'}));
 await userEvent.keyboard('{ArrowDown}{Enter}');
 const category=screen.getByRole('combobox',{name:'Category'});
 await waitFor(()=>expect(category).toHaveFocus());expect(category).toHaveAttribute('aria-expanded','true');
 await userEvent.click(screen.getByRole('option',{name:/Ready to Assign/}));
 expect(category).toHaveValue('Inflow: Ready to Assign');
 await waitFor(()=>expect(screen.getByRole('textbox',{name:'Memo'})).toHaveFocus());
});

it('tabs from the payee field to the next editable field and skips inline dropdown chevrons',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));await userEvent.click(screen.getByRole('button',{name:'Edit Shop 2026-10-21'}));
 const payee=screen.getByRole('combobox',{name:'Payee'});payee.focus();
 expect(screen.getByRole('button',{name:'Choose payee'})).toHaveAttribute('tabindex','-1');
 await userEvent.tab();expect(screen.getByRole('combobox',{name:'Category'})).toHaveFocus();
 expect(screen.getByRole('button',{name:'Choose category'})).toHaveAttribute('tabindex','-1');
 await userEvent.tab();expect(screen.getByRole('textbox',{name:'Memo'})).toHaveFocus();
 expect(screen.getByRole('button',{name:'Choose date and repeat'})).toHaveAttribute('tabindex','-1');
});

it('expands scheduled rows, posts Enter Now explicitly, and keeps forecast actions separate',async()=>{
 scheduledItems=[{id:'rule',occurrenceId:'occurrence',repeat:'monthly',startDate:'2036-10-25',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{...register.items[0],id:'occurrence',date:'2036-10-25',payee:'Employer',memo:'Next salary',amount:2500}}];
 const page=render(<AccountsPage selectedAccount="checking" onSelectAccount={vi.fn()} onChanged={vi.fn()} onMenu={vi.fn()}/>);await ready();await screen.findByRole('button',{name:'Edit Employer 2036-10-25'});
 await userEvent.click(screen.getByText('Next salary'));expect(screen.queryByText('1 selected')).not.toBeInTheDocument();await userEvent.click(screen.getByRole('button',{name:'More account actions'}));expect(screen.getByRole('button',{name:'Enter Now'})).toBeEnabled();
 await userEvent.click(screen.getByRole('button',{name:'Enter Now'}));await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0]).toMatchObject({path:'/api/schedules/rule/actions',body:{action:'post',occurrenceId:'occurrence'}});
 await ready();const scheduledToggle=screen.getByRole('button',{name:/Scheduled Transactions/});expect(scheduledToggle).toHaveAttribute('aria-expanded','true');await userEvent.click(scheduledToggle);await waitFor(()=>expect(views['/api/register-views/checking']?.showScheduledTransactions).toBe(false));expect(scheduledToggle).toHaveAttribute('aria-expanded','false');expect(screen.queryByText('Next salary')).not.toBeInTheDocument();
 page.unmount();show('checking');await ready();expect(await screen.findByRole('button',{name:/Scheduled Transactions/})).toHaveAttribute('aria-expanded','false');expect(screen.queryByText('Next salary')).not.toBeInTheDocument();
});

it('hides scheduled transactions for an account without any',async()=>{
 scheduledItems=[{id:'rule',occurrenceId:'occurrence',repeat:'monthly',startDate:'2036-10-25',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{...register.items[0],id:'occurrence',accountId:'savings',date:'2036-10-25'}}];
 show('checking');await ready();
 await waitFor(()=>expect(vi.mocked(fetch).mock.calls.some(([path])=>String(path).startsWith('/api/schedules?')&&String(path).includes('accountId=checking'))).toBe(true));
 expect(screen.queryByRole('button',{name:/Scheduled Transactions/})).not.toBeInTheDocument();
});

it('shows the approval banner and review i only for unapproved entries and sends explicit review/filter commands',async()=>{
 register={...register,accounts:{...register.accounts,accounts:[{...checking,unapprovedCount:1,attentionCount:1}]},items:[{...register.items[0],isApproved:false,needsCategory:false}]};
 show('checking');await ready();expect(screen.getByText(/1 new transaction to approve or categorize/)).toBeInTheDocument();
 await userEvent.click(screen.getByRole('button',{name:'Review unapproved transaction'}));await userEvent.click(screen.getByRole('button',{name:'Approve Transaction'}));await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0]).toMatchObject({path:'/api/transactions/transaction/review',body:{action:'approve'}});
 await ready();await userEvent.click(screen.getByRole('button',{name:'View'}));
 await waitFor(()=>expect(vi.mocked(fetch).mock.calls.some(([path])=>String(path).includes('needsAttention=true'))).toBe(true));
});

it('shows YNAB-style split subrows, calculates the remainder and validates exact signed allocations',async()=>{
 show('checking');await ready();await userEvent.click(screen.getByRole('button',{name:'Add Transaction'}));await userEvent.type(screen.getByRole('textbox',{name:'Outflow'}),'20');await userEvent.click(screen.getByRole('combobox',{name:'Category'}));await userEvent.click(screen.getByRole('button',{name:'Split'}));
 expect(screen.getByRole('row',{name:'Split 1'})).toBeInTheDocument();expect(screen.getByRole('button',{name:/Add another split/})).toBeInTheDocument();
 await userEvent.click(screen.getByRole('combobox',{name:'Split 1 category'}));await userEvent.click(screen.getByRole('option',{name:/^Food/}));await userEvent.click(screen.getByRole('combobox',{name:'Split 2 category'}));await userEvent.click(screen.getByRole('option',{name:/^Food/}));
 await userEvent.click(screen.getByRole('combobox',{name:'Split 1 payee'}));await userEvent.click(screen.getByRole('option',{name:'Shop'}));expect(screen.getByRole('combobox',{name:'Split 1 payee'})).toHaveValue('Shop');
 await userEvent.type(screen.getByLabelText('Split 1 outflow'),'10');await userEvent.clear(screen.getByLabelText('Split 1 payee'));await userEvent.type(screen.getByLabelText('Split 1 payee'),'Market');await userEvent.type(screen.getByLabelText('Split 1 memo'),'Produce');await userEvent.type(screen.getByLabelText('Split 2 outflow'),'5');
 expect(screen.getByText('Amount remaining to assign:').nextElementSibling).toHaveTextContent('5.00');
 await userEvent.click(screen.getByRole('button',{name:'Save'}));expect(screen.getByRole('alert')).toHaveTextContent('Split amounts must equal');expect(mutations).toHaveLength(0);
 await userEvent.clear(screen.getByLabelText('Split 2 outflow'));await userEvent.type(screen.getByLabelText('Split 2 outflow'),'10');await userEvent.click(screen.getByRole('button',{name:'Save'}));await waitFor(()=>expect(mutations).toHaveLength(1));expect(mutations[0].body).toMatchObject({amount:-20,splits:[{categoryId:'food',amount:-10,payee:'Market',memo:'Produce'},{categoryId:'food',amount:-10,payee:'',memo:''}]});
});

it('trades width only with the right neighbour and keeps both columns at least 24px', () => {
  const order = registerColumnOrder({ showFlags: false, showImages: false, showRunningBalance: true });
  expect(order).toEqual(['check', 'info', 'date', 'payee', 'category', 'memo', 'outflow', 'inflow', 'balance', 'status']);
  const widths = { ...defaultColumnWidths };
  expect(resizeColumnPair(widths, order, 'memo', -40)).toEqual({ ...widths, memo: 140, outflow: 160 });
  expect(resizeColumnPair(widths, order, 'outflow', 500)).toEqual({ ...widths, outflow: 216, inflow: 24 });
  expect(resizeColumnPair(widths, order, 'date', -500)).toEqual({ ...widths, date: 24, payee: 306 });
  expect(resizeColumnPair(widths, order, 'status', 30)).toBe(widths);
});
