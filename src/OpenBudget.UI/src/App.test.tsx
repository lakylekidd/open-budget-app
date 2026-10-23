import { fireEvent, render,screen,waitFor,within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach,expect,it,vi } from 'vitest';
import App from './App';

type Month={month:string;note:string;version:number;fundsAdded:number;openingReadyToAssign:number;ledgerIncome:number;readyToAssign:number;groups:{id:string;name:string;sortOrder:number;isArchived:boolean;categories:{id:string;name:string;assigned:number;activity:number;available:number;target:number;icon:string;snoozed:boolean;sortOrder:number;isArchived:boolean}[]}[]};
const budget=(month:string,assigned=0,activity=0,available=0):Month=>({month,note:'',version:1,fundsAdded:0,openingReadyToAssign:500,ledgerIncome:0,readyToAssign:500-assigned,groups:[{id:'home',name:'Home',sortOrder:0,isArchived:false,categories:[{id:'utilities',name:'Electricity',assigned,activity,available,target:0,icon:'',snoozed:false,sortOrder:0,isArchived:false}]}]});
const response=(data:unknown)=>new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}});
let months:Record<string,Month>;let ownedBudgets:{id:string;name:string;currency:string;version:number;isArchived:boolean;createdAt:string}[];let writes:Record<string,unknown>[];let releaseSave:(()=>void)|undefined;let holdSave:boolean;let focusRows:Record<string,unknown>[];let focusAccounts:Record<string,unknown>[];let scheduleRows:Record<string,unknown>[];
beforeEach(()=>{
  window.history.replaceState(null, '', '/');
  window.localStorage.clear();ownedBudgets=[{id:'default',name:'Test budget',currency:'EUR',version:1,isArchived:false,createdAt:'2026-01-01T00:00:00Z'}];months={'2026-08':budget('2026-08',50,-35,15),'2026-09':budget('2026-09',0,0,15),'2026-10':budget('2026-10',0,0,15),'2026-11':budget('2026-11',0,0,15)};writes=[];holdSave=false;releaseSave=undefined;focusRows=[];focusAccounts=[];scheduleRows=[];
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>{
    if(path==='/api/accounts')return response({version:1,accounts:focusAccounts});
    if(path==='/api/budgets'&&init?.method==='POST')return response({id:'new-budget',name:JSON.parse(String(init.body)).name,currency:'EUR',version:1,isArchived:false,createdAt:'2026-10-01T00:00:00Z'});
    if(path==='/api/budgets')return response({budgets:ownedBudgets,limit:{membership:'pro',membershipName:'Pro',maxActiveBudgets:null,activeBudgets:ownedBudgets.length,budgetLimitOverride:null}});
    if(path.startsWith('/api/long-term-planning/')){
      const year=Number(path.split('/').at(-1));
      return response({year,version:1,hasPlanningData:false,months:Array.from({length:12},(_,index)=>({month:`${year}-${String(index+1).padStart(2,'0')}`,expectedIncome:0,hasExpectedIncome:false,expectedExpenses:0,remaining:0,hasActualData:false})),groups:[]});
    }
    if(path.startsWith('/api/schedules?'))return response({version:1,page:1,pageSize:100,total:scheduleRows.length,items:scheduleRows});
    if(path.startsWith('/api/register-views/'))return response({version:1,datePreset:'all',fromMonth:null,toMonth:null,showReconciled:false,showRunningBalance:false,showFlags:false,showImages:false});
    if(path==='/api/flags')return response({version:1,flags:[]});
    if(path.startsWith('/api/transactions?')){
      const query=new URLSearchParams(path.slice(path.indexOf('?')+1));
      const items=query.has('accountId')?focusRows:focusRows.filter(item=>(item as {categoryId?:string}).categoryId===query.get('categoryId'));
      return response({version:1,page:Number(query.get('page')??1),pageSize:Number(query.get('pageSize')??100),total:items.length,items,accounts:{version:1,accounts:focusAccounts}});
    }
    if(path.startsWith('/api/budgets/')&&path.includes('/months/')){
      const month=path.slice(path.lastIndexOf('/months/')+'/months/'.length);
      if(init?.method==='PUT'){
        const request=JSON.parse(String(init.body));writes.push(request);
        if(holdSave)await new Promise<void>(resolve=>{releaseSave=resolve;});
        const amount=request.groups.flatMap((group:{categories:{id:string;assigned:number}[]})=>group.categories).find((category:{id:string})=>category.id==='utilities')?.assigned??0;
        if(month==='2026-09'&&amount===50){months[month]=budget(month,50,0,65);months['2026-10']=budget('2026-10',0,0,65);}
        else if(month==='2026-08'&&amount===80){months[month]=budget(month,80,-35,45);months['2026-09']=budget('2026-09',50,0,95);months['2026-10']=budget('2026-10',0,0,95);}
        else {months[month]=budget(month,amount,0,amount);months['2026-11']=budget('2026-11',0,0,amount);}
        months[month].version++;
      }
      return response(months[month]);
    }
    if(path.startsWith('/api/budget/')){
      const month=path.slice('/api/budget/'.length);
      return response(months[month]);
    }
    throw new Error('Unexpected request '+path);
  }));
});
async function saved(){await screen.findByText('Plan saved');}
async function next(direction:'Previous'|'Next',month:string){await userEvent.click(screen.getByRole('button',{name:direction+' month'}));await screen.findByRole('button',{name:month});await saved();}
function row(){return screen.getByRole('button',{name:'Edit assigned for Electricity'}).closest('.category-row')!;}
async function assign(value:string){await userEvent.click(screen.getByRole('button',{name:'Edit assigned for Electricity'}));const input=screen.getByRole('textbox',{name:'Assigned to Electricity'});await userEvent.clear(input);await userEvent.type(input,value);await userEvent.keyboard('{Enter}');}

