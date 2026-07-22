namespace ActivityMonitor.Core.Interfaces;

public interface ILockScreenDetector
{
    event EventHandler<bool>? OnLockStateChanged;
    bool IsLocked { get; }
    long LockThresholdMs { get; set; }
    void Start();
    void Stop();
}
