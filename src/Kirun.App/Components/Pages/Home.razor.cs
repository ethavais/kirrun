#pragma warning disable CA1848, CA1873

using Kirun.App.Models;
using Kirun.App.Services;
using Microsoft.AspNetCore.Components;

namespace Kirun.App.Components.Pages;

public partial class Home
{
    [SupplyParameterFromQuery(Name = "tab")]
    public string Tab { get; set; } = "service";

    [SupplyParameterFromQuery(Name = "subtab")]
    public string Subtab { get; set; } = "All";

    [SupplyParameterFromQuery(Name = "file")]
    public string? FileQuery { get; set; }

    [SupplyParameterFromQuery(Name = "noticeTone")]
    public string? NoticeTone { get; set; }

    [SupplyParameterFromQuery(Name = "noticeTitle")]
    public string? NoticeTitle { get; set; }

    [SupplyParameterFromQuery(Name = "noticeMessage")]
    public string? NoticeMessage { get; set; }

    [SupplyParameterFromQuery(Name = "noticeMs")]
    public int? NoticeMilliseconds { get; set; }

    private IReadOnlyList<ProcessGroup> groups = [];
    private HashSet<string> PinnedCategories = new(StringComparer.OrdinalIgnoreCase);
    private List<string> PinnedCategoriesList = [];

    private bool IsServiceTab => Tab.Equals("service", StringComparison.OrdinalIgnoreCase);

    private bool IsHandlerTab => Tab.Equals("handler", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<ProcessGroup> FilteredGroups => GetFilteredGroups();

    private BasePopupModel? Popup => BuildPopup();

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Tab))
            Tab = "service";

        if (string.IsNullOrWhiteSpace(Subtab) || (!Subtab.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !Enum.TryParse<ProcessCategory>(Subtab, out _)))
            Subtab = "All";

        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation("Home: loading processes and configuration. tab={Tab}, subtab={Subtab}", Tab, Subtab);
        PinnedCategories = ProcessService.GetPinnedCategories();
        PinnedCategoriesList = Enum.GetNames<ProcessCategory>()
            .Where(name => PinnedCategories.Contains(name))
            .ToList();

        groups = IsServiceTab
            ? ProcessService.GetProcessGroups()
            : [];
    }

    private IReadOnlyList<ProcessGroup> GetFilteredGroups()
    {
        if (Subtab.Equals("All", StringComparison.OrdinalIgnoreCase))
            return groups;

        if (!Enum.TryParse<ProcessCategory>(Subtab, out var selectedCat))
            return groups;

        return groups
            .Where(group => group.Category == selectedCat)
            .ToList();
    }

    private BasePopupModel? BuildPopup()
    {
        if (string.IsNullOrWhiteSpace(NoticeTitle) || string.IsNullOrWhiteSpace(NoticeMessage))
            return null;

        var tone = Enum.TryParse<PopupTone>(NoticeTone, true, out var parsedTone)
            ? parsedTone
            : PopupTone.Info;

        var autoCloseMilliseconds = NoticeMilliseconds.GetValueOrDefault(UiFeedbackDefaults.PopupAutoCloseMilliseconds);
        return new BasePopupModel(
            $"popup-{Guid.NewGuid():N}",
            tone,
            NoticeTitle,
            NoticeMessage,
            autoCloseMilliseconds);
    }
}

#pragma warning restore CA1848, CA1873
