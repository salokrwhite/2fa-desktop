using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.ViewModels;

public sealed class OperationLogViewModel : ViewModelBase
{
    private const string PageSizeAll = "all";
    private readonly OperationLogRepository _repository;
    private readonly SettingsRepository? _settingsRepository;
    private int _logCount;
    private string _loadError = string.Empty;
    private bool _isAllSelected;
    private int _selectedCount;
    private string _pageSize = "10";
    private bool _showRawLog;
    private List<OperationLog> _allLogs = new();

    public ObservableCollection<OperationLogItem> Logs { get; } = new();
    public ObservableCollection<PageSizeOption> PageSizeOptions { get; } = new();

    public int LogCount
    {
        get => _logCount;
        private set => SetField(ref _logCount, value);
    }

    public string LoadError
    {
        get => _loadError;
        private set => SetField(ref _loadError, value);
    }

    public bool IsAllSelected
    {
        get => _isAllSelected;
        set
        {
            if (SetField(ref _isAllSelected, value))
            {
                foreach (var log in Logs)
                {
                    log.IsSelected = value;
                }
                UpdateSelectedCount();
            }
        }
    }

    public int SelectedCount
    {
        get => _selectedCount;
        private set => SetField(ref _selectedCount, value);
    }

    public string PageSize
    {
        get => _pageSize;
        set
        {
            if (SetField(ref _pageSize, value))
            {
                UpdateDisplayedLogs();
            }
        }
    }

    public bool ShowRawLog
    {
        get => _showRawLog;
        set
        {
            if (SetField(ref _showRawLog, value))
            {
                if (_settingsRepository != null)
                {
                    _ = _settingsRepository.SetValueAsync(SettingKeys.ShowRawLog, value.ToString());
                }
                foreach (var logItem in Logs)
                {
                    logItem.SetDisplayMode(value);
                }
            }
        }
    }

    public ICommand DeleteSelectedCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand ClearAllLogsCommand { get; }
    public ICommand CancelSelectionCommand { get; }

