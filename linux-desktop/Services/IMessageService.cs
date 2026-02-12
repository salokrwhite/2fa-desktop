using System;

namespace TwoFactorAuth.Services;

public interface IMessageService
{
    event EventHandler<MessageEventArgs>? MessageRequested;
    void ShowWarning(string message);
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowInfo(string message);
}

public class MessageEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public MessageType Type { get; set; }
}

public enum MessageType
{
    Info,
    Success,
    Warning,
    Error
}
