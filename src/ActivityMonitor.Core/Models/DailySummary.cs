namespace ActivityMonitor.Core.Models;

/// <summary>
/// 每日聚合数据模型，用于加速 Dashboard 查询和日报生成。
/// </summary>
public class DailySummary
{
    /// <summary>日期（YYYY-MM-DD）。</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>总活跃时长（毫秒）。</summary>
    public long TotalActiveMs { get; set; }

    /// <summary>总空闲时长（毫秒）。</summary>
    public long TotalIdleMs { get; set; }

    /// <summary>总睡眠/锁屏时长（毫秒）。</summary>
    public long TotalSleepMs { get; set; }

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

    /// <summary>标记为"工作"的总时长（毫秒）。</summary>
    public long WorkMs { get; set; }

    /// <summary>标记为"休息"的总时长（毫秒）。</summary>
    public long BreakMs { get; set; }

    /// <summary>
    /// 关键词词频 JSON。
    /// 格式：{ "keyword1": 5, "keyword2": 3 }
    /// </summary>
    public string? KeywordCloud { get; set; }

    /// <summary>预生成的日报 Markdown 草稿。</summary>
    public string? RawReport { get; set; }

    /// <summary>用户补充的日报内容。</summary>
    public string? UserNotes { get; set; }
}
