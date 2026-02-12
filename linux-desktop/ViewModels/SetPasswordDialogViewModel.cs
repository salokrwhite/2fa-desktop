namespace TwoFactorAuth.ViewModels;

public sealed class SetPasswordDialogViewModel : ViewModelBase
{
    private string _title = "设置主密码";
    private string _newPasswordPrompt = "请输入新密码";
    private string _confirmPasswordPrompt = "请再次输入新密码";
    private string _cancelText = "取消";
    private string _confirmText = "保存";
    private string _errorMessage = string.Empty;

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string NewPasswordPrompt
    {
        get => _newPasswordPrompt;
        set => SetField(ref _newPasswordPrompt, value);
    }

    public string ConfirmPasswordPrompt
    {
        get => _confirmPasswordPrompt;
        set => SetField(ref _confirmPasswordPrompt, value);
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
}

