using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 操作日志仓储实现 — W1-M3 窗口切换日志持久化。
/// 提供 operation_logs 表的插入和按日期查询能力。
/// 遵循 ActivityEventRepository 的读写模式：参数化 SQL、批量事务、ISO 8601 时间戳。
/// </summary>
public class OperationLogRepository : IOperationLogRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    public OperationLogRepository(SqliteContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task<OperationLog> InsertAsync(OperationLog log)
    {
        const string sql = @"
            INSERT INTO operation_logs
                (timestamp, window_title, process_name, process_id, process_path, category, detail)
            VALUES
                (@timestamp, @window_title, @process_name, @process_id, @process_path, @category, @detail);

            SELECT last_insert_rowid();";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, log);

        var result = await cmd.ExecuteScalarAsync();
        log.Id = Convert.ToInt64(result);
        return log;
    }

    /// <inheritdoc />
    public async Task InsertBatchAsync(IEnumerable<OperationLog> logs)
    {
        const string sql = @"
            INSERT INTO operation_logs
                (timestamp, window_title, process_name, process_id, process_path, category, detail)
            VALUES
                (@timestamp, @window_title, @process_name, @process_id, @process_path, @category, @detail);";

        using var connection = await _db.GetConnectionAsync();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction as SqliteTransaction;

        foreach (var log in logs)
        {
            cmd.Parameters.Clear();
            BindParameters(cmd, log);
            await cmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    /// <inheritdoc />
    public Task<List<OperationLog>> GetByDateAsync(DateTime date)
        => GetOperationLogsAsync(date);

    /// <inheritdoc />
    public async Task<List<OperationLog>> GetOperationLogsAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        const string sql = @"
            SELECT * FROM operation_logs
            WHERE timestamp >= @start AND timestamp < @end
            ORDER BY timestamp ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        return await ReadLogsAsync(cmd);
    }

    private static void BindParameters(SqliteCommand cmd, OperationLog log)
    {
        cmd.Parameters.AddWithValue("@timestamp", log.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@window_title", (object?)log.WindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_name", (object?)log.ProcessName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_id", (object?)log.ProcessId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_path", (object?)log.ProcessPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", (object?)log.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@detail", (object?)log.Detail ?? DBNull.Value);
    }

    private static async Task<List<OperationLog>> ReadLogsAsync(SqliteCommand cmd)
    {
        var logs = new List<OperationLog>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(ReadLog(reader));
        }

        return logs;
    }

    private static OperationLog ReadLog(SqliteDataReader reader)
    {
        var log = new OperationLog
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp"))),
        };

        if (!reader.IsDBNull(reader.GetOrdinal("window_title")))
            log.WindowTitle = reader.GetString(reader.GetOrdinal("window_title"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_name")))
            log.ProcessName = reader.GetString(reader.GetOrdinal("process_name"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_id")))
            log.ProcessId = reader.GetInt32(reader.GetOrdinal("process_id"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_path")))
            log.ProcessPath = reader.GetString(reader.GetOrdinal("process_path"));

        if (!reader.IsDBNull(reader.GetOrdinal("category")))
            log.Category = reader.GetString(reader.GetOrdinal("category"));

        if (!reader.IsDBNull(reader.GetOrdinal("detail")))
            log.Detail = reader.GetString(reader.GetOrdinal("detail"));

        return log;
    }
}
