using System.Globalization;
using System.Windows.Data;

namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 多值转换器：将 WidthFactor 乘以父容器的实际宽度，得出柱条的目标宽度。
/// </summary>
public class BarWidthMultiConverter : IMultiValueConverter
{
    /// <summary>
    /// values[0]: WidthFactor (double, 0-1)
    /// values[1]: Parent ActualWidth (double)
    /// 返回：柱子应有的宽度（减去一些内边距）。
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;

        double factor = 0;
        if (values[0] is double d)
            factor = d;
        else if (values[0] is decimal dec)
            factor = (double)dec;

        double parentWidth = 0;
        if (values[1] is double pw)
            parentWidth = pw;

        // 留 4px 边距
        var result = factor * Math.Max(0, parentWidth - 4);
        return Math.Max(result, 2); // 最小 2px 确保可见
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
