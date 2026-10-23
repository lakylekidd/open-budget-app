using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class EntitlementKeyTests
{
    [Theory]
    [InlineData("ledger.export")]
    [InlineData("feature_v2")]
    [InlineData("a")]
    public void Accepts_lowercase_feature_keys(string key) => Assert.True(EntitlementService.IsValidKey(key));

    [Theory]
    [InlineData("")]
    [InlineData("Ledger.Export")]
    [InlineData("1feature")]
    [InlineData("feature name")]
    [InlineData("feature/")]
    [InlineData(null)]
    public void Rejects_invalid_feature_keys(string? key) => Assert.False(EntitlementService.IsValidKey(key));
}
