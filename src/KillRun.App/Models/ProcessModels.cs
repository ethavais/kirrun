using System;
using System.Collections.Generic;

namespace KillRun.App.Models;

internal sealed record PortEntry(int Port, string Protocol);

internal enum ProcessCategory
{
    DotNet,
    Database,
    Editor,
    SystemCore,
    Network,
    Application,
    Other,
}

internal sealed record ProcessGroup(
    int Pid,
    string Name,
    string? AppName,
    ProcessCategory Category,
    IReadOnlyList<PortEntry> Ports);

internal static class ProcessCategoryExtensions
{
    private static readonly Dictionary<string, ProcessCategory> s_categoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET runtimes and apps
        ["dotnet"] = ProcessCategory.DotNet,
        ["iisexpress"] = ProcessCategory.DotNet,

        // Databases
        ["sqlservr"] = ProcessCategory.Database,
        ["postgres"] = ProcessCategory.Database,
        ["mysqld"] = ProcessCategory.Database,
        ["mongod"] = ProcessCategory.Database,
        ["redis-server"] = ProcessCategory.Database,

        // Editors & IDEs
        ["code"] = ProcessCategory.Editor,
        ["devenv"] = ProcessCategory.Editor,
        ["rider"] = ProcessCategory.Editor,
        ["kiro"] = ProcessCategory.Editor,
        ["language_server_windows_x64"] = ProcessCategory.Editor,
        ["antigravity ide"] = ProcessCategory.Editor,

        // Windows core system processes
        ["svchost"] = ProcessCategory.SystemCore,
        ["lsass"] = ProcessCategory.SystemCore,
        ["wininit"] = ProcessCategory.SystemCore,
        ["services"] = ProcessCategory.SystemCore,
        ["spoolsv"] = ProcessCategory.SystemCore,
        ["system"] = ProcessCategory.SystemCore,
        ["jhi_service"] = ProcessCategory.SystemCore,

        // Network / drivers
        ["mdnsresponder"] = ProcessCategory.Network,
        ["hasplms"] = ProcessCategory.Network,
        ["vmms"] = ProcessCategory.Network,
    };

    public static ProcessCategory CategorizeProcess(string processName)
    {
        if (s_categoryMap.TryGetValue(processName, out var cat))
            return cat;

        if (processName.EndsWith(".App", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("server", StringComparison.OrdinalIgnoreCase))
            return ProcessCategory.Application;

        return ProcessCategory.Other;
    }

    public static string ToFriendlyName(this ProcessCategory category) => category switch
    {
        ProcessCategory.DotNet => ".NET",
        ProcessCategory.Database => "Database",
        ProcessCategory.Editor => "Editor",
        ProcessCategory.SystemCore => "System Core",
        ProcessCategory.Network => "Network",
        ProcessCategory.Application => "Application",
        _ => "Other",
    };

    public static string ToIcon(this ProcessCategory category) => category switch
    {
        ProcessCategory.DotNet => "⬡",
        ProcessCategory.Database => "🗄",
        ProcessCategory.Editor => "✏",
        ProcessCategory.SystemCore => "⚙",
        ProcessCategory.Network => "🌐",
        ProcessCategory.Application => "📦",
        _ => "◉",
    };
}

internal sealed class PinnedConfig
{
    public List<string> StarredCategories { get; set; } = [];
}
