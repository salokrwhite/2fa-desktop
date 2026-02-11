namespace TwoFactorAuth.ViewModels;

public sealed class PasswordDialogViewModel : ViewModelBase
{
    private string _title = "主密码";
    private string _prompt = "请输入主密码";
    private string _cancelText = "取消";
    private string _confirmText = "解锁";
    private string _errorMessage = string.Empty;
    private bool _allowCancel = true;

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Prompt
    {
        get => _prompt;
        set => SetField(ref _prompt, value);
    }

    public string CancelText
    {
        get => _cancelText;
        set => SetField(ref _cancelText, value);
    }

    public string ConfirmText
    {
        get => _confirmText;
        set => SetField(ref _confirmText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool AllowCancel
    {
        get => _allowCancel;
        set => SetField(ref _allowCancel, value);
    }
}
