using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 误报记录仓储。基于 misreport_flags 表提供标记、取消、查询接口。
/// </summary>
public class MisreportFlagRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public MisreportFlagRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步标记一条活动事件为误报。
    /// 如果该事件已被标记且未解决，返回现有记录而非重复插入。
    /// </summary>
    /// <param name="flag">误报记录（EventId、FlagType、FlagReason 为必填）。</param>
    /// <returns>创建的误报记录（含自增 ID）。</returns>
    public async Task<MisreportFlag> MarkAsync(MisreportFlag flag)
    {
        // 检查是否已存在未解决的标记
        var existing = await GetUnresolvedByEventAsync(flag.EventId);
        if (existing != null)
        {
            return existing;
        }

        const string sql = @"
            INSERT INTO misreport_flags
                (event_id, flag_type, flag_reason, is_resolved,
                 notes, created_at, resolved_at)
            VALUES
                (@event_id, @flag_type, @flag_reason, @is_resolved,
                 @notes, @created_at, @resolved_at);

            SELECT last_insert_rowid();";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, flag);

        var result = await cmd.ExecuteScalarAsync();
        flag.Id = Convert.ToInt64(result);
        return flag;
    }

    /// <summary>
    /// 异步取消（解决）指定事件 ID 的误报标记。
    /// 将 is_resolved 置为 1 并记录解决时间。
    /// </summary>
    /// <param name="eventId">活动事件 ID。</param>
    /// <returns>是否成功取消了标记（无未解决标记时返回 false）。</returns>
    public async Task<bool> CancelAsync(long eventId)
    {
        const string sql = @"
            UPDATE misreport_flags
            SET is_resolved = 1, resolved_at = @resolved_at
            WHERE event_id = @event_id AND is_resolved = 0;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@resolved_at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@event_id", eventId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 异步根据误报记录 ID 取消标记。
    /// </summary>
    /// <param name="flagId">误报记录 ID。</param>
    /// <returns>是否成功取消。</returns>
    public async Task<bool> CancelByIdAsync(long flagId)
    {
        const string sql = @"
            UPDATE misreport_flags
            SET is_resolved = 1, resolved_at = @resolved_at
            WHERE id = @id AND is_resolved = 0;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@resolved_at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", flagId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 异步根据主键获取误报记录。
    /// </summary>
    public async Task<MisreportFlag?> GetByIdAsync(long id)
    {
        const string sql = "SELECT * FROM misreport_flags WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        var flags = await ReadFlagsAsync(cmd);
        return flags.Count > 0 ? flags[0] : null;
    }

    /// <summary>
    /// 异步获取指定事件的所有误报标记。
    /// </summary>
    public async Task<List<MisreportFlag>> GetByEventAsync(long eventId)
    {
        const string sql = @"
            SELECT * FROM misreport_flags
            WHERE event_id = @event_id
            ORDER BY created_at DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@event_id", eventId);

        return await ReadFlagsAsync(cmd);
    }

    /// <summary>
    /// 异步获取指定事件的未解决误报标记。
    /// </summary>
    public async Task<MisreportFlag?> GetUnresolvedByEventAsync(long eventId)
    {
        const string sql = @"
            SELECT * FROM misreport_flags
            WHERE event_id = @event_id AND is_resolved = 0
            LIMIT 1;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@event_id", eventId);

        var flags = await ReadFlagsAsync(cmd);
        return flags.Count > 0 ? flags[0] : null;
    }

    /// <summary>
    /// 异步查询所有误报记录，支持按解决状态筛选。
    /// </summary>
    /// <param name="resolved">null 表示全部；true 仅已解决；false 仅未解决。</param>
    public async Task<List<MisreportFlag>> GetAllAsync(bool? resolved = null)
    {
        var sql = "SELECT * FROM misreport_flags";

        if (resolved.HasValue)
        {
            sql += resolved.Value ? " WHERE is_resolved = 1" : " WHERE is_resolved = 0";
        }

        sql += " ORDER BY created_at DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        return await ReadFlagsAsync(cmd);
    }

    /// <summary>
    /// 异步查询指定日期范围内的误报记录。
    /// </summary>
    /// <param name="start">起始时间。</param>
    /// <param name="end">结束时间。</param>
    /// <param name="resolved">null 表示全部；true 仅已解决；false 仅未解决。</param>
    public async Task<List<MisreportFlag>> GetByDateRangeAsync(
        DateTime start, DateTime end, bool? resolved = null)
    {
        var sql = @"
            SELECT m.* FROM misreport_flags m
            WHERE m.created_at >= @start AND m.created_at < @end";

        if (resolved.HasValue)
        {
            sql += resolved.Value ? " AND m.is_resolved = 1" : " AND m.is_resolved = 0";
        }

        sql += " ORDER BY m.created_at DESC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", start.ToString("O"));
        cmd.Parameters.AddWithValue("@end", end.ToString("O"));

        return await ReadFlagsAsync(cmd);
    }

    /// <summary>
    /// 异步获取指定类型的所有未解决误报数量。
    /// </summary>
    public async Task<int> CountUnresolvedByTypeAsync(string flagType)
    {
        const string sql = @"
            SELECT COUNT(*) FROM misreport_flags
            WHERE flag_type = @flag_type AND is_resolved = 0;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@flag_type", flagType);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// 异步获取所有未解决误报的总数。
    /// </summary>
    public async Task<int> CountUnresolvedAsync()
    {
        const string sql = "SELECT COUNT(*) FROM misreport_flags WHERE is_resolved = 0;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// 为 MisreportFlag 绑定参数到 SQL 命令。
    /// </summary>
    private static void BindParameters(SqliteCommand cmd, MisreportFlag flag)
    {
        cmd.Parameters.AddWithValue("@event_id", flag.EventId);
        cmd.Parameters.AddWithValue("@flag_type", flag.FlagType);
        cmd.Parameters.AddWithValue("@flag_reason", flag.FlagReason);
        cmd.Parameters.AddWithValue("@is_resolved", flag.IsResolved ? 1 : 0);
        cmd.Parameters.AddWithValue("@notes", (object?)flag.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", flag.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@resolved_at", (object?)flag.ResolvedAt?.ToString("O") ?? DBNull.Value);
    }

    /// <summary>
    /// 从 SqliteDataReader 读取误报记录列表。
    /// </summary>
    private static async Task<List<MisreportFlag>> ReadFlagsAsync(SqliteCommand cmd)
    {
        var flags = new List<MisreportFlag>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            flags.Add(ReadFlag(reader));
        }

        return flags;
    }

    /// <summary>
    /// 从当前行读取一条 MisreportFlag。
    /// </summary>
    private static MisreportFlag ReadFlag(SqliteDataReader reader)
    {
        return new MisreportFlag
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            EventId = reader.GetInt64(reader.GetOrdinal("event_id")),
            FlagType = reader.GetString(reader.GetOrdinal("flag_type")),
            FlagReason = reader.GetString(reader.GetOrdinal("flag_reason")),
            IsResolved = reader.GetInt32(reader.GetOrdinal("is_resolved")) != 0,
            Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("notes")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            ResolvedAt = reader.IsDBNull(reader.GetOrdinal("resolved_at"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("resolved_at"))),
        };
    }
}
