using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

/// <summary>
/// DailyStatsService tests — W1-M6 日统计视图。
/// 验证按日期查询活动明细和按软件聚合统计的正确性。
/// </summary>
public class DailyStatsServiceTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"DSS_{Guid.NewGuid():N}.db");

    private static void TryDel(string p) { try { File.Delete(p); } catch { } }

    // ════════════════════════════════════════════
    // GetDetailByDateAsync
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetDetailByDateAsync_EventsExist_ReturnsAllForThatDate()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 3600000,
                Category = Category.App, ProcessName = "code.exe",
                WindowTitle = "Code - main.cs",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), DurationMs = 1800000,
                Category = Category.Web, ProcessName = "chrome.exe",
                WindowTitle = "GitHub - Google Chrome", Domain = "github.com",
            });

            var result = await service.GetDetailByDateAsync(date);

            result.Should().HaveCount(2);
            result.Should().AllSatisfy(e => e.StartTime.Date.Should().Be(date));
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetDetailByDateAsync_NoEvents_ReturnsEmptyList()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var result = await service.GetDetailByDateAsync(new DateTime(2026, 1, 1));

            result.Should().BeEmpty();
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetDetailByDateAsync_EventsOnDifferentDates_ReturnsOnlyRequestedDate()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 3600000,
                Category = Category.App, ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddDays(-1).AddHours(10), DurationMs = 1800000,
                Category = Category.App, ProcessName = "notepad.exe",
            });

            var result = await service.GetDetailByDateAsync(date);

            result.Should().HaveCount(1);
            result[0].ProcessName.Should().Be("code.exe");
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetDetailByDateAsync_Events_OrderedByStartTimeAsc()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(14), DurationMs = 1800000,
                Category = Category.App, ProcessName = "late.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(8), DurationMs = 3600000,
                Category = Category.App, ProcessName = "early.exe",
            });

            var result = await service.GetDetailByDateAsync(date);

            result.Should().HaveCount(2);
            result[0].ProcessName.Should().Be("early.exe");
            result[1].ProcessName.Should().Be("late.exe");
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // GetSoftwareStatsByDateAsync
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_MultipleApps_AggregatesCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            // code.exe: 2 events totaling 1h
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 1800000,
                Category = Category.App, ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), DurationMs = 1800000,
                Category = Category.App, ProcessName = "code.exe",
            });
            // chrome.exe: 1 event totaling 30min
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(11), DurationMs = 1800000,
                Category = Category.Web, ProcessName = "chrome.exe",
            });

            var stats = await service.GetSoftwareStatsByDateAsync(date);

            stats.Should().HaveCount(2);
            stats[0].Name.Should().Be("code.exe");     // longest first
            stats[0].DurationMs.Should().Be(3600000);   // 2 * 1800000
            stats[0].RecordCount.Should().Be(2);
            stats[1].Name.Should().Be("chrome.exe");
            stats[1].DurationMs.Should().Be(1800000);
            stats[1].RecordCount.Should().Be(1);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_NoEvents_ReturnsEmptyList()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var stats = await service.GetSoftwareStatsByDateAsync(new DateTime(2026, 1, 1));

            stats.Should().BeEmpty();
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_Percentage_CalculatedCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 3_600_000,
                Category = Category.App, ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), DurationMs = 1_200_000,
                Category = Category.Web, ProcessName = "chrome.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(11), DurationMs = 1_200_000,
                Category = Category.App, ProcessName = "notepad.exe",
            });

            var stats = await service.GetSoftwareStatsByDateAsync(date);

            stats[0].Percentage.Should().Be(60.0); // 3600000/6000000
            stats[1].Percentage.Should().Be(20.0); // 1200000/6000000
            stats[2].Percentage.Should().Be(20.0);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_SingleApp_Has100Percent()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 3_600_000,
                Category = Category.App, ProcessName = "code.exe",
            });

            var stats = await service.GetSoftwareStatsByDateAsync(date);

            stats.Should().ContainSingle();
            stats[0].Percentage.Should().Be(100.0);
            stats[0].RecordCount.Should().Be(1);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 排除 idle / sleep
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_IdleAndSleep_Excluded()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 3_600_000,
                Category = Category.App, ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), DurationMs = 900_000,
                Category = Category.Idle, ProcessName = "idle",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(11), DurationMs = 600_000,
                Category = Category.Sleep, ProcessName = "sleep",
            });

            var stats = await service.GetSoftwareStatsByDateAsync(date);

            stats.Should().ContainSingle();
            stats[0].Name.Should().Be("code.exe");
            stats[0].DurationMs.Should().Be(3_600_000);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 按总时长降序排列
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetSoftwareStatsByDateAsync_MultipleApps_OrderedByDurationDesc()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var eventRepo = new ActivityEventRepository(ctx);
            var service = new DailyStatsService(ctx, eventRepo);

            var date = new DateTime(2026, 7, 22);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), DurationMs = 600_000,
                Category = Category.App, ProcessName = "notepad.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10), DurationMs = 3_600_000,
                Category = Category.App, ProcessName = "code.exe",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(11), DurationMs = 1_200_000,
                Category = Category.Web, ProcessName = "chrome.exe",
            });

            var stats = await service.GetSoftwareStatsByDateAsync(date);

            stats.Should().HaveCount(3);
            stats[0].Name.Should().Be("code.exe");      // 3600000
            stats[1].Name.Should().Be("chrome.exe");    // 1200000
            stats[2].Name.Should().Be("notepad.exe");   // 600000
        }
        finally { TryDel(db); }
    }
}
