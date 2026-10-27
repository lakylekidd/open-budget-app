using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class BudgetCarryForwardApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string August="2026-08", September="2026-09", October="2026-10";
    private static Task<BudgetDto> Month(HttpClient c,string month)=>Get<BudgetDto>(c,"/api/budget/"+month);
    private static CategoryDto Electricity(BudgetDto b,string id="utilities")=>b.Groups.SelectMany(g=>g.Categories).Single(c=>c.Id==id);
    private static Task<AccountsDto> Accounts(HttpClient c)=>Get<AccountsDto>(c,"/api/accounts");
    private static async Task<T> Get<T>(HttpClient c,string path)=>await Read<T>(await c.GetAsync(path));
    private static async Task<T> Read<T>(HttpResponseMessage response){Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<T> Put<T>(HttpClient c,string path,object body)=>await Read<T>(await c.PutAsJsonAsync(path,body));
    private static async Task<T> Post<T>(HttpClient c,string path,object body)=>await Read<T>(await c.PostAsJsonAsync(path,body));
    private static BudgetUpdateDto Editable(BudgetDto b)=>new(b.Month,b.Note,b.Version,b.FundsAdded,b.Groups.Select(g=>new CategoryGroupUpdateDto(g.Id,g.Name,g.SortOrder,g.IsArchived,
        g.Categories.Select(c=>new CategoryUpdateDto(c.Id,c.Name,c.Assigned,c.Target,c.Icon,c.Snoozed,c.SortOrder,c.IsArchived)).ToList())).ToList());
    private static BudgetUpdateDto WithAssignment(BudgetDto b,decimal amount,string id="utilities"){
        var e=Editable(b);return e with{Groups=e.Groups.Select(g=>g with{Categories=g.Categories.Select(c=>c.Id==id?c with{Assigned=amount}:c).ToList()}).ToList()};
    }
    private static async Task<BudgetDto> Assign(HttpClient c,string month,decimal amount,string id="utilities")=>await Put<BudgetDto>(c,"/api/budget/"+month,WithAssignment(await Month(c,month),amount,id));
    private static async Task<Guid> Account(HttpClient c,DateOnly? date=null,decimal amount=500)=>
        (await Post<MutationDto>(c,"/api/accounts",new CreateAccountRequest(Guid.NewGuid(),(await Accounts(c)).Version,"Checking",date??new DateOnly(2026,8,1),amount))).Id;
    private static async Task<TransactionRequest> Row(HttpClient c,Guid account,DateOnly date,decimal amount,string category="utilities")=>
        new(Guid.NewGuid(),(await Accounts(c)).Version,account,date,amount,"easyEnergy","Electricity",category,"uncleared",[]);
    private static async Task<Guid> Spend(HttpClient c,Guid account,DateOnly date,decimal amount,string category="utilities")=>
        (await Post<MutationDto>(c,"/api/transactions",await Row(c,account,date,amount,category))).Id;
    private static async Task Delete(HttpClient c,Guid id)=>await Post<MutationDto>(c,$"/api/transactions/{id}/delete",new DeleteTransactionRequest(Guid.NewGuid(),(await Accounts(c)).Version));
    private static async Task AssertAvailable(HttpClient c,string month,decimal available,decimal assigned,string category="utilities"){
        var budget=await Month(c,month);Assert.Equal(available,Electricity(budget,category).Available);Assert.Equal(assigned,Electricity(budget,category).Assigned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_August_September_example_works_before_or_after_later_months_exist_and_survives_restart(bool precreated)
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);
        if(precreated){await Month(c,September);await Month(c,October);}
        await Assign(c,August,50);await Spend(c,account,new(2026,8,15),-35);
        await AssertAvailable(c,August,15,50);await AssertAvailable(c,September,15,0);
        await Assign(c,September,50);await AssertAvailable(c,September,65,50);await AssertAvailable(c,October,65,0);
        await using var restarted=new TestApp(app.ConnectionString);var read=restarted.CreateClient();
        for(var i=0;i<3;i++){await AssertAvailable(read,August,15,50);await AssertAvailable(read,September,65,50);await AssertAvailable(read,October,65,0);}
        Assert.Equal(400,(await Month(read,October)).ReadyToAssign);Assert.Equal(465,Assert.Single((await Accounts(read)).Accounts).Balance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uncovered_overspending_resets_the_next_category_and_withholds_the_shortfall_from_ready_to_assign(bool precreated)
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);
        if(precreated){await Month(c,September);await Month(c,October);}
        var spend=await Spend(c,account,new(2026,8,15),-75);
        await AssertAvailable(c,August,-75,0);await AssertAvailable(c,September,0,0);await AssertAvailable(c,October,0,0);
        Assert.Equal(425,(await Month(c,September)).ReadyToAssign);Assert.Equal(425,(await Month(c,October)).ReadyToAssign);

        await Put<MutationDto>(c,$"/api/transactions/{spend}",await Row(c,account,new(2026,8,15),-50));
        await AssertAvailable(c,September,0,0);Assert.Equal(450,(await Month(c,September)).ReadyToAssign);
        await Assign(c,August,20);await AssertAvailable(c,August,-30,20);await AssertAvailable(c,September,0,0);
        Assert.Equal(450,(await Month(c,September)).ReadyToAssign);
        await Assign(c,August,80);await AssertAvailable(c,August,30,80);await AssertAvailable(c,September,30,0);
        Assert.Equal(420,(await Month(c,September)).ReadyToAssign);
    }

    [Fact]
    public async Task Increasing_decreasing_and_removing_past_assignments_preserves_each_later_assignment_across_gaps_and_years()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);
        foreach(var month in new[]{September,October,"2026-12","2027-01"})await Month(c,month);
        await Assign(c,August,50);await Spend(c,account,new(2026,8,15),-35);await Assign(c,September,50);await Assign(c,October,10);
        foreach(var (amount,sep,oct) in new[]{(80m,95m,105m),(20m,50m,60m),(0m,50m,60m),(50m,65m,75m)}){
            await Assign(c,August,amount);await AssertAvailable(c,August,amount-35,amount);await AssertAvailable(c,September,sep,50);
            await AssertAvailable(c,October,oct,10);await AssertAvailable(c,"2027-01",oct,0);
        }
        await Assign(c,September,30);await AssertAvailable(c,"2027-01",55,0);
        var later=await Month(c,"2027-01");var august=await Month(c,August);await Put<BudgetDto>(c,"/api/budget/"+August,Editable(august) with{Note="Historical note only"});
        Assert.Equivalent(later,await Month(c,"2027-01"),strict:true);Assert.Equal(410,later.ReadyToAssign);
    }

    [Fact]
    public async Task Backdated_amount_date_category_refund_delete_and_transfer_changes_reconcile_with_assignment_rollover()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);
        await Month(c,September);await Month(c,October);await Assign(c,August,50);await Assign(c,September,50);await Assign(c,October,10);
        var spend=await Spend(c,account,new(2026,8,15),-35);await AssertAvailable(c,October,75,10);
        await Put<MutationDto>(c,$"/api/transactions/{spend}",await Row(c,account,new(2026,8,15),-45));await AssertAvailable(c,October,65,10);
        var refund=await Spend(c,account,new(2026,8,20),5);await AssertAvailable(c,October,70,10);
        await Put<MutationDto>(c,$"/api/transactions/{spend}",await Row(c,account,new(2026,8,15),-45,"transport"));await AssertAvailable(c,October,115,10);await AssertAvailable(c,October,0,0,"transport");Assert.Equal(345,(await Month(c,October)).ReadyToAssign);
        await Put<MutationDto>(c,$"/api/transactions/{spend}",await Row(c,account,new(2026,9,15),-45));await AssertAvailable(c,August,55,50);await AssertAvailable(c,September,60,50);await AssertAvailable(c,October,70,10);await AssertAvailable(c,October,0,0,"transport");
        var reserve=await Account(c,amount:0);await Post<MutationDto>(c,"/api/transfers",new TransferRequest(Guid.NewGuid(),(await Accounts(c)).Version,account,reserve,new(2026,8,25),20,"Internal transfer"));await AssertAvailable(c,October,70,10);Assert.Equal(390,(await Month(c,October)).ReadyToAssign);
        await Delete(c,spend);await AssertAvailable(c,October,115,10);await Delete(c,refund);await AssertAvailable(c,October,110,10);
    }

    [Fact]
    public async Task Historical_new_categories_carry_through_missing_later_rows_without_changing_later_archive_metadata()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);
        foreach(var month in new[]{September,"2026-11","2027-01"})await Month(c,month);
        var edit=Editable(await Month(c,August));edit.Groups.Add(new("energy","Energy",edit.Groups.Max(g=>g.SortOrder)+1,false,[new("retro-energy","Electricity",0,0,null,false,0,false)]));
        await Put<BudgetDto>(c,"/api/budget/"+August,edit);await Assign(c,August,50,"retro-energy");await Spend(c,account,new(2026,8,15),-35,"retro-energy");
        foreach(var month in new[]{September,"2026-11","2027-01"})await AssertAvailable(c,month,15,0,"retro-energy");
        var november=await Assign(c,"2026-11",5,"retro-energy");edit=Editable(november);edit=edit with{Groups=edit.Groups.Select(g=>g.Id=="energy"?g with{Name="Later energy label",Categories=g.Categories.Select(cat=>cat with{IsArchived=true}).ToList()}:g).ToList()};
        await Put<BudgetDto>(c,"/api/budget/2026-11",edit);await Assign(c,August,80,"retro-energy");
        november=await Month(c,"2026-11");Assert.Equal(50,Electricity(november,"retro-energy").Available);Assert.True(Electricity(november,"retro-energy").IsArchived);Assert.Equal("Later energy label",november.Groups.Single(g=>g.Id=="energy").Name);
        await AssertAvailable(c,October,45,0,"retro-energy");await Assign(c,October,2,"retro-energy");await AssertAvailable(c,"2027-01",52,0,"retro-energy");
    }

    [Fact]
    public async Task Invalid_and_failed_assignment_saves_rollback_source_future_and_implicit_month_initialization()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();await Account(c);await Month(c,September);await Assign(c,August,50);
        var before=await Month(c,August);var later=await Month(c,September);var invalid=WithAssignment(before,200);invalid.Groups[0].Categories.Add(new("invalid-new","Invalid",1,0,null,false,100,false));
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync("/api/budget/"+August,invalid)).StatusCode);Assert.Equivalent(before,await Month(c,August),strict:true);Assert.Equivalent(later,await Month(c,September),strict:true);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync("/api/budget/2026-07",invalid with{Month="2026-07",Version=1})).StatusCode);
        await using(var db=app.CreateDb())Assert.False(await db.Budgets.AnyAsync(b=>b.Month=="2026-07"));
        var valid=WithAssignment(before,80);app.Failure.FailBudgetAfterSave=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>c.PutAsJsonAsync("/api/budget/"+August,valid));
        Assert.Equivalent(before,await Month(c,August),strict:true);Assert.Equivalent(later,await Month(c,September),strict:true);
        await Put<BudgetDto>(c,"/api/budget/"+August,valid);await AssertAvailable(c,September,80,0);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync("/api/budget/"+August,valid)).StatusCode);await AssertAvailable(c,September,80,0);
    }

    [Fact]
    public async Task Stale_and_concurrent_edits_cannot_overwrite_changed_carry_or_later_explicit_assignments()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();await Account(c);await Month(c,September);await Assign(c,August,50);
        var stale=WithAssignment(await Month(c,September),50);await Assign(c,August,70);
        var conflict=await c.PutAsJsonAsync("/api/budget/"+September,stale);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);var json=await conflict.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();Assert.Equal(70,json.GetProperty("current").GetProperty("groups")[0].GetProperty("categories")[1].GetProperty("available").GetDecimal());
        await AssertAvailable(c,September,70,0);await Assign(c,September,50);
        var august=WithAssignment(await Month(c,August),80);var september=WithAssignment(await Month(c,September),60);
        var results=await Task.WhenAll(c.PutAsJsonAsync("/api/budget/"+August,august),app.CreateClient().PutAsJsonAsync("/api/budget/"+September,september));Assert.True(results[0].IsSuccessStatusCode);
        if(results[1].StatusCode==HttpStatusCode.Conflict)await Assign(c,September,60);else Assert.True(results[1].IsSuccessStatusCode);
        await AssertAvailable(c,August,80,80);await AssertAvailable(c,September,140,60);Assert.Equal(360,(await Month(c,September)).ReadyToAssign);
    }

    [Fact]
    public async Task Concurrent_backdated_spending_and_assignment_change_preserve_activity_and_rollover()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);await Assign(c,August,50);await Assign(c,September,50);
        var august=WithAssignment(await Month(c,August),80);var spend=await Row(c,account,new(2026,8,15),-35);
        var results=await Task.WhenAll(c.PutAsJsonAsync("/api/budget/"+August,august),app.CreateClient().PostAsJsonAsync("/api/transactions",spend));Assert.True(results[1].IsSuccessStatusCode);
        if(results[0].StatusCode==HttpStatusCode.Conflict)await Assign(c,August,80);else Assert.True(results[0].IsSuccessStatusCode);
        await AssertAvailable(c,August,45,80);await AssertAvailable(c,September,95,50);Assert.Equal(-35,Electricity(await Month(c,August)).Activity);Assert.Equal(0,Electricity(await Month(c,September)).Activity);
    }

    [Fact]
    public async Task Moving_a_historical_assignment_between_categories_preserves_ready_to_assign_and_carries_both_changes_once()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c);await Month(c,September);await Assign(c,August,50);await Assign(c,September,50);await Spend(c,account,new(2026,8,15),-35);
        var before=await Month(c,September);var edit=WithAssignment(await Month(c,August),40);
        edit=edit with{Groups=edit.Groups.Select(g=>g with{Categories=g.Categories.Select(cat=>cat.Id=="groceries"?cat with{Assigned=10}:cat).ToList()}).ToList()};
        await Put<BudgetDto>(c,"/api/budget/"+August,edit);var after=await Month(c,September);
        Assert.Equal(55,Electricity(after).Available);Assert.Equal(50,Electricity(after).Assigned);Assert.Equal(10,Electricity(after,"groceries").Available);Assert.Equal(0,Electricity(after,"groceries").Assigned);Assert.Equal(before.ReadyToAssign,after.ReadyToAssign);Assert.Equal(before.Version+1,after.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync("/api/budget/"+August,edit)).StatusCode);Assert.Equivalent(after,await Month(c,September),strict:true);
    }

    [Fact]
    public async Task Decimal_rollover_matches_an_independent_prefix_sum_with_reverse_initialization_leap_day_and_many_past_edits()
    {
        await using var app=await fixture.CreateAppAsync();var c=app.CreateClient();var account=await Account(c,new(2027,12,1));
        string[] months=["2027-12","2028-01","2028-02","2028-03","2028-06","2029-01"];
        foreach(var month in months.Reverse())await Month(c,month);
        var assigned=new decimal[months.Length];var activity=new decimal[months.Length];
        for(var i=0;i<months.Length;i++){
            assigned[i]=(i+1)*10.01m;await Assign(c,months[i],assigned[i]);activity[i]=-(i+0.07m);
            var date=i==2?new DateOnly(2028,2,29):DateOnly.Parse(months[i]+"-15");await Spend(c,account,date,activity[i]);
        }
        for(var pass=0;pass<12;pass++){
            var changed=(pass*5)%months.Length;assigned[changed]=(pass*17+3)/100m;await Assign(c,months[changed],assigned[changed]);
            decimal available=0,readyToAssign=500;
            for(var i=0;i<months.Length;i++){
                if(i>0){readyToAssign+=Math.Min(available,0);available=Math.Max(available,0);}
                readyToAssign-=assigned[i];available+=assigned[i]+activity[i];var actual=await Month(c,months[i]);
                Assert.Equal(available,Electricity(actual).Available);Assert.Equal(assigned[i],Electricity(actual).Assigned);Assert.Equal(activity[i],Electricity(actual).Activity);Assert.Equal(readyToAssign,actual.ReadyToAssign);
                Assert.Equivalent(actual,await Month(c,months[i]),strict:true);
            }
        }
    }
}
