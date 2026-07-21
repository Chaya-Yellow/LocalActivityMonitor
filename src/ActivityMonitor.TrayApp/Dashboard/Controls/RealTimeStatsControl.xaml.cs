using System.Collections.ObjectModel;
using System.Windows;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 实时统计面板控件。
/// 通过 Tab 切换展示按应用/项目/网页/类别维度的时长分布和占比。
/// </summary>
public partial class RealTimeStatsControl
{
    /// <summary>按应用程序的统计数据。</summary>
    public static readonly DependencyProperty AppStatsProperty =
        DependencyProperty.Register(nameof(AppStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    /// <summary>按项目的统计数据。</summary>
    public static readonly DependencyProperty ProjectStatsProperty =
        DependencyProperty.Register(nameof(ProjectStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    /// <summary>按域名的统计数据。</summary>
    public static readonly DependencyProperty DomainStatsProperty =
        DependencyProperty.Register(nameof(DomainStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    /// <summary>按类别的统计数据。</summary>
    public static readonly DependencyProperty CategoryStatsProperty =
        DependencyProperty.Register(nameof(CategoryStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    /// <summary>当前选中的 Tab 索引（0=应用,1=项目,2=网页,3=类别）。</summary>
    public static readonly DependencyProperty SelectedTabProperty =
        DependencyProperty.Register(nameof(SelectedTab), typeof(int), typeof(RealTimeStatsControl),
            new PropertyMetadata(0, OnSelectedTabChanged));

    /// <summary>当前 Tab 对应的统计列表（只读）。</summary>
    private static readonly DependencyPropertyKey CurrentStatsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CurrentStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    public static readonly DependencyProperty CurrentStatsProperty = CurrentStatsPropertyKey.DependencyProperty;

    public ObservableCollection<StatsItem>? AppStats
    {
        get => (ObservableCollection<StatsItem>?)GetValue(AppStatsProperty);
        set => SetValue(AppStatsProperty, value);
    }

    public ObservableCollection<StatsItem>? ProjectStats
    {
        get => (ObservableCollection<StatsItem>?)GetValue(ProjectStatsProperty);
        set => SetValue(ProjectStatsProperty, value);
    }

    public ObservableCollection<StatsItem>? DomainStats
    {
        get => (ObservableCollection<StatsItem>?)GetValue(DomainStatsProperty);
        set => SetValue(DomainStatsProperty, value);
    }

    public ObservableCollection<StatsItem>? CategoryStats
    {
        get => (ObservableCollection<StatsItem>?)GetValue(CategoryStatsProperty);
        set => SetValue(CategoryStatsProperty, value);
    }

    public int SelectedTab
    {
        get => (int)GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public ObservableCollection<StatsItem>? CurrentStats
    {
        get => (ObservableCollection<StatsItem>?)GetValue(CurrentStatsProperty);
        private set => SetValue(CurrentStatsPropertyKey, value);
    }

    public RealTimeStatsControl()
    {
        InitializeComponent();
        UpdateCurrentStats();
    }

    /// <summary>
    /// Tab 切换时更新当前显示的统计列表。
    /// </summary>
    private static void OnSelectedTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealTimeStatsControl control)
            control.UpdateCurrentStats();
    }

    private void UpdateCurrentStats()
    {
        CurrentStats = SelectedTab switch
        {
            0 => AppStats,
            1 => ProjectStats,
            2 => DomainStats,
            3 => CategoryStats,
            _ => AppStats,
        };
    }
}
