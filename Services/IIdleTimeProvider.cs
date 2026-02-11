using System;

namespace TwoFactorAuth.Services;

public interface IIdleTimeProvider
{
    bool IsSupported { get; }
    TimeSpan GetIdleTime();
}

