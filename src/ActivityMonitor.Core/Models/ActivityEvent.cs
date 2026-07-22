namespace ActivityMonitor.Core.Models;

/// <summary>
/// 核心活动事件模型，代表一条连续的前台活动记录。
/// </summary>
public class ActivityEvent
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>活动开始时间（ISO 8601）。</summary>
    public DateTime StartTime { get; set; }

    /// <summary>活动结束时间（ISO 8601）；为 null 表示进行中。</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>实际活跃毫秒数。</summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 活动类别：<see cref="Category.Web"/>、<see cref="Category.File"/>、
    /// <see cref="Category.App"/>、<see cref="Category.Idle"/>、<see cref="Category.Sleep"/>。
    /// </summary>
    public string Category { get; set; } = Models.Category.App;

    /// <summary>
    /// 工作标记：<see cref="WorkTag.Work"/>、<see cref="WorkTag.Break"/>、
    /// <see cref="WorkTag.Personal"/>、<see cref="WorkTag.Unknown"/>。
    /// </summary>
    public string WorkTag { get; set; } = Models.WorkTag.Unknown;

    /// <summary>细化子类别，如 "editor"、"terminal"、"remote"、"browser"。</summary>
    public string? SubCategory { get; set; }

    /// <summary>窗口标题。</summary>
    public string? WindowTitle { get; set; }

    /// <summary>可执行文件名（如 "code.exe"）。</summary>
    public string? ProcessName { get; set; }

    /// <summary>可执行文件完整路径。</summary>
    public string? ProcessPath { get; set; }

    /// <summary>进程 ID。</summary>
    public int? ProcessId { get; set; }

    /// <summary>详细描述：URL / 文件路径 / 其他上下文。</summary>
    public string? Detail { get; set; }

    /// <summary>浏览器域名。</summary>
    public string? Domain { get; set; }

    /// <summary>所属项目/仓库名称。</summary>
    public string? Project { get; set; }

    /// <summary>关键词 JSON 数组（如 ["keyword1", "keyword2"]）。</summary>
    public string? Keywords { get; set; }

    /// <summary>是否延续上一条记录（空闲后恢复场景）。</summary>
    public bool IsContinued { get; set; }

    /// <summary>是否隐私模式（无痕浏览窗口）。</summary>
    public bool IsPrivate { get; set; }

    /// <summary>是否崩溃恢复的记录。</summary>
    public bool IsCrashRecovered { get; set; }

    /// <summary>用户修改后的标题。</summary>
    public string? EditedTitle { get; set; }

    /// <summary>用户补充的描述（如空闲时段说明）。</summary>
    public string? EditedDesc { get; set; }

    /// <summary>用户重新标记的类别。</summary>
    public string? UserCategory { get; set; }

    /// <summary>
    /// 是否被用户标记为误报（通过右键菜单标记）。
    /// 误报记录将在实时统计中排除显示。
    /// </summary>
    public bool IsFalsePositive { get; set; }

    /// <summary>
    /// 原始窗口标题（捕获时的完整标题，不做任何截断/处理）。
    /// 用于来源追溯（F2.6），与 <see cref="WindowTitle"/> 的区别在于
    /// 该字段始终保留轮询时刻从 GetWindowText 获取的原始值。
    /// </summary>
    public string? RawWindowTitle { get; set; }

    /// <summary>
    /// 原始进程完整路径（捕获时的原始值，不做任何处理）。
    /// 用于来源追溯（F2.6），确保进程路径始终可追溯。
    /// </summary>
    public string? RawProcessPath { get; set; }
}
