namespace TwoFactorAuth.ViewModels;

public sealed class AutoLockMinuteOption
{
    public int Value { get; }
    public string DisplayText => Value.ToString();

    public AutoLockMinuteOption(int value)
    {
        Value = value;
    }
}

