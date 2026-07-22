using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Repositories;

namespace ActivityMonitor.TrayApp.Exporters;

/// <summary>
/// 周报数据构建器。
/// 从 <see cref="WeeklySummaryRepository"/> 读取本周和上周聚合数据，
/// 构建包含占比和环比信息的 <see cref="WeeklyReportData"/>。
/// </summary>
public class WeeklyReportBuilder
{
    private readonly WeeklySummaryRepository _weeklyRepo;

    /// <summary>
    /// 使用指定的周聚合仓储初始化。
    /// </summary>
    /// <param name="weeklyRepo">周聚合仓储。</param>
    public WeeklyReportBuilder(WeeklySummaryRepository weeklyRepo)
    {
        _weeklyRepo = weeklyRepo;
    }

    /// <summary>
    /// 构建指定周数的周报数据，包含本周占比和上周环比。
    /// </summary>
    /// <param name="dateInWeek">周内任意日期。</param>
    /// <returns>周报数据模型，无数据时返回仅含日期范围的空模型。</returns>
    public async Task<WeeklyReportData> BuildAsync(DateTime dateInWeek)
    {
        var (weekStart, weekEnd) = WeeklyAggregationService.GetWeekRange(dateInWeek);
        var weekStartStr = weekStart.ToString("yyyy-MM-dd");
        var weekEndStr = weekEnd.ToString("yyyy-MM-dd");

        // 获取本周数据
        var thisWeek = await _weeklyRepo.GetAsync(weekStartStr);

        // 获取上周数据
        var lastWeekStart = weekStart.AddDays(-7);
        var lastWeek = await _weeklyRepo.GetAsync(lastWeekStart.ToString("yyyy-MM-dd"));

        if (thisWeek == null)
        {
            return new WeeklyReportData
            {
                WeekStart = weekStartStr,
                WeekEnd = weekEndStr,
            };
        }

        // 构建 BreakdownItem 列表
        var thisWeekAppDict = DailyReportBuilder.DeserializeBreakdown(thisWeek.AppBreakdown);
        var thisWeekProjectDict = DailyReportBuilder.DeserializeBreakdown(thisWeek.ProjectBreakdown);

        Dictionary<string, long>? lastWeekAppDict = null;
        Dictionary<string, long>? lastWeekProjectDict = null;
        if (lastWeek != null)
        {
            lastWeekAppDict = DailyReportBuilder.DeserializeBreakdown(lastWeek.AppBreakdown);
            lastWeekProjectDict = DailyReportBuilder.DeserializeBreakdown(lastWeek.ProjectBreakdown);
        }

        var appBreakdown = BuildBreakdownItems(thisWeekAppDict, thisWeek.TotalActiveMs, lastWeekAppDict);
        var projectBreakdown = BuildBreakdownItems(thisWeekProjectDict, thisWeek.TotalActiveMs, lastWeekProjectDict);

        // 构建环比对比
        WeekOverWeekComparison? comparison = null;
        if (lastWeek != null)
        {
            var changeMs = thisWeek.TotalActiveMs - lastWeek.TotalActiveMs;
            var changePercent = lastWeek.TotalActiveMs > 0
                ? (double)changeMs / lastWeek.TotalActiveMs * 100
                : 0;

            comparison = new WeekOverWeekComparison
            {
                ThisWeekTotalMs = thisWeek.TotalActiveMs,
                LastWeekTotalMs = lastWeek.TotalActiveMs,
                ChangeMs = changeMs,
                ChangePercent = Math.Round(changePercent, 1),
                ThisWeekAvgDailyHours = thisWeek.AvgDailyHours,
                LastWeekAvgDailyHours = lastWeek.AvgDailyHours,
            };
        }

        return new WeeklyReportData
        {
            WeekStart = weekStartStr,
            WeekEnd = weekEndStr,
            TotalActiveMs = thisWeek.TotalActiveMs,
            TotalIdleMs = thisWeek.TotalIdleMs,
            AvgDailyHours = thisWeek.AvgDailyHours,
            AppBreakdown = appBreakdown,
            ProjectBreakdown = projectBreakdown,
            WeekComparison = comparison,
        };
    }

    // ── 辅助 ──────────────────────────────────────────────────────

    /// <summary>
    /// 将本周字典转为 BreakdownItem 列表，并计算占比和环比。
    /// </summary>
    /// <param name="thisWeekDict">本周分布字典。</param>
    /// <param name="totalMs">本周总活跃时长（毫秒）。</param>
    /// <param name="lastWeekDict">上周分布字典，为 null 表示无上周数据。</param>
    private static List<BreakdownItem> BuildBreakdownItems(
        Dictionary<string, long> thisWeekDict,
        long totalMs,
        Dictionary<string, long>? lastWeekDict)
    {
        var items = new List<BreakdownItem>();

        // 先处理本周有的项
        foreach (var (name, ms) in thisWeekDict.OrderByDescending(kv => kv.Value))
        {
            var item = new BreakdownItem
            {
                Name = name,
                TotalMs = ms,
                Percentage = totalMs > 0 ? Math.Round((double)ms / totalMs * 100, 1) : 0,
            };

            if (lastWeekDict != null && lastWeekDict.TryGetValue(name, out var lastMs))
            {
                item.LastWeekMs = lastMs;
                item.ChangeMs = ms - lastMs;
                item.ChangePercent = lastMs > 0
                    ? Math.Round((double)(ms - lastMs) / lastMs * 100, 1)
                    : null;
            }

            items.Add(item);
        }

        // 再处理仅上周有的项（退出的软件/项目），放在末尾
        if (lastWeekDict != null)
        {
            foreach (var (name, lastMs) in lastWeekDict.OrderByDescending(kv => kv.Value))
            {
                if (thisWeekDict.ContainsKey(name))
                    continue; // 已在上面处理

                items.Add(new BreakdownItem
                {
                    Name = name,
                    TotalMs = 0,
                    Percentage = 0,
                    LastWeekMs = lastMs,
                    ChangeMs = -lastMs,
                    ChangePercent = -100,
                });
            }
        }

        return items;
    }
}
