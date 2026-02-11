using System.Collections.Generic;
using System.Collections.ObjectModel;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.ViewModels;

public sealed class MoveToCategoryViewModel : ViewModelBase
{
    private Category? _selectedCategory;

    public ObservableCollection<Category> Categories { get; } = new();

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetField(ref _selectedCategory, value);
    }

    public void InitCategories(IEnumerable<Category> categories)
    {
        Categories.Clear();
        foreach (var c in categories) Categories.Add(c);
    }
}
