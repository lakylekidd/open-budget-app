using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class TransactionSelectionApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026,10,21);
    private static async Task<T> Get<T>(HttpClient c,string path)=>(await c.GetFromJsonAsync<T>(path))!;
    private static async Task<AccountsDto> Accounts(HttpClient c)=>await Get<AccountsDto>(c,"/api/accounts");
    private static async Task<RegisterDto> Register(HttpClient c)=>await Get<RegisterDto>(c,"/api/transactions?pageSize=100");
    private static async Task<T> Post<T>(HttpClient c,string path,object body){var r=await c.PostAsJsonAsync(path,body);Assert.True(r.IsSuccessStatusCode,await r.Content.ReadAsStringAsync());return (await r.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<Guid> Account(HttpClient c,string name,decimal balance=100)=> (await Post<MutationDto>(c,"/api/accounts",new CreateAccountRequest(Guid.NewGuid(),(await Accounts(c)).Version,name,Date,balance))).Id;
    private static async Task<Guid> Entry(HttpClient c,Guid account,decimal amount=-10)=> (await Post<MutationDto>(c,"/api/transactions",new TransactionRequest(Guid.NewGuid(),(await Accounts(c)).Version,account,Date,amount,"Shop","Original","groceries","uncleared",[]))).Id;
    private static async Task<TransactionSelectionRequest> Command(HttpClient c,string action,params Guid[] ids)=>new(Guid.NewGuid(),(await Accounts(c)).Version,ids.ToList(),action);

    [Fact]
    public async Task Selection_memo_flag_status_move_reload_and_replay_preserve_amounts_and_budget()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");var b=await Account(c,"Savings");var one=await Entry(c,a);var two=await Entry(c,a,-5);
        var memo=(await Command(c,"memo",one,two)) with{Memo=" New memo "};var receipt=await Post<MutationDto>(c,"/api/transactions/selection",memo);
        Assert.Equal(receipt,await Post<MutationDto>(c,"/api/transactions/selection",memo));
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"flag",one,two)) with{Color="purple"});
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"status",one,two)) with{Status="cleared"});
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"move",one,two)) with{AccountId=b});
        var entries=(await Register(app.CreateClient())).Items.Where(t=>t.Id==one||t.Id==two).ToList();Assert.Equal(2,entries.Count);
        Assert.All(entries,t=>{Assert.Equal(b,t.AccountId);Assert.Equal("New memo",t.Memo);Assert.Equal("purple",t.FlagColor);Assert.Equal("cleared",t.Status);});
        var totals=await Accounts(c);Assert.Equal(100,totals.Accounts.Single(t=>t.Id==a).Balance);Assert.Equal(85,totals.Accounts.Single(t=>t.Id==b).Balance);
        var month=await Get<BudgetDto>(c,"/api/budget/2026-10");Assert.Equal(-15,month.Groups.SelectMany(g=>g.Categories).Single(t=>t.Id=="groceries").Activity);
    }

    [Fact]
    public async Task Duplicate_preserves_splits_but_resets_status_approval_and_schedule_and_is_replay_safe()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");
        var original=(await Post<MutationDto>(c,"/api/transactions",new TransactionRequest(Guid.NewGuid(),(await Accounts(c)).Version,a,Date,-10,"Split shop","Memo",null,"cleared",[new("groceries",-6),new("rent",-4)],"monthly"))).Id;
        var request=await Command(c,"duplicate",original);var result=await Post<MutationDto>(c,"/api/transactions/selection",request);Assert.Equal(result,await Post<MutationDto>(c,"/api/transactions/selection",request));
        var copy=Assert.Single((await Register(c)).Items,t=>!t.IsOpening&&t.Id!=original);Assert.Equal(-10,copy.Amount);Assert.Equal("Split shop",copy.Payee);Assert.Equal("Memo",copy.Memo);Assert.Equal(2,copy.Splits.Count);Assert.Equal("uncleared",copy.Status);Assert.True(copy.IsApproved);Assert.Null(copy.ScheduleId);
        Assert.Equal(80,(await Accounts(c)).Accounts.Single().Balance);
        var month=await Get<BudgetDto>(c,"/api/budget/2026-10");Assert.Equal(-12,month.Groups.SelectMany(g=>g.Categories).Single(t=>t.Id=="groceries").Activity);
    }

    [Fact]
    public async Task Transfer_duplicate_memo_repeat_and_delete_change_complete_pairs_once()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");var b=await Account(c,"Savings");
        var pair=(await Post<MutationDto>(c,"/api/transfers",new TransferRequest(Guid.NewGuid(),(await Accounts(c)).Version,a,b,Date,20,"Transfer"))).Id;
        var original=(await Register(c)).Items.Where(t=>t.TransferId==pair).ToList();
        await Post<MutationDto>(c,"/api/transactions/selection",await Command(c,"duplicate",original.Select(t=>t.Id).ToArray()));
        var copies=(await Register(c)).Items.Where(t=>t.TransferId!=null&&t.TransferId!=pair).ToList();Assert.Equal(2,copies.Count);Assert.Equal(copies[0].TransferId,copies[1].TransferId);Assert.Equal(0,copies.Sum(t=>t.Amount));
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"memo",copies[0].Id)) with{Memo="Both sides"});Assert.All((await Register(c)).Items.Where(t=>t.TransferId==copies[0].TransferId),t=>Assert.Equal("Both sides",t.Memo));
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"repeat",copies.Select(t=>t.Id).ToArray())) with{Repeat="monthly"});
        var schedules=await Get<SchedulesDto>(c,"/api/schedules");Assert.Single(schedules.Items);Assert.Equal("monthly",schedules.Items[0].Repeat);
        await Post<MutationDto>(c,"/api/transactions/selection",await Command(c,"delete",copies.Select(t=>t.Id).ToArray()));
        Assert.Equal(2,(await Register(c)).Items.Count(t=>t.TransferId!=null));Assert.Equal(80,(await Accounts(c)).Accounts.Single(t=>t.Id==a).Balance);Assert.Equal(120,(await Accounts(c)).Accounts.Single(t=>t.Id==b).Balance);
    }

    [Fact]
    public async Task Reconciled_memo_requires_confirmation_and_retains_invalidation_audit()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");var id=await Entry(c,a);
        await Post<MutationDto>(c,$"/api/transactions/{id}/status",new StatusRequest(Guid.NewGuid(),(await Accounts(c)).Version,"cleared"));
        await Post<MutationDto>(c,$"/api/accounts/{a}/reconcile",new ReconcileRequest(Guid.NewGuid(),(await Accounts(c)).Version,90));
        var request=(await Command(c,"memo",id)) with{Memo="Correction"};Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/transactions/selection",request)).StatusCode);
        Assert.Equal("Original",(await Register(c)).Items.Single(t=>t.Id==id).Memo);
        await Post<MutationDto>(c,"/api/transactions/selection",request with{ConfirmReconciledEdit=true});
        Assert.Equal("cleared",(await Register(c)).Items.Single(t=>t.Id==id).Status);
        await using var db=app.CreateDb();Assert.True(Assert.Single(await db.Reconciliations.ToListAsync()).IsInvalidated);var audit=Assert.Single(await db.LedgerCommands.Where(t=>t.Kind=="transaction.selection").ToListAsync());Assert.Contains("Original",audit.BeforeJson);Assert.Contains("Correction",audit.AfterJson);
    }

    [Fact]
    public async Task Invalid_selections_and_closed_destinations_reject_without_partial_writes()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");var b=await Account(c,"Closed",0);var id=await Entry(c,a);
        var close=await c.PutAsJsonAsync($"/api/accounts/{b}",new UpdateAccountRequest(Guid.NewGuid(),(await Accounts(c)).Version,"Closed",true));Assert.True(close.IsSuccessStatusCode);
        var baseline=(await Accounts(c)).Version;var template=await Command(c,"memo",id);
        var requests=new[]{template with{Ids=null},template with{Ids=[]},template with{Ids=[id,id]},template with{Ids=Enumerable.Range(0,101).Select(_=>Guid.NewGuid()).ToList()},template with{Ids=[Guid.Empty]},template with{Memo=new string('x',1001)},template with{Action="unknown"},template with{Action="status",Status="reconciled"},template with{Action="flag",Color="pink"},template with{Action="move",AccountId=b},template with{Action="repeat",Repeat="never"}};
        foreach(var r in requests)Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/transactions/selection",r)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync("/api/transactions/selection",template with{Ids=[id,Guid.NewGuid()],Memo="Not saved"})).StatusCode);
        Assert.Equal(baseline,(await Accounts(c)).Version);Assert.Equal("Original",(await Register(c)).Items.Single(t=>t.Id==id).Memo);
        var opening=(await Register(c)).Items.First(t=>t.IsOpening&&t.AccountId==a);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/transactions/selection",(await Command(c,"duplicate",id,opening.Id)))).StatusCode);
        Assert.Equal(3,(await Register(c)).Total);
    }

    [Fact]
    public async Task Bulk_clear_approves_pending_imports_and_repeating_keeps_actual_balances_unchanged()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");
        var one=(await Post<MutationDto>(c,"/api/transactions/import",new TransactionRequest(Guid.NewGuid(),(await Accounts(c)).Version,a,Date,-5,"Imported","",null,"uncleared",[]))).Id;
        var two=await Entry(c,a,-10);
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"status",one,two)) with{Status="cleared"});
        Assert.All((await Register(c)).Items.Where(t=>t.Id==one||t.Id==two),t=>Assert.True(t.IsApproved));Assert.Equal(0,(await Accounts(c)).Accounts.Single().UnapprovedCount);
        await Post<MutationDto>(c,"/api/transactions/selection",(await Command(c,"repeat",one,two)) with{Repeat="monthly"});
        Assert.Equal(85,(await Accounts(c)).Accounts.Single().Balance);Assert.Equal(3,(await Register(c)).Total);
        var scheduled=await Get<SchedulesDto>(c,"/api/schedules");Assert.Equal(2,scheduled.Total);Assert.All(scheduled.Items,t=>Assert.Equal(new DateOnly(2026,11,21),t.Transaction.Date));
        await using var db=app.CreateDb();Assert.Contains("monthly",(await db.LedgerCommands.Where(t=>t.Kind=="transaction.selection").OrderByDescending(t=>t.Version).FirstAsync()).AfterJson);
    }

    [Fact]
    public async Task Batch_failure_rolls_back_and_concurrent_commands_accept_one_revision()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var a=await Account(c,"Checking");var one=await Entry(c,a);var two=await Entry(c,a);
        var command=await Command(c,"duplicate",one,two);app.Failure.FailAuditSave=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>c.PostAsJsonAsync("/api/transactions/selection",command));Assert.Equal(3,(await Register(c)).Total);Assert.Equal(command.Version,(await Accounts(c)).Version);
        var results=await Task.WhenAll(c.PostAsJsonAsync("/api/transactions/selection",command),app.CreateClient().PostAsJsonAsync("/api/transactions/selection",command with{CommandId=Guid.NewGuid()}));
        Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(5,(await Register(c)).Total);Assert.Equal(60,(await Accounts(c)).Accounts.Single().Balance);
    }
}
