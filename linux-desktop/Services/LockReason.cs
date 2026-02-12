namespace TwoFactorAuth.Services;

public enum LockReason
{
    Startup = 0,
    IdleTimeout = 1,
    Manual = 2
}

