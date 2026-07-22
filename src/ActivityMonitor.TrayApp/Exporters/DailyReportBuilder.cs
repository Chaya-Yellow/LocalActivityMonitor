using System.IO;
using System.Text.Json;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Exporters;

/// <summary>
/// 日报数据构建器。
/// 将原始 <see cref="ActivityEvent"/> 列表转换为结构化的 <see cref="DailyReportData"/>，
/// 供 <see cref="MarkdownExporter"/> 渲染 Markdown。
/// </summary>
public class DailyReportBuilder
{
    private static readonly TimeSpan Noon = new(12, 0, 0);

    /// <summary>
    /// 从活动事件和用户备注构建日报数据。
    /// </summary>
    /// <param name="events">指定日期的活动事件列表（按 StartTime 升序）。</param>
    /// <param name="summary">可选的每日聚合数据（用于用户备注等）。</param>
    public DailyReportData Build(DateTime date, IReadOnlyList<ActivityEvent> events, DailySummary? summary = null)
    {
        var data = new DailyReportData();

        if (events.Count == 0)
        {
            data.Date = date.Date;
            return data;
        }

        data.Date = events[0].StartTime.Date;

        // ── 概览统计 ──────────────────────────────────────────────
        foreach (var ev in events)
        {
            if (ev.Category == Category.Idle)
                data.TotalIdleMs += ev.DurationMs;
            else if (ev.Category == Category.Sleep)
                data.TotalSleepMs += ev.DurationMs;
            else
                data.TotalActiveMs += ev.DurationMs;

            if (ev.WorkTag == WorkTag.Work)
                data.WorkMs += ev.DurationMs;
            else if (ev.WorkTag is WorkTag.Break or WorkTag.Personal)
                data.NonWorkMs += ev.DurationMs;
        }

        // ── 时间线（上午 / 下午）──────────────────────────────────
        foreach (var ev in events)
        {
            var entry = new TimelineEntry
            {
                StartTime = ev.StartTime,
                EndTime = ev.EndTime,
                AppName = GetDisplayName(ev),
                ProjectName = ev.Project,
                WindowTitle = ev.EditedTitle ?? ev.WindowTitle,
                Detail = ev.EditedDesc ?? ev.Detail,
                Domain = ev.Domain,
                Category = ev.Category,
                DurationMs = ev.DurationMs,
                IsIdle = ev.Category == Category.Idle,
                IsSleep = ev.Category == Category.Sleep,
                // 来源追溯：保留原始窗口标题和进程路径
                RawWindowTitle = ev.RawWindowTitle ?? ev.WindowTitle,
                RawProcessPath = ev.RawProcessPath ?? ev.ProcessPath,
            };

            if (ev.StartTime.TimeOfDay < Noon)
                data.MorningEntries.Add(entry);
            else
                data.AfternoonEntries.Add(entry);
        }

        // ── 聚合分布 ──────────────────────────────────────────────
        data.AppBreakdown = BuildBreakdown(events,
            e => e.ProcessName,
            e => e.Category is not (Category.Idle or Category.Sleep)
                 && !string.IsNullOrWhiteSpace(e.ProcessName));

        data.ProjectBreakdown = BuildBreakdown(events,
            e => e.Project,
            e => e.Category is not (Category.Idle or Category.Sleep)
                 && !string.IsNullOrWhiteSpace(e.Project));

        data.DomainBreakdown = BuildBreakdown(events,
            e => e.Domain,
            e => e.Category is not (Category.Idle or Category.Sleep)
                 && !string.IsNullOrWhiteSpace(e.Domain));

        // ── 用户备注 ──────────────────────────────────────────────
        data.UserNotes = summary?.UserNotes;

        return data;
    }

    /// <summary>
    /// 将内部 Breakdown 字典序列化为 JSON（用于持久化）。
    /// </summary>
    public static string? SerializeBreakdown(IReadOnlyDictionary<string, long>? breakdown)
    {
        if (breakdown is null || breakdown.Count == 0)
            return null;

        return JsonSerializer.Serialize(breakdown);
    }

