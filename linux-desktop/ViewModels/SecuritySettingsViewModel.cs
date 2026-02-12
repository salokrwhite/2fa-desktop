using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using TwoFactorAuth.Data;
using TwoFactorAuth.Services;
using TwoFactorAuth.Views.Dialogs;

namespace TwoFactorAuth.ViewModels;

public sealed class SecuritySettingsViewModel : ViewModelBase
{
    private readonly SettingsRepository _settingsRepository;
    private readonly IScreenshotProtectionService _screenshotProtectionService;
    private readonly ISecurityService _securityService;
    private readonly IStorageService _storageService;
    private readonly IAutoLockManager _autoLockManager;
    private readonly IIdleTimeProvider _idleTimeProvider;
    private readonly IClipboardClearService _clipboardClearService;
    private readonly IMessageService _messageService;

    private bool _isInternalUpdate;
    private bool _blockScreenshots;
    private bool _appLockEnabled;
    private bool _idleAutoLockEnabled;
    private int _autoLockMinutes = 5;
    private string _screenshotBlockHint = string.Empty;
    private string _appLockHint = string.Empty;
    private string _idleAutoLockHint = string.Empty;
    private string _masterPasswordHint = string.Empty;
    private bool _clipboardClearEnabled;
    private int _clipboardClearDelaySeconds = 30;
    private ScreenshotProtectionApplyStatus _lastScreenshotStatus = ScreenshotProtectionApplyStatus.NotSupported;
    private bool _hasMasterPassword;

    public string Title => "安全设置";

    public ObservableCollection<AutoLockMinuteOption> AutoLockMinuteOptions { get; } = new()
    {
        new AutoLockMinuteOption(1),
        new AutoLockMinuteOption(2),
        new AutoLockMinuteOption(5),
        new AutoLockMinuteOption(10),
        new AutoLockMinuteOption(15),
        new AutoLockMinuteOption(30)
    };

    public ObservableCollection<ClipboardClearDelayOption> ClipboardClearDelayOptions { get; } = new()
    {
        new ClipboardClearDelayOption(5),
        new ClipboardClearDelayOption(10),
        new ClipboardClearDelayOption(30),
        new ClipboardClearDelayOption(60)
    };

