using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TwoFactorAuth.Views.Dialogs;

public partial class CategorySettingsDialog : Window
{
    public CategorySettingsDialog()
    {
        InitializeComponent();
    }

    public CategorySettingsDialog(string currentName, string currentDescription) : this()
    {
        DataContext = new CategorySettingsDialogViewModel
        {
            CategoryName = currentName,
            OriginalName = currentName,
            Description = currentDescription,
            OriginalDescription = currentDescription
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (this.FindControl<TextBox>("NameTextBox") is { } textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CategorySettingsDialogViewModel vm)
        {
            var newName = vm.CategoryName?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(newName))
            {
                Close(new CategorySettingsResult
                {
                    Name = newName,
                    Description = vm.Description?.Trim() ?? string.Empty
                });
            }
        }
    }
}

public class CategorySettingsDialogViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OriginalDescription { get; set; } = string.Empty;
}

public class CategorySettingsResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
