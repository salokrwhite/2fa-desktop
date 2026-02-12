using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class PasswordDialog : Window
{
    private readonly Func<string, Task<bool>>? _validateAsync;

    public PasswordDialog()
    {
        InitializeComponent();
        DataContext = new PasswordDialogViewModel();
        if (DataContext is PasswordDialogViewModel vm)
        {
            vm.Title = GetLang("Lang.Security.PasswordDialogTitle", vm.Title);
            vm.Prompt = GetLang("Lang.Security.PasswordDialogPrompt", vm.Prompt);
            vm.CancelText = GetLang("Lang.Cancel", vm.CancelText);
            vm.ConfirmText = GetLang("Lang.Security.Unlock", vm.ConfirmText);
        }
    }

    public PasswordDialog(Func<string, Task<bool>> validateAsync, Action<PasswordDialogViewModel>? configure = null) : this()
    {
        _validateAsync = validateAsync;
        if (DataContext is PasswordDialogViewModel vm)
        {
            configure?.Invoke(vm);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnUnlockClick(object? sender, RoutedEventArgs e)
    {
        var passwordBox = this.FindControl<TextBox>("PasswordBox");
        var password = passwordBox?.Text ?? string.Empty;

        if (DataContext is PasswordDialogViewModel vm)
        {
            vm.ErrorMessage = string.Empty;
        }

        if (_validateAsync == null)
        {
            Close(password);
            return;
        }

        bool ok;
        try
        {
            ok = await _validateAsync(password);
        }
        catch
        {
            ok = false;
        }

        if (ok)
        {
            Close(password);
            return;
        }

        if (DataContext is PasswordDialogViewModel vm2)
        {
            vm2.ErrorMessage = GetLang("Lang.Security.PasswordIncorrect", "密码错误");
        }

        if (passwordBox != null)
        {
            passwordBox.Text = string.Empty;
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
