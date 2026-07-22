using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Aggregation;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

/// <summary>
/// TimeSegmentStatsService tests — W1-M2 半小时聚合查询。
/// 验证 48 时段聚合逻辑、软件列表、总时长和占比计算的正确性。
/// </summary>
public class TimeSegmentStatsServiceTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"TSS_{Guid.NewGuid():N}.db");

    private static void TryDel(string p) { try { File.Delete(p); } catch { } }

    // ════════════════════════════════════════════
    // 基本聚合逻辑
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetTimeSegmentStatsAsync_OneEventInMorning_ReturnsCorrectSegment()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // Insert an event at 09:30 lasting 30 min (10 min in bucket 19 = 09:30-09:59)
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(30),
                EndTime = date.AddHours(10).AddMinutes(5),
                DurationMs = 2_100_000, // 35 min
                Category = Category.App,
                ProcessName = "code.exe",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            // 48 segments always returned
            segments.Should().HaveCount(48);

            // Segment 19 = 09:30-09:59
            var seg19 = segments[19]; // 9:30 = idx 19
            seg19.TotalDurationMs.Should().BeGreaterThan(0);
            seg19.SoftwareList.Should().ContainSingle(s => s.Name == "code.exe");
            seg19.SoftwareList[0].DurationMs.Should().Be(2_100_000);
            seg19.SoftwareList[0].Percentage.Should().Be(100.0);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetTimeSegmentStatsAsync_MultipleEventsSameBucket_AggregatesCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // 2 events in the same morning bucket (09:00-09:29 = index 18)
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(0),
                DurationMs = 1_800_000, // 30 min
                Category = Category.App,
                ProcessName = "code.exe",
            });
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(10),
                DurationMs = 600_000, // 10 min
                Category = Category.Web,
                ProcessName = "chrome.exe",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            var seg = segments[18]; // 09:00-09:29
            seg.TotalDurationMs.Should().Be(2_400_000);
            seg.SoftwareList.Should().HaveCount(2);
            seg.SoftwareList[0].Name.Should().Be("code.exe"); // longest first
            seg.SoftwareList[0].DurationMs.Should().Be(1_800_000);
            seg.SoftwareList[1].Name.Should().Be("chrome.exe");
            seg.SoftwareList[1].DurationMs.Should().Be(600_000);
        }
        finally { TryDel(db); }
    }

    [Fact]
    public async Task GetTimeSegmentStatsAsync_Percentage_CalculatedCorrectly()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // bucket 09:00-09:29 (index 18): 2 apps with different durations
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(0),
                DurationMs = 1_200_000,
                Category = Category.App,
                ProcessName = "code.exe",
            });
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(5),
                DurationMs = 800_000,
                Category = Category.Web,
                ProcessName = "chrome.exe",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            var seg = segments[18];
            seg.TotalDurationMs.Should().Be(2_000_000);
            seg.SoftwareList[0].Percentage.Should().Be(60.0); // 1200000/2000000
            seg.SoftwareList[1].Percentage.Should().Be(40.0); // 800000/2000000
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 空数据场景
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetTimeSegmentStatsAsync_EmptyDay_Returns48EmptySegments()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var service = new TimeSegmentStatsService(ctx);

            var segments = await service.GetTimeSegmentStatsAsync(new DateTime(2026, 7, 22));

            segments.Should().HaveCount(48);
            segments.Should().AllSatisfy(s =>
            {
                s.TotalDurationMs.Should().Be(0);
                s.SoftwareList.Should().BeEmpty();
            });
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 排除 idle / sleep 类别
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetTimeSegmentStatsAsync_IdleAndSleep_ExcludedFromAggregation()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // Active event
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(0),
                DurationMs = 1_800_000,
                Category = Category.App,
                ProcessName = "code.exe",
            });
            // Idle event (should be excluded)
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(15),
                DurationMs = 600_000,
                Category = Category.Idle,
                ProcessName = "idle",
            });
            // Sleep event (should be excluded)
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(20),
                DurationMs = 300_000,
                Category = Category.Sleep,
                ProcessName = "sleep",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            // Segment 18 should only include the active event's duration
            var seg = segments[18];
            seg.TotalDurationMs.Should().Be(1_800_000); // only code.exe counted
            seg.SoftwareList.Should().ContainSingle(s => s.Name == "code.exe");
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 多时段场景
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetTimeSegmentStatsAsync_EventsSpanningMultipleSegments_EachBucketCorrect()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // Morning: code.exe in 09:00 bucket
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9).AddMinutes(0),
                DurationMs = 1_800_000,
                Category = Category.App,
                ProcessName = "code.exe",
            });
            // Afternoon: chrome.exe in 14:00 bucket
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(14).AddMinutes(0),
                DurationMs = 900_000,
                Category = Category.Web,
                ProcessName = "chrome.exe",
            });
            // Evening: code.exe in 20:00 bucket
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(20).AddMinutes(30),
                DurationMs = 3_600_000,
                Category = Category.App,
                ProcessName = "code.exe",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            segments[18].TotalDurationMs.Should().Be(1_800_000); // 09:00
            segments[18].SoftwareList[0].Name.Should().Be("code.exe");

            segments[28].TotalDurationMs.Should().Be(900_000); // 14:00
            segments[28].SoftwareList[0].Name.Should().Be("chrome.exe");

            segments[41].TotalDurationMs.Should().Be(3_600_000); // 20:30
            segments[41].SoftwareList[0].Name.Should().Be("code.exe");

            // Other segments should be empty
            segments[0].TotalDurationMs.Should().Be(0);
            segments[47].TotalDurationMs.Should().Be(0);
        }
        finally { TryDel(db); }
    }

    // ════════════════════════════════════════════
    // 按总时长降序排列
    // ════════════════════════════════════════════

    [Fact]
    public async Task GetTimeSegmentStatsAsync_SoftwareList_OrderedByDurationDesc()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new ActivityEventRepository(ctx);
            var service = new TimeSegmentStatsService(ctx);

            var date = new DateTime(2026, 7, 22);

            // 3 apps in the same bucket, varying durations
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10).AddMinutes(0),
                DurationMs = 300_000,
                Category = Category.App,
                ProcessName = "notepad.exe",
            });
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10).AddMinutes(5),
                DurationMs = 1_800_000,
                Category = Category.App,
                ProcessName = "code.exe",
            });
            await repo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(10).AddMinutes(10),
                DurationMs = 600_000,
                Category = Category.Web,
                ProcessName = "chrome.exe",
            });

            var segments = await service.GetTimeSegmentStatsAsync(date);

            var seg = segments[20]; // 10:00-10:29
            seg.SoftwareList.Should().HaveCount(3);
            seg.SoftwareList[0].Name.Should().Be("code.exe");     // 1800000 ms — longest
            seg.SoftwareList[1].Name.Should().Be("chrome.exe");   // 600000 ms
            seg.SoftwareList[2].Name.Should().Be("notepad.exe");  // 300000 ms — shortest
        }
        finally { TryDel(db); }
    }
}
