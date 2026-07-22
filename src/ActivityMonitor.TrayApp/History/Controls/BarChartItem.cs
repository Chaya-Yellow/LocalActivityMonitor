using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 柱状图单项数据模型，代表一根柱子（一个软件/应用的统计）。
/// </summary>
public class BarChartItem
{
    /// <summary>软件名称（显示在柱子左侧）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>累计时长（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>占比百分比（0-100），用于计算柱宽。</summary>
    public double Percentage { get; set; }

    /// <summary>柱条颜色刷。</summary>
    public WpfBrush BarColor { get; set; } = WpfBrushes.Gray;

    /// <summary>格式化后的时长字符串（如 "2h 45m"）。</summary>
    public string FormattedDuration =>
        DurationMs >= 3_600_000
            ? $"{(int)(DurationMs / 3_600_000)}h {(int)((DurationMs % 3_600_000) / 60_000)}m"
            : DurationMs >= 60_000
                ? $"{(int)(DurationMs / 60_000)}m"
                : $"{DurationMs / 1000}s";

    /// <summary>百分比格式化文本。</summary>
    public string FormattedPercentage => $"{Percentage:F1}%";

    /// <summary>ToolTip 完整提示文本。</summary>
    public string TooltipText => $"{Name}\n{FormattedDuration} · {FormattedPercentage}";

    /// <summary>柱条宽度因子（0-1），相对于最大宽度的比例。</summary>
    public double WidthFactor { get; set; } = 1.0;
}
