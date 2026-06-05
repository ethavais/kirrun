#pragma warning disable CA1848, CA1873

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KillRun.App.Components;
using KillRun.App.Services;

namespace KillRun.App;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Clear console logging to keep workspace outputs clean and tidy
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Register custom application services
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<ProcessManagerService>();

        var app = builder.Build();

        // Configure standard middleware pipeline
        app.UseStaticFiles();
        app.UseAntiforgery();

        // ── HTTP POST Route: Kill Process ──────────────────────
        app.MapPost("/kill/{pid:int}", (
            int pid, 
            string? tab, 
            string? subtab, 
            ProcessManagerService svc, 
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Program");
            logger.LogInformation("Kill requested for PID {Pid} from tab: {Tab}, subtab: {Subtab}", pid, tab, subtab);
            
            var success = svc.KillProcess(pid);
            logger.LogInformation("Kill result for PID {Pid}: success={Success}", pid, success);

            // Redirect back to the exact tab and subtab the user was viewing
            var targetTab = tab ?? "service";
            var targetSubtab = subtab ?? "DotNet";
            return Results.Redirect($"/?tab={targetTab}&subtab={targetSubtab}");
        }).DisableAntiforgery();

        // ── HTTP POST Route: Toggle Pin Category ────────────────
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
            return Results.Redirect($"/?tab={targetTab}&subtab={targetSubtab}");
        }).DisableAntiforgery();

        // Map component routing using the App root component
        app.MapRazorComponents<KillRun.App.Components.App>();

        // Start the application host on port 7777
        var port = 7777;
        var url = $"http://localhost:{port}";
        app.Urls.Add(url);

        var appTask = app.RunAsync();

        // Auto-open the application in the system's default browser after startup delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000).ConfigureAwait(false);
            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            try
            {
                startupLogger.LogInformation("Launching default browser at: {Url}", url);
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                startupLogger.LogWarning("Failed to automatically launch default browser: {Message}", ex.Message);
            }
        });

        // Run application task synchronously
        appTask.Wait();
    }
}
