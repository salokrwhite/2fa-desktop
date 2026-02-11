using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class ExportBackupDialog : Window
{
    public ExportBackupDialog()
    {
        InitializeComponent();
        DataContext = new ExportBackupDialogViewModel();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var password = this.FindControl<TextBox>("PasswordBox")?.Text ?? string.Empty;
        var confirmPassword = this.FindControl<TextBox>("ConfirmPasswordBox")?.Text ?? string.Empty;

        if (DataContext is ExportBackupDialogViewModel vm)
        {
            vm.ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(password))
            {
                vm.ErrorMessage = GetLang("Lang.Backup.PasswordEmpty", "备份密码不能为空");
                return;
            }

            if (password.Length < 8)
            {
                vm.ErrorMessage = GetLang("Lang.Backup.PasswordTooShort", "密码至少需要8个字符");
                return;
            }

            if (!string.Equals(password, confirmPassword, System.StringComparison.Ordinal))
            {
                vm.ErrorMessage = GetLang("Lang.Backup.PasswordMismatch", "两次输入的密码不一致");
                return;
            }
            var result = new ExportBackupResult
            {
                Password = password,
                IncludeSettings = vm.IncludeSettings,
                IncludeLogs = vm.IncludeLogs
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

public sealed class ExportBackupResult
{
    public string Password { get; set; } = string.Empty;
    public bool IncludeSettings { get; set; }
    public bool IncludeLogs { get; set; }
}
