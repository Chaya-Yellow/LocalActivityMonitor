using System.Windows;

namespace ActivityMonitor.TrayApp.ReportEditor;

/// <summary>
/// 日报编辑器窗口。
/// 支持 Markdown 日报的预览、编辑和导出为 .md 文件。
/// </summary>
public partial class ReportEditorWindow : Window
{
    public ReportEditorViewModel ViewModel { get; }

    public ReportEditorWindow()
    {
        InitializeComponent();
        ViewModel = new ReportEditorViewModel();
        DataContext = ViewModel;
    }

    /// <summary>关闭按钮点击事件。</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
