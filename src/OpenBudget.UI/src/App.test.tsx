import { fireEvent, render,screen,waitFor,within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach,expect,it,vi } from 'vitest';
import App from './App';

type Month={month:string;note:string;version:number;fundsAdded:number;openingReadyToAssign:number;ledgerIncome:number;readyToAssign:number;groups:{id:string;name:string;sortOrder:number;isArchived:boolean;categories:{id:string;name:string;assigned:number;activity:number;available:number;target:number;icon:string;snoozed:boolean;sortOrder:number;isArchived:boolean}[]}[]};
const budget=(month:string,assigned=0,activity=0,available=0):Month=>({month,note:'',version:1,fundsAdded:0,openingReadyToAssign:500,ledgerIncome:0,readyToAssign:500-assigned,groups:[{id:'home',name:'Home',sortOrder:0,isArchived:false,categories:[{id:'utilities',name:'Electricity',assigned,activity,available,target:0,icon:'',snoozed:false,sortOrder:0,isArchived:false}]}]});
const response=(data:unknown)=>new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}});
let months:Record<string,Month>;let ownedBudgets:{id:string;name:string;currency:string;version:number;isArchived:boolean;createdAt:string}[];let archivedBudgets:{id:string;name:string;currency:string;version:number;isArchived:boolean;createdAt:string}[];let writes:Record<string,unknown>[];let releaseSave:(()=>void)|undefined;let holdSave:boolean;let focusRows:Record<string,unknown>[];let focusAccounts:Record<string,unknown>[];let scheduleRows:Record<string,unknown>[];
let planningHistory:Record<number,unknown>;
let sharing:{manager:Record<string,unknown>;members:Record<string,unknown>[];invitations:Record<string,unknown>[];canManage:boolean;invitesRemaining:number};let recentActions:Record<string,unknown>[];
const planningYear=(year:number,actuals:Record<string,{assigned:number|null;spent:number|null}>)=>({year,version:1,hasPlanningData:false,months:[],groups:[{id:'home',name:'Home',sortOrder:0,isArchived:false,categories:[{id:'utilities',name:'Electricity',sortOrder:0,isArchived:false,months:Array.from({length:12},(_,index)=>{const month=`${year}-${String(index+1).padStart(2,'0')}`;const actual=actuals[month];return {month,plannedBudgetOverride:null,plannedBudgetBeforeMonth:0,plannedBudget:0,plannedExpenses:0,plannedAvailable:0,actualAssigned:actual?.assigned??null,actualExpenses:actual?.spent??null,actualAvailable:null};})}]}]});
beforeEach(()=>{
  window.history.replaceState(null, '', '/');planningHistory={};
  window.localStorage.clear();ownedBudgets=[{id:'default',name:'Test budget',currency:'EUR',version:1,isArchived:false,createdAt:'2026-01-01T00:00:00Z'}];archivedBudgets=[];months={'2026-08':budget('2026-08',50,-35,15),'2026-09':budget('2026-09',0,0,15),'2026-10':budget('2026-10',0,0,15),'2026-11':budget('2026-11',0,0,15)};writes=[];holdSave=false;releaseSave=undefined;focusRows=[];focusAccounts=[];scheduleRows=[];
  sharing={manager:{id:'u1',name:'alice',email:'alice@example.test',role:'manager',joinedAt:'2026-01-01T00:00:00Z'},members:[],invitations:[],canManage:true,invitesRemaining:4};recentActions=[];
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>{
    if(path==='/api/auth/session')return response({id:'u1',userName:'alice',email:'alice@example.test',isAdministrator:true});
    if(path==='/api/accounts')return response({version:1,accounts:focusAccounts});
    if(path==='/api/budgets'&&init?.method==='POST')return response({id:'new-budget',name:JSON.parse(String(init.body)).name,currency:'EUR',version:1,isArchived:false,createdAt:'2026-10-01T00:00:00Z'});
    if(path.startsWith('/api/budgets/')&&init?.method==='DELETE'){const id=path.split('/')[3];ownedBudgets=ownedBudgets.filter(item=>item.id!==id);archivedBudgets=archivedBudgets.filter(item=>item.id!==id);return new Response(null,{status:204});}
    if(path.startsWith('/api/budgets/')&&path.endsWith('/archive')){const id=path.split('/')[3];const budget=ownedBudgets.find(item=>item.id===id);if(budget){ownedBudgets=ownedBudgets.filter(item=>item.id!==id);archivedBudgets.push({...budget,isArchived:true,version:budget.version+1});}return new Response(null,{status:204});}
    if(path.startsWith('/api/budgets/')&&path.endsWith('/restore')){const id=path.split('/')[3];const budget=archivedBudgets.find(item=>item.id===id);if(budget){archivedBudgets=archivedBudgets.filter(item=>item.id!==id);ownedBudgets.push({...budget,isArchived:false,version:budget.version+1});}return new Response(null,{status:204});}
    if(path==='/api/budgets?includeArchived=true')return response({budgets:[...ownedBudgets,...archivedBudgets],limit:{membership:'pro',membershipName:'Pro',maxActiveBudgets:null,activeBudgets:ownedBudgets.length,budgetLimitOverride:null}});
    if(path==='/api/budgets/default/sharing')return response(sharing);
    if(path==='/api/budgets/default/invitations'&&init?.method==='POST'){const email=JSON.parse(String(init.body)).email;sharing.invitations.push({id:'invite-1',email,createdAt:'2026-10-24T12:00:00Z'});sharing.invitesRemaining--;return new Response(JSON.stringify({message:'Invitation saved. It will be accepted when this email signs in.'}),{status:202,headers:{'Content-Type':'application/json'}});}
    if(path.startsWith('/api/budgets/default/plan-actions')){const kind=new URL(`http://local${path}`).searchParams.get('kind');return response(kind?recentActions.filter(action=>kind==='assigned'?action.kind==='assigned'||action.kind==='removed':action.kind===kind):recentActions);}
    if(path.startsWith('/api/long-term-planning/')){
      const year=Number(path.split('/').at(-1));
      return response(planningHistory[year]??{year,version:1,hasPlanningData:false,months:Array.from({length:12},(_,index)=>({month:`${year}-${String(index+1).padStart(2,'0')}`,expectedIncome:0,hasExpectedIncome:false,expectedExpenses:0,remaining:0,hasActualData:false})),groups:[]});
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
function row(){return screen.getByRole('button',{name:'Edit assigned for Electricity'}).closest('.category-row') as HTMLElement;}
async function assign(value:string){await userEvent.click(screen.getByRole('button',{name:'Edit assigned for Electricity'}));const input=screen.getByRole('textbox',{name:'Assigned to Electricity'});await userEvent.clear(input);await userEvent.type(input,value);await userEvent.keyboard('{Enter}');}

it('shows the all-money-assigned summary at zero and offers Assign in a funded month',async()=>{
  months['2026-10'].readyToAssign=0;
  render(<App/>);await saved();
  expect(screen.getByRole('button',{name:'Budget menu'})).toHaveTextContent('Test budget');
  expect(screen.getByRole('button',{name:'Budget menu'}).querySelector('img')).toHaveAttribute('src','/openbudget-logo.svg');
  expect(await screen.findByText('alice@example.test')).toBeInTheDocument();
  expect(screen.getByRole('button',{name:'Budget menu'})).not.toHaveTextContent('EUR · 1 budget');
  const summary=within(screen.getByRole('region',{name:'Ready to assign'}));
  expect(summary.getByText('€0.00')).toBeInTheDocument();
  expect(summary.getByText('All Money Assigned')).toBeInTheDocument();
  expect(summary.queryByRole('button',{name:'Record income'})).not.toBeInTheDocument();
  await next('Next','Nov 2026');
  expect(summary.queryByText('All Money Assigned')).not.toBeInTheDocument();
  expect(summary.getByRole('button',{name:/Assign/})).toBeEnabled();
});

it('does not render the promotional card in the Plan summary panel',async()=>{
  render(<App/>);await saved();
  expect(screen.queryByText('For You')).not.toBeInTheDocument();
  expect(screen.queryByRole('button',{name:'Read the Blog'})).not.toBeInTheDocument();
});

it('shows actor-attributed Plan history in Recent Moves',async()=>{
  recentActions=[{id:'move-1',month:'2026-10',kind:'moved',amount:50,fromCategoryName:'Rent',toCategoryName:'Groceries',actorUserId:'u2',actorName:'Sam',actorEmail:'sam@example.test',createdAt:'2026-10-24T12:00:00Z'}];
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:/Recent Moves/}));
  const dialog=await screen.findByRole('dialog',{name:'Recent Moves'});
  expect(within(dialog).getByText('Sam moved €50.00')).toBeInTheDocument();
  expect(within(dialog).getByText(/from/)).toHaveTextContent('from Rent to Groceries · 2026-10');
  await userEvent.click(within(dialog).getByRole('tab',{name:'Moved'}));
  await waitFor(()=>expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/default/plan-actions?kind=moved',expect.objectContaining({signal:expect.any(AbortSignal)})));
});

it('opens Budget Together from the logo menu and invites a member',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Budget menu'}));
  await userEvent.click(screen.getByRole('menuitem',{name:/Budget Together/}));
  expect(await screen.findByRole('region',{name:'OpenBudget Together'})).toBeInTheDocument();
  expect(screen.getByText('alice (you)')).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText('Email address'),'friend@example.test');
  await userEvent.click(screen.getByRole('button',{name:'Invite Member'}));
  expect(await screen.findByText('friend@example.test')).toBeInTheDocument();
  expect(screen.getByText(/Invitation saved/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:/Back to Plan/}));
  await saved();
});

