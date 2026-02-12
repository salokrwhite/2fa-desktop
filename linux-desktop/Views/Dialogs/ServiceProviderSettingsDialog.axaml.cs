using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TwoFactorAuth.Utils;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public class ServiceProviderSettingsResult
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }
    public string? IconColor { get; set; }
}

public partial class ServiceProviderSettingsDialog : Window
{
    private readonly List<IconOption> _icons;

    public ServiceProviderSettingsDialog()
    {
        InitializeComponent();
        _icons = new List<IconOption>();
    }

    public ServiceProviderSettingsDialog(string name, string? description, string? iconPath, string? iconColor, List<IconOption> availableIcons)
    {
        InitializeComponent();
        _icons = availableIcons;

        NameBox.Text = name;
        DescriptionBox.Text = description ?? string.Empty;

        IconComboBox.ItemsSource = _icons;
        IconComboBox.SelectionChanged += OnIconSelectionChanged;

        if (!string.IsNullOrEmpty(iconPath))
        {
            var svgContent = SvgImageHelper.IsFullSvg(iconPath)
                ? iconPath
                : SvgImageHelper.WrapPathDataAsSvg(iconPath, iconColor);

            var currentIcon = new IconOption(GetResource("Lang.ServiceProvider.CurrentIcon"), svgContent, iconColor);
            _icons.Insert(0, currentIcon);
            IconComboBox.SelectedIndex = 0;
        }
        else if (_icons.Count > 0)
        {
            IconComboBox.SelectedIndex = 0;
        }
    }

    private string GetResource(string key)
    {
        if (Avalonia.Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
        return key;
    }

    private async void OnIconSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = IconComboBox.SelectedItem as IconOption;
        if (selected?.IsUploadOption == true)
        {
            await UploadSvgAsync();
        }
    }

    private async Task UploadSvgAsync()
    {
        var storageProvider = StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = GetResource("Lang.ServiceProvider.SelectSvg"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(GetResource("Lang.ServiceProvider.SvgFiles"))
                {
                    Patterns = new[] { "*.svg" }
                }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            var path = file.Path.LocalPath;

            if (SvgParser.IsValidSvg(path))
            {
                var svgContent = File.ReadAllText(path);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    var uploadedIcon = new IconOption(GetResource("Lang.ServiceProvider.UploadedIcon"), svgContent);
                    var existingUploaded = _icons.FirstOrDefault(i => i.DisplayName == GetResource("Lang.ServiceProvider.UploadedIcon"));
                    if (existingUploaded != null)
                        _icons.Remove(existingUploaded);

                    _icons.Insert(0, uploadedIcon);
                    IconComboBox.SelectedIndex = 0;
                    return;
                }
            }
            IconComboBox.SelectedIndex = _icons.FindIndex(i => i.SvgContent == null && !i.IsUploadOption);
        }
        else
        {
            IconComboBox.SelectedIndex = _icons.FindIndex(i => i.SvgContent == null && !i.IsUploadOption);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Close(null);
            return;
        }

        var selectedIcon = IconComboBox.SelectedItem as IconOption;

        Close(new ServiceProviderSettingsResult
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            IconPath = selectedIcon?.SvgContent,
            IconColor = selectedIcon?.Color
        });
    }
}
