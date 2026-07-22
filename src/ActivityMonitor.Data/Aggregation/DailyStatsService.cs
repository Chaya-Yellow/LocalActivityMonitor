using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Aggregation;

/// <summary>
/// 日统计视图服务。
/// 提供按日期查询活动明细（委托 <see cref="ActivityEventRepository"/>）
/// 和按软件聚合的统计能力（直接使用 <see cref="SqliteContext"/> 执行 GROUP BY SQL）。
/// 用于 "W1-M6 日统计视图"。
/// </summary>
public class DailyStatsService : IDailyStatsService
{
    private readonly SqliteContext _db;
    private readonly ActivityEventRepository _eventRepo;

    /// <summary>
    /// 使用指定的数据库上下文和事件仓储初始化。
    /// </summary>
    /// <param name="db">数据库上下文，用于执行聚合 SQL。</param>
    /// <param name="eventRepo">活动事件仓储，用于查询明细。</param>
    public DailyStatsService(SqliteContext db, ActivityEventRepository eventRepo)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _eventRepo = eventRepo ?? throw new ArgumentNullException(nameof(eventRepo));
    }

    /// <inheritdoc />
    public async Task<List<ActivityEvent>> GetDetailByDateAsync(DateTime date)
    {
        // 复用已有的仓储方法，返回按 start_time 升序排列的完整事件
        return await _eventRepo.GetByDateAsync(date);
    }

    /// <inheritdoc />
    public async Task<List<DailySoftwareStats>> GetSoftwareStatsByDateAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        // GROUP BY process_name，汇总 duration_ms 和 count，排除 idle/sleep
        const string sql = @"
            SELECT
                process_name,
                SUM(duration_ms) AS total_ms,
                COUNT(*) AS record_count
            FROM activity_events
            WHERE start_time >= @start AND start_time < @end
              AND category NOT IN ('idle', 'sleep')
              AND process_name IS NOT NULL AND process_name != ''
            GROUP BY process_name
            ORDER BY total_ms DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        // ── 读取聚合结果 ──────────────────────────────────────────
        var rawList = new List<(string Name, long DurationMs, int RecordCount)>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var durationMs = reader.GetInt64(1);
            var recordCount = reader.GetInt32(2);

            rawList.Add((name, durationMs, recordCount));
        }

        if (rawList.Count == 0)
        {
            return new List<DailySoftwareStats>(0);
        }

        // ── 计算占比百分比 ────────────────────────────────────────
        var totalMs = rawList.Sum(r => r.DurationMs);

        var result = rawList
            .Select(r => new DailySoftwareStats
            {
                Name = r.Name,
                DurationMs = r.DurationMs,
                Percentage = totalMs > 0
                    ? Math.Round((double)r.DurationMs / totalMs * 100, 1)
                    : 0,
                RecordCount = r.RecordCount,
            })
            .ToList();

        return result;
    }
}
