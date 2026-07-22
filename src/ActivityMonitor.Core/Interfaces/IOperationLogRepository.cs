using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 操作日志仓储接口（W1-M3）。
/// 提供 <see cref="OperationLog"/> 的查询操作。
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// 获取指定日期的操作日志列表，按时间戳升序排列。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>操作日志列表。</returns>
    Task<List<OperationLog>> GetByDateAsync(DateTime date);

    /// <summary>
    /// 获取指定日期的操作日志列表（别名，供 MarkdownExporter 调用）。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>操作日志列表。</returns>
    Task<List<OperationLog>> GetOperationLogsAsync(DateTime date);
}
