using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class CategoryDetailDialog : Window
{
    public CategoryDetailDialog()
    {
        InitializeComponent();
    }

    public CategoryDetailDialog(CategoryItemViewModel category) : this()
    {
        CategoryNameText.Text = category.Name;
        
        if (string.IsNullOrWhiteSpace(category.Description))
        {
            DescriptionText.Text = GetResource("Lang.Category.NoDescription");
            DescriptionText.Opacity = 0.5;
        }
        else
        {
            DescriptionText.Text = category.Description;
        }
        
        AccountCountText.Text = category.Count.ToString();
        SortOrderText.Text = (category.SortOrder + 1).ToString();
    }

    private string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
