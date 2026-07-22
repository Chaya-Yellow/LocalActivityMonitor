using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Database;

/// <summary>
/// SQLite 数据库上下文。
/// 负责数据库初始化、连接管理、DDL 迁移和索引创建。
/// </summary>
public class SqliteContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    /// <summary>
    /// 使用默认数据库路径初始化（%LOCALAPPDATA%\ActivityMonitor\data.db）。
    /// </summary>
    public SqliteContext()
        : this(GetDefaultDatabasePath())
    {
    }

    /// <summary>
    /// 使用指定的数据库路径初始化。
    /// </summary>
    /// <param name="databasePath">数据库文件完整路径。</param>
    public SqliteContext(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    /// <summary>
    /// 获取数据库连接。首次调用时创建连接并初始化数据库。
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
            await InitializeDatabaseAsync(_connection);
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        return _connection;
    }

    /// <summary>
    /// 获取一个新的独立数据库连接（不共享主连接），用于并发场景。
    /// 调用方负责释放。
    /// </summary>
    public async Task<SqliteConnection> OpenNewConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// 初始化数据库：创建表结构和索引。
    /// </summary>
    private static async Task InitializeDatabaseAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = GetCreateTableSql();
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = GetCreateIndexSql();
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = GetInsertDefaultsSql();
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取建表 DDL。
    /// </summary>
    private static string GetCreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS activity_events (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                start_time          TEXT NOT NULL,
                end_time            TEXT,
                duration_ms         INTEGER DEFAULT 0,
                category            TEXT NOT NULL DEFAULT 'app',
                work_tag            TEXT DEFAULT 'unknown',
                sub_category        TEXT,
                window_title        TEXT,
                process_name        TEXT,
                process_path        TEXT,
                process_id          INTEGER,
                detail              TEXT,
                domain              TEXT,
                project             TEXT,
                keywords            TEXT,
                is_continued        INTEGER DEFAULT 0,
                is_private          INTEGER DEFAULT 0,
                is_crash_recovered  INTEGER DEFAULT 0,
                edited_title        TEXT,
                edited_desc         TEXT,
                user_category       TEXT,
                raw_window_title    TEXT,
                raw_process_path    TEXT
            );

            CREATE TABLE IF NOT EXISTS daily_summaries (
                date                TEXT PRIMARY KEY,
                total_active_ms     INTEGER DEFAULT 0,
                total_idle_ms       INTEGER DEFAULT 0,
                total_sleep_ms      INTEGER DEFAULT 0,
                app_breakdown       TEXT,
                domain_breakdown    TEXT,
                project_breakdown   TEXT,
                work_ms             INTEGER DEFAULT 0,
                break_ms            INTEGER DEFAULT 0,
                keyword_cloud       TEXT,
                raw_report          TEXT,
                user_notes          TEXT
            );

            CREATE TABLE IF NOT EXISTS weekly_summaries (
                week_start          TEXT PRIMARY KEY,
                week_end            TEXT,
                total_active_ms     INTEGER DEFAULT 0,
                total_idle_ms       INTEGER DEFAULT 0,
                app_breakdown       TEXT,
                domain_breakdown    TEXT,
                project_breakdown   TEXT,
                avg_daily_hours     REAL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS monthly_summaries (
                month               TEXT PRIMARY KEY,
                total_active_ms     INTEGER DEFAULT 0,
                total_idle_ms       INTEGER DEFAULT 0,
                app_breakdown       TEXT,
                domain_breakdown    TEXT,
                project_breakdown   TEXT,
                avg_daily_hours     REAL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS settings (
                key                 TEXT PRIMARY KEY,
                value               TEXT
            );
        ";
    }

    /// <summary>
    /// 获取索引 DDL。
    /// </summary>
    private static string GetCreateIndexSql()
    {
        return @"
            CREATE INDEX IF NOT EXISTS idx_events_date ON activity_events(start_time);
            CREATE INDEX IF NOT EXISTS idx_events_cat  ON activity_events(category, start_time);
            CREATE INDEX IF NOT EXISTS idx_events_proc ON activity_events(process_name, start_time);
            CREATE INDEX IF NOT EXISTS idx_events_proj ON activity_events(project, start_time);
            CREATE INDEX IF NOT EXISTS idx_events_domain ON activity_events(domain, start_time);
        ";
    }

    /// <summary>
    /// 插入默认设置值。
    /// </summary>
    private static string GetInsertDefaultsSql()
    {
        return @"
            INSERT OR IGNORE INTO settings (key, value) VALUES ('idle_threshold_minutes', '15');
            INSERT OR IGNORE INTO settings (key, value) VALUES ('retention_days', '30');
            INSERT OR IGNORE INTO settings (key, value) VALUES ('auto_start', 'true');
            INSERT OR IGNORE INTO settings (key, value) VALUES ('poll_interval_ms', '2000');
        ";
    }

    /// <summary>
    /// 获取默认数据库路径：%LOCALAPPDATA%\ActivityMonitor\data.db。
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "ActivityMonitor", "data.db");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}
