using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 设置项仓储实现。基于 settings 表提供键值对读写。
/// </summary>
public class SettingsRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public SettingsRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步获取指定键的设置值。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <returns>设置值；若键不存在返回 null。</returns>
    public async Task<string?> GetAsync(string key)
    {
        const string sql = "SELECT value FROM settings WHERE key = @key;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>
    /// 异步获取指定键的设置值，若不存在则返回默认值。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>设置值或默认值。</returns>
    public async Task<string> GetAsync(string key, string defaultValue)
    {
        var value = await GetAsync(key);
        return value ?? defaultValue;
    }

    /// <summary>
    /// 异步设置指定键的值。使用 INSERT OR REPLACE 实现 upsert。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <param name="value">设置值。</param>
    public async Task SetAsync(string key, string value)
    {
        const string sql = "INSERT OR REPLACE INTO settings (key, value) VALUES (@key, @value);";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步删除指定键的设置项。
    /// </summary>
    /// <param name="key">设置键名。</param>
    public async Task DeleteAsync(string key)
    {
        const string sql = "DELETE FROM settings WHERE key = @key;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@key", key);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取所有设置项。
    /// </summary>
    /// <returns>键值对字典。</returns>
    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        const string sql = "SELECT key, value FROM settings;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var result = new Dictionary<string, string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(reader.GetOrdinal("key"));
            var value = reader.IsDBNull(reader.GetOrdinal("value"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("value"));
            result[key] = value;
        }

        return result;
    }
}
