using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TwoFactorAuth.Data;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public class NtpServerOption
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}

public sealed class TimeSettingsViewModel : ViewModelBase
{
    private const int TestTimeoutMs = 10_000;
    private enum TestStatus
    {
        None,
        SelectServer,
        EnterServer,
        Testing,
        Success,
        Failed,
        Error
    }
    private readonly SettingsRepository _settingsRepository;
    private readonly INtpTimeProvider _ntpTimeProvider;
    
    private bool _useSystemTime = true;
    private bool _useNtpTime;
    private NtpServerOption? _selectedNtpServer;
    private string _customNtpServer = string.Empty;
    private string _testResult = string.Empty;
    private bool _isTesting;
    private TestStatus _testStatus = TestStatus.None;
    private DateTime? _lastNetworkTimeUtc;
    private double? _lastOffsetSeconds;
    private string _lastErrorMessage = string.Empty;

    public TimeSettingsViewModel(SettingsRepository settingsRepository, INtpTimeProvider ntpTimeProvider)
    {
        _settingsRepository = settingsRepository;
        _ntpTimeProvider = ntpTimeProvider;
        
        InitializeNtpServers();
    }

    public ObservableCollection<NtpServerOption> NtpServers { get; } = new();

    public bool UseSystemTime
    {
        get => _useSystemTime;
        set
        {
            if (SetField(ref _useSystemTime, value) && value)
            {
                UseNtpTime = false;
                _ = SaveTimeSourceAsync();
            }
        }
    }

    public bool UseNtpTime
    {
        get => _useNtpTime;
        set
        {
            if (SetField(ref _useNtpTime, value) && value)
            {
                UseSystemTime = false;
                _ = SaveTimeSourceAsync();
            }
        }
    }

    public NtpServerOption? SelectedNtpServer
    {
        get => _selectedNtpServer;
        set
        {
            if (SetField(ref _selectedNtpServer, value))
            {
                _ = SaveNtpServerAsync();
            }
        }
    }

    public string CustomNtpServer
    {
        get => _customNtpServer;
        set => SetField(ref _customNtpServer, value);
    }

    public string TestResult
    {
        get => _testResult;
        set => SetField(ref _testResult, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        set => SetField(ref _isTesting, value);
    }

    private void InitializeNtpServers()
    {
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.WindowsTime", "Windows Time (Recommended)"), Address = "time.windows.com" });
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.Aliyun", "Aliyun NTP"), Address = "ntp.aliyun.com" });
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.Tencent", "Tencent Cloud NTP"), Address = "ntp.tencent.com" });
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.Google", "Google NTP"), Address = "time.google.com" });
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.Cloudflare", "Cloudflare NTP"), Address = "time.cloudflare.com" });
        NtpServers.Add(new NtpServerOption { Name = GetLocalizedString("Lang.TimeSettings.NtpServer.Custom", "Custom Server"), Address = "", IsCustom = true });
    }

    private string GetLocalizedString(string key, string fallback)
    {
        if (Avalonia.Application.Current?.TryGetResource(key, null, out var resource) == true 
            && resource is string text)
        {
            return text;
        }
        return fallback;
    }

    public async Task LoadSettingsAsync()
    {
        var timeSource = await _settingsRepository.GetValueAsync(SettingKeys.TimeSource);
        if (timeSource == "Ntp")
        {
            UseNtpTime = true;
            UseSystemTime = false;
        }
        else
        {
            UseSystemTime = true;
            UseNtpTime = false;
        }

        var ntpServer = await _settingsRepository.GetValueAsync(SettingKeys.NtpServer);
        if (!string.IsNullOrEmpty(ntpServer))
        {
            var predefined = NtpServers.FirstOrDefault(s => s.Address == ntpServer && !s.IsCustom);
            if (predefined != null)
            {
                SelectedNtpServer = predefined;
            }
            else
            {
                CustomNtpServer = ntpServer;
                SelectedNtpServer = NtpServers.FirstOrDefault(s => s.IsCustom);
            }
        }
        else
        {
            SelectedNtpServer = NtpServers.FirstOrDefault();
        }

        var customServers = await _settingsRepository.GetValueAsync(SettingKeys.CustomNtpServers);
        if (!string.IsNullOrEmpty(customServers))
        {
            var servers = customServers.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var server in servers)
            {
                if (!NtpServers.Any(s => s.Address == server))
                {
                    var customLabel = GetLocalizedString("Lang.TimeSettings.NtpServer.CustomPrefix", "Custom");
                    NtpServers.Insert(NtpServers.Count - 1, new NtpServerOption 
                    { 
                        Name = $"{customLabel}: {server}", 
                        Address = server,
                        IsCustom = false
                    });
                }
            }
        }
    }

    public void RefreshServerNames()
    {
        var selectedAddress = SelectedNtpServer?.Address;
        NtpServers.Clear();
        InitializeNtpServers();
        if (!string.IsNullOrEmpty(selectedAddress))
        {
            SelectedNtpServer = NtpServers.FirstOrDefault(s => s.Address == selectedAddress);
        }
    }

