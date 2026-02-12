using System;

namespace TwoFactorAuth.Services;

public sealed class UnsupportedIdleTimeProvider : IIdleTimeProvider
{
    public bool IsSupported => false;

    public TimeSpan GetIdleTime()
    {
        throw new NotSupportedException("Idle time detection is not supported on this platform.");
    }
}

