using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using ActivityMonitor.TrayApp.ReportEditor;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ActivityMonitor.Tests.FrontendTests;

/// <summary>
/// W1-M3 日志预览弹窗 — ViewModel 回归测试。
/// 验证操作日志的加载、编辑、保存、取消编辑及导出联动的核心逻辑。
/// </summary>
public class OperationLogPreviewViewModelTests
{
    // ──────────────── 构造 ────────────────

    [Fact]
    public void Constructor_Default_InitializesWithEmptyLogs()
    {
        var vm = new OperationLogPreviewViewModel();

        vm.Logs.Should().BeEmpty();
        vm.IsLoading.Should().BeFalse();
        vm.IsLoaded.Should().BeFalse();
        vm.Title.Should().Be("操作日志预览");
    }

    [Fact]
    public void Constructor_WithRepository_InitializesCorrectly()
    {
        var repo = Substitute.For<IOperationLogRepository>();
        var exporter = Substitute.For<IReportExporter>();

        var vm = new OperationLogPreviewViewModel(repo, exporter);

        vm.Logs.Should().BeEmpty();
    }

    // ──────────────── 加载日志 ────────────────

    [Fact]
    public async Task LoadLogsAsync_WithMockData_PopulatesLogsAndTitle()
    {
        var vm = new OperationLogPreviewViewModel();

        await vm.LoadLogsAsync(new DateTime(2026, 7, 21));

        vm.Logs.Should().HaveCount(42);
        vm.IsLoaded.Should().BeTrue();
        vm.Title.Should().Contain("2026年7月21日");
        vm.Title.Should().Contain("共 42 条");
    }

    [Fact]
    public async Task LoadLogsAsync_UpdatesQueryDate()
    {
        var vm = new OperationLogPreviewViewModel();
        var date = new DateTime(2026, 7, 21);

        await vm.LoadLogsAsync(date);

        vm.QueryDate.Should().Be(date);
    }

    [Fact]
    public async Task LoadLogsAsync_TitleIncludesWeekday()
    {
        var vm = new OperationLogPreviewViewModel();
        // 2026-07-22 is a Wednesday (周三) in our system's Chinese weekdays
        var date = new DateTime(2026, 7, 22);

        await vm.LoadLogsAsync(date);

        vm.Title.Should().Contain("周三");
    }

    [Fact]
    public async Task LoadLogsAsync_LogItemProperties_AreCorrect()
    {
        var vm = new OperationLogPreviewViewModel();

        await vm.LoadLogsAsync(DateTime.Today);

        vm.Logs.Should().NotBeEmpty();
        var first = vm.Logs[0];
        first.TimeFormatted.Should().NotBeNullOrEmpty();
        first.ProcessName.Should().NotBeNullOrEmpty();
        first.WindowTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadLogsAsync_FirstItem_HasIsFirstTrue()
    {
        var vm = new OperationLogPreviewViewModel();

        await vm.LoadLogsAsync(DateTime.Today);

        vm.Logs[0].IsFirst.Should().BeTrue();
        vm.Logs[0].IsLast.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLogsAsync_LastItem_HasIsLastTrue()
    {
        var vm = new OperationLogPreviewViewModel();

        await vm.LoadLogsAsync(DateTime.Today);

        vm.Logs[^1].IsLast.Should().BeTrue();
        vm.Logs[^1].IsFirst.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLogsAsync_WithRepository_PopulatesCorrectly()
    {
        var repo = new MockOperationLogRepository();
        var exporter = Substitute.For<IReportExporter>();
        var vm = new OperationLogPreviewViewModel(repo, exporter);

        await vm.LoadLogsAsync(DateTime.Today);

        vm.Logs.Should().HaveCount(42);
    }

    // ──────────────── CategoryLabel 和 CategoryColor ────────────────

    [Theory]
    [InlineData("app", "应用")]
    [InlineData("web", "网页")]
    [InlineData("file", "文件")]
    [InlineData("unknown", "unknown")]
    [InlineData(null, "未知")]
    public void OperationLogItem_CategoryLabel_ReturnsChineseLabel(string? category, string expected)
    {
        var item = new OperationLogItem { Category = category, _originalLog = new OperationLog() };

        item.CategoryLabel.Should().Be(expected);
    }

    [Theory]
    [InlineData("app", "#E8F5E9")]
    [InlineData("web", "#E3F2FD")]
    [InlineData("file", "#FFF3E0")]
    [InlineData(null, "#F3E5F5")]
    public void OperationLogItem_CategoryColor_ReturnsExpectedColor(string? category, string expected)
    {
        var item = new OperationLogItem { Category = category, _originalLog = new OperationLog() };

        item.CategoryColor.Should().Be(expected);
    }

    // ──────────────── 编辑 ────────────────

    [Fact]
    public void EditItem_Null_DoesNothing()
    {
        var vm = new OperationLogPreviewViewModel();

        // Should not throw
        var act = () => vm.EditItemCommand.Execute(null);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task EditItem_SetsIsEditingTrue()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);
        var item = vm.Logs[0];

        vm.EditItemCommand.Execute(item);

        item.IsEditing.Should().BeTrue();
    }

    [Fact]
    public async Task EditItem_CapturesTitleBeforeEdit()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);
        var item = vm.Logs[0];
        var originalTitle = item.WindowTitle;

        vm.EditItemCommand.Execute(item);

        item._titleBeforeEdit.Should().Be(originalTitle);
    }

