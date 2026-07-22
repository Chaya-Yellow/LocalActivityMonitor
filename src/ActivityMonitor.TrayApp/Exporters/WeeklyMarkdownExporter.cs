using System.IO;
using System.Text;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Repositories;

namespace ActivityMonitor.TrayApp.Exporters;

/// <summary>
/// Markdown 周报导出器。
/// 实现 <see cref="IWeeklyReportExporter"/>，生成包含环比对比的 Markdown 工作周报。
/// 复用 <see cref="MarkdownExporter.FormatDuration"/> 进行时长格式化。
/// </summary>
public class WeeklyMarkdownExporter : IWeeklyReportExporter
{
    private readonly WeeklySummaryRepository _weeklyRepo;
    private readonly WeeklyReportBuilder _builder;

    /// <summary>
    /// 使用指定的仓储和构建器初始化。
    /// </summary>
    public WeeklyMarkdownExporter(
        WeeklySummaryRepository weeklyRepo,
        WeeklyReportBuilder? builder = null)
    {
        _weeklyRepo = weeklyRepo;
        _builder = builder ?? new WeeklyReportBuilder(weeklyRepo);
    }

    /// <inheritdoc />
    public async Task<string> ExportWeeklyAsync(DateTime dateInWeek)
    {
        var data = await _builder.BuildAsync(dateInWeek);
        return FormatMarkdown(data);
    }

