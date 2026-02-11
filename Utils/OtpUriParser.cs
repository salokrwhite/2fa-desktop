using System;
using System.Collections.Generic;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Utils;

public static class OtpUriParser
{
    public static Account Parse(string uri)
    {
        var parsed = new Uri(uri);
        var type = parsed.Host.Equals("totp", StringComparison.OrdinalIgnoreCase) ? OtpType.Totp : OtpType.Hotp;
        var label = Uri.UnescapeDataString(parsed.AbsolutePath.Trim('/'));
        string issuerFromLabel = string.Empty;
        string name = label;
        if (label.Contains(':'))
        {
            var parts = label.Split(':', 2);
            issuerFromLabel = parts[0];
            name = parts[1];
        }

        var queryParams = ParseQuery(parsed.Query);
        var secret = queryParams.TryGetValue("secret", out var s) ? s : string.Empty;
        var issuer = queryParams.TryGetValue("issuer", out var i) ? i : issuerFromLabel;
        var digits = queryParams.TryGetValue("digits", out var ds) && int.TryParse(ds, out var d) ? d : 6;
        var period = queryParams.TryGetValue("period", out var ps) && int.TryParse(ps, out var p) ? p : 30;
        var counter = queryParams.TryGetValue("counter", out var cs) && int.TryParse(cs, out var c) ? c : 0;

        return new Account
        {
            Name = name,
            Issuer = issuer,
            Secret = secret,
            Digits = digits,
            Period = period,
            Counter = counter,
            Type = type
        };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var q = query.TrimStart('?');
        if (string.IsNullOrEmpty(q)) return dict;
        var parts = q.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : string.Empty);
            dict[key] = value;
        }
        return dict;
    }
}
