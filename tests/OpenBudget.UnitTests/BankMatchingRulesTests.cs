using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class BankMatchingRulesTests
{
    [Fact]
    public void PayeeSimilarity_IgnoresCaseAndPunctuation()
    {
        Assert.Equal(1m, BankMatchingRules.PayeeSimilarity("Coffee-Shop 123", "coffee shop 123"));
    }

    [Fact]
    public void CandidateScore_RequiresSameAmountAndNearDate()
    {
        var date = new DateOnly(2026, 11, 3);
        Assert.True(BankMatchingRules.CandidateScore(date, -14.50m, "Albert Heijn", date.AddDays(1), -14.50m, "ALBERTHEIJN NL") >= 0.76m);
        Assert.Equal(0m, BankMatchingRules.CandidateScore(date, -14.50m, "Albert Heijn", date, -14.49m, "Albert Heijn"));
        Assert.Equal(0m, BankMatchingRules.CandidateScore(date, -14.50m, "Albert Heijn", date.AddDays(5), -14.50m, "Albert Heijn"));
    }

    [Fact]
    public void CandidateScore_DoesNotSuggestUnrelatedPayeeEvenWithExactAmount()
    {
        var date = new DateOnly(2026, 11, 3);
        Assert.Equal(0m, BankMatchingRules.CandidateScore(date, -10m, "Coffee shop", date, -10m, "Train ticket"));
    }
}
