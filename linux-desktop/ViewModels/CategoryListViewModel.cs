using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public class CategoryAccountItemViewModel : ViewModelBase
{
    private readonly CategoryListViewModel _parent;
    public Account Account { get; }

    public CategoryAccountItemViewModel(Account account, CategoryListViewModel parent)
    {
        Account = account;
        _parent = parent;
    }

    public string DisplayName => string.IsNullOrEmpty(Account.Name) ? GetResource("Lang.NotSet") : Account.Name;
    public string DisplayIssuer => string.IsNullOrEmpty(Account.Issuer) ? GetResource("Lang.NotSet") : Account.Issuer;

    public bool IsFavorite
    {
        get => Account.IsFavorite;
        set
        {
            if (Account.IsFavorite != value)
            {
                Account.IsFavorite = value;
                RaisePropertyChanged();
                _ = UpdateFavoriteAsync();
            }
        }
    }

    private async Task UpdateFavoriteAsync()
    {
        await _parent.UpdateAccountAsync(Account);
    }

    private string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }

    public void OnLanguageChanged()
    {
        RaisePropertyChanged(nameof(DisplayName));
        RaisePropertyChanged(nameof(DisplayIssuer));
    }

    public void Delete()
    {
        _ = _parent.DeleteAccountAsync(this);
    }

    public void RemoveFromCategory()
    {
        _ = _parent.RemoveAccountFromCategoryAsync(this);
    }
}

public class CategoryItemViewModel : ViewModelBase
{
    private readonly CategoryListViewModel _parent;
    public Category Category { get; }
    public ObservableCollection<CategoryAccountItemViewModel> Accounts { get; } = new();
    public int Count => Accounts.Count;
    public string Name => Category.Name;
    public string Description => Category.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Category.Description);
    public int SortOrder => Category.SortOrder;

    public bool CanMoveUp => _parent.CanMoveUp(this);
    public bool CanMoveDown => _parent.CanMoveDown(this);

    private bool _isDragSource;
    public bool IsDragSource
    {
        get => _isDragSource;
        set
        {
            if (SetField(ref _isDragSource, value))
            {
                RaisePropertyChanged(nameof(DragOpacity));
            }
        }
    }

    private bool _isDropTarget;
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (SetField(ref _isDropTarget, value))
            {
                RaisePropertyChanged(nameof(DragOpacity));
            }
        }
    }

    public double DragOpacity => IsDragSource ? 0.4 : IsDropTarget ? 0.7 : 1.0;

    public CategoryItemViewModel(Category category, IEnumerable<Account> accounts, CategoryListViewModel parent)
    {
        Category = category;
        _parent = parent;
        foreach (var a in accounts) Accounts.Add(new CategoryAccountItemViewModel(a, parent));
    }

    public void RefreshMoveState()
    {
        RaisePropertyChanged(nameof(CanMoveUp));
        RaisePropertyChanged(nameof(CanMoveDown));
    }

    public void MoveUp()
    {
        _parent.MoveCategoryUp(this);
    }

    public void MoveDown()
    {
        _parent.MoveCategoryDown(this);
    }
}

public sealed class CategoryListViewModel : ViewModelBase
{
    private readonly CategoryRepository _repository;
    public readonly IAccountService _accountService;
    private readonly MainViewModel _mainViewModel;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly SettingsRepository _settingsRepository;
    
    public ObservableCollection<CategoryItemViewModel> Categories { get; } = new();
    private List<CategoryItemViewModel> _allCategories = new();
    public ObservableCollection<CategoryItemViewModel> SelectedCategories { get; } = new();

