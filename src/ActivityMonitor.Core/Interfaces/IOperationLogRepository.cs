using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 操作日志仓储接口 — W1-M3 窗口切换日志。
/// 提供 <see cref="OperationLog"/> 的写入与按日期查询操作。
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// 异步插入单条操作日志。
    /// </summary>
    /// <param name="log">要插入的操作日志。</param>
    /// <returns>包含自增 ID 的完整操作日志。</returns>
    Task<OperationLog> InsertAsync(OperationLog log);

    /// <summary>
    /// 异步批量插入多条操作日志，使用事务包裹。
    /// </summary>
    /// <param name="logs">要插入的操作日志列表。</param>
    Task InsertBatchAsync(IEnumerable<OperationLog> logs);

    /// <summary>
    /// 异步获取指定日期的操作日志列表。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <returns>按时间升序排列的操作日志列表。</returns>
    Task<List<OperationLog>> GetOperationLogsAsync(DateTime date);
}
