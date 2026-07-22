using System.Windows;

namespace ActivityMonitor.TrayApp.ReportEditor;

/// <summary>
/// 操作日志预览对话框。
/// 以时间线形式展示指定日期的窗口切换日志。
/// </summary>
public partial class OperationLogPreviewDialog : Window
{
    /// <summary>关联的 ViewModel。</summary>
    public OperationLogPreviewViewModel ViewModel { get; }

    public OperationLogPreviewDialog()
    {
        InitializeComponent();
        ViewModel = new OperationLogPreviewViewModel();
        DataContext = ViewModel;

        // 通过自定义 Action 实现窗口关闭，避免直接修改只读的 RelayCommand
        ViewModel.CloseWindowAction = Close;
    }

    /// <summary>
    /// 以指定日期和日志仓储初始化对话框并加载日志。
    /// </summary>
    /// <param name="date">查询日期。</param>
    /// <param name="repository">操作日志仓储（可选，默认使用 Mock）。</param>
    public OperationLogPreviewDialog(DateTime date, Core.Interfaces.IOperationLogRepository? repository = null)
        : this()
    {
        if (repository != null)
        {
            ViewModel = new OperationLogPreviewViewModel(repository);
            DataContext = ViewModel;
            ViewModel.CloseWindowAction = Close;
        }

        Loaded += async (_, _) => await ViewModel.LoadLogsAsync(date);
    }

    /// <summary>
    /// 关闭按钮点击事件（备用，主要靠 Command 绑定）。
    /// </summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 按下 Esc 键关闭窗口。
    /// </summary>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
        }
    }
}
