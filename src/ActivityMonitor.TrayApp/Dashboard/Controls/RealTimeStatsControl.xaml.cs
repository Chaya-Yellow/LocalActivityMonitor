using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 实时统计面板控件。
/// 通过 Tab 切换展示按应用/项目/网页/类别维度的时长分布和占比，
/// 底部支持分页导航（每页 10 条）。
/// </summary>
public partial class RealTimeStatsControl
{
    /// <summary>每页显示的统计条目数。</summary>
    private const int PageSize = 10;

    // ──────────────── 统计集合 DP ────────────────

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

    /// <summary>当前 Tab 对应的完整统计列表（只读）。</summary>
    private static readonly DependencyPropertyKey CurrentStatsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CurrentStats), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null, OnCurrentStatsChanged));

    public static readonly DependencyProperty CurrentStatsProperty = CurrentStatsPropertyKey.DependencyProperty;

    // ──────────────── 分页 DP ────────────────

    /// <summary>当前页码（从 1 开始）。</summary>
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(RealTimeStatsControl),
            new PropertyMetadata(1, OnCurrentPageChanged));

    /// <summary>总页数（只读）。</summary>
    private static readonly DependencyPropertyKey TotalPagesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TotalPages), typeof(int), typeof(RealTimeStatsControl),
            new PropertyMetadata(1));

    public static readonly DependencyProperty TotalPagesProperty = TotalPagesPropertyKey.DependencyProperty;

    /// <summary>当前页显示的条目（只读）。</summary>
    private static readonly DependencyPropertyKey PageItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(PageItems), typeof(ObservableCollection<StatsItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    public static readonly DependencyProperty PageItemsProperty = PageItemsPropertyKey.DependencyProperty;

    /// <summary>是否有上一页（只读）。</summary>
    private static readonly DependencyPropertyKey HasPrevPagePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasPrevPage), typeof(bool), typeof(RealTimeStatsControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasPrevPageProperty = HasPrevPagePropertyKey.DependencyProperty;

    /// <summary>是否有下一页（只读）。</summary>
    private static readonly DependencyPropertyKey HasNextPagePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasNextPage), typeof(bool), typeof(RealTimeStatsControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasNextPageProperty = HasNextPagePropertyKey.DependencyProperty;

    /// <summary>页码导航项列表（只读，包含数字和省略号）。</summary>
    private static readonly DependencyPropertyKey PageNumbersPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(PageNumbers), typeof(ObservableCollection<PageNavItem>),
            typeof(RealTimeStatsControl), new PropertyMetadata(null));

    public static readonly DependencyProperty PageNumbersProperty = PageNumbersPropertyKey.DependencyProperty;

    /// <summary>跳转页码命令。</summary>
    public static readonly DependencyProperty GoToPageCommandProperty =
        DependencyProperty.Register(nameof(GoToPageCommand), typeof(ICommand), typeof(RealTimeStatsControl));

    // ──────────────── 防止递归 ────────────────
    private bool _isUpdatingPage;

    // ──────────────── CLR 属性 ────────────────

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

    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int TotalPages
    {
        get => (int)GetValue(TotalPagesProperty);
        private set => SetValue(TotalPagesPropertyKey, value);
    }

    public ObservableCollection<StatsItem>? PageItems
    {
        get => (ObservableCollection<StatsItem>?)GetValue(PageItemsProperty);
        private set => SetValue(PageItemsPropertyKey, value);
    }

    public bool HasPrevPage
    {
        get => (bool)GetValue(HasPrevPageProperty);
        private set => SetValue(HasPrevPagePropertyKey, value);
    }

    public bool HasNextPage
    {
        get => (bool)GetValue(HasNextPageProperty);
        private set => SetValue(HasNextPagePropertyKey, value);
    }

    public ObservableCollection<PageNavItem>? PageNumbers
    {
        get => (ObservableCollection<PageNavItem>?)GetValue(PageNumbersProperty);
        private set => SetValue(PageNumbersPropertyKey, value);
    }

    public ICommand? GoToPageCommand
    {
        get => (ICommand?)GetValue(GoToPageCommandProperty);
        set => SetValue(GoToPageCommandProperty, value);
    }

    // ──────────────── 构造 ────────────────

    public RealTimeStatsControl()
    {
        GoToPageCommand = new RelayCommand<int>(GoToPage);
        InitializeComponent();
        UpdateCurrentStats();
    }

    // ──────────────── 回调 ────────────────

    /// <summary>Tab 切换时更新当前统计列表并重置到第 1 页。</summary>
    private static void OnSelectedTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RealTimeStatsControl control) return;
        control.UpdateCurrentStats();
        control.CurrentPage = 1;
    }

    /// <summary>CurrentStats 变化时重新计算分页。</summary>
    private static void OnCurrentStatsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealTimeStatsControl control)
            control.UpdatePageState();
    }

    /// <summary>页码变化时重新计算分页状态。</summary>
    private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealTimeStatsControl control)
            control.UpdatePageState();
    }

    // ──────────────── 内部方法 ────────────────

    /// <summary>根据当前 Tab 索引更新 <see cref="CurrentStats"/>。</summary>
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

    /// <summary>
    /// 根据 <see cref="CurrentStats"/> 和 <see cref="CurrentPage"/>
    /// 重新计算分页相关属性：总页数、当前页条目、导航状态、页码列表。
    /// </summary>
    private void UpdatePageState()
    {
        if (_isUpdatingPage) return;
        _isUpdatingPage = true;

        try
        {
            var source = CurrentStats;
            var totalItems = source?.Count ?? 0;
            var totalPages = totalItems > 0
                ? (int)System.Math.Ceiling((double)totalItems / PageSize)
                : 1;

            TotalPages = totalPages;

            // 如果当前页超出范围则修正
            if (CurrentPage < 1) CurrentPage = 1;
            else if (CurrentPage > totalPages) CurrentPage = totalPages;

            // 截取当前页条目
            var startIndex = (CurrentPage - 1) * PageSize;
            var count = System.Math.Min(PageSize, totalItems - startIndex);

            PageItems = source != null && startIndex < totalItems
                ? new ObservableCollection<StatsItem>(source.Skip(startIndex).Take(count))
                : new ObservableCollection<StatsItem>();

            HasPrevPage = CurrentPage > 1;
            HasNextPage = CurrentPage < totalPages;

            // 生成页码导航
            PageNumbers = new ObservableCollection<PageNavItem>(GeneratePageNumbers(totalPages, CurrentPage));
        }
        finally
        {
            _isUpdatingPage = false;
        }
    }

    /// <summary>
    /// 生成页码导航项列表，包含数字按钮和省略号。
    /// 总页数 ≤ 7 时全部显示；超过时首尾固定 + 当前页前后 ±1，中间用 "..." 填充。
    /// </summary>
    private static IEnumerable<PageNavItem> GeneratePageNumbers(int totalPages, int currentPage)
    {
        if (totalPages <= 1)
            yield break;

        if (totalPages <= 7)
        {
            for (var i = 1; i <= totalPages; i++)
                yield return new PageNavItem(i, i == currentPage);
            yield break;
        }

        // 始终显示第一页
        yield return new PageNavItem(1, currentPage == 1);

        // 左侧省略号
        if (currentPage > 3)
            yield return PageNavItem.Ellipsis;

        // 当前页前后
        var start = System.Math.Max(2, currentPage - 1);
        var end = System.Math.Min(totalPages - 1, currentPage + 1);
        for (var i = start; i <= end; i++)
            yield return new PageNavItem(i, i == currentPage);

        // 右侧省略号
        if (currentPage < totalPages - 2)
            yield return PageNavItem.Ellipsis;

        // 始终显示最后一页
        yield return new PageNavItem(totalPages, currentPage == totalPages);
    }

    /// <summary>跳转到指定页。</summary>
    private void GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages && page != CurrentPage)
            CurrentPage = page;
    }

    /// <summary>上一页。</summary>
    private void GoToPrevPage()
    {
        if (HasPrevPage)
            CurrentPage--;
    }

    /// <summary>下一页。</summary>
    private void GoToNextPage()
    {
        if (HasNextPage)
            CurrentPage++;
    }

    // ──────────────── 事件处理（供 XAML Click 事件绑定） ────────────────

    private void OnPrevPageClick(object sender, RoutedEventArgs e) => GoToPrevPage();
    private void OnNextPageClick(object sender, RoutedEventArgs e) => GoToNextPage();
}

/// <summary>
/// 页码导航项，表示一个可点击的数字按钮或省略号占位。
/// </summary>
public class PageNavItem
{
    /// <summary>省略号占位实例（Page = -1）。</summary>
    public static readonly PageNavItem Ellipsis = new(-1, false);

    /// <summary>页码（-1 表示省略号占位）。</summary>
    public int Page { get; }

    /// <summary>显示的文本：页码或 "…"。</summary>
    public string DisplayText { get; }

    /// <summary>是否为当前页。</summary>
    public bool IsCurrent { get; }

    /// <summary>是否为省略号占位。</summary>
    public bool IsEllipsis => Page == -1;

    public PageNavItem(int page, bool isCurrent)
    {
        Page = page;
        IsCurrent = isCurrent;
        DisplayText = page == -1 ? "…" : page.ToString();
    }
}

/// <summary>
/// 简化版 ICommand 实现（避免引入 CommunityToolkit.Mvvm 依赖到控件层）。
/// </summary>
internal class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke((T)parameter!) ?? true;

    public void Execute(object? parameter) => _execute((T)parameter!);
}
