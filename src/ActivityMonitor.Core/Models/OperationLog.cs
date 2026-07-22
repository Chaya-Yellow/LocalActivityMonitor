namespace ActivityMonitor.Core.Models;

/// <summary>
/// 操作日志 — W1-M3 窗口切换日志。
/// 每次前台窗口切换时记录，包含切换目标窗口/进程的基本信息。
/// </summary>
public class OperationLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>事件发生时间。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>前台窗口标题。</summary>
    public string? WindowTitle { get; set; }

    /// <summary>进程名（含 .exe 后缀）。</summary>
    public string? ProcessName { get; set; }

    /// <summary>进程 ID。</summary>
    public int? ProcessId { get; set; }

    /// <summary>进程完整路径。</summary>
    public string? ProcessPath { get; set; }

    /// <summary>活动类别（如 app/web/file）。</summary>
    public string? Category { get; set; }

    /// <summary>详细信息/说明。</summary>
    public string? Detail { get; set; }
}
