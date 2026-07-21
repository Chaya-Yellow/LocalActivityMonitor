using System.Diagnostics;
using ActivityEvent = ActivityMonitor.Core.Models.ActivityEvent;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

/// <summary>
/// ActivityEventRepository tests using file-based SQLite for reliable isolation.
/// Each test gets a unique temp database file — no shared-cache issues.
/// </summary>
public class ActivityEventRepositoryTests : IDisposable
{
    private readonly string _dbPath;

    public ActivityEventRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"AM_EventRepo_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch { /* best-effort cleanup */ }
    }

    private SqliteContext CreateContext() => new(_dbPath);

    // -----------------------------------------------------------------------
    // TC-DB-002: InsertAsync – valid event returns with positive Id.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertAsync_ValidEvent_ReturnsEventWithPositiveId()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        var ev = new ActivityEvent
        {
            StartTime = DateTime.UtcNow,
            DurationMs = 5000,
            Category = Category.App,
            WorkTag = WorkTag.Work,
            ProcessName = "notepad.exe",
            WindowTitle = "Untitled - Notepad",
            IsPrivate = false,
        };

        // Act
        var result = await repo.InsertAsync(ev);

        // Assert
        result.Id.Should().BeGreaterThan(0);
        result.StartTime.Should().Be(ev.StartTime);
        result.DurationMs.Should().Be(5000);
        result.Category.Should().Be(Category.App);
        result.WorkTag.Should().Be(WorkTag.Work);
        result.ProcessName.Should().Be("notepad.exe");
        result.WindowTitle.Should().Be("Untitled - Notepad");
    }

    // -----------------------------------------------------------------------
    // TC-DB-003: InsertBatchAsync – 50 events all saved.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertBatchAsync_50Events_AllSavedSuccessfully()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        var events = Enumerable.Range(1, 50).Select(i => new ActivityEvent
        {
            StartTime = DateTime.UtcNow.AddMinutes(-i),
            DurationMs = 60000,
            Category = Category.App,
            ProcessName = $"process_{i}.exe",
            WindowTitle = $"Window {i}",
        }).ToList();

        // Act
        await repo.InsertBatchAsync(events);

        // Assert – count via a new connection (file persists)
        using var verifyCtx = CreateContext();
        using var conn = await verifyCtx.GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM activity_events;";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        count.Should().Be(50);
    }

    // -----------------------------------------------------------------------
    // TC-DB-004-a: GetTodayEventsAsync – returns only today's events.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetTodayEventsAsync_HasData_ReturnsOnlyTodayEvents()
    {
        // Arrange
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        using var insertCtx = CreateContext();
        using var conn = await insertCtx.GetConnectionAsync();

        // Insert yesterday(2), today(3), tomorrow(1)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO activity_events (start_time, duration_ms, category, process_name)
                VALUES (@y1, 1000, 'app', 'yesterday1'),
                       (@y2, 1000, 'app', 'yesterday2'),
                       (@t1, 1000, 'app', 'today1'),
                       (@t2, 1000, 'web', 'today2'),
                       (@t3, 1000, 'file', 'today3'),
                       (@tm1,1000, 'app', 'tomorrow1');";
            cmd.Parameters.AddWithValue("@y1", yesterday.AddHours(10).ToString("O"));
            cmd.Parameters.AddWithValue("@y2", yesterday.AddHours(15).ToString("O"));
            cmd.Parameters.AddWithValue("@t1", today.AddHours(9).ToString("O"));
            cmd.Parameters.AddWithValue("@t2", today.AddHours(12).ToString("O"));
            cmd.Parameters.AddWithValue("@t3", today.AddHours(16).ToString("O"));
            cmd.Parameters.AddWithValue("@tm1", tomorrow.AddHours(8).ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        // Act
        var result = await repo.GetTodayEventsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(e => e.StartTime.Date.Should().Be(today));
    }

    // -----------------------------------------------------------------------
    // TC-DB-004-b: GetByDateAsync – returns only that specific date.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetByDateAsync_SpecificDate_ReturnsOnlyThatDate()
    {
        // Arrange
        var date1 = DateTime.Today.AddDays(-2);
        var date2 = DateTime.Today;

        using var insertCtx = CreateContext();
        using var conn = await insertCtx.GetConnectionAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO activity_events (start_time, duration_ms, category, process_name)
                VALUES (@d1a, 1000, 'app', 'date1_a'),
                       (@d1b, 1000, 'app', 'date1_b'),
                       (@d2a, 1000, 'app', 'date2_a');";
            cmd.Parameters.AddWithValue("@d1a", date1.AddHours(10).ToString("O"));
            cmd.Parameters.AddWithValue("@d1b", date1.AddHours(14).ToString("O"));
            cmd.Parameters.AddWithValue("@d2a", date2.AddHours(11).ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        // Act
        var result = await repo.GetByDateAsync(date1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.StartTime.Date.Should().Be(date1));
    }

    // -----------------------------------------------------------------------
    // GetByDateRangeAsync – returns all events in range.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetByDateRangeAsync_DateRange_ReturnsAllInRange()
    {
        // Arrange
        var d1 = DateTime.Today.AddDays(-3);
        var d2 = DateTime.Today.AddDays(-2);
        var d3 = DateTime.Today.AddDays(-1);

        using var insertCtx = CreateContext();
        using var conn = await insertCtx.GetConnectionAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO activity_events (start_time, duration_ms, category, process_name)
                VALUES (@d1, 1000, 'app', 'd1'),
                       (@d2a, 1000, 'web', 'd2_a'),
                       (@d2b, 1000, 'web', 'd2_b'),
                       (@d3, 1000, 'file', 'd3');";
            cmd.Parameters.AddWithValue("@d1", d1.AddHours(9).ToString("O"));
            cmd.Parameters.AddWithValue("@d2a", d2.AddHours(10).ToString("O"));
            cmd.Parameters.AddWithValue("@d2b", d2.AddHours(15).ToString("O"));
            cmd.Parameters.AddWithValue("@d3", d3.AddHours(12).ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        // Act
        var result = await repo.GetByDateRangeAsync(d2, d3);

        // Assert
        result.Should().HaveCount(3);
        result.Select(e => e.ProcessName).Should().Contain(["d2_a", "d2_b", "d3"]);
    }

    // -----------------------------------------------------------------------
    // UpdateAsync – edits EditedTitle/EditedDesc correctly.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task UpdateAsync_EditedFields_UpdatesCorrectly()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        var ev = new ActivityEvent
        {
            StartTime = DateTime.UtcNow,
            DurationMs = 5000,
            Category = Category.App,
            WorkTag = WorkTag.Unknown,
            ProcessName = "original.exe",
            WindowTitle = "Original Title",
        };
        var inserted = await repo.InsertAsync(ev);

        inserted.EditedTitle = "Edited Title";
        inserted.EditedDesc = "User-added description";

        // Act
        await repo.UpdateAsync(inserted);

        // Assert
        var reloaded = await repo.GetByIdAsync(inserted.Id);
        reloaded.Should().NotBeNull();
        reloaded!.EditedTitle.Should().Be("Edited Title");
        reloaded.EditedDesc.Should().Be("User-added description");
        reloaded.ProcessName.Should().Be("original.exe");
    }

    // -----------------------------------------------------------------------
    // DeleteAsync – removes record.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesRecord()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        var ev = new ActivityEvent
        {
            StartTime = DateTime.UtcNow,
            DurationMs = 1000,
            Category = Category.App,
            ProcessName = "to_delete.exe",
        };
        var inserted = await repo.InsertAsync(ev);

        // Act
        await repo.DeleteAsync(inserted.Id);

        // Assert
        var result = await repo.GetByIdAsync(inserted.Id);
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetByIdAsync – non-existing Id returns null.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        // Act
        var result = await repo.GetByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetDailyStatsAsync – mixed events return correct stats.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetDailyStatsAsync_MixedEvents_ReturnsCorrectStats()
    {
        // Arrange
        var date = DateTime.Today;

        using var insertCtx = CreateContext();
        using var conn = await insertCtx.GetConnectionAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO activity_events (start_time, duration_ms, category, process_name)
                VALUES (@t1, 3600000, 'web',  'chrome.exe'),
                       (@t2, 1800000, 'file', 'code.exe'),
                       (@t3, 900000,  'app',  'notepad.exe'),
                       (@t4, 600000,  'idle', NULL),
                       (@t5, 300000,  'sleep', NULL);";
            cmd.Parameters.AddWithValue("@t1", date.AddHours(9).ToString("O"));
            cmd.Parameters.AddWithValue("@t2", date.AddHours(10).ToString("O"));
            cmd.Parameters.AddWithValue("@t3", date.AddHours(11).ToString("O"));
            cmd.Parameters.AddWithValue("@t4", date.AddHours(12).ToString("O"));
            cmd.Parameters.AddWithValue("@t5", date.AddHours(14).ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        // Act
        var stats = await repo.GetDailyStatsAsync(date);

        // Assert
        stats.TotalActiveMs.Should().Be(6_300_000);
        stats.TotalIdleMs.Should().Be(600_000);
        stats.TotalSleepMs.Should().Be(300_000);
        stats.EventCount.Should().Be(5);
    }

    // -----------------------------------------------------------------------
    // Performance: InsertBatchAsync – 1000 events under 500ms.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertBatchAsync_1000Events_CompletesUnder500ms()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new ActivityEventRepository(ctx);

        var events = Enumerable.Range(1, 1000).Select(i => new ActivityEvent
        {
            StartTime = DateTime.UtcNow.AddSeconds(-i * 10),
            DurationMs = 5000,
            Category = Category.App,
            ProcessName = $"perf_process_{i}.exe",
            WindowTitle = $"Performance Test Window {i}",
        }).ToList();

        // Act
        var sw = Stopwatch.StartNew();
        await repo.InsertBatchAsync(events);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
