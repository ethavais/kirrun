using System.Diagnostics;
using Kirun.App.Models;
using Kirun.App.Options;
using Microsoft.Extensions.Options;

namespace Kirun.App.Services;

internal interface IEmbeddedHandleExtractor
{
    string? Extract();
}

internal interface IHandleBinaryInspector
{
    HandleBinaryInspection Inspect(string executablePath);
}

internal interface IHandleToolBinaryResolver
{
    HandleToolStatus Resolve();
}

internal sealed record HandleBinaryInspection(bool Exists, Version? Version, string? ErrorMessage);

internal sealed partial class HandleToolBinaryResolver(
    IOptions<HandleOptions> options,
    IEmbeddedHandleExtractor extractor,
    IHandleBinaryInspector inspector,
    ILogger<HandleToolBinaryResolver> logger)
    : IHandleToolBinaryResolver
{
    private readonly Lock syncRoot = new();
    private HandleToolStatus? cachedStatus;

    public HandleToolStatus Resolve()
    {
        lock (syncRoot)
        {
            if (cachedStatus is not null)
            {
                LogResolveCached(cachedStatus.State, cachedStatus.ExecutablePath ?? "", cachedStatus.SourceDetail);
                return cachedStatus;
            }

            var stopwatch = Stopwatch.StartNew();
            var minimumVersion = ParseMinimumVersion(options.Value.MinimumVersion);
            var configuredPath = options.Value.ExecutablePath?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var configuredStatus = ResolveFromPath(configuredPath, HandleToolSource.ConfiguredPath, minimumVersion);
                if (configuredStatus.State != HandleToolState.Missing)
                {
                    cachedStatus = configuredStatus;
                    LogResolved(configuredStatus.State, configuredStatus.ExecutablePath ?? "", stopwatch.ElapsedMilliseconds);
                    return configuredStatus;
                }
            }

            var embeddedPath = extractor.Extract();
            if (string.IsNullOrWhiteSpace(embeddedPath))
            {
                cachedStatus = new HandleToolStatus(
                    HandleToolState.Missing,
                    HandleToolSource.Embedded,
                    null,
                    null,
                    minimumVersion,
                    "Bundled handle64.exe is not available.",
                    "The app expected a bundled Handle binary but could not extract it from the application resources.",
                    "Cache Path");
                return cachedStatus;
            }

            cachedStatus = ResolveFromPath(embeddedPath, HandleToolSource.Embedded, minimumVersion);
            LogResolved(cachedStatus.State, cachedStatus.ExecutablePath ?? "", stopwatch.ElapsedMilliseconds);
            return cachedStatus;
        }
    }

    private HandleToolStatus ResolveFromPath(string executablePath, HandleToolSource source, Version minimumVersion)
    {
        var inspection = inspector.Inspect(executablePath);
        if (!inspection.Exists)
        {
            return new HandleToolStatus(
                HandleToolState.Missing,
                source,
                executablePath,
                null,
                minimumVersion,
                inspection.ErrorMessage ?? "handle executable was not found.",
                GetSourceDetail(source),
                GetPathLabel(source));
        }

        if (inspection.Version is null)
        {
            return new HandleToolStatus(
                HandleToolState.Invalid,
                source,
                executablePath,
                null,
                minimumVersion,
                inspection.ErrorMessage ?? "handle executable version could not be read.",
                GetSourceDetail(source),
                GetPathLabel(source));
        }

        if (inspection.Version < minimumVersion)
        {
            return new HandleToolStatus(
                HandleToolState.Outdated,
                source,
                executablePath,
                inspection.Version,
                minimumVersion,
                $"handle version {inspection.Version} is below minimum {minimumVersion}.",
                GetSourceDetail(source),
                GetPathLabel(source));
        }

        return new HandleToolStatus(
            HandleToolState.Ready,
            source,
            executablePath,
            inspection.Version,
            minimumVersion,
            "Handle is ready.",
            GetSourceDetail(source),
            GetPathLabel(source));
    }

    private static string GetSourceDetail(HandleToolSource source)
    {
        return source switch
        {
            HandleToolSource.Embedded => "This binary is bundled with the app and extracted to a local cache automatically. No extra download is required after cloning and building the repository.",
            HandleToolSource.ConfiguredPath => "This binary comes from the configured executable path in appsettings.",
            _ => "Unknown handle binary source."
        };
    }

    private static string GetPathLabel(HandleToolSource source)
    {
        return source == HandleToolSource.Embedded
            ? "Cache Path"
            : "Executable Path";
    }

    private static Version ParseMinimumVersion(string? value)
    {
        return Version.TryParse(value, out var version)
            ? version
            : new Version(5, 0);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handle resolver: returning cached status {State} at {Path}. {Detail}")]
    private partial void LogResolveCached(HandleToolState state, string path, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handle resolver: resolved status {State} at {Path} in {ElapsedMs} ms")]
    private partial void LogResolved(HandleToolState state, string path, long elapsedMs);
}
