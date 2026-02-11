using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TwoFactorAuth.Models;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class UnifiedAddAccountDialog : Window
{
    public UnifiedAddAccountDialog()
    {
        InitializeComponent();
    }

    public UnifiedAddAccountDialog(
        List<Category> categories, 
        List<ServiceProvider> serviceProviders,
        List<Account> existingAccounts)
    {
        InitializeComponent();
        var vm = new UnifiedAddAccountViewModel(categories, serviceProviders, existingAccounts);
        DataContext = vm;
    }

    private UnifiedAddAccountViewModel? ViewModel => DataContext as UnifiedAddAccountViewModel;

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null)
        {
            Close(null);
            return;
        }

        List<Account>? accounts = null;

        switch (vm.SelectedTabIndex)
        {
            case 0: 
                var account = vm.BuildAccountFromManualInput();
                if (account != null)
                {
                    accounts = new List<Account> { account };
                }
                break;

            case 1: 
                if (vm.IsUrlValid)
                {
                    account = vm.BuildAccountFromManualInput();
                    if (account != null)
                    {
                        accounts = new List<Account> { account };
                    }
                }
                break;

            case 2: 
                accounts = vm.BuildAccountsFromQrImport();
                break;
        }

        if (accounts == null || accounts.Count == 0)
        {
            return;
        }

        Close(accounts);
    }

    private async void OnSelectQrImagesClick(object? sender, RoutedEventArgs e)
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

        await vm.AddQrImagesAsync(sources);
    }

    private void OnClearQrClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearQrImages();
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
