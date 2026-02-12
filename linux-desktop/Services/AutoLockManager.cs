using System;
using System.Threading;
using Avalonia.Threading;

namespace TwoFactorAuth.Services;

public sealed class AutoLockManager : IAutoLockManager
{
    private readonly IIdleTimeProvider _idleTimeProvider;
    private readonly ISecurityService _securityService;
    private readonly IAppLockCoordinator _appLockCoordinator;
    private readonly DispatcherTimer _timer;

    private volatile bool _appLockEnabled;
    private volatile bool _idleAutoLockEnabled;
    private volatile int _autoLockMinutes;

    private int _lockInProgress;

    public AutoLockManager(IIdleTimeProvider idleTimeProvider, ISecurityService securityService, IAppLockCoordinator appLockCoordinator)
    {
        _idleTimeProvider = idleTimeProvider;
        _securityService = securityService;
        _appLockCoordinator = appLockCoordinator;

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, OnTick);
        _timer.Start();
    }

    public void UpdateConfiguration(bool appLockEnabled, bool idleAutoLockEnabled, int autoLockMinutes)
    {
        _appLockEnabled = appLockEnabled;
        _idleAutoLockEnabled = idleAutoLockEnabled;
        _autoLockMinutes = autoLockMinutes;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!_idleAutoLockEnabled) return;
        if (!_idleTimeProvider.IsSupported) return;
        if (_autoLockMinutes <= 0) return;
        if (!_securityService.IsUnlocked) return;

        TimeSpan idle;
        try
        {
            idle = _idleTimeProvider.GetIdleTime();
        }
        catch
        {
            return;
        }

        if (idle < TimeSpan.FromMinutes(_autoLockMinutes)) return;
        if (Interlocked.CompareExchange(ref _lockInProgress, 1, 0) != 0) return;

        try
        {
            await _appLockCoordinator.LockAsync(LockReason.IdleTimeout);
        }
        finally
        {
            Interlocked.Exchange(ref _lockInProgress, 0);
        }
    }
}

