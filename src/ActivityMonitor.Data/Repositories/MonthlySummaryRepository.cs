using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 每月聚合表仓储。
/// 使用参数化 SQL 和 INSERT OR REPLACE 实现覆盖写入。
/// </summary>
public class MonthlySummaryRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public MonthlySummaryRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步写入或覆盖每月聚合。
    /// </summary>
    public async Task UpsertAsync(MonthlySummary summary)
    {
        const string sql = @"
            INSERT OR REPLACE INTO monthly_summaries
                (month, total_active_ms, total_idle_ms,
                 app_breakdown, domain_breakdown, project_breakdown, avg_daily_hours)
            VALUES
                (@month, @total_active_ms, @total_idle_ms,
                 @app_breakdown, @domain_breakdown, @project_breakdown, @avg_daily_hours);";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, summary);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取指定月份的聚合数据。
    /// </summary>
    /// <param name="month">月份（YYYY-MM）。</param>
    /// <returns>每月聚合对象，不存在时返回 null。</returns>
    public async Task<MonthlySummary?> GetAsync(string month)
    {
        const string sql = "SELECT * FROM monthly_summaries WHERE month = @month;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@month", month);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadMonthlySummary(reader);
        }

        return null;
    }

    /// <summary>
    /// 异步获取所有月聚合列表，按月份降序排列。
    /// </summary>
    public async Task<List<MonthlySummary>> GetAllAsync()
    {
        const string sql = "SELECT * FROM monthly_summaries ORDER BY month DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var results = new List<MonthlySummary>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadMonthlySummary(reader));
        }

        return results;
    }

    /// <summary>
    /// 异步删除指定月份的聚合数据。
    /// </summary>
    public async Task DeleteAsync(string month)
    {
        const string sql = "DELETE FROM monthly_summaries WHERE month = @month;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@month", month);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindParameters(SqliteCommand cmd, MonthlySummary summary)
    {
        cmd.Parameters.AddWithValue("@month", summary.Month);
        cmd.Parameters.AddWithValue("@total_active_ms", summary.TotalActiveMs);
        cmd.Parameters.AddWithValue("@total_idle_ms", summary.TotalIdleMs);
        cmd.Parameters.AddWithValue("@app_breakdown", (object?)summary.AppBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@domain_breakdown", (object?)summary.DomainBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@project_breakdown", (object?)summary.ProjectBreakdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@avg_daily_hours", summary.AvgDailyHours);
    }

    private static MonthlySummary ReadMonthlySummary(SqliteDataReader reader)
    {
        var summary = new MonthlySummary
        {
            Month = reader.GetString(reader.GetOrdinal("month")),
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
