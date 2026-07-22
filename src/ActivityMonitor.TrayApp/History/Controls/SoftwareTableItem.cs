namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 数据表格项模型 — 对应软件统计表格中的一行。
/// 包含软件名、累计时长、占比、记录条数，供 DataGrid/ListView 绑定。
/// </summary>
public class SoftwareTableItem
{
    /// <summary>软件名称（如 "chrome.exe"、"code.exe"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>累计活跃时长（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>时长占比（0–100）。</summary>
    public double Percentage { get; set; }

    /// <summary>活动记录条数。</summary>
    public int RecordCount { get; set; }

    /// <summary>格式化时长字符串（如 "2h 45m"、"30m"、"15s"）。</summary>
    public string FormattedDuration =>
        DurationMs >= 3_600_000
            ? $"{(int)(DurationMs / 3_600_000)}h {(int)((DurationMs % 3_600_000) / 60_000)}m"
            : DurationMs >= 60_000
                ? $"{(int)(DurationMs / 60_000)}m"
                : $"{DurationMs / 1000}s";

    /// <summary>百分比格式化文本（如 "35.2%"）。</summary>
    public string FormattedPercentage => $"{Percentage:F1}%";
}
