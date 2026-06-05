namespace KillRun.App.Models;

internal sealed record ProcessGroup(
    int Pid,
    string Name,
    string? AppName,
    ProcessCategory Category,
    IReadOnlyList<PortEntry> Ports);
