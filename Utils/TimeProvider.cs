using System;

namespace TwoFactorAuth.Utils;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
