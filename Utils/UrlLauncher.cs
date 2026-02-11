using System;
using System.Diagnostics;

namespace TwoFactorAuth.Utils;

public static class UrlLauncher
{
    public static bool TryOpen(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"")
                    {
                        CreateNoWindow = true
                    });
                    return true;
                }

                if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", url);
                    return true;
                }

                if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", url);
                    return true;
                }
            }
            catch
            {
                
            }

            return false;
        }
    }
}