it('rotates the collapse-all chevron with the group state',async()=>{
  render(<App/>);await saved();
  const collapseAll=screen.getByRole('button',{name:'Collapse all'});
  expect(collapseAll).toHaveAttribute('aria-expanded','true');
  expect(collapseAll.querySelector('svg')).toHaveAttribute('width','18');
  expect(screen.getByRole('button',{name:'Toggle Home'}).querySelector('svg')).toHaveAttribute('width','18');
  expect(collapseAll.querySelector('.group-chevron')).not.toHaveClass('closed');
  await userEvent.click(collapseAll);
  const expandAll=screen.getByRole('button',{name:'Expand all'});
  expect(expandAll).toHaveAttribute('aria-expanded','false');
  expect(expandAll.querySelector('.group-chevron')).toHaveClass('closed');
  await userEvent.click(expandAll);
  expect(screen.getByRole('button',{name:'Collapse all'}).querySelector('.group-chevron')).not.toHaveClass('closed');
});

it('uses compact category rows when progress bars are hidden',async()=>{
  render(<App/>);await saved();
  const table=document.querySelector('.budget-table') as HTMLElement;
  expect(table).not.toHaveClass('compact');
  await userEvent.click(screen.getByRole('button',{name:'Progress Bars Off'}));
  expect(table).toHaveClass('compact');
  await userEvent.click(screen.getByRole('button',{name:'Progress Bars On'}));
  expect(table).not.toHaveClass('compact');
});

