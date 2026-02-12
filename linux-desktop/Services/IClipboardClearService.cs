using System.Threading.Tasks;

namespace TwoFactorAuth.Services;

public interface IClipboardClearService
{
    bool IsEnabled { get; set; }
    int DelaySeconds { get; set; }
    
    void ScheduleClear(string code);
    void CancelScheduledClear();
}
