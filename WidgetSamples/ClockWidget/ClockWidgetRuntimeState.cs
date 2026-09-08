namespace GlueDock.ClockWidget;

public sealed class ClockWidgetRuntimeState
{
    public List<ClockWidgetAlarmEntry> Alarms { get; set; } =
        new();

    public List<ClockWidgetTimerEntry> Timers { get; set; } =
        new();

    public ClockWidgetStopwatchState Stopwatch { get; set; } =
        new();
}

public sealed class ClockWidgetAlarmEntry
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString(
            "N");

    public DateTime TriggerAt { get; set; }

    public string Note { get; set; } =
        string.Empty;

    public bool Enabled { get; set; } =
        true;
}

public sealed class ClockWidgetTimerEntry
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString(
            "N");

    public string Note { get; set; } =
        string.Empty;

    public int InitialSeconds { get; set; }

    public int RemainingSeconds { get; set; }

    public bool IsRunning { get; set; }

    public DateTime? EndUtc { get; set; }

    public bool HasFired { get; set; }
}

public sealed class ClockWidgetStopwatchState
{
    public long ElapsedMilliseconds { get; set; }

    public bool IsRunning { get; set; }

    public DateTime? StartedUtc { get; set; }
}
