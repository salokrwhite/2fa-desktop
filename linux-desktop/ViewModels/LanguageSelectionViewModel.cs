using System;
using System.Collections.ObjectModel;

namespace TwoFactorAuth.ViewModels;

public class LanguageSelectionViewModel : ViewModelBase
{
    private readonly Action<string>? _onLanguageChanged;
    private string _selectedLanguage;

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new()
    {
        new LanguageItem("zh-CN", "简体中文 (Chinese - Simplified)"),
        new LanguageItem("en-US", "English (US)"),
        new LanguageItem("ja-JP", "日本語 (Japanese)"),
        new LanguageItem("ko-KR", "한국어 (Korean)"),
        new LanguageItem("es-ES", "Español (Spanish)"),
        new LanguageItem("fr-FR", "Français (French)"),
        new LanguageItem("de-DE", "Deutsch (German)"),
        new LanguageItem("ru-RU", "Русский (Russian)")
    };

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!SetField(ref _selectedLanguage, value)) return;
            _onLanguageChanged?.Invoke(_selectedLanguage);
        }
    }

    public LanguageSelectionViewModel(string currentLanguage, Action<string>? onLanguageChanged = null)
    {
        _onLanguageChanged = onLanguageChanged;
        _selectedLanguage = currentLanguage;
    }
}

public class LanguageItem
{
    public string Code { get; }
    public string Name { get; }

    public LanguageItem(string code, string name)
    {
        Code = code;
        Name = name;
    }
}
