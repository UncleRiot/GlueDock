namespace GlueDock.ClockWidget;

internal static class ClockWidgetLog
{
    public static Action<string, string>? Writer { get; set; }

    public static void Write(
        string category,
        string message)
    {
        Writer?.Invoke(
            category,
            message);
    }
}