it('switches between owned budgets and remembers the selected budget',async()=>{
  ownedBudgets.push({id:'second',name:'Travel',currency:'EUR',version:1,isArchived:false,createdAt:'2026-02-01T00:00:00Z'});
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Budget menu'}));
  await userEvent.click(screen.getByRole('menuitem',{name:/Open Plan/}));
  expect(screen.getByRole('menu',{name:'Budget menu options'})).toBeInTheDocument();
  const recent=screen.getByRole('menu',{name:'Recent Plans'});
  expect(within(recent).getByRole('menuitem',{name:/View All Plans/})).toBeInTheDocument();
  await userEvent.click(within(recent).getByRole('menuitem',{name:/Travel/}));
  await waitFor(()=>expect(screen.getByRole('button',{name:'Budget menu'})).toHaveTextContent('Travel'));
  await waitFor(()=>expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/second/months/2026-10',expect.objectContaining({signal:expect.any(AbortSignal)})));
  expect(window.localStorage.getItem('openbudget.activeBudgetId')).toBe('second');
  expect(document.cookie).toContain('openbudget.activeBudgetId=second');
});

it('shows recent plans on hover and hides them after leaving the item and panel',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Budget menu'}));
  const openPlan=screen.getByRole('menuitem',{name:/Open Plan/});
  fireEvent.mouseEnter(openPlan);
  const panel=screen.getByRole('menu',{name:'Recent Plans'});
  expect(panel).toHaveClass('is-open');
  fireEvent.mouseLeave(openPlan);
  fireEvent.mouseEnter(panel);
  expect(panel).toHaveClass('is-open');
  fireEvent.mouseLeave(panel);
  await waitFor(()=>expect(panel).not.toHaveClass('is-open'));
});

it('restores the remembered owned budget on page load',async()=>{
  ownedBudgets.push({id:'second',name:'Travel',currency:'EUR',version:1,isArchived:false,createdAt:'2026-02-01T00:00:00Z'});
  window.localStorage.setItem('openbudget.activeBudgetId','second');
  render(<App/>);await saved();
  expect(screen.getByRole('button',{name:'Budget menu'})).toHaveTextContent('Travel');
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/budgets/second/months/2026-10',expect.objectContaining({signal:expect.any(AbortSignal)}));
  expect(document.cookie).toContain('openbudget.activeBudgetId=second');
});

it('omits budget management controls from the sidebar menu',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Budget menu'}));
  expect(await screen.findByRole('menuitem',{name:/Admin/})).toHaveAttribute('href','/admin');
  expect(screen.getByRole('menuitem',{name:/Sign Out/})).toBeInTheDocument();
  expect(screen.getByRole('menuitem',{name:/New Plan/})).toBeInTheDocument();
  expect(screen.getByRole('menuitem',{name:/Open Plan/})).toBeInTheDocument();
  for (const name of ['Rename budget','Create budget','Archive budget','Delete budget','Archived budget','Restore']) {
    expect(screen.queryByRole('button',{name})).not.toBeInTheDocument();
    expect(screen.queryByRole('menuitem',{name})).not.toBeInTheDocument();
  }
});

it('collapses and expands cash accounts while retaining the total',async()=>{
  focusAccounts=[{id:'cash-1',name:'Salary',isClosed:false,balance:3459.08,clearedBalance:3459.08,unclearedBalance:0,accountType:'checking',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0}];
  render(<App/>);await saved();
  const heading=screen.getByRole('button',{name:/CASH/});
  expect(heading).toHaveAttribute('aria-expanded','true');
  expect(heading).toHaveTextContent('€3,459.08');
  expect(screen.getByRole('button',{name:/Salary €3,459.08/})).toBeVisible();
  await userEvent.click(heading);
  expect(heading).toHaveAttribute('aria-expanded','false');
  expect(document.getElementById('cash-account-list')).not.toBeVisible();
  expect(screen.getByRole('button',{name:'Add Account'})).toBeVisible();
  expect(document.getElementById('cash-account-list')).not.toContainElement(screen.getByRole('button',{name:'Add Account'}));
  expect(heading).toHaveTextContent('€3,459.08');
  await userEvent.click(heading);
  expect(screen.getByRole('button',{name:/Salary €3,459.08/})).toBeVisible();
  expect(screen.queryByText('No Accounts')).not.toBeInTheDocument();
});

