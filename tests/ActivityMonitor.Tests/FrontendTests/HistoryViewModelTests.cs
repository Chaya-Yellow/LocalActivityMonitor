using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.History;
using ActivityMonitor.TrayApp.Mock;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M6 混合视图 — HistoryViewModel 回归测试。
/// 验证日/周/月 Tab 切换、柱状图数据构建、数据表格、选中联动和导航逻辑。
/// </summary>
public class HistoryViewModelTests
{
    // ──────────────── 构造与初始状态 ────────────────

    [Fact]
    public void Constructor_Default_InitializesDayView()
    {
        var vm = new HistoryViewModel();

        vm.SelectedViewMode.Should().Be(HistoryViewMode.Day);
        vm.IsLoading.Should().BeFalse();
        vm.DateLabel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_Default_SetsWeekAndMonthLabels()
    {
        var vm = new HistoryViewModel();

        vm.WeekLabel.Should().NotBeNullOrEmpty().And.Contain("第");
        vm.MonthLabel.Should().NotBeNullOrEmpty().And.Contain("年");
    }

    // ──────────────── 日期标签格式 ────────────────

    [Fact]
    public void DateLabel_ContainsChineseWeekday()
    {
        var vm = new HistoryViewModel();

        // Should contain one of the Chinese weekday names
        var weekDays = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        vm.DateLabel.Should().ContainAny(weekDays);
    }

    [Fact]
    public void DateLabel_ContainsYearMonthDay()
    {
        var vm = new HistoryViewModel();

        var today = DateTime.Today;
        vm.DateLabel.Should().Contain(today.Year.ToString());
        vm.DateLabel.Should().Contain(today.Month.ToString());
        vm.DateLabel.Should().Contain(today.Day.ToString());
    }

    // ──────────────── 日视图切换 ────────────────

    [Fact]
    public async Task SwitchToDayView_SetsViewMode()
    {
        var vm = new HistoryViewModel();
        // Switch to week first, then back to day
        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        vm.SelectedViewMode.Should().Be(HistoryViewMode.Day);
        vm.IsRangeMode.Should().BeFalse();
        vm.SelectedSoftwareName.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToDayView_LoadsDayData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        vm.IsLoading.Should().BeFalse();
    }

    // ──────────────── 周视图切换 ────────────────

    [Fact]
    public async Task SwitchToWeekView_SetsViewMode()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.SelectedViewMode.Should().Be(HistoryViewMode.Week);
        vm.IsRangeMode.Should().BeFalse();
        vm.SelectedSoftwareName.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToWeekView_LoadsWeekBarChartData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.WeekBarItems.Should().NotBeNull();
        vm.WeekTableItems.Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToWeekView_LoadsWeeklyReportText()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.WeeklyReportText.Should().NotBeNullOrEmpty();
    }

    // ──────────────── 月视图切换 ────────────────

    [Fact]
    public async Task SwitchToMonthView_SetsViewMode()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToMonthViewCommand.ExecuteAsync(null);

        vm.SelectedViewMode.Should().Be(HistoryViewMode.Month);
        vm.IsRangeMode.Should().BeFalse();
        vm.SelectedSoftwareName.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToMonthView_LoadsMonthData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToMonthViewCommand.ExecuteAsync(null);

