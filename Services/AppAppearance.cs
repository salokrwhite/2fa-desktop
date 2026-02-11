using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace TwoFactorAuth.Services;

public static class AppAppearance
{
    public static void ApplyTheme(string theme)
    {
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ResourceInclude))]
    [UnconditionalSuppressMessage("Aot", "IL2026", Justification = "ResourceInclude is preserved via DynamicDependency")]
    public static void ApplyLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return;

        var app = Application.Current;
        if (app == null) return;

        var uri = new Uri($"avares://TwoFactorAuthDesktop/Assets/Lang/{lang}.axaml");
        if (app.Resources is ResourceDictionary dict)
        {
            dict.MergedDictionaries.Clear();
            dict.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });
        }
    }
}