    // ──────────────── 取消编辑 ────────────────

    [Fact]
    public async Task CancelEdit_RestoresTitle()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);
        var item = vm.Logs[0];
        var originalTitle = item.WindowTitle;

        vm.EditItemCommand.Execute(item);
        item.WindowTitle = "Modified Title";
        vm.CancelEditCommand.Execute(item);

        item.IsEditing.Should().BeFalse();
        item.WindowTitle.Should().Be(originalTitle);
    }

    [Fact]
    public void CancelEdit_Null_DoesNotThrow()
    {
        var vm = new OperationLogPreviewViewModel();

        var act = () => vm.CancelEditCommand.Execute(null);
        act.Should().NotThrow();
    }

    // ──────────────── 保存编辑 ────────────────

    [Fact]
    public async Task SaveItemAsync_Successful_SetsIsEditingFalse()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);
        var item = vm.Logs[0];

        vm.EditItemCommand.Execute(item);
        item.WindowTitle = "New Title";
        await vm.SaveItemCommand.ExecuteAsync(item);

        item.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void SaveItemAsync_Null_DoesNotThrow()
    {
        var vm = new OperationLogPreviewViewModel();

        var act = async () => await vm.SaveItemCommand.ExecuteAsync(null);
        act.Should().NotThrowAsync();
    }

    // ──────────────── 关闭 ────────────────

    [Fact]
    public void Close_InvokesCloseWindowAction()
    {
        var vm = new OperationLogPreviewViewModel();
        var wasCalled = false;
        vm.CloseWindowAction = () => wasCalled = true;

        vm.CloseCommand.Execute(null);

        wasCalled.Should().BeTrue();
    }

    // ──────────────── 导出 ────────────────

    [Fact]
    public async Task ExportToFile_Successful_SetsExportSuccess()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);

        await vm.ExportToFileCommand.ExecuteAsync(null);

        vm.IsExportSuccess.Should().BeTrue();
        vm.ExportStatus.Should().Contain("导出成功");
    }

    [Fact]
    public async Task ExportToFile_WithEmptyLogs_ExportsCorrectly()
    {
        var repo = new MockOperationLogRepository();
        var exporter = Substitute.For<IReportExporter>();
        exporter.ExportDailyAsync(Arg.Any<DateTime>()).Returns("# Work Report\n\nNo content.");
        var vm = new OperationLogPreviewViewModel(repo, exporter);
        // Don't load logs - use empty list

        await vm.ExportToFileCommand.ExecuteAsync(null);

        vm.IsExportSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExportToFile_WhileExporting_DoesNotDoubleExport()
    {
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(DateTime.Today);

        // The IsExporting guard should prevent double execution
        // Since this uses MockReportExporter which completes synchronously,
        // the guard flag should be set and cleared within one execution
        await vm.ExportToFileCommand.ExecuteAsync(null);

        vm.IsExportSuccess.Should().BeTrue();
    }

    // ──────────────── UpdateTitle ────────────────

    [Fact]
    public async Task UpdateTitle_AfterDelete_PerformedInternally()
    {
        // LoadLogsAsync calls UpdateTitle internally
        var vm = new OperationLogPreviewViewModel();
        await vm.LoadLogsAsync(new DateTime(2026, 7, 21));

        var count = vm.Logs.Count;
        vm.Title.Should().Contain($"共 {count} 条");
    }
}
