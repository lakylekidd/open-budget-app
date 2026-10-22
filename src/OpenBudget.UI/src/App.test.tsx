import { render,screen,waitFor,within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach,expect,it,vi } from 'vitest';
import App from './App';

type Month={month:string;note:string;version:number;fundsAdded:number;openingReadyToAssign:number;readyToAssign:number;groups:{id:string;name:string;sortOrder:number;isArchived:boolean;categories:{id:string;name:string;assigned:number;activity:number;available:number;target:number;icon:string;snoozed:boolean;sortOrder:number;isArchived:boolean}[]}[]};
const budget=(month:string,assigned=0,activity=0,available=0):Month=>({month,note:'',version:1,fundsAdded:0,openingReadyToAssign:500,readyToAssign:500-assigned,groups:[{id:'home',name:'Home',sortOrder:0,isArchived:false,categories:[{id:'utilities',name:'Electricity',assigned,activity,available,target:0,icon:'',snoozed:false,sortOrder:0,isArchived:false}]}]});
const response=(data:unknown)=>new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}});
let months:Record<string,Month>;let writes:Record<string,unknown>[];let releaseSave:(()=>void)|undefined;let holdSave:boolean;
beforeEach(()=>{
  months={'2026-08':budget('2026-08',50,-35,15),'2026-09':budget('2026-09',0,0,15),'2026-10':budget('2026-10',0,0,15),'2026-11':budget('2026-11',0,0,15)};writes=[];holdSave=false;releaseSave=undefined;
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>{
    if(path==='/api/accounts')return response({version:1,accounts:[]});
    if(path==='/api/budget')return response({id:'default',name:'Test budget',version:1,currency:'EUR'});
    if(path.startsWith('/api/budget/')){
      const month=path.slice('/api/budget/'.length);
      if(init?.method==='PUT'){
        const request=JSON.parse(String(init.body));writes.push(request);
        if(holdSave)await new Promise<void>(resolve=>{releaseSave=resolve;});
        const amount=request.groups[0].categories[0].assigned;
        if(month==='2026-09'&&amount===50){months[month]=budget(month,50,0,65);months['2026-10']=budget('2026-10',0,0,65);}
        else if(month==='2026-08'&&amount===80){months[month]=budget(month,80,-35,45);months['2026-09']=budget('2026-09',50,0,95);months['2026-10']=budget('2026-10',0,0,95);}
        else {months[month]=budget(month,amount,0,amount);months['2026-11']=budget('2026-11',0,0,amount);}
        months[month].version++;
      }
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
  const summary=within(screen.getByRole('region',{name:'Ready to assign'}));
  expect(summary.getByText('€0.00')).toBeInTheDocument();
  expect(summary.getByText('All Money Assigned')).toBeInTheDocument();
  expect(summary.queryByRole('button',{name:'Record income'})).not.toBeInTheDocument();
  await next('Next','Nov 2026');
  expect(summary.queryByText('All Money Assigned')).not.toBeInTheDocument();
  expect(summary.getByRole('button',{name:'Record income'})).toBeEnabled();
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
  vi.stubGlobal('fetch',vi.fn(async(path:string,init?:RequestInit)=>path==='/api/budget/2026-10'&&!init?.method?new Promise<Response>(resolve=>{complete=resolve;}):original(path,init)));
  render(<App/>);expect(screen.getByRole('button',{name:'Edit assigned for Electricity & water'})).toBeDisabled();await waitFor(()=>expect(complete).toBeDefined());complete!(response(months['2026-10']));await saved();expect(screen.getByRole('button',{name:'Edit assigned for Electricity'})).toBeEnabled();
});
