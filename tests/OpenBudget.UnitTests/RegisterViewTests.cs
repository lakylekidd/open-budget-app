using OpenBudget.Api;

namespace OpenBudget.UnitTests;
public sealed class RegisterViewTests
{
    [Theory]
    [InlineData("custom", "2024-02", "2024-02", true)]
    [InlineData("custom", "1900-01", "9998-12", true)]
    [InlineData("custom", "2026-12", "2026-01", false)]
    [InlineData("custom", null, "2026-01", false)]
    [InlineData("allDates", null, null, true)]
    [InlineData("thisMonth", null, null, true)]
    [InlineData("lastMonth", null, null, true)]
    [InlineData("latest3Months", null, null, true)]
    [InlineData("thisYear", null, null, true)]
    [InlineData("lastYear", null, null, true)]
    [InlineData("allDates", "2026-13", null, false)]
    [InlineData("allDates", "1899-12", null, false)]
    [InlineData("allDates", "2026-1", null, false)]
    [InlineData("arbitrary", null, null, false)]
    public void Validates_preset_months_and_boundaries(string preset, string? from, string? to, bool valid)
    {
        var value = new RegisterViewDto(0, preset, from, to, true, false, false, false);
        var error = Record.Exception(() => RegisterViewService.Validate(value));
        if (valid) Assert.Null(error); else Assert.IsType<LedgerException>(error);
    }
    [Theory]
    [InlineData(null, true)]
    [InlineData("red", true)]
    [InlineData("orange", true)]
    [InlineData("yellow", true)]
    [InlineData("green", true)]
    [InlineData("blue", true)]
    [InlineData("purple", true)]
    [InlineData("pink", false)]
    public void Flag_colors_are_fixed_but_can_be_cleared(string? color, bool valid)
    {
        var error=Record.Exception(()=>LedgerService.ValidateFlagColor(color));
        if(valid)Assert.Null(error);else Assert.IsType<LedgerException>(error);
    }
}