    /// <inheritdoc />
    public async Task<string> ExportWeeklyToFileAsync(DateTime dateInWeek, string? filePath = null)
    {
        var markdown = await ExportWeeklyAsync(dateInWeek);
        var path = filePath ?? GetDefaultPath(dateInWeek);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, markdown, Encoding.UTF8);
        return path;
    }

    // ── Markdown 渲染 ─────────────────────────────────────────────

    private static string FormatMarkdown(WeeklyReportData data)
    {
        var sb = new StringBuilder(4096);

        // ── 标题 ──────────────────────────────────────────────────
        sb.Append("# 第");
        sb.Append(GetWeekNumber(data));
        sb.Append("周工作周报（");
        sb.Append(data.WeekStart);
        sb.Append(" ~ ");
        sb.Append(data.WeekEnd);
        sb.AppendLine("）");
        sb.AppendLine();

        // ── 概览 ──────────────────────────────────────────────────
        AppendOverview(sb, data);

        // ── 环比对比 ──────────────────────────────────────────────
        AppendComparison(sb, data);

        // ── 软件分布 ──────────────────────────────────────────────
        AppendBreakdownSection(sb, data.AppBreakdown, "软件分布", "软件名");

        // ── 项目分布 ──────────────────────────────────────────────
        AppendBreakdownSection(sb, data.ProjectBreakdown, "项目分布", "项目名");

        return sb.ToString();
    }

    private static void AppendOverview(StringBuilder sb, WeeklyReportData data)
    {
        sb.AppendLine("## 概览");
        sb.Append("- 本周总活跃时长：");
        sb.AppendLine(MarkdownExporter.FormatDuration(data.TotalActiveMs));
        sb.Append("- 日均活跃时长：");
        sb.Append(data.AvgDailyHours.ToString("F2"));
        sb.AppendLine("h");
        sb.Append("- 空闲/休息时长：");
        sb.AppendLine(MarkdownExporter.FormatDuration(data.TotalIdleMs));
        sb.AppendLine();
    }

    private static void AppendComparison(StringBuilder sb, WeeklyReportData data)
    {
        sb.AppendLine("## 环比对比");

        if (data.WeekComparison == null)
        {
            sb.AppendLine("（无上周对比数据）");
            sb.AppendLine();
            return;
        }

        var c = data.WeekComparison;

        sb.AppendLine("| 指标 | 本周 | 上周 | 增减 |");
        sb.AppendLine("|------|------|------|------|");

        // 总活跃时长
        sb.Append("| 总活跃时长 | ");
        sb.Append(MarkdownExporter.FormatDuration(c.ThisWeekTotalMs));
        sb.Append(" | ");
        sb.Append(MarkdownExporter.FormatDuration(c.LastWeekTotalMs));
        sb.Append(" | ");
        AppendChangeCell(sb, c.ChangeMs, c.ChangePercent);
        sb.AppendLine(" |");

        // 日均活跃
        sb.Append("| 日均活跃时长 | ");
        sb.Append(c.ThisWeekAvgDailyHours.ToString("F2"));
        sb.Append("h | ");
        sb.Append(c.LastWeekAvgDailyHours.ToString("F2"));
        sb.Append("h | ");

        var avgChange = c.ThisWeekAvgDailyHours - c.LastWeekAvgDailyHours;
        var avgChangePct = c.LastWeekAvgDailyHours > 0
            ? avgChange / c.LastWeekAvgDailyHours * 100
            : 0;
        var sign = avgChange >= 0 ? "+" : "";
        sb.Append(sign);
        sb.Append(avgChange.ToString("F2"));
        sb.Append("h (");
        sb.Append(sign);
        sb.Append(avgChangePct.ToString("F1"));
        sb.AppendLine("%) |");

        sb.AppendLine();
    }

    private static void AppendBreakdownSection(
        StringBuilder sb,
        List<BreakdownItem> items,
        string sectionTitle,
        string columnLabel)
    {
        sb.Append("## ");
        sb.AppendLine(sectionTitle);
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.Append("（无");
            sb.Append(columnLabel);
            sb.AppendLine("记录）");
            sb.AppendLine();
            return;
        }

        var hasLastWeek = items.Any(i => i.LastWeekMs.HasValue);

        if (hasLastWeek)
        {
            sb.Append("| ");
            sb.Append(columnLabel);
            sb.AppendLine(" | 本周时长 | 占比 | 上周时长 | 增减 |");
            sb.AppendLine("|------|------|------|------|------|");
        }
        else
        {
            sb.Append("| ");
            sb.Append(columnLabel);
            sb.AppendLine(" | 本周时长 | 占比 |");
            sb.AppendLine("|------|------|------|");
        }

        foreach (var item in items)
        {
            sb.Append("| ");
            sb.Append(item.Name);
            sb.Append(" | ");
            sb.Append(MarkdownExporter.FormatDuration(item.TotalMs));
            sb.Append(" | ");
            sb.Append(item.Percentage.ToString("F1"));
            sb.Append("% |");

            if (hasLastWeek)
            {
                sb.Append(" ");
                sb.Append(item.LastWeekMs.HasValue
                    ? MarkdownExporter.FormatDuration(item.LastWeekMs.Value)
                    : "-");
                sb.Append(" | ");
                AppendChangeCell(sb, item.ChangeMs, item.ChangePercent);
                sb.Append(" |");
            }

            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private static void AppendChangeCell(StringBuilder sb, long? changeMs, double? changePercent)
    {
        if (!changeMs.HasValue)
        {
            sb.Append("-");
            return;
        }

        var sign = changeMs.Value >= 0 ? "+" : "";
        sb.Append(sign);
        sb.Append(MarkdownExporter.FormatDuration(changeMs.Value));

        if (changePercent.HasValue)
        {
            sb.Append(" (");
            sb.Append(sign);
            sb.Append(changePercent.Value.ToString("F1"));
            sb.Append("%)");
        }
    }

    // ── 辅助 ──────────────────────────────────────────────────────

    private static int GetWeekNumber(WeeklyReportData data)
    {
        if (DateTime.TryParse(data.WeekStart, out var weekStart))
        {
            var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return calendar.GetWeekOfYear(
                weekStart,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
        return 0;
    }

    private static string GetDefaultPath(DateTime dateInWeek)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var (weekStart, _) = WeeklyAggregationService.GetWeekRange(dateInWeek);
        var weekNum = System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetWeekOfYear(weekStart, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var fileName = $"工作周报_W{weekNum}_{weekStart:yyyy-MM-dd}.md";
        return Path.Combine(documents, "ActivityMonitor", "Reports", fileName);
    }
}
