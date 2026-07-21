using ActivityMonitor.Core.Classification;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="TodayStatsService"/> using NSubstitute to mock <see cref="IActivityRepository"/>.
/// Verifies multi-dimensional aggregation returns correct durations and percentages.
/// </summary>
public class TodayStatsServiceTests
{
    // ──────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRepo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TodayStatsService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    // ──────────────────────────────────────────────
    // GetByAppAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByAppAsync_MultipleProcesses_ReturnsCorrectDurations()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "code.exe", DurationMs = 3_600_000 },
            new() { ProcessName = "chrome.exe", DurationMs = 1_800_000 },
            new() { ProcessName = "photoshop.exe", DurationMs = 1_200_000 },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByAppAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeInDescendingOrder(s => s.DurationMs);

        var codeItem = results.Should().ContainSingle(s => s.Name == "code.exe").Subject;
        codeItem.DurationMs.Should().Be(3_600_000);
        codeItem.Percentage.Should().Be(54.5);

        var chromeItem = results.Should().ContainSingle(s => s.Name == "chrome.exe").Subject;
        chromeItem.DurationMs.Should().Be(1_800_000);
        chromeItem.Percentage.Should().Be(27.3);

        var psItem = results.Should().ContainSingle(s => s.Name == "photoshop.exe").Subject;
        psItem.DurationMs.Should().Be(1_200_000);
        psItem.Percentage.Should().Be(18.2);

        results.Sum(s => s.Percentage).Should().BeApproximately(100.0, 0.1);

