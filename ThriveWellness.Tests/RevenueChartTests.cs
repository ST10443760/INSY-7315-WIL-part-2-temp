using ThriveWellness.Models;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class RevenueChartTests
{
    private int _nextId = 1;

    private RevenuePaymentRow Paid(decimal amount, DateTime classDate) =>
        new(_nextId++, 1, "Client", "c@example.com", amount, PaymentStatuses.Confirmed, PaymentMethods.Eft, "per-class", "Sunninghill", classDate);

    private static readonly RevenueRange AllTime = new(RevenuePreset.AllTime, null, null);

    // Scale

    [Fact]
    public void Scale_LargeRange_UsesRoundThousands()
    {
        var scale = ChartScale.For(0m, 9300m);

        Assert.Equal(new[] { 0m, 2000m, 4000m, 6000m, 8000m, 10000m }, scale.Ticks);
    }

    [Fact]
    public void Scale_SmallRange_UsesSmallRoundSteps()
    {
        var scale = ChartScale.For(0m, 450m);

        Assert.Equal(100m, scale.Step);
        Assert.Equal(0m, scale.Min);
        Assert.Equal(500m, scale.Max);
    }

    [Fact]
    public void Scale_AllPositive_StartsAtZero()
    {
        var scale = ChartScale.For(1200m, 1500m);

        Assert.Equal(0m, scale.Ticks[0]);
        Assert.True(scale.Max >= 1500m);
    }

    [Fact]
    public void Scale_NegativeMinimum_ReachesBelowZeroAndKeepsZero()
    {
        var scale = ChartScale.For(-240m, 960m);

        Assert.True(scale.Min <= -240m);
        Assert.True(scale.Max >= 960m);
        Assert.Contains(0m, scale.Ticks);
        Assert.Equal(-500m, scale.Min);
    }

    [Fact]
    public void Scale_AllZero_StillHasAnAxis()
    {
        var scale = ChartScale.For(0m, 0m);

        Assert.True(scale.Max > scale.Min);
        Assert.Contains(0m, scale.Ticks);
    }

    [Theory]
    [InlineData("1860", "2000")]
    [InlineData("230", "500")]
    [InlineData("100", "100")]
    [InlineData("0.37", "0.5")]
    public void NiceStep_RoundsUpToOneTwoOrFive(string rough, string expected)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(decimal.Parse(expected, invariant), ChartScale.NiceStep(decimal.Parse(rough, invariant)));
    }

    // Bucketing

    [Fact]
    public void Series_Weekly_GroupsMondayToSundayAndFillsEmptyWeeks()
    {
        var rows = new[]
        {
            Paid(120m, new DateTime(2026, 10, 5)),  // Monday
            Paid(450m, new DateTime(2026, 10, 11)), // Sunday, same week
            Paid(120m, new DateTime(2026, 10, 26))  // two empty weeks later
        };

        var series = RevenueService.BuildSeries(rows, AllTime, RevenueGrouping.Weekly);

        Assert.Equal(
            new[] { new DateTime(2026, 10, 5), new DateTime(2026, 10, 12), new DateTime(2026, 10, 19), new DateTime(2026, 10, 26) },
            series.Select(p => p.PeriodStart));
        Assert.Equal(new[] { 570m, 0m, 0m, 120m }, series.Select(p => p.Amount));
        Assert.Equal("Week of 5 Oct 2026", series[0].LongLabel);
    }

    [Fact]
    public void Series_Monthly_GroupsByCalendarMonthAndFillsEmptyMonths()
    {
        var rows = new[]
        {
            Paid(120m, new DateTime(2026, 8, 31)),
            Paid(450m, new DateTime(2026, 10, 1)),
            Paid(120m, new DateTime(2026, 10, 31))
        };

        var series = RevenueService.BuildSeries(rows, AllTime, RevenueGrouping.Monthly);

        Assert.Equal(new[] { "Aug 26", "Sep 26", "Oct 26" }, series.Select(p => p.Label));
        Assert.Equal(new[] { 120m, 0m, 570m }, series.Select(p => p.Amount));
    }

    [Fact]
    public void Series_CoversTheWholeSelectedRangeNotJustTheDataDates()
    {
        var range = new RevenueRange(RevenuePreset.Custom, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        var series = RevenueService.BuildSeries(new[] { Paid(120m, new DateTime(2026, 10, 14)) }, range, RevenueGrouping.Weekly);

        Assert.Equal(new DateTime(2026, 9, 28), series[0].PeriodStart);
        Assert.Equal(new DateTime(2026, 10, 26), series[^1].PeriodStart);
        Assert.Equal(5, series.Count);
        Assert.Equal(120m, series.Sum(p => p.Amount));
    }

    [Fact]
    public void Series_NoPayments_IsEmpty()
    {
        Assert.Empty(RevenueService.BuildSeries(Array.Empty<RevenuePaymentRow>(), AllTime, RevenueGrouping.Weekly));
    }

    [Fact]
    public void Report_SeriesTotal_EqualsNetRevenue()
    {
        var rows = new[]
        {
            Paid(120m, new DateTime(2026, 9, 3)),
            Paid(450m, new DateTime(2026, 10, 14)),
            new RevenuePaymentRow(99, 2, "Refunded", "r@example.com", 300m, PaymentStatuses.Refunded, PaymentMethods.Card, "per-class", "Kyalami", new DateTime(2026, 10, 15))
        };

        foreach (var grouping in new[] { RevenueGrouping.Weekly, RevenueGrouping.Monthly })
        {
            var report = RevenueService.Build(rows, AllTime, grouping);
            Assert.Equal(report.NetRevenue, report.Series.Sum(p => p.Amount));
            Assert.Equal(grouping, report.Grouping);
        }
    }

    // Geometry

    private static RevenuePoint Point(decimal amount, int day) =>
        new(new DateTime(2026, 10, day), $"{day} Oct", $"Week of {day} Oct 2026", amount);

    [Fact]
    public void Geometry_SinglePoint_HasAMarkerButNoLine()
    {
        var chart = RevenueChart.Build(new[] { Point(450m, 5) }, 800, 300, 8);

        Assert.Single(chart.Points);
        Assert.Equal(string.Empty, chart.LinePath);
        Assert.Equal(string.Empty, chart.AreaPath);
        Assert.Equal((chart.PlotLeft + chart.PlotRight) / 2, chart.Points[0].X);
    }

    [Fact]
    public void Geometry_SeveralPoints_DrawsALineLeftToRightAndHigherAmountsHigher()
    {
        var chart = RevenueChart.Build(new[] { Point(100m, 5), Point(900m, 12), Point(0m, 19) }, 800, 300, 8);

        Assert.StartsWith("M", chart.LinePath);
        Assert.EndsWith("Z", chart.AreaPath);
        Assert.Equal(chart.PlotLeft, chart.Points[0].X);
        Assert.Equal(chart.PlotRight, chart.Points[^1].X);
        Assert.True(chart.Points[1].Y < chart.Points[0].Y);
        Assert.Equal(chart.ZeroY, chart.Points[2].Y);
    }

    [Fact]
    public void Geometry_NegativeAmount_SitsBelowTheZeroLine()
    {
        var chart = RevenueChart.Build(new[] { Point(500m, 5), Point(-200m, 12) }, 800, 300, 8);

        Assert.True(chart.Points[1].Y > chart.ZeroY);
        Assert.Contains(chart.YTicks, t => t.Label == "R0");
        Assert.Contains(chart.YTicks, t => t.Label.StartsWith("-R"));
    }

    [Fact]
    public void Geometry_ManyPoints_ThinsTheXLabels()
    {
        var points = Enumerable.Range(1, 26).Select(i => Point(i * 10m, 1 + (i % 28))).ToList();

        Assert.True(RevenueChart.Build(points, 800, 300, 8).XLabels.Count <= 8);
        Assert.True(RevenueChart.Build(points, 320, 220, 4).XLabels.Count <= 4);
    }

    [Theory]
    [InlineData("2000", "R2 000")]
    [InlineData("0", "R0")]
    [InlineData("0.5", "R0.50")]
    public void AxisLabel_DropsCentsForWholeRands(string amount, string expected)
    {
        Assert.Equal(expected, RevenueChart.AxisLabel(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
