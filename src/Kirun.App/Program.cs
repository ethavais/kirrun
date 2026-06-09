#pragma warning disable CA1848, CA1873

using System.Diagnostics;
using System.Text;
using Kirun.App.Models;
using Kirun.App.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Kirun.App;

public static class Program
{
    public static void Main(string[] args)
    {
        var appDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
        var webRootPath = Path.Combine(appDirectory, "wwwroot");

        Directory.SetCurrentDirectory(appDirectory);
        var builder = Directory.Exists(webRootPath)
            ? WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = appDirectory,
                WebRootPath = webRootPath,
            })
            : WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = appDirectory,
            });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddDataProtection()
            .SetApplicationName("Kirun.App")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Kirun", "dpkeys")));

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.Configure<Options.HandleOptions>(builder.Configuration.GetSection("Handle"));
        builder.Services.AddSingleton<ProcessManagerService>();
        builder.Services.AddSingleton<IEmbeddedHandleExtractor, EmbeddedHandleExtractor>();
        builder.Services.AddSingleton<IHandleBinaryInspector, HandleBinaryInspector>();
        builder.Services.AddSingleton<IHandleToolBinaryResolver, HandleToolBinaryResolver>();
        builder.Services.AddSingleton<IHandleCommandRunner, HandleCommandRunner>();
        builder.Services.AddSingleton<IHandleProcessCandidateProvider, HandleProcessCandidateProvider>();
        builder.Services.AddSingleton<HandleLockService>();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapPost("/kill/{pid:int}", (
            int pid, 
            string? tab, 
            string? subtab, 
            string? file,
            ProcessManagerService svc, 
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Program");
            logger.LogInformation("Kill requested for PID {Pid} from tab: {Tab}, subtab: {Subtab}", pid, tab, subtab);
            
            var success = svc.KillProcess(pid);
            logger.LogInformation("Kill result for PID {Pid}: success={Success}", pid, success);

            var targetTab = tab ?? "service";
            var targetSubtab = subtab ?? "DotNet";
            var popup = success
                ? new BasePopupModel("kill-success", PopupTone.Success, "Process terminated", $"Killed PID {pid} and reloaded the current view.", UiFeedbackDefaults.PopupAutoCloseMilliseconds)
                : new BasePopupModel("kill-failed", PopupTone.Error, "Kill failed", $"Could not terminate PID {pid}. The process may already be closed or require higher privileges.", UiFeedbackDefaults.PopupAutoCloseMilliseconds);

            return Results.Redirect(BuildRedirectUrl(targetTab, targetSubtab, file, popup));
        }).DisableAntiforgery();

        app.MapPost("/toggle-pin/{categoryName}", (
            string categoryName, 
            string? tab, 
            string? subtab, 
            ProcessManagerService svc, 
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Program");
            logger.LogInformation("Toggle pin requested for category: {CategoryName}", categoryName);

            svc.TogglePinCategory(categoryName);
            
            var targetTab = tab ?? "service";
            var targetSubtab = subtab ?? categoryName;
            var popup = new BasePopupModel(
                "pin-updated",
                PopupTone.Info,
                "Pinned categories updated",
                $"Updated the pinned state for {categoryName}.",
                UiFeedbackDefaults.PopupAutoCloseMilliseconds);
            return Results.Redirect(BuildRedirectUrl(targetTab, targetSubtab, null, popup));
        }).DisableAntiforgery();

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        var port = 7777;
        var url = $"http://localhost:{port}";
        app.Urls.Add(url);

        var appTask = app.RunAsync();

        if (!Debugger.IsAttached)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000).ConfigureAwait(false);
                var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
                try
                {
                    startupLogger.LogInformation("Launching default browser at: {Url}", url);

                    var psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    startupLogger.LogWarning("Failed to automatically launch default browser: {Message}", ex.Message);
                }
            });
        }

        appTask.Wait();
    }

    private static string BuildRedirectUrl(string targetTab, string? targetSubtab, string? file, BasePopupModel popup)
    {
        var query = new StringBuilder("/?tab=").Append(Uri.EscapeDataString(targetTab));

        if (string.Equals(targetTab, "handler", StringComparison.OrdinalIgnoreCase))
        {
            query.Append("&file=").Append(Uri.EscapeDataString(file ?? ""));
        }
        else
        {
            query.Append("&subtab=").Append(Uri.EscapeDataString(targetSubtab ?? "All"));
        }

        query.Append("&noticeTone=").Append(Uri.EscapeDataString(popup.Tone.ToString()));
        query.Append("&noticeTitle=").Append(Uri.EscapeDataString(popup.Title));
        query.Append("&noticeMessage=").Append(Uri.EscapeDataString(popup.Message));
        query.Append("&noticeMs=").Append(popup.AutoCloseMilliseconds);
        return query.ToString();
    }
}