it('shows only the no accounts message and add action when the account list is empty',async()=>{
  render(<App/>);await saved();
  expect(await screen.findByText('No Accounts')).toBeInTheDocument();
  expect(screen.getByText("You can't plan without adding accounts to Open Budget first. How about adding one now?")).toBeInTheDocument();
  expect(screen.queryByRole('button',{name:/CASH/})).not.toBeInTheDocument();
  expect(screen.getByRole('button',{name:'Add Account'})).toBeVisible();
});

it('resizes and collapses the desktop sidebar while preserving its width',async()=>{
  render(<App/>);await saved();
  const sidebar=document.querySelector('.sidebar');
  const resize=screen.getByRole('separator',{name:'Resize sidebar'});
  fireEvent.keyDown(resize,{key:'ArrowRight'});
  expect(sidebar).toHaveStyle({'--sidebar-width':'270px'});
  expect(window.localStorage.getItem('openbudget.sidebarWidth')).toBe('270');
  Object.defineProperty(resize,'setPointerCapture',{value:vi.fn()});
  Object.defineProperty(resize,'releasePointerCapture',{value:vi.fn()});
  fireEvent.pointerDown(resize,{pointerId:1,clientX:270});
  fireEvent.pointerMove(resize,{pointerId:1,clientX:320});
  fireEvent.pointerUp(resize,{pointerId:1});
  expect(sidebar).toHaveStyle({'--sidebar-width':'320px'});
  await userEvent.click(screen.getByRole('button',{name:'Collapse sidebar'}));
  expect(sidebar).toHaveClass('is-collapsed');
  expect(sidebar).toHaveStyle({'--sidebar-width':'56px'});
  expect(screen.getByRole('button',{name:'Plan'})).toBeVisible();
  await userEvent.click(screen.getByRole('button',{name:'Expand sidebar'}));
  expect(sidebar).not.toHaveClass('is-collapsed');
  expect(sidebar).toHaveStyle({'--sidebar-width':'320px'});
});

it('opens the account creation form from beneath CASH',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Add Account'}));
  expect(await screen.findByRole('dialog',{name:'Add cash account'})).toBeInTheDocument();
  expect(screen.getByLabelText('Account name')).toBeInTheDocument();
});

it('creates a budget and selects it as the active plan',async()=>{
  vi.spyOn(window,'prompt').mockReturnValue('Travel');
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Budget menu'}));
  expect(screen.queryByRole('menuitem',{name:/Share YNAB|Join a Workshop|Keyboard Shortcuts|Legal/})).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole('menuitem',{name:/New Plan/}));
  await waitFor(()=>expect(screen.getByRole('button',{name:'Budget menu'})).toHaveTextContent('Travel'));
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

it.each([{amount:0.01,label:'Ready to assign',button:/Assign/},{amount:-0.01,label:'You assigned more than you have',button:/Fix This/}])('retains the nonzero summary for $amount euros',async({amount,label,button})=>{
  months['2026-10'].readyToAssign=amount;
  render(<App/>);await saved();
  const summary=within(screen.getByRole('region',{name:'Ready to assign'}));
  expect(summary.getByText(label)).toBeInTheDocument();
  expect(summary.queryByText('All Money Assigned')).not.toBeInTheDocument();
  expect(summary.getByRole('button',{name:button})).toBeEnabled();
});

it('opens Fix This with positive categories and unassigns the selected amount',async()=>{
  months['2026-10'].readyToAssign=-100;
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:400,activity:0,available:400,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Fix This'}));
  const dialog=screen.getByRole('dialog',{name:'Un-assign money from'});
  expect(within(dialog).getByRole('button',{name:'Month to unassign from'})).toHaveTextContent('Oct 2026');
  expect(within(dialog).getByRole('button',{name:/Food/})).toHaveTextContent('€400.00');
  await userEvent.click(within(dialog).getByRole('button',{name:/Food/}));
  await waitFor(()=>expect(writes.at(-1)?.groups).toEqual(expect.arrayContaining([expect.objectContaining({categories:expect.arrayContaining([expect.objectContaining({id:'food',assigned:300})])})])));
});

it('keeps the future Fix This category picker empty and can switch back one month',async()=>{
  months['2026-11'].readyToAssign=-100;
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:400,activity:0,available:400,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  await next('Next','Nov 2026');
  await userEvent.click(screen.getByRole('button',{name:'Fix This'}));
  const dialog=screen.getByRole('dialog',{name:'Un-assign money from'});
  expect(within(dialog).getByText('No categories found.')).toBeInTheDocument();
  expect(within(dialog).getByRole('combobox',{name:'Category to unassign'})).toBeDisabled();
  await userEvent.click(within(dialog).getByRole('button',{name:'Month to unassign from'}));
  await userEvent.click(within(dialog).getByRole('button',{name:'Oct 2026'}));
  await userEvent.click(within(dialog).getByRole('button',{name:'OK'}));
  await waitFor(()=>expect(within(dialog).getByRole('button',{name:/Food/})).toHaveTextContent('€400.00'));
});

it('opens the category group rename popover and persists the new name',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Home'}));
  const dialog=screen.getByRole('dialog',{name:'Rename category group'});
  const name=within(dialog).getByRole('textbox',{name:'Name'});
  expect(name).toHaveValue('Home');
  await userEvent.clear(name);await userEvent.type(name,'Household');
  await userEvent.click(within(dialog).getByRole('button',{name:'OK'}));
  await waitFor(()=>expect(writes).toHaveLength(1));
  expect(writes[0].groups).toMatchObject([{name:'Household'}]);
});

