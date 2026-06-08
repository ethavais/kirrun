namespace KillRun.App.Models;

internal sealed record PortEntry(int Port, string Protocol);

internal enum ProcessCategory
{
    DotNet,
    Database,
    Editor,
    SystemCore,
    Other,
}

internal sealed record ProcessGroup(
    int Pid,
    string Name,
    string? AppName,
    ProcessCategory Category,
    IReadOnlyList<PortEntry> Ports,
    string? ExecutablePath = null,
    long MemoryBytes = 0,
    string? CommandLine = null);

internal static class ProcessCategoryExtensions
{
    private static readonly Dictionary<string, ProcessCategory> s_categoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = ProcessCategory.DotNet,
        ["iisexpress"] = ProcessCategory.DotNet,
        ["sqlservr"] = ProcessCategory.Database,
        ["postgres"] = ProcessCategory.Database,
        ["mysqld"] = ProcessCategory.Database,
        ["mongod"] = ProcessCategory.Database,
        ["redis-server"] = ProcessCategory.Database,
        ["code"] = ProcessCategory.Editor,
        ["devenv"] = ProcessCategory.Editor,
        ["rider"] = ProcessCategory.Editor,
        ["kiro"] = ProcessCategory.Editor,
        ["language_server_windows_x64"] = ProcessCategory.Editor,
        ["antigravity ide"] = ProcessCategory.Editor,
        ["svchost"] = ProcessCategory.SystemCore,
        ["lsass"] = ProcessCategory.SystemCore,
        ["wininit"] = ProcessCategory.SystemCore,
        ["services"] = ProcessCategory.SystemCore,
        ["spoolsv"] = ProcessCategory.SystemCore,
        ["system"] = ProcessCategory.SystemCore,
        ["jhi_service"] = ProcessCategory.SystemCore,
        ["mdnsresponder"] = ProcessCategory.SystemCore,
        ["hasplms"] = ProcessCategory.SystemCore,
        ["vmms"] = ProcessCategory.SystemCore,
    };

    public static ProcessCategory CategorizeProcess(string processName)
    {
        if (s_categoryMap.TryGetValue(processName, out var cat))
            return cat;

        return ProcessCategory.Other;
    }

    public static string ToFriendlyName(this ProcessCategory category) => category switch
    {
        ProcessCategory.DotNet => ".NET",
        ProcessCategory.Database => "Database",
        ProcessCategory.Editor => "Editor",
        ProcessCategory.SystemCore => "System Core",
        _ => "Other",
    };

    public static string ToIcon(this ProcessCategory category) => category switch
    {
        ProcessCategory.DotNet => "⬡",
        ProcessCategory.Database => "🗄",
        ProcessCategory.Editor => "✏",
        ProcessCategory.SystemCore => "⚙",
        _ => "◉",
    };
}

internal sealed class PinnedConfig
{
    public List<string> StarredCategories { get; set; } = [];
}
