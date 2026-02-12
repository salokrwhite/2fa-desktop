using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public sealed class AccountListViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly IOtpService _otpService;
    private readonly CategoryRepository _categoryRepository;
    private readonly DispatcherTimer _timer;
    private string _searchQuery = string.Empty;
    private AccountItemViewModel? _selectedAccount;
    private Category? _selectedCategory;
    private bool _isSelectionMode;

    public AccountListViewModel(IAccountService accountService, IOtpService otpService, CategoryRepository categoryRepository)
    {
        _accountService = accountService;
        _otpService = otpService;
        _categoryRepository = categoryRepository;
        _categoryRepository.CategoriesChanged += (s, e) => _ = LoadCategoriesAsync();
        Accounts = new ObservableCollection<AccountItemViewModel>();
        FilteredAccounts = new ObservableCollection<AccountItemViewModel>();
        PinnedAccounts = new ObservableCollection<AccountItemViewModel>();
        UnpinnedAccounts = new ObservableCollection<AccountItemViewModel>();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateOtps());
    }

    public ObservableCollection<AccountItemViewModel> Accounts { get; }

    public ObservableCollection<AccountItemViewModel> FilteredAccounts { get; }
    
    public ObservableCollection<AccountItemViewModel> PinnedAccounts { get; }
    public ObservableCollection<AccountItemViewModel> UnpinnedAccounts { get; }
    public ObservableCollection<Category> Categories { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    public AccountItemViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set => SetField(ref _selectedAccount, value);
    }

    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (SetField(ref _isSelectionMode, value))
            {
                if (!value)
                {
                    foreach (var account in Accounts)
                    {
                        account.IsSelected = false;
                    }
                }
            }
        }
    }

    public ObservableCollection<string> AllAccountNames { get; } = new ObservableCollection<string>();

    public void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
    }
    public void SelectAll()
    {
        foreach (var account in FilteredAccounts)
        {
            account.IsSelected = true;
        }
    }

    public void DeselectAll()
    {
        foreach (var account in Accounts)
        {
            account.IsSelected = false;
        }
    }

    public bool IsAllSelected => FilteredAccounts.Count > 0 && FilteredAccounts.All(a => a.IsSelected);

    public void ClearCategoryFilter()
    {
        SelectedCategory = null;
    }

    public async Task DeleteSelectedAsync()
    {
        var selected = Accounts.Where(x => x.IsSelected).ToList();
        foreach (var item in selected)
        {
            await DeleteAccountAsync(item);
        }
        IsSelectionMode = false;
    }

    public async Task PinSelectedAsync()
    {
        var selected = Accounts.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        bool newStatus = !selected.All(x => x.IsFavorite);

        foreach (var item in selected)
        {
            if (item.IsFavorite != newStatus)
            {
                item.IsFavorite = newStatus;
                await _accountService.UpdateAccountAsync(item.Account);
            }
        }
        ApplyFilter();
        IsSelectionMode = false;
    }

    public async Task MoveSelectedToCategoryAsync(Category targetCategory)
    {
        var selected = Accounts.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;

        foreach (var item in selected)
        {
            item.Account.Group = targetCategory.Name;
            await _accountService.UpdateAccountAsync(item.Account);
        }
        
        await LoadAsync();
        IsSelectionMode = false;
    }

    public async Task LoadAsync()
    {
        var list = await _accountService.GetAllAccountsAsync();
        Accounts.Clear();
        foreach (var a in list)
        {
            Accounts.Add(new AccountItemViewModel(a, this));
        }

        await LoadCategoriesAsync();

        UpdateAccountNames();
        ApplyFilter();
        UpdateOtps();
        _timer.Start();
    }

    private async Task LoadCategoriesAsync()
    {
        var cats = await _categoryRepository.GetAllAsync();
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateCategories(cats);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => UpdateCategories(cats));
        }
    }

    private void UpdateCategories(List<Category> cats)
    {
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        if (SelectedCategory != null && !cats.Any(c => c.Id == SelectedCategory.Id))
        {
            SelectedCategory = null;
        }
    }

    public async Task AddAccountAsync(Account account)
    {
        await _accountService.AddAccountAsync(account);
        Accounts.Add(new AccountItemViewModel(account, this));
        UpdateAccountNames();
        ApplyFilter();
    }

    public async Task AddAccountsAsync(IReadOnlyList<Account> accounts)
    {
        if (accounts.Count == 0) return;

        foreach (var account in accounts)
        {
            await _accountService.AddAccountAsync(account);
            Accounts.Add(new AccountItemViewModel(account, this));
        }

        UpdateAccountNames();
        ApplyFilter();
    }

    public async Task UpdateAccountAsync(Account account)
    {
        await _accountService.UpdateAccountAsync(account);
        var existing = Accounts.FirstOrDefault(x => x.Id == account.Id);
        if (existing != null)
        {
            existing.UpdateFrom(account);
        }
        UpdateAccountNames();
        ApplyFilter();
    }

    public async Task DeleteAccountAsync(AccountItemViewModel item)
    {
        await _accountService.DeleteAccountAsync(item.Id);
        Accounts.Remove(item);
        FilteredAccounts.Remove(item);
        PinnedAccounts.Remove(item);
        UnpinnedAccounts.Remove(item);
    }

    public async Task TogglePinAsync(AccountItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        await _accountService.UpdateAccountAsync(item.Account);
        ApplyFilter(); 
    }

    private void UpdateOtps()
    {
        foreach (var vm in Accounts)
        {
            if (vm.Type == OtpType.Totp)
            {
                var (otp, remain) = _otpService.GetTotpWithRemaining(vm.Account.Secret, vm.Digits, vm.Period);
                vm.Otp = otp;
                vm.RemainingSeconds = remain;
            }
            else
            {
                // HOTP logic if needed, usually event-based or manual
            }
        }
    }

    private void ApplyFilter()
    {
        FilteredAccounts.Clear();
        PinnedAccounts.Clear();
        UnpinnedAccounts.Clear();

        var query = SearchQuery.Trim();
        IEnumerable<AccountItemViewModel> result = Accounts;

        if (SelectedCategory != null)
        {
            result = result.Where(a => a.Account.Group == SelectedCategory.Name);
        }

        if (!string.IsNullOrEmpty(query))
        {
            result = result.Where(a => 
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        result = result.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Name);

        foreach (var a in result)
        {
            FilteredAccounts.Add(a);
            
            if (a.IsFavorite)
            {
                PinnedAccounts.Add(a);
            }
            else
            {
                UnpinnedAccounts.Add(a);
            }
        }
    }

    public void StopTimer()
    {
        _timer.Stop();
    }

    public void OnLanguageChanged()
    {
        RaisePropertyChanged(nameof(IsSelectionMode));
    }

    public void RefreshIconColors()
    {
        TwoFactorAuth.Utils.SvgImageHelper.ClearCache();
        foreach (var vm in Accounts)
        {
            vm.NotifyIconColorChanged();
        }
    }

    private void UpdateAccountNames()
    {
        AllAccountNames.Clear();
        var names = Accounts.Select(x => x.Name).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
        foreach (var name in names)
        {
            AllAccountNames.Add(name);
        }
    }
}