it('requires a subcategory to be selected before its rename popover opens',async()=>{
  render(<App/>);await saved();
  const categoryName=screen.getByRole('button',{name:'Electricity'});
  await userEvent.click(categoryName);
  expect(screen.queryByRole('dialog',{name:'Rename subcategory'})).not.toBeInTheDocument();
  expect(screen.getByRole('checkbox',{name:'Select Electricity'})).toBeChecked();
  expect(categoryName).toHaveClass('is-selected');
  await userEvent.click(categoryName);
  const dialog=screen.getByRole('dialog',{name:'Rename subcategory'});
  const name=within(dialog).getByRole('textbox',{name:'Name'});
  expect(name).toHaveValue('Electricity');
  await userEvent.clear(name);await userEvent.type(name,'Power');
  await userEvent.click(within(dialog).getByRole('button',{name:'OK'}));
  await waitFor(()=>expect(writes).toHaveLength(1));
  expect(writes[0].groups).toMatchObject([{categories:[{name:'Power'}]}]);
});

it('expands Auto-Assign options and caps category assignments at Ready to Assign',async()=>{
  scheduleRows=[{id:'rent-schedule',occurrenceId:'rent-occurrence',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'rent-occurrence',accountId:'account-id',date:'2026-10-27',amount:-1000,payee:'Landlord',memo:'Rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:/Auto-Assign/}));
  const underfunded=await screen.findByRole('button',{name:'Underfunded €985.00'});
  await waitFor(()=>expect(underfunded).toBeEnabled());
  await userEvent.click(underfunded);
  await waitFor(()=>expect(writes).toHaveLength(1));
  const categories=(writes[0].groups as {categories:{id:string;assigned:number}[]}[]).flatMap(group=>group.categories);
  expect(categories.find(category=>category.id==='utilities')?.assigned).toBe(500);
});

it('auto-assigns scheduled shortfalls across every subcategory from Ready to Assign when none are selected',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  scheduleRows=[
    {id:'rent-schedule',occurrenceId:'rent-occurrence',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{id:'rent-occurrence',accountId:'account-id',date:'2026-10-27',amount:-100,payee:'Landlord',memo:'Rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}},
    {id:'food-schedule',occurrenceId:'food-occurrence',repeat:'monthly',startDate:'2026-10-28',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{id:'food-occurrence',accountId:'account-id',date:'2026-10-28',amount:-50,payee:'Grocer',memo:'Groceries',categoryId:'food',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}},
  ];
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Assign'}));
  const underfunded=await screen.findByRole('button',{name:'Underfunded €135.00'});
  await waitFor(()=>expect(underfunded).toBeEnabled());
  await userEvent.click(underfunded);
  await waitFor(()=>expect(writes).toHaveLength(1));
  const categories=(writes[0].groups as {categories:{id:string;assigned:number}[]}[]).flatMap(group=>group.categories);
  expect(categories.find(category=>category.id==='utilities')?.assigned).toBe(85);
  expect(categories.find(category=>category.id==='food')?.assigned).toBe(50);
});

it('limits scheduled Underfunded auto-assignment to selected subcategories',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  scheduleRows=[
    {id:'rent-schedule',occurrenceId:'rent-occurrence',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{id:'rent-occurrence',accountId:'account-id',date:'2026-10-27',amount:-100,payee:'Landlord',memo:'Rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}},
    {id:'food-schedule',occurrenceId:'food-occurrence',repeat:'monthly',startDate:'2026-10-28',isPaused:false,endDate:null,count:null,toAccountId:null,transaction:{id:'food-occurrence',accountId:'account-id',date:'2026-10-28',amount:-50,payee:'Grocer',memo:'Groceries',categoryId:'food',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}},
  ];
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('checkbox',{name:'Select Food'}));
  await userEvent.click(screen.getByRole('button',{name:'Assign'}));
  const underfunded=await screen.findByRole('button',{name:'Underfunded €50.00'});
  await waitFor(()=>expect(underfunded).toBeEnabled());
  await userEvent.click(underfunded);
  await waitFor(()=>expect(writes).toHaveLength(1));
  const categories=(writes[0].groups as {categories:{id:string;assigned:number}[]}[]).flatMap(group=>group.categories);
  expect(categories.find(category=>category.id==='utilities')?.assigned).toBe(0);
  expect(categories.find(category=>category.id==='food')?.assigned).toBe(50);
});

it('loads historical values for the last-month and 12-month assignment options',async()=>{
  planningHistory[2025]=planningYear(2025,{'2025-10':{assigned:120,spent:80}});
  planningHistory[2026]=planningYear(2026,{'2026-09':{assigned:360,spent:240}});
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:/Auto-Assign/}));
  expect(await screen.findByRole('button',{name:'Assigned Last Month €360.00'})).toBeEnabled();
  expect(screen.getByRole('button',{name:'Spent Last Month €240.00'})).toBeEnabled();
  expect(screen.getByRole('button',{name:'Average Assigned €40.00'})).toBeEnabled();
  expect(screen.getByRole('button',{name:'Average Spent €26.67'})).toBeEnabled();
  const historyRequests=vi.mocked(fetch).mock.calls.map(([path])=>String(path)).filter(path=>path.startsWith('/api/long-term-planning/'));
  expect(historyRequests).toContain('/api/long-term-planning/2025');
  expect(historyRequests).toContain('/api/long-term-planning/2026');
});

