using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public class PayeeRulesTests
{
    [Theory]
    [InlineData("is", " Power Co ", "power co", true)]
    [InlineData("is", "Power", "Power Co", false)]
    [InlineData("contains", "POWER", "Direct debit Power Co 123", true)]
    [InlineData("contains", "a.b", "axb", false)]
    [InlineData("contains", "a.b", "X a.b X", true)]
    [InlineData("contains", "", "other", false)]
    [InlineData("regex", "Power", "Power", false)]
    public void Matching_is_literal_case_insensitive_and_operator_specific(string op, string value, string imported, bool expected)
        => Assert.Equal(expected, PayeeRules.Matches(new(op,value),imported));

    [Fact]
    public void History_uses_three_entries_majority_then_recency_including_uncategorized()
    {
        Assert.Null(PayeeRules.HistoryCategory([]));
        Assert.Equal("food",PayeeRules.HistoryCategory(["rent","food","food","rent","rent"]));
        Assert.Equal("rent",PayeeRules.HistoryCategory(["rent","food","travel"]));
        Assert.Null(PayeeRules.HistoryCategory([null,null,"food"]));
    }

    [Theory]
    [InlineData("", "history", null, "is", "abc")]
    [InlineData("Name", "invalid", null, "is", "abc")]
    [InlineData("Name", "always", null, "is", "abc")]
    [InlineData("Name", "history", null, "regex", "abc")]
    [InlineData("Name", "history", null, "contains", " ")]
    public void Invalid_settings_are_rejected(string name,string mode,string? category,string op,string text)
        => Assert.NotNull(PayeeRules.Validate(new(Guid.NewGuid(),name,false,mode,category,[new(op,text)])));

    [Fact]
    public void Limits_and_valid_defaults_are_explicit()
    {
        var p=new PayeeUpdateDto(Guid.NewGuid(),new string('a',160),false,"always","food",[new("is",new string('b',160))]);
        Assert.Null(PayeeRules.Validate(p));Assert.NotNull(PayeeRules.Validate(p with{Name=p.Name+"a"}));
        Assert.NotNull(PayeeRules.Validate(p with{Rules=[new("is",new string('b',161))]}));
        Assert.NotNull(PayeeRules.Validate(p with{Rules=Enumerable.Repeat(new PayeeRuleDto("is","a"),51).ToList()}));
    }
}
