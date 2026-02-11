using System;

namespace TwoFactorAuth.Utils;

public static class TotpGenerator
{
    public static string GenerateTotp(string secret, int digits = OtpConstants.DefaultDigits, int period = OtpConstants.DefaultPeriod, DateTime? utcNow = null)
    {
        var time = utcNow ?? DateTime.UtcNow;
        long counter = GetCurrentCounter(time, period);
        return HotpGenerator.GenerateHotp(secret, counter, digits);
    }

    public static int GetRemainingSeconds(int period, DateTime? utcNow = null)
    {
        var time = utcNow ?? DateTime.UtcNow;
        var seconds = (int)(time - DateTime.UnixEpoch).TotalSeconds;
        var elapsed = seconds % period;
        return period - elapsed;
    }

    public static long GetCurrentCounter(DateTime utcNow, int period)
    {
        var seconds = (long)(utcNow - DateTime.UnixEpoch).TotalSeconds;
        return seconds / period;
    }
}
