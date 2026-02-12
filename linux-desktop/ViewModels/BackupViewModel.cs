using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;
using TwoFactorAuth.Views.Dialogs;

namespace TwoFactorAuth.ViewModels;

public sealed class BackupViewModel : ViewModelBase
{
    private readonly BackupService _backupService;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly AccountRepository _accountRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly SettingsRepository _settingsRepository;
    private Window? _parentWindow;
    private MainViewModel? _mainViewModel;

    private int _accountCount;
    private int _categoryCount;
    private string _lastBackupTime;
    private bool _hasActualBackupTime; 
    private string _title;

    public BackupViewModel(
        BackupService backupService,
        OperationLogRepository operationLogRepository,
        AccountRepository accountRepository,
        CategoryRepository categoryRepository,
        SettingsRepository settingsRepository)
    {
        _backupService = backupService;
        _operationLogRepository = operationLogRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _settingsRepository = settingsRepository;
        _lastBackupTime = GetLocalizedNeverBackedUp();
        _title = GetLocalizedTitle();
    }
    
    private string GetLocalizedTitle()
    {
        if (Application.Current?.TryGetResource("Lang.Backup.PageTitle", null, out var resource) == true 
            && resource is string text)
        {
            return text;
        }
        return "Backup & Restore"; 
    }
    
    private string GetLocalizedNeverBackedUp()
    {
        if (Application.Current?.TryGetResource("Lang.Backup.NeverBackedUp", null, out var resource) == true 
            && resource is string text)
        {
            return text;
        }
        return "Never backed up"; 
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

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public int AccountCount
    {
        get => _accountCount;
        set => SetField(ref _accountCount, value);
    }

    public int CategoryCount
    {
        get => _categoryCount;
        set => SetField(ref _categoryCount, value);
    }

    public string LastBackupTime
    {
        get => _lastBackupTime;
        set => SetField(ref _lastBackupTime, value);
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public async Task LoadDataAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var categories = await _categoryRepository.GetAllAsync();
        AccountCount = accounts.Count;
        CategoryCount = categories.Count;
        if (!_hasActualBackupTime)
        {
            LastBackupTime = GetLocalizedNeverBackedUp();
        }
    }
    
    public void OnLanguageChanged()
    {
        Title = GetLocalizedTitle();
        if (!_hasActualBackupTime)
        {
            LastBackupTime = GetLocalizedNeverBackedUp();
        }
    }

    public async Task ExportBackupAsync()
    {
        if (_parentWindow == null) return;

        try
        {
            var exportDialog = new ExportBackupDialog();
            var exportResult = await exportDialog.ShowDialog<ExportBackupResult?>(_parentWindow);

            if (exportResult == null) return;
            var backupFile = await _backupService.ExportAsync(
                exportResult.Password,
                exportResult.IncludeSettings,
                exportResult.IncludeLogs);
            var saveDialog = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = GetLocalizedString("Lang.Backup.SaveBackupFileTitle", "Save Backup File"),
                SuggestedFileName = _backupService.GenerateBackupFileName(),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("2FA 备份文件")
                    {
                        Patterns = new[] { "*.2fabackup" }
                    }
                }
            });

            if (saveDialog == null) return;
            var json = JsonSerializer.Serialize(backupFile, BackupJsonContext.Default.BackupFile);

