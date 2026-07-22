using ActivityMonitor.TrayApp.History.Controls;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M6 混合视图 — 数据表格项数据模型回归测试。
/// 验证 SoftwareTableItem 的格式化输出（时长、百分比）。
/// </summary>
public class SoftwareTableItemTests
{
    [Fact]
    public void FormattedDuration_MoreThanOneHour_FormatsAsHoursMinutes()
    {
        var item = new SoftwareTableItem { DurationMs = 7_200_000 }; // 2 hours

        item.FormattedDuration.Should().Be("2h 0m");
    }

    [Fact]
    public void FormattedDuration_ExactlyOneHour_FormatsAsHoursMinutes()
    {
        var item = new SoftwareTableItem { DurationMs = 3_600_000 };

        item.FormattedDuration.Should().Be("1h 0m");
    }

    [Fact]
    public void FormattedDuration_MoreThanOneMinute_FormatsAsMinutes()
    {
        var item = new SoftwareTableItem { DurationMs = 300_000 }; // 5 minutes

        item.FormattedDuration.Should().Be("5m");
    }

    [Fact]
    public void FormattedDuration_ExactlyOneMinute_FormatsAsMinutes()
    {
        var item = new SoftwareTableItem { DurationMs = 60_000 };

        item.FormattedDuration.Should().Be("1m");
    }

    [Fact]
    public void FormattedDuration_LessThanOneMinute_FormatsAsSeconds()
    {
        var item = new SoftwareTableItem { DurationMs = 15_000 }; // 15 seconds

        item.FormattedDuration.Should().Be("15s");
    }

    [Fact]
    public void FormattedDuration_Zero_FormatsAsSeconds()
    {
        var item = new SoftwareTableItem { DurationMs = 0 };

        item.FormattedDuration.Should().Be("0s");
    }

    [Fact]
    public void FormattedPercentage_FormatsWithOneDecimal()
    {
        var item = new SoftwareTableItem { Percentage = 42.567 };

        item.FormattedPercentage.Should().Be("42.6%");
    }

    [Fact]
    public void FormattedPercentage_WholeNumber_FormatsWithOneDecimal()
    {
        var item = new SoftwareTableItem { Percentage = 100 };

        item.FormattedPercentage.Should().Be("100.0%");
    }

    [Fact]
    public void Properties_DefaultValues_AreCorrect()
    {
        var item = new SoftwareTableItem();

        item.Name.Should().BeEmpty();
        item.DurationMs.Should().Be(0);
        item.Percentage.Should().Be(0);
        item.RecordCount.Should().Be(0);
    }

    [Fact]
    public void RecordCount_SetValue_StoresCorrectly()
    {
        var item = new SoftwareTableItem { RecordCount = 42 };

        item.RecordCount.Should().Be(42);
    }
}
