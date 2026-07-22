using System.Collections;
using System.Windows;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 自定义水平柱状图控件。
/// 使用 Rectangle + ItemsControl 绘制，不依赖第三方图表库。
/// </summary>
public partial class BarChartView : WpfUserControl
{
    /// <summary>柱状图的数据源（BarChartItem 列表）。</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(BarChartView),
            new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>柱状图的标题文本。</summary>
    public static readonly DependencyProperty ChartTitleProperty =
        DependencyProperty.Register(
            nameof(ChartTitle),
            typeof(string),
            typeof(BarChartView),
            new PropertyMetadata("软件分布"));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string ChartTitle
    {
        get => (string)GetValue(ChartTitleProperty);
        set => SetValue(ChartTitleProperty, value);
    }

    public BarChartView()
    {
        InitializeComponent();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BarChartView view)
        {
            view.UpdateEmptyState(e.NewValue);
        }
    }

    private void UpdateEmptyState(object? itemsSource)
    {
        var items = itemsSource as ICollection;
        var hasData = items is not null && items.Count > 0;

        BarItemsControl.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        EmptyLabel.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
    }
}