it('shows the all-money-assigned summary at zero and restores the income action in a funded month',async()=>{
  months['2026-10'].readyToAssign=0;
  render(<App/>);await saved();
  expect(screen.getByRole('combobox',{name:'Active budget'})).toHaveValue('default');
  expect(screen.getByRole('button',{name:'Rename budget'}).parentElement?.querySelector('img')).toHaveAttribute('src','/openbudget-logo.svg');
  const summary=within(screen.getByRole('region',{name:'Ready to assign'}));
  expect(summary.getByText('€0.00')).toBeInTheDocument();
  expect(summary.getByText('All Money Assigned')).toBeInTheDocument();
  expect(summary.queryByRole('button',{name:'Record income'})).not.toBeInTheDocument();
  await next('Next','Nov 2026');
  expect(summary.queryByText('All Money Assigned')).not.toBeInTheDocument();
  expect(summary.getByRole('button',{name:'Record income'})).toBeEnabled();
});

it('switches between owned budgets and remembers the selected budget',async()=>{
  ownedBudgets.push({id:'second',name:'Travel',currency:'EUR',version:1,isArchived:false,createdAt:'2026-02-01T00:00:00Z'});
  render(<App/>);await saved();
  await userEvent.selectOptions(screen.getByRole('combobox',{name:'Active budget'}),'second');
  await waitFor(()=>expect(screen.getByRole('combobox',{name:'Active budget'})).toHaveValue('second'));
  await waitFor(()=>expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/second/months/2026-10',expect.objectContaining({signal:expect.any(AbortSignal)})));
  expect(window.localStorage.getItem('openbudget.activeBudgetId')).toBe('second');
});

it('restores the remembered owned budget on page load',async()=>{
  ownedBudgets.push({id:'second',name:'Travel',currency:'EUR',version:1,isArchived:false,createdAt:'2026-02-01T00:00:00Z'});
  window.localStorage.setItem('openbudget.activeBudgetId','second');
  render(<App/>);await saved();
  expect(screen.getByRole('combobox',{name:'Active budget'})).toHaveValue('second');
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/second/months/2026-10',expect.objectContaining({signal:expect.any(AbortSignal)}));
});

it('creates a budget and selects it as the active plan',async()=>{
  vi.spyOn(window,'prompt').mockReturnValue('Travel');
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Create budget'}));
  await waitFor(()=>expect(screen.getByRole('combobox',{name:'Active budget'})).toHaveValue('new-budget'));
  expect(window.localStorage.getItem('openbudget.activeBudgetId')).toBe('new-budget');
  await waitFor(()=>expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/new-budget/months/2026-10',expect.objectContaining({signal:expect.any(AbortSignal)})));
});

it('selects the entire existing assignment when its editor opens',async()=>{
  months['2026-10']=budget('2026-10',1245.67,0,1245.67);
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Edit assigned for Electricity'}));
  const input=screen.getByRole('textbox',{name:'Assigned to Electricity'}) as HTMLInputElement;
  expect(input).toHaveFocus();
  expect(input.selectionStart).toBe(0);
  expect(input.selectionEnd).toBe(input.value.length);
});

it('shows actual ledger income in the monthly summary instead of a fixed estimate',async()=>{
  months['2026-10'].ledgerIncome=2000;
  months['2026-10'].readyToAssign=2000;
  render(<App/>);await saved();
  const incomeSummary=screen.getByText('Expected Income').parentElement!;
  expect(incomeSummary).toHaveTextContent('€2,000.00');
  expect(incomeSummary).not.toHaveTextContent('€12,500.00');
});

it.each([{amount:0.01,label:'Ready to assign'},{amount:-0.01,label:'Assigned more than available'}])('retains the nonzero summary for $amount euros',async({amount,label})=>{
  months['2026-10'].readyToAssign=amount;
  render(<App/>);await saved();
  const summary=within(screen.getByRole('region',{name:'Ready to assign'}));
  expect(summary.getByText(label)).toBeInTheDocument();
  expect(summary.queryByText('All Money Assigned')).not.toBeInTheDocument();
  expect(summary.getByRole('button',{name:'Record income'})).toBeEnabled();
});

it('shows September carry, saves fifty more, and reloads corrections made to August',async()=>{
  render(<App/>);await saved();await next('Previous','Sept 2026');expect(row()).toHaveTextContent('€15.00');await assign('50');await saved();expect(row()).toHaveTextContent('€65.00');
  await next('Next','Oct 2026');expect(row()).toHaveTextContent('€65.00');await next('Previous','Sept 2026');await next('Previous','Aug 2026');expect(row()).toHaveTextContent('€15.00');
  await assign('80');await saved();expect(row()).toHaveTextContent('€45.00');await next('Next','Sept 2026');expect(row()).toHaveTextContent('€95.00');expect(screen.getByRole('button',{name:'Edit assigned for Electricity'})).toHaveTextContent('€50.00');
  expect(writes).toHaveLength(2);expect(writes[0]).toMatchObject({month:'2026-09'});expect(writes[1]).toMatchObject({month:'2026-08'});
});

it('does not switch months or lose an assignment while its save is pending',async()=>{
  holdSave=true;render(<App/>);await saved();await assign('50');await userEvent.click(screen.getByRole('button',{name:'Next month'}));expect(screen.getByRole('button',{name:'Oct 2026'})).toBeInTheDocument();expect(screen.queryByRole('button',{name:'Nov 2026'})).not.toBeInTheDocument();
  await waitFor(()=>expect(releaseSave).toBeDefined());releaseSave!();await saved();await next('Next','Nov 2026');expect(row()).toHaveTextContent('€50.00');expect(writes).toHaveLength(1);expect(screen.getByRole('button',{name:/Undo/})).toBeDisabled();
});

it('keeps invalid assigned input visible and never sends a silent zero assignment',async()=>{
  render(<App/>);await saved();await assign('bad50');expect(screen.getByRole('alert')).toHaveTextContent('valid amount');expect(screen.getByRole('textbox',{name:'Assigned to Electricity'})).toHaveAttribute('aria-invalid','true');expect(writes).toHaveLength(0);
  await userEvent.click(screen.getByRole('button',{name:'Next month'}));expect(screen.getByRole('button',{name:'Oct 2026'})).toBeInTheDocument();await userEvent.keyboard('{Escape}');
});

it('does not allow editing stale category assignments while the requested month is loading',async()=>{
  const original=vi.mocked(fetch).getMockImplementation()!;let complete:((response:Response)=>void)|undefined;
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>path==='/api/budgets/default/months/2026-10'&&!init?.method?new Promise<Response>(resolve=>{complete=resolve;}):original(path,init)));
  render(<App/>);expect(screen.queryByRole('button',{name:'Edit assigned for Electricity & water'})).not.toBeInTheDocument();await waitFor(()=>expect(complete).toBeDefined());complete!(response(months['2026-10']));await saved();expect(screen.getByRole('button',{name:'Edit assigned for Electricity'})).toBeEnabled();
});

