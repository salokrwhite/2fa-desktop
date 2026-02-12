using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TwoFactorAuth.Models;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class QrImportDialog : Window
{
    public QrImportDialog()
    {
        InitializeComponent();
        DataContext = new QrImportViewModel(Array.Empty<Category>(), null, Array.Empty<Account>());
    }

    public QrImportDialog(IReadOnlyList<Category> categories, Category? selectedCategory, IReadOnlyList<Account> existingAccounts)
    {
        InitializeComponent();
        DataContext = new QrImportViewModel(categories, selectedCategory, existingAccounts);
    }

    private QrImportViewModel? ViewModel => DataContext as QrImportViewModel;

    private async void OnSelectImagesClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = GetResourceString("Lang.QrImport.SelectImages"),
            FileTypeFilter = new[]
            {
                new FilePickerFileType(GetResourceString("Lang.QrImport.FileType.Images"))
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" }
                }
            }
        });

        if (files.Count == 0) return;

        var sources = files.Select(f => new QrImportImageSource(
            f.Name,
            () => f.OpenReadAsync()
        ));

        await vm.AddImagesAsync(sources);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Clear();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        var result = vm.BuildAccountsForImport();
        Close(result);
    }

    private void OnSelectAllPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !vm.CanSelectAll) return;

        vm.SelectAllState = vm.SelectAllState == true ? false : true;
        e.Handled = true;
    }

    private static string GetResourceString(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }
}
