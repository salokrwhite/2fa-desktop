using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class ThemeSelectionDialog : Window
{
    public ThemeSelectionDialog()
    {
        InitializeComponent();
    }

    public ThemeSelectionDialog(string currentTheme)
    {
        InitializeComponent();
        DataContext = new ThemeSelectionViewModel(currentTheme);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ThemeSelectionViewModel vm)
        {
            Close(vm.SelectedTheme);
        }
        else
        {
            Close(null);
        }
    }
}

