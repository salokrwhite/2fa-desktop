using Avalonia;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;

namespace TwoFactorAuth;

class Program
{
    [STAThread]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(App))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DatabaseContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OperationLog))]
    public static void Main(string[] args)
    {
        if (args.Contains("--dump-operationlogs", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--add-test-operationlog", StringComparer.OrdinalIgnoreCase))
        {
            RunOperationLogCliAsync(args).GetAwaiter().GetResult();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Avalonia platform detection is AOT compatible")]
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static async System.Threading.Tasks.Task RunOperationLogCliAsync(string[] args)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwoFactorAuth-Desktop");
        Directory.CreateDirectory(root);
        var outFile = Path.Combine(root, "operationlogs_cli.txt");

        var dbContext = new DatabaseContext();
        await dbContext.InitializeAsync();
        var repo = new OperationLogRepository(dbContext);

        if (args.Contains("--add-test-operationlog", StringComparer.OrdinalIgnoreCase))
        {
            await repo.AddAsync(new OperationLog
            {
                Operation = "op.test_log",
                Target = "CLI",
                Details = DateTime.UtcNow.ToString("O")
            });
        }

        if (args.Contains("--dump-operationlogs", StringComparer.OrdinalIgnoreCase))
        {
            var logs = await repo.GetAllAsync();
            await File.WriteAllTextAsync(outFile, $"OperationLogs.Count={logs.Count}{Environment.NewLine}");
            foreach (var log in logs.Take(50))
            {
                await File.AppendAllTextAsync(outFile, $"{log.Timestamp:O}\t{log.Operation}\t{log.Target}\t{log.Details}{Environment.NewLine}");
            }
        }
    }
}
