using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 活动事件仓储接口。提供对 <see cref="ActivityEvent"/> 的 CRUD 和聚合查询操作。
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// 异步插入单条活动事件。
    /// </summary>
    /// <param name="event">要插入的活动事件对象。</param>
    /// <returns>包含自增 ID 的完整事件对象。</returns>
    Task<ActivityEvent> InsertAsync(ActivityEvent @event);

    /// <summary>
    /// 异步批量插入多条活动事件，使用事务包裹。
    /// </summary>
    /// <param name="events">要插入的活动事件列表。</param>
    Task InsertBatchAsync(IEnumerable<ActivityEvent> events);

    /// <summary>
    /// 异步获取当天（00:00 至今）的所有活动事件，按开始时间升序排列。
    /// </summary>
    Task<List<ActivityEvent>> GetTodayEventsAsync();

    /// <summary>
    /// 异步获取指定日期的所有活动事件。
    /// </summary>
    /// <param name="date">查询日期。</param>
    Task<List<ActivityEvent>> GetByDateAsync(DateTime date);

    /// <summary>
    /// 异步获取指定日期范围内的活动事件。
    /// </summary>
    /// <param name="start">开始日期（含）。</param>
    /// <param name="end">结束日期（含）。</param>
    Task<List<ActivityEvent>> GetByDateRangeAsync(DateTime start, DateTime end);

    /// <summary>
    /// 异步更新单条活动事件。
    /// </summary>
    /// <param name="event">要更新的事件对象（根据 Id 匹配）。</param>
    Task UpdateAsync(ActivityEvent @event);

    /// <summary>
    /// 异步删除指定 ID 的活动事件。
    /// </summary>
    /// <param name="id">事件 ID。</param>
    Task DeleteAsync(long id);

    /// <summary>
    /// 异步获取当天的基本统计信息（总活跃时长、空闲时长等）。
    /// </summary>
    /// <param name="date">统计日期。</param>
    Task<DailyStats> GetDailyStatsAsync(DateTime date);

    /// <summary>
    /// 异步获取指定 ID 的活动事件。
    /// </summary>
    /// <param name="id">事件 ID。</param>
    Task<ActivityEvent?> GetByIdAsync(long id);

    /// <summary>
    /// 根据活动事件 ID 获取来源追溯信息（原始窗口标题 + 完整进程路径）。
    /// 用于 F2.6 来源追溯功能。
    /// </summary>
    /// <param name="id">事件 ID。</param>
    /// <returns>来源追溯信息；事件不存在时返回 null。</returns>
    Task<EventSourceInfo?> GetSourceInfoAsync(long id);
}

/// <summary>
/// 来源追溯信息 — 用于 F2.6：查看原始窗口标题和完整进程路径。
/// </summary>
public class EventSourceInfo
{
    /// <summary>活动事件 ID。</summary>
    public long EventId { get; set; }

    /// <summary>原始窗口标题（捕获时完整标题，不做截断/处理）。</summary>
    public string? RawWindowTitle { get; set; }

    /// <summary>原始进程完整路径。</summary>
    public string? RawProcessPath { get; set; }

    /// <summary>进程名（含 .exe 后缀）。</summary>
    public string? ProcessName { get; set; }

    /// <summary>活动开始时间。</summary>
    public DateTime StartTime { get; set; }

    /// <summary>活动结束时间。</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 每日统计摘要。
/// </summary>
public class DailyStats
{
    /// <summary>总活跃时长（毫秒）。</summary>
    public long TotalActiveMs { get; set; }

    /// <summary>总空闲时长（毫秒）。</summary>
    public long TotalIdleMs { get; set; }

    /// <summary>总睡眠时长（毫秒）。</summary>
    public long TotalSleepMs { get; set; }

    /// <summary>事件总数。</summary>
    public int EventCount { get; set; }
}
