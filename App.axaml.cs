using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwoFactorAuth.Data;
using TwoFactorAuth.Services;
using TwoFactorAuth.ViewModels;
using TwoFactorAuth.Views;
using TwoFactorAuth.Views.Dialogs;

namespace TwoFactorAuth;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    public static Window? MainWindow { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Service configuration is AOT compatible with explicit type registration")]
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = ConfigureServices();
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            var clipboardClearService = Services.GetRequiredService<IClipboardClearService>();
            var messageService = Services.GetRequiredService<IMessageService>();
            mainWindow.SetClipboardClearService(clipboardClearService);
            mainWindow.SetMessageService(messageService);
            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;
            mainWindow.Opened += async (_, _) => await InitializeAsync(mainWindow, mainViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DatabaseContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AccountRepository))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CategoryRepository))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OperationLogRepository))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SettingsRepository))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ServiceProviderRepository))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ScreenshotProtectionService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SecurityService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(WindowsIdleTimeProvider))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(UnsupportedIdleTimeProvider))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AppLockCoordinator))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AutoLockManager))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StorageService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AccountService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NtpTimeProvider))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TimeService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OtpService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ClipboardClearService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MessageService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MainViewModel))]
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseContext>();
        services.AddSingleton<AccountRepository>();
        services.AddSingleton<CategoryRepository>();
        services.AddSingleton<OperationLogRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<ServiceProviderRepository>();
        services.AddSingleton<IScreenshotProtectionService, ScreenshotProtectionService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IIdleTimeProvider>(sp =>
            OperatingSystem.IsWindows()
                ? new WindowsIdleTimeProvider()
                : new UnsupportedIdleTimeProvider());
        services.AddSingleton<IAppLockCoordinator, AppLockCoordinator>();
        services.AddSingleton<IAutoLockManager, AutoLockManager>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<INtpTimeProvider, NtpTimeProvider>();
        services.AddSingleton<ITimeService, TimeService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IClipboardClearService, ClipboardClearService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }

    private static async Task InitializeAsync(Window mainWindow, MainViewModel mainViewModel)
    {
        var (isFirstRun, inferredLang, inferredTheme) = DetectFirstRun();

        var storageService = Services.GetRequiredService<IStorageService>();
        var securityService = Services.GetRequiredService<ISecurityService>();
        var settingsRepository = Services.GetRequiredService<SettingsRepository>();
        var screenshotProtectionService = Services.GetRequiredService<IScreenshotProtectionService>();
        var appLockCoordinator = Services.GetRequiredService<IAppLockCoordinator>();
        var autoLockManager = Services.GetRequiredService<IAutoLockManager>();
        var timeService = Services.GetRequiredService<ITimeService>();
        var serviceProviderRepository = Services.GetRequiredService<ServiceProviderRepository>();
        await storageService.InitializeAsync();
        await settingsRepository.PreloadAsync();
        await timeService.InitializeAsync();
        _ = serviceProviderRepository.InitializeBuiltInProvidersAsync();

        if (isFirstRun || await NeedsSetupAsync(settingsRepository))
        {
            var (selectedLang, selectedTheme) = await mainViewModel.StartSetupWizardAsync(inferredLang, inferredTheme);
            mainViewModel.EndSetupWizard();
            await settingsRepository.SetValueAsync("Language", selectedLang);
            await settingsRepository.SetValueAsync("Theme", selectedTheme);
            AppAppearance.ApplyLanguage(selectedLang);
            AppAppearance.ApplyTheme(selectedTheme);
        }
        else
        {
            var storedLang = await settingsRepository.GetValueAsync("Language");
            if (!string.IsNullOrWhiteSpace(storedLang))
            {
                AppAppearance.ApplyLanguage(storedLang);
            }
            var storedTheme = await settingsRepository.GetValueAsync("Theme");
            if (!string.IsNullOrWhiteSpace(storedTheme))
            {
                AppAppearance.ApplyTheme(storedTheme);
            }
        }

        var storedAppLock = await settingsRepository.GetValueAsync(SettingKeys.AppLockEnabled);
        var appLockEnabled = string.Equals(storedAppLock, "1", StringComparison.Ordinal) ||
                             bool.TryParse(storedAppLock, out var parsedAppLock) && parsedAppLock;

        var storedIdleAutoLock = await settingsRepository.GetValueAsync(SettingKeys.IdleAutoLockEnabled);
        var idleAutoLockEnabled = string.Equals(storedIdleAutoLock, "1", StringComparison.Ordinal) ||
                                  bool.TryParse(storedIdleAutoLock, out var parsedIdleAutoLock) && parsedIdleAutoLock;

        var storedAutoLockMinutes = await settingsRepository.GetValueAsync(SettingKeys.AutoLockMinutes);
        var autoLockMinutes = int.TryParse(storedAutoLockMinutes, out var minutes) ? minutes : 5;
        if (autoLockMinutes <= 0) autoLockMinutes = 5;

        if ((appLockEnabled || idleAutoLockEnabled) && !await securityService.HasMasterPasswordAsync())
        {
            appLockEnabled = false;
            idleAutoLockEnabled = false;
            await settingsRepository.SetValueAsync(SettingKeys.AppLockEnabled, "false");
            await settingsRepository.SetValueAsync(SettingKeys.IdleAutoLockEnabled, "false");
        }

        autoLockManager.UpdateConfiguration(appLockEnabled, idleAutoLockEnabled, autoLockMinutes);

        var hasMasterPassword = await securityService.HasMasterPasswordAsync();

        if (appLockEnabled)
        {
            await appLockCoordinator.EnsureUnlockedAsync(LockReason.Startup);
        }
        else if (idleAutoLockEnabled)
        {
            await securityService.UnlockOnStartupAsync();
        }
        else
        {
            await securityService.UnlockAsync(string.Empty);
        }

        if (securityService.IsUnlocked)
        {
            await mainViewModel.LoadAsync();

            var stored = await settingsRepository.GetValueAsync(SettingKeys.BlockScreenshots);
            var enabled = string.Equals(stored, "1", StringComparison.Ordinal) ||
                          bool.TryParse(stored, out var parsed) && parsed;
            screenshotProtectionService.ApplyTo(mainWindow, enabled);
        }
    }

    private static (bool IsFirstRun, string InferredLanguage, string InferredTheme) DetectFirstRun()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwoFactorAuth-Desktop");
        var dbPath = Path.Combine(root, "TwoFactorAuth.db");

        var missingRoot = !Directory.Exists(root);
        var missingDb = !File.Exists(dbPath);
        var emptyRoot = !missingRoot && !Directory.EnumerateFileSystemEntries(root).Any();

        var isFirstRun = missingRoot || missingDb || emptyRoot;
        return (isFirstRun, "en-US", "Light");
    }

    private static async Task<bool> NeedsSetupAsync(SettingsRepository settingsRepository)
    {
        var lang = await settingsRepository.GetValueAsync("Language");
        var theme = await settingsRepository.GetValueAsync("Theme");
        return string.IsNullOrWhiteSpace(lang) || string.IsNullOrWhiteSpace(theme);
    }
}
