using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.ViewModels;

public class ServiceProviderItemViewModel : ViewModelBase
{
    private readonly ServiceProviderListViewModel _parent;
    public ServiceProvider Provider { get; }
    private Bitmap? _iconImage;

    public ServiceProviderItemViewModel(ServiceProvider provider, int usageCount, ServiceProviderListViewModel parent)
    {
        Provider = provider;
        _usageCount = usageCount;
        _parent = parent;
    }

    public string Name => Provider.Name;
    public string? IconPath => Provider.IconPath;
    public string? IconColor => Provider.IconColor;
    public string? Description => Provider.Description;
    public bool IsBuiltIn => Provider.IsBuiltIn;
    public bool IsNotBuiltIn => !Provider.IsBuiltIn;

    private int _usageCount;
    public int UsageCount
    {
        get => _usageCount;
        set => SetField(ref _usageCount, value);
    }

    public bool HasIcon => !string.IsNullOrEmpty(IconPath);
    public Bitmap? IconImage
    {
        get
        {
            if (_iconImage == null && HasIcon)
            {
                var svgContent = GetSvgContent();
                _iconImage = SvgImageHelper.FromSvgString(svgContent, 64, 64);
            }
            return _iconImage;
        }
    }

    private string? GetSvgContent()
    {
        if (string.IsNullOrEmpty(IconPath)) return null;
        if (SvgImageHelper.IsFullSvg(IconPath)) return IconPath;
        return SvgImageHelper.WrapPathDataAsSvg(IconPath, IconColor);
    }

    public void OnLanguageChanged()
    {
        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(Description));
    }

    public void NotifyIconColorChanged()
    {
        _iconImage = null;
        RaisePropertyChanged(nameof(IconImage));
    }
}

public sealed class ServiceProviderListViewModel : ViewModelBase
{
    private readonly ServiceProviderRepository _repository;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly IAccountService _accountService;

    public ObservableCollection<ServiceProviderItemViewModel> Providers { get; } = new();
    private List<ServiceProviderItemViewModel> _allProviders = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                ApplyFilter();
        }
    }

    private string _newProviderName = string.Empty;
    public string NewProviderName
    {
        get => _newProviderName;
        set => SetField(ref _newProviderName, value);
    }

    public ObservableCollection<IconOption> AvailableIcons { get; } = new();

    private IconOption? _selectedIcon;
    public IconOption? SelectedIcon
    {
        get => _selectedIcon;
        set => SetField(ref _selectedIcon, value);
    }

    public ICommand SortByNameCommand { get; }
    public ICommand SortByUsageCommand { get; }

    public ServiceProviderListViewModel(
        ServiceProviderRepository repository,
        OperationLogRepository operationLogRepository,
        IAccountService accountService)
    {
        _repository = repository;
        _operationLogRepository = operationLogRepository;
        _accountService = accountService;

        SortByNameCommand = new RelayCommand(() => _ = SortByNameAsync());
        SortByUsageCommand = new RelayCommand(() => _ = SortByUsageAsync());

        LoadAvailableIcons();
    }

    public async Task LoadAsync()
    {
        var providers = await _repository.GetAllAsync();
        var accounts = await _accountService.GetAllAccountsAsync();

        _allProviders.Clear();
        foreach (var p in providers)
        {
            var usage = accounts.Count(a =>
                !string.IsNullOrEmpty(a.Issuer) &&
                a.Issuer.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            _allProviders.Add(new ServiceProviderItemViewModel(p, usage, this));
        }

        ApplyFilter();
    }

    private void LoadAvailableIcons()
    {
        AvailableIcons.Clear();
        AvailableIcons.Add(new IconOption(GetResource("Lang.ServiceProvider.NoIcon"), null));
        AvailableIcons.Add(new IconOption(GetResource("Lang.ServiceProvider.UploadSvg"), null, null, true));
    }

    private string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
        return key;
    }

    public async Task AddProviderAsync()
    {
        var name = NewProviderName?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_allProviders.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        string? iconPath = SelectedIcon?.IsUploadOption == true ? null : SelectedIcon?.SvgContent;
        string? iconColor = SelectedIcon?.IsUploadOption == true ? null : SelectedIcon?.Color;

        var provider = new ServiceProvider
        {
            Name = name,
            IconPath = iconPath,
            IconColor = iconColor,
            SortOrder = _allProviders.Count,
            IsBuiltIn = false
        };

        await _repository.AddAsync(provider);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.add_service_provider",
            Target = provider.Name
        });

        NewProviderName = string.Empty;
        SelectedIcon = null;
        await LoadAsync();
    }

    public async Task DeleteProviderAsync(ServiceProviderItemViewModel item)
    {
        await _repository.DeleteAsync(item.Provider.Id);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.delete_service_provider",
            Target = item.Name
        });

        _allProviders.Remove(item);
        ApplyFilter();
    }

    public async Task UpdateProviderAsync(ServiceProviderItemViewModel item, string newName, string? newDescription, string? newIconPath, string? newIconColor)
    {
        var trimmedName = newName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName)) return;

        if (_allProviders.Any(p => p != item && p.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            return;

        item.Provider.Name = trimmedName;
        item.Provider.Description = newDescription?.Trim();
        item.Provider.IconPath = newIconPath;
        item.Provider.IconColor = newIconColor;

        await _repository.UpdateAsync(item.Provider);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.update_service_provider",
            Target = trimmedName
        });

        await LoadAsync();
    }

    private async Task SortByNameAsync()
    {
        _allProviders = _allProviders.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        ApplyFilter();
    }

    private async Task SortByUsageAsync()
    {
        _allProviders = _allProviders.OrderByDescending(p => p.UsageCount).ThenBy(p => p.Name).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;

        IEnumerable<ServiceProviderItemViewModel> filtered;
        if (string.IsNullOrWhiteSpace(search))
            filtered = _allProviders;
        else
            filtered = _allProviders.Where(p =>
                p.Name.ToLowerInvariant().Contains(search) ||
                (!string.IsNullOrEmpty(p.Description) && p.Description.ToLowerInvariant().Contains(search)));

        Providers.Clear();
        foreach (var item in filtered)
            Providers.Add(item);
    }

    public void OnLanguageChanged()
    {
        foreach (var p in Providers)
            p.OnLanguageChanged();
    }

    public void RefreshIconColors()
    {
        SvgImageHelper.ClearCache();
        foreach (var p in Providers)
            p.NotifyIconColorChanged();
    }

    public async Task<List<ServiceProvider>> GetAllProvidersAsync()
    {
        return await _repository.GetAllAsync();
    }
}

public class IconOption
{
    public string DisplayName { get; }
    public string? SvgContent { get; }
    public string? Color { get; }
    public bool IsUploadOption { get; }
    private Bitmap? _iconImage;

    public IconOption(string displayName, string? svgContent, string? color = null, bool isUploadOption = false)
    {
        DisplayName = displayName;
        SvgContent = svgContent;
        Color = color;
        IsUploadOption = isUploadOption;
    }

    public Bitmap? IconImage
    {
        get
        {
            if (_iconImage == null && !string.IsNullOrEmpty(SvgContent))
                _iconImage = SvgImageHelper.FromSvgString(SvgContent, 40, 40);
            return _iconImage;
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(SvgContent);

    public override string ToString() => DisplayName;
}
