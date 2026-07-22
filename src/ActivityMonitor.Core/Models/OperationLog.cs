namespace ActivityMonitor.Core.Models;

/// <summary>
/// 操作日志条目 — 记录前台窗口切换事件（W1-M3）。
/// 每次前台窗口标题或进程变化时记录一行，
/// 用于操作详情日志导出功能。
/// </summary>
public class OperationLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>事件发生时间戳。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>进程名称（如 "code.exe"）。</summary>
    public string? ProcessName { get; set; }

    /// <summary>窗口标题。</summary>
    public string? WindowTitle { get; set; }

    /// <summary>活动类别，与 ActivityEvent.Category 一致。</summary>
    public string? Category { get; set; }

    /// <summary>进程 ID。</summary>
    public int? ProcessId { get; set; }

    /// <summary>进程完整路径。</summary>
    public string? ProcessPath { get; set; }

    /// <summary>附加详情（如 URL、文件路径等上下文信息）。</summary>
    public string? Detail { get; set; }
}
