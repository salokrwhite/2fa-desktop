namespace TwoFactorAuth.ViewModels;

public sealed class ExportBackupDialogViewModel : ViewModelBase
{
    private bool _includeSettings = true;
    private bool _includeLogs;
    private string _errorMessage = string.Empty;

    public bool IncludeSettings
    {
        get => _includeSettings;
        set => SetField(ref _includeSettings, value);
    }

    public bool IncludeLogs
    {
        get => _includeLogs;
        set => SetField(ref _includeLogs, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }
}
