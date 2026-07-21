using ActivityMonitor.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.Settings;

/// <summary>
/// 设置页 ViewModel。
/// 管理数据保留策略、开机自启、空闲阈值等配置项。
/// 使用 Mock 数据先行开发，后期接入 ISettingsRepository。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // ──────────────── 设置键名常量 ────────────────
    private const string KeyIdleThreshold = "idle_threshold_minutes";
    private const string KeyRetentionDays = "retention_days";
    private const string KeyAutoStart = "auto_start";

    // ──────────────── 可观察属性 ────────────────

    /// <summary>空闲阈值（分钟）。</summary>
    [ObservableProperty]
    private int _idleThresholdMinutes = 15;

    /// <summary>数据保留策略：30=30天, 90=90天, 0=永久, -1=手动清理。</summary>
    [ObservableProperty]
    private int _retentionDays = 30;

    /// <summary>是否开机自启。</summary>
    [ObservableProperty]
    private bool _autoStart = true;

    /// <summary>状态信息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>操作是否成功。</summary>
    [ObservableProperty]
    private bool _isSuccess;

    /// <summary>数据保留策略的选项显示文本。</summary>
    public string[] RetentionOptions { get; } = { "30 天", "90 天", "永久保留", "手动清理" };

    /// <summary>数据保留策略对应的值。</summary>
    public int[] RetentionValues { get; } = { 30, 90, 0, -1 };

    /// <summary>当前保留策略在选项列表中的索引。</summary>
    [ObservableProperty]
    private int _retentionIndex;

    public SettingsViewModel()
    {
        // 初始化默认值
        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// 加载设置（Mock 实现：使用默认值）。
    /// </summary>
    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            // Mock：模拟异步加载
            await Task.Delay(100);

            // 后续接入真正的 ISettingsRepository
            // IdleThresholdMinutes = int.Parse(await _settingsRepo.GetAsync(KeyIdleThreshold, "15"));
            // RetentionDays = int.Parse(await _settingsRepo.GetAsync(KeyRetentionDays, "30"));
            // AutoStart = bool.Parse(await _settingsRepo.GetAsync(KeyAutoStart, "true"));

            // 计算 RetentionIndex
            RetentionIndex = Array.IndexOf(RetentionValues, RetentionDays);
            if (RetentionIndex < 0) RetentionIndex = 0;

            StatusMessage = "设置已加载。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设置失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 保存所有设置。
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Mock：模拟异步保存
            await Task.Delay(200);

            // 同步 RetentionDays 和 RetentionIndex
            if (RetentionIndex >= 0 && RetentionIndex < RetentionValues.Length)
                RetentionDays = RetentionValues[RetentionIndex];

            // 后续接入真正的 ISettingsRepository
            // await _settingsRepo.SetAsync(KeyIdleThreshold, IdleThresholdMinutes.ToString());
            // await _settingsRepo.SetAsync(KeyRetentionDays, RetentionDays.ToString());
            // await _settingsRepo.SetAsync(KeyAutoStart, AutoStart.ToString());

            // 开机自启（注册表）
            if (AutoStart)
                SetAutoStart(true);

            StatusMessage = "✅ 设置已保存";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败：{ex.Message}";
            IsSuccess = false;
        }
    }

    /// <summary>
    /// 重置为默认值。
    /// </summary>
    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        IdleThresholdMinutes = 15;
        RetentionDays = 30;
        RetentionIndex = 0; // 对应 "30 天"
        AutoStart = true;

        StatusMessage = "✅ 已恢复默认设置（尚未保存）";
        IsSuccess = true;

        await Task.CompletedTask;
    }

    /// <summary>
    /// 清理所有数据（危险操作，需确认）。
    /// </summary>
    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        // TODO: 弹出确认对话框
        StatusMessage = "⚠️ 该功能尚未实现：确认对话框 + 数据清除";
        IsSuccess = false;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 设置开机自启（注册表 HKCU\...\Run）。
    /// </summary>
    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                    key.SetValue("ActivityMonitor", exePath);
            }
            else
            {
                key.DeleteValue("ActivityMonitor", false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsVM] 设置开机自启失败: {ex.Message}");
        }
    }
}
