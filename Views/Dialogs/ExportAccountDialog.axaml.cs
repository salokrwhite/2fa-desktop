using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TwoFactorAuth.Models;
using TwoFactorAuth.Utils;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class ExportAccountDialog : Window
{
    private readonly ExportAccountViewModel _viewModel;

    public ExportAccountDialog()
    {
        InitializeComponent();
        _viewModel = new ExportAccountViewModel(new List<Account>());
    }

    public ExportAccountDialog(List<Account> accounts) : this()
    {
        _viewModel = new ExportAccountViewModel(accounts);
        DataContext = _viewModel;
        if (accounts.Count == 1)
        {
            _viewModel.ExportMode = ExportMode.Single;
        }
        else
        {
            _viewModel.ExportMode = ExportMode.Batch;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var selectedAccounts = _viewModel.GetSelectedAccounts();
        
        if (selectedAccounts.Count == 0)
        {
            await ShowMessageAsync(
                GetString("Lang.Export.Title"),
                GetString("Lang.Export.NoAccountSelected")
            );
            return;
        }

        _viewModel.IsBusy = true;
        _viewModel.StatusMessage = GetString("Lang.Export.Generating");

        try
        {
            if (_viewModel.SelectedFormat == ExportFormat.QrCode)
            {
                await ExportAsQrCodeAsync(selectedAccounts);
            }
            else
            {
                await ExportAsUrlAsync(selectedAccounts);
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                GetString("Lang.Export.Failed"),
                ex.Message
            );
        }
        finally
        {
            _viewModel.IsBusy = false;
            _viewModel.StatusMessage = null;
        }
    }

    private async Task ExportAsQrCodeAsync(List<Account> accounts)
    {
        if (accounts.Count == 1)
        {
            var account = accounts[0];
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = GetString("Lang.Export.SaveQrCode"),
                SuggestedFileName = $"{SanitizeFileName(account.Name)}_QR.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(GetString("Lang.Export.PngImage"))
                    {
                        Patterns = new[] { "*.png" }
                    }
                }
            });

            if (file != null)
            {
                var otpUrl = OtpUrlGenerator.Generate(account);
                var qrBitmap = QrCodeGenerator.Generate(otpUrl, 512, 512);
                
                await using var stream = await file.OpenWriteAsync();
                qrBitmap.Save(stream);

                _viewModel.StatusMessage = GetString("Lang.Export.Success");
                await Task.Delay(1500);
                Close(true);
            }
        }
        else
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = GetString("Lang.Export.SelectFolder"),
                AllowMultiple = false
            });

            if (folder != null && folder.Count > 0)
            {
                var targetFolder = folder[0];
                int successCount = 0;

                foreach (var account in accounts)
                {
                    try
                    {
                        var fileName = $"{SanitizeFileName(account.Name)}_QR.png";
                        var filePath = Path.Combine(targetFolder.Path.LocalPath, fileName);
                        
                        var otpUrl = OtpUrlGenerator.Generate(account);
                        var qrBitmap = QrCodeGenerator.Generate(otpUrl, 512, 512);
                        
                        using var fileStream = File.Create(filePath);
                        qrBitmap.Save(fileStream);
                        
                        successCount++;
                    }
                    catch
                    {
                        
                    }
                }

                await ShowMessageAsync(
                    GetString("Lang.Export.Title"),
                    string.Format(GetString("Lang.Export.BatchSuccess"), successCount, accounts.Count)
                );
                
                Close(true);
            }
        }
    }

    private async Task ExportAsUrlAsync(List<Account> accounts)
    {
        if (accounts.Count != 1)
            return;

        var account = accounts[0];
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = GetString("Lang.Export.SaveUrl"),
            SuggestedFileName = $"{SanitizeFileName(account.Name)}_OTP.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(GetString("Lang.Export.TextFile"))
                {
                    Patterns = new[] { "*.txt" }
                }
            }
        });

        if (file != null)
        {
            var otpUrl = OtpUrlGenerator.Generate(account);
            
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(otpUrl);

            _viewModel.StatusMessage = GetString("Lang.Export.Success");
            await Task.Delay(1500);
            Close(true);
        }
    }

    private void OnQrCodeFormatClick(object? sender, PointerPressedEventArgs e)
    {
        _viewModel.SelectedFormat = ExportFormat.QrCode;
    }

    private void OnUrlFormatClick(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel.CanSelectFormat)
        {
            _viewModel.SelectedFormat = ExportFormat.Url;
        }
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _viewModel.Accounts)
        {
            item.IsSelected = true;
        }
        _viewModel.NotifySelectionChanged();
    }

    private void OnDeselectAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _viewModel.Accounts)
        {
            item.IsSelected = false;
        }
        _viewModel.NotifySelectionChanged();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var button = new Button
        {
            Content = GetString("Lang.Confirm"),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        button.Click += (s, e) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                button
            }
        };

        await dialog.ShowDialog(this);
    }

    private static string GetString(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var resource) == true && resource is string text)
        {
            return text;
        }
        return key;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
