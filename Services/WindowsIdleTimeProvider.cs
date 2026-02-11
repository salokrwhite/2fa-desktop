using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TwoFactorAuth.Services;

public sealed class WindowsIdleTimeProvider : IIdleTimeProvider
{
    public bool IsSupported => true;

    public TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var currentTick = (uint)Environment.TickCount64;
        var idleMs = currentTick - info.dwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}

