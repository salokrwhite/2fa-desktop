using System;
using System.Web;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Utils;

public static class OtpUrlGenerator
{
    public static string Generate(Account account)
    {
        var type = account.Type == OtpType.Totp ? "totp" : "hotp";
        var label = HttpUtility.UrlEncode($"{account.Issuer}:{account.Name}");
        
        var url = $"otpauth://{type}/{label}?secret={account.Secret}";
        
        if (!string.IsNullOrEmpty(account.Issuer))
        {
            url += $"&issuer={HttpUtility.UrlEncode(account.Issuer)}";
        }
        
        if (account.Digits != 6)
        {
            url += $"&digits={account.Digits}";
        }
        
        if (account.Type == OtpType.Totp && account.Period != 30)
        {
            url += $"&period={account.Period}";
        }
        
        if (account.Type == OtpType.Hotp)
        {
            url += $"&counter={account.Counter}";
        }
        
        return url;
    }
}
