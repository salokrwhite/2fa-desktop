namespace TwoFactorAuth.Data;

public static class SettingKeys
{
    public const string BlockScreenshots = "Security.BlockScreenshots";
    public const string AppLockEnabled = "Security.AppLockEnabled";
    public const string IdleAutoLockEnabled = "Security.IdleAutoLockEnabled";
    public const string AutoLockMinutes = "AutoLockMinutes";
    public const string MasterPasswordHash = "MasterPasswordHash";
    public const string ClipboardClearEnabled = "Security.ClipboardClearEnabled";
    public const string ClipboardClearDelaySeconds = "Security.ClipboardClearDelaySeconds";
    public const string CategoryManualSortMode = "Category.ManualSortMode";
    public const string TimeSource = "Time.Source";
    public const string NtpServer = "Time.NtpServer";
    public const string CustomNtpServers = "Time.CustomNtpServers";
    public const string ShowRawLog = "Logs.ShowRawLog";
}
