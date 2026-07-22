using System.IO;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using ActivityMonitor.TrayApp.Exporters;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ReporterTests;

public class AggregationServiceTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"agg_{Guid.NewGuid():N}.db");

    private static async Task InitSchema(SqliteContext ctx)
    {
        await ctx.GetConnectionAsync();
    }

    private static void TryDel(string p) { try { File.Delete(p); } catch { } }

    // ════════════════════════════════════════════
    //  DailyAggregationService
    // ════════════════════════════════════════════

    [Fact]
    public async Task Daily_AggregateAsync_MultipleEvents_BreakdownSumsEqualTotal()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(8), EndTime = date.AddHours(9),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe", Project = "ProjectA",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), EndTime = date.AddHours(10),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe", Project = "ProjectA",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), EndTime = date.AddHours(10).AddMinutes(30),
                DurationMs = 1800000, Category = Category.Web, WorkTag = WorkTag.Work,
                ProcessName = "chrome.exe", Domain = "github.com", Project = "ProjectA",
            });

            await service.AggregateAsync(date);

            var s = await summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
            s!.TotalActiveMs.Should().Be(9000000);
            s.AppBreakdown.Should().NotBeNullOrWhiteSpace();
            s.ProjectBreakdown.Should().NotBeNullOrWhiteSpace();
            s.DomainBreakdown.Should().NotBeNullOrWhiteSpace();
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Daily_AggregateAsync_WithIdleAndSleep_CalculatesSeparateTotals()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(8), EndTime = date.AddHours(9),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe", Project = "ProjectA",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), EndTime = date.AddHours(10).AddMinutes(15),
                DurationMs = 900000, Category = Category.Idle, WorkTag = WorkTag.Unknown,
                ProcessName = "idle",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(13), EndTime = date.AddHours(14),
                DurationMs = 3600000, Category = Category.Sleep, WorkTag = WorkTag.Unknown,
                ProcessName = "sleep",
            });

            await service.AggregateAsync(date);

            var s = await summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
            s!.TotalActiveMs.Should().Be(3600000);
            s.TotalIdleMs.Should().Be(900000);
            s.TotalSleepMs.Should().Be(3600000);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Daily_AggregateAsync_NoEvents_WritesZeroSummary()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await service.AggregateAsync(date);

            var s = await summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
            s!.TotalActiveMs.Should().Be(0);
            s.WorkMs.Should().Be(0);
            s.BreakMs.Should().Be(0);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Daily_AggregateAsync_WorkBreakTag_SeparatesCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(8), EndTime = date.AddHours(9),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), EndTime = date.AddHours(9).AddMinutes(45),
                DurationMs = 2700000, Category = Category.Web, WorkTag = WorkTag.Work,
                ProcessName = "chrome.exe", Domain = "github.com",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), EndTime = date.AddHours(10).AddMinutes(15),
                DurationMs = 900000, Category = Category.App, WorkTag = WorkTag.Break,
                ProcessName = "spotify.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10).AddMinutes(15), EndTime = date.AddHours(11),
                DurationMs = 2700000, Category = Category.App, WorkTag = WorkTag.Personal,
                ProcessName = "steam.exe",
            });

            await service.AggregateAsync(date);

            var s = await summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
            s!.WorkMs.Should().Be(6300000);
            s.BreakMs.Should().Be(3600000);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Daily_AggregateRangeAsync_ThreeDays_AllHaveSummaries()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var d1 = new DateTime(2026, 7, 20);
            var d2 = new DateTime(2026, 7, 21);
            var d3 = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = d1.AddHours(9), EndTime = d1.AddHours(10),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = d2.AddHours(9), EndTime = d2.AddHours(11),
                DurationMs = 7200000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = d3.AddHours(9), EndTime = d3.AddHours(10).AddMinutes(30),
                DurationMs = 5400000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });

            await service.AggregateRangeAsync(d1, d3);

            (await summaryRepo.GetAsync(d1.ToString("yyyy-MM-dd")))!.TotalActiveMs.Should().Be(3600000);
            (await summaryRepo.GetAsync(d2.ToString("yyyy-MM-dd")))!.TotalActiveMs.Should().Be(7200000);
            (await summaryRepo.GetAsync(d3.ToString("yyyy-MM-dd")))!.TotalActiveMs.Should().Be(5400000);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Daily_AggregateAsync_AppBreakdown_ProducesValidJson()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var service = new DailyAggregationService(ctx, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(8), EndTime = date.AddHours(9),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), EndTime = date.AddHours(9).AddMinutes(30),
                DurationMs = 1800000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(30), EndTime = date.AddHours(10),
                DurationMs = 1800000, Category = Category.Web, WorkTag = WorkTag.Work,
                ProcessName = "chrome.exe", Domain = "github.com",
            });

            await service.AggregateAsync(date);

            var s = await summaryRepo.GetAsync(date.ToString("yyyy-MM-dd"));
            s!.AppBreakdown.Should().NotBeNullOrWhiteSpace();
            var bd = DailyReportBuilder.DeserializeBreakdown(s.AppBreakdown);
            bd["code.exe"].Should().Be(5400000);
            bd["chrome.exe"].Should().Be(1800000);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  WeeklyAggregationService
    // ════════════════════════════════════════════

    [Fact]
    public async Task Weekly_AggregateAsync_SevenDays_TotalEqualsSum()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var service = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);

            var ws = new DateTime(2026, 7, 20);
            long[] msPerDay = { 7200000, 5400000, 3600000, 9000000, 0, 10800000, 3600000 };
            long totalActive = 0;

            for (int i = 0; i < 7; i++)
            {
                totalActive += msPerDay[i];
                await dailyRepo.UpsertAsync(new DailySummary
                {
                    Date = ws.AddDays(i).ToString("yyyy-MM-dd"),
                    TotalActiveMs = msPerDay[i],
                    TotalIdleMs = 600000,
                    WorkMs = msPerDay[i],
                });
            }

            await service.AggregateAsync(ws);

            var w = await weeklyRepo.GetAsync(ws.ToString("yyyy-MM-dd"));
            w.Should().NotBeNull();
            w!.TotalActiveMs.Should().Be(totalActive);
            w.TotalIdleMs.Should().Be(600000 * 7);
            // avgDailyHours uses dailyList.Average() across all 7 items (incl. 0)
            double expectedAvg = (totalActive / 3600000.0) / 7.0;
            w.AvgDailyHours.Should().BeApproximately(expectedAvg, 0.01);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Weekly_AggregateAsync_NoDailySummaries_WritesEmptyWeekly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var service = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);

            await service.AggregateAsync(new DateTime(2026, 7, 20));

            var w = await weeklyRepo.GetAsync("2026-07-20");
            w.Should().NotBeNull();
            w!.TotalActiveMs.Should().Be(0);
            w.AvgDailyHours.Should().Be(0);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Weekly_AggregateAsync_PartialWeek_OnlyAvailableDaysCounted()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var service = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);

            var ws = new DateTime(2026, 7, 20);
            foreach (var off in new[] { 0, 2, 4 })
            {
                await dailyRepo.UpsertAsync(new DailySummary
                {
                    Date = ws.AddDays(off).ToString("yyyy-MM-dd"),
                    TotalActiveMs = 3600000, WorkMs = 3600000,
                });
            }

            await service.AggregateAsync(ws);

            var w = await weeklyRepo.GetAsync(ws.ToString("yyyy-MM-dd"));
            w!.TotalActiveMs.Should().Be(3600000 * 3);
            w.AvgDailyHours.Should().BeApproximately(
                3600000.0 * 3 / 3 / 3600000.0, 0.001);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public void Weekly_GetWeekRange_Monday_ReturnsMondayToSunday()
    {
        var (s, e) = WeeklyAggregationService.GetWeekRange(new DateTime(2026, 7, 20));
        s.Should().Be(new DateTime(2026, 7, 20));
        e.Should().Be(new DateTime(2026, 7, 26));
    }

    [Fact]
    public void Weekly_GetWeekRange_Wednesday_ReturnsSameMondayToSunday()
    {
        var (s, e) = WeeklyAggregationService.GetWeekRange(new DateTime(2026, 7, 22));
        s.Should().Be(new DateTime(2026, 7, 20));
        e.Should().Be(new DateTime(2026, 7, 26));
    }

    [Fact]
    public void Weekly_GetWeekRange_Sunday_ReturnsMondayToSunday()
    {
        var (s, e) = WeeklyAggregationService.GetWeekRange(new DateTime(2026, 7, 26));
        s.Should().Be(new DateTime(2026, 7, 20));
        e.Should().Be(new DateTime(2026, 7, 26));
    }

    [Fact]
    public async Task Weekly_AggregateAsync_BreakdownMerge_CombinesAcrossDays()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var weeklyRepo = new WeeklySummaryRepository(ctx);
            var service = new WeeklyAggregationService(ctx, dailyRepo, weeklyRepo);

            await dailyRepo.UpsertAsync(new DailySummary
            {
                Date = "2026-07-20", TotalActiveMs = 7200000,
                AppBreakdown = @"{""code.exe"":3600000,""chrome.exe"":3600000}",
                WorkMs = 7200000,
            });
            await dailyRepo.UpsertAsync(new DailySummary
            {
                Date = "2026-07-21", TotalActiveMs = 3600000,
                AppBreakdown = @"{""code.exe"":1800000,""excel.exe"":1800000}",
                WorkMs = 3600000,
            });

            await service.AggregateAsync(new DateTime(2026, 7, 20));

            var w = await weeklyRepo.GetAsync("2026-07-20");
            w!.AppBreakdown.Should().NotBeNullOrWhiteSpace();
            var bd = DailyReportBuilder.DeserializeBreakdown(w.AppBreakdown);
            bd["code.exe"].Should().Be(5400000);
            bd["chrome.exe"].Should().Be(3600000);
            bd["excel.exe"].Should().Be(1800000);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    //  MonthlyAggregationService
    // ════════════════════════════════════════════

    [Fact]
    public async Task Monthly_AggregateAsync_TenDays_TotalEqualsSum()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var monthlyRepo = new MonthlySummaryRepository(ctx);
            var service = new MonthlyAggregationService(ctx, dailyRepo, monthlyRepo);

            long total = 0;
            for (int d = 1; d <= 10; d++)
            {
                total += 3600000;
                await dailyRepo.UpsertAsync(new DailySummary
                {
                    Date = new DateTime(2026, 7, d).ToString("yyyy-MM-dd"),
                    TotalActiveMs = 3600000, WorkMs = 3600000,
                });
            }

            await service.AggregateAsync(2026, 7);

            var m = await monthlyRepo.GetAsync("2026-07");
            m!.TotalActiveMs.Should().Be(total);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Monthly_AggregateAsync_NoData_WritesEmptyMonthly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var monthlyRepo = new MonthlySummaryRepository(ctx);
            var service = new MonthlyAggregationService(ctx, dailyRepo, monthlyRepo);

            await service.AggregateAsync(2026, 8);

            var m = await monthlyRepo.GetAsync("2026-08");
            m!.TotalActiveMs.Should().Be(0);
            m.AvgDailyHours.Should().Be(0);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Monthly_AggregateAsync_DateTimeOverload_WorksCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var monthlyRepo = new MonthlySummaryRepository(ctx);
            var service = new MonthlyAggregationService(ctx, dailyRepo, monthlyRepo);

            await dailyRepo.UpsertAsync(new DailySummary
            {
                Date = "2026-07-15", TotalActiveMs = 7200000, WorkMs = 7200000,
            });

            await service.AggregateAsync(new DateTime(2026, 7, 15));

            var m = await monthlyRepo.GetAsync("2026-07");
            m!.TotalActiveMs.Should().Be(7200000);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Monthly_AggregateAsync_AvgDailyHours_UsesAllDaysInMonth()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var monthlyRepo = new MonthlySummaryRepository(ctx);
            var service = new MonthlyAggregationService(ctx, dailyRepo, monthlyRepo);

            long total = 0;
            for (int d = 1; d <= 5; d++)
            {
                total += 3600000;
                await dailyRepo.UpsertAsync(new DailySummary
                {
                    Date = new DateTime(2026, 7, d).ToString("yyyy-MM-dd"),
                    TotalActiveMs = 3600000, WorkMs = 3600000,
                });
            }

            await service.AggregateAsync(2026, 7);

            var m = await monthlyRepo.GetAsync("2026-07");
            m!.TotalActiveMs.Should().Be(total);
            m.AvgDailyHours.Should().BeApproximately(
                Math.Round(total / (31.0 * 3600000), 2), 0.001);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task Monthly_AggregateAsync_BreakdownMerge_CombinesAcrossDays()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchema(ctx);
            var dailyRepo = new DailySummaryRepository(ctx);
            var monthlyRepo = new MonthlySummaryRepository(ctx);
            var service = new MonthlyAggregationService(ctx, dailyRepo, monthlyRepo);

            await dailyRepo.UpsertAsync(new DailySummary
            {
                Date = "2026-07-01", TotalActiveMs = 7200000,
                AppBreakdown = @"{""code.exe"":3600000,""chrome.exe"":3600000}",
                DomainBreakdown = @"{""github.com"":3600000}",
                WorkMs = 7200000,
            });
            await dailyRepo.UpsertAsync(new DailySummary
            {
                Date = "2026-07-02", TotalActiveMs = 3600000,
                AppBreakdown = @"{""code.exe"":1800000,""excel.exe"":1800000}",
                DomainBreakdown = @"{""stackoverflow.com"":1800000}",
                WorkMs = 3600000,
            });

            await service.AggregateAsync(2026, 7);

            var m = await monthlyRepo.GetAsync("2026-07");
            m.Should().NotBeNull();

            var appBd = DailyReportBuilder.DeserializeBreakdown(m!.AppBreakdown);
            appBd["code.exe"].Should().Be(5400000);
            appBd["chrome.exe"].Should().Be(3600000);
            appBd["excel.exe"].Should().Be(1800000);

            var domBd = DailyReportBuilder.DeserializeBreakdown(m.DomainBreakdown);
            domBd["github.com"].Should().Be(3600000);
            domBd["stackoverflow.com"].Should().Be(1800000);
        }
        finally { TryDel(db); }
    }
}
