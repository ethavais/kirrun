using System.Diagnostics;
using System.Reflection;
using Kirun.App.Options;
using Microsoft.Extensions.Options;

namespace Kirun.App.Services;

internal sealed partial class EmbeddedHandleExtractor(
    IOptions<HandleOptions> options,
    ILogger<EmbeddedHandleExtractor> logger) : IEmbeddedHandleExtractor
{
    private const string ResourceName = "Kirun.App.Resources.Handle.handle64.exe";
    private readonly Lock syncRoot = new();

    public string? Extract()
    {
        var stopwatch = Stopwatch.StartNew();
        var targetDirectory = GetExtractDirectory(options.Value);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, "handle64.exe");
        lock (syncRoot)
        {
            if (File.Exists(targetPath))
            {
                LogCacheHit(targetPath, stopwatch.ElapsedMilliseconds);
                return targetPath;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                LogExtractMissing(stopwatch.ElapsedMilliseconds);
                return null;
            }

            using var fileStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            stream.CopyTo(fileStream);
            LogExtracted(targetPath, stopwatch.ElapsedMilliseconds);
            return targetPath;
        }
    }

    private static string GetExtractDirectory(HandleOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ExtractDirectory))
            return options.ExtractDirectory;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var versionFolder = string.IsNullOrWhiteSpace(options.MinimumVersion)
            ? "embedded"
            : options.MinimumVersion.Replace('.', '_');
        return Path.Combine(localAppData, "Kirun", "tools", "handle", versionFolder);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handle extractor: using cached embedded binary at {Path} in {ElapsedMs} ms")]
    private partial void LogCacheHit(string path, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handle extractor: embedded resource not found after {ElapsedMs} ms")]
    private partial void LogExtractMissing(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handle extractor: extracted embedded binary to {Path} in {ElapsedMs} ms")]
    private partial void LogExtracted(string path, long elapsedMs);
}
