using System.Globalization;
using System.Windows.Data;

namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 多值转换器：比较两个字符串是否匹配（忽略大小写）。
/// 用于柱状图和表格的选中联动高亮。
/// values[0] = 当前项的 Name
/// values[1] = 外部选中的名称（SelectedItemName）
/// 返回：二者是否非空且匹配。
/// </summary>
public class NameMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;

        var name = values[0] as string ?? string.Empty;
        var selected = values[1] as string ?? string.Empty;

        return !string.IsNullOrEmpty(selected)
               && string.Equals(name, selected, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
