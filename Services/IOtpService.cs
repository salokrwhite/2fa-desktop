using System;

namespace TwoFactorAuth.Services;

public interface IOtpService
{
    string GenerateTotp(string secret, int digits = 6, int period = 30);
    string GenerateHotp(string secret, long counter, int digits = 6);
    int GetRemainingSeconds(int period);
    (string otp, int remainingSeconds) GetTotpWithRemaining(string secret, int digits = 6, int period = 30);
    bool VerifyOtp(string secret, string otp, int digits = 6, int period = 30, int tolerance = 1);
}