    private CategoryItemViewModel? _selectedCategory;
    public CategoryItemViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetField(ref _selectedCategory, value);
    }

    private bool _isMultiSelectMode;
    public bool IsMultiSelectMode
    {
        get => _isMultiSelectMode;
        set
        {
            if (SetField(ref _isMultiSelectMode, value))
            {
                if (!value)
                {
                    SelectedCategories.Clear();
                }
            }
        }
    }
    
    private string _newCategoryName = string.Empty;
    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetField(ref _newCategoryName, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    private bool _isManualSortMode;
    public bool IsManualSortMode
    {
        get => _isManualSortMode;
        set
        {
            if (!SetField(ref _isManualSortMode, value)) return;
            if (_suspendManualSortModePersistence) return;
            _ = _settingsRepository.SetValueAsync(SettingKeys.CategoryManualSortMode, value ? "true" : "false");
        }
    }
    private bool _suspendManualSortModePersistence;

    public ICommand SortByAccountCountCommand { get; }
    public ICommand SortByNameCommand { get; }
    public ICommand SaveSortOrderCommand { get; }
    public ICommand ToggleMultiSelectModeCommand { get; }
    public ICommand MergeCategoriesCommand { get; }

    public CategoryListViewModel(
        CategoryRepository repository,
        IAccountService accountService,
        MainViewModel mainViewModel,
        OperationLogRepository operationLogRepository,
        SettingsRepository settingsRepository)
    {
        _repository = repository;
        _accountService = accountService;
        _mainViewModel = mainViewModel;
        _operationLogRepository = operationLogRepository;
        _settingsRepository = settingsRepository;

        SortByAccountCountCommand = new RelayCommand(() => _ = SortByAccountCountAsync());
        SortByNameCommand = new RelayCommand(() => _ = SortByNameAsync());
        SaveSortOrderCommand = new RelayCommand(() => _ = SaveSortOrderAsync());
        ToggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode);
        MergeCategoriesCommand = new RelayCommand(() => _ = MergeCategoriesAsync(), () => SelectedCategories.Count >= 2);
    }

    private void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
    }

    public async Task LoadAsync()
    {
        var manualSortMode = ParseBool(await _settingsRepository.GetValueAsync(SettingKeys.CategoryManualSortMode));
        _suspendManualSortModePersistence = true;
        try
        {
            IsManualSortMode = manualSortMode;
        }
        finally
        {
            _suspendManualSortModePersistence = false;
        }

        var list = await _repository.GetAllAsync();
        var accounts = await _accountService.GetAllAccountsAsync();
        
        _allCategories.Clear();
        foreach (var item in list)
        {
            var itemAccounts = accounts.Where(a => a.Group == item.Name);
            _allCategories.Add(new CategoryItemViewModel(item, itemAccounts, this));
        }

        if (!IsManualSortMode)
        {
            await SortByAccountCountAsync(false);
        }
        else
        {
            ApplyFilter();
        }
    }

    public void OnLanguageChanged()
    {
        RaisePropertyChanged(nameof(IsMultiSelectMode));
        foreach (var category in Categories)
        {
            foreach (var account in category.Accounts)
            {
                account.OnLanguageChanged();
            }
        }
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (string.Equals(value, "1", StringComparison.Ordinal)) return true;
        return bool.TryParse(value, out var parsed) && parsed;
    }

    public async Task SortByAccountCountAsync(bool saveOrder = true)
    {
        var sorted = _allCategories.OrderByDescending(c => c.Count).ThenBy(c => c.Name).ToList();
        _allCategories = sorted;
        IsManualSortMode = false;
        ApplyFilter();
        
        if (saveOrder)
        {
            await SaveSortOrderAsync();
        }
    }

    public async Task SortByNameAsync()
    {
        var sorted = _allCategories.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _allCategories = sorted;
        IsManualSortMode = false;
        ApplyFilter();
        await SaveSortOrderAsync();
    }

    private void ApplyFilter()
    {
        var searchLower = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        
        IEnumerable<CategoryItemViewModel> filtered;
        if (string.IsNullOrWhiteSpace(searchLower))
        {
            filtered = _allCategories;
        }
        else
        {
            filtered = _allCategories.Where(c =>
            {
                if (c.Name.ToLowerInvariant().Contains(searchLower))
                    return true;
                if (!string.IsNullOrWhiteSpace(c.Description) && 
                    c.Description.ToLowerInvariant().Contains(searchLower))
                    return true;
                return c.Accounts.Any(a =>
                    (!string.IsNullOrWhiteSpace(a.Account.Name) && a.Account.Name.ToLowerInvariant().Contains(searchLower)) ||
                    (!string.IsNullOrWhiteSpace(a.Account.Issuer) && a.Account.Issuer.ToLowerInvariant().Contains(searchLower))
                );
            });
        }
        
        Categories.Clear();
        foreach (var item in filtered)
        {
            Categories.Add(item);
        }
        RefreshAllMoveStates();
    }

    private void ReorderCategories(List<CategoryItemViewModel> sortedList)
    {
        _allCategories = sortedList;
        ApplyFilter();
    }

    public bool CanMoveUp(CategoryItemViewModel vm)
    {
        var index = Categories.IndexOf(vm);
        return index > 0;
    }

    public bool CanMoveDown(CategoryItemViewModel vm)
    {
        var index = Categories.IndexOf(vm);
        return index >= 0 && index < Categories.Count - 1;
    }

    public void MoveCategoryUp(CategoryItemViewModel vm)
    {
        var index = _allCategories.IndexOf(vm);
        if (index <= 0) return;

        _allCategories.RemoveAt(index);
        _allCategories.Insert(index - 1, vm);
        IsManualSortMode = true;
        ApplyFilter();
        _ = SaveSortOrderAsync();
    }

    public void MoveCategoryDown(CategoryItemViewModel vm)
    {
        var index = _allCategories.IndexOf(vm);
        if (index < 0 || index >= _allCategories.Count - 1) return;

        _allCategories.RemoveAt(index);
        _allCategories.Insert(index + 1, vm);
        IsManualSortMode = true;
        ApplyFilter();
        _ = SaveSortOrderAsync();
    }

    public async Task HandleDropAsync(CategoryItemViewModel source, CategoryItemViewModel target)
    {
        if (source == target) return;

        var sourceIndex = _allCategories.IndexOf(source);
        var targetIndex = _allCategories.IndexOf(target);

        if (sourceIndex < 0 || targetIndex < 0) return;

        _allCategories[sourceIndex] = target;
        _allCategories[targetIndex] = source;
        
        IsManualSortMode = true;
        ApplyFilter();
        await SaveSortOrderAsync();
    }

    private void RefreshAllMoveStates()
    {
        foreach (var vm in Categories)
        {
            vm.RefreshMoveState();
        }
    }

    public async Task SaveSortOrderAsync()
    {
        for (int i = 0; i < _allCategories.Count; i++)
        {
            var vm = _allCategories[i];
            if (vm.Category.SortOrder != i)
            {
                vm.Category.SortOrder = i;
                await _repository.UpdateAsync(vm.Category);
            }
        }
    }

    public async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

        var category = new Category
        {
            Name = NewCategoryName.Trim(),
            SortOrder = _allCategories.Count
        };

        await _repository.AddAsync(category);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.add_category",
            Target = category.Name
        });
        var newItem = new CategoryItemViewModel(category, Enumerable.Empty<Account>(), this);
        _allCategories.Add(newItem);
        ApplyFilter();
        NewCategoryName = string.Empty;
    }

    public async Task DeleteCategoryAsync(CategoryItemViewModel vm)
    {
        await _repository.DeleteAsync(vm.Category.Id);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.delete_category",
            Target = vm.Category.Name
        });
        _allCategories.Remove(vm);
        ApplyFilter();
    }

    public async Task UpdateCategoryAsync(CategoryItemViewModel vm, string newName, string newDescription)
    {
        var trimmedName = newName?.Trim() ?? string.Empty;
        var trimmedDescription = newDescription?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(trimmedName))
            return;

        if (!trimmedName.Equals(vm.Category.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (Categories.Any(c => c != vm && c.Category.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
        }

        var oldName = vm.Category.Name;
        var nameChanged = oldName != trimmedName;
        var descriptionChanged = vm.Category.Description != trimmedDescription;
        
        if (!nameChanged && !descriptionChanged)
            return;

        vm.Category.Name = trimmedName;
        vm.Category.Description = trimmedDescription;
        
        await _repository.UpdateAsync(vm.Category);
        
        if (nameChanged)
        {
            await _operationLogRepository.AddAsync(new OperationLog
            {
                Operation = "op.update_category",
                Target = $"{oldName} -> {trimmedName}",
                Details = descriptionChanged ? "NameAndDescription" : "Name"
            });

            var accounts = await _accountService.GetAllAccountsAsync();
            foreach (var account in accounts.Where(a => a.Group == oldName))
            {
                account.Group = trimmedName;
                await _accountService.UpdateAccountAsync(account);
            }
        }
        else
        {
            await _operationLogRepository.AddAsync(new OperationLog
            {
                Operation = "op.update_category",
                Target = trimmedName,
                Details = "Description"
            });
        }

        await LoadAsync();
        
        await _mainViewModel.AccountListVM.LoadAsync();
    }

    [System.Obsolete("Use UpdateCategoryAsync instead")]
    public async Task RenameCategoryAsync(CategoryItemViewModel vm, string newName)
    {
        await UpdateCategoryAsync(vm, newName, vm.Category.Description);
    }

    public async Task UpdateAccountAsync(Account account)
    {
        await _accountService.UpdateAccountAsync(account);
        await _mainViewModel.AccountListVM.LoadAsync();
    }

    public async Task DeleteAccountAsync(CategoryAccountItemViewModel item)
    {
        await _accountService.DeleteAccountAsync(item.Account.Id);
        foreach (var cat in Categories)
        {
            if (cat.Accounts.Contains(item))
            {
                cat.Accounts.Remove(item);
            }
        }
        await _mainViewModel.AccountListVM.LoadAsync();
    }

    public async Task RemoveAccountFromCategoryAsync(CategoryAccountItemViewModel item)
    {
        item.Account.Group = null;
        await _accountService.UpdateAccountAsync(item.Account);
        foreach (var cat in Categories)
        {
            if (cat.Accounts.Contains(item))
            {
                cat.Accounts.Remove(item);
            }
        }
        await _mainViewModel.AccountListVM.LoadAsync();
    }

    public async Task MergeCategoriesAsync()
    {
        if (SelectedCategories.Count < 2)
            return;
    }

    public void ToggleCategorySelection(CategoryItemViewModel category)
    {
        if (SelectedCategories.Contains(category))
        {
            SelectedCategories.Remove(category);
        }
        else
        {
            SelectedCategories.Add(category);
        }
    }

    public async Task LogMergeOperationAsync(string sourceCategory, string targetCategory)
    {
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.merge_category",
            Target = $"{sourceCategory} -> {targetCategory}"
        });
    }
}
