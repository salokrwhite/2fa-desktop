using System;
using System.Collections.ObjectModel;

namespace TwoFactorAuth.ViewModels;

public sealed class ThemeSelectionViewModel : ViewModelBase
{
    private readonly Action<string>? _onThemeChanged;
    private string _selectedTheme;

    public ObservableCollection<ThemeItem> AvailableThemes { get; } = new()
    {
        new ThemeItem("Light", "Lang.Theme.Light", "M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zm0 9c-2.21 0-4-1.79-4-4s1.79-4 4-4 4 1.79 4 4-1.79 4-4 4zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1zM5.99 4.58c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0s.39-1.03 0-1.41L5.99 4.58zm12.37 12.37c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0 .39-.39.39-1.03 0-1.41l-1.06-1.06zm1.06-10.96c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06zM7.05 18.36c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06z"),
        new ThemeItem("Dark", "Lang.Theme.Dark", "M12 3c-4.97 0-9 4.97-9 9s4.97 9 9 9 9-4.97 9-9c0-.46-.04-.92-.1-1.36-.98 1.37-2.58 2.26-4.4 2.26-2.98 0-5.4-2.42-5.4-5.4 0-1.81.89-3.42 2.26-4.4-.44-.06-.9-.1-1.36-.1z")
    };

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!SetField(ref _selectedTheme, value)) return;
            RaisePropertyChanged(nameof(IsLight));
            RaisePropertyChanged(nameof(IsDark));
            _onThemeChanged?.Invoke(_selectedTheme);
        }
    }

    public bool IsLight
    {
        get => SelectedTheme != "Dark";
        set
        {
            if (value) SelectedTheme = "Light";
        }
    }

    public bool IsDark
    {
        get => SelectedTheme == "Dark";
        set
        {
            if (value) SelectedTheme = "Dark";
        }
    }

    public ThemeSelectionViewModel(string currentTheme, Action<string>? onThemeChanged = null)
    {
        _onThemeChanged = onThemeChanged;
        _selectedTheme = currentTheme == "Dark" ? "Dark" : "Light";
    }
}

public class ThemeItem
{
    public string Id { get; }
    public string NameKey { get; }
    public string Icon { get; }

    public ThemeItem(string id, string nameKey, string icon)
    {
        Id = id;
        NameKey = nameKey;
        Icon = icon;
    }
}
