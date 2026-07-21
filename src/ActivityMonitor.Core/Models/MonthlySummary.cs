namespace ActivityMonitor.Core.Models;

/// <summary>
/// 每月聚合数据模型。
/// </summary>
public class MonthlySummary
{
    /// <summary>月份（YYYY-MM）。</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>总活跃时长（毫秒）。</summary>
    public long TotalActiveMs { get; set; }

    /// <summary>总空闲时长（毫秒）。</summary>
    public long TotalIdleMs { get; set; }

    /// <summary>
    /// 按应用程序的时长分布 JSON。
    /// 格式：{ "code.exe": 3600000, "chrome.exe": 1800000 }
    /// </summary>
    public string? AppBreakdown { get; set; }

    /// <summary>
    /// 按域名的时长分布 JSON。
    /// 格式：{ "github.com": 1800000, "stackoverflow.com": 900000 }
    /// </summary>
    public string? DomainBreakdown { get; set; }

    /// <summary>
    /// 按项目的时长分布 JSON。
    /// 格式：{ "project-name": 7200000, "other": 3600000 }
    /// </summary>
    public string? ProjectBreakdown { get; set; }

    /// <summary>平均每日活跃小时数。</summary>
    public double AvgDailyHours { get; set; }
}
