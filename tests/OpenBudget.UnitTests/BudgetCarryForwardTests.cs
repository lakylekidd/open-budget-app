using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public class BudgetCarryForwardTests
{
    private static BudgetEntity Month(string month, decimal assigned=0, decimal activity=0, decimal opening=0) => new() {
        Month=month,OpeningReadyToAssign=500,Groups=[new(){Id="home",Name="Home",BudgetMonth=month,
            Categories=[new(){Id="utilities",Name="Electricity",GroupId="home",BudgetMonth=month,Assigned=assigned,Activity=activity,OpeningAvailable=opening}]}] };
    private static CategoryEntity Electricity(BudgetEntity month)=>month.Groups.SelectMany(g=>g.Categories).Single(c=>c.Id=="utilities");
    private static BudgetCarryForward.Changes Change(BudgetEntity source,Action mutate){
        var ready=BudgetService.CalculateReadyToAssign(source);var available=source.Groups.SelectMany(g=>g.Categories).ToDictionary(c=>c.Id,c=>c.Available);
        mutate();return BudgetCarryForward.CalculateChanges(ready,BudgetService.CalculateReadyToAssign(source),available,source.Groups.SelectMany(g=>g.Categories));
    }
    private static void Carry(BudgetEntity source,BudgetEntity target,Action mutate){var change=Change(source,mutate);BudgetCarryForward.Apply(target,change.ReadyToAssignDelta,change.AvailableDeltas,source);}

    [Fact]
    public void Fifty_assigned_minus_thirty_five_spent_then_fifty_more_is_sixty_five()
    {
        var august=Month("2026-08");var september=Month("2026-09");
        Carry(august,september,()=>Electricity(august).Assigned=50);
        Carry(august,september,()=>Electricity(august).Activity=-35);
        Assert.Equal(15,Electricity(september).Available);Assert.Equal(0,Electricity(september).Assigned);
        Electricity(september).Assigned=50;Assert.Equal(65,Electricity(september).Available);Assert.Equal(15,Electricity(august).Available);
    }

    [Theory]
    [InlineData(50,35,15)]
    [InlineData(50,70,-20)]
    [InlineData(0,0,0)]
    [InlineData(0.30,0.10,0.20)]
    [InlineData(-10,5,-15)]
    public void Decimal_positive_zero_and_overspending_changes_are_split_between_category_and_ready_to_assign(decimal assigned,decimal spent,decimal expected)
    {
        var source=Month("2026-08");var target=Month("2027-01");
        Carry(source,target,()=>{Electricity(source).Assigned=assigned;Electricity(source).Activity=-spent;});
        Assert.Equal(Math.Max(expected,0),Electricity(target).Available);
        Assert.Equal(500-assigned+Math.Min(expected,0),BudgetService.CalculateReadyToAssign(target));
    }

    [Fact]
    public void Crossing_zero_removes_the_positive_carry_and_withholds_only_the_uncovered_amount()
    {
        var source=Month("2026-08",opening:10);var change=Change(source,()=>Electricity(source).Activity=-15);
        Assert.Equal(-10,change.AvailableDeltas["utilities"]);Assert.Equal(-5,change.ReadyToAssignDelta);
        Assert.Equal(0,BudgetCarryForward.OpeningAvailable(Electricity(source).Available));
        Assert.Equal(495,BudgetCarryForward.OpeningReadyToAssign(source));
    }

    [Fact]
    public void Assigning_money_to_cover_prior_overspending_does_not_charge_the_next_month_twice()
    {
        var source=Month("2026-08",activity:-20);var change=Change(source,()=>Electricity(source).Assigned=20);
        Assert.Equal(0,change.AvailableDeltas["utilities"]);Assert.Equal(0,change.ReadyToAssignDelta);
    }

    [Fact]
    public void Later_explicit_assignments_activity_metadata_and_legacy_openings_are_preserved()
    {
        var source=Month("2026-08");var target=Month("2026-09",30,-7,12);target.Note="Keep this";Electricity(target).Name="Later name";Electricity(target).IsArchived=true;
        BudgetCarryForward.Apply(target,-10,new Dictionary<string,decimal>{{"utilities",10}},source);
        Assert.Equal(45,Electricity(target).Available);Assert.Equal(30,Electricity(target).Assigned);Assert.Equal(-7,Electricity(target).Activity);
        Assert.Equal("Later name",Electricity(target).Name);Assert.True(Electricity(target).IsArchived);Assert.Equal("Keep this",target.Note);
    }

    [Fact]
    public void Missing_category_and_group_are_cloned_for_carry_without_copying_source_assignment_or_activity()
    {
        var source=Month("2026-08",50,-35);Electricity(source).Target=80;
        var target=new BudgetEntity{Month="2026-09",Groups=[new(){Id="other",Name="Other",SortOrder=0}]};
        BudgetCarryForward.Apply(target,-50,new Dictionary<string,decimal>{{"utilities",15}},source);
        var clone=Electricity(target);Assert.Equal(15,clone.OpeningAvailable);Assert.Equal(0,clone.Assigned);Assert.Equal(0,clone.Activity);
        Assert.Equal(80,clone.Target);Assert.Equal("2026-09",clone.BudgetMonth);Assert.NotSame(Electricity(source),clone);Assert.Equal(2,target.Groups.Select(g=>g.SortOrder).Distinct().Count());
    }

    [Fact]
    public void Zero_delta_is_a_noop_and_does_not_create_absent_categories()
    {
        var source=Month("2026-08");var target=new BudgetEntity{Month="2026-09"};
        Assert.False(BudgetCarryForward.Apply(target,0,new Dictionary<string,decimal>{{"utilities",0}},source));Assert.Empty(target.Groups);
    }

    [Fact]
    public void Many_signed_cent_changes_match_the_independent_total_without_rounding_drift()
    {
        var source=Month("2026-08");var targets=Enumerable.Range(1,24).Select(i=>Month(new DateOnly(2026,8,1).AddMonths(i).ToString("yyyy-MM"),i,-i/10m)).ToArray();
        for(var i=0;i<1000;i++){
            var delta=((i*37)%199-99)/100m;var change=Change(source,()=>Electricity(source).Activity+=delta);
            foreach(var target in targets)BudgetCarryForward.Apply(target,change.ReadyToAssignDelta,change.AvailableDeltas,source);
        }
        var carried=Math.Max(Electricity(source).Available,0);var withheld=Math.Min(Electricity(source).Available,0);
        for(var i=0;i<targets.Length;i++){Assert.Equal(carried+(i+1)-(i+1)/10m,Electricity(targets[i]).Available);Assert.Equal(500+withheld-(i+1),BudgetService.CalculateReadyToAssign(targets[i]));}
    }
}