it('manually assigns a chosen amount to a selected subcategory',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Assign'}));
  expect(screen.getByRole('tab',{name:'Auto'})).toHaveAttribute('aria-selected','true');
  expect(screen.getByRole('button',{name:/Underfunded/})).toBeInTheDocument();
  await userEvent.click(screen.getByRole('tab',{name:'Manually'}));
  const amount=screen.getByRole('textbox',{name:'Amount to assign'});
  expect(amount).toHaveValue('500.00');
  await userEvent.clear(amount);await userEvent.type(amount,'125.50');
  await userEvent.click(screen.getByRole('combobox',{name:'To'}));
  await userEvent.click(within(screen.getByRole('group',{name:'Home'})).getByRole('option',{name:/Food/}));
  expect(screen.getByRole('combobox',{name:'To'})).toHaveValue('Home: Food');
  await userEvent.click(within(document.querySelector('.manual-assign-form') as HTMLElement).getByRole('button',{name:/^Assign$/}));
  await waitFor(()=>expect(writes).toHaveLength(1));
  const categories=(writes[0].groups as {categories:{id:string;assigned:number}[]}[]).flatMap(group=>group.categories);
  expect(categories.find(category=>category.id==='food')?.assigned).toBe(125.5);
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

it('disables the activity amount when the category has no activity',async()=>{
  render(<App/>);await saved();
  const activity=screen.getByRole('button',{name:'View transactions for Electricity activity'});
  expect(activity).toBeDisabled();

  await next('Previous','Sept 2026');
  expect(screen.getByRole('button',{name:'View transactions for Electricity activity'})).toBeDisabled();
  await next('Previous','Aug 2026');
  expect(screen.getByRole('button',{name:'View transactions for Electricity activity'})).toBeEnabled();
});

it('selects the category and focuses Assigned when clicking its row background',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  const categoryRow=row() as HTMLElement;
  const checkbox=within(categoryRow).getByRole('checkbox',{name:'Select Electricity'});
  const foodCheckbox=screen.getByRole('checkbox',{name:'Select Food'});
  expect(checkbox).not.toBeChecked();

  await userEvent.click(foodCheckbox);
  expect(foodCheckbox).toBeChecked();
  await userEvent.click(categoryRow);

  expect(checkbox).toBeChecked();
  expect(foodCheckbox).not.toBeChecked();
  expect(categoryRow).toHaveClass('category-selected');
  expect(screen.getByRole('textbox',{name:'Assigned to Electricity'})).toHaveFocus();
  expect(document.querySelector('.category-detail-card strong')).toHaveTextContent('Electricity');

  await userEvent.click(foodCheckbox);
  expect(checkbox).toBeChecked();
  expect(foodCheckbox).toBeChecked();

  await userEvent.click(categoryRow);
  expect(checkbox).not.toBeChecked();
  expect(foodCheckbox).toBeChecked();
  expect(categoryRow).not.toHaveClass('category-selected');
  expect(screen.queryByRole('textbox',{name:'Assigned to Electricity'})).not.toBeInTheDocument();
});

it('selects the whole category row when opening its Assigned input',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  const assigned=screen.getByRole('button',{name:'Edit assigned for Food'});
  const categoryRow=assigned.closest('.category-row') as HTMLElement;
  const checkbox=within(categoryRow).getByRole('checkbox',{name:'Select Food'});

  expect(assigned.querySelector('.assignment-adjust-icon svg')).toBeInTheDocument();

  await userEvent.click(assigned);

  expect(checkbox).toBeChecked();
  expect(categoryRow).toHaveClass('category-selected');
  expect(screen.getByRole('textbox',{name:'Assigned to Food'})).toHaveFocus();
  expect(screen.getByRole('textbox',{name:'Assigned to Food'}).closest('.amount-input-shell')?.querySelector('.assignment-adjust-icon svg')).toBeInTheDocument();
});

