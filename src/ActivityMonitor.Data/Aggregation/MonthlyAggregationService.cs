using System.Text.Json;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Aggregation;

/// <summary>
/// 每月聚合服务。
/// 每月 1 日执行，聚合上月整月的每日汇总数据。
/// 从 daily_summaries 表读取已有聚合结果合并，不再查询原始事件表。
/// </summary>
public class MonthlyAggregationService
{
    private readonly SqliteContext _db;
    private readonly DailySummaryRepository _dailyRepo;
    private readonly MonthlySummaryRepository _monthlyRepo;

    /// <summary>
    /// 使用指定的数据库上下文和仓储初始化。
    /// </summary>
    public MonthlyAggregationService(
        SqliteContext db,
        DailySummaryRepository dailyRepo,
        MonthlySummaryRepository monthlyRepo)
    {
        _db = db;
        _dailyRepo = dailyRepo;
        _monthlyRepo = monthlyRepo;
    }

    /// <summary>
    /// 对指定月份执行聚合。
    /// </summary>
    /// <param name="year">年份。</param>
    /// <param name="month">月份（1-12）。</param>
    public async Task AggregateAsync(int year, int month)
    {
        var monthStr = $"{year:D4}-{month:D2}";
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");

        // ── 1. 读取本月所有日汇总 ────────────────────────────────
        var dailyList = await _dailyRepo.GetRangeAsync(startStr, endStr);

        if (dailyList.Count == 0)
        {
            // 无数据时也生成一条空记录占位
            await _monthlyRepo.UpsertAsync(new MonthlySummary
            {
                Month = monthStr,
            });
            return;
        }

        // ── 2. 合并数值字段 ──────────────────────────────────────
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var totalActiveMs = dailyList.Sum(d => d.TotalActiveMs);
        var totalIdleMs = dailyList.Sum(d => d.TotalIdleMs);
        var avgDailyHours = daysInMonth > 0
            ? dailyList.Sum(d => d.TotalActiveMs) / (double)(daysInMonth * 3600000)
            : 0;

        // ── 3. 合并 Breakdown JSON ───────────────────────────────
        var appBreakdown = MergeBreakdowns(dailyList.Select(d => d.AppBreakdown));
        var domainBreakdown = MergeBreakdowns(dailyList.Select(d => d.DomainBreakdown));
        var projectBreakdown = MergeBreakdowns(dailyList.Select(d => d.ProjectBreakdown));

        // ── 4. 写入 ──────────────────────────────────────────────
        var monthly = new MonthlySummary
        {
            Month = monthStr,
            TotalActiveMs = totalActiveMs,
            TotalIdleMs = totalIdleMs,
            AppBreakdown = SerializeBreakdown(appBreakdown),
            DomainBreakdown = SerializeBreakdown(domainBreakdown),
            ProjectBreakdown = SerializeBreakdown(projectBreakdown),
            AvgDailyHours = Math.Round(avgDailyHours, 2),
        };

        await _monthlyRepo.UpsertAsync(monthly);
    }

    /// <summary>
    /// 对指定日期所在的月份执行聚合。
    /// </summary>
    /// <param name="dateInMonth">月份内任意日期。</param>
    public async Task AggregateAsync(DateTime dateInMonth)
    {
        await AggregateAsync(dateInMonth.Year, dateInMonth.Month);
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
