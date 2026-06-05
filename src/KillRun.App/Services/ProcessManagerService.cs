#pragma warning disable CA1031 // Do not catch general exception types

using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using KillRun.App.Models;

namespace KillRun.App.Services;

#pragma warning disable CA1812 // internal class that is apparently never instantiated
internal sealed partial class ProcessManagerService
{
    private static readonly string[] s_lineSeparators = ["\r\n", "\r", "\n"];
    private static readonly char[] s_spaceSeparators = [' '];

#pragma warning disable CA1822 // Mark members as static
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

            string protocol = parts[0];   // TCP / UDP
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
            LogScanResult(0);
            return [];
        }

        // 1. Bulk Process Resolution: Query all active processes once
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
                    // Ignore window title access failure
                }
                processMap[p.Id] = (name, title);
            }
            catch
            {
                // Ignore processes that exited or threw exceptions
            }
        }

        // 2. Single WMI Query for all .NET command lines
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
                    foreach (ManagementObject obj in results)
                    {
                        if (obj["ProcessId"] is uint wmiPid && obj["CommandLine"] is string cmdLine)
                        {
                            dotnetCommandLineMap[(int)wmiPid] = cmdLine;
                        }
                    }
                }
                catch
                {
                    // WMI might fail or be disabled, ignore
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
        var current = new System.Text.StringBuilder();
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
                    // Ignore path parsing exceptions
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
            p.Kill(true); // true to kill process tree
            LogKillSuccess(pid);
            return true;
        }
        catch (ArgumentException ex) { LogKillNotFound(ex, pid); return false; }
        catch (InvalidOperationException ex) { LogKillFailed(ex, pid); return false; }
        catch (NotSupportedException ex) { LogKillFailed(ex, pid); return false; }
        catch (Win32Exception ex) { LogKillAccessDenied(ex, pid); return false; }
    }
#pragma warning restore CA1822

}
#pragma warning restore CA1812

#pragma warning restore CA1031
