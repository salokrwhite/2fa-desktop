using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public class NavigationItem : ObservableObject
{
    private string _name = string.Empty;
    public required string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }
    public required string Icon { get; set; }
    public required ViewModelBase ViewModel { get; set; }
}

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly IOtpService _otpService;
    private readonly SettingsRepository _settingsRepository;

    public AccountListViewModel AccountListVM { get; }
    public DashboardViewModel DashboardVM { get; }
    public CategoryListViewModel CategoryListVM { get; }
    public ServiceProviderListViewModel ServiceProviderListVM { get; }
    public OperationLogViewModel OperationLogVM { get; }
    public BackupViewModel BackupVM { get; }
    public TimeSettingsViewModel TimeSettingsVM { get; }
    public AboutViewModel AboutVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    private const string SunIconData = "M6.76 4.84l-1.8-1.79-1.41 1.41 1.79 1.79 1.42-1.41zM4 10.5H1v2h3v-2zm9-9.95h-2V3.5h2V.55zm7.45 3.91l-1.41-1.41-1.79 1.79 1.41 1.41 1.79-1.79zm-3.21 13.7l1.79 1.8 1.41-1.41-1.8-1.79-1.4 1.4zM20 10.5v2h3v-2h-3zm-8-5c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm-1 16.95h2V19.5h-2v2.95zm-7.45-3.91l1.41 1.41 1.79-1.8-1.41-1.41-1.79 1.8z";
    private const string MoonIconData = "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z";

    private string _themeIcon = MoonIconData;
    public string ThemeIcon
    {
        get => _themeIcon;
        set => SetField(ref _themeIcon, value);
    }

    private string _themeIconColor = "#666666";
    public string ThemeIconColor
    {
        get => _themeIconColor;
        set => SetField(ref _themeIconColor, value);
    }

    private bool _isSidebarExpanded = true;
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set => SetField(ref _isSidebarExpanded, value);
    }

    public void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    private NavigationItem? _selectedNavigationItem;
    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetField(ref _selectedNavigationItem, value))
            {
                RaisePropertyChanged(nameof(CurrentPage));
                if (value?.ViewModel is DashboardViewModel)
                    _ = DashboardVM.LoadAsync();
                else if (value?.ViewModel is AccountListViewModel)
                    _ = AccountListVM.LoadAsync();
                else if (value?.ViewModel is CategoryListViewModel)
                    _ = CategoryListVM.LoadAsync();
                else if (value?.ViewModel is ServiceProviderListViewModel)
                    _ = ServiceProviderListVM.LoadAsync();
                else if (value?.ViewModel is OperationLogViewModel)
                    _ = OperationLogVM.LoadAsync();
                else if (value?.ViewModel is TimeSettingsViewModel)
                    _ = TimeSettingsVM.LoadSettingsAsync();
            }
        }
    }

    public ViewModelBase? CurrentPage => _setupWizardVm ?? SelectedNavigationItem?.ViewModel;

    private SetupWizardViewModel? _setupWizardVm;

    public SetupWizardViewModel? SetupWizard => _setupWizardVm;
    public bool IsSetupWizardVisible => _setupWizardVm != null;

    public async Task<(string Language, string Theme)> StartSetupWizardAsync(string defaultLanguage, string defaultTheme)
    {
        _setupWizardVm = new SetupWizardViewModel(defaultLanguage, defaultTheme);
        RaisePropertyChanged(nameof(SetupWizard));
        RaisePropertyChanged(nameof(IsSetupWizardVisible));
        RaisePropertyChanged(nameof(CurrentPage));
        return await _setupWizardVm.Completion;
    }

    public void EndSetupWizard()
    {
        _setupWizardVm = null;
        RaisePropertyChanged(nameof(SetupWizard));
        RaisePropertyChanged(nameof(IsSetupWizardVisible));
        RaisePropertyChanged(nameof(CurrentPage));
    }

    public MainViewModel(
        IAccountService accountService,
        AccountRepository accountRepository,
        CategoryRepository categoryRepository,
        OperationLogRepository operationLogRepository,
        SettingsRepository settingsRepository,
        ServiceProviderRepository serviceProviderRepository,
        IOtpService otpService,
        ISecurityService securityService,
        IStorageService storageService,
        INtpTimeProvider ntpTimeProvider,
        IClipboardClearService clipboardClearService,
        IMessageService messageService)
    {
        _accountService = accountService;
        _otpService = otpService;
        _settingsRepository = settingsRepository;

        DashboardVM = new DashboardViewModel(_accountService, accountRepository, categoryRepository, operationLogRepository, _settingsRepository);
        DashboardVM.SetMainViewModel(this);
        AccountListVM = new AccountListViewModel(_accountService, _otpService, categoryRepository);
        CategoryListVM = new CategoryListViewModel(categoryRepository, _accountService, this, operationLogRepository, _settingsRepository);
        ServiceProviderListVM = new ServiceProviderListViewModel(serviceProviderRepository, operationLogRepository, _accountService);
        OperationLogVM = new OperationLogViewModel(operationLogRepository, _settingsRepository);
        BackupVM = new BackupViewModel(
            new BackupService(accountRepository,
                categoryRepository,
                _settingsRepository,
                operationLogRepository,
                securityService),
            operationLogRepository,
            accountRepository,
            categoryRepository,
            _settingsRepository);
        TimeSettingsVM = new TimeSettingsViewModel(_settingsRepository, ntpTimeProvider);
        AboutVM = new AboutViewModel();
        SettingsVM = new SettingsViewModel();

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem { Name = "仪表盘", Icon = "M13 3c-4.97 0-9 4.97-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.97 9-9s-4.97-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z", ViewModel = DashboardVM },
            new NavigationItem { Name = "所有账号", Icon = "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z", ViewModel = AccountListVM },
            new NavigationItem { Name = "分类管理", Icon = "M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z", ViewModel = CategoryListVM },
            new NavigationItem { Name = "服务商模板", Icon = "M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z", ViewModel = ServiceProviderListVM },
            new NavigationItem { Name = "操作日志", Icon = "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z", ViewModel = OperationLogVM },
            new NavigationItem { Name = "数据备份", Icon = "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z", ViewModel = BackupVM },
            new NavigationItem { Name = "时间设置", Icon = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z", ViewModel = TimeSettingsVM },
            new NavigationItem { Name = "关于", Icon = "M11 17h2v-6h-2v6zm1-15C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-9c.55 0 1-.45 1-1s-.45-1-1-1-1 .45-1 1 .45 1 1 1z", ViewModel = AboutVM },
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    public async Task LoadAsync()
    {
        await InitializeSettingsAsync();
        await DashboardVM.LoadAsync();
        _ = TimeSettingsVM.LoadSettingsAsync();
    }

    private async Task InitializeSettingsAsync()
    {
        var theme = await _settingsRepository.GetValueAsync("Theme");
        if (Application.Current != null)
        {
            if (theme == "Dark") 
            {
                AppAppearance.ApplyTheme("Dark");
                ThemeIcon = SunIconData;
                ThemeIconColor = "#FFFFFF"; 
            }
            else 
            {
                AppAppearance.ApplyTheme("Light");
                ThemeIcon = MoonIconData;
                ThemeIconColor = "#666666"; 
            }
        }

        var lang = await _settingsRepository.GetValueAsync("Language");
        SetLanguage(lang ?? "zh-CN");
    }

    public void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null) return;
        var current = app.RequestedThemeVariant;
        var newTheme = current == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        app.RequestedThemeVariant = newTheme;
        
        if (newTheme == ThemeVariant.Dark)
        {
            ThemeIcon = SunIconData;
            ThemeIconColor = "#FFFFFF"; 
        }
        else
        {
            ThemeIcon = MoonIconData;
            ThemeIconColor = "#666666"; 
        }
        
        AccountListVM.RefreshIconColors();
        ServiceProviderListVM.RefreshIconColors();
        
        _ = _settingsRepository.SetValueAsync("Theme", newTheme == ThemeVariant.Dark ? "Dark" : "Light");
    }

    private string _currentLanguage = "zh-CN";
    public string CurrentLanguage => _currentLanguage;

    public void ChangeLanguage(string lang)
    {
        SetLanguage(lang);
        _ = _settingsRepository.SetValueAsync("Language", lang);
    }

    private void SetLanguage(string lang)
    {
        _currentLanguage = lang;
        AppAppearance.ApplyLanguage(lang);
        
        UpdateNavigationItems();
        DashboardVM.OnLanguageChanged();
        AccountListVM.OnLanguageChanged();
        CategoryListVM.OnLanguageChanged();
        ServiceProviderListVM.OnLanguageChanged();
        OperationLogVM.OnLanguageChanged();
        BackupVM.OnLanguageChanged();
        TimeSettingsVM.OnLanguageChanged();
    }

    private void UpdateNavigationItems()
    {
        string GetStr(string key)
        {
            if (Application.Current!.TryGetResource(key, null, out var res) && res is string s) return s;
            return key;
        }

        if (NavigationItems.Count >= 8)
        {
            NavigationItems[0].Name = GetStr("Lang.Nav.Dashboard");
            NavigationItems[1].Name = GetStr("Lang.Nav.AllAccounts");
            NavigationItems[2].Name = GetStr("Lang.Nav.Category");
            NavigationItems[3].Name = GetStr("Lang.Nav.ServiceProvider");
            NavigationItems[4].Name = GetStr("Lang.Nav.Logs");
            NavigationItems[5].Name = GetStr("Lang.Nav.Backup");
            NavigationItems[6].Name = GetStr("Lang.Nav.TimeSettings");
            NavigationItems[7].Name = GetStr("Lang.Nav.About");
        }
    }

    public async Task AddAccountAsync(Account account)
    {
        await AccountListVM.AddAccountAsync(account);
    }

    public async Task UpdateAccountAsync(Account account)
    {
        await AccountListVM.UpdateAccountAsync(account);
    }

    public async Task DeleteAccountAsync(AccountItemViewModel item)
    {
        await AccountListVM.DeleteAccountAsync(item);
    }

    public void NavigateToAccountList()
    {
        if (NavigationItems.Count > 1)
        {
            SelectedNavigationItem = NavigationItems[1]; 
        }
    }
    public event EventHandler? QrImportRequested;

    public void RequestQrImport()
    {
        QrImportRequested?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateToBackup()
    {
        if (NavigationItems.Count > 5)
        {
            SelectedNavigationItem = NavigationItems[5]; 
        }
    }

    public void NavigateToTimeSettings()
    {
        if (NavigationItems.Count > 6)
        {
            SelectedNavigationItem = NavigationItems[6]; 
        }
    }
}
