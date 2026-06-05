using KillRun.App.Logging;
using KillRun.App.Services;
using KillRun.App.Components;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Remove console logging to hide logs
builder.Logging.ClearProviders();

builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ProcessManagerService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// Pure HTTP kill endpoint - no SignalR needed
app.MapPost("/kill/{pid:int}", (int pid, string? tab, ProcessManagerService svc, ILogger<Program> logger) =>
{
    KillEndpointLog.Attempt(logger, pid, tab ?? "dotnet");
    var success = svc.KillProcess(pid);
    KillEndpointLog.Result(logger, pid, success);
    return Results.Redirect($"/?tab={tab ?? "dotnet"}");
}).DisableAntiforgery();

app.MapRazorComponents<App>();

// Start on port 7777
var port = 7777;
var url = $"http://localhost:{port}";
app.Urls.Add(url);

// Start the app
var task = app.RunAsync();

// Open browser after a short delay
_ = Task.Run(async () =>
{
    await Task.Delay(1000).ConfigureAwait(false);
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch (Exception) { }
});

task.Wait();
