using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 单条活动事件卡片控件。
/// 在时间线中展示一条活动记录：时间段、进程名、窗口标题、项目/域名、时长和操作按钮。
/// </summary>
public partial class TimelineItem
{
    // ──────────────── 依赖属性 ────────────────

    /// <summary>绑定的事件数据。</summary>
    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.Register(nameof(EventData), typeof(ActivityEvent), typeof(TimelineItem),
            new PropertyMetadata(null, OnEventChanged));

    /// <summary>编辑命令。</summary>
    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(TimelineItem));

    /// <summary>删除命令。</summary>
    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(TimelineItem));

    // ──────────────── 计算属性（只读） ────────────────

    private static readonly DependencyPropertyKey StartTimeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(StartTimeText), typeof(string), typeof(TimelineItem),
            new PropertyMetadata(""));

    public static readonly DependencyProperty StartTimeTextProperty = StartTimeTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey EndTimeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(EndTimeText), typeof(string), typeof(TimelineItem),
            new PropertyMetadata(""));

    public static readonly DependencyProperty EndTimeTextProperty = EndTimeTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DisplayTitlePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayTitle), typeof(string), typeof(TimelineItem),
            new PropertyMetadata(""));

    public static readonly DependencyProperty DisplayTitleProperty = DisplayTitlePropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DurationTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DurationText), typeof(string), typeof(TimelineItem),
            new PropertyMetadata(""));

    public static readonly DependencyProperty DurationTextProperty = DurationTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DetailTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DetailText), typeof(string), typeof(TimelineItem),
            new PropertyMetadata(""));

    public static readonly DependencyProperty DetailTextProperty = DetailTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey CategoryColorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CategoryColor), typeof(Brush), typeof(TimelineItem),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray)));

    public static readonly DependencyProperty CategoryColorProperty = CategoryColorPropertyKey.DependencyProperty;

    // ──────────────── CLR 属性 ────────────────

    public ActivityEvent? EventData
    {
        get => (ActivityEvent?)GetValue(EventDataProperty);
        set => SetValue(EventDataProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public string StartTimeText => (string)GetValue(StartTimeTextProperty);
    public string EndTimeText => (string)GetValue(EndTimeTextProperty);
    public string DisplayTitle => (string)GetValue(DisplayTitleProperty);
    public string DurationText => (string)GetValue(DurationTextProperty);
    public string DetailText => (string)GetValue(DetailTextProperty);
    public Brush CategoryColor => (Brush)GetValue(CategoryColorProperty);

    /// <summary>类别 → 颜色映射。</summary>
    private static readonly Dictionary<string, Color> CategoryColorMap = new()
    {
        [Category.Web] = Color.FromRgb(0, 0x78, 0xD4),   // 蓝
        [Category.File] = Color.FromRgb(0x10, 0x7C, 0x10), // 绿
        [Category.App] = Color.FromRgb(0xFF, 0x8C, 0x00), // 橙
        [Category.Idle] = Color.FromRgb(0x76, 0x76, 0x76), // 灰
        [Category.Sleep] = Color.FromRgb(0x4B, 0x00, 0x82), // 紫
    };

    public TimelineItem()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 事件数据变化时重新计算所有展示文本。
    /// </summary>
    private static void OnEventChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TimelineItem item || e.NewValue is not ActivityEvent evt) return;

        // 时间格式化
        item.SetValue(StartTimeTextPropertyKey, evt.StartTime.ToString("HH:mm"));
        item.SetValue(EndTimeTextPropertyKey, evt.EndTime?.ToString("HH:mm") ?? "进行中");

        // 标题：优先使用用户编辑标题，否则用窗口标题或进程名
        var title = evt.EditedTitle ?? evt.WindowTitle ?? evt.ProcessName ?? "(无标题)";
        item.SetValue(DisplayTitlePropertyKey, title);

        // 时长格式化
        var ts = TimeSpan.FromMilliseconds(evt.DurationMs);
        item.SetValue(DurationTextPropertyKey, ts.TotalHours >= 1 ? $"{ts.Hours:D2}h{ts.Minutes:D2}m" : $"{ts.Minutes}m");

        // 详情：项目 / 域名 / 描述
        var detailParts = new List<string>();
        if (!string.IsNullOrEmpty(evt.Project)) detailParts.Add($"📁 {evt.Project}");
        if (!string.IsNullOrEmpty(evt.Domain)) detailParts.Add($"🌐 {evt.Domain}");
        if (!string.IsNullOrEmpty(evt.EditedDesc)) detailParts.Add($"📝 {evt.EditedDesc}");
        item.SetValue(DetailTextPropertyKey, detailParts.Count > 0 ? string.Join(" · ", detailParts) : "");

        // 类别颜色
        var color = CategoryColorMap.TryGetValue(evt.Category, out var c) ? c : Colors.Gray;
        item.SetValue(CategoryColorPropertyKey, new SolidColorBrush(color));
    }
}
