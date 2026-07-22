using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

/// <summary>
/// OperationLogRepository tests — W1-M3 窗口切换日志数据层。
/// 使用基于文件的 SQLite 实现隔离，每个测试使用独立的临时数据库。
/// </summary>
public class OperationLogRepositoryTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"OpLog_{Guid.NewGuid():N}.db");

    private static void TryDel(string p) { try { File.Delete(p); } catch { } }

    // -----------------------------------------------------------------------
    // InsertAsync — 插入单条操作日志并返回自增 ID
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertAsync_ValidLog_ReturnsLogWithPositiveId()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var log = new OperationLog
            {
                Timestamp = new DateTime(2026, 7, 22, 9, 0, 0),
                WindowTitle = "GitHub - Google Chrome",
                ProcessName = "chrome.exe",
                ProcessId = 1234,
                ProcessPath = @"C:\Program Files\Google\Chrome\chrome.exe",
                Category = "web",
                Detail = "github.com",
            };

            var result = await repo.InsertAsync(log);

            result.Id.Should().BeGreaterThan(0);
            result.Timestamp.Should().Be(log.Timestamp);
            result.WindowTitle.Should().Be("GitHub - Google Chrome");
            result.ProcessName.Should().Be("chrome.exe");
            result.ProcessId.Should().Be(1234);
            result.ProcessPath.Should().Be(@"C:\Program Files\Google\Chrome\chrome.exe");
            result.Category.Should().Be("web");
            result.Detail.Should().Be("github.com");
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // InsertBatchAsync — 批量插入 20 条记录
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertBatchAsync_20Logs_AllSavedSuccessfully()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var logs = Enumerable.Range(1, 20).Select(i => new OperationLog
            {
                Timestamp = new DateTime(2026, 7, 22, 9, i, 0),
                WindowTitle = $"Window {i}",
                ProcessName = i % 2 == 0 ? "chrome.exe" : "code.exe",
                Category = i % 2 == 0 ? "web" : "file",
            }).ToList();

            await repo.InsertBatchAsync(logs);

            // Verify count via raw query
            using var conn = await ctx.GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM operation_logs;";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            count.Should().Be(20);
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // InsertBatchAsync — 空列表不报错
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InsertBatchAsync_EmptyList_DoesNotThrow()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            await repo.Invoking(r => r.InsertBatchAsync(new List<OperationLog>()))
                .Should().NotThrowAsync();
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // GetOperationLogsAsync — 按日期查询
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetOperationLogsAsync_ByDate_ReturnsOnlyThatDate()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var date = new DateTime(2026, 7, 22);
            var prevDay = date.AddDays(-1);

            await repo.InsertAsync(new OperationLog
            {
                Timestamp = date.AddHours(10), WindowTitle = "Today", ProcessName = "today.exe",
            });
            await repo.InsertAsync(new OperationLog
            {
                Timestamp = prevDay.AddHours(10), WindowTitle = "Yesterday", ProcessName = "yesterday.exe",
            });

            var result = await repo.GetOperationLogsAsync(date);

            result.Should().HaveCount(1);
            result[0].WindowTitle.Should().Be("Today");
            result[0].Timestamp.Date.Should().Be(date);
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // GetOperationLogsAsync — 按时间升序排列
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetOperationLogsAsync_MultipleLogs_OrderedByTimestampAsc()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var date = new DateTime(2026, 7, 22);
            for (int i = 0; i < 5; i++)
            {
                await repo.InsertAsync(new OperationLog
                {
                    Timestamp = date.AddHours(9).AddMinutes(i * 10),
                    WindowTitle = $"Log {i}",
                    ProcessName = "test.exe",
                });
            }

            var result = await repo.GetOperationLogsAsync(date);

            result.Should().HaveCount(5);
            result[0].WindowTitle.Should().Be("Log 0");
            result[4].WindowTitle.Should().Be("Log 4");
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // GetOperationLogsAsync — 无记录返回空列表
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetOperationLogsAsync_EmptyDay_ReturnsEmptyList()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var result = await repo.GetOperationLogsAsync(new DateTime(2026, 1, 1));

            result.Should().BeEmpty();
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // UpdateAsync — 更新标题和详情
    // -----------------------------------------------------------------------
    [Fact]
    public async Task UpdateAsync_ValidUpdate_ReturnsTrueAndUpdatesFields()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var inserted = await repo.InsertAsync(new OperationLog
            {
                Timestamp = new DateTime(2026, 7, 22, 10, 0, 0),
                WindowTitle = "Original Title",
                ProcessName = "app.exe",
                Category = "app",
            });

            inserted.WindowTitle = "Updated Title";
            inserted.Detail = "User note";

            var result = await repo.UpdateAsync(inserted);

            result.Should().BeTrue();

            var reloaded = await repo.GetOperationLogsAsync(new DateTime(2026, 7, 22));
            reloaded.Should().ContainSingle();
            reloaded[0].WindowTitle.Should().Be("Updated Title");
            reloaded[0].Detail.Should().Be("User note");
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // UpdateAsync — 不存在的 ID 返回 false
    // -----------------------------------------------------------------------
    [Fact]
    public async Task UpdateAsync_NonExistingId_ReturnsFalse()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var result = await repo.UpdateAsync(new OperationLog
            {
                Id = 99999,
                WindowTitle = "Ghost",
            });

            result.Should().BeFalse();
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // DeleteAsync — 删除存在的记录
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesRecord()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var inserted = await repo.InsertAsync(new OperationLog
            {
                Timestamp = new DateTime(2026, 7, 22, 11, 0, 0),
                WindowTitle = "ToDelete",
                ProcessName = "del.exe",
            });

            var result = await repo.DeleteAsync(inserted.Id);

            result.Should().BeTrue();

            var remaining = await repo.GetOperationLogsAsync(new DateTime(2026, 7, 22));
            remaining.Should().BeEmpty();
        }
        finally { TryDel(db); }
    }

    // -----------------------------------------------------------------------
    // DeleteAsync — 不存在的 ID 返回 false
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
        var db = TempDb();
        try
        {
            using var ctx = new SqliteContext(db);
            var repo = new OperationLogRepository(ctx);

            var result = await repo.DeleteAsync(99999);

            result.Should().BeFalse();
        }
        finally { TryDel(db); }
    }
}
