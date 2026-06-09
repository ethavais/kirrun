using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Management;
using System.Text;
using System.Text.Json;
using Kirun.App.Models;

namespace Kirun.App.Services;

#pragma warning disable CA1812
internal sealed partial class ProcessManagerService
{
    private static readonly string[] s_lineSeparators = ["\r\n", "\r", "\n"];
    private static readonly char[] s_spaceSeparators = [' '];
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly ILogger<ProcessManagerService> _logger;
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "pinned.json");

    public ProcessManagerService(ILogger<ProcessManagerService> logger)
    {
        _logger = logger;
        LogServiceInitialized();
    }

    public IReadOnlyList<ProcessGroup> GetProcessGroups()
    {
        var totalStopwatch = Stopwatch.StartNew();
        LogScanStart();

        string output;
        long netstatMilliseconds;
        try
        {
            var netstatStopwatch = Stopwatch.StartNew();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            netstatMilliseconds = netstatStopwatch.ElapsedMilliseconds;
            LogNetstatExited(process.ExitCode, output.Length, netstatMilliseconds);
        }
        catch (InvalidOperationException ex)
        {
            LogNetstatFailed(ex);
            return [];
        }
        catch (Win32Exception ex)
        {
            LogNetstatFailed(ex);
            return [];
        }

        var mapStopwatch = Stopwatch.StartNew();
        var pidToPorts = new Dictionary<int, List<PortEntry>>();
        var lines = output.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
        int listeningCount = 0;

        foreach (var line in lines)
        {
            if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                continue;

            listeningCount++;
            var parts = line.Split(s_spaceSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;

            string protocol = parts[0];
            string localAddress = parts[1];
            string pidStr = parts[4];

            int portIndex = localAddress.LastIndexOf(':');
            if (portIndex == -1)
                continue;

            if (!int.TryParse(localAddress.AsSpan(portIndex + 1), out int port))
                continue;

            if (!int.TryParse(pidStr, out int pid) || pid <= 0)
                continue;

            if (!pidToPorts.TryGetValue(pid, out var portList))
            {
                portList = [];
                pidToPorts[pid] = portList;
            }

            if (!portList.Any(x => x.Port == port))
                portList.Add(new PortEntry(port, protocol));
        }

        if (pidToPorts.Count == 0)
        {
            LogScanSummary(listeningCount, 0, 0);
            return [];
        }

        var processMap = OperatingSystem.IsWindows()
            ? GetWindowsProcessInfo(pidToPorts.Keys)
            : GetFallbackProcessInfo(pidToPorts.Keys);
        var processInfoMilliseconds = mapStopwatch.ElapsedMilliseconds;

        LogScanSummary(listeningCount, pidToPorts.Count, processInfoMilliseconds);

        var result = new List<ProcessGroup>(pidToPorts.Count);
        foreach (var kv in pidToPorts)
        {
            int pid = kv.Key;
            var ports = kv.Value;

            string name = "Unknown";
            string? appName = null;
            string? exePath = null;
            long memory = 0;

            if (processMap.TryGetValue(pid, out var procInfo))
            {
                name = procInfo.Name;
                exePath = procInfo.ExecutablePath;
                memory = procInfo.MemoryBytes;
                if (!string.IsNullOrWhiteSpace(procInfo.MainWindowTitle))
                {
                    appName = procInfo.MainWindowTitle;
                }
                else if (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(procInfo.CommandLine))
                    {
                        appName = ParseDotNetAppName(procInfo.CommandLine);
                    }
                }
            }

            var commandLine = processMap.TryGetValue(pid, out var processInfo)
                ? processInfo.CommandLine
                : null;

            var category = ProcessCategoryExtensions.CategorizeProcess(name);
            var sortedPorts = ports.OrderBy(p => p.Port).ToList();
            result.Add(new ProcessGroup(pid, name, appName, category, sortedPorts, exePath, memory, commandLine));
        }

        var sortedResult = result
            .OrderBy(g => g.Category)
            .ThenBy(g => g.Name)
            .ToList();

        LogScanResult(sortedResult.Count, totalStopwatch.ElapsedMilliseconds);
        return sortedResult;
    }

    [SupportedOSPlatform("windows")]
    private static Dictionary<int, ProcessInfo> GetWindowsProcessInfo(IEnumerable<int> pids)
    {
        var result = new Dictionary<int, ProcessInfo>();
        var pidList = pids.Distinct().ToList();
        if (pidList.Count == 0)
            return result;

        try
        {
            var conditions = string.Join(" OR ", pidList.Select(pid => $"ProcessId = {pid}"));
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, Name, ExecutablePath, WorkingSetSize, CommandLine FROM Win32_Process WHERE {conditions}");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results.Cast<ManagementObject>())
            {
                var pidValue = obj["ProcessId"];
                if (pidValue is null)
                    continue;

                var pid = Convert.ToInt32(pidValue, System.Globalization.CultureInfo.InvariantCulture);
                var rawName = obj["Name"]?.ToString() ?? "Unknown";
                var normalizedName = Path.GetFileNameWithoutExtension(rawName);
                var executablePath = obj["ExecutablePath"]?.ToString();
                var commandLine = obj["CommandLine"]?.ToString();
                var memory = ParseWorkingSet(obj["WorkingSetSize"]?.ToString());
                var title = TryGetMainWindowTitle(pid);

                result[pid] = new ProcessInfo(normalizedName, title, executablePath, memory, commandLine);
            }
        }
        catch
        {
        }

        return result;
    }

    private static Dictionary<int, ProcessInfo> GetFallbackProcessInfo(IEnumerable<int> pids)
    {
        var result = new Dictionary<int, ProcessInfo>();
        foreach (var pid in pids.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                var title = TryGetMainWindowTitle(pid);
                result[pid] = new ProcessInfo(process.ProcessName, title, null, process.WorkingSet64, null);
            }
            catch
            {
            }
        }

        return result;
    }

    private static string? TryGetMainWindowTitle(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var title = process.MainWindowTitle;
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }

    private static long ParseWorkingSet(string? value)
    {
        return long.TryParse(value, out var memory)
            ? memory
            : 0;
    }

    private sealed record ProcessInfo(
        string Name,
        string? MainWindowTitle,
        string? ExecutablePath,
        long MemoryBytes,
        string? CommandLine);

    private static string? ParseDotNetAppName(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        foreach (var token in tokens)
        {
            if (token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var fileName = Path.GetFileName(token);
                    if (!fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFileNameWithoutExtension(token);
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    public bool KillProcess(int pid)
    {
        LogKillStart(pid);
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(true);
            LogKillSuccess(pid);
            return true;
        }
        catch (ArgumentException ex)
        {
            LogKillNotFound(ex, pid);
            return false;
        }
        catch (Win32Exception ex)
        {
            LogKillAccessDenied(ex, pid);
            return false;
        }
        catch (Exception ex)
        {
            LogKillFailed(ex, pid);
            return false;
        }
    }

    public HashSet<string> GetPinnedCategories()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<PinnedConfig>(json);
                if (config?.StarredCategories != null)
                {
                    return new HashSet<string>(config.StarredCategories, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            LogConfigReadFailed(ex, _configPath);
        }

        var defaultConfig = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DotNet", "Database" };
        SavePinnedCategories(defaultConfig);
        return defaultConfig;
    }

    public void SavePinnedCategories(IEnumerable<string> categories)
    {
        try
        {
            var config = new PinnedConfig();
            foreach (var category in categories)
                config.StarredCategories.Add(category);
            var json = JsonSerializer.Serialize(config, s_jsonOptions);
            File.WriteAllText(_configPath, json);
            LogConfigSaved(_configPath);
        }
        catch (Exception ex)
        {
            LogConfigSaveFailed(ex, _configPath);
        }
    }

    public void TogglePinCategory(string categoryName)
    {
        var pinned = GetPinnedCategories();
        if (pinned.Remove(categoryName))
        {
            LogUnpinnedCategory(categoryName);
        }
        else
        {
            pinned.Add(categoryName);
            LogPinnedCategory(categoryName);
        }
        SavePinnedCategories(pinned);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ProcessManagerService initialized")]
    private partial void LogServiceInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: starting netstat scan")]
    private partial void LogScanStart();

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetProcessGroups: netstat exited code={ExitCode}, outputLength={Length}, elapsed={ElapsedMs} ms")]
    private partial void LogNetstatExited(int exitCode, int length, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetProcessGroups: failed to run netstat")]
    private partial void LogNetstatFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: found {Listening} LISTENING lines, uniquePIDs={Pids}, processInfoElapsed={ProcessInfoElapsedMs} ms")]
    private partial void LogScanSummary(int listening, int pids, long processInfoElapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "GetProcessGroups: returning {Count} process groups in {ElapsedMs} ms")]
    private partial void LogScanResult(int count, long elapsedMs);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read pinned categories config from {Path}")]
    private partial void LogConfigReadFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save pinned categories config to {Path}")]
    private partial void LogConfigSaveFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved pinned categories to {Path}")]
    private partial void LogConfigSaved(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pinned category: {Category}")]
    private partial void LogPinnedCategory(string category);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unpinned category: {Category}")]
    private partial void LogUnpinnedCategory(string category);
}
#pragma warning restore CA1812
