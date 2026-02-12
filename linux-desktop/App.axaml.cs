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
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SecurityService))]
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
        services.AddSingleton<ISecurityService, SecurityService>();
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
        var timeService = Services.GetRequiredService<ITimeService>();
        var serviceProviderRepository = Services.GetRequiredService<ServiceProviderRepository>();

        // 数据库初始化（必须最先完成）
        await storageService.InitializeAsync();

        // 预加载所有设置到缓存，后续 GetValueAsync 不再访问数据库
        await settingsRepository.PreloadAsync();

        // 时间服务初始化必须等（影响 OTP 计算）
        await timeService.InitializeAsync();

        // 内置服务商初始化不影响首屏，后台执行
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

        // 直接解锁（无安全设置，使用空字符串）
        await securityService.UnlockAsync(string.Empty);

        if (securityService.IsUnlocked)
        {
            await mainViewModel.LoadAsync();
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