it('shows selected-month upcoming activity in category details and opens its transaction list',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-31',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-10-31',amount:-67.99,payee:'Landlord',memo:'Monthly rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  render(<App/>);await saved();
  await userEvent.click(row() as HTMLElement);

  const upcoming=await screen.findByRole('button',{name:/1 Upcoming Transaction/});
  expect(upcoming.closest('.category-detail-card')).toBeInTheDocument();
  expect(upcoming).toHaveTextContent('Available After Upcoming');
  expect(upcoming).toHaveTextContent('-€67.99');
  await userEvent.click(upcoming);

  expect(await screen.findByRole('heading',{name:'Upcoming activity · Electricity'})).toBeInTheDocument();
  expect(screen.getByText('Landlord')).toBeInTheDocument();
  expect(screen.getByText('Monthly rent')).toBeInTheDocument();
});

it('shows projected shortfall in category details and lists the scheduled transaction',async()=>{
  const accountId='11111111-1111-4111-8111-111111111111';
  focusAccounts=[{id:accountId,name:'Household',isClosed:false,balance:15,clearedBalance:0,unclearedBalance:15,accountType:'checking',isFavorite:false,connectionStatus:'unlinked',lastReconciledAt:null,needsCategoryCount:0}];
  const schedule={id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId,date:'2026-10-27',amount:-49.9,payee:'Gym',memo:'Membership',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}};
  scheduleRows=[schedule];
  months['2026-10']=budget('2026-10',20.6,0,20.6);
  render(<App/>);await saved();
  const categoryRow=row();
  await userEvent.click(screen.getByRole('button',{name:/^Electricity$/}));
  expect(categoryRow.querySelector('.available-cell')).toHaveClass('underfunded');
  expect(within(categoryRow).getByRole('img',{name:'Upcoming transactions will make Electricity negative'})).toBeInTheDocument();
  expect(categoryRow.querySelector('.category-upcoming')).not.toBeInTheDocument();
  expect(Number.parseFloat((categoryRow.querySelector('.progress-orange') as HTMLElement).style.width)).toBeGreaterThan(0);
  expect((categoryRow.querySelector('.progress-red') as HTMLElement).style.width).toBe('0%');
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

it('marks a collapsed category group when scheduled spending would make a subcategory negative',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-10-27',amount:-49.9,payee:'Gym',memo:'Membership',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,isLocked:false}}];
  months['2026-10']=budget('2026-10',20.6,0,20.6);
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Toggle Home'}));
  expect(screen.getByRole('img',{name:'Upcoming transactions will make a subcategory negative'})).toBeInTheDocument();
  expect(screen.queryByRole('img',{name:'Upcoming transactions will make Electricity negative'})).not.toBeInTheDocument();
});

it('keeps Available positive when current-month scheduled spending does not cause a shortfall',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-10-27',amount:-10,payee:'Gym',memo:'Membership',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  months['2026-10']=budget('2026-10',20,0,20);
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell') as HTMLElement;
  expect(available).toHaveClass('positive');
  expect(available).not.toHaveClass('underfunded');
});

it('does not highlight Available for a future-month scheduled transaction',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-11-27',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-11-27',amount:-1600,payee:'Rent',memo:'Monthly rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  months['2026-10']=budget('2026-10',1500,0,1500);
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell') as HTMLElement;
  expect(available).toHaveClass('positive');
  expect(available).not.toHaveClass('underfunded');
  expect(screen.queryByRole('button',{name:/Upcoming Transaction/})).not.toBeInTheDocument();
});

it('does not highlight Available when current-month scheduled spending leaves exactly zero',async()=>{
  scheduleRows=[{id:'schedule-id',occurrenceId:'occurrence-id',repeat:'monthly',startDate:'2026-10-31',isPaused:false,endDate:null,count:null,toAccountId:null,
    transaction:{id:'occurrence-id',accountId:'account-id',date:'2026-10-31',amount:-1500,payee:'Landlord',memo:'Monthly rent',categoryId:'utilities',status:'uncleared',transferId:null,isOpening:false,splits:[],needsAttention:false,runningBalance:null,transferAccountName:null,categoryName:null,isLocked:false}}];
  months['2026-10']=budget('2026-10',1500,0,1500);
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell') as HTMLElement;
  expect(available).toHaveClass('positive');
  expect(available).not.toHaveClass('underfunded');
});

it('opens move money from a positive Available amount and keeps zero Available plain text',async()=>{
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell') as HTMLElement;
  expect(within(available).getByRole('button',{name:'Move available money from Electricity'})).toBeInTheDocument();
  const foodRow=screen.getByRole('button',{name:'Edit assigned for Food'}).closest('.category-row') as HTMLElement;
  expect(within(foodRow).queryByRole('button',{name:'Move available money from Food'})).not.toBeInTheDocument();

  await userEvent.click(within(available).getByRole('button',{name:'Move available money from Electricity'}));
  const dialog=screen.getByRole('dialog',{name:'Move'});
  expect(within(dialog).getByLabelText('Amount to move')).toHaveValue('15');
  expect(within(dialog).getByLabelText('Move to')).toHaveValue('Home: Food');
  await userEvent.clear(within(dialog).getByLabelText('Amount to move'));
  await userEvent.type(within(dialog).getByLabelText('Amount to move'),'5');
  await userEvent.click(within(dialog).getByRole('button',{name:'OK'}));
  await waitFor(()=>expect(writes).toHaveLength(1));
  expect(writes[0].groups).toEqual(expect.arrayContaining([expect.objectContaining({categories:expect.arrayContaining([expect.objectContaining({id:'utilities',assigned:-5}),expect.objectContaining({id:'food',assigned:5})])})]));
});

