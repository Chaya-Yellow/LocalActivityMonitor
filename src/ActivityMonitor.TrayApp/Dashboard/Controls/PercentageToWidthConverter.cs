using System.Globalization;
using System.Windows.Data;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 百分比 → 进度条宽度转换器。
/// 接收 (percentage, containerActualWidth) 两个值，返回 percentage% 的宽度。
/// </summary>
public class PercentageToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;

        double percentage = 0;
        double containerWidth = 100;

        if (values[0] is double p)
            percentage = p;
        else if (values[0] is int pi)
            percentage = pi;
        else if (values[0] is string s && double.TryParse(s, out var parsed))
            percentage = parsed;

        if (values[1] is double w)
            containerWidth = w;

        return Math.Max(0, containerWidth * percentage / 100.0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 非 null → Visible, null → Collapsed 转换器。
/// 用于控制项目路径和重命名按钮的可见性。
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 若 value 非 null 则返回 Visible，否则 Collapsed。
    /// parameter = "invert" 时反转行为。
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isVisible = value != null && (value is not string s || !string.IsNullOrEmpty(s));
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            isVisible = !isVisible;
        return isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
