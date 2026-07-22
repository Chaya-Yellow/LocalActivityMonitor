using System.Collections;
using System.Windows;
using System.Windows.Input;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace ActivityMonitor.TrayApp.History.Controls;

/// <summary>
/// 自定义水平柱状图控件。
/// 使用 Rectangle + ItemsControl 绘制，不依赖第三方图表库。
/// 支持点击选中高亮，通过 SelectedItemName 与外部表格联动。
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

    /// <summary>外部选中的软件名称（用于高亮联动）。由 ViewModel 双向驱动。</summary>
    public static readonly DependencyProperty SelectedItemNameProperty =
        DependencyProperty.Register(
            nameof(SelectedItemName),
            typeof(string),
            typeof(BarChartView),
            new PropertyMetadata(string.Empty));

    /// <summary>点击柱子时执行的 ICommand。CommandParameter 为软件名称。</summary>
    public static readonly DependencyProperty ItemClickCommandProperty =
        DependencyProperty.Register(
            nameof(ItemClickCommand),
            typeof(ICommand),
            typeof(BarChartView));

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

    /// <summary>当前高亮的软件名称。设置为空字符串则取消所有高亮。</summary>
    public string SelectedItemName
    {
        get => (string)GetValue(SelectedItemNameProperty);
        set => SetValue(SelectedItemNameProperty, value);
    }

    /// <summary>柱状图点击命令，传入软件名作为参数。</summary>
    public ICommand ItemClickCommand
    {
        get => (ICommand)GetValue(ItemClickCommandProperty);
        set => SetValue(ItemClickCommandProperty, value);
    }

    public BarChartView()
    {
        InitializeComponent();
    }

    /// <summary>柱子鼠标左键点击事件处理：执行 ItemClickCommand，传入软件名。</summary>
    private void BarRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BarChartItem item)
        {
            ItemClickCommand?.Execute(item.Name);
            e.Handled = true;
        }
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
