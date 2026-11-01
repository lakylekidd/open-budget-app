import { afterEach, describe, expect, it, vi } from 'vitest';
import { budgetMonthPath, loadCategoryBudget, parseMoney } from './api';

afterEach(()=>vi.unstubAllGlobals());
it('loads group names and available balances from the requested transaction month including archive status',async()=>{
  const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify({readyToAssign:-35.14,groups:[{id:'home',name:'Home',isArchived:false,categories:[{id:'rent',name:'Rent',available:42.5}]},{id:'old',name:'Archived',isArchived:true,categories:[{id:'old-rent',name:'Rent',available:100}]}]}),{status:200}));vi.stubGlobal('fetch',fetcher);
  const controller=new AbortController();const data=await loadCategoryBudget('2027-02-28',controller.signal);expect(fetcher).toHaveBeenCalledWith('/api/budget/2027-02',{signal:controller.signal});expect(data.readyToAssign).toBe(-35.14);expect(data.categories).toEqual([{id:'rent',name:'Rent',available:42.5,groupId:'home',groupName:'Home',isArchived:undefined},{id:'old-rent',name:'Rent',available:100,groupId:'old',groupName:'Archived',isArchived:true}]);
});

describe('money input', () => {
  it.each([['12.34', 12.34], ['-0,25', -0.25], ['0', 0], ['999999999.99', 999999999.99]])('accepts %s', (value, expected) => {
    expect(parseMoney(String(value))).toBe(expected);
  });
  it.each(['', 'abc', '1.001', '1e3', '1.2.3', '1000000000', '--2'])('rejects %s without silently coercing it', value => {
    expect(parseMoney(value)).toBeNull();
  });
});

describe('budgetMonthPath', () => {
  afterEach(() => { document.cookie = 'openbudget.activeBudgetId=; Path=/; Max-Age=0'; });
  it('uses the active budget so category pickers never read the default budget', () => {
    document.cookie = 'openbudget.activeBudgetId=my-budget; Path=/';
    expect(budgetMonthPath('2026-11')).toBe('/api/budgets/my-budget/months/2026-11');
  });
  it('falls back to the legacy route when no budget is selected', () => {
    expect(budgetMonthPath('2026-11')).toBe('/api/budget/2026-11');
  });
});
