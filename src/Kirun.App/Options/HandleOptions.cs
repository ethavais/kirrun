namespace Kirun.App.Options;

internal sealed class HandleOptions
{
    public string ExecutablePath { get; set; } = "";
    public string MinimumVersion { get; set; } = "5.0";
    public string PreferredSource { get; set; } = "Embedded";
    public string ExtractDirectory { get; set; } = "";
}