    public OperationLogViewModel(OperationLogRepository repository, SettingsRepository? settingsRepository = null)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => SelectedCount > 0);
        ExportLogsCommand = new RelayCommand(async () => await ExportLogsAsync(), () => LogCount > 0);
        ClearAllLogsCommand = new RelayCommand(async () => await ClearAllLogsAsync(), () => LogCount > 0);
        CancelSelectionCommand = new RelayCommand(async () => await CancelSelectionAsync(), () => SelectedCount > 0);
        RefreshPageSizeOptions();
        
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedCount))
            {
                ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelSelectionCommand).RaiseCanExecuteChanged();
            }
            if (e.PropertyName == nameof(LogCount))
            {
                ((RelayCommand)ExportLogsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearAllLogsCommand).RaiseCanExecuteChanged();
            }
        };
    }

    public async Task LoadAsync()
    {
        try
        {
            if (_settingsRepository != null)
            {
                var rawSetting = await _settingsRepository.GetValueAsync(SettingKeys.ShowRawLog);
                _showRawLog = rawSetting == "True";
                OnPropertyChanged(nameof(ShowRawLog));
            }

            _allLogs = await _repository.GetAllAsync();
            LoadError = string.Empty;
            LogCount = _allLogs.Count;
            UpdateDisplayedLogs();
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            LogCount = 0;
            _allLogs.Clear();
            if (Dispatcher.UIThread.CheckAccess())
            {
                Logs.Clear();
                return;
            }
            await Dispatcher.UIThread.InvokeAsync(() => Logs.Clear());
        }
    }

    private void UpdateDisplayedLogs()
    {
        var logsToDisplay = PageSize == PageSizeAll 
            ? _allLogs 
            : _allLogs.Take(int.TryParse(PageSize, out var size) ? size : 10).ToList();

        if (Dispatcher.UIThread.CheckAccess())
        {
            Logs.Clear();
            foreach (var item in logsToDisplay)
            {
                var logItem = new OperationLogItem(item, _showRawLog);
                logItem.SelectionChanged += OnLogSelectionChanged;
                Logs.Add(logItem);
            }
            UpdateSelectedCount();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Logs.Clear();
            foreach (var item in logsToDisplay)
            {
                var logItem = new OperationLogItem(item, _showRawLog);
                logItem.SelectionChanged += OnLogSelectionChanged;
                Logs.Add(logItem);
            }
            UpdateSelectedCount();
        });
    }

    public void OnLanguageChanged()
    {
        RefreshPageSizeOptions();
        UpdateDisplayedLogs();
    }

    private void RefreshPageSizeOptions()
    {
        PageSizeOptions.Clear();
        PageSizeOptions.Add(new PageSizeOption("10"));
        PageSizeOptions.Add(new PageSizeOption("20"));
        PageSizeOptions.Add(new PageSizeOption("50"));
        PageSizeOptions.Add(new PageSizeOption("100"));
        PageSizeOptions.Add(new PageSizeOption(PageSizeAll, "Lang.Logs.PageSizeAll"));
    }

    public async Task AddLogAsync(string operation, string target, string details = "")
    {
        var log = new OperationLog
        {
            Operation = operation,
            Target = target,
            Details = details
        };
        await _repository.AddAsync(log);
        
        _allLogs.Insert(0, log);
        LogCount = _allLogs.Count;
        
        var logItem = new OperationLogItem(log, _showRawLog);
        logItem.SelectionChanged += OnLogSelectionChanged;
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            Logs.Insert(0, logItem);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Logs.Insert(0, logItem);
        });
    }

    private void OnLogSelectionChanged(object? sender, EventArgs e)
    {
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Logs.Count(l => l.IsSelected);
        _isAllSelected = Logs.Count > 0 && SelectedCount == Logs.Count;
        OnPropertyChanged(nameof(IsAllSelected));
    }

    private async Task DeleteSelectedAsync()
    {
        var selectedLogs = Logs.Where(l => l.IsSelected).ToList();
        if (selectedLogs.Count == 0) return;

        foreach (var log in selectedLogs)
        {
            await _repository.DeleteAsync(log.Log.Id);
            log.SelectionChanged -= OnLogSelectionChanged;
            Logs.Remove(log);
            _allLogs.Remove(log.Log);
        }
        
        LogCount = _allLogs.Count;
        UpdateSelectedCount();
    }

    private async Task CancelSelectionAsync()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            foreach (var log in Logs)
            {
                log.IsSelected = false;
            }
            UpdateSelectedCount();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var log in Logs)
            {
                log.IsSelected = false;
            }
            UpdateSelectedCount();
        });
    }

    private async Task ExportLogsAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(App.MainWindow);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = GetResource("Lang.Logs.ExportDialogTitle"),
                SuggestedFileName = $"operation_logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(GetResource("Lang.Logs.ExportFileTypeCsv")) { Patterns = new[] { "*.csv" } }
                }
            });

            if (file == null) return;

            var csv = new StringBuilder();
            csv.AppendLine(GetResource("Lang.Logs.ExportCsvHeader"));

            foreach (var log in Logs)
            {
                var timestamp = log.Log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var operation = EscapeCsvField(log.DisplayOperation);
                var target = EscapeCsvField(log.DisplayTarget);
                var details = EscapeCsvField(log.DisplayDetails);
                csv.AppendLine($"{timestamp},{operation},{target},{details}");
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
            await writer.WriteAsync(csv.ToString());
            
            LoadError = GetResource("Lang.Logs.ExportSuccess");
        }
        catch (Exception ex)
        {
            LoadError = string.Format(CultureInfo.CurrentCulture, GetResource("Lang.Logs.ExportFailedFormat"), ex.Message);
        }
    }

    private async Task ClearAllLogsAsync()
    {
        await _repository.DeleteAllAsync();
        foreach (var log in Logs)
        {
            log.SelectionChanged -= OnLogSelectionChanged;
        }
        Logs.Clear();
        _allLogs.Clear();
        LogCount = 0;
        UpdateSelectedCount();
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    internal static string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }
}

public sealed class PageSizeOption
{
    public string Value { get; }
    public string? DisplayResourceKey { get; }

    public string DisplayText => string.IsNullOrEmpty(DisplayResourceKey)
        ? Value
        : OperationLogViewModel.GetResource(DisplayResourceKey);

    public PageSizeOption(string value, string? displayResourceKey = null)
    {
        Value = value;
        DisplayResourceKey = displayResourceKey;
    }
}

public class OperationLogItem : ObservableObject
{
    private bool _isSelected;
    public OperationLog Log { get; }

    private string _displayOperation = string.Empty;
    public string DisplayOperation
    {
        get => _displayOperation;
        private set => SetField(ref _displayOperation, value);
    }

    private string _displayTarget = string.Empty;
    public string DisplayTarget
    {
        get => _displayTarget;
        private set => SetField(ref _displayTarget, value);
    }