            await using var stream = await saveDialog.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            _hasActualBackupTime = true;
            LastBackupTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _settingsRepository.SetValueAsync("LastBackupTime", DateTime.Now.ToString("O"));
            await _operationLogRepository.AddAsync(new OperationLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Operation = "op.export_backup",
                Target = saveDialog.Name,
                Details = $"Accounts={backupFile.Metadata.AccountCount}; Categories={backupFile.Metadata.CategoryCount}"
            });
            var successTitle = GetLocalizedString("Lang.Backup.ExportSuccessTitle", "Export Successful");
            var successMessage = GetLocalizedString("Lang.Backup.ExportSuccessMessage", "Backup file saved to:");
            await ShowMessageAsync(successTitle, $"{successMessage}\n{saveDialog.Path.LocalPath}");
        }
        catch (Exception ex)
        {
            var failedTitle = GetLocalizedString("Lang.Backup.ExportFailedTitle", "Export Failed");
            var failedMessage = GetLocalizedString("Lang.Backup.ExportFailedMessage", "Error exporting backup:");
            await ShowMessageAsync(failedTitle, $"{failedMessage}\n{ex.Message}");
        }
    }

    public async Task ImportBackupAsync()
    {
        if (_parentWindow == null) return;

        try
        {
            var openDialog = await _parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = GetLocalizedString("Lang.Backup.SelectBackupFileTitle", "Select Backup File"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("2FA 备份文件")
                    {
                        Patterns = new[] { "*.2fabackup" }
                    }
                }
            });

            if (openDialog.Count == 0) return;
            await using var stream = await openDialog[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var backupFile = JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupFile);

            if (backupFile == null || !_backupService.ValidateBackupFile(backupFile))
            {
                var failedTitle = GetLocalizedString("Lang.Backup.ImportFailedTitle", "Import Failed");
                var invalidMessage = GetLocalizedString("Lang.Backup.InvalidBackupFileMessage", "Invalid backup file format");
                await ShowMessageAsync(failedTitle, invalidMessage);
                return;
            }

            var importDialog = new ImportBackupDialog(backupFile);
            var importResult = await importDialog.ShowDialog<ImportBackupResult?>(_parentWindow);
            if (importResult == null) return;
            var result = await _backupService.ImportAsync(
                backupFile,
                importResult.Password,
                importResult.Mode,
                importResult.ConflictStrategy);

            if (!result.Success)
            {
                var failedTitle = GetLocalizedString("Lang.Backup.ImportFailedTitle", "Import Failed");
                await ShowMessageAsync(failedTitle, result.ErrorMessage);
                return;
            }

            await _operationLogRepository.AddAsync(new OperationLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Operation = "op.import_backup",
                Target = openDialog[0].Name,
                Details = $"Imported={result.AccountsImported}; Categories={result.CategoriesImported}; " +
                         $"Skipped={result.AccountsSkipped}; Updated={result.AccountsUpdated}"
            });

            await LoadDataAsync();
            
            if (_mainViewModel != null)
            {
                await _mainViewModel.DashboardVM.LoadAsync();
                await _mainViewModel.AccountListVM.LoadAsync();
                await _mainViewModel.CategoryListVM.LoadAsync();
                await _mainViewModel.OperationLogVM.LoadAsync();
            }

            var completedMsg = GetLocalizedString("Lang.Backup.ImportCompletedMessage", "Import completed!");
            var accountsLabel = GetLocalizedString("Lang.Backup.AccountsLabel", "Accounts:");
            var categoriesLabel = GetLocalizedString("Lang.Backup.CategoriesLabel", "Categories:");
            var importedLabel = GetLocalizedString("Lang.Backup.ImportedLabel", "imported");
            var updatedLabel = GetLocalizedString("Lang.Backup.UpdatedLabel", "updated");
            var skippedLabel = GetLocalizedString("Lang.Backup.SkippedLabel", "skipped");
            var deletedLabel = GetLocalizedString("Lang.Backup.DeletedLabel", "deleted");
            
            var message = $"{completedMsg}\n\n" +
                         $"{accountsLabel} {importedLabel} {result.AccountsImported}, {updatedLabel} {result.AccountsUpdated}, {skippedLabel} {result.AccountsSkipped}\n" +
                         $"{categoriesLabel} {importedLabel} {result.CategoriesImported}, {skippedLabel} {result.CategoriesSkipped}";

            if (result.AccountsDeleted > 0)
            {
                message += $"\n{deletedLabel}: {result.AccountsDeleted}";
            }

            var successTitle = GetLocalizedString("Lang.Backup.ImportSuccessTitle", "Import Successful");
            await ShowMessageAsync(successTitle, message);
        }
        catch (Exception ex)
        {
            var failedTitle = GetLocalizedString("Lang.Backup.ImportFailedTitle", "Import Failed");
            var failedMessage = GetLocalizedString("Lang.Backup.ImportFailedMessage", "Error importing backup:");
            await ShowMessageAsync(failedTitle, $"{failedMessage}\n{ex.Message}");
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (_parentWindow == null) return;

        Window? messageDialog = null;
        messageDialog = new Window
        {
            Title = title,
            Width = 450,
            MinWidth = 350,
            MaxWidth = 600,
            MinHeight = 150,
            MaxHeight = 600,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        var button = new Button
        {
            Content = GetLocalizedString("Lang.Backup.ConfirmButton", "OK"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        button.Click += (_, _) => messageDialog.Close();

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    button
                }
            }
        };

        messageDialog.Content = scrollViewer;

        await messageDialog.ShowDialog(_parentWindow);
    }
}
