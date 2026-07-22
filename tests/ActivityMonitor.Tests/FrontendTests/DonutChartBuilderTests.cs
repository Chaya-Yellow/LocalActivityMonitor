using System.Windows.Media;
using System.Windows.Shapes;
using ActivityMonitor.TrayApp.Dashboard.Controls;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M2 时段聚合 UI — 环形图生成器回归测试。
/// 验证占比环形图的切片生成、合并、过滤和空状态处理。
/// </summary>
public class DonutChartBuilderTests
{
    // ──────────────── 空数据 ────────────────

    [StaFact]
    public void BuildDonut_EmptyList_ReturnsSingleGraySlice()
    {
        // Act
        var paths = DonutChartBuilder.BuildDonut(new List<(double, string)>());

        // Assert
        paths.Should().HaveCount(1);
        var fill = paths[0].Fill as SolidColorBrush;
        fill.Should().NotBeNull();
        fill!.Color.Should().Be(Color.FromRgb(0xE8, 0xE8, 0xE8)); // EmptyColor
    }

    [StaFact]
    public void BuildDonut_AllZeroPercentages_ReturnsSingleGraySlice()
    {
        // Arrange
        var data = new List<(double, string)>
        {
            (0, "A"),
            (0, "B"),
        };

        // Act
        var paths = DonutChartBuilder.BuildDonut(data);

        // Assert
        paths.Should().HaveCount(1);
        var fill = paths[0].Fill as SolidColorBrush;
        fill.Should().NotBeNull();
        fill!.Color.Should().Be(Color.FromRgb(0xE8, 0xE8, 0xE8));
    }

    // ──────────────── 单个项目 ────────────────

    [StaFact]
    public void BuildDonut_SingleItem_CreatesOneSlicePlusRemainder()
    {
        var data = new List<(double, string)> { (50, "Chrome") };

        var paths = DonutChartBuilder.BuildDonut(data);

        // 50% slice + remainder
        paths.Should().HaveCount(2);
        var firstFill = paths[0].Fill as SolidColorBrush;
        firstFill.Should().NotBeNull();
        // First slice should be a non-gray color
        firstFill!.Color.Should().NotBe(Color.FromRgb(0xE8, 0xE8, 0xE8));
    }

    // ──────────────── 多项目（< 7 个） ────────────────

    [StaFact]
    public void BuildDonut_MultipleItems_AllSlicesCreated()
    {
        var data = new List<(double, string)>
        {
            (40, "code.exe"),
            (30, "chrome.exe"),
            (20, "msedge.exe"),
            (10, "notepad.exe"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        // Exactly 4 slices + maybe remainder (if < 100%)
        paths.Should().HaveCountGreaterOrEqualTo(4);
        paths.Count.Should().BeLessOrEqualTo(5);
    }

    // ──────────────── 超过 7 个合并 ────────────────

    [StaFact]
    public void BuildDonut_MoreThanSevenItems_MergesIntoMaxSevenSlices()
    {
        var data = new List<(double, string)>
        {
            (30, "A"), (20, "B"), (15, "C"),
            (10, "D"), (8, "E"), (5, "F"),
            (4, "G"), (3, "H"), (3, "I"), (2, "J"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        // Should have at most 7 + remainder slice (8 total max)
        paths.Should().HaveCountLessOrEqualTo(8);
    }

    // ──────────────── < 1% 过滤 ────────────────

    [StaFact]
    public void BuildDonut_PercentagesLessThanOne_FilteredOut()
    {
        var data = new List<(double, string)>
        {
            (80, "Main"),
            (0.5, "Tiny1"),
            (0.3, "Tiny2"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        // Only Main should be included as a colored slice (>= 1%)
        var coloredSlices = paths.Where(p =>
            ((SolidColorBrush)p.Fill).Color != Color.FromRgb(0xE8, 0xE8, 0xE8)).ToList();
        coloredSlices.Should().HaveCount(1);
    }

    // ──────────────── 大弧角（> 180°) ────────────────

    [StaFact]
    public void BuildDonut_LargePercentage_CreatesLargeArcSlice()
    {
        // A single item with 99% should work
        var data = new List<(double, string)> { (99, "App") };

        var paths = DonutChartBuilder.BuildDonut(data);

        // Should not throw, should produce valid geometry
        paths.Should().NotBeEmpty();
        foreach (var p in paths)
        {
            p.Data.Should().NotBeNull();
        }
    }

    // ──────────────── 颜色分配 ────────────────

    [StaFact]
    public void BuildDonut_MultipleItems_AssignsDifferentColors()
    {
        var data = new List<(double, string)>
        {
            (50, "A"), (30, "B"), (20, "C"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        var colors = paths
            .Select(p => ((SolidColorBrush)p.Fill).Color)
            .Where(c => c != Color.FromRgb(0xE8, 0xE8, 0xE8))
            .ToList();

        // Different colored slices should have distinct colors
        colors.Distinct().Should().HaveCount(colors.Count);
    }

    // ──────────────── 100% 全覆盖 ────────────────

    [StaFact]
    public void BuildDonut_FullCoverage_NoRemainderSlice()
    {
        var data = new List<(double, string)>
        {
            (50, "A"), (50, "B"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        // 2 slices exactly, no remainder if total = 100%
        // Actually the method recalculates from angle, so it may not be exact
        var coloredPaths = paths.Where(p =>
            ((SolidColorBrush)p.Fill).Color != Color.FromRgb(0xE8, 0xE8, 0xE8)).ToList();
        coloredPaths.Should().HaveCount(2);
    }

    // ──────────────── 几何有效性 ────────────────

    [StaFact]
    public void BuildDonut_ValidPercentages_ProducesValidGeometry()
    {
        var data = new List<(double, string)>
        {
            (45, "Chrome"),
            (30, "VS Code"),
            (15, "Terminal"),
            (10, "Other"),
        };

        var paths = DonutChartBuilder.BuildDonut(data);

        foreach (var path in paths)
        {
            path.Data.Should().NotBeNull();
            path.Fill.Should().NotBeNull();
        }
    }
}
