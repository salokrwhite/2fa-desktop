using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Views.Dialogs;

public partial class AccountDetailDialog : Window
{
    public AccountDetailDialog()
    {
        InitializeComponent();
    }

    public AccountDetailDialog(Account account)
    {
        InitializeComponent();
        DataContext = account;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