    /// <summary>
    /// 从 JSON 反序列化为 Breakdown 字典。
    /// </summary>
    public static Dictionary<string, long> DeserializeBreakdown(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, long>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json)
                   ?? new Dictionary<string, long>();
        }
        catch
        {
            return new Dictionary<string, long>();
        }
    }

    // ── 私有辅助 ──────────────────────────────────────────────────

    private static string GetDisplayName(ActivityEvent ev)
    {
        if (ev.Category == Category.Idle) return "空闲";
        if (ev.Category == Category.Sleep) return "睡眠/锁屏";
        if (!string.IsNullOrEmpty(ev.ProcessName))
            return Path.GetFileNameWithoutExtension(ev.ProcessName);
        return ev.ProcessName ?? "未知";
    }

    private static Dictionary<string, long> BuildBreakdown(
        IReadOnlyList<ActivityEvent> events,
        Func<ActivityEvent, string?> keySelector,
        Func<ActivityEvent, bool> filter)
    {
        var result = new Dictionary<string, long>();

        foreach (var ev in events)
        {
            if (!filter(ev)) continue;
            var key = keySelector(ev);
            if (string.IsNullOrWhiteSpace(key)) continue;

            result.TryGetValue(key, out var existing);
            result[key] = existing + ev.DurationMs;
        }

        return result;
    }
}

/// <summary>
/// 日报结构化数据模型。
/// </summary>
public class DailyReportData
{
    /// <summary>日报日期。</summary>
    public DateTime Date { get; set; }

    // ── 概览 ──
    public long TotalActiveMs { get; set; }
    public long TotalIdleMs { get; set; }
    public long TotalSleepMs { get; set; }
    public long WorkMs { get; set; }
    public long NonWorkMs { get; set; }

    /// <summary>工作占比（0.0-1.0）；无工作时返回 0。</summary>
    public double WorkRatio
    {
        get
        {
            var total = WorkMs + NonWorkMs;
            return total > 0 ? (double)WorkMs / total : 0;
        }
    }

    // ── 时间线 ──
    public List<TimelineEntry> MorningEntries { get; set; } = new();
    public List<TimelineEntry> AfternoonEntries { get; set; } = new();

    // ── 分布 ──
    public Dictionary<string, long> AppBreakdown { get; set; } = new();
    public Dictionary<string, long> ProjectBreakdown { get; set; } = new();
    public Dictionary<string, long> DomainBreakdown { get; set; } = new();

    // ── 备注 ──
    public string? UserNotes { get; set; }
}

/// <summary>
/// 时间线上的一条活动条目。
/// </summary>
public class TimelineEntry
{
    /// <summary>开始时间。</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间；为 null 表示进行中。</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>显示用应用名（如 "VS Code"、"Chrome"）。</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>项目名。</summary>
    public string? ProjectName { get; set; }

    /// <summary>窗口标题（优先显示用户编辑后的标题）。</summary>
    public string? WindowTitle { get; set; }

    /// <summary>详细描述（优先显示用户编辑后的描述）。</summary>
    public string? Detail { get; set; }

    /// <summary>浏览器域名。</summary>
    public string? Domain { get; set; }

    /// <summary>原始窗口标题（来源追溯 F2.6）。</summary>
    public string? RawWindowTitle { get; set; }

    /// <summary>原始进程完整路径（来源追溯 F2.6）。</summary>
    public string? RawProcessPath { get; set; }

    /// <summary>活动类别。</summary>
    public string Category { get; set; } = "app";

    /// <summary>活跃时长（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>是否空闲条目。</summary>
    public bool IsIdle { get; set; }

    /// <summary>是否睡眠/锁屏条目。</summary>
    public bool IsSleep { get; set; }

    /// <summary>格式化的开始时间（HH:mm）。</summary>
    public string StartTimeFormatted => StartTime.ToString("HH:mm");

    /// <summary>格式化的结束时间（HH:mm），进行中返回"至今"。</summary>
    public string EndTimeFormatted => EndTime?.ToString("HH:mm") ?? "至今";
}
