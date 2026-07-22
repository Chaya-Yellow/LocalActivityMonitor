using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 日统计视图服务接口。
/// 提供按日期查询活动明细和按软件聚合的统计能力。
/// 用于 "W1-M6 日统计视图"。
/// </summary>
public interface IDailyStatsService
{
    /// <summary>
    /// 获取指定日期的活动事件明细列表。
    /// 按 <see cref="ActivityEvent.StartTime"/> 升序排列，包含完整的时间戳、
    /// 软件名、窗口标题、时长、类别等信息。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>活动事件明细列表，无事件时返回空列表。</returns>
    Task<List<ActivityEvent>> GetDetailByDateAsync(DateTime date);

    /// <summary>
    /// 获取指定日期按软件（process_name）聚合的日统计。
    /// 排除 idle 和 sleep 类别的记录，仅统计活跃活动。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>
    /// 按累计时长降序排列的软件统计列表。每项包含软件名、累计时长（毫秒）、
    /// 占比百分比和记录条数。无活跃事件时返回空列表。
    /// </returns>
    Task<List<DailySoftwareStats>> GetSoftwareStatsByDateAsync(DateTime date);
}
