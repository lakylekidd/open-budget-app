using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class PlanActionRulesTests
{
    private static CategoryEntity Category(string id,string name,decimal assigned)=>new(){Id=id,Name=name,Assigned=assigned};

    [Fact]
    public void Equal_opposite_assignment_changes_are_one_move()
    {
        var previous=new Dictionary<string,(string Name,decimal Assigned)>{{"rent",("Rent",100)},{"food",("Food",20)}};
        var action=Assert.Single(PlanActionRules.Describe(previous,[Category("rent","Rent",70),Category("food","Food",50)]));
        Assert.Equal(new PlanActionDraft("moved",30,"Rent","Food"),action);
    }

    [Fact]
    public void Independent_assignment_and_removal_keep_amount_and_category()
    {
        var previous=new Dictionary<string,(string Name,decimal Assigned)>{{"rent",("Rent",100)},{"food",("Food",20)}};
        var actions=PlanActionRules.Describe(previous,[Category("rent","Rent",90),Category("food","Food",55)]);
        Assert.Contains(new PlanActionDraft("removed",10,"Rent",null),actions);
        Assert.Contains(new PlanActionDraft("assigned",35,null,"Food"),actions);
    }

    [Fact]
    public void Metadata_only_save_is_recorded_as_an_update()
    {
        var previous=new Dictionary<string,(string Name,decimal Assigned)>{{"rent",("Rent",100)}};
        Assert.Equal(new PlanActionDraft("updated",null,null,null),Assert.Single(PlanActionRules.Describe(previous,[Category("rent","Housing",100)])));
    }
}
