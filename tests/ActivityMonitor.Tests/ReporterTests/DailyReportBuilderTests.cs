using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Exporters;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ReporterTests;

public class DailyReportBuilderTests
{
    private readonly DailyReportBuilder _builder = new();

    private static ActivityEvent CreateActiveEvent(
        string processName,
        string workTag,
        long durationMs,
        DateTime startTime,
        string? project = null,
        string? domain = null,
        string? category = null,
        string? windowTitle = null)
    {
        return new ActivityEvent
        {
            StartTime = startTime,
            EndTime = startTime.AddMilliseconds(durationMs),
            DurationMs = durationMs,
            Category = category ?? Category.App,
            WorkTag = workTag,
            ProcessName = processName,
            Domain = domain,
            Project = project,
            WindowTitle = windowTitle,
        };
    }

    private static ActivityEvent CreateIdleEvent(long durationMs, DateTime startTime)
    {
        return new ActivityEvent
        {
            StartTime = startTime,
            EndTime = startTime.AddMilliseconds(durationMs),
            DurationMs = durationMs,
            Category = Category.Idle,
            WorkTag = WorkTag.Unknown,
            ProcessName = "idle",
        };
    }

    private static ActivityEvent CreateSleepEvent(long durationMs, DateTime startTime)
    {
        return new ActivityEvent
        {
            StartTime = startTime,
            EndTime = startTime.AddMilliseconds(durationMs),
            DurationMs = durationMs,
            Category = Category.Sleep,
            WorkTag = WorkTag.Unknown,
            ProcessName = "sleep",
        };
    }

    // ────────────────────────────────────────────
    //  Build – empty / basic
    // ────────────────────────────────────────────

