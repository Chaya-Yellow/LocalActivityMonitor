using System.Text.Json;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Aggregation;

/// <summary>
/// 每周聚合服务。
/// 每周一执行，聚合前 7 天（周一至周日）的每日汇总数据。
/// 从 daily_summaries 表读取已有聚合结果合并，不再查询原始事件表。
/// </summary>
public class WeeklyAggregationService
{
    private readonly SqliteContext _db;
    private readonly DailySummaryRepository _dailyRepo;
    private readonly WeeklySummaryRepository _weeklyRepo;

    /// <summary>
    /// 使用指定的数据库上下文和仓储初始化。
    /// </summary>
    public WeeklyAggregationService(
        SqliteContext db,
        DailySummaryRepository dailyRepo,
        WeeklySummaryRepository weeklyRepo)
    {
        _db = db;
        _dailyRepo = dailyRepo;
        _weeklyRepo = weeklyRepo;
    }

    /// <summary>
    /// 对包含指定日期的周执行聚合。
    /// 会自动计算该周周一至周日的范围。
    /// </summary>
    /// <param name="dateInWeek">周内任意日期。</param>
    public async Task AggregateAsync(DateTime dateInWeek)
    {
        var (weekStart, weekEnd) = GetWeekRange(dateInWeek);

        var weekStartStr = weekStart.ToString("yyyy-MM-dd");
        var weekEndStr = weekEnd.ToString("yyyy-MM-dd");

        // ── 1. 读取本周所有日汇总 ────────────────────────────────
        var dailyList = await _dailyRepo.GetRangeAsync(weekStartStr, weekEndStr);

        if (dailyList.Count == 0)
        {
            // 无数据时也生成一条空记录占位
            await _weeklyRepo.UpsertAsync(new WeeklySummary
            {
                WeekStart = weekStartStr,
                WeekEnd = weekEndStr,
            });
            return;
        }

        // ── 2. 合并数值字段 ──────────────────────────────────────
        var totalActiveMs = dailyList.Sum(d => d.TotalActiveMs);
        var totalIdleMs = dailyList.Sum(d => d.TotalIdleMs);
        var avgDailyHours = dailyList.Count > 0
            ? dailyList.Average(d => d.TotalActiveMs / 3600000.0)
            : 0;

        // ── 3. 合并 Breakdown JSON ───────────────────────────────
        var appBreakdown = MergeBreakdowns(dailyList.Select(d => d.AppBreakdown));
        var domainBreakdown = MergeBreakdowns(dailyList.Select(d => d.DomainBreakdown));
        var projectBreakdown = MergeBreakdowns(dailyList.Select(d => d.ProjectBreakdown));

        // ── 4. 写入 ──────────────────────────────────────────────
        var weekly = new WeeklySummary
        {
            WeekStart = weekStartStr,
            WeekEnd = weekEndStr,
            TotalActiveMs = totalActiveMs,
            TotalIdleMs = totalIdleMs,
            AppBreakdown = SerializeBreakdown(appBreakdown),
            DomainBreakdown = SerializeBreakdown(domainBreakdown),
            ProjectBreakdown = SerializeBreakdown(projectBreakdown),
            AvgDailyHours = Math.Round(avgDailyHours, 2),
        };

        await _weeklyRepo.UpsertAsync(weekly);
    }

    /// <summary>
    /// 获取指定日期所在周的周一和周日。
    /// </summary>
    public static (DateTime weekStart, DateTime weekEnd) GetWeekRange(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - 1) % 7; // 周一 = 0
        var weekStart = date.Date.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6);
        return (weekStart, weekEnd);
    }

    // ── 辅助 ──────────────────────────────────────────────────────

    /// <summary>
    /// 合并多个 Breakdown JSON 字典，相同 key 累加。
    /// </summary>
    private static Dictionary<string, long> MergeBreakdowns(IEnumerable<string?> jsonSources)
    {
        var result = new Dictionary<string, long>();

        foreach (var json in jsonSources)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (dict is null) continue;

                foreach (var (key, value) in dict)
                {
                    result.TryGetValue(key, out var existing);
                    result[key] = existing + value;
                }
            }
            catch
            {
                // 单个 JSON 解析失败不影响其他天
            }
        }

        return result;
    }

    private static string? SerializeBreakdown(Dictionary<string, long> dict)
    {
        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }
}
