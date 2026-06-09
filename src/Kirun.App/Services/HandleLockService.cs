using System.Diagnostics;
using Kirun.App.Models;

namespace Kirun.App.Services;

internal sealed partial class HandleLockService(
    IHandleToolBinaryResolver resolver,
    IHandleCommandRunner runner,
    IHandleProcessCandidateProvider candidateProvider,
    ILogger<HandleLockService> logger)
{
    private const int CandidateScanTimeoutMilliseconds = 1500;

    public HandleToolStatus GetToolStatus()
    {
        return resolver.Resolve();
    }

    public LockScanResult Scan(string? query)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var normalizedQuery = query?.Trim() ?? "";
        var resolveStopwatch = Stopwatch.StartNew();
        var toolStatus = resolver.Resolve();
        var resolveMilliseconds = resolveStopwatch.ElapsedMilliseconds;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return new LockScanResult("", "", toolStatus, [], LockScanMetrics.Empty, "Enter a file or folder path first.");

        if (toolStatus.State != HandleToolState.Ready || string.IsNullOrWhiteSpace(toolStatus.ExecutablePath))
            return new LockScanResult(query!, normalizedQuery, toolStatus, [], LockScanMetrics.Empty, toolStatus.Message);

        var fragment = GetSearchFragment(normalizedQuery);
        var candidateStopwatch = Stopwatch.StartNew();
        var candidates = candidateProvider.GetCandidateProcessNames(normalizedQuery);
        var candidateDiscoveryMilliseconds = candidateStopwatch.ElapsedMilliseconds;

        long commandMilliseconds = 0;
        long parseMilliseconds = 0;
        var candidatesChecked = 0;

        foreach (var candidate in candidates)
        {
            candidatesChecked++;
            var commandStopwatch = Stopwatch.StartNew();
            var candidateResult = RunHandleScan(toolStatus.ExecutablePath, candidate, fragment, TimeSpan.FromMilliseconds(CandidateScanTimeoutMilliseconds));
            commandMilliseconds += commandStopwatch.ElapsedMilliseconds;
            if (candidateResult.TimedOut)
                continue;

            var parseStopwatch = Stopwatch.StartNew();
            var candidateEntries = HandleOutputParser.Parse(candidateResult.StandardOutput, fragment);
            parseMilliseconds += parseStopwatch.ElapsedMilliseconds;
            if (candidateEntries.Count > 0)
            {
                var metrics = new LockScanMetrics(
                    totalStopwatch.ElapsedMilliseconds,
                    resolveMilliseconds,
                    candidateDiscoveryMilliseconds,
                    commandMilliseconds,
                    parseMilliseconds,
                    candidates.Count,
                    candidatesChecked,
                    candidate);
                LogScanCompleted(normalizedQuery, metrics.TotalMilliseconds, metrics.CandidateCount, metrics.CandidatesChecked, candidate);
                return new LockScanResult(query!, normalizedQuery, toolStatus, candidateEntries, metrics, null);
            }
        }

        var failedMetrics = new LockScanMetrics(
            totalStopwatch.ElapsedMilliseconds,
            resolveMilliseconds,
            candidateDiscoveryMilliseconds,
            commandMilliseconds,
            parseMilliseconds,
            candidates.Count,
            candidatesChecked,
            null);
        LogScanCompleted(normalizedQuery, failedMetrics.TotalMilliseconds, failedMetrics.CandidateCount, failedMetrics.CandidatesChecked, null);
        return new LockScanResult(query!, normalizedQuery, toolStatus, [], failedMetrics, "No matching lock handles were found in the quick scan.");
    }

    private HandleCommandResult RunHandleScan(string executablePath, string? processName, string queryFragment, TimeSpan timeout)
    {
        var arguments = new List<string>
        {
            "-accepteula",
            "-nobanner",
            "-vt"
        };

        if (!string.IsNullOrWhiteSpace(processName))
        {
            arguments.Add("-p");
            arguments.Add(processName);
        }

        arguments.Add(queryFragment);
        return runner.Run(executablePath, arguments, timeout);
    }

    private static string GetSearchFragment(string query)
    {
        var trimmed = query.Trim().TrimEnd('\\', '/');
        var fragment = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(fragment)
            ? trimmed
            : fragment;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handle scan: query={Query}, elapsed={ElapsedMs} ms, candidates={CandidateCount}, checked={CheckedCount}, winner={WinningCandidate}")]
    private partial void LogScanCompleted(string query, long elapsedMs, int candidateCount, int checkedCount, string? winningCandidate);
}
