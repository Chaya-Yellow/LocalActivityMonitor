namespace ActivityMonitor.Core.Models;

/// <summary>
/// 误报记录模型。标记某条活动事件为误报，供后续审查或过滤使用。
/// <see cref="MisreportFlagRepository"/> 提供标记/取消/查询操作。
/// </summary>
public class MisreportFlag
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>关联的活动事件 ID（外键 → activity_events.id）。</summary>
    public long EventId { get; set; }

    /// <summary>
    /// 误报类型分类：
    /// <c>wrong_category</c>（分类错误）、<c>wrong_title</c>（标题错误）、
    /// <c>duplicate</c>（重复记录）、<c>noise</c>（无关噪音）、
    /// <c>privacy</c>（隐私内容）、<c>other</c>（其他）。
    /// </summary>
    public string FlagType { get; set; } = string.Empty;

    /// <summary>标记原因说明。</summary>
    public string FlagReason { get; set; } = string.Empty;

    /// <summary>是否已解决。已解决的误报不再影响统计过滤。</summary>
    public bool IsResolved { get; set; }

    /// <summary>用户备注。</summary>
    public string? Notes { get; set; }

    /// <summary>标记时间（ISO 8601）。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>解决时间（ISO 8601）；为 null 表示未解决。</summary>
    public DateTime? ResolvedAt { get; set; }
}