    private string _displayDetails = string.Empty;
    public string DisplayDetails
    {
        get => _displayDetails;
        private set => SetField(ref _displayDetails, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? SelectionChanged;

    public OperationLogItem(OperationLog log, bool isRawMode = false)
    {
        Log = log;
        SetDisplayMode(isRawMode);
    }

    public void SetDisplayMode(bool isRawMode)
    {
        if (isRawMode)
        {
            DisplayOperation = Log.Operation;
            DisplayTarget = Log.Target;
            DisplayDetails = Log.Details;
        }
        else
        {
            DisplayOperation = TranslateOperation(Log.Operation);
            DisplayTarget = TranslateTarget(Log.Operation, Log.Target);
            DisplayDetails = TranslateDetails(Log.Operation, Log.Target, Log.Details);
        }
    }

    private static string TranslateOperation(string operation)
    {
        return operation switch
        {
            "op.add_account" => OperationLogViewModel.GetResource("Lang.Logs.Op.AddAccount"),
            "op.update_account" => OperationLogViewModel.GetResource("Lang.Logs.Op.UpdateAccount"),
            "op.delete_account" => OperationLogViewModel.GetResource("Lang.Logs.Op.DeleteAccount"),
            "op.import_backup" => OperationLogViewModel.GetResource("Lang.Logs.Op.ImportBackup"),
            "op.export_backup" => OperationLogViewModel.GetResource("Lang.Logs.Op.ExportBackup"),
            "op.update_settings" => OperationLogViewModel.GetResource("Lang.Logs.Op.UpdateSettings"),
            "op.add_category" => OperationLogViewModel.GetResource("Lang.Logs.Op.AddCategory"),
            "op.delete_category" => OperationLogViewModel.GetResource("Lang.Logs.Op.DeleteCategory"),
            "op.update_category" => OperationLogViewModel.GetResource("Lang.Logs.Op.UpdateCategory"),
            "op.merge_category" => OperationLogViewModel.GetResource("Lang.Logs.Op.MergeCategory"),
            "op.test_log" => OperationLogViewModel.GetResource("Lang.Logs.Op.TestLog"),
            "新增账号" => OperationLogViewModel.GetResource("Lang.Logs.Op.AddAccount"),
            "更新账号" => OperationLogViewModel.GetResource("Lang.Logs.Op.UpdateAccount"),
            "删除账号" => OperationLogViewModel.GetResource("Lang.Logs.Op.DeleteAccount"),
            "导入备份" => OperationLogViewModel.GetResource("Lang.Logs.Op.ImportBackup"),
            "导出备份" => OperationLogViewModel.GetResource("Lang.Logs.Op.ExportBackup"),
            "更新设置" => OperationLogViewModel.GetResource("Lang.Logs.Op.UpdateSettings"),
            "新增分类" => OperationLogViewModel.GetResource("Lang.Logs.Op.AddCategory"),
            "删除分类" => OperationLogViewModel.GetResource("Lang.Logs.Op.DeleteCategory"),
            "测试日志" => OperationLogViewModel.GetResource("Lang.Logs.Op.TestLog"),
            _ => operation
        };
    }

    private static string TranslateTarget(string operation, string target)
    {
        if (string.IsNullOrEmpty(target)) return string.Empty;
        if (operation is "op.update_settings" or "更新设置")
        {
            return target switch
            {
                "Security" => OperationLogViewModel.GetResource("Lang.Logs.Target.Security"),
                "Settings" => OperationLogViewModel.GetResource("Lang.Logs.Target.Settings"),
                _ => target
            };
        }

        if (operation is "op.add_account" or "op.update_account" or "op.delete_account"
            or "新增账号" or "更新账号" or "删除账号")
        {
            if (target.Contains(" / "))
            {
                var parts = target.Split(" / ", 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    var issuer = parts[0];
                    var name = parts[1];
                    return $"{OperationLogViewModel.GetResource("Lang.Logs.Target.Issuer")}：{issuer}，{OperationLogViewModel.GetResource("Lang.Logs.Target.AccountName")}：{name}";
                }
            }
        }

        if ((operation is "op.update_category" or "op.merge_category") && target.Contains(" -> "))
        {
            var parts = target.Split(" -> ", 2);
            return $"{parts[0]} → {parts[1]}";
        }

        return target;
    }

    private static string TranslateDetails(string operation, string target, string details)
    {
        if (string.IsNullOrEmpty(details)) return string.Empty;
        if (operation is "op.update_settings" or "更新设置")
        {
            if (target is "Security")
            {
                return details switch
                {
                    "RotateAccountSecrets" => OperationLogViewModel.GetResource("Lang.Logs.Details.RotateSecrets"),
                    _ => details
                };
            }

            if (target is "Settings")
            {
                return TranslateSettingsDetails(details);
            }
        }

        if (operation is "op.export_backup" or "op.import_backup" or "导出备份" or "导入备份")
        {
            return TranslateBackupDetails(details);
        }

        if (operation is "op.add_account" or "op.update_account" or "op.delete_account"
            or "新增账号" or "更新账号" or "删除账号")
        {
            return TranslateAccountDetails(details);
        }

        if (operation is "op.update_category")
        {
            return details switch
            {
                "Name" => OperationLogViewModel.GetResource("Lang.Logs.Details.CategoryName"),
                "Description" => OperationLogViewModel.GetResource("Lang.Logs.Details.CategoryDescription"),
                "NameAndDescription" => OperationLogViewModel.GetResource("Lang.Logs.Details.CategoryNameAndDescription"),
                _ => details
            };
        }

        return details;
    }

    private static string TranslateSettingsDetails(string details)
    {
        var parts = details.Split(';', StringSplitOptions.TrimEntries);
        var translated = new List<string>();

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) { translated.Add(part); continue; }

            var key = kv[0].Trim();
            var val = kv[1].Trim();

            var translatedPart = key switch
            {
                "Theme" => $"{OperationLogViewModel.GetResource("Lang.Logs.Details.Theme")}: {TranslateThemeValue(val)}",
                "Language" => $"{OperationLogViewModel.GetResource("Lang.Logs.Details.Language")}: {val}",
                "RefreshPeriod" => $"{OperationLogViewModel.GetResource("Lang.Logs.Details.RefreshPeriod")}: {val}s",
                "AutoLockMinutes" => $"{OperationLogViewModel.GetResource("Lang.Logs.Details.AutoLockMinutes")}: {val}{OperationLogViewModel.GetResource("Lang.Security.MinutesUnit")}",
                _ => part
            };
            translated.Add(translatedPart);
        }

        return string.Join("; ", translated);
    }

