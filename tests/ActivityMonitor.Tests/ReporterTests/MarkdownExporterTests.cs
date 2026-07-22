using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using ActivityMonitor.TrayApp.Exporters;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ReporterTests;

public class MarkdownExporterTests
{
    private static string TempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"md_export_{Guid.NewGuid():N}.db");

    private static async Task InitSchemaAsync(SqliteContext ctx)
    {
        await ctx.GetConnectionAsync();
    }

    private static ActivityEvent MakeEvent(
        string processName, string category, string? domain,
        string? project, string workTag, long durationMs, DateTime start)
    {
        return new ActivityEvent
        {
            StartTime = start,
            EndTime = start.AddMilliseconds(durationMs),
            DurationMs = durationMs,
            Category = category,
            WorkTag = workTag,
            ProcessName = processName,
            Domain = domain,
            Project = project,
            WindowTitle = $"{processName} - testing",
        };
    }

    [Fact]
    public async Task ExportDailyAsync_NoEvents_ReturnsMarkdownWithAllSections()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var exporter = new MarkdownExporter(
                new ActivityEventRepository(ctx),
                new DailySummaryRepository(ctx));

            var markdown = await exporter.ExportDailyAsync(new DateTime(2026, 7, 21));

            markdown.Should().Contain("# 工作日报");
            markdown.Should().Contain("2026-07-21");
            markdown.Should().Contain("📊 今日概览");
            markdown.Should().Contain("⏱ 时间线");
            markdown.Should().Contain("（无记录）");
            markdown.Should().Contain("📁 项目分布");
            markdown.Should().Contain("📈 应用分布");
            markdown.Should().Contain("🌐 网页分类");
            markdown.Should().Contain("📝 手动补充");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_OneActiveEvent_ContainsAppNameAndTime()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);
            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("code");
            markdown.Should().Contain("09:00");
            markdown.Should().Contain("10:00");
            markdown.Should().Contain("MyProject");
            markdown.Should().NotContain("（无记录）");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_MultipleEvents_ContainsAllDistributionSections()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "ProjectA", WorkTag.Work,
                3600000, date.AddHours(9)));
            await eventRepo.InsertAsync(MakeEvent(
                "chrome.exe", Category.Web, "github.com", "ProjectA", WorkTag.Work,
                1800000, date.AddHours(10)));
            await eventRepo.InsertAsync(MakeEvent(
                "excel.exe", Category.App, null, "Reporting", WorkTag.Work,
                900000, date.AddHours(10).AddMinutes(30)));
            await eventRepo.InsertAsync(MakeEvent(
                "chrome.exe", Category.Web, "stackoverflow.com", null, WorkTag.Work,
                600000, date.AddHours(11)));

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("ProjectA");
            markdown.Should().Contain("Reporting");
            markdown.Should().Contain("github.com");
            markdown.Should().Contain("stackoverflow.com");
            markdown.Should().Contain("📁 项目分布");
            markdown.Should().Contain("📈 应用分布");
            markdown.Should().Contain("🌐 网页分类");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_IdleAndSleepEvents_AreLabeledInTimeline()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(9), EndTime = date.AddHours(10),
                DurationMs = 3600000, Category = Category.App, WorkTag = WorkTag.Work,
                ProcessName = "code.exe", WindowTitle = "code.exe - working",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(12), EndTime = date.AddHours(12).AddMinutes(15),
                DurationMs = 900000, Category = Category.Idle, WorkTag = WorkTag.Unknown,
                ProcessName = "idle", WindowTitle = "idle",
            });
            await eventRepo.InsertAsync(new ActivityEvent
            {
                StartTime = date.AddHours(14), EndTime = date.AddHours(14).AddMinutes(30),
                DurationMs = 1800000, Category = Category.Sleep, WorkTag = WorkTag.Unknown,
                ProcessName = "sleep", WindowTitle = "sleep",
            });

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("空闲");
            markdown.Should().Contain("睡眠/锁屏");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_WithUserNotes_RendersNotesInSection6()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var summaryRepo = new DailySummaryRepository(ctx);
            var exporter = new MarkdownExporter(eventRepo, summaryRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            await summaryRepo.UpsertAsync(new DailySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                TotalActiveMs = 3600000,
                UserNotes = "今天完成了登录模块的开发\n处理了3个bug",
            });

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("登录模块");
            markdown.Should().Contain("3个bug");
            markdown.Should().NotContain("（暂无补充内容）");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyToFileAsync_ValidTempPath_WritesFileWithContent()
    {
        var db = TempDbPath();
        var dir = Path.Combine(Path.GetTempPath(), $"md_file_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "daily-report.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);
            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            var result = await exporter.ExportDailyToFileAsync(date, file);

            result.Should().Be(file);
            File.Exists(file).Should().BeTrue();
            (await File.ReadAllTextAsync(file)).Should().Contain("# 工作日报");
        }
        finally { TryDelete(file); TryDeleteDir(dir); TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyToFileAsync_NoEvents_WritesEmptyReport()
    {
        var db = TempDbPath();
        var dir = Path.Combine(Path.GetTempPath(), $"md_file_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "empty.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var exporter = new MarkdownExporter(
                new ActivityEventRepository(ctx), new DailySummaryRepository(ctx));

            var result = await exporter.ExportDailyToFileAsync(
                new DateTime(2026, 7, 21), file);

            result.Should().Be(file);
            File.Exists(file).Should().BeTrue();
            (await File.ReadAllTextAsync(file)).Should().Contain("（无记录）");
        }
        finally { TryDelete(file); TryDeleteDir(dir); TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyToFileAsync_NonExistentDir_CreatesDirAndWrites()
    {
        var db = TempDbPath();
        var baseDir = Path.Combine(Path.GetTempPath(), $"md_file_{Guid.NewGuid():N}");
        var file = Path.Combine(baseDir, "a", "b", "report.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var exporter = new MarkdownExporter(
                new ActivityEventRepository(ctx), new DailySummaryRepository(ctx));

            var result = await exporter.ExportDailyToFileAsync(
                new DateTime(2026, 7, 21), file);

            result.Should().Be(file);
            File.Exists(file).Should().BeTrue();
        }
        finally { TryDelete(file); TryDeleteDir(baseDir); TryDelete(db); }
    }

    // ────────────────────────────────────────────
    //  Operation logs – embedded in section 7 (W1-M3)
    // ────────────────────────────────────────────

    [Fact]
    public async Task ExportDailyAsync_WithOperationLogs_ContainsSection7()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var logRepo = new OperationLogRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx),
                operationLogRepo: logRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            await logRepo.InsertAsync(new OperationLog
            {
                Timestamp = date.AddHours(9),
                ProcessName = "code.exe",
                WindowTitle = "code.exe - Program.cs",
                Category = Category.App,
            });

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("📋 操作日志");
            markdown.Should().Contain("09:00");
            markdown.Should().Contain("code");
            markdown.Should().Contain("code.exe - Program.cs");
            markdown.Should().NotContain("（无操作日志记录）");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_WithoutOperationLogs_ShowsEmptySection7()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("📋 操作日志");
            markdown.Should().Contain("（无操作日志记录）");
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public async Task ExportDailyAsync_WithOperationLogCategory_RendersCategoryBadge()
    {
        var db = TempDbPath();
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var logRepo = new OperationLogRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx),
                operationLogRepo: logRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            await logRepo.InsertAsync(new OperationLog
            {
                Timestamp = date.AddHours(9).AddMinutes(30),
                ProcessName = "chrome.exe",
                WindowTitle = "GitHub - Pull Requests",
                Category = Category.Web,
                Detail = "https://github.com/pulls",
            });

            var markdown = await exporter.ExportDailyAsync(date);

            markdown.Should().Contain("`web`");
            markdown.Should().Contain("chrome");
            markdown.Should().Contain("https://github.com/pulls");
        }
        finally { TryDelete(db); }
    }

    // ────────────────────────────────────────────
    //  Operation logs – companion file export (W1-M3)
    // ────────────────────────────────────────────

    [Fact]
    public async Task ExportOperationLogsFileAsync_WithLogs_WritesCompanionFile()
    {
        var db = TempDbPath();
        var dir = Path.Combine(Path.GetTempPath(), $"md_logs_{Guid.NewGuid():N}");
        var mainFile = Path.Combine(dir, "daily-report.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var logRepo = new OperationLogRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx),
                operationLogRepo: logRepo);

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            await logRepo.InsertAsync(new OperationLog
            {
                Timestamp = date.AddHours(9),
                ProcessName = "code.exe",
                WindowTitle = "Program.cs",
                Category = Category.App,
            });

            var companionPath = await exporter.ExportOperationLogsFileAsync(date, mainFile);

            File.Exists(companionPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(companionPath);
            content.Should().Contain("# 操作日志");
            content.Should().Contain("2026-07-21");
            content.Should().Contain("code.exe");
            content.Should().Contain("| 时间 | 进程 |");
            content.Should().Contain("1 条操作记录");
        }
        finally { TryDelete(mainFile); TryDeleteDir(dir); TryDelete(db); }
    }

    [Fact]
    public async Task ExportOperationLogsFileAsync_WithoutLogRepo_WritesEmptyFile()
    {
        var db = TempDbPath();
        var dir = Path.Combine(Path.GetTempPath(), $"md_logs_{Guid.NewGuid():N}");
        var mainFile = Path.Combine(dir, "daily-report.md");
        try
        {
            using var ctx = new SqliteContext(db);
            await InitSchemaAsync(ctx);
            var eventRepo = new ActivityEventRepository(ctx);
            var exporter = new MarkdownExporter(
                eventRepo, new DailySummaryRepository(ctx));

            var date = new DateTime(2026, 7, 21);

            await eventRepo.InsertAsync(MakeEvent(
                "code.exe", Category.App, null, "MyProject", WorkTag.Work,
                3600000, date.AddHours(9)));

            var companionPath = await exporter.ExportOperationLogsFileAsync(date, mainFile);

            File.Exists(companionPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(companionPath);
            content.Should().Contain("0 条操作记录");
        }
        finally { TryDelete(mainFile); TryDeleteDir(dir); TryDelete(db); }
    }

    private static void TryDelete(string p) { try { File.Delete(p); } catch { } }
    private static void TryDeleteDir(string p) { try { Directory.Delete(p, true); } catch { } }
}
