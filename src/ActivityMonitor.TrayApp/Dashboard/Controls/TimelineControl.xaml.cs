using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 当日时间线控件。
/// 以垂直列表形式展示所有活动事件记录，支持编辑、删除和插入线下活动。
/// </summary>
public partial class TimelineControl
{
    /// <summary>事件列表。</summary>
    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(ObservableCollection<ActivityEvent>),
            typeof(TimelineControl), new PropertyMetadata(null));

    /// <summary>编辑事件命令。</summary>
    public static readonly DependencyProperty EditEventCommandProperty =
        DependencyProperty.Register(nameof(EditEventCommand), typeof(ICommand), typeof(TimelineControl));

    /// <summary>删除事件命令。</summary>
    public static readonly DependencyProperty DeleteEventCommandProperty =
        DependencyProperty.Register(nameof(DeleteEventCommand), typeof(ICommand), typeof(TimelineControl));

    /// <summary>插入线下活动命令。</summary>
    public static readonly DependencyProperty InsertOfflineCommandProperty =
        DependencyProperty.Register(nameof(InsertOfflineCommand), typeof(ICommand), typeof(TimelineControl));

    public ObservableCollection<ActivityEvent>? Events
    {
        get => (ObservableCollection<ActivityEvent>?)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public ICommand? EditEventCommand
    {
        get => (ICommand?)GetValue(EditEventCommandProperty);
        set => SetValue(EditEventCommandProperty, value);
    }

    public ICommand? DeleteEventCommand
    {
        get => (ICommand?)GetValue(DeleteEventCommandProperty);
        set => SetValue(DeleteEventCommandProperty, value);
    }

    public ICommand? InsertOfflineCommand
    {
        get => (ICommand?)GetValue(InsertOfflineCommandProperty);
        set => SetValue(InsertOfflineCommandProperty, value);
    }

    public TimelineControl()
    {
        InitializeComponent();
    }
}
