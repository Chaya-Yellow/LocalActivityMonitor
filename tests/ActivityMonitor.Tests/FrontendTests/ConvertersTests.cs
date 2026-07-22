using System.Globalization;
using System.Windows;
using ActivityMonitor.TrayApp.Dashboard.Controls;
using ActivityMonitor.TrayApp.History.Controls;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M6 混合视图 — 转换器回归测试。
/// 验证柱状图宽度、名称匹配、百分比宽度和可见性转换器的正确性。
/// </summary>
public class ConvertersTests
{
    // ──────────────── BarWidthMultiConverter ────────────────

    [Fact]
    public void BarWidthMultiConverter_ValidInputs_ReturnsCorrectWidth()
    {
        var converter = new BarWidthMultiConverter();

        var result = converter.Convert([0.5, 100.0], typeof(double), null!, CultureInfo.InvariantCulture);

        // 0.5 * (100 - 4) = 48
        ((double)result).Should().BeApproximately(48.0, 0.1);
    }

    [Fact]
    public void BarWidthMultiConverter_FullWidth_ReturnsParentsWidthMinusPadding()
    {
        var converter = new BarWidthMultiConverter();

        var result = converter.Convert([1.0, 300.0], typeof(double), null!, CultureInfo.InvariantCulture);

        ((double)result).Should().BeApproximately(296.0, 0.1);
    }

    [Fact]
    public void BarWidthMultiConverter_ZeroWidth_ReturnsMinimumTwoPx()
    {
        var converter = new BarWidthMultiConverter();

        var result = converter.Convert([0.0, 500.0], typeof(double), null!, CultureInfo.InvariantCulture);

        // Min 2px
        var d = (double)result;
        d.Should().BeGreaterOrEqualTo(2.0);
    }

    [Fact]
    public void BarWidthMultiConverter_WidthFactorAsDecimal_HandlesCorrectly()
    {
        var converter = new BarWidthMultiConverter();

        var result = converter.Convert([0.5m, 200.0], typeof(double), null!, CultureInfo.InvariantCulture);

        ((double)result).Should().BeApproximately(98.0, 0.1);
    }

    [Fact]
    public void BarWidthMultiConverter_InsufficientValues_ReturnsZero()
    {
        var converter = new BarWidthMultiConverter();

        var result = converter.Convert([0.5], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(0.0);
    }

    [Fact]
    public void BarWidthMultiConverter_ConvertBack_ThrowsNotSupported()
    {
        var converter = new BarWidthMultiConverter();

        Action act = () => converter.ConvertBack(100.0, [], null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }

    // ──────────────── NameMatchConverter ────────────────

    [Fact]
    public void NameMatchConverter_MatchingNames_ReturnsTrue()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["chrome.exe", "chrome.exe"], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(true);
    }

    [Fact]
    public void NameMatchConverter_CaseInsensitiveMatch_ReturnsTrue()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["Chrome.Exe", "chrome.exe"], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(true);
    }

    [Fact]
    public void NameMatchConverter_NonMatchingNames_ReturnsFalse()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["chrome.exe", "code.exe"], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }

    [Fact]
    public void NameMatchConverter_EmptySelected_ReturnsFalse()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["chrome.exe", ""], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }

    [Fact]
    public void NameMatchConverter_NullSelected_ReturnsFalse()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["chrome.exe", null!], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }

    [Fact]
    public void NameMatchConverter_InsufficientValues_ReturnsFalse()
    {
        var converter = new NameMatchConverter();

        var result = converter.Convert(["chrome.exe"], typeof(bool), null!, CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }

    [Fact]
    public void NameMatchConverter_ConvertBack_ThrowsNotSupported()
    {
        var converter = new NameMatchConverter();

        Action act = () => converter.ConvertBack(true, [], null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }

    // ──────────────── PercentageToWidthConverter ────────────────

    [Fact]
    public void PercentageToWidthConverter_FiftyPercent_ReturnsHalfWidth()
    {
        var converter = new PercentageToWidthConverter();

        var result = converter.Convert([50.0, 200.0], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(100.0);
    }

    [Fact]
    public void PercentageToWidthConverter_StringPercentage_HandlesCorrectly()
    {
        var converter = new PercentageToWidthConverter();

        var result = converter.Convert(["75", 200.0], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(150.0);
    }

    [Fact]
    public void PercentageToWidthConverter_IntPercentage_HandlesCorrectly()
    {
        var converter = new PercentageToWidthConverter();

        var result = converter.Convert([25, 400.0], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(100.0);
    }

    [Fact]
    public void PercentageToWidthConverter_ZeroPercentage_ReturnsZero()
    {
        var converter = new PercentageToWidthConverter();

        var result = converter.Convert([0.0, 200.0], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(0.0);
    }

    [Fact]
    public void PercentageToWidthConverter_InsufficientValues_ReturnsZero()
    {
        var converter = new PercentageToWidthConverter();

        var result = converter.Convert([50.0], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(0.0);
    }

    [Fact]
    public void PercentageToWidthConverter_ConvertBack_ThrowsNotSupported()
    {
        var converter = new PercentageToWidthConverter();

        Action act = () => converter.ConvertBack(100.0, [], null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }

    // ──────────────── NullToVisibilityConverter ────────────────

    [Fact]
    public void NullToVisibilityConverter_NonNullString_ReturnsVisible()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert("hello", typeof(Visibility), null!, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void NullToVisibilityConverter_Null_ReturnsCollapsed()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert(null, typeof(Visibility), null!, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void NullToVisibilityConverter_EmptyString_ReturnsCollapsed()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert("", typeof(Visibility), null!, CultureInfo.InvariantCulture);

        // Empty string treated same as null
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void NullToVisibilityConverter_NonNullWithInvert_ReturnsCollapsed()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert("hello", typeof(Visibility), "invert", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void NullToVisibilityConverter_NullWithInvert_ReturnsVisible()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert(null, typeof(Visibility), "invert", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void NullToVisibilityConverter_ConvertBack_ThrowsNotSupported()
    {
        var converter = new NullToVisibilityConverter();

        Action act = () => converter.ConvertBack(Visibility.Visible, typeof(object), null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
