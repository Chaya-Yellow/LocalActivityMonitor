using System.Collections.ObjectModel;
using System.Windows;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

public partial class TimeSegmentDetailWindow : Window
{
    public string TimeRangeText { get; }
    public string TotalActiveText { get; }
    public ObservableCollection<StatsItem> SoftwareList { get; }

    public TimeSegmentDetailWindow(TimeSegmentStats segment)
    {
        var start = segment.SegmentStart;
        TimeRangeText = $"{start:HH:mm} — {start.AddMinutes(30):HH:mm}";

        if (segment.TotalDurationMs > 0)
        {
            var ts = TimeSpan.FromMilliseconds(segment.TotalDurationMs);
            TotalActiveText = ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m";
        }
        else TotalActiveText = "0m";

        var items = segment.SoftwareList?
            .Where(s => s.DurationMs >= 60_000)
            .OrderByDescending(s => s.DurationMs)
            .ToList() ?? new List<StatsItem>();

        SoftwareList = new ObservableCollection<StatsItem>(items);
        InitializeComponent();
        DataContext = this;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
