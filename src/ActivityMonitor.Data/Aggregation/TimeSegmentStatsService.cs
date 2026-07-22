using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Aggregation;

/// <summary>
/// 半小时时段聚合统计服务。
/// 将 activity_events 按开始时间分桶到 30 分钟槽（48 段/天）中，
/// 统计每个时段内的软件（process_name）分布和占比。
///
/// SQLite 端的整数除法自动向下取整，分组聚合在服务端完成，
/// 不将原始事件加载到内存中。
/// </summary>
public class TimeSegmentStatsService
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public TimeSegmentStatsService(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取指定日期的半小时段聚合统计。
    /// 按 (start_time - 当日 00:00) / 1800 秒 计算时段索引，
    /// 然后按 segment_index + process_name GROUP BY 汇总。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>按时间升序排列的 48 个时段统计。</returns>
    public async Task<List<TimeSegmentStats>> GetTimeSegmentStatsAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        const string sql = @"
            SELECT
                CAST((strftime('%s', start_time) - strftime('%s', @start)) / 1800 AS INTEGER) AS segment_index,
                process_name,
                SUM(duration_ms) AS total_ms
            FROM activity_events
            WHERE start_time >= @start AND start_time < @end
              AND category NOT IN ('idle', 'sleep')
              AND process_name IS NOT NULL AND process_name != ''
            GROUP BY segment_index, process_name
            ORDER BY segment_index, total_ms DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        // ── 预置 48 个空时段 ──────────────────────────────────────
        var segments = new List<TimeSegmentStats>(48);
        for (var i = 0; i < 48; i++)
        {
            segments.Add(new TimeSegmentStats
            {
                SegmentStart = startOfDay.AddMinutes(i * 30),
                TotalDurationMs = 0,
                SoftwareList = new List<StatsItem>(),
            });
        }

        // ── 填充查询结果 ──────────────────────────────────────────
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var segIdx = reader.GetInt32(0);
            if (segIdx < 0 || segIdx >= 48) continue;

            var processName = reader.GetString(1);
            var totalMs = reader.GetInt64(2);

            segments[segIdx].SoftwareList.Add(new StatsItem
            {
                Name = processName,
                DurationMs = totalMs,
                Percentage = 0, // 第二轮计算
            });
            segments[segIdx].TotalDurationMs += totalMs;
        }

        // ── 第二轮：计算占比百分比 ────────────────────────────────
        for (var i = 0; i < 48; i++)
        {
            var seg = segments[i];
            if (seg.TotalDurationMs <= 0) continue;

            foreach (var item in seg.SoftwareList)
            {
                item.Percentage = Math.Round((double)item.DurationMs / seg.TotalDurationMs * 100, 1);
            }
        }

        return segments;
    }
}
