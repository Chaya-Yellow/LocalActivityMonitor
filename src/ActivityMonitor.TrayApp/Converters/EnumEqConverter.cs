using System.Globalization;
using System.Windows.Data;
using WpfBinding = System.Windows.Data.Binding;

namespace ActivityMonitor.TrayApp.Converters;

/// <summary>
/// 枚举相等比较转换器。
/// 用于 RadioButton 的 IsChecked 绑定：当绑定值与 ConverterParameter 相等时返回 true。
/// </summary>
public class EnumEqConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return string.Equals(enumValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return WpfBinding.DoNothing;
    }
}
