using System.Diagnostics;

namespace Kirun.App.Services;

internal sealed record HandleCommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

internal interface IHandleCommandRunner
{
    HandleCommandResult Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout);
}

internal sealed partial class HandleCommandRunner(ILogger<HandleCommandRunner> logger) : IHandleCommandRunner
{
    public HandleCommandResult Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            var timedOutResult = new HandleCommandResult(-1, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd(), true);
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var joinedArguments = string.Join(" ", arguments);
                LogTimedOut(executablePath, joinedArguments, stopwatch.ElapsedMilliseconds);
            }
            return timedOutResult;
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var joinedArguments = string.Join(" ", arguments);
            LogCompleted(executablePath, joinedArguments, process.ExitCode, standardOutput.Length, stopwatch.ElapsedMilliseconds);
        }
        return new HandleCommandResult(process.ExitCode, standardOutput, standardError, false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handle command: {Executable} {Arguments} completed with exitCode={ExitCode}, outputLength={OutputLength}, elapsed={ElapsedMs} ms")]
    private partial void LogCompleted(string executable, string arguments, int exitCode, int outputLength, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handle command: {Executable} {Arguments} timed out after {ElapsedMs} ms")]
    private partial void LogTimedOut(string executable, string arguments, long elapsedMs);
}
