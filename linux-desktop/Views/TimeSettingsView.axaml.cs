using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views;

public partial class TimeSettingsView : UserControl
{
    public TimeSettingsView()
    {
        InitializeComponent();
    }

    private async void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TimeSettingsViewModel vm)
        {
            await vm.TestNtpConnectionAsync();
        }
    }

    private async void OnAddCustomServerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TimeSettingsViewModel vm)
        {
            await vm.AddCustomServerAsync();
        }
    }
}
