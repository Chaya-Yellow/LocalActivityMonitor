namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 周报导出器接口。
/// 将指定周的活动聚合数据导出为 Markdown 格式的工作周报（含环比对比）。
/// </summary>
public interface IWeeklyReportExporter
{
    /// <summary>
    /// 异步导出指定周的周报为 Markdown 字符串。
    /// </summary>
    /// <param name="dateInWeek">周内任意日期。</param>
    /// <returns>Markdown 格式的周报内容。</returns>
    Task<string> ExportWeeklyAsync(DateTime dateInWeek);

    /// <summary>
    /// 异步将 Markdown 周报写入文件。
    /// </summary>
    /// <param name="dateInWeek">周内任意日期。</param>
    /// <param name="filePath">输出文件路径。为 null 时使用默认路径。</param>
    /// <returns>写入的文件路径。</returns>
    Task<string> ExportWeeklyToFileAsync(DateTime dateInWeek, string? filePath = null);
}
