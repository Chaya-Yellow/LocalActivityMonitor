namespace ActivityMonitor.Core.Models;

/// <summary>
/// 活动类别常量。
/// </summary>
public static class Category
{
    /// <summary>浏览器网页活动。</summary>
    public const string Web = "web";

    /// <summary>文件编辑活动（编辑器、IDE、Office 等）。</summary>
    public const string File = "file";

    /// <summary>通用应用程序活动。</summary>
    public const string App = "app";

    /// <summary>空闲状态（超过空闲阈值无输入）。</summary>
    public const string Idle = "idle";

    /// <summary>系统睡眠/休眠/锁屏。</summary>
    public const string Sleep = "sleep";
}