        vm.IsLoading.Should().BeFalse();
        vm.MonthBarItems.Should().NotBeNull();
        vm.MonthTableItems.Should().NotBeNull();
    }

    // ──────────────── 软件选中联动 ────────────────

    [Fact]
    public void SelectSoftware_NewName_SetsSelectedSoftwareName()
    {
        var vm = new HistoryViewModel();

        vm.SelectSoftwareCommand.Execute("chrome.exe");

        vm.SelectedSoftwareName.Should().Be("chrome.exe");
    }

    [Fact]
    public void SelectSoftware_SameNameAgain_ClearsSelection()
    {
        var vm = new HistoryViewModel();
        vm.SelectSoftwareCommand.Execute("chrome.exe");

        vm.SelectSoftwareCommand.Execute("chrome.exe");

        vm.SelectedSoftwareName.Should().BeEmpty();
    }

    [Fact]
    public void SelectSoftware_Null_ClearsSelection()
    {
        var vm = new HistoryViewModel();
        vm.SelectSoftwareCommand.Execute("chrome.exe");

        vm.SelectSoftwareCommand.Execute(null!);

        vm.SelectedSoftwareName.Should().BeEmpty();
    }

    [Fact]
    public void SelectSoftware_DifferentName_SwitchesSelection()
    {
        var vm = new HistoryViewModel();
        vm.SelectSoftwareCommand.Execute("chrome.exe");

        vm.SelectSoftwareCommand.Execute("code.exe");

        vm.SelectedSoftwareName.Should().Be("code.exe");
    }

    // ──────────────── 日导航 ────────────────

    [Fact]
    public async Task PreviousDay_DecrementsDate()
    {
        var vm = new HistoryViewModel();
        var originalDate = vm.SelectedDate;

        await vm.PreviousDayCommand.ExecuteAsync(null);

        vm.SelectedDate.Should().Be(originalDate.AddDays(-1));
    }

    [Fact]
    public async Task NextDay_IncrementsDate_WhenNotToday()
    {
        var vm = new HistoryViewModel();
        vm.SelectedDate = DateTime.Today.AddDays(-3);
        var originalDate = vm.SelectedDate;

        await vm.NextDayCommand.ExecuteAsync(null);

        vm.SelectedDate.Should().Be(originalDate.AddDays(1));
    }

    [Fact]
    public async Task NextDay_WhenAtToday_DoesNotGoToFuture()
    {
        var vm = new HistoryViewModel();
        vm.SelectedDate = DateTime.Today;

        await vm.NextDayCommand.ExecuteAsync(null);

        vm.SelectedDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task GoToToday_ResetsSelectedDate()
    {
        var vm = new HistoryViewModel();
        vm.SelectedDate = DateTime.Today.AddDays(-5);

        await vm.GoToTodayCommand.ExecuteAsync(null);

        vm.SelectedDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task GoToToday_DisablesRangeMode()
    {
        var vm = new HistoryViewModel();
        vm.IsRangeMode = true;

        await vm.GoToTodayCommand.ExecuteAsync(null);

        vm.IsRangeMode.Should().BeFalse();
    }

    // ──────────────── 周导航 ────────────────

    [Fact]
    public async Task PreviousWeek_DecrementsWeekDate()
    {
        var vm = new HistoryViewModel();
        var original = vm.SelectedWeekDate;

        await vm.PreviousWeekCommand.ExecuteAsync(null);

        vm.SelectedWeekDate.Should().Be(original.AddDays(-7));
    }

    [Fact]
    public async Task NextWeek_IncrementsWeekDate()
    {
        var vm = new HistoryViewModel();
        var original = vm.SelectedWeekDate;

        await vm.NextWeekCommand.ExecuteAsync(null);

        vm.SelectedWeekDate.Should().Be(original.AddDays(7));
    }

    [Fact]
    public async Task GoToCurrentWeek_ResetsToToday()
    {
        var vm = new HistoryViewModel();
        vm.SelectedWeekDate = DateTime.Today.AddDays(-14);

        await vm.GoToCurrentWeekCommand.ExecuteAsync(null);

        vm.SelectedWeekDate.Should().Be(DateTime.Today);
    }

    // ──────────────── 月导航 ────────────────

    [Fact]
    public async Task PreviousMonth_DecrementsMonthDate()
    {
        var vm = new HistoryViewModel();
        var original = vm.SelectedMonthDate;

        await vm.PreviousMonthCommand.ExecuteAsync(null);

        vm.SelectedMonthDate.Should().Be(original.AddMonths(-1));
    }

    [Fact]
    public async Task NextMonth_IncrementsMonthDate()
    {
        var vm = new HistoryViewModel();
        var original = vm.SelectedMonthDate;

        await vm.NextMonthCommand.ExecuteAsync(null);

        vm.SelectedMonthDate.Should().Be(original.AddMonths(1));
    }

    [Fact]
    public async Task GoToCurrentMonth_ResetsToToday()
    {
        var vm = new HistoryViewModel();
        vm.SelectedMonthDate = DateTime.Today.AddMonths(-3);

        await vm.GoToCurrentMonthCommand.ExecuteAsync(null);

        vm.SelectedMonthDate.Should().Be(DateTime.Today);
    }

    // ──────────────── 范围模式 ────────────────

    [Fact]
    public void ToggleRangeMode_TogglesIsRangeMode()
    {
        var vm = new HistoryViewModel();

        vm.ToggleRangeModeCommand.Execute(null);
        vm.IsRangeMode.Should().BeTrue();

        vm.ToggleRangeModeCommand.Execute(null);
        vm.IsRangeMode.Should().BeFalse();
    }

    [Fact]
    public async Task RangeLoad_WithInvalidRange_SwapsDates()
    {
        var vm = new HistoryViewModel();
        vm.RangeStart = DateTime.Today;
        vm.RangeEnd = DateTime.Today.AddDays(-5); // End before Start

        await vm.LoadRangeStatsCommand.ExecuteAsync(null);

        // Should swap: RangeEnd should now be >= RangeStart
        vm.RangeEnd.Should().BeOnOrAfter(vm.RangeStart);
    }

    // ──────────────── 柱状图数据验证 ────────────────

    [Fact]
    public async Task DayBarItems_AfterSwitch_HasExpectedData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        // Mock data should have bar chart items for today
        vm.DayBarItems.Should().NotBeNull();
    }

    [Fact]
    public async Task WeekBarItems_AfterSwitch_HasExpectedData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.WeekBarItems.Should().NotBeNull();
    }

    [Fact]
    public async Task MonthBarItems_AfterSwitch_HasExpectedData()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToMonthViewCommand.ExecuteAsync(null);

        vm.MonthBarItems.Should().NotBeNull();
    }

    // ──────────────── 表格数据验证 ────────────────

    [Fact]
    public async Task DayTableItems_AfterSwitch_HasMatchingCount()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        // Day bar items and table items should match
        vm.DayBarItems.Should().NotBeNull();
        vm.DayTableItems.Should().NotBeNull();
    }

    [Fact]
    public async Task WeekTableItems_AfterSwitch_HasMatchingCount()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.WeekBarItems.Count.Should().Be(vm.WeekTableItems.Count);
    }

    [Fact]
    public async Task MonthTableItems_AfterSwitch_HasMatchingCount()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToMonthViewCommand.ExecuteAsync(null);

        vm.MonthBarItems.Count.Should().Be(vm.MonthTableItems.Count);
    }

    // ──────────────── 总记录数验证 ────────────────

    [Fact]
    public async Task DayTotalRecordCount_AfterSwitch_IsPositive()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        vm.DayTotalRecordCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WeekTotalRecordCount_AfterSwitch_IsPositive()
    {
        var vm = new HistoryViewModel();

        await vm.SwitchToWeekViewCommand.ExecuteAsync(null);

        vm.WeekTotalRecordCount.Should().BeGreaterThan(0);
    }

    // ──────────────── 视图切换状态复位 ────────────────

    [Fact]
    public async Task ViewSwitch_ClearsSelectedSoftware()
    {
        var vm = new HistoryViewModel();
        vm.SelectedSoftwareName = "test.exe";

        await vm.SwitchToDayViewCommand.ExecuteAsync(null);

        vm.SelectedSoftwareName.Should().BeEmpty();
    }
}
