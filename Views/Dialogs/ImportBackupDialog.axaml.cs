using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class ImportBackupDialog : Window
{
    public ImportBackupDialog()
    {
        InitializeComponent();
        DataContext = new ImportBackupDialogViewModel();
    }

    public ImportBackupDialog(BackupFile backupFile) : this()
    {
        if (DataContext is ImportBackupDialogViewModel vm)
        {
            vm.BackupTimestamp = backupFile.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            vm.AccountCount = backupFile.Metadata.AccountCount.ToString();
            vm.CategoryCount = backupFile.Metadata.CategoryCount.ToString();
            vm.AppVersion = backupFile.AppVersion;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var password = this.FindControl<TextBox>("PasswordBox")?.Text ?? string.Empty;

        if (DataContext is ImportBackupDialogViewModel vm)
        {
            vm.ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(password))
            {
                vm.ErrorMessage = GetLang("Lang.Backup.PasswordEmpty", "备份密码不能为空");
                return;
            }
            var result = new ImportBackupResult
            {
                Password = password,
                Mode = vm.IsMergeMode ? ImportMode.Merge : ImportMode.Overwrite,
                ConflictStrategy = vm.ConflictStrategyIndex switch
                {
                    0 => ConflictStrategy.Skip,
                    1 => ConflictStrategy.Overwrite,
                    2 => ConflictStrategy.Rename,
                    _ => ConflictStrategy.Skip
                }
            };

            Close(result);
        }
    }

    private static string GetLang(string key, string fallback)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return fallback;
    }
}

public sealed class ImportBackupResult
{
    public string Password { get; set; } = string.Empty;
    public ImportMode Mode { get; set; }
    public ConflictStrategy ConflictStrategy { get; set; }
}
