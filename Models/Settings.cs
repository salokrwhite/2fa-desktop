namespace TwoFactorAuth.Models;

public class Settings : ObservableObject
{
    private string _theme = "Light";
    private string _language = "zh-CN";
    private int _refreshPeriod = 30;
    private int _autoLockMinutes = 5;

    public string Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public int RefreshPeriod
    {
        get => _refreshPeriod;
        set => SetField(ref _refreshPeriod, value);
    }

    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set => SetField(ref _autoLockMinutes, value);
    }
}
