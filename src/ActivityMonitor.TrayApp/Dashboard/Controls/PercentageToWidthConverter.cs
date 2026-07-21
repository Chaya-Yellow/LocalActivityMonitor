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
