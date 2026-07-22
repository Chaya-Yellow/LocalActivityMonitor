using System.Windows.Media;
using ActivityMonitor.TrayApp.History.Controls;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M6 混合视图 — 柱状图项数据模型回归测试。
/// 验证 BarChartItem 的格式化输出（时长、百分比、Tooltip）。
/// </summary>
public class BarChartItemTests
{
    [StaFact]
    public void FormattedDuration_MoreThanOneHour_FormatsAsHoursMinutes()
    {
        var item = new BarChartItem { DurationMs = 5_400_000 }; // 1.5 hours

        item.FormattedDuration.Should().Be("1h 30m");
    }

    [StaFact]
    public void FormattedDuration_ExactlyOneHour_FormatsAsHoursMinutes()
    {
        var item = new BarChartItem { DurationMs = 3_600_000 };

        item.FormattedDuration.Should().Be("1h 0m");
    }

    [StaFact]
    public void FormattedDuration_MoreThanOneMinute_FormatsAsMinutes()
    {
        var item = new BarChartItem { DurationMs = 120_000 }; // 2 minutes

        item.FormattedDuration.Should().Be("2m");
    }

    [StaFact]
    public void FormattedDuration_ExactlyOneMinute_FormatsAsMinutes()
    {
        var item = new BarChartItem { DurationMs = 60_000 };

        item.FormattedDuration.Should().Be("1m");
    }

    [StaFact]
    public void FormattedDuration_LessThanOneMinute_FormatsAsSeconds()
    {
        var item = new BarChartItem { DurationMs = 30_000 }; // 30 seconds

        item.FormattedDuration.Should().Be("30s");
    }

    [StaFact]
    public void FormattedDuration_Zero_FormatsAsSeconds()
    {
        var item = new BarChartItem { DurationMs = 0 };

        item.FormattedDuration.Should().Be("0s");
    }

    [StaFact]
    public void FormattedPercentage_FormatsWithOneDecimal()
    {
        var item = new BarChartItem { Percentage = 35.678 };

        item.FormattedPercentage.Should().Be("35.7%");
    }

    [StaFact]
    public void FormattedPercentage_WholeNumber_FormatsWithOneDecimal()
    {
        var item = new BarChartItem { Percentage = 100 };

        item.FormattedPercentage.Should().Be("100.0%");
    }

    [StaFact]
    public void FormattedPercentage_Zero_FormatsCorrectly()
    {
        var item = new BarChartItem { Percentage = 0 };

        item.FormattedPercentage.Should().Be("0.0%");
    }

    [StaFact]
    public void TooltipText_ContainsAllInfo()
    {
        var item = new BarChartItem
        {
            Name = "chrome.exe",
            DurationMs = 3_600_000,
            Percentage = 45.2,
        };

        item.TooltipText.Should().Be("chrome.exe\n1h 0m · 45.2%");
    }

    [StaFact]
    public void WidthFactor_Negative_DoesNotCrash()
    {
        // Just verify the property can hold negative values (used in binding)
        var item = new BarChartItem { WidthFactor = -0.1 };

        item.WidthFactor.Should().Be(-0.1);
    }

    [StaFact]
    public void BarColor_Default_IsGray()
    {
        var item = new BarChartItem();

        var brush = item.BarColor as SolidColorBrush;
        brush.Should().NotBeNull();
        brush!.Color.Should().Be(Colors.Gray);
    }
}
