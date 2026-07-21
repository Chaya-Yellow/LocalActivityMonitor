namespace ActivityMonitor.Core.Models;

/// <summary>
/// 工作/非工作标记常量。
/// </summary>
public static class WorkTag
{
    /// <summary>工作活动。</summary>
    public const string Work = "work";

    /// <summary>休息/缓冲活动。</summary>
    public const string Break = "break";

    /// <summary>个人/非工作活动。</summary>
    public const string Personal = "personal";

    /// <summary>未知/未分类。</summary>
    public const string Unknown = "unknown";
}
