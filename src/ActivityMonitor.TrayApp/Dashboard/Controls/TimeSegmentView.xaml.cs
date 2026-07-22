using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

public partial class TimeSegmentView
{
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments), typeof(ObservableCollection<TimeSegmentStats>),
            typeof(TimeSegmentView), new PropertyMetadata(null, OnSegmentsChanged));

    public static readonly DependencyProperty ViewDetailCommandProperty =
        DependencyProperty.Register(nameof(ViewDetailCommand), typeof(ICommand), typeof(TimeSegmentView));

    public ObservableCollection<TimeSegmentStats>? Segments
    {
        get => (ObservableCollection<TimeSegmentStats>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public ICommand? ViewDetailCommand
    {
        get => (ICommand?)GetValue(ViewDetailCommandProperty);
        set => SetValue(ViewDetailCommandProperty, value);
    }

    public TimeSegmentView() => InitializeComponent();

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TimeSegmentView view) return;
        if (e.NewValue is ObservableCollection<TimeSegmentStats> segments)
            view.ActiveSegmentCount.Text = $"活跃时段 {segments.Count(s => s.TotalDurationMs > 0)}/48";
        else
            view.ActiveSegmentCount.Text = "暂无数据";
    }
}
