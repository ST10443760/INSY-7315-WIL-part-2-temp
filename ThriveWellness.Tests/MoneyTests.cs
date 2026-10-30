using System.Globalization;
using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("0", "R0.00")]
    [InlineData("120", "R120.00")]
    [InlineData("450.5", "R450.50")]
    [InlineData("1234", "R1 234.00")]
    [InlineData("1234567.891", "R1 234 567.89")]
    [InlineData("-120", "-R120.00")]
    public void Format_UsesNonBreakingSpacesForThousandsAndAFullStopForCents(string amount, string expected)
    {
        Assert.Equal(expected, Money.Format(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Format_IsTheSameWhateverTheMachinesCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-ZA");
            var southAfrican = Money.Format(1234.5m);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var german = Money.Format(1234.5m);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = Money.Format(1234.5m);

            Assert.Equal("R1 234.50", southAfrican);
            Assert.Equal(southAfrican, german);
            Assert.Equal(southAfrican, invariant);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ForCsv_IsAPlainNumber()
    {
        Assert.Equal("1234.50", Money.ForCsv(1234.5m));
    }
}
