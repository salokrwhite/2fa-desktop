using System;
using System.Threading.Tasks;

namespace TwoFactorAuth.Services;

public interface IAppLockCoordinator
{
    bool IsLocked { get; }
    event EventHandler? IsLockedChanged;
    
    Task LockAsync(LockReason reason);
    Task EnsureUnlockedAsync(LockReason reason);
    Task<bool> UnlockAsync(string password);
}

