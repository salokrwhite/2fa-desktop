using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TwoFactorAuth.Services;

public sealed class ClipboardClearService : IClipboardClearService
{
    private readonly CancellationTokenSource _cts = new();
    private string? _lastCopiedCode;
    private CancellationTokenSource? _currentTimerCts;
    private readonly object _lock = new();

    public bool IsEnabled { get; set; }
    public int DelaySeconds { get; set; } = 30;

    public void ScheduleClear(string code)
    {
        if (!IsEnabled || string.IsNullOrEmpty(code))
            return;

        lock (_lock)
        {
            _currentTimerCts?.Cancel();
            _currentTimerCts?.Dispose();
            _currentTimerCts = new CancellationTokenSource();
            
            _lastCopiedCode = code;
            
            var token = _currentTimerCts.Token;
            _ = ClearClipboardAfterDelayAsync(code, DelaySeconds, token);
        }
    }

    public void CancelScheduledClear()
    {
        lock (_lock)
        {
            _currentTimerCts?.Cancel();
            _currentTimerCts?.Dispose();
            _currentTimerCts = null;
            _lastCopiedCode = null;
        }
    }

    private async Task ClearClipboardAfterDelayAsync(string code, int delaySeconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delaySeconds * 1000, cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    Avalonia.Input.Platform.IClipboard? clipboard = null;
                    
                    if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        clipboard = desktop.MainWindow?.Clipboard;
                    }
                    
                    if (clipboard == null)
                    {
                        var topLevel = TopLevel.GetTopLevel(App.MainWindow);
                        clipboard = topLevel?.Clipboard;
                    }

                    if (clipboard == null)
                        return;

#pragma warning disable CS0618
                    var currentText = await clipboard.GetTextAsync();
#pragma warning restore CS0618
                    
                    if (currentText == code)
                    {
                        await clipboard.ClearAsync();
                    }
                }
                catch
                {

                }
            });
        }
        catch (OperationCanceledException)
        {
            
        }
        catch
        {
            
        }
    }
}