    private static string TranslateThemeValue(string theme)
    {
        return theme switch
        {
            "Dark" => OperationLogViewModel.GetResource("Lang.Theme.Dark"),
            "Light" => OperationLogViewModel.GetResource("Lang.Theme.Light"),
            _ => theme
        };
    }

    private static string TranslateBackupDetails(string details)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = details.Split(';', StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                dict[kv[0].Trim()] = kv[1].Trim();
            }
        }

        if (dict.Count == 0) return details;

        var translated = new List<string>();
        if (dict.TryGetValue("Accounts", out var accounts))
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.AccountsCount"), accounts));
        }
        if (dict.TryGetValue("Categories", out var categories) && !dict.ContainsKey("Imported"))
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.CategoriesCount"), categories));
        }

        if (dict.TryGetValue("Imported", out var imported))
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.Imported"), imported));
        }
        if (dict.TryGetValue("Imported", out _) && dict.TryGetValue("Categories", out var importedCats))
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.CategoriesCount"), importedCats));
        }
        if (dict.TryGetValue("Skipped", out var skipped) && skipped != "0")
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.Skipped"), skipped));
        }
        if (dict.TryGetValue("Updated", out var updated) && updated != "0")
        {
            translated.Add(string.Format(
                OperationLogViewModel.GetResource("Lang.Logs.Details.Updated"), updated));
        }

        return translated.Count > 0 ? string.Join(", ", translated) : details;
    }

    private static string TranslateAccountDetails(string details)
    {
        if (string.IsNullOrWhiteSpace(details)) return string.Empty;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = details.Split(';', StringSplitOptions.TrimEntries);
        
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                dict[kv[0].Trim()] = kv[1].Trim();
            }
        }

        if (dict.Count > 0 && dict.ContainsKey("Type"))
        {
            var type = dict.TryGetValue("Type", out var t) ? t : "TOTP";
            var digits = dict.TryGetValue("Digits", out var d) ? d : "6";
            var period = dict.TryGetValue("Period", out var p) ? p : "30";
            
            return $"{type.ToUpperInvariant()}, {digits}{OperationLogViewModel.GetResource("Lang.Logs.Details.DigitsUnit")}, {period}s";
        }

        var spaceParts = details.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (spaceParts.Length >= 3 && (spaceParts[0] == "TOTP" || spaceParts[0] == "HOTP"))
        {
            var type = spaceParts[0];
            var digits = spaceParts[1];
            var period = spaceParts[2];
            return $"{type}, {digits}{OperationLogViewModel.GetResource("Lang.Logs.Details.DigitsUnit")}, {period}";
        }

        return details;
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<Task>? _execute;
    private readonly Action? _executeSync;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _executeSync = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (_execute != null)
        {
            _ = ExecuteAsync();
        }
        else
        {
            _executeSync?.Invoke();
        }
    }

    private async Task ExecuteAsync()
    {
        if (_execute != null)
        {
            await _execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
