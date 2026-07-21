namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 设置项仓储接口。提供键值对形式的设置读写。
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// 异步获取指定键的设置值。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <returns>设置值；若键不存在返回 null。</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 异步获取指定键的设置值，若不存在则返回默认值。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>设置值或默认值。</returns>
    Task<string> GetAsync(string key, string defaultValue);

    /// <summary>
    /// 异步设置指定键的值。
    /// </summary>
    /// <param name="key">设置键名。</param>
    /// <param name="value">设置值。</param>
    Task SetAsync(string key, string value);

    /// <summary>
    /// 异步删除指定键的设置项。
    /// </summary>
    /// <param name="key">设置键名。</param>
    Task DeleteAsync(string key);

    /// <summary>
    /// 异步获取所有设置项。
    /// </summary>
    /// <returns>键值对字典。</returns>
    Task<Dictionary<string, string>> GetAllAsync();
}
