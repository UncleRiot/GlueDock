using System.Windows;

namespace GlueDock;

public interface IGlueDockWidget : IDisposable
{
    string DisplayName { get; }

    bool HasSettings { get; }

    bool HasAlarm { get; }

    bool HasTimer { get; }

    bool HasStopwatch { get; }

    bool HasCalendar { get; }

    bool DisableDefaultHoverEffect { get; }

    bool HasCompanionView { get; }

    event EventHandler? CompanionViewChanged;

    void Initialize(
        GlueDockWidgetContext context);

    FrameworkElement CreateView();

    FrameworkElement? CreateCompanionView();

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
