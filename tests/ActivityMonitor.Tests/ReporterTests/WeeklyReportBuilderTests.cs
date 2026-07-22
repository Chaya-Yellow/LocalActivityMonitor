using System.IO;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using ActivityMonitor.TrayApp.Exporters;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ReporterTests;

public class WeeklyReportBuilderTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"weekly_{Guid.NewGuid():N}.db");

    private static async Task InitSchema(SqliteContext ctx)
    {
        await ctx.GetConnectionAsync();
    }

    private static void TryDel(string p) { try { File.Delete(p); } catch { } }

    /// <summary>
    /// 创建周聚合记录的快捷方法。
    /// 填充 daily_summaries 后再聚合，以确保同本周/上周数据完整。
    /// </summary>
    private static async Task SeedWeekAsync(
        DailySummaryRepository dailyRepo,
        WeeklySummaryRepository weeklyRepo,
        WeeklyAggregationService service,
        DateTime weekStart,
        long[] msPerDay,
        Dictionary<string, long>? appBreakdownTemplate = null,
        Dictionary<string, long>? projectBreakdownTemplate = null)
    {
        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var summary = new DailySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                TotalActiveMs = msPerDay[i],
                TotalIdleMs = 600000,
                WorkMs = msPerDay[i],
            };

            if (appBreakdownTemplate != null && msPerDay[i] > 0)
            {
                var scaled = new Dictionary<string, long>();
                foreach (var (k, v) in appBreakdownTemplate)
                {
                    // Scale the breakdown proportionally for this day
                    var proportion = (double)v / appBreakdownTemplate.Values.Sum();
                    scaled[k] = (long)(msPerDay[i] * proportion);
                }
                summary.AppBreakdown = DailyReportBuilder.SerializeBreakdown(scaled)
                    ?? string.Empty;
            }

            if (projectBreakdownTemplate != null && msPerDay[i] > 0)
            {
                var scaled = new Dictionary<string, long>();
                foreach (var (k, v) in projectBreakdownTemplate)
                {
                    var proportion = (double)v / projectBreakdownTemplate.Values.Sum();
                    scaled[k] = (long)(msPerDay[i] * proportion);
                }
                summary.ProjectBreakdown = DailyReportBuilder.SerializeBreakdown(scaled)
                    ?? string.Empty;
            }

            await dailyRepo.UpsertAsync(summary);
        }

        await service.AggregateAsync(weekStart);
    }

    // ════════════════════════════════════════════
    //  BuildAsync – 完整周数据
    // ════════════════════════════════════════════

    [Fact]
    public async Task BuildAsync_WithFullWeekData_ComputesCorrectBreakdown()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var builder = new WeeklyReportBuilder(weeklyRepo);

            var weekStart = new DateTime(2026, 7, 20); // Monday

            // 7 days, with app breakdown template
            long[] msPerDay = { 7200000, 5400000, 3600000, 9000000, 10800000, 7200000, 3600000 };
            var appTemplate = new Dictionary<string, long>
            {
                ["code.exe"] = 60,  // ~60% of time
                ["chrome.exe"] = 30, // ~30%
                ["excel.exe"] = 10,  // ~10%
            };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, weekStart, msPerDay, appTemplate);

            // Act
            var report = await builder.BuildAsync(weekStart);

            // Assert
            report.WeekStart.Should().Be("2026-07-20");
            report.WeekEnd.Should().Be("2026-07-26");

            var totalActive = msPerDay.Sum();
            report.TotalActiveMs.Should().Be(totalActive);

            report.AppBreakdown.Should().NotBeEmpty();
            report.AppBreakdown.Should().HaveCount(3);

            // All app breakdown items should have their proportions
            var totalBreakdownMs = report.AppBreakdown.Sum(i => i.TotalMs);
            totalBreakdownMs.Should().Be(totalActive);

            // No last week data, so all WoW fields should be null
            foreach (var item in report.AppBreakdown)
            {
                item.LastWeekMs.Should().BeNull();
                item.ChangeMs.Should().BeNull();
                item.ChangePercent.Should().BeNull();
            }

            report.WeekComparison.Should().BeNull();
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  BuildAsync – 周环比
    // ════════════════════════════════════════════

    [Fact]
    public async Task BuildAsync_WithWeekOverWeek_ComputesCorrectComparison()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var builder = new WeeklyReportBuilder(weeklyRepo);

            var thisWeekStart = new DateTime(2026, 7, 20); // Monday
            var lastWeekStart = thisWeekStart.AddDays(-7);

            // This week: total ~30h active
            long[] thisWeekMs = { 7200000, 7200000, 7200000, 0, 0, 0, 0 }; // 21600000 = 6h
            // Actually let's use more realistic data
            // Mon-Thu all with code.exe, with 10800000 per day
            long[] thisWeekMsReal = { 10800000, 10800000, 10800000, 10800000, 0, 0, 0 }; // 43200000 = 12h
            var appTemplate = new Dictionary<string, long>
            {
                ["code.exe"] = 100,
            };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, lastWeekStart,
                new long[] { 10800000, 0, 0, 0, 0, 0, 0 }, appTemplate);
            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, thisWeekStart,
                thisWeekMsReal, appTemplate);

            // Act
            var report = await builder.BuildAsync(thisWeekStart);

            // Assert
            report.WeekComparison.Should().NotBeNull();
            var cmp = report.WeekComparison!;

            // This week: 4 days * 10800000 = 43200000
            cmp.ThisWeekTotalMs.Should().Be(43200000);
            // Last week: 1 day * 10800000 = 10800000
            cmp.LastWeekTotalMs.Should().Be(10800000);
            // Change: 43200000 - 10800000 = 32400000
            cmp.ChangeMs.Should().Be(32400000);
            // Change percent: 32400000/10800000 * 100 = 300%
            cmp.ChangePercent.Should().Be(300);

            // App breakdown should show WoW comparison
            var codeItem = report.AppBreakdown.FirstOrDefault(i => i.Name == "code.exe");
            codeItem.Should().NotBeNull();
            codeItem!.LastWeekMs.Should().Be(10800000);
            codeItem.ChangeMs.Should().Be(32400000);
            codeItem.ChangePercent.Should().Be(300);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  BuildAsync – 无上周数据
    // ════════════════════════════════════════════

    [Fact]
    public async Task BuildAsync_MissingLastWeek_ReturnsNullComparison()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var builder = new WeeklyReportBuilder(weeklyRepo);

            var weekStart = new DateTime(2026, 7, 20);
            var appTemplate = new Dictionary<string, long> { ["code.exe"] = 100 };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, weekStart,
                new long[] { 10800000, 0, 0, 0, 0, 0, 0 }, appTemplate);

            // Act
            var report = await builder.BuildAsync(weekStart);

            // Assert
            report.WeekComparison.Should().BeNull();
            // Breakdown items should still exist but without WoW data
            report.AppBreakdown.Should().NotBeEmpty();
            foreach (var item in report.AppBreakdown)
            {
                item.LastWeekMs.Should().BeNull();
                item.ChangeMs.Should().BeNull();
                item.ChangePercent.Should().BeNull();
            }
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  BuildAsync – 无数据
    // ════════════════════════════════════════════

    [Fact]
    public async Task BuildAsync_NoData_ReturnsEmptyReport()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var builder = new WeeklyReportBuilder(weeklyRepo);

            // Act – no weekly data inserted at all
            var report = await builder.BuildAsync(new DateTime(2026, 7, 20));

            // Assert
            report.WeekStart.Should().Be("2026-07-20");
            report.WeekEnd.Should().Be("2026-07-26");
            report.TotalActiveMs.Should().Be(0);
            report.TotalIdleMs.Should().Be(0);
            report.AvgDailyHours.Should().Be(0);
            report.AppBreakdown.Should().BeEmpty();
            report.ProjectBreakdown.Should().BeEmpty();
            report.WeekComparison.Should().BeNull();
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  BuildAsync – 本周有但上周没有的项目
    // ════════════════════════════════════════════

    [Fact]
    public async Task BuildAsync_DifferentAppsAcrossWeeks_ComputesCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var builder = new WeeklyReportBuilder(weeklyRepo);

            var thisWeekStart = new DateTime(2026, 7, 20);
            var lastWeekStart = thisWeekStart.AddDays(-7);

            // Last week: only excel.exe
            var lastAppTemplate = new Dictionary<string, long> { ["excel.exe"] = 100 };
            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, lastWeekStart,
                new long[] { 7200000, 0, 0, 0, 0, 0, 0 }, lastAppTemplate);

            // This week: only code.exe
            var thisAppTemplate = new Dictionary<string, long> { ["code.exe"] = 100 };
            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, thisWeekStart,
                new long[] { 10800000, 0, 0, 0, 0, 0, 0 }, thisAppTemplate);

            // Act
            var report = await builder.BuildAsync(thisWeekStart);

            // Assert
            report.AppBreakdown.Should().HaveCount(2); // code.exe and excel.exe (from last week)

            var codeItem = report.AppBreakdown.First(i => i.Name == "code.exe");
            codeItem.TotalMs.Should().Be(10800000);
            codeItem.LastWeekMs.Should().BeNull();
            codeItem.ChangeMs.Should().BeNull();

            var excelItem = report.AppBreakdown.First(i => i.Name == "excel.exe");
            excelItem.TotalMs.Should().Be(0); // Not used this week
            excelItem.LastWeekMs.Should().Be(7200000);
            excelItem.ChangeMs.Should().Be(-7200000);
            excelItem.ChangePercent.Should().Be(-100);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  ExportWeeklyAsync – Markdown 格式
    // ════════════════════════════════════════════

    [Fact]
    public async Task ExportWeeklyAsync_ProducesValidMarkdown()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var exporter = new WeeklyMarkdownExporter(weeklyRepo);

            var weekStart = new DateTime(2026, 7, 20);
            var appTemplate = new Dictionary<string, long>
            {
                ["code.exe"] = 60,
                ["chrome.exe"] = 30,
                ["excel.exe"] = 10,
            };
            var projectTemplate = new Dictionary<string, long>
            {
                ["ProjectA"] = 70,
                ["ProjectB"] = 30,
            };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, weekStart,
                new long[] { 7200000, 5400000, 3600000, 9000000, 0, 0, 0 },
                appTemplate, projectTemplate);

            // Act
            var markdown = await exporter.ExportWeeklyAsync(weekStart);

            // Assert – 基本结构
            markdown.Should().Contain("# 第");
            markdown.Should().Contain("周工作周报");
            markdown.Should().Contain("2026-07-20");
            markdown.Should().Contain("2026-07-26");

            // 概览
            markdown.Should().Contain("## 概览");
            markdown.Should().Contain("本周总活跃时长");
            markdown.Should().Contain("日均活跃时长");
            markdown.Should().Contain("空闲/休息时长");

            // 环比对比（无上周数据）
            markdown.Should().Contain("## 环比对比");
            markdown.Should().Contain("无上周对比数据");

            // 软件分布
            markdown.Should().Contain("## 软件分布");
            markdown.Should().Contain("code.exe");
            markdown.Should().Contain("chrome.exe");
            markdown.Should().Contain("excel.exe");

            // 项目分布
            markdown.Should().Contain("## 项目分布");
            markdown.Should().Contain("ProjectA");
            markdown.Should().Contain("ProjectB");
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task ExportWeeklyAsync_WithWowComparison_ContainsComparisonTable()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var exporter = new WeeklyMarkdownExporter(weeklyRepo);

            var thisWeekStart = new DateTime(2026, 7, 20);
            var lastWeekStart = thisWeekStart.AddDays(-7);

            var appTemplate = new Dictionary<string, long> { ["code.exe"] = 100 };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, lastWeekStart,
                new long[] { 10800000, 0, 0, 0, 0, 0, 0 }, appTemplate);
            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, thisWeekStart,
                new long[] { 10800000, 10800000, 0, 0, 0, 0, 0 }, appTemplate);

            // Act
            var markdown = await exporter.ExportWeeklyAsync(thisWeekStart);

            // Assert – 环比对比表格
            markdown.Should().Contain("## 环比对比");
            markdown.Should().Contain("| 指标 | 本周 | 上周 | 增减 |");
            markdown.Should().Contain("| 总活跃时长 |");

            // 不应再显示"无上周对比数据"
            markdown.Should().NotContain("无上周对比数据");

            // 软件分布表格应有上周时长和增减列
            markdown.Should().Contain("上周时长");
            markdown.Should().Contain("增减");
            markdown.Should().Contain("code.exe");

            // 验证有具体的时长数字
            markdown.Should().Contain("6h"); // 21600000ms = 6h
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task ExportWeeklyAsync_NoData_ReturnsEmptyMarkdown()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var exporter = new WeeklyMarkdownExporter(weeklyRepo);

            // Act
            var markdown = await exporter.ExportWeeklyAsync(new DateTime(2026, 7, 20));

            // Assert
            markdown.Should().Contain("# 第");
            markdown.Should().Contain("周工作周报");
            markdown.Should().Contain("2026-07-20");
            markdown.Should().Contain("2026-07-26");
            markdown.Should().Contain("## 概览");
            markdown.Should().Contain("0s"); // FormatDuration(0) => "0s"
            markdown.Should().Contain("## 环比对比");
            markdown.Should().Contain("无上周对比数据");
            markdown.Should().Contain("## 软件分布");
            markdown.Should().Contain("（无软件名记录）");
            markdown.Should().Contain("## 项目分布");
            markdown.Should().Contain("（无项目名记录）");
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task ExportWeeklyToFileAsync_WritesFileWithContent()
    {
        var db = TempDb();
        var dir = Path.Combine(Path.GetTempPath(), $"weekly_file_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "weekly-report.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var aggService = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);
            var exporter = new WeeklyMarkdownExporter(weeklyRepo);

            var weekStart = new DateTime(2026, 7, 20);
            var appTemplate = new Dictionary<string, long> { ["code.exe"] = 100 };

            await SeedWeekAsync(dailyRepo, weeklyRepo, aggService, weekStart,
                new long[] { 7200000, 0, 0, 0, 0, 0, 0 }, appTemplate);

            // Act
            var result = await exporter.ExportWeeklyToFileAsync(weekStart, file);

            // Assert
            result.Should().Be(file);
            File.Exists(file).Should().BeTrue();
            var content = await File.ReadAllTextAsync(file);
            content.Should().Contain("# 第");
            content.Should().Contain("周工作周报");
        }
        finally
        {
            try { File.Delete(file); } catch { }
            try { Directory.Delete(dir, true); } catch { }
            try { File.Delete(db); } catch { }
        }
    }
}
