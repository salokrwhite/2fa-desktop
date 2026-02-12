using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace TwoFactorAuth.Services;

public enum ScreenshotProtectionApplyStatus
{
    Applied = 0,
    NotSupported = 1,
    Failed = 2
}

public sealed class ScreenshotProtectionApplyResult
{
    public ScreenshotProtectionApplyStatus Status { get; }
    public int Win32Error { get; }
    public string? Message { get; }

    public ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus status, int win32Error = 0, string? message = null)
    {
        Status = status;
        Win32Error = win32Error;
        Message = message;
    }
}

public interface IScreenshotProtectionService
{
    ScreenshotProtectionApplyResult ApplyTo(Window window, bool enabled);
}

public sealed class ScreenshotProtectionService : IScreenshotProtectionService
{
    private const uint WdaNone = 0x0;
    private const uint WdaExcludeFromCapture = 0x11;

    public ScreenshotProtectionApplyResult ApplyTo(Window window, bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus.NotSupported, message: "Non-Windows");
        }

        if (!enabled)
        {
            return TrySetDisplayAffinity(window, WdaNone);
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return new ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus.NotSupported, message: "Requires Windows 10 2004+");
        }

        return TrySetDisplayAffinity(window, WdaExcludeFromCapture);
    }

    private static ScreenshotProtectionApplyResult TrySetDisplayAffinity(Window window, uint affinity)
    {
        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle == null || platformHandle.Handle == IntPtr.Zero)
        {
            return new ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus.Failed, message: "Window handle not available");
        }

        if (!SetWindowDisplayAffinity(platformHandle.Handle, affinity))
        {
            var error = Marshal.GetLastWin32Error();
            return new ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus.Failed, win32Error: error, message: "SetWindowDisplayAffinity failed");
        }

        return new ScreenshotProtectionApplyResult(ScreenshotProtectionApplyStatus.Applied);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}
