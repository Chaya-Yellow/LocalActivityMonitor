namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 日报导出器接口。
/// 将指定日期的活动数据导出为 Markdown 格式的工作日报。
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// 异步导出指定日期的日报为 Markdown 字符串。
    /// </summary>
    /// <param name="date">日报日期。</param>
    /// <returns>Markdown 格式的日报内容。</returns>
    Task<string> ExportDailyAsync(DateTime date);

    /// <summary>
    /// 异步将 Markdown 日报写入文件。
    /// </summary>
    /// <param name="date">日报日期。</param>
    /// <param name="filePath">输出文件路径。为 null 时使用默认路径。</param>
    /// <returns>写入的文件路径。</returns>
    Task<string> ExportDailyToFileAsync(DateTime date, string? filePath = null);
}
