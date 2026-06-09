namespace Kirun.App.Models;

public enum HandleToolState
{
    Ready,
    Missing,
    Outdated,
    Invalid,
}

public enum HandleToolSource
{
    Embedded,
    ConfiguredPath,
}

public sealed record HandleToolStatus(
    HandleToolState State,
    HandleToolSource? Source,
    string? ExecutablePath,
    Version? CurrentVersion,
    Version MinimumVersion,
    string Message,
    string SourceDetail,
    string PathLabel);

public sealed record LockHandleEntry(
    string ProcessName,
    int Pid,
    string UserName,
    string HandleValue,
    string HandleType,
    string ShareFlags,
    string ResourceName);

public sealed record LockScanResult(
    string Query,
    string NormalizedQuery,
    HandleToolStatus ToolStatus,
    IReadOnlyList<LockHandleEntry> Entries,
    LockScanMetrics Metrics,
    string? ErrorMessage)
{
    public bool Succeeded => ToolStatus.State == HandleToolState.Ready && string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed record LockScanMetrics(
    long TotalMilliseconds,
    long ResolveMilliseconds,
    long CandidateDiscoveryMilliseconds,
    long CommandMilliseconds,
    long ParseMilliseconds,
    int CandidateCount,
    int CandidatesChecked,
    string? WinningCandidate)
{
    public static LockScanMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, null);
}