    [Fact]
    public void Build_NoEvents_ReturnsEmptyData()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = Array.Empty<ActivityEvent>();

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.Date.Should().Be(date);
        data.TotalActiveMs.Should().Be(0);
        data.TotalIdleMs.Should().Be(0);
        data.TotalSleepMs.Should().Be(0);
        data.WorkMs.Should().Be(0);
        data.NonWorkMs.Should().Be(0);
        data.WorkRatio.Should().Be(0);
        data.MorningEntries.Should().BeEmpty();
        data.AfternoonEntries.Should().BeEmpty();
        data.AppBreakdown.Should().BeEmpty();
        data.ProjectBreakdown.Should().BeEmpty();
        data.DomainBreakdown.Should().BeEmpty();
        data.UserNotes.Should().BeNull();
    }

    [Fact]
    public void Build_SingleActiveEvent_CalculatesTotalAndTimeline()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9), project: "MyProject"),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.Date.Should().Be(date);
        data.TotalActiveMs.Should().Be(3600000);
        data.TotalIdleMs.Should().Be(0);
        data.TotalSleepMs.Should().Be(0);
        data.WorkMs.Should().Be(3600000);
        data.NonWorkMs.Should().Be(0);
        data.WorkRatio.Should().Be(1.0);

        data.MorningEntries.Should().HaveCount(1);
        data.MorningEntries[0].AppName.Should().Be("code");
        data.MorningEntries[0].ProjectName.Should().Be("MyProject");
        data.MorningEntries[0].IsIdle.Should().BeFalse();
        data.MorningEntries[0].IsSleep.Should().BeFalse();
        data.AfternoonEntries.Should().BeEmpty();
    }

    // ────────────────────────────────────────────
    //  Build – active / idle / sleep separation
    // ────────────────────────────────────────────

    [Fact]
    public void Build_MixedActiveIdleSleep_CalculatesSeparateTotals()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(8)),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(9)),
            CreateActiveEvent("excel.exe", WorkTag.Work, 1200000, date.AddHours(9).AddMinutes(30)),
            CreateIdleEvent(900000, date.AddHours(10)),
            CreateActiveEvent("code.exe", WorkTag.Work, 2400000, date.AddHours(10).AddMinutes(15)),
            CreateSleepEvent(3600000, date.AddHours(11)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.TotalActiveMs.Should().Be(3600000 + 1800000 + 1200000 + 2400000); // 9000000
        data.TotalIdleMs.Should().Be(900000);
        data.TotalSleepMs.Should().Be(3600000);
    }

    // ────────────────────────────────────────────
    //  Build – work / non-work tagging
    // ────────────────────────────────────────────

    [Fact]
    public void Build_WorkAndNonWorkTags_CalculatesCorrectly()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
            CreateActiveEvent("spotify.exe", WorkTag.Break, 900000, date.AddHours(10)),
            CreateActiveEvent("steam.exe", WorkTag.Personal, 1800000, date.AddHours(10).AddMinutes(30)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.TotalActiveMs.Should().Be(3600000 + 900000 + 1800000); // 6300000
        data.WorkMs.Should().Be(3600000);
        data.NonWorkMs.Should().Be(900000 + 1800000); // 2700000
    }

    [Fact]
    public void Build_UnknownWorkTag_NotCountedInWorkOrNonWork()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Unknown, 3600000, date.AddHours(9)),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(10)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.TotalActiveMs.Should().Be(3600000 + 1800000); // 5400000
        data.WorkMs.Should().Be(1800000);
        data.NonWorkMs.Should().Be(0);
    }

    [Fact]
    public void Build_WorkRatio_CalculatesCorrectPercentage()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 7200000, date.AddHours(8)),
            CreateActiveEvent("spotify.exe", WorkTag.Break, 1800000, date.AddHours(10)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.WorkMs.Should().Be(7200000);
        data.NonWorkMs.Should().Be(1800000);
        data.WorkRatio.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void Build_NoWorkAndNoNonWork_WorkRatioIsZero()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("someapp.exe", WorkTag.Unknown, 3600000, date.AddHours(9)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.WorkRatio.Should().Be(0);
    }

    // ────────────────────────────────────────────
    //  Build – morning / afternoon split (noon)
    // ────────────────────────────────────────────

    [Fact]
    public void Build_MorningEvent_GoesToMorningEntries()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries.Should().HaveCount(1);
        data.AfternoonEntries.Should().BeEmpty();
    }

    [Fact]
    public void Build_AfternoonEvent_GoesToAfternoonEntries()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(14)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries.Should().BeEmpty();
        data.AfternoonEntries.Should().HaveCount(1);
    }

    [Fact]
    public void Build_EventAtExactlyNoon_GoesToAfternoonEntries()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(12)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries.Should().BeEmpty();
        data.AfternoonEntries.Should().HaveCount(1);
    }

    [Fact]
    public void Build_EventAtOneMinuteBeforeNoon_GoesToMorningEntries()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(11).AddMinutes(59)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries.Should().HaveCount(1);
        data.AfternoonEntries.Should().BeEmpty();
    }

    [Fact]
    public void Build_MorningAndAfternoonEvents_BothSegmentsPopulated()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(14)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries.Should().HaveCount(1);
        data.AfternoonEntries.Should().HaveCount(1);
        data.MorningEntries[0].AppName.Should().Be("code");
        data.AfternoonEntries[0].AppName.Should().Be("chrome");
    }

    // ────────────────────────────────────────────
    //  Build – breakdowns
    // ────────────────────────────────────────────

    [Fact]
    public void Build_AppBreakdown_AggregatesByProcessName()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
            CreateActiveEvent("code.exe", WorkTag.Work, 1800000, date.AddHours(10)),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1200000, date.AddHours(11)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.AppBreakdown.Should().ContainKey("code.exe").WhoseValue.Should().Be(5400000);
        data.AppBreakdown.Should().ContainKey("chrome.exe").WhoseValue.Should().Be(1200000);
        data.AppBreakdown.Should().HaveCount(2);
    }

    [Fact]
    public void Build_AppBreakdown_ExcludesIdleAndSleep()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
            CreateIdleEvent(900000, date.AddHours(10)),
            CreateSleepEvent(1800000, date.AddHours(11)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.AppBreakdown.Should().HaveCount(1);
        data.AppBreakdown.Should().ContainKey("code.exe");
        data.AppBreakdown.Should().NotContainKey("idle");
        data.AppBreakdown.Should().NotContainKey("sleep");
    }

    [Fact]
    public void Build_ProjectBreakdown_AggregatesByProject()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9), project: "ProjectA"),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(10), project: "ProjectA"),
            CreateActiveEvent("code.exe", WorkTag.Work, 900000, date.AddHours(11), project: "ProjectB"),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.ProjectBreakdown.Should().ContainKey("ProjectA").WhoseValue.Should().Be(5400000);
        data.ProjectBreakdown.Should().ContainKey("ProjectB").WhoseValue.Should().Be(900000);
    }

    [Fact]
    public void Build_DomainBreakdown_AggregatesByDomain()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(9), domain: "github.com"),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1200000, date.AddHours(10), domain: "github.com"),
            CreateActiveEvent("chrome.exe", WorkTag.Break, 600000, date.AddHours(11), domain: "twitter.com"),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.DomainBreakdown.Should().ContainKey("github.com").WhoseValue.Should().Be(3000000);
        data.DomainBreakdown.Should().ContainKey("twitter.com").WhoseValue.Should().Be(600000);
    }

    [Fact]
    public void Build_DomainBreakdown_ExcludesNullDomain()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9), domain: null),
            CreateActiveEvent("chrome.exe", WorkTag.Work, 1800000, date.AddHours(10), domain: "github.com"),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.DomainBreakdown.Should().HaveCount(1);
        data.DomainBreakdown.Should().ContainKey("github.com");
    }

    // ────────────────────────────────────────────
    //  Build – user notes from summary
    // ────────────────────────────────────────────

    [Fact]
    public void Build_WithSummary_SetsUserNotes()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
        };
        var summary = new DailySummary
        {
            Date = date.ToString("yyyy-MM-dd"),
            UserNotes = "今天修复了3个生产环境bug",
        };

        // Act
        var data = _builder.Build(date, events, summary);

        // Assert
        data.UserNotes.Should().Be("今天修复了3个生产环境bug");
    }

    [Fact]
    public void Build_WithNullSummary_SetsNullUserNotes()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9)),
        };

        // Act
        var data = _builder.Build(date, events, summary: null);

        // Assert
        data.UserNotes.Should().BeNull();
    }

    // ────────────────────────────────────────────
    //  Build – timeline entry properties
    // ────────────────────────────────────────────

    [Fact]
    public void Build_TimelineEntry_HasCorrectTimeFormatting()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateActiveEvent("code.exe", WorkTag.Work, 3600000, date.AddHours(9).AddMinutes(5)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        var entry = data.MorningEntries[0];
        entry.StartTimeFormatted.Should().Be("09:05");
        entry.EndTimeFormatted.Should().Be("10:05");
    }

    [Fact]
    public void Build_IdleTimelineEntry_HasIsIdleFlag()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateIdleEvent(900000, date.AddHours(12)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.AfternoonEntries.Should().HaveCount(1);
        data.AfternoonEntries[0].IsIdle.Should().BeTrue();
        data.AfternoonEntries[0].AppName.Should().Be("空闲");
    }

    [Fact]
    public void Build_SleepTimelineEntry_HasIsSleepFlag()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            CreateSleepEvent(3600000, date.AddHours(23)),
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.AfternoonEntries.Should().HaveCount(1);
        data.AfternoonEntries[0].IsSleep.Should().BeTrue();
        data.AfternoonEntries[0].AppName.Should().Be("睡眠/锁屏");
    }

    [Fact]
    public void Build_EditedTitle_UsedForWindowTitleInEntry()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            new ActivityEvent
            {
                StartTime = date.AddHours(9),
                EndTime = date.AddHours(10),
                DurationMs = 3600000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
                WindowTitle = "original title",
                EditedTitle = "user-edited title",
            },
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries[0].WindowTitle.Should().Be("user-edited title");
        data.MorningEntries[0].Detail.Should().BeNull();
    }

    [Fact]
    public void Build_EditedDesc_UsedForDetailInEntry()
    {
        // Arrange
        var date = new DateTime(2026, 7, 21);
        var events = new List<ActivityEvent>
        {
            new ActivityEvent
            {
                StartTime = date.AddHours(9),
                EndTime = date.AddHours(10),
                DurationMs = 3600000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                ProcessName = "code.exe",
                Detail = "original detail",
                EditedDesc = "user description",
            },
        };

        // Act
        var data = _builder.Build(date, events);

        // Assert
        data.MorningEntries[0].Detail.Should().Be("user description");
    }

    // ────────────────────────────────────────────
    //  SerializeBreakdown / DeserializeBreakdown
    // ────────────────────────────────────────────

    [Fact]
    public void SerializeBreakdown_ValidDict_ReturnsJson()
    {
        // Arrange
        var dict = new Dictionary<string, long>
        {
            ["code.exe"] = 3600000,
            ["chrome.exe"] = 1800000,
        };

        // Act
        var json = DailyReportBuilder.SerializeBreakdown(dict);

        // Assert
        json.Should().NotBeNull();
        json.Should().Contain("code.exe");
        json.Should().Contain("3600000");
        json.Should().Contain("chrome.exe");
        json.Should().Contain("1800000");
    }

    [Fact]
    public void SerializeBreakdown_EmptyDict_ReturnsNull()
    {
        // Arrange
        var dict = new Dictionary<string, long>();

        // Act
        var json = DailyReportBuilder.SerializeBreakdown(dict);

        // Assert
        json.Should().BeNull();
    }

    [Fact]
    public void SerializeBreakdown_Null_ReturnsNull()
    {
        // Act
        var json = DailyReportBuilder.SerializeBreakdown(null);

        // Assert
        json.Should().BeNull();
    }

    [Fact]
    public void DeserializeBreakdown_ValidJson_ReturnsDict()
    {
        // Arrange
        var json = @"{""code.exe"":3600000,""chrome.exe"":1800000}";

        // Act
        var dict = DailyReportBuilder.DeserializeBreakdown(json);

        // Assert
        dict.Should().HaveCount(2);
        dict["code.exe"].Should().Be(3600000);
        dict["chrome.exe"].Should().Be(1800000);
    }

    [Fact]
    public void DeserializeBreakdown_EmptyString_ReturnsEmptyDict()
    {
        // Act
        var dict = DailyReportBuilder.DeserializeBreakdown("");

        // Assert
        dict.Should().NotBeNull();
        dict.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeBreakdown_Null_ReturnsEmptyDict()
    {
        // Act
        var dict = DailyReportBuilder.DeserializeBreakdown(null);

        // Assert
        dict.Should().NotBeNull();
        dict.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeBreakdown_InvalidJson_ReturnsEmptyDict()
    {
        // Act
        var dict = DailyReportBuilder.DeserializeBreakdown("{invalid json");

        // Assert
        dict.Should().NotBeNull();
        dict.Should().BeEmpty();
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new Dictionary<string, long>
        {
            ["code.exe"] = 7200000,
            ["chrome.exe"] = 3600000,
            ["excel.exe"] = 1800000,
            ["notepad.exe"] = 900000,
        };

        // Act
        var json = DailyReportBuilder.SerializeBreakdown(original);
        var restored = DailyReportBuilder.DeserializeBreakdown(json);

        // Assert
        restored.Should().HaveCount(4);
        foreach (var kvp in original)
        {
            restored.Should().ContainKey(kvp.Key).WhoseValue.Should().Be(kvp.Value);
        }
    }
}
