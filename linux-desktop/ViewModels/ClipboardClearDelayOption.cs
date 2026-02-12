namespace TwoFactorAuth.ViewModels;

public sealed class ClipboardClearDelayOption
{
    public int Value { get; }
    public string DisplayText => $"{Value}s";

    public ClipboardClearDelayOption(int value)
    {
        Value = value;
    }
}
