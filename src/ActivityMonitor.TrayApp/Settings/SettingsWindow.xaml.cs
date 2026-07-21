using System.Windows;

namespace ActivityMonitor.TrayApp.Settings;

/// <summary>
/// 设置窗口。
/// 提供空闲阈值、数据保留策略、开机自启等配置项的界面。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
    }

    /// <summary>关闭按钮点击事件。</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
