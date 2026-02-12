using System;
using TwoFactorAuth.Services;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.Services;

public sealed class OtpService : IOtpService
{
    private readonly ITimeService _timeService;

    public OtpService(ITimeService timeService)
    {
        _timeService = timeService;
    }

    public string GenerateTotp(string secret, int digits = 6, int period = 30)
    {
        return TotpGenerator.GenerateTotp(secret, digits, period, _timeService.UtcNow);
    }

    public string GenerateHotp(string secret, long counter, int digits = 6)
    {
        return HotpGenerator.GenerateHotp(secret, counter, digits);
    }

    public int GetRemainingSeconds(int period)
    {
        return TotpGenerator.GetRemainingSeconds(period, _timeService.UtcNow);
    }

    public (string otp, int remainingSeconds) GetTotpWithRemaining(string secret, int digits = 6, int period = 30)
    {
        var otp = GenerateTotp(secret, digits, period);
        var remaining = GetRemainingSeconds(period);
        return (otp, remaining);
    }

    public bool VerifyOtp(string secret, string otp, int digits = 6, int period = 30, int tolerance = 1)
    {
        var now = _timeService.UtcNow;
        var counter = TotpGenerator.GetCurrentCounter(now, period);
        for (var i = -tolerance; i <= tolerance; i++)
        {
            var candidate = HotpGenerator.GenerateHotp(secret, counter + i, digits);
            if (candidate == otp)
            {
                return true;
            }
        }
        return false;
    }
}
