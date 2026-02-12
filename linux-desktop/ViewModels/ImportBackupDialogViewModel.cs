namespace TwoFactorAuth.ViewModels;

public sealed class ImportBackupDialogViewModel : ViewModelBase
{
    private string _backupTimestamp = string.Empty;
    private string _accountCount = "0";
    private string _categoryCount = "0";
    private string _appVersion = string.Empty;
    private bool _isMergeMode = true;
    private int _conflictStrategyIndex = 0;
    private string _errorMessage = string.Empty;

    public string BackupTimestamp
    {
        get => _backupTimestamp;
        set => SetField(ref _backupTimestamp, value);
    }

    public string AccountCount
    {
        get => _accountCount;
        set => SetField(ref _accountCount, value);
    }

    public string CategoryCount
    {
        get => _categoryCount;
        set => SetField(ref _categoryCount, value);
    }

    public string AppVersion
    {
        get => _appVersion;
        set => SetField(ref _appVersion, value);
    }

    public bool IsMergeMode
    {
        get => _isMergeMode;
        set => SetField(ref _isMergeMode, value);
    }

    public int ConflictStrategyIndex
    {
        get => _conflictStrategyIndex;
        set => SetField(ref _conflictStrategyIndex, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }
}
