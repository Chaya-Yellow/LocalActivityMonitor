namespace ActivityMonitor.Core.Models;

/// <summary>
/// 用户项目规则模型。定义对指定项目/进程的分类、标记等自动处理规则。
/// <see cref="UserProjectRuleRepository"/> 提供持久化 CRUD。
/// </summary>
public class UserProjectRule
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>项目名称或进程名称匹配模式（如 "code.exe"、"MyProject"）。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 规则类型：<c>category</c>（自动分类）、<c>work_tag</c>（工作标记）、
    /// <c>exclude</c>（排除）、<c>rename</c>（重命名标题）。
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>规则值：如目标分类名、工作标记值、排除标识等。</summary>
    public string RuleValue { get; set; } = string.Empty;

    /// <summary>优先级（数值越大优先级越高），用于多规则冲突时裁决。</summary>
    public int Priority { get; set; }

    /// <summary>是否启用。禁用规则不会参与自动处理。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>用户可读的描述说明。</summary>
    public string? Description { get; set; }

    /// <summary>创建时间（ISO 8601）。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（ISO 8601）。</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