        // Verify repo was called exactly once (caching)
        await repo.Received(1).GetTodayEventsAsync();
    }

    // ──────────────────────────────────────────────
    // GetByProjectAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByProjectAsync_MultipleProjects_ReturnsCorrectDurations()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "code.exe", DurationMs = 5_400_000, Project = "projectA" },
            new() { ProcessName = "code.exe", DurationMs = 1_200_000, Project = "projectB" },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByProjectAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().BeInDescendingOrder(s => s.DurationMs);

        var projectA = results.Should().ContainSingle(s => s.Name == "projectA").Subject;
        projectA.DurationMs.Should().Be(5_400_000);
        projectA.Percentage.Should().Be(81.8);

        var projectB = results.Should().ContainSingle(s => s.Name == "projectB").Subject;
        projectB.DurationMs.Should().Be(1_200_000);
        projectB.Percentage.Should().Be(18.2);

        results.Sum(s => s.Percentage).Should().BeApproximately(100.0, 0.1);
    }

    // ──────────────────────────────────────────────
    // GetByDomainAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByDomainAsync_MultipleDomains_ReturnsCorrectDurations()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "chrome.exe", DurationMs = 2_400_000, Domain = "github.com", Category = Category.Web },
            new() { ProcessName = "chrome.exe", DurationMs = 1_200_000, Domain = "stackoverflow.com", Category = Category.Web },
            new() { ProcessName = "code.exe", DurationMs = 600_000, Domain = null, Category = Category.File },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByDomainAsync();

        // Assert — only web events with domains are included
        results.Should().HaveCount(2);
        results.Should().BeInDescendingOrder(s => s.DurationMs);

        var github = results.Should().ContainSingle(s => s.Name == "github.com").Subject;
        github.DurationMs.Should().Be(2_400_000);
        github.Percentage.Should().Be(66.7);

        var so = results.Should().ContainSingle(s => s.Name == "stackoverflow.com").Subject;
        so.DurationMs.Should().Be(1_200_000);
        so.Percentage.Should().Be(33.3);

        results.Sum(s => s.Percentage).Should().BeApproximately(100.0, 0.1);
    }

    // ──────────────────────────────────────────────
    // GetByWorkTagAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByWorkTagAsync_MultipleTags_ReturnsCorrectDurations()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "code.exe", DurationMs = 5_400_000, WorkTag = WorkTag.Work },
            new() { ProcessName = "chrome.exe", DurationMs = 600_000, WorkTag = WorkTag.Break },
            new() { ProcessName = "steam.exe", DurationMs = 600_000, WorkTag = WorkTag.Personal },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByWorkTagAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeInDescendingOrder(s => s.DurationMs);

        var workItem = results.Should().ContainSingle(s => s.Name == WorkTag.Work).Subject;
        workItem.DurationMs.Should().Be(5_400_000);
        workItem.Percentage.Should().Be(81.8);

        var breakItem = results.Should().ContainSingle(s => s.Name == WorkTag.Break).Subject;
        breakItem.DurationMs.Should().Be(600_000);

        var personalItem = results.Should().ContainSingle(s => s.Name == WorkTag.Personal).Subject;
        personalItem.DurationMs.Should().Be(600_000);

        results.Sum(s => s.Percentage).Should().BeApproximately(100.0, 0.1);
    }

    // ──────────────────────────────────────────────
    // GetByCategoryAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByCategoryAsync_MultipleCategories_ReturnsCorrectDurations()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "chrome.exe", DurationMs = 3_000_000, Category = Category.Web },
            new() { ProcessName = "code.exe", DurationMs = 2_000_000, Category = Category.File },
            new() { ProcessName = "photoshop.exe", DurationMs = 1_600_000, Category = Category.App },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByCategoryAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeInDescendingOrder(s => s.DurationMs);

        var web = results.Should().ContainSingle(s => s.Name == Category.Web).Subject;
        web.DurationMs.Should().Be(3_000_000);
        web.Percentage.Should().Be(45.5);

        var file = results.Should().ContainSingle(s => s.Name == Category.File).Subject;
        file.DurationMs.Should().Be(2_000_000);
        file.Percentage.Should().Be(30.3);

        var app = results.Should().ContainSingle(s => s.Name == Category.App).Subject;
        app.DurationMs.Should().Be(1_600_000);
        app.Percentage.Should().Be(24.2);

        results.Sum(s => s.Percentage).Should().BeApproximately(100.0, 0.1);
    }

    // ──────────────────────────────────────────────
    // GetOverviewAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetOverviewAsync_MixedData_ReturnsCorrectOverview()
    {
        // Arrange
        var events = new List<ActivityEvent>
        {
            new() { ProcessName = "chrome.exe", DurationMs = 2_000_000, Category = Category.Web, WorkTag = WorkTag.Work },
            new() { ProcessName = "code.exe", DurationMs = 1_500_000, Category = Category.File, WorkTag = WorkTag.Work },
            new() { ProcessName = "steam.exe", DurationMs = 1_000_000, Category = Category.App, WorkTag = WorkTag.Personal },
            new() { ProcessName = null, DurationMs = 500_000, Category = Category.Idle, WorkTag = WorkTag.Unknown },
            new() { ProcessName = null, DurationMs = 300_000, Category = Category.Sleep, WorkTag = WorkTag.Unknown },
        };
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(events);
        var service = new TodayStatsService(repo);

        // Act
        var overview = await service.GetOverviewAsync();

        // Assert
        overview.Should().NotBeNull();
        overview.EventCount.Should().Be(5);

        // Active = sum of web + file + app (non-idle, non-sleep)
        overview.TotalActiveMs.Should().Be(2_000_000 + 1_500_000 + 1_000_000);
        overview.TotalIdleMs.Should().Be(500_000);
        overview.TotalSleepMs.Should().Be(300_000);

        // WorkMs = web(work) + file(work)
        overview.WorkMs.Should().Be(2_000_000 + 1_500_000);

        // NonWorkMs = app(personal) only; idle/sleep with "unknown" tag are not counted
        overview.NonWorkMs.Should().Be(1_000_000);
    }

    // ──────────────────────────────────────────────
    // GetByAppAsync — empty
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByAppAsync_NoEvents_ReturnsEmptyList()
    {
        // Arrange
        var repo = Substitute.For<IActivityRepository>();
        repo.GetTodayEventsAsync().Returns(new List<ActivityEvent>());
        var service = new TodayStatsService(repo);

        // Act
        var results = await service.GetByAppAsync();

        // Assert
        results.Should().BeEmpty();
    }
}
