namespace KillRun.App.Models;

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
}
