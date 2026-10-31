using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public class AccountOrderRulesTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid Foreign = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    [Fact]
    public void Accepts_any_permutation_of_the_budget_accounts()
    {
        Assert.Null(AccountOrderRules.Validate([A, B, C], [C, A, B]));
        Assert.Null(AccountOrderRules.Validate([A, B, C], [A, B, C]));
        Assert.Null(AccountOrderRules.Validate([], []));
    }

    public static TheoryData<Guid[]?> InvalidOrders => new()
    {
        null,
        new Guid[] { A, B },
        new Guid[] { A, B, C, C },
        new Guid[] { A, B, B },
        new Guid[] { A, B, Foreign },
        new Guid[] { A, B, C, Foreign },
        new Guid[] { },
    };

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public void Rejects_missing_duplicate_foreign_or_extra_ids(Guid[]? submitted)
        => Assert.Equal(AccountOrderRules.InvalidOrder, AccountOrderRules.Validate([A, B, C], submitted));

    [Fact]
    public void New_accounts_append_after_the_highest_existing_position()
    {
        Assert.Equal(0, AccountOrderRules.NextSortOrder([]));
        Assert.Equal(3, AccountOrderRules.NextSortOrder([0, 1, 2]));
        Assert.Equal(8, AccountOrderRules.NextSortOrder([7, 0, 3]));
    }
}
