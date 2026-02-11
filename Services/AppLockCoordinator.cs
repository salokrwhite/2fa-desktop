using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TwoFactorAuth.Views.Dialogs;

namespace TwoFactorAuth.Services;

public sealed class AppLockCoordinator : IAppLockCoordinator
{
    private readonly ISecurityService _securityService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TaskCompletionSource? _unlockTcs;
    
    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        private set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                IsLockedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    public event EventHandler? IsLockedChanged;

    public AppLockCoordinator(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public Task LockAsync(LockReason reason)
    {
        _ = _securityService.LockAsync();
        IsLocked = true;
        
        if (_unlockTcs == null || _unlockTcs.Task.IsCompleted)
        {
             _unlockTcs = new TaskCompletionSource();
        }
        
        return Task.CompletedTask;
    }

    public async Task EnsureUnlockedAsync(LockReason reason)
    {
        if (_securityService.IsUnlocked) return;
        
        await _gate.WaitAsync();
        try
        {
            if (_securityService.IsUnlocked) return;
            
            IsLocked = true;
            
            if (_unlockTcs == null || _unlockTcs.Task.IsCompleted)
            {
                _unlockTcs = new TaskCompletionSource();
            }
            
            await _unlockTcs.Task;
        }
        finally
        {
            _gate.Release();
        }
    }
    
    public async Task<bool> UnlockAsync(string password)
    {
        var success = await _securityService.UnlockAsync(password);
        if (success)
        {
            IsLocked = false;
            _unlockTcs?.TrySetResult();
        }
        return success;
    }
    
    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
