using System.IO;
using System.Text;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Data.Repositories;

namespace ActivityMonitor.TrayApp.Exporters;

/// <summary>
/// Markdown 日报导出器。
/// 实现 <see cref="IReportExporter"/>，生成 6 章节的 Markdown 工作日报。
/// 使用 StringBuilder 拼接，避免字符串频繁分配。
/// </summary>
public class MarkdownExporter : IReportExporter
{
    private static readonly string[] DayNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

    private readonly ActivityEventRepository _eventRepo;
    private readonly DailySummaryRepository _summaryRepo;
    private readonly DailyReportBuilder _builder;

    /// <summary>
    /// 使用指定的仓储和构建器初始化。
    /// </summary>
    public MarkdownExporter(
        ActivityEventRepository eventRepo,
        DailySummaryRepository summaryRepo,
        DailyReportBuilder? builder = null)
    {
        _eventRepo = eventRepo;
        _summaryRepo = summaryRepo;
        _builder = builder ?? new DailyReportBuilder();
    }

    /// <inheritdoc />
    public async Task<string> ExportDailyAsync(DateTime date)
    {
        var events = await _eventRepo.GetByDateAsync(date);
        var summary = await _summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
        var data = _builder.Build(events, summary);
        return FormatMarkdown(data);
    }

