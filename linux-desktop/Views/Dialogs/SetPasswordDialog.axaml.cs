using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class SetPasswordDialog : Window
{
    public SetPasswordDialog()
    {
        InitializeComponent();
        DataContext = new SetPasswordDialogViewModel();
        if (DataContext is SetPasswordDialogViewModel vm)
        {
            vm.Title = GetLang("Lang.Security.SetMasterPasswordTitle", vm.Title);
            vm.NewPasswordPrompt = GetLang("Lang.Security.NewPasswordPrompt", vm.NewPasswordPrompt);
            vm.ConfirmPasswordPrompt = GetLang("Lang.Security.ConfirmPasswordPrompt", vm.ConfirmPasswordPrompt);
            vm.CancelText = GetLang("Lang.Cancel", vm.CancelText);
            vm.ConfirmText = GetLang("Lang.Save", vm.ConfirmText);
        }
    }

    public SetPasswordDialog(System.Action<SetPasswordDialogViewModel>? configure) : this()
    {
        if (DataContext is SetPasswordDialogViewModel vm)
        {
            configure?.Invoke(vm);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var newPassword = this.FindControl<TextBox>("NewPasswordBox")?.Text ?? string.Empty;
        var confirmPassword = this.FindControl<TextBox>("ConfirmPasswordBox")?.Text ?? string.Empty;

        if (DataContext is SetPasswordDialogViewModel vm)
        {
            vm.ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                vm.ErrorMessage = GetLang("Lang.Security.PasswordEmpty", "密码不能为空");
                return;
            }

            if (!string.Equals(newPassword, confirmPassword, System.StringComparison.Ordinal))
            {
                vm.ErrorMessage = GetLang("Lang.Security.PasswordMismatch", "两次输入的密码不一致");
                return;
            }
        }

        Close(newPassword);
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
