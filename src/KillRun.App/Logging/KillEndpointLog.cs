namespace KillRun.App.Logging;

internal static partial class KillEndpointLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Kill endpoint: PID={Pid} tab={Tab}")]
    public static partial void Attempt(ILogger logger, int pid, string tab);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kill endpoint: PID={Pid} success={Success}")]
    public static partial void Result(ILogger logger, int pid, bool success);
}