it('opens cover overspending from a negative Available amount',async()=>{
  months['2026-10']=budget('2026-10',0,-10,-10);
  months['2026-10'].groups[0].categories.push({id:'food',name:'Food',assigned:20,activity:0,available:20,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  const available=row().querySelector('.available-cell') as HTMLElement;
  const cover=within(available).getByRole('button',{name:'Cover overspending for Electricity'});
  expect(cover).toBeInTheDocument();
  await userEvent.click(cover);
  const dialog=screen.getByRole('dialog',{name:'Cover overspending'});
  expect(within(dialog).getByLabelText('Move from')).toHaveValue('Home: Food');
  expect(within(dialog).getByLabelText('Amount to move')).toHaveValue('10');
  expect(within(dialog).getByLabelText('Move to')).toHaveValue('Home: Electricity');
  await userEvent.click(within(dialog).getByRole('button',{name:'OK'}));
  await waitFor(()=>expect(writes).toHaveLength(1));
  expect(writes[0].groups).toEqual(expect.arrayContaining([expect.objectContaining({categories:expect.arrayContaining([expect.objectContaining({id:'utilities',assigned:10}),expect.objectContaining({id:'food',assigned:10})])})]));
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

it('reorders a subcategory before another subcategory in its current group',async()=>{
  const month=months['2026-10'];
  month.groups[0].categories.push({id:'internet',name:'Internet',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false});
  render(<App/>);await saved();
  const source=screen.getByRole('button',{name:/^Internet$/}).closest('.category-row')!;
  const target=screen.getByRole('button',{name:/^Electricity$/}).closest('.category-row')!;
  const dataTransfer={effectAllowed:'',setData:vi.fn()};
  fireEvent.dragStart(source,{dataTransfer});fireEvent.drop(target,{dataTransfer});
  await waitFor(()=>expect(writes).toHaveLength(1));
  const submitted= writes[0] as {groups:{id:string;categories:{id:string}[]}[]};
  expect(submitted.groups.find(group=>group.id==='home')?.categories.map(category=>category.id)).toEqual(['internet','utilities']);
});

it('allows a moved subcategory to be reordered in its destination group',async()=>{
  const month=months['2026-10'];
  month.groups.push({id:'other',name:'Other',sortOrder:1,isArchived:false,categories:[
    {id:'other-first',name:'Other first',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:0,isArchived:false},
    {id:'other-second',name:'Other second',assigned:0,activity:0,available:0,target:0,icon:'',snoozed:false,sortOrder:1,isArchived:false}
  ]});
  render(<App/>);await saved();
  const dataTransfer={effectAllowed:'',setData:vi.fn()};
  const source=screen.getByRole('button',{name:/^Electricity$/}).closest('.category-row')!;
  const destination=screen.getByRole('button',{name:/^Other$/}).closest('.category-group') as HTMLElement;
  fireEvent.dragStart(source,{dataTransfer});fireEvent.drop(destination,{dataTransfer});
  await waitFor(()=>expect(within(destination).getByRole('button',{name:/^Electricity$/})).toBeInTheDocument());
  const moved=within(destination).getByRole('button',{name:/^Electricity$/}).closest('.category-row') as HTMLElement;
  const first=within(destination).getByRole('button',{name:/^Other first$/}).closest('.category-row') as HTMLElement;
  fireEvent.dragStart(moved,{dataTransfer});fireEvent.dragOver(first,{dataTransfer});fireEvent.drop(first,{dataTransfer});
  await waitFor(()=>expect(writes).toHaveLength(1));
  const submitted= writes[0] as {groups:{id:string;categories:{id:string}[]}[]};
  expect(submitted.groups.find(group=>group.id==='home')?.categories.map(category=>category.id)).toEqual([]);
  expect(submitted.groups.find(group=>group.id==='other')?.categories.map(category=>category.id)).toEqual(['utilities','other-first','other-second']);
});

it('opens Long-Term Planning from the main navigation',async()=>{
  render(<App/>);await saved();
  await userEvent.click(screen.getByRole('button',{name:'Long-Term Planning'}));
  expect(await screen.findByRole('heading',{name:'Long-Term Planning'})).toBeInTheDocument();
  expect(await screen.findByText('Add budget categories in Plan to start long-term planning.')).toBeInTheDocument();
  const year=new Date().getFullYear();
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(`/api/long-term-planning/${year}`,expect.objectContaining({signal:expect.any(AbortSignal)}));
});
