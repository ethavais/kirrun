using System.Diagnostics;

namespace Kirun.App.Services;

internal sealed class HandleBinaryInspector : IHandleBinaryInspector
{
    public HandleBinaryInspection Inspect(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return new HandleBinaryInspection(false, null, "handle executable path is empty.");

        if (!File.Exists(executablePath))
            return new HandleBinaryInspection(false, null, $"handle executable was not found at {executablePath}.");

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            var versionText = versionInfo.ProductVersion;
            if (string.IsNullOrWhiteSpace(versionText))
                versionText = versionInfo.FileVersion;

            if (!Version.TryParse(versionText, out var version))
                return new HandleBinaryInspection(true, null, $"handle executable version is invalid: {versionText ?? "<empty>"}.");

            return new HandleBinaryInspection(true, version, null);
        }
        catch (Exception ex)
        {
            return new HandleBinaryInspection(true, null, ex.Message);
        }
    }
}
