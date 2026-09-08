using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace GlueDock;

public sealed class DockItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private string _displayName = string.Empty;
    private double _layoutWidth = 58;
    private double _layoutHeight = 58;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Path { get; init; } = string.Empty;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(
                    _displayName,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _displayName =
                value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(DisplayName)));
        }
    }

    public bool IsSubmenu { get; init; }

    public bool IsWidgetDragDropLocked { get; set; }

    [JsonIgnore]
    public bool IsRuntimeOnly { get; init; }

    [JsonIgnore]
    public int SlotSpan { get; set; } = 1;

    [JsonIgnore]
    public double LayoutWidth
    {
        get => _layoutWidth;
        set
        {
            if (Math.Abs(
                    _layoutWidth -
                    value) <
                0.001)
            {
                return;
            }

            _layoutWidth =
                value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(LayoutWidth)));
        }
    }

    [JsonIgnore]
    public double LayoutHeight
    {
        get => _layoutHeight;
        set
        {
            if (Math.Abs(
                    _layoutHeight -
                    value) <
                0.001)
            {
                return;
            }

            _layoutHeight =
                value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(LayoutHeight)));
        }
    }

    public string SubmenuIconRepositoryPath { get; set; } = string.Empty;

    public ObservableCollection<DockItem> Children { get; } = [];

    [JsonIgnore]
    public IGlueDockWidget? WidgetInstance { get; init; }

    [JsonIgnore]
    public FrameworkElement? WidgetView { get; init; }

    [JsonIgnore]
    public bool IsWidget =>
        WidgetView is not null;

    [JsonIgnore]
    public bool HasWidgetSettings =>
        WidgetInstance?.HasSettings == true;

    [JsonIgnore]
    public bool HasWidgetAlarm =>
        WidgetInstance?.HasAlarm == true;

    [JsonIgnore]
    public bool HasWidgetTimer =>
        WidgetInstance?.HasTimer == true;

    [JsonIgnore]
    public bool HasWidgetStopwatch =>
        WidgetInstance?.HasStopwatch == true;

    [JsonIgnore]
    public bool HasWidgetCalendar =>
        WidgetInstance?.HasCalendar == true;

    [JsonIgnore]
    public bool IsWidgetAlarmActive =>
        WidgetInstance?.IsAlarmActive == true;

    [JsonIgnore]
    public bool IsWidgetTimerActive =>
        WidgetInstance?.IsTimerActive == true;

    [JsonIgnore]
    public bool IsWidgetStopwatchActive =>
        WidgetInstance?.IsStopwatchActive == true;

    [JsonIgnore]
    public bool IsWidgetCalendarActive =>
        WidgetInstance?.IsCalendarActive == true;

    [JsonIgnore]
    public bool DisableDefaultHoverEffect =>
        IsRuntimeOnly ||
        WidgetInstance?.DisableDefaultHoverEffect == true;

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(
                    _icon,
                    value))
            {
                return;
            }

            _icon = value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(Icon)));
        }
    }

    public void NotifyWidgetStateChanged()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(IsWidgetAlarmActive)));

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(IsWidgetTimerActive)));

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(IsWidgetStopwatchActive)));

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(IsWidgetCalendarActive)));
    }

    public void DisposeWidget()
    {
        WidgetInstance?.Dispose();

        foreach (DockItem child in Children)
        {
            child.DisposeWidget();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