    public void OnLanguageChanged()
    {
        RefreshServerNames();
        TestResult = BuildTestResult();
    }

    private void SetTestStatus(TestStatus status)
    {
        _testStatus = status;
        TestResult = BuildTestResult();
    }

    private string BuildTestResult()
    {
        switch (_testStatus)
        {
            case TestStatus.SelectServer:
                return GetLocalizedString("Lang.TimeSettings.TestSelectServer", "Please select an NTP server");
            case TestStatus.EnterServer:
                return GetLocalizedString("Lang.TimeSettings.TestEnterServer", "Please enter NTP server address");
            case TestStatus.Testing:
                return GetLocalizedString("Lang.TimeSettings.Testing", "Testing connection...");
            case TestStatus.Success:
                return BuildSuccessResult();
            case TestStatus.Failed:
                return GetLocalizedString("Lang.TimeSettings.TestFailed", "Test failed. Please check the server address or network connection.");
            case TestStatus.Error:
                var errorTemplate = GetLocalizedString("Lang.TimeSettings.TestErrorFormat", "Test failed: {0}");
                return string.Format(errorTemplate, _lastErrorMessage);
            default:
                return string.Empty;
        }
    }

    private string BuildSuccessResult()
    {
        var template = GetLocalizedString(
            "Lang.TimeSettings.TestSuccessFormat",
            "Connection successful!\nNetwork time: {0} UTC\nTime offset: {1} seconds");
        var timeText = _lastNetworkTimeUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
        var offsetText = _lastOffsetSeconds?.ToString("F2") ?? "0";
        return string.Format(template, timeText, offsetText);
    }

    private async Task SaveTimeSourceAsync()
    {
        var source = UseNtpTime ? "Ntp" : "System";
        await _settingsRepository.SetValueAsync(SettingKeys.TimeSource, source);
    }

    private async Task SaveNtpServerAsync()
    {
        if (SelectedNtpServer == null) return;

        var address = SelectedNtpServer.IsCustom ? CustomNtpServer : SelectedNtpServer.Address;
        if (!string.IsNullOrEmpty(address))
        {
            await _settingsRepository.SetValueAsync(SettingKeys.NtpServer, address);
        }
    }

    public async Task TestNtpConnectionAsync()
    {
        if (SelectedNtpServer == null)
        {
            SetTestStatus(TestStatus.SelectServer);
            return;
        }

        var address = SelectedNtpServer.IsCustom ? CustomNtpServer : SelectedNtpServer.Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            SetTestStatus(TestStatus.EnterServer);
            return;
        }

        IsTesting = true;
        SetTestStatus(TestStatus.Testing);

        try
        {
            _lastErrorMessage = string.Empty;
            _lastNetworkTimeUtc = null;
            _lastOffsetSeconds = null;

            var networkTimeTask = _ntpTimeProvider.GetNetworkTimeAsync(address, TestTimeoutMs);
            var completedTask = await Task.WhenAny(networkTimeTask, Task.Delay(TestTimeoutMs));
            if (completedTask != networkTimeTask)
            {
                _ = networkTimeTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                SetTestStatus(TestStatus.Failed);
                return;
            }

            var networkTime = await networkTimeTask;
            if (networkTime.HasValue)
            {
                var offset = networkTime.Value - DateTime.UtcNow;
                _lastNetworkTimeUtc = networkTime.Value;
                _lastOffsetSeconds = offset.TotalSeconds;
                SetTestStatus(TestStatus.Success);
            }
            else
            {
                SetTestStatus(TestStatus.Failed);
            }
        }
        catch (Exception ex)
        {
            _lastErrorMessage = ex.Message;
            SetTestStatus(TestStatus.Error);
        }
        finally
        {
            IsTesting = false;
        }
    }

    public async Task AddCustomServerAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomNtpServer))
            return;

        if (NtpServers.Any(s => s.Address == CustomNtpServer))
            return;

        var customLabel = GetLocalizedString("Lang.TimeSettings.NtpServer.CustomPrefix", "Custom");
        var newServer = new NtpServerOption
        {
            Name = $"{customLabel}: {CustomNtpServer}",
            Address = CustomNtpServer,
            IsCustom = false
        };

        NtpServers.Insert(NtpServers.Count - 1, newServer);
        
        var customServers = await _settingsRepository.GetValueAsync(SettingKeys.CustomNtpServers) ?? string.Empty;
        var serverList = customServers.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!serverList.Contains(CustomNtpServer))
        {
            serverList.Add(CustomNtpServer);
            await _settingsRepository.SetValueAsync(SettingKeys.CustomNtpServers, string.Join(";", serverList));
        }

        SelectedNtpServer = newServer;
        CustomNtpServer = string.Empty;
    }
}
