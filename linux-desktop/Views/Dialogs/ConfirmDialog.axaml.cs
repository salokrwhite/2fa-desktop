using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TwoFactorAuth.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        DataContext = new ConfirmDialogViewModel
        {
            Title = title,
            Message = message
        };
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}

public class ConfirmDialogViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
