using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 每周聚合表仓储。
/// 使用参数化 SQL 和 INSERT OR REPLACE 实现覆盖写入。
/// </summary>
public class WeeklySummaryRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public WeeklySummaryRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步写入或覆盖每周聚合。
    /// </summary>
    public async Task UpsertAsync(WeeklySummary summary)
    {
        const string sql = @"
            INSERT OR REPLACE INTO weekly_summaries
                (week_start, week_end, total_active_ms, total_idle_ms,
                 app_breakdown, domain_breakdown, project_breakdown, avg_daily_hours)
            VALUES
                (@week_start, @week_end, @total_active_ms, @total_idle_ms,
                 @app_breakdown, @domain_breakdown, @project_breakdown, @avg_daily_hours);";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, summary);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取指定周起始的聚合数据。
    /// </summary>
    /// <param name="weekStart">周起始日期（YYYY-MM-DD，周一）。</param>
    /// <returns>每周聚合对象，不存在时返回 null。</returns>
    public async Task<WeeklySummary?> GetAsync(string weekStart)
    {
        const string sql = "SELECT * FROM weekly_summaries WHERE week_start = @week_start;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@week_start", weekStart);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadWeeklySummary(reader);
        }

        return null;
    }

    /// <summary>
    /// 异步获取所有周聚合列表，按周起始降序排列。
    /// </summary>
    public async Task<List<WeeklySummary>> GetAllAsync()
    {
        const string sql = "SELECT * FROM weekly_summaries ORDER BY week_start DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var results = new List<WeeklySummary>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadWeeklySummary(reader));
        }

        return results;
    }

    /// <summary>
    /// 异步删除指定周的聚合数据。
    /// </summary>
    public async Task DeleteAsync(string weekStart)
    {
        const string sql = "DELETE FROM weekly_summaries WHERE week_start = @week_start;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@week_start", weekStart);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindParameters(SqliteCommand cmd, WeeklySummary summary)
    {
        cmd.Parameters.AddWithValue("@week_start", summary.WeekStart);
        cmd.Parameters.AddWithValue("@week_end", summary.WeekEnd);
        cmd.Parameters.AddWithValue("@total_active_ms", summary.TotalActiveMs);
        cmd.Parameters.AddWithValue("@total_idle_ms", summary.TotalIdleMs);
        cmd.Parameters.AddWithValue("@app_breakdown", (object?)summary.AppBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@domain_breakdown", (object?)summary.DomainBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@project_breakdown", (object?)summary.ProjectBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@avg_daily_hours", summary.AvgDailyHours);
    }

    private static WeeklySummary ReadWeeklySummary(SqliteDataReader reader)
    {
        var summary = new WeeklySummary
        {
            WeekStart = reader.GetString(reader.GetOrdinal("week_start")),
            WeekEnd = reader.IsDBNull(reader.GetOrdinal("week_end"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("week_end")),
            TotalActiveMs = reader.GetInt64(reader.GetOrdinal("total_active_ms")),
            TotalIdleMs = reader.GetInt64(reader.GetOrdinal("total_idle_ms")),
            AvgDailyHours = reader.GetDouble(reader.GetOrdinal("avg_daily_hours")),
        };

        if (!reader.IsDBNull(reader.GetOrdinal("app_breakdown")))
            summary.AppBreakdown = reader.GetString(reader.GetOrdinal("app_breakdown"));

        if (!reader.IsDBNull(reader.GetOrdinal("domain_breakdown")))
            summary.DomainBreakdown = reader.GetString(reader.GetOrdinal("domain_breakdown"));

        if (!reader.IsDBNull(reader.GetOrdinal("project_breakdown")))
            summary.ProjectBreakdown = reader.GetString(reader.GetOrdinal("project_breakdown"));

        return summary;
    }
}
