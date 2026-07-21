using System.Text.Json;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Aggregation;

/// <summary>
/// 每日聚合服务。
/// 每晚 00:05 执行，按 app / project / domain 三个维度聚合前一天的活动数据。
/// 使用 SQL GROUP BY 聚合，不加载原始事件到内存。
/// </summary>
public class DailyAggregationService
{
    private readonly SqliteContext _db;
    private readonly DailySummaryRepository _summaryRepo;

    /// <summary>
    /// 使用指定的数据库上下文和仓储初始化。
    /// </summary>
    public DailyAggregationService(SqliteContext db, DailySummaryRepository summaryRepo)
    {
        _db = db;
        _summaryRepo = summaryRepo;
    }

    /// <summary>
    /// 对指定日期执行每日聚合。
    /// </summary>
    /// <param name="date">聚合目标日期。</param>
    public async Task AggregateAsync(DateTime date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var startTime = date.Date;
        var endTime = startTime.AddDays(1);

        var summary = new DailySummary { Date = dateStr };

        // ── 1. 基本统计：活跃/空闲/睡眠时长 ───────────────────────
        await AggregateBasicStatsAsync(summary, startTime, endTime);

        // ── 2. App 分布 ───────────────────────────────────────────
        summary.AppBreakdown = await AggregateGroupAsync(
            startTime, endTime, "process_name",
            "WHERE category NOT IN ('idle','sleep') AND process_name IS NOT NULL AND process_name != ''");

        // ── 3. 项目分布 ───────────────────────────────────────────
        summary.ProjectBreakdown = await AggregateGroupAsync(
            startTime, endTime, "project",
            "WHERE category NOT IN ('idle','sleep') AND project IS NOT NULL AND project != ''");

        // ── 4. 域名分布 ───────────────────────────────────────────
        summary.DomainBreakdown = await AggregateGroupAsync(
            startTime, endTime, "domain",
            "WHERE category NOT IN ('idle','sleep') AND domain IS NOT NULL AND domain != ''");

        // ── 5. 写入数据库 ─────────────────────────────────────────
        await _summaryRepo.UpsertAsync(summary);
    }

    /// <summary>
    /// 对指定日期范围内的每一天执行聚合。
    /// </summary>
    /// <param name="startDate">起始日期（含）。</param>
    /// <param name="endDate">结束日期（含）。</param>
    public async Task AggregateRangeAsync(DateTime startDate, DateTime endDate)
    {
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
        {
            await AggregateAsync(d);
        }
    }

    // ── 私有方法 ──────────────────────────────────────────────────

    /// <summary>
    /// 聚合基本统计（总活跃/空闲/睡眠/工作/休息时长）。
    /// 单条 SQL，不加载行数据到内存。
    /// </summary>
    private async Task AggregateBasicStatsAsync(DailySummary summary, DateTime startTime, DateTime endTime)
    {
        const string sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN category NOT IN ('idle','sleep') THEN duration_ms ELSE 0 END), 0) AS total_active_ms,
                COALESCE(SUM(CASE WHEN category = 'idle' THEN duration_ms ELSE 0 END), 0) AS total_idle_ms,
                COALESCE(SUM(CASE WHEN category = 'sleep' THEN duration_ms ELSE 0 END), 0) AS total_sleep_ms,
                COALESCE(SUM(CASE WHEN work_tag = 'work' THEN duration_ms ELSE 0 END), 0) AS work_ms,
                COALESCE(SUM(CASE WHEN work_tag IN ('break','personal') THEN duration_ms ELSE 0 END), 0) AS break_ms
            FROM activity_events
            WHERE start_time >= @start AND start_time < @end;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startTime.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endTime.ToString("O"));

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            summary.TotalActiveMs = reader.GetInt64(0);
            summary.TotalIdleMs = reader.GetInt64(1);
            summary.TotalSleepMs = reader.GetInt64(2);
            summary.WorkMs = reader.GetInt64(3);
            summary.BreakMs = reader.GetInt64(4);
        }
    }

    /// <summary>
    /// 按指定字段 GROUP BY 聚合时长分布，返回 JSON 字符串。
    /// 使用 SQL 聚合，不加载原始行到内存。
    /// </summary>
    private async Task<string?> AggregateGroupAsync(
        DateTime startTime, DateTime endTime,
        string groupField, string whereClause)
    {
        var sql = $@"
            SELECT {groupField} AS key, SUM(duration_ms) AS total_ms
            FROM activity_events
            {whereClause}
              AND start_time >= @start AND start_time < @end
            GROUP BY {groupField}
            ORDER BY total_ms DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startTime.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endTime.ToString("O"));

        var dict = new Dictionary<string, long>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var totalMs = reader.GetInt64(1);
            dict[key] = totalMs;
        }

        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }
}
