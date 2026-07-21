using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 每日聚合表仓储。
/// 使用参数化 SQL 和 INSERT OR REPLACE 实现覆盖写入。
/// </summary>
public class DailySummaryRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public DailySummaryRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步写入或覆盖每日聚合。
    /// </summary>
    public async Task UpsertAsync(DailySummary summary)
    {
        const string sql = @"
            INSERT OR REPLACE INTO daily_summaries
                (date, total_active_ms, total_idle_ms, total_sleep_ms,
                 app_breakdown, domain_breakdown, project_breakdown,
                 work_ms, break_ms, keyword_cloud, raw_report, user_notes)
            VALUES
                (@date, @total_active_ms, @total_idle_ms, @total_sleep_ms,
                 @app_breakdown, @domain_breakdown, @project_breakdown,
                 @work_ms, @break_ms, @keyword_cloud, @raw_report, @user_notes);";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, summary);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取指定日期的聚合数据。
    /// </summary>
    /// <param name="date">日期字符串（YYYY-MM-DD）。</param>
    /// <returns>每日聚合对象，不存在时返回 null。</returns>
    public async Task<DailySummary?> GetAsync(string date)
    {
        const string sql = "SELECT * FROM daily_summaries WHERE date = @date;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@date", date);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadDailySummary(reader);
        }

        return null;
    }

    /// <summary>
    /// 异步获取指定日期范围内的每日聚合列表。
    /// </summary>
    /// <param name="startDate">起始日期（含，YYYY-MM-DD）。</param>
    /// <param name="endDate">结束日期（含，YYYY-MM-DD）。</param>
    public async Task<List<DailySummary>> GetRangeAsync(string startDate, string endDate)
    {
        const string sql = @"
            SELECT * FROM daily_summaries
            WHERE date >= @start AND date <= @end
            ORDER BY date ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startDate);
        cmd.Parameters.AddWithValue("@end", endDate);

        var results = new List<DailySummary>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadDailySummary(reader));
        }

        return results;
    }

    /// <summary>
    /// 异步删除指定日期的聚合数据。
    /// </summary>
    public async Task DeleteAsync(string date)
    {
        const string sql = "DELETE FROM daily_summaries WHERE date = @date;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@date", date);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindParameters(SqliteCommand cmd, DailySummary summary)
    {
        cmd.Parameters.AddWithValue("@date", summary.Date);
        cmd.Parameters.AddWithValue("@total_active_ms", summary.TotalActiveMs);
        cmd.Parameters.AddWithValue("@total_idle_ms", summary.TotalIdleMs);
        cmd.Parameters.AddWithValue("@total_sleep_ms", summary.TotalSleepMs);
        cmd.Parameters.AddWithValue("@app_breakdown", (object?)summary.AppBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@domain_breakdown", (object?)summary.DomainBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@project_breakdown", (object?)summary.ProjectBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@work_ms", summary.WorkMs);
        cmd.Parameters.AddWithValue("@break_ms", summary.BreakMs);
        cmd.Parameters.AddWithValue("@keyword_cloud", (object?)summary.KeywordCloud ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@raw_report", (object?)summary.RawReport ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@user_notes", (object?)summary.UserNotes ?? DBNull.Value);
    }

    private static DailySummary ReadDailySummary(SqliteDataReader reader)
    {
        var summary = new DailySummary
        {
            Date = reader.GetString(reader.GetOrdinal("date")),
            TotalActiveMs = reader.GetInt64(reader.GetOrdinal("total_active_ms")),
            TotalIdleMs = reader.GetInt64(reader.GetOrdinal("total_idle_ms")),
            TotalSleepMs = reader.GetInt64(reader.GetOrdinal("total_sleep_ms")),
            WorkMs = reader.GetInt64(reader.GetOrdinal("work_ms")),
            BreakMs = reader.GetInt64(reader.GetOrdinal("break_ms")),
        };

        if (!reader.IsDBNull(reader.GetOrdinal("app_breakdown")))
            summary.AppBreakdown = reader.GetString(reader.GetOrdinal("app_breakdown"));

        if (!reader.IsDBNull(reader.GetOrdinal("domain_breakdown")))
            summary.DomainBreakdown = reader.GetString(reader.GetOrdinal("domain_breakdown"));

        if (!reader.IsDBNull(reader.GetOrdinal("project_breakdown")))
            summary.ProjectBreakdown = reader.GetString(reader.GetOrdinal("project_breakdown"));

        if (!reader.IsDBNull(reader.GetOrdinal("keyword_cloud")))
            summary.KeywordCloud = reader.GetString(reader.GetOrdinal("keyword_cloud"));

        if (!reader.IsDBNull(reader.GetOrdinal("raw_report")))
            summary.RawReport = reader.GetString(reader.GetOrdinal("raw_report"));

        if (!reader.IsDBNull(reader.GetOrdinal("user_notes")))
            summary.UserNotes = reader.GetString(reader.GetOrdinal("user_notes"));

        return summary;
    }
}
