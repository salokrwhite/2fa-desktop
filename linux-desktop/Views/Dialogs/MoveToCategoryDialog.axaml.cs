using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using TwoFactorAuth.Models;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class MoveToCategoryDialog : Window
{
    public MoveToCategoryDialog()
    {
        InitializeComponent();
    }

    public MoveToCategoryDialog(List<Category> categories)
    {
        InitializeComponent();
        var vm = new MoveToCategoryViewModel();
        vm.InitCategories(categories);
        DataContext = vm;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MoveToCategoryViewModel vm)
        {
            Close(null);
            return;
        }

        Close(vm.SelectedCategory);
    }
}