it('opens category activity and takes a reconciled transaction to its account despite saved filters',async()=>{
  const accountId='11111111-1111-4111-8111-111111111111';
  const transactionId='22222222-2222-4222-8222-222222222222';
  const account={id:accountId,name:'Household',isClosed:false,balance:15,clearedBalance:0,unclearedBalance:15,accountType:'checking',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0};
  const entry={id:transactionId,accountId,date:'2026-10-27',amount:-35,payee:'Health insurer',memo:'Monthly premium',categoryId:'utilities',status:'reconciled',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:15,transferAccountName:null,categoryName:'Home: Electricity',isLocked:true};
  focusAccounts=[account];focusRows=[entry];
  months['2026-10']=budget('2026-10',0,-35,-35);
  render(<App/>);await saved();

  await userEvent.click(screen.getByRole('button',{name:'View transactions for Electricity activity'}));
  await screen.findByText('Health insurer');
  await userEvent.click(screen.getByRole('button',{name:'Open Health insurer in Household account'}));

  await screen.findByText(/Showing the transaction selected from the Plan screen/);
  expect(await screen.findByRole('checkbox',{name:`Select transaction ${transactionId}`})).toBeChecked();
  const requests=vi.mocked(fetch).mock.calls.map(([path])=>String(path)).filter(path=>path.startsWith('/api/transactions?'));
  expect(requests.some(path=>{const query=new URL(path,'http://localhost').searchParams;return query.get('accountId')===accountId&&query.get('includeReconciled')==='true'&&query.get('from')==='2026-10-27'&&query.get('to')==='2026-10-27'&&!query.has('categoryId')&&!query.has('status');})).toBe(true);
});

