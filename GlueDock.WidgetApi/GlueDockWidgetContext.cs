namespace GlueDock;

public sealed class GlueDockWidgetContext
{
    public required string InstanceId { get; init; }

    public required string WidgetDirectory { get; init; }

    public required string SettingsDirectory { get; init; }

    public Action<string, string>? Log { get; init; }

    public Func<string, string>? Localize { get; init; }

    public Action<EventHandler>? SubscribeLanguageChanged { get; init; }

    public Action<EventHandler>? UnsubscribeLanguageChanged { get; init; }

    public Func<string>? GetLanguageCode { get; init; }

    public Action<string>? OpenSettingsSection { get; init; }
}
