using System.Diagnostics;

namespace Kirun.App.Services;

internal interface IHandleProcessCandidateProvider
{
    IReadOnlyList<string> GetCandidateProcessNames(string query);
}

internal sealed partial class HandleProcessCandidateProvider(ILogger<HandleProcessCandidateProvider> logger) : IHandleProcessCandidateProvider
{
    private static readonly string[] CommonCandidates =
    [
        "explorer",
        "code",
        "devenv",
        "rider",
        "powershell",
        "pwsh",
        "cmd",
        "wt",
        "windowsterminal",
        "chrome",
        "msedge",
        "teams",
        "notepad",
        "acrord32"
    ];

    public IReadOnlyList<string> GetCandidateProcessNames(string query)
    {
        var stopwatch = Stopwatch.StartNew();
        var prioritizedCandidates = GetExtensionCandidates(query)
            .Concat(CommonCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var extensionCandidates = GetExtensionCandidates(query)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsProcessRunning)
            .ToList();
        var orderedCandidates = extensionCandidates.Count > 0
            ? extensionCandidates
            : prioritizedCandidates
                .Where(IsProcessRunning)
                .Take(4)
                .ToList();

        LogCandidates(query, orderedCandidates.Count, stopwatch.ElapsedMilliseconds);
        return orderedCandidates;
    }

    private static IEnumerable<string> GetExtensionCandidates(string query)
    {
        var extension = Path.GetExtension(query)?.Trim().ToUpperInvariant() ?? "";
        return extension switch
        {
            ".PPT" or ".PPTX" => ["powerpnt", "explorer"],
            ".DOC" or ".DOCX" => ["winword", "explorer"],
            ".XLS" or ".XLSX" => ["excel", "explorer"],
            ".PDF" => ["acrord32", "chrome", "msedge", "explorer"],
            ".SLN" or ".CSPROJ" or ".CS" => ["devenv", "rider", "code", "explorer"],
            _ => ["explorer", "powerpnt", "winword", "excel", "outlook"]
        };
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handle candidates: query={Query}, count={Count}, elapsed={ElapsedMs} ms")]
    private partial void LogCandidates(string query, int count, long elapsedMs);
}
