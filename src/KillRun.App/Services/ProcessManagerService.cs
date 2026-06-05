using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;
using KillRun.App.Models;

namespace KillRun.App.Services;

#pragma warning disable CA1812
internal sealed partial class ProcessManagerService
{
    private static readonly string[] s_lineSeparators = ["\r\n", "\r", "\n"];
    private static readonly char[] s_spaceSeparators = [' '];
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly ILogger<ProcessManagerService> _logger;
    private readonly string _configPath = Path.Combine(Directory.GetCurrentDirectory(), "pinned.json");

    public ProcessManagerService(ILogger<ProcessManagerService> logger)
    {
        _logger = logger;
        LogServiceInitialized();
    }

    public IReadOnlyList<ProcessGroup> GetProcessGroups()
    {
        LogScanStart();

        string output;
        try
        {
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
            LogNetstatExited(process.ExitCode, output.Length);
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
            LogScanSummary(listeningCount, 0);
            return [];
        }

        var processMap = new Dictionary<int, (string Name, string MainWindowTitle)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                string title = "";
                try
                {
                    title = p.MainWindowTitle;
                }
                catch
                {
                }
                processMap[p.Id] = (name, title);
            }
            catch
            {
            }
        }

        var dotnetCommandLineMap = new Dictionary<int, string>();
        if (OperatingSystem.IsWindows())
        {
            bool hasDotnet = false;
            foreach (var pid in pidToPorts.Keys)
            {
                if (processMap.TryGetValue(pid, out var info) && 
                    info.Name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    hasDotnet = true;
                    break;
                }
            }

            if (hasDotnet)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'dotnet.exe'");
                    using var results = searcher.Get();
                    foreach (ManagementObject obj in results.Cast<ManagementObject>())
                    {
                        if (obj["ProcessId"] is uint wmiPid && obj["CommandLine"] is string cmdLine)
                            dotnetCommandLineMap[(int)wmiPid] = cmdLine;
                    }
                }
                catch
                {
                }
            }
        }

        LogScanSummary(listeningCount, pidToPorts.Count);

        var result = new List<ProcessGroup>(pidToPorts.Count);
        foreach (var kv in pidToPorts)
        {
            int pid = kv.Key;
            var ports = kv.Value;

            string name = "Unknown";
            string? appName = null;

            if (processMap.TryGetValue(pid, out var procInfo))
            {
                name = procInfo.Name;
                if (!string.IsNullOrWhiteSpace(procInfo.MainWindowTitle))
                {
                    appName = procInfo.MainWindowTitle;
                }
                else if (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    if (dotnetCommandLineMap.TryGetValue(pid, out var cmdLine))
                    {
                        appName = ParseDotNetAppName(cmdLine);
                    }
                }
            }

            var category = ProcessCategoryExtensions.CategorizeProcess(name);
            var sortedPorts = ports.OrderBy(p => p.Port).ToList();
            result.Add(new ProcessGroup(pid, name, appName, category, sortedPorts));
        }

        var sortedResult = result
            .OrderBy(g => g.Category)
            .ThenBy(g => g.Name)
            .ToList();

        LogScanResult(sortedResult.Count);
        return sortedResult;
    }

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
            var config = new PinnedConfig { StarredCategories = [.. categories] };
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
