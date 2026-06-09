namespace Kirun.App.Models;

public enum PopupTone
{
    Success,
    Error,
    Info,
}

public sealed record BasePopupModel(
    string Id,
    PopupTone Tone,
    string Title,
    string Message,
    int AutoCloseMilliseconds);

public static class UiFeedbackDefaults
{
    public const int PopupAutoCloseMilliseconds = 3600;
}