it('shows projected shortfall in category details and lists the scheduled transaction',async()=>{
  const accountId='11111111-1111-4111-8111-111111111111';
  focusAccounts=[{id:accountId,name:'Household',isClosed:false,balance:15,clearedBalance:0,unclearedBalance:15,accountType:'checking',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0}];
  const schedule={id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId,date:'2026-10-27',amount:-49.9,payee:'Gym',memo:'Membership',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}};
  scheduleRows=[schedule];
  months['2026-10']=budget('2026-10',20.6,0,20.6);
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:/^Electricity$/}));
  expect(screen.getByText('Available After Upcoming')).toBeInTheDocument();
  expect(screen.getByText(/-€29\.30/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:/1 Upcoming Transaction/}));
  expect(await screen.findByRole('heading',{name:'Upcoming activity · Electricity'})).toBeInTheDocument();
  expect(screen.getByText('Gym')).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:'Open Gym in Household account'}));
  await screen.findByText(/Showing the transaction selected from the Plan screen/);
  expect(await screen.findByRole('row',{name:/Gym/})).toHaveClass('register-row-selected');
  const scheduleRequests=vi.mocked(fetch).mock.calls.map(([path])=>String(path)).filter(path=>path.startsWith('/api/schedules?'));
  expect(scheduleRequests.some(path=>{const query=new URL(path,'http://localhost').searchParams;return query.get('accountId')===accountId&&query.get('from')==='2026-10-27'&&query.get('to')==='2026-10-27';})).toBe(true);
});

it('highlights Available when a category has upcoming transactions, even without a projected shortfall',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-10-27',amount:-10,payee:'Gym',memo:'Membership',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  months['2026-10']=budget('2026-10',20,0,20);
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell')!;
  expect(available).toHaveClass('underfunded');
  expect(available).not.toHaveClass('positive');
});

it('adds a category from the group popover and saves it to the month',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Add category to Home'}));
  await userEvent.type(screen.getByRole('textbox',{name:'New category'}),'Dental care');
  await userEvent.click(screen.getByRole('button',{name:'OK'}));
  expect(screen.getByText('Category added')).toBeInTheDocument();
  await waitFor(()=>expect(writes).toHaveLength(1));
  expect(writes.at(-1)?.groups).toEqual(expect.arrayContaining([expect.objectContaining({id:'home',categories:expect.arrayContaining([expect.objectContaining({name:'Dental care',assigned:0})])})]));
});

it('drags a category into another group and saves the new membership',async()=>{
  const month=months['2026-10'];
  month.groups.push({id:'other',name:'Other',sortOrder:1,isArchived:false,categories:[{id:'other-category',name:'Other category',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:0,isArchived:false}]});
  render(<App/>);await saved();
  const source=screen.getByRole('button',{name:/^Electricity$/}).closest('.category-row')!;
  const target=screen.getByRole('button',{name:/^Other$/}).closest('.category-group')!;
  const dataTransfer={effectAllowed:'',setData:vi.fn()};
  fireEvent.dragStart(source,{dataTransfer});fireEvent.drop(target,{dataTransfer});
  await waitFor(()=>expect(writes).toHaveLength(1));
  const submitted= writes[0] as {groups:{id:string;categories:{id:string}[]}[]};
  expect(submitted.groups.find(group=>group.id==='home')?.categories.some(category=>category.id==='utilities')).toBe(false);
  expect(submitted.groups.find(group=>group.id==='other')?.categories.some(category=>category.id==='utilities')).toBe(true);
});

it('opens Long-Term Planning from the main navigation',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Long-Term Planning'}));
  expect(await screen.findByRole('heading',{name:'Long-Term Planning'})).toBeInTheDocument();
  expect(await screen.findByText('Add budget categories in Plan to start long-term planning.')).toBeInTheDocument();
  const year=new Date().getFullYear();
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(`/api/long-term-planning/${year}`,expect.objectContaining({signal:expect.any(AbortSignal)}));
});
