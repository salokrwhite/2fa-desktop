using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class LanguageSelectionDialog : Window
{
    public LanguageSelectionDialog()
    {
        InitializeComponent();
        SetupWindowClipping();
    }

    public LanguageSelectionDialog(string currentLanguage)
    {
        InitializeComponent();
        DataContext = new LanguageSelectionViewModel(currentLanguage);
        SetupWindowClipping();
    }

    private void SetupWindowClipping()
    {
        var geometry = new RectangleGeometry
        {
            Rect = new Avalonia.Rect(0, 0, Width, Height),
            RadiusX = 12,
            RadiusY = 12
        };
        Clip = geometry;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LanguageSelectionViewModel vm)
        {
            Close(vm.SelectedLanguage);
        }
        else
        {
            Close(null);
        }
    }
}