namespace ActivityMonitor.Core.Models;

/// <summary>
/// 周报结构化数据模型。
/// 包含本周软件/项目分布以及上周环比对比数据。
/// </summary>
public class WeeklyReportData
{
    /// <summary>周起始日期（yyyy-MM-dd，周一）。</summary>
    public string WeekStart { get; set; } = string.Empty;

    /// <summary>周结束日期（yyyy-MM-dd，周日）。</summary>
    public string WeekEnd { get; set; } = string.Empty;

    /// <summary>本周总活跃时长（毫秒）。</summary>
    public long TotalActiveMs { get; set; }

    /// <summary>本周总空闲时长（毫秒）。</summary>
    public long TotalIdleMs { get; set; }

    /// <summary>本周日均活跃小时数。</summary>
    public double AvgDailyHours { get; set; }

    /// <summary>软件分布（含占比和环比）。</summary>
    public List<BreakdownItem> AppBreakdown { get; set; } = new();

    /// <summary>项目分布（含占比和环比）。</summary>
    public List<BreakdownItem> ProjectBreakdown { get; set; } = new();

    /// <summary>上周对比数据（总体），无上周数据时为 null。</summary>
    public WeekOverWeekComparison? WeekComparison { get; set; }
}

/// <summary>
/// 单项分布数据，包含本周时长占比和上周环比。
/// </summary>
public class BreakdownItem
{
    /// <summary>软件名或项目名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>本周总时长（毫秒）。</summary>
    public long TotalMs { get; set; }

    /// <summary>本周占比（0-100）。</summary>
    public double Percentage { get; set; }

    /// <summary>上周同项时长（毫秒），无上周数据时为 null。</summary>
    public long? LastWeekMs { get; set; }

    /// <summary>增减绝对值（毫秒），无上周数据时为 null（正=增加，负=减少）。</summary>
    public long? ChangeMs { get; set; }

    /// <summary>增减百分比，无上周数据或上周时长为 0 时为 null。</summary>
    public double? ChangePercent { get; set; }
}

/// <summary>
/// 周环比对比数据。
/// </summary>
public class WeekOverWeekComparison
{
    /// <summary>本周总活跃时长（毫秒）。</summary>
    public long ThisWeekTotalMs { get; set; }

    /// <summary>上周总活跃时长（毫秒）。</summary>
    public long LastWeekTotalMs { get; set; }

    /// <summary>增减绝对值（正=增加，负=减少）。</summary>
    public long ChangeMs { get; set; }

    /// <summary>环比百分比。</summary>
    public double ChangePercent { get; set; }

    /// <summary>本周日均活跃小时数。</summary>
    public double ThisWeekAvgDailyHours { get; set; }

    /// <summary>上周日均活跃小时数。</summary>
    public double LastWeekAvgDailyHours { get; set; }
}
