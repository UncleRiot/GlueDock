using System.Windows;

namespace GlueDock;

public sealed class GlueDockWidgetSettingsSection : IDisposable
{
    private readonly Action? _dispose;

    public GlueDockWidgetSettingsSection(
        string groupId,
        string groupTitleKey,
        int groupOrder,
        string sectionId,
        string sectionTitleKey,
        int sectionOrder,
        FrameworkElement content,
        Action? dispose = null)
    {
        GroupId = groupId;
        GroupTitleKey = groupTitleKey;
        GroupOrder = groupOrder;
        SectionId = sectionId;
        SectionTitleKey = sectionTitleKey;
        SectionOrder = sectionOrder;
        Content = content;
        _dispose = dispose;
    }

    public string GroupId { get; }

    public string GroupTitleKey { get; }

    public int GroupOrder { get; }

    public string SectionId { get; }

    public string SectionTitleKey { get; }

    public int SectionOrder { get; }

    public FrameworkElement Content { get; }

    public void Dispose()
    {
        _dispose?.Invoke();
    }
}
