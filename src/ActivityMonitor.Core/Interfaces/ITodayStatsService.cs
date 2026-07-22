namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 当日实时统计服务接口。
/// 查询当天（00:00 至今）的活动事件，按多种维度聚合时长和占比。
/// </summary>
public interface ITodayStatsService
{
    /// <summary>
    /// 按应用程序（进程名）聚合当日时长。
    /// </summary>
    /// <returns>应用程序统计列表。</returns>
    Task<List<StatsItem>> GetByAppAsync();

    /// <summary>
    /// 按项目名称聚合当日时长。
    /// </summary>
    /// <returns>项目统计列表。</returns>
    Task<List<StatsItem>> GetByProjectAsync();

    /// <summary>
    /// 按域名聚合当日时长。
    /// </summary>
    /// <returns>域名统计列表。</returns>
    Task<List<StatsItem>> GetByDomainAsync();

    /// <summary>
    /// 按活动类别聚合当日时长。
    /// </summary>
    /// <returns>类别统计列表。</returns>
    Task<List<StatsItem>> GetByCategoryAsync();

    /// <summary>
    /// 按工作/非工作标记聚合当日时长。
    /// </summary>
    /// <returns>工作标记统计列表。</returns>
    Task<List<StatsItem>> GetByWorkTagAsync();

    /// <summary>
    /// 获取当天的总体概览数据。
    /// </summary>
    /// <returns>当日概览。</returns>
    Task<TodayOverview> GetOverviewAsync();
}

/// <summary>
/// 单维度统计项。
/// </summary>
public class StatsItem
{
    /// <summary>维度名称（如 "code.exe"、"project-x"、"github.com"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>累计时长（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>占比百分比（0-100）。</summary>
    public double Percentage { get; set; }

    /// <summary>附加详情（路径、URL 等上下文信息）。</summary>
    public string? Detail { get; set; }
}

/// <summary>
/// 当日总体概览。
/// </summary>
public class TodayOverview
{
    /// <summary>总活跃时长（毫秒）。</summary>
    public long TotalActiveMs { get; set; }

    /// <summary>总空闲时长（毫秒）。</summary>
    public long TotalIdleMs { get; set; }

    /// <summary>总睡眠时长（毫秒）。</summary>
    public long TotalSleepMs { get; set; }

    /// <summary>工作活动时长（毫秒）。</summary>
    public long WorkMs { get; set; }

    /// <summary>非工作活动时长（毫秒）。</summary>
    public long NonWorkMs { get; set; }

    /// <summary>事件总数。</summary>
    public int EventCount { get; set; }
}