    /// <inheritdoc />
    public async Task<string> ExportDailyToFileAsync(DateTime date, string? filePath = null)
    {
        var markdown = await ExportDailyAsync(date);
        var path = filePath ?? GetDefaultPath(date);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, markdown, Encoding.UTF8);
        return path;
    }

    // ── Markdown 渲染 ─────────────────────────────────────────────

    private static string FormatMarkdown(DailyReportData data)
    {
        var sb = new StringBuilder(4096);

        // ── 标题 ──────────────────────────────────────────────────
        var dayName = DayNames[(int)data.Date.DayOfWeek];
        sb.Append("# 工作日报 - ");
        sb.Append(data.Date.ToString("yyyy-MM-dd"));
        sb.Append(" (");
        sb.Append(dayName);
        sb.AppendLine(")");
        sb.AppendLine();

        // ── 章节 1：今日概览 ──────────────────────────────────────
        AppendSection1_Overview(sb, data);

        // ── 章节 2：时间线 ────────────────────────────────────────
        AppendSection2_Timeline(sb, data);

        // ── 章节 3：项目分布 ──────────────────────────────────────
        AppendSection3_Projects(sb, data);

        // ── 章节 4：应用分布 ──────────────────────────────────────
        AppendSection4_Apps(sb, data);

        // ── 章节 5：网页分类 ──────────────────────────────────────
        AppendSection5_Web(sb, data);

        // ── 章节 6：手动补充 ──────────────────────────────────────
        AppendSection6_Notes(sb, data);

        return sb.ToString();
    }

    private static void AppendSection1_Overview(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## 📊 今日概览");
        sb.Append("- 工作时长：");
        sb.AppendLine(FormatDuration(data.TotalActiveMs));
        sb.Append("- 空闲/休息：");
        sb.AppendLine(FormatDuration(data.TotalIdleMs));
        sb.Append("- 睡眠/锁屏：");
        sb.AppendLine(FormatDuration(data.TotalSleepMs));

        var workPct = data.WorkRatio * 100;
        var nonWorkPct = 100 - workPct;
        sb.Append("- 工作占比：");
        sb.Append(workPct.ToString("F0"));
        sb.Append("% · 非工作占比：");
        sb.Append(nonWorkPct.ToString("F0"));
        sb.AppendLine("%");

        sb.AppendLine();
    }

    private static void AppendSection2_Timeline(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## ⏱ 时间线");

        if (data.MorningEntries.Count > 0)
        {
            sb.AppendLine("### 上午");
            AppendTimelineBlock(sb, data.MorningEntries);
        }

        if (data.AfternoonEntries.Count > 0)
        {
            sb.AppendLine("### 下午");
            AppendTimelineBlock(sb, data.AfternoonEntries);
        }

        if (data.MorningEntries.Count == 0 && data.AfternoonEntries.Count == 0)
            sb.AppendLine("（无记录）");

        sb.AppendLine();
    }

    private static void AppendTimelineBlock(StringBuilder sb, List<TimelineEntry> entries)
    {
        foreach (var entry in entries)
        {
            sb.Append("- **");
            sb.Append(entry.StartTimeFormatted);
            sb.Append(" - ");
            sb.Append(entry.EndTimeFormatted);
            sb.Append("** ");

            if (entry.IsIdle)
            {
                sb.Append("空闲");
                if (!string.IsNullOrWhiteSpace(entry.Detail))
                {
                    sb.Append(" · ");
                    sb.Append(entry.Detail);
                }
            }
            else if (entry.IsSleep)
            {
                sb.Append("睡眠/锁屏");
            }
            else
            {
                sb.Append(entry.AppName);

                if (!string.IsNullOrWhiteSpace(entry.ProjectName))
                {
                    sb.Append(" · `");
                    sb.Append(entry.ProjectName);
                    sb.Append('`');
                }

                if (!string.IsNullOrWhiteSpace(entry.Detail))
                {
                    sb.Append(" · ");
                    sb.Append(entry.Detail);
                }
                else if (!string.IsNullOrWhiteSpace(entry.WindowTitle))
                {
                    sb.Append(" · ");
                    sb.Append(entry.WindowTitle);
                }
            }

            sb.AppendLine();
        }
    }

    private static void AppendSection3_Projects(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## 📁 项目分布");
        AppendBreakdownTable(sb, data.ProjectBreakdown, data.TotalActiveMs, "项目");
    }

    private static void AppendSection4_Apps(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## 📈 应用分布");
        AppendBreakdownTable(sb, data.AppBreakdown, data.TotalActiveMs, "应用");
    }

    private static void AppendSection5_Web(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## 🌐 网页分类");

        if (data.DomainBreakdown.Count == 0)
        {
            sb.AppendLine("（无浏览器记录）");
        }
        else
        {
            // 按时长降序排列
            var sorted = data.DomainBreakdown
                .OrderByDescending(kv => kv.Value)
                .ToList();

            var totalWeb = sorted.Sum(kv => kv.Value);

            foreach (var (domain, ms) in sorted)
            {
                var pct = totalWeb > 0 ? (double)ms / totalWeb * 100 : 0;
                sb.Append("- ");
                sb.Append(domain);
                sb.Append(" · ");
                sb.Append(FormatDuration(ms));
                sb.Append(" (");
                sb.Append(pct.ToString("F0"));
                sb.AppendLine("%)");
            }
        }

        sb.AppendLine();
    }

    private static void AppendSection6_Notes(StringBuilder sb, DailyReportData data)
    {
        sb.AppendLine("## 📝 手动补充");

        if (string.IsNullOrWhiteSpace(data.UserNotes))
        {
            sb.AppendLine("> （暂无补充内容）");
        }
        else
        {
            foreach (var line in data.UserNotes.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append("> ");
                sb.AppendLine(line.Trim());
            }
        }

        sb.AppendLine();
    }

    // ── 辅助 ──────────────────────────────────────────────────────

    private static void AppendBreakdownTable(
        StringBuilder sb,
        Dictionary<string, long> breakdown,
        long totalMs,
        string columnLabel)
    {
        if (breakdown.Count == 0)
        {
            sb.Append("（无");
            sb.Append(columnLabel);
            sb.AppendLine("记录）");
            sb.AppendLine();
            return;
        }

        var sorted = breakdown.OrderByDescending(kv => kv.Value).ToList();

        sb.Append('|');
        sb.Append(columnLabel);
        sb.AppendLine(" | 时长 | 占比 |");
        sb.AppendLine("|------|------|------|");

        foreach (var (name, ms) in sorted)
        {
            var pct = totalMs > 0 ? (double)ms / totalMs * 100 : 0;
            sb.Append("| ");
            sb.Append(name);
            sb.Append(" | ");
            sb.Append(FormatDuration(ms));
            sb.Append(" | ");
            sb.Append(pct.ToString("F0"));
            sb.AppendLine("% |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// 将毫秒格式化为可读时长（如 "6h 42m"、"45m"、"30s"）。
    /// </summary>
    internal static string FormatDuration(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";

        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m";

        return $"{ts.Seconds}s";
    }

    private static string GetDefaultPath(DateTime date)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fileName = $"工作日报_{date:yyyy-MM-dd}.md";
        return Path.Combine(documents, "ActivityMonitor", "Reports", fileName);
    }
}
