using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActivityMonitor.Core.Interfaces;
using Brush = System.Windows.Media.Brush;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

public partial class TimeSegmentCard
{
    public static readonly DependencyProperty SegmentDataProperty =
        DependencyProperty.Register(nameof(SegmentData), typeof(TimeSegmentStats), typeof(TimeSegmentCard),
            new PropertyMetadata(null, OnSegmentDataChanged));

    public static readonly DependencyProperty ViewDetailCommandProperty =
        DependencyProperty.Register(nameof(ViewDetailCommand), typeof(ICommand), typeof(TimeSegmentCard));

    private static readonly DependencyPropertyKey TimeRangeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TimeRangeText), typeof(string), typeof(TimeSegmentCard), new PropertyMetadata(""));
    public static readonly DependencyProperty TimeRangeTextProperty = TimeRangeTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey TotalActiveTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TotalActiveText), typeof(string), typeof(TimeSegmentCard), new PropertyMetadata("0m"));
    public static readonly DependencyProperty TotalActiveTextProperty = TotalActiveTextPropertyKey.DependencyProperty;

    private static readonly System.Windows.Media.Color[] SoftwarePalette =
    {
        System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4),
        System.Windows.Media.Color.FromRgb(0x10, 0x7C, 0x10),
        System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00),
        System.Windows.Media.Color.FromRgb(0x7B, 0x3F, 0xB0),
        System.Windows.Media.Color.FromRgb(0xD1, 0x34, 0x38),
        System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xBC),
        System.Windows.Media.Color.FromRgb(0xE8, 0x11, 0x23),
        System.Windows.Media.Color.FromRgb(0x49, 0x79, 0x6B),
    };

    public TimeSegmentStats? SegmentData
    {
        get => (TimeSegmentStats?)GetValue(SegmentDataProperty);
        set => SetValue(SegmentDataProperty, value);
    }

    public ICommand? ViewDetailCommand
    {
        get => (ICommand?)GetValue(ViewDetailCommandProperty);
        set => SetValue(ViewDetailCommandProperty, value);
    }

    public string TimeRangeText => (string)GetValue(TimeRangeTextProperty);
    public string TotalActiveText => (string)GetValue(TotalActiveTextProperty);

    public TimeSegmentCard()
    {
        InitializeComponent();
        MouseLeftButtonUp += (_, _) =>
        {
            if (ViewDetailCommand?.CanExecute(SegmentData) == true)
                ViewDetailCommand.Execute(SegmentData);
        };
    }

    private static void OnSegmentDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TimeSegmentCard card || e.NewValue is not TimeSegmentStats data) return;
        card.BuildCard(data);
    }

    private void BuildCard(TimeSegmentStats data)
    {
        var start = data.SegmentStart;
        var end = start.AddMinutes(30);
        SetValue(TimeRangeTextPropertyKey, $"{start:HH:mm} — {end:HH:mm}");

        if (data.TotalDurationMs > 0)
        {
            var ts = TimeSpan.FromMilliseconds(data.TotalDurationMs);
            SetValue(TotalActiveTextPropertyKey, ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m");
        }
        else
        {
            SetValue(TotalActiveTextPropertyKey, "无活动");
        }

        BuildDonutChart(data);
        BuildSoftwareList(data);
    }

    private void BuildDonutChart(TimeSegmentStats data)
    {
        foreach (var p in DonutContainer.Children.OfType<System.Windows.Shapes.Path>().ToList())
            DonutContainer.Children.Remove(p);

        if (data.TotalDurationMs <= 0 || data.SoftwareList.Count == 0)
        {
            foreach (var p in DonutChartBuilder.BuildDonut(new List<(double, string)>()))
                DonutContainer.Children.Insert(0, p);
            return;
        }

        var filtered = data.SoftwareList.Where(s => s.DurationMs >= 60_000).ToList();
        if (filtered.Count == 0)
        {
            foreach (var p in DonutChartBuilder.BuildDonut(new List<(double, string)>()))
                DonutContainer.Children.Insert(0, p);
            return;
        }

        var pcts = filtered.Select(s => (s.Percentage, s.Name)).ToList();
        foreach (var p in DonutChartBuilder.BuildDonut(pcts))
            DonutContainer.Children.Insert(0, p);

        var ts = TimeSpan.FromMilliseconds(data.TotalDurationMs);
        TotalTimeText.Text = ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h{ts.Minutes}m" : $"{ts.Minutes}m";
    }

    private void BuildSoftwareList(TimeSegmentStats data)
    {
        SoftwareListPanel.Children.Clear();
        if (data.TotalDurationMs <= 0 || data.SoftwareList.Count == 0)
        {
            SoftwareListPanel.Children.Add(new TextBlock
            {
                Text = "暂无活动", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }

        var items = data.SoftwareList.Where(s => s.DurationMs >= 60_000)
            .OrderByDescending(s => s.DurationMs).ToList();

        if (items.Count == 0)
        {
            SoftwareListPanel.Children.Add(new TextBlock
            {
                Text = "所有活动 < 1 分钟", FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }

        foreach (var item in items.Take(4))
        {
            var ci = Math.Abs(item.Name.GetHashCode()) % SoftwarePalette.Length;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(SoftwarePalette[ci]),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var nameText = new TextBlock
            {
                Text = item.Name, FontSize = 11,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 90,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(nameText, 1);
            row.Children.Add(nameText);
            SoftwareListPanel.Children.Add(row);
        }

        if (items.Count > 4)
        {
            SoftwareListPanel.Children.Add(new TextBlock
            {
                Text = $"更多 {items.Count - 4} 项...", FontSize = 10,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(10, 1, 0, 0),
            });
        }
    }
}
