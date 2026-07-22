namespace ActivityMonitor.Core.Models;

/// <summary>
/// 日统计视图中的单款软件聚合统计项。
/// 用于 "W1-M6 日统计视图"，按日期对 activity_events 表按 process_name
/// 进行 GROUP BY 聚合后的统计结果。
/// </summary>
public class DailySoftwareStats
{
    /// <summary>软件名（可执行文件名，如 "chrome.exe"、"code.exe"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>该软件在查询日期内的累计活跃时长（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 该软件占当日总活跃时长的百分比（0–100）。
    /// 计算方式：(该软件 DurationMs / 当日各软件 DurationMs 总和) × 100。
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// 该软件在查询日期内的活动事件记录条数。
    /// 等价于 GROUP BY + COUNT(*) 的返回值。
    /// </summary>
    public int RecordCount { get; set; }
}
