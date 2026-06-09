using Kirun.App.Models;

namespace Kirun.App.Services;

internal static class HandleOutputParser
{
    public static IReadOnlyList<LockHandleEntry> Parse(string output, string query)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = Normalize(query);
        var result = new List<LockHandleEntry>();
        var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("Process\tPID\tUser\tHandle\tType\tShare Flags\tName", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split('\t');
            LockHandleEntry entry;
            if (parts.Length >= 7)
            {
                if (!int.TryParse(parts[1], out var pid))
                    continue;

                entry = new LockHandleEntry(
                    parts[0],
                    pid,
                    parts[2],
                    parts[3],
                    parts[4],
                    parts[5],
                    parts[6]);
            }
            else if (parts.Length >= 5)
            {
                if (!int.TryParse(parts[1], out var pid))
                    continue;

                entry = new LockHandleEntry(
                    parts[0],
                    pid,
                    "",
                    parts[3],
                    parts[2],
                    "",
                    parts[4]);
            }
            else
            {
                continue;
            }

            if (!Normalize(entry.ResourceName).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(entry);
        }

        return result
            .OrderBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Pid)
            .ToList();
    }

    private static string Normalize(string value)
    {
        return value.Trim().Trim('"').Replace('/', '\\');
    }
}
