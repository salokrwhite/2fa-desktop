namespace TwoFactorAuth.Services;

public interface IAutoLockManager
{
    void UpdateConfiguration(bool appLockEnabled, bool idleAutoLockEnabled, int autoLockMinutes);
}

