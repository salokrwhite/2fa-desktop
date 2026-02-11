using System;

namespace TwoFactorAuth.Services;

public class MessageService : IMessageService
{
    public event EventHandler<MessageEventArgs>? MessageRequested;

    public void ShowWarning(string message)
    {
        MessageRequested?.Invoke(this, new MessageEventArgs
        {
            Message = message,
            Type = MessageType.Warning
        });
    }

    public void ShowSuccess(string message)
    {
        MessageRequested?.Invoke(this, new MessageEventArgs
        {
            Message = message,
            Type = MessageType.Success
        });
    }

    public void ShowError(string message)
    {
        MessageRequested?.Invoke(this, new MessageEventArgs
        {
            Message = message,
            Type = MessageType.Error
        });
    }

    public void ShowInfo(string message)
    {
        MessageRequested?.Invoke(this, new MessageEventArgs
        {
            Message = message,
            Type = MessageType.Info
        });
    }
}
