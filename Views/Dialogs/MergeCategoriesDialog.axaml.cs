using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.ViewModels;

namespace TwoFactorAuth.Views.Dialogs;

public partial class MergeCategoriesDialog : Window
{
    private readonly List<CategoryItemViewModel> _selectedCategories;

    public MergeCategoriesDialog()
    {
        InitializeComponent();
        _selectedCategories = new List<CategoryItemViewModel>();
    }

    public MergeCategoriesDialog(List<CategoryItemViewModel> selectedCategories) : this()
    {
        _selectedCategories = selectedCategories;
        SelectedCategoriesControl.ItemsSource = selectedCategories;
        TargetCategoryCombo.ItemsSource = selectedCategories;
        
        if (selectedCategories.Count > 0)
        {
            TargetCategoryCombo.SelectedIndex = 0;
        }

        TargetCategoryCombo.SelectionChanged += OnTargetChanged;
        UpdatePreview();
    }

    private void OnTargetChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (TargetCategoryCombo.SelectedItem is not CategoryItemViewModel target)
        {
            PreviewText.Text = GetResource("Lang.Category.MergeSelectTarget");
            return;
        }

        var totalAccounts = _selectedCategories.Sum(c => c.Count);
        var categoriesToDelete = _selectedCategories.Where(c => c != target).ToList();
        
        var previewFormat = GetResource("Lang.Category.MergePreviewFormat");
        PreviewText.Text = string.Format(previewFormat, 
            target.Name, 
            totalAccounts, 
            categoriesToDelete.Count);
    }

    private string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnMergeClick(object? sender, RoutedEventArgs e)
    {
        if (TargetCategoryCombo.SelectedItem is CategoryItemViewModel target)
        {
            Close(target);
        }
    }
}
