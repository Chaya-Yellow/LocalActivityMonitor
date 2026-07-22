namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 半小时时段聚合统计服务接口。
/// 将一天划分为 48 个 30 分钟时段，统计每个时段内的软件使用分布。
/// 用于 "W1-M2 半小时聚合查询"。
/// </summary>
public interface ITimeSegmentStatsService
{
    /// <summary>
    /// 获取指定日期的半小时段聚合统计。
    /// 按时间升序排列返回 48 个时段（00:00、00:30 … 23:30）。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>48 个时段的软件使用统计列表，无事件的时段返回空列表。</returns>
    Task<List<TimeSegmentStats>> GetTimeSegmentStatsAsync(DateTime date);
}

/// <summary>
/// 半小时时间段的聚合统计结果。
/// 包含该时段的总活跃时长以及各软件的耗时和占比。
/// </summary>
public class TimeSegmentStats
{
    /// <summary>时段起始时间（如 09:00 对应 9:00 – 9:29 时段）。</summary>
    public DateTime SegmentStart { get; set; }

    /// <summary>该时段总活跃时长（毫秒）。排除 idle / sleep 类别。</summary>
    public long TotalDurationMs { get; set; }

    /// <summary>该时段的软件使用分布列表，按时长降序排列。</summary>
    public List<StatsItem> SoftwareList { get; set; } = new();
}