    public bool AppLockEnabled
    {
        get => _appLockEnabled;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _appLockEnabled, value);
                return;
            }

            if (SetField(ref _appLockEnabled, value))
            {
                _ = OnAppLockToggledAsync(value);
            }
        }
    }

    public bool IdleAutoLockEnabled
    {
        get => _idleAutoLockEnabled;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _idleAutoLockEnabled, value);
                return;
            }

            if (SetField(ref _idleAutoLockEnabled, value))
            {
                _ = OnIdleAutoLockToggledAsync(value);
            }
        }
    }

    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _autoLockMinutes, value);
                return;
            }

            if (SetField(ref _autoLockMinutes, value))
            {
                _ = _settingsRepository.SetValueAsync(SettingKeys.AutoLockMinutes, value.ToString());
                _autoLockManager.UpdateConfiguration(AppLockEnabled, IdleAutoLockEnabled, value);
            }
        }
    }

    public string AppLockHint
    {
        get => _appLockHint;
        private set => SetField(ref _appLockHint, value);
    }

    public bool HasMasterPassword
    {
        get => _hasMasterPassword;
        private set => SetField(ref _hasMasterPassword, value);
    }

    public string MasterPasswordHint
    {
        get => _masterPasswordHint;
        private set => SetField(ref _masterPasswordHint, value);
    }

    public string IdleAutoLockHint
    {
        get => _idleAutoLockHint;
        private set => SetField(ref _idleAutoLockHint, value);
    }

    public bool BlockScreenshots
    {
        get => _blockScreenshots;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _blockScreenshots, value);
                return;
            }

            if (SetField(ref _blockScreenshots, value))
            {
                _ = PersistAndApplyAsync(value);
            }
        }
    }

    public string ScreenshotBlockHint
    {
        get => _screenshotBlockHint;
        private set => SetField(ref _screenshotBlockHint, value);
    }

    public bool ClipboardClearEnabled
    {
        get => _clipboardClearEnabled;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _clipboardClearEnabled, value);
                return;
            }

            if (SetField(ref _clipboardClearEnabled, value))
            {
                _ = OnClipboardClearToggledAsync(value);
            }
        }
    }

    public int ClipboardClearDelaySeconds
    {
        get => _clipboardClearDelaySeconds;
        set
        {
            if (_isInternalUpdate)
            {
                SetField(ref _clipboardClearDelaySeconds, value);
                return;
            }

            if (SetField(ref _clipboardClearDelaySeconds, value))
            {
                _ = _settingsRepository.SetValueAsync(SettingKeys.ClipboardClearDelaySeconds, value.ToString());
                _clipboardClearService.DelaySeconds = value;
            }
        }
    }

    public SecuritySettingsViewModel(
        SettingsRepository settingsRepository,
        IScreenshotProtectionService screenshotProtectionService,
        ISecurityService securityService,
        IStorageService storageService,
        IAutoLockManager autoLockManager,
        IIdleTimeProvider idleTimeProvider,
        IClipboardClearService clipboardClearService,
        IMessageService messageService)
    {
        _settingsRepository = settingsRepository;
        _screenshotProtectionService = screenshotProtectionService;
        _securityService = securityService;
        _storageService = storageService;
        _autoLockManager = autoLockManager;
        _idleTimeProvider = idleTimeProvider;
        _clipboardClearService = clipboardClearService;
        _messageService = messageService;

        SetMasterPasswordCommand = new RelayCommand(SetMasterPasswordAsync);
        RemoveMasterPasswordCommand = new RelayCommand(RemoveMasterPasswordAsync);
    }

    public ICommand SetMasterPasswordCommand { get; }
    public ICommand RemoveMasterPasswordCommand { get; }

    public async Task InitializeAsync()
    {
        var storedAppLock = await _settingsRepository.GetValueAsync(SettingKeys.AppLockEnabled);
        var appLockEnabled = ParseBool(storedAppLock);

        var storedIdleAutoLock = await _settingsRepository.GetValueAsync(SettingKeys.IdleAutoLockEnabled);
        var idleAutoLockEnabled = ParseBool(storedIdleAutoLock);

        var storedAutoLockMinutes = await _settingsRepository.GetValueAsync(SettingKeys.AutoLockMinutes);
        var autoLockMinutes = int.TryParse(storedAutoLockMinutes, out var minutes) ? minutes : 5;
        if (autoLockMinutes <= 0) autoLockMinutes = 5;

        if (!_idleTimeProvider.IsSupported)
        {
            idleAutoLockEnabled = false;
            _ = _settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "false");
            IdleAutoLockHint = GetLang("Lang.Security.IdleAutoLockUnsupported") ?? "当前平台不支持全局空闲检测";
        }
        else
        {
            IdleAutoLockHint = string.Empty;
        }

        var hasMasterPassword = await _securityService.HasMasterPasswordAsync();

        if (!hasMasterPassword)
        {
            if (appLockEnabled)
            {
                appLockEnabled = false;
                await _settingsRepository.SetValueAsync(SettingKeys.AppLockEnabled, "false");
            }
            if (idleAutoLockEnabled)
            {
                idleAutoLockEnabled = false;
                await _settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "false");
            }
        }

        var stored = await _settingsRepository.GetValueAsync(SettingKeys.BlockScreenshots);
        var enabled = string.Equals(stored, "1", StringComparison.Ordinal) ||
                      bool.TryParse(stored, out var parsed) && parsed;

        var storedClipboardClear = await _settingsRepository.GetValueAsync(SettingKeys.ClipboardClearEnabled);
        var clipboardClearEnabled = ParseBool(storedClipboardClear);

        var storedClipboardDelay = await _settingsRepository.GetValueAsync(SettingKeys.ClipboardClearDelaySeconds);
        var clipboardClearDelay = int.TryParse(storedClipboardDelay, out var delay) ? delay : 30;
        if (clipboardClearDelay <= 0) clipboardClearDelay = 30;

        _isInternalUpdate = true;
        try
        {
            _appLockEnabled = appLockEnabled;
            RaisePropertyChanged(nameof(AppLockEnabled));

            _idleAutoLockEnabled = idleAutoLockEnabled;
            RaisePropertyChanged(nameof(IdleAutoLockEnabled));

            _autoLockMinutes = autoLockMinutes;
            RaisePropertyChanged(nameof(AutoLockMinutes));

            _blockScreenshots = enabled;
            RaisePropertyChanged(nameof(BlockScreenshots));

            _clipboardClearEnabled = clipboardClearEnabled;
            RaisePropertyChanged(nameof(ClipboardClearEnabled));

            _clipboardClearDelaySeconds = clipboardClearDelay;
            RaisePropertyChanged(nameof(ClipboardClearDelaySeconds));

            _hasMasterPassword = hasMasterPassword;
            RaisePropertyChanged(nameof(HasMasterPassword));
        }
        finally
        {
            _isInternalUpdate = false;
        }

        _autoLockManager.UpdateConfiguration(appLockEnabled, idleAutoLockEnabled, autoLockMinutes);
        _clipboardClearService.IsEnabled = clipboardClearEnabled;
        _clipboardClearService.DelaySeconds = clipboardClearDelay;
        await ApplyAsync(enabled, persist: false);
    }

    private async Task PersistAndApplyAsync(bool enabled)
    {
        await _settingsRepository.SetValueAsync(SettingKeys.BlockScreenshots, enabled ? "true" : "false");
        await ApplyAsync(enabled, persist: true);
    }

    private async Task ApplyAsync(bool enabled, bool persist)
    {
        var window = TwoFactorAuth.App.MainWindow;
        if (window == null)
        {
            ScreenshotBlockHint = string.Empty;
            return;
        }

        var result = _screenshotProtectionService.ApplyTo(window, enabled);
        _lastScreenshotStatus = result.Status;

        if (!enabled)
        {
            ScreenshotBlockHint = string.Empty;
            return;
        }

        if (result.Status == ScreenshotProtectionApplyStatus.Applied)
        {
            ScreenshotBlockHint = GetLang("Lang.Security.BlockScreenshotsHintSupported") ?? string.Empty;
            return;
        }

        if (persist)
        {
            await _settingsRepository.SetValueAsync(SettingKeys.BlockScreenshots, "false");
        }

        var hint = result.Status == ScreenshotProtectionApplyStatus.NotSupported
            ? GetLang("Lang.Security.BlockScreenshotsHintUnsupported")
            : string.Format(GetLang("Lang.Security.BlockScreenshotsHintFailedFormat") ?? "Failed: {0}", result.Win32Error);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _isInternalUpdate = true;
            try
            {
                BlockScreenshots = false;
                ScreenshotBlockHint = hint ?? string.Empty;
            }
            finally
            {
                _isInternalUpdate = false;
            }
        });
    }

    private static string? GetLang(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }

        return null;
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (string.Equals(value, "1", StringComparison.Ordinal)) return true;
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private async Task OnAppLockToggledAsync(bool enabled)
    {
        AppLockHint = string.Empty;

        if (enabled)
        {
            if (!await _securityService.HasMasterPasswordAsync())
            {
                var owner = TwoFactorAuth.App.MainWindow;
                if (owner == null)
                {
                    RevertAppLock(false);
                    return;
                }

                var dialog = new SetPasswordDialog(_ => { });
                var newPassword = await dialog.ShowDialog<string?>(owner);
                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    RevertAppLock(false);
                    return;
                }

                try
                {
                    await _storageService.RotateAccountSecretsAsync(string.Empty, newPassword);
                    await _securityService.SetMasterPasswordAsync(newPassword);
                    HasMasterPassword = true;
                }
                catch
                {
                    try { await _securityService.ClearMasterPasswordAsync(); } catch { /* best effort */ }
                    RevertAppLock(false);
                    AppLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "设置密码失败";
                    return;
                }
            }
            else
            {
                try
                {
                    var password = _securityService.GetSessionPassword();
                    if (string.IsNullOrEmpty(password))
                    {
                        RevertAppLock(false);
                        AppLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "无法获取会话密码";
                        return;
                    }
                    await _storageService.RotateAccountSecretsAsync(string.Empty, password);
                }
                catch
                {
                    RevertAppLock(false);
                    AppLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "数据迁移失败";
                    return;
                }
            }

            await _settingsRepository.SetValueAsync(SettingKeys.AppLockEnabled, "true");
            _autoLockManager.UpdateConfiguration(true, IdleAutoLockEnabled, AutoLockMinutes);
            return;
        }

        if (IdleAutoLockEnabled)
        {
            if (await _securityService.HasMasterPasswordAsync())
            {
                var owner = TwoFactorAuth.App.MainWindow;
                if (owner == null)
                {
                    RevertAppLock(true);
                    return;
                }

                var passwordDialog = new PasswordDialog(pwd => _securityService.UnlockAsync(pwd), vm =>
                {
                    vm.Title = GetLang("Lang.Security.ConfirmPasswordTitle") ?? "确认密码";
                    vm.Prompt = GetLang("Lang.Security.EnterPasswordToDisableLock") ?? "请输入主密码以关闭应用锁定";
                    vm.CancelText = GetLang("Lang.Cancel") ?? vm.CancelText;
                    vm.ConfirmText = GetLang("Lang.Confirm") ?? vm.ConfirmText;
                });

                var password = await passwordDialog.ShowDialog<string?>(owner);
                if (password == null)
                {
                    RevertAppLock(true);
                    return;
                }
            }

            await _settingsRepository.SetValueAsync(SettingKeys.AppLockEnabled, "false");
            _autoLockManager.UpdateConfiguration(false, true, AutoLockMinutes);
            return;
        }

        if (await _securityService.HasMasterPasswordAsync())
        {
            var owner2 = TwoFactorAuth.App.MainWindow;
            if (owner2 == null)
            {
                RevertAppLock(true);
                return;
            }

            var passwordDialog = new PasswordDialog(pwd => _securityService.UnlockAsync(pwd), vm =>
            {
                vm.Title = GetLang("Lang.Security.ConfirmPasswordTitle") ?? "确认密码";
                vm.Prompt = GetLang("Lang.Security.EnterPasswordToDisableLock") ?? "请输入主密码以关闭应用锁定";
                vm.CancelText = GetLang("Lang.Cancel") ?? vm.CancelText;
                vm.ConfirmText = GetLang("Lang.Confirm") ?? vm.ConfirmText;
            });

            var oldPassword = await passwordDialog.ShowDialog<string?>(owner2);
            if (oldPassword == null)
            {
                RevertAppLock(true);
                return;
            }

            try
            {
                await _storageService.RotateAccountSecretsAsync(oldPassword, string.Empty);
                await _securityService.ClearMasterPasswordAsync();
                HasMasterPassword = false;
            }
            catch
            {
                RevertAppLock(true);
                AppLockHint = GetLang("Lang.Security.RemovePasswordFailed") ?? "移除密码失败";
                return;
            }
        }

        await _settingsRepository.SetValueAsync(SettingKeys.AppLockEnabled, "false");
        _autoLockManager.UpdateConfiguration(false, false, AutoLockMinutes);
    }

    private async Task OnIdleAutoLockToggledAsync(bool enabled)
    {
        if (!_idleTimeProvider.IsSupported)
        {
            RevertIdleAutoLock(false);
            return;
        }

        if (enabled)
        {
            if (!await _securityService.HasMasterPasswordAsync())
            {
                var owner = TwoFactorAuth.App.MainWindow;
                if (owner == null)
                {
                    RevertIdleAutoLock(false);
                    return;
                }

                var dialog = new SetPasswordDialog(_ => { });
                var newPassword = await dialog.ShowDialog<string?>(owner);
                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    RevertIdleAutoLock(false);
                    return;
                }

                try
                {
                    await _storageService.RotateAccountSecretsAsync(string.Empty, newPassword);
                    await _securityService.SetMasterPasswordAsync(newPassword);
                    HasMasterPassword = true;
                }
                catch
                {
                    try { await _securityService.ClearMasterPasswordAsync(); } catch { /* best effort */ }
                    RevertIdleAutoLock(false);
                    IdleAutoLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "设置密码失败";
                    return;
                }
            }
            else
            {
                try
                {
                    var password = _securityService.GetSessionPassword();
                    if (string.IsNullOrEmpty(password))
                    {
                        RevertIdleAutoLock(false);
                        IdleAutoLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "无法获取会话密码";
                        return;
                    }
                    await _storageService.RotateAccountSecretsAsync(string.Empty, password);
                }
                catch
                {
                    RevertIdleAutoLock(false);
                    IdleAutoLockHint = GetLang("Lang.Security.SetPasswordFailed") ?? "数据迁移失败";
                    return;
                }
            }

            await _settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "true");
            _autoLockManager.UpdateConfiguration(AppLockEnabled, true, AutoLockMinutes);
            return;
        }

        if (AppLockEnabled)
        {
            if (await _securityService.HasMasterPasswordAsync())
            {
                var owner = TwoFactorAuth.App.MainWindow;
                if (owner == null)
                {
                    RevertIdleAutoLock(true);
                    return;
                }

                var passwordDialog = new PasswordDialog(pwd => _securityService.UnlockAsync(pwd), vm =>
                {
                    vm.Title = GetLang("Lang.Security.ConfirmPasswordTitle") ?? "确认密码";
                    vm.Prompt = GetLang("Lang.Security.EnterPasswordToDisableLock") ?? "请输入主密码以关闭空闲自动锁定";
                    vm.CancelText = GetLang("Lang.Cancel") ?? vm.CancelText;
                    vm.ConfirmText = GetLang("Lang.Confirm") ?? vm.ConfirmText;
                });

                var password = await passwordDialog.ShowDialog<string?>(owner);
                if (password == null)
                {
                    RevertIdleAutoLock(true);
                    return;
                }
            }

            await _settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "false");
            _autoLockManager.UpdateConfiguration(true, false, AutoLockMinutes);
            return;
        }

        if (await _securityService.HasMasterPasswordAsync())
        {
            var ownerWindow = TwoFactorAuth.App.MainWindow;
            if (ownerWindow == null)
            {
                RevertIdleAutoLock(true);
                return;
            }

            var passwordDialog = new PasswordDialog(pwd => _securityService.UnlockAsync(pwd), vm =>
            {
                vm.Title = GetLang("Lang.Security.ConfirmPasswordTitle") ?? "确认密码";
                vm.Prompt = GetLang("Lang.Security.EnterPasswordToDisableLock") ?? "请输入主密码以关闭空闲自动锁定";
                vm.CancelText = GetLang("Lang.Cancel") ?? vm.CancelText;
                vm.ConfirmText = GetLang("Lang.Confirm") ?? vm.ConfirmText;
            });

            var oldPassword = await passwordDialog.ShowDialog<string?>(ownerWindow);
            if (oldPassword == null)
            {
                RevertIdleAutoLock(true);
                return;
            }

            try
            {
                await _storageService.RotateAccountSecretsAsync(oldPassword, string.Empty);
                await _securityService.ClearMasterPasswordAsync();
                HasMasterPassword = false;
            }
            catch
            {
                RevertIdleAutoLock(true);
                IdleAutoLockHint = GetLang("Lang.Security.RemovePasswordFailed") ?? "移除密码失败";
                return;
            }
        }

        await _settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "false");
        _autoLockManager.UpdateConfiguration(false, false, AutoLockMinutes);
    }

    private void RevertAppLock(bool value)
    {
        _isInternalUpdate = true;
        try
        {
            AppLockEnabled = value;
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private void RevertIdleAutoLock(bool value)
    {
        _isInternalUpdate = true;
        try
        {
            IdleAutoLockEnabled = value;
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private async Task OnClipboardClearToggledAsync(bool enabled)
    {
        await _settingsRepository.SetValueAsync(SettingKeys.ClipboardClearEnabled, enabled ? "true" : "false");
        _clipboardClearService.IsEnabled = enabled;
    }

    private async Task SetMasterPasswordAsync()
    {
        MasterPasswordHint = string.Empty;

        var owner = TwoFactorAuth.App.MainWindow;
        if (owner == null) return;

        if (HasMasterPassword) return;

        var dialog = new SetPasswordDialog(_ => { });
        var newPassword = await dialog.ShowDialog<string?>(owner);
        if (string.IsNullOrWhiteSpace(newPassword)) return;

        try
        {
            await _securityService.SetMasterPasswordAsync(newPassword);
            HasMasterPassword = true;
        }
        catch
        {
            try { await _securityService.ClearMasterPasswordAsync(); } catch { /* best effort */ }
            MasterPasswordHint = GetLang("Lang.Security.SetPasswordFailed") ?? "设置密码失败";
        }
    }

    private async Task RemoveMasterPasswordAsync()
    {
        MasterPasswordHint = string.Empty;

        if (!HasMasterPassword) return;

        var owner = TwoFactorAuth.App.MainWindow;
        if (owner == null) return;

        if (AppLockEnabled || IdleAutoLockEnabled)
        {
            _messageService.ShowWarning(GetLang("Lang.Security.CannotRemovePasswordWithLockEnabled") ?? "请先禁用所有锁定功能，再移除主密码");
            return;
        }

        var passwordDialog = new PasswordDialog(pwd => _securityService.UnlockAsync(pwd), vm =>
        {
            vm.Title = GetLang("Lang.Security.RemoveMasterPasswordTitle") ?? vm.Title;
            vm.Prompt = GetLang("Lang.Security.CurrentPasswordPrompt") ?? vm.Prompt;
            vm.CancelText = GetLang("Lang.Cancel") ?? vm.CancelText;
            vm.ConfirmText = GetLang("Lang.Confirm") ?? vm.ConfirmText;
        });

        var oldPassword = await passwordDialog.ShowDialog<string?>(owner);
        if (oldPassword == null) return;

        try
        {
            var valid = await _securityService.UnlockAsync(oldPassword);
            if (!valid)
            {
                MasterPasswordHint = GetLang("Lang.Security.PasswordIncorrect") ?? "密码错误";
                return;
            }

            await _securityService.ClearMasterPasswordAsync();
            HasMasterPassword = false;
        }
        catch
        {
            MasterPasswordHint = GetLang("Lang.Security.RemovePasswordFailed") ?? "移除密码失败";
        }
    }

    public void OnLanguageChanged()
    {
        if (!_idleTimeProvider.IsSupported)
        {
            IdleAutoLockHint = GetLang("Lang.Security.IdleAutoLockUnsupported") ?? "当前平台不支持全局空闲检测";
        }

        if (BlockScreenshots)
        {
            switch (_lastScreenshotStatus)
            {
                case ScreenshotProtectionApplyStatus.Applied:
                    ScreenshotBlockHint = GetLang("Lang.Security.BlockScreenshotsHintSupported") ?? string.Empty;
                    break;
                case ScreenshotProtectionApplyStatus.NotSupported:
                    ScreenshotBlockHint = GetLang("Lang.Security.BlockScreenshotsHintUnsupported") ?? string.Empty;
                    break;
                default:
                    ScreenshotBlockHint = string.Empty;
                    break;
            }
        }
    }
}
