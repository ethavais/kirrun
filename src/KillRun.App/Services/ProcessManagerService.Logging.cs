namespace KillRun.App.Services;

internal sealed partial class ProcessManagerService
{
    private readonly ILogger<ProcessManagerService> _logger;

    public ProcessManagerService(ILogger<ProcessManagerService> logger)
    {
        _logger = logger;
        LogServiceInitialized();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ProcessManagerService initialized")]
    private partial void LogServiceInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: starting netstat scan")]
    private partial void LogScanStart();

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetProcessGroups: netstat exited code={ExitCode}, outputLength={Length}")]
    private partial void LogNetstatExited(int exitCode, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetProcessGroups: failed to run netstat")]
    private partial void LogNetstatFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: found {Listening} LISTENING lines, uniquePIDs={Pids}")]
    private partial void LogScanSummary(int listening, int pids);

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: returning {Count} process groups")]
    private partial void LogScanResult(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "KillProcess: attempting to kill PID {Pid}")]
    private partial void LogKillStart(int pid);

    [LoggerMessage(Level = LogLevel.Information, Message = "KillProcess: successfully killed PID {Pid}")]
    private partial void LogKillSuccess(int pid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KillProcess: PID {Pid} not found (already exited?)")]
    private partial void LogKillNotFound(Exception ex, int pid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KillProcess: failed for PID {Pid}")]
    private partial void LogKillFailed(Exception ex, int pid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KillProcess: access denied killing PID {Pid}")]
    private partial void LogKillAccessDenied(Exception ex, int pid);
}
