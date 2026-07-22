using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 活动事件仓储实现。
/// 所有 SQL 使用参数化查询，批量操作使用事务包裹。
/// </summary>
public class ActivityEventRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public ActivityEventRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步插入单条活动事件。
    /// </summary>
    public async Task<ActivityEvent> InsertAsync(ActivityEvent @event)
    {
        const string sql = @"
            INSERT INTO activity_events
                (start_time, end_time, duration_ms, category, work_tag, sub_category,
                 window_title, process_name, process_path, process_id,
                 detail, domain, project, keywords,
                 is_continued, is_private, is_crash_recovered,
                 edited_title, edited_desc, user_category,
                 raw_window_title, raw_process_path)
            VALUES
                (@start_time, @end_time, @duration_ms, @category, @work_tag, @sub_category,
                 @window_title, @process_name, @process_path, @process_id,
                 @detail, @domain, @project, @keywords,
                 @is_continued, @is_private, @is_crash_recovered,
                 @edited_title, @edited_desc, @user_category,
                 @raw_window_title, @raw_process_path);

            SELECT last_insert_rowid();";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindActivityEventParameters(cmd, @event);

        var result = await cmd.ExecuteScalarAsync();
        @event.Id = Convert.ToInt64(result);
        return @event;
    }

    /// <summary>
    /// 异步批量插入多条活动事件，使用事务包裹。
    /// </summary>
    public async Task InsertBatchAsync(IEnumerable<ActivityEvent> events)
    {
        const string sql = @"
            INSERT INTO activity_events
                (start_time, end_time, duration_ms, category, work_tag, sub_category,
                 window_title, process_name, process_path, process_id,
                 detail, domain, project, keywords,
                 is_continued, is_private, is_crash_recovered,
                 edited_title, edited_desc, user_category,
                 raw_window_title, raw_process_path)
            VALUES
                (@start_time, @end_time, @duration_ms, @category, @work_tag, @sub_category,
                 @window_title, @process_name, @process_path, @process_id,
                 @detail, @domain, @project, @keywords,
                 @is_continued, @is_private, @is_crash_recovered,
                 @edited_title, @edited_desc, @user_category,
                 @raw_window_title, @raw_process_path);";

        using var connection = await _db.GetConnectionAsync();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction as SqliteTransaction;

        foreach (var @event in events)
        {
            cmd.Parameters.Clear();
            BindActivityEventParameters(cmd, @event);
            await cmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    /// <summary>
    /// 异步获取当天（00:00 至今）的所有活动事件，按开始时间升序排列。
    /// </summary>
    public async Task<List<ActivityEvent>> GetTodayEventsAsync()
    {
        var today = DateTime.Today;
        return await GetByDateAsync(today);
    }

    /// <summary>
    /// 异步获取指定日期的所有活动事件。
    /// </summary>
    public async Task<List<ActivityEvent>> GetByDateAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        const string sql = @"
            SELECT * FROM activity_events
            WHERE start_time >= @start AND start_time < @end
            ORDER BY start_time ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        return await ReadActivityEventsAsync(cmd);
    }

    /// <summary>
    /// 异步获取指定日期范围内的活动事件。
    /// </summary>
    public async Task<List<ActivityEvent>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        var startOfDay = start.Date;
        var endOfDay = end.Date.AddDays(1);

        const string sql = @"
            SELECT * FROM activity_events
            WHERE start_time >= @start AND start_time < @end
            ORDER BY start_time ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        return await ReadActivityEventsAsync(cmd);
    }

    /// <summary>
    /// 异步更新单条活动事件。
    /// </summary>
    public async Task UpdateAsync(ActivityEvent @event)
    {
        const string sql = @"
            UPDATE activity_events SET
                start_time = @start_time,
                end_time = @end_time,
                duration_ms = @duration_ms,
                category = @category,
                work_tag = @work_tag,
                sub_category = @sub_category,
                window_title = @window_title,
                process_name = @process_name,
                process_path = @process_path,
                process_id = @process_id,
                detail = @detail,
                domain = @domain,
                project = @project,
                keywords = @keywords,
                is_continued = @is_continued,
                is_private = @is_private,
                is_crash_recovered = @is_crash_recovered,
                edited_title = @edited_title,
                edited_desc = @edited_desc,
                user_category = @user_category,
                raw_window_title = @raw_window_title,
                raw_process_path = @raw_process_path
            WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindActivityEventParameters(cmd, @event);
        cmd.Parameters.AddWithValue("@id", @event.Id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步删除指定 ID 的活动事件。
    /// </summary>
    public async Task DeleteAsync(long id)
    {
        const string sql = "DELETE FROM activity_events WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取当天的基本统计信息。
    /// </summary>
    public async Task<DailyStats> GetDailyStatsAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        const string sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN category != 'idle' AND category != 'sleep' THEN duration_ms ELSE 0 END), 0) AS total_active_ms,
                COALESCE(SUM(CASE WHEN category = 'idle' THEN duration_ms ELSE 0 END), 0) AS total_idle_ms,
                COALESCE(SUM(CASE WHEN category = 'sleep' THEN duration_ms ELSE 0 END), 0) AS total_sleep_ms,
                COUNT(*) AS event_count
            FROM activity_events
            WHERE start_time >= @start AND start_time < @end;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
        cmd.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DailyStats
            {
                TotalActiveMs = reader.GetInt64(0),
                TotalIdleMs = reader.GetInt64(1),
                TotalSleepMs = reader.GetInt64(2),
                EventCount = reader.GetInt32(3),
            };
        }

        return new DailyStats();
    }

    /// <summary>
    /// 异步获取指定 ID 的活动事件。
    /// </summary>
    public async Task<ActivityEvent?> GetByIdAsync(long id)
    {
        const string sql = "SELECT * FROM activity_events WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        var events = await ReadActivityEventsAsync(cmd);
        return events.Count > 0 ? events[0] : null;
    }

    /// <summary>
    /// 为活动事件绑定参数到 SQL 命令。
    /// </summary>
    private static void BindActivityEventParameters(SqliteCommand cmd, ActivityEvent @event)
    {
        cmd.Parameters.AddWithValue("@start_time", @event.StartTime.ToString("O"));
        cmd.Parameters.AddWithValue("@end_time", (object?)@event.EndTime?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_ms", @event.DurationMs);
        cmd.Parameters.AddWithValue("@category", @event.Category);
        cmd.Parameters.AddWithValue("@work_tag", @event.WorkTag);
        cmd.Parameters.AddWithValue("@sub_category", (object?)@event.SubCategory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@window_title", (object?)@event.WindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_name", (object?)@event.ProcessName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_path", (object?)@event.ProcessPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@process_id", (object?)@event.ProcessId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@detail", (object?)@event.Detail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@domain", (object?)@event.Domain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@project", (object?)@event.Project ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@keywords", (object?)@event.Keywords ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_continued", @event.IsContinued ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_private", @event.IsPrivate ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_crash_recovered", @event.IsCrashRecovered ? 1 : 0);
        cmd.Parameters.AddWithValue("@edited_title", (object?)@event.EditedTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@edited_desc", (object?)@event.EditedDesc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@user_category", (object?)@event.UserCategory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@raw_window_title", (object?)@event.RawWindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@raw_process_path", (object?)@event.RawProcessPath ?? DBNull.Value);
    }

    /// <summary>
    /// 从 SqliteDataReader 读取活动事件列表。
    /// </summary>
    private static async Task<List<ActivityEvent>> ReadActivityEventsAsync(SqliteCommand cmd)
    {
        var events = new List<ActivityEvent>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            events.Add(ReadActivityEvent(reader));
        }

        return events;
    }

    /// <summary>
    /// 从当前行读取一条活动事件。
    /// </summary>
    private static ActivityEvent ReadActivityEvent(SqliteDataReader reader)
    {
        var @event = new ActivityEvent
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("start_time"))),
            DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
            Category = reader.GetString(reader.GetOrdinal("category")),
            WorkTag = reader.IsDBNull(reader.GetOrdinal("work_tag"))
                ? WorkTag.Unknown
                : reader.GetString(reader.GetOrdinal("work_tag")),
            IsContinued = reader.GetInt32(reader.GetOrdinal("is_continued")) != 0,
            IsPrivate = reader.GetInt32(reader.GetOrdinal("is_private")) != 0,
            IsCrashRecovered = reader.GetInt32(reader.GetOrdinal("is_crash_recovered")) != 0,
        };

        // Nullable fields
        if (!reader.IsDBNull(reader.GetOrdinal("end_time")))
            @event.EndTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("end_time")));

        if (!reader.IsDBNull(reader.GetOrdinal("sub_category")))
            @event.SubCategory = reader.GetString(reader.GetOrdinal("sub_category"));

        if (!reader.IsDBNull(reader.GetOrdinal("window_title")))
            @event.WindowTitle = reader.GetString(reader.GetOrdinal("window_title"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_name")))
            @event.ProcessName = reader.GetString(reader.GetOrdinal("process_name"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_path")))
            @event.ProcessPath = reader.GetString(reader.GetOrdinal("process_path"));

        if (!reader.IsDBNull(reader.GetOrdinal("process_id")))
            @event.ProcessId = reader.GetInt32(reader.GetOrdinal("process_id"));

        if (!reader.IsDBNull(reader.GetOrdinal("detail")))
            @event.Detail = reader.GetString(reader.GetOrdinal("detail"));

        if (!reader.IsDBNull(reader.GetOrdinal("domain")))
            @event.Domain = reader.GetString(reader.GetOrdinal("domain"));

        if (!reader.IsDBNull(reader.GetOrdinal("project")))
            @event.Project = reader.GetString(reader.GetOrdinal("project"));

        if (!reader.IsDBNull(reader.GetOrdinal("keywords")))
            @event.Keywords = reader.GetString(reader.GetOrdinal("keywords"));

        if (!reader.IsDBNull(reader.GetOrdinal("edited_title")))
            @event.EditedTitle = reader.GetString(reader.GetOrdinal("edited_title"));

        if (!reader.IsDBNull(reader.GetOrdinal("edited_desc")))
            @event.EditedDesc = reader.GetString(reader.GetOrdinal("edited_desc"));

        if (!reader.IsDBNull(reader.GetOrdinal("user_category")))
            @event.UserCategory = reader.GetString(reader.GetOrdinal("user_category"));

        if (!reader.IsDBNull(reader.GetOrdinal("raw_window_title")))
            @event.RawWindowTitle = reader.GetString(reader.GetOrdinal("raw_window_title"));

        if (!reader.IsDBNull(reader.GetOrdinal("raw_process_path")))
            @event.RawProcessPath = reader.GetString(reader.GetOrdinal("raw_process_path"));

        return @event;
    }

    /// <summary>
    /// 根据事件 ID 获取来源追溯信息（原始窗口标题 + 完整进程路径）。
    /// 仅查询所需字段，避免全表扫描开销。
    /// </summary>
    public async Task<EventSourceInfo?> GetSourceInfoAsync(long id)
    {
        const string sql = @"
            SELECT id, raw_window_title, raw_process_path, process_name,
                   start_time, end_time
            FROM activity_events
            WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new EventSourceInfo
            {
                EventId = reader.GetInt64(reader.GetOrdinal("id")),
                RawWindowTitle = reader.IsDBNull(reader.GetOrdinal("raw_window_title"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("raw_window_title")),
                RawProcessPath = reader.IsDBNull(reader.GetOrdinal("raw_process_path"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("raw_process_path")),
                ProcessName = reader.IsDBNull(reader.GetOrdinal("process_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("process_name")),
                StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("start_time"))),
                EndTime = reader.IsDBNull(reader.GetOrdinal("end_time"))
                    ? null
                    : DateTime.Parse(reader.GetString(reader.GetOrdinal("end_time"))),
            };
        }

        return null;
    }
}
