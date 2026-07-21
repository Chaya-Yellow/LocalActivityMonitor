using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 摘要统计卡片控件。
/// 显示今日概览中的单项指标（总时长、工作时长、空闲时长、事件数等），
/// 支持自定义标签、数值、单位和强调色。
/// </summary>
public partial class SummaryCard
{
    /// <summary>标签（如"总时长"）。</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SummaryCard), new PropertyMetadata("标签"));

    /// <summary>数值（如"6h 42m"）。</summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(SummaryCard), new PropertyMetadata("0"));

    /// <summary>单位/副文本（如"活跃 · 含空闲"）。</summary>
    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SummaryCard), new PropertyMetadata(null));

    /// <summary>强调色（卡片左侧装饰条颜色）。</summary>
    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(SummaryCard),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 0x78, 0xD4))));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Unit
    {
        get => (string?)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public Brush AccentColor
    {
        get => (Brush)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public SummaryCard()
    {
        InitializeComponent();
    }
}
