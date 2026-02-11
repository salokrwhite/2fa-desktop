using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public sealed class LockScreenViewModel : ViewModelBase
{
    private readonly IAppLockCoordinator _appLockCoordinator;
    
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    
    public string Password
    {
        get => _password;
        set 
        {
             if (SetField(ref _password, value))
             {
                 ErrorMessage = string.Empty;
             }
        }
    }
    
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }
    
    public ICommand UnlockCommand { get; }
    
    public LockScreenViewModel(IAppLockCoordinator appLockCoordinator)
    {
        _appLockCoordinator = appLockCoordinator;
        UnlockCommand = new RelayCommand(UnlockAsync);
    }
    
    private async Task UnlockAsync()
    {   
        var success = await _appLockCoordinator.UnlockAsync(Password);
        if (success)
        {
            Password = string.Empty;
            ErrorMessage = string.Empty;
        }
        else
        {
            ErrorMessage = GetResource("Lang.Security.PasswordIncorrect") ?? "Password Incorrect";
        }
    }
    
    private static string? GetResource(string key)
    {
         if (Avalonia.Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
         return null;
    }
}
