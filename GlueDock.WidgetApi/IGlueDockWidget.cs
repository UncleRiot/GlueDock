using System.Windows;

namespace GlueDock;

public interface IGlueDockWidget : IDisposable
{
    string DisplayName { get; }

    string DisplayNameKey =>
        string.Empty;

    string Description =>
        string.Empty;

    string DescriptionKey =>
        string.Empty;

    bool HasSettings { get; }

    bool HasAlarm { get; }

    bool HasTimer { get; }

    bool HasStopwatch { get; }

    bool HasCalendar { get; }

    bool IsAlarmActive =>
        false;

    bool IsTimerActive =>
        false;

    bool IsStopwatchActive =>
        false;

    bool IsCalendarActive =>
        false;

    bool DisableDefaultHoverEffect { get; }

    bool HasCompanionView { get; }

    int CompanionSlotSpan =>
        1;

    bool ShowCompanionAsSubDock =>
        false;

    event EventHandler? CompanionViewChanged;

    void Initialize(
        GlueDockWidgetContext context);

    FrameworkElement CreateView();

    FrameworkElement? CreateCompanionView();

    void CloseCompanion()
    {
    }

    void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
    }

    IReadOnlyList<GlueDockWidgetSettingsSection> CreateSettingsSections()
    {
        return Array.Empty<GlueDockWidgetSettingsSection>();
    }

    void OpenSettings(
        Window owner);

    void OpenAlarm(
        Window owner);

    void OpenTimer(
        Window owner);

    void OpenStopwatch(
        Window owner);

    void OpenCalendar(
        Window owner);
}
