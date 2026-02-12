using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using TwoFactorAuth.Data;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly AccountRepository _accountRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly SettingsRepository _settingsRepository;
    private MainViewModel? _mainViewModel;

    private int _accountCount;
    public int AccountCount
    {
        get => _accountCount;
        set => SetField(ref _accountCount, value);
    }

    private int _categoryCount;
    public int CategoryCount
    {
        get => _categoryCount;
        set => SetField(ref _categoryCount, value);
    }

    private int _logCount;
    public int LogCount
    {
        get => _logCount;
        set => SetField(ref _logCount, value);
    }
    
    private bool _isMorning;
    public bool IsMorning
    {
        get => _isMorning;
        set => SetField(ref _isMorning, value);
    }

    private bool _isAfternoon;
    public bool IsAfternoon
    {
        get => _isAfternoon;
        set => SetField(ref _isAfternoon, value);
    }

    private bool _isEvening;
    public bool IsEvening
    {
        get => _isEvening;
        set => SetField(ref _isEvening, value);
    }

    private string _backupWarningText = string.Empty;
    public string BackupWarningText
    {
        get => _backupWarningText;
        set => SetField(ref _backupWarningText, value);
    }

    private bool _showBackupWarning;
    public bool ShowBackupWarning
    {
        get => _showBackupWarning;
        set => SetField(ref _showBackupWarning, value);
    }

    public ObservableCollection<string> RecentActivities { get; } = new();

    public DashboardViewModel(
        IAccountService accountService,
        AccountRepository accountRepository,
        CategoryRepository categoryRepository, 
        OperationLogRepository operationLogRepository,
        SettingsRepository settingsRepository)
    {
        _accountService = accountService;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _operationLogRepository = operationLogRepository;
        _settingsRepository = settingsRepository;
        AddAccountCommand = new RelayCommand(AddAccount);
        ScanQrCodeCommand = new RelayCommand(ScanQrCode);
        ImportBackupCommand = new RelayCommand(ImportBackup);
    }
    
    public ICommand AddAccountCommand { get; }
    public ICommand ScanQrCodeCommand { get; }
    public ICommand ImportBackupCommand { get; }

    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public async Task LoadAsync()
    {
        var accountCountTask = _accountRepository.GetCountAsync();
        var categoryCountTask = _categoryRepository.GetCountAsync();
        var logCountTask = _operationLogRepository.GetCountAsync();

        await Task.WhenAll(accountCountTask, categoryCountTask, logCountTask);

        AccountCount = accountCountTask.Result;
        CategoryCount = categoryCountTask.Result;
        LogCount = logCountTask.Result;
        
        var hour = System.DateTime.Now.Hour;
        IsMorning = hour < 12;
        IsAfternoon = hour >= 12 && hour < 18;
        IsEvening = hour >= 18;
        await Task.WhenAll(
            LoadSecurityRemindersAsync(),
            LoadRecentActivitiesAsync()
        );
    }

    public void OnLanguageChanged()
    {
        _ = LoadSecurityRemindersAsync();
        _ = LoadRecentActivitiesAsync();
    }

    private async Task LoadSecurityRemindersAsync()
    {
        try
        {
            var lastBackup = await _settingsRepository.GetValueAsync("LastBackupTime");
            var warningTemplate = GetLocalizedString("Lang.Dashboard.BackupWarning", "No backup for {0} days, backup recommended");
            var longWarningText = GetLocalizedString("Lang.Dashboard.BackupWarningLong", "No backup for a long time, backup recommended immediately");

            if (!string.IsNullOrEmpty(lastBackup) && DateTime.TryParse(lastBackup, out var backupTime))
            {
                var daysSinceBackup = (DateTime.Now.Date - backupTime.Date).Days;
                if (daysSinceBackup < 0)
                {
                    daysSinceBackup = 0;
                }

                if (daysSinceBackup > 365)
                {
                    BackupWarningText = longWarningText;
                    ShowBackupWarning = true;
                }
                else if (daysSinceBackup > 45)
                {
                    BackupWarningText = string.Format(warningTemplate, daysSinceBackup);
                    ShowBackupWarning = true;
                }
                else
                {
                    ShowBackupWarning = false;
                }
            }
            else
            {
                BackupWarningText = longWarningText;
                ShowBackupWarning = true;
            }
        }
        catch
        {
            ShowBackupWarning = false;
        }
    }

    private async Task LoadRecentActivitiesAsync()
    {
        try
        {
            RecentActivities.Clear();
            var recentLogs = await _operationLogRepository.GetRecentAsync(5);

            foreach (var log in recentLogs)
            {
                var timeAgo = GetTimeAgo(log.Timestamp);
                var operation = TranslateDashboardOperation(log.Operation);
                var target = TranslateDashboardTarget(log.Operation, log.Target);
                var activity = $"• {timeAgo} {operation}";
                if (!string.IsNullOrEmpty(target))
                {
                    activity += $" \"{target}\"";
                }
                RecentActivities.Add(activity);
            }
        }
        catch
        {
            
        }
    }

    private static string TranslateDashboardOperation(string operation)
    {
        return operation switch
        {
            "op.add_account" => GetLocalizedStringStatic("Lang.Logs.Op.AddAccount", "Add Account"),
            "op.update_account" => GetLocalizedStringStatic("Lang.Logs.Op.UpdateAccount", "Update Account"),
            "op.delete_account" => GetLocalizedStringStatic("Lang.Logs.Op.DeleteAccount", "Delete Account"),
            "op.import_backup" => GetLocalizedStringStatic("Lang.Logs.Op.ImportBackup", "Import Backup"),
            "op.export_backup" => GetLocalizedStringStatic("Lang.Logs.Op.ExportBackup", "Export Backup"),
            "op.update_settings" => GetLocalizedStringStatic("Lang.Logs.Op.UpdateSettings", "Update Settings"),
            "op.add_category" => GetLocalizedStringStatic("Lang.Logs.Op.AddCategory", "Add Category"),
            "op.delete_category" => GetLocalizedStringStatic("Lang.Logs.Op.DeleteCategory", "Delete Category"),
            "op.update_category" => GetLocalizedStringStatic("Lang.Logs.Op.UpdateCategory", "Update Category"),
            "op.merge_category" => GetLocalizedStringStatic("Lang.Logs.Op.MergeCategory", "Merge Category"),
            "op.test_log" => GetLocalizedStringStatic("Lang.Logs.Op.TestLog", "Test Log"),
            _ => operation
        };
    }

    private static string TranslateDashboardTarget(string operation, string target)
    {
        if (string.IsNullOrEmpty(target)) return string.Empty;

        if (operation is "op.update_settings")
        {
            return target switch
            {
                "Security" => GetLocalizedStringStatic("Lang.Logs.Target.Security", "Security Settings"),
                "Settings" => GetLocalizedStringStatic("Lang.Logs.Target.Settings", "App Settings"),
                _ => target
            };
        }

        return target;
    }

    private static string GetLocalizedStringStatic(string key, string fallback)
    {
        if (Application.Current?.TryGetResource(key, null, out var resource) == true 
            && resource is string text)
        {
            return text;
        }
        return fallback;
    }

    private string GetTimeAgo(DateTime timestamp)
    {
        var timeSpan = DateTime.Now - timestamp;
        
        if (timeSpan.TotalMinutes < 1)
            return GetLocalizedString("Lang.Dashboard.JustNow", "刚刚");
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} {GetLocalizedString("Lang.Dashboard.MinutesAgo", "分钟前")}";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} {GetLocalizedString("Lang.Dashboard.HoursAgo", "小时前")}";
        if (timeSpan.TotalDays < 30)
            return $"{(int)timeSpan.TotalDays} {GetLocalizedString("Lang.Dashboard.DaysAgo", "天前")}";
        
        return timestamp.ToString("yyyy-MM-dd");
    }

    private string GetLocalizedString(string key, string fallback)
    {
        if (Application.Current?.TryGetResource(key, null, out var resource) == true 
            && resource is string text)
        {
            return text;
        }
        return fallback;
    }

    public void AddAccount()
    {
        _mainViewModel?.NavigateToAccountList();
    }

    public void ScanQrCode()
    {
        _mainViewModel?.RequestQrImport();
    }

    public void ImportBackup()
    {
        _mainViewModel?.NavigateToBackup();
    }
}
