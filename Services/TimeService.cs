using System;
using System.Threading.Tasks;
using TwoFactorAuth.Data;

namespace TwoFactorAuth.Services;

public enum TimeSource
{
    System,
    Ntp
}

public interface ITimeService
{
    DateTime UtcNow { get; }
    Task InitializeAsync();
    Task UpdateTimeOffsetAsync();
}

public sealed class TimeService : ITimeService
{
    private readonly SettingsRepository _settingsRepository;
    private readonly INtpTimeProvider _ntpTimeProvider;
    private TimeSpan _timeOffset = TimeSpan.Zero;
    private TimeSource _timeSource = TimeSource.System;
    private string _ntpServer = "time.windows.com";

    public TimeService(SettingsRepository settingsRepository, INtpTimeProvider ntpTimeProvider)
    {
        _settingsRepository = settingsRepository;
        _ntpTimeProvider = ntpTimeProvider;
    }

    public DateTime UtcNow
    {
        get
        {
            var systemTime = DateTime.UtcNow;
            return _timeSource == TimeSource.Ntp ? systemTime.Add(_timeOffset) : systemTime;
        }
    }

    public async Task InitializeAsync()
    {
        var timeSourceStr = await _settingsRepository.GetValueAsync(SettingKeys.TimeSource);
        if (Enum.TryParse<TimeSource>(timeSourceStr, out var source))
        {
            _timeSource = source;
        }

        var ntpServer = await _settingsRepository.GetValueAsync(SettingKeys.NtpServer);
        if (!string.IsNullOrEmpty(ntpServer))
        {
            _ntpServer = ntpServer;
        }

        if (_timeSource == TimeSource.Ntp)
        {
            _ = Task.Run(async () =>
            {
                try { await UpdateTimeOffsetAsync(); }
                catch {  }
            });
        }
    }

    public async Task UpdateTimeOffsetAsync()
    {
        if (_timeSource != TimeSource.Ntp)
            return;

        var offset = await _ntpTimeProvider.GetTimeOffsetAsync(_ntpServer);
        if (offset.HasValue)
        {
            _timeOffset = offset.Value;
        }
    }
}
