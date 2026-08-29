namespace GlueDock;

public sealed class DockTheme
{
    public string Name { get; set; } = "Default";

    public string DockBackgroundColor { get; set; } = "#12161C";

    public string ItemBackgroundColor { get; set; } = "#22FFFFFF";

    public string ItemBorderColor { get; set; } = "#22FFFFFF";

    public string TextColor { get; set; } = "#FFFFFF";

    public string SubmenuIndicatorColor { get; set; } = "#FFFFFF";

    public string DragGhostBackgroundColor { get; set; } = "#22FFFFFF";

    public string DragGhostBorderColor { get; set; } = "#66FFFFFF";

    public bool GlassSurfaceEnabled { get; set; }

    public string GlassTopColor { get; set; } = "#00FFFFFF";

    public string GlassBottomColor { get; set; } = "#00FFFFFF";

    public string GlassHighlightColor { get; set; } = "#00FFFFFF";

    public string GlassShadowColor { get; set; } = "#000000";

    public double GlassCornerRadius { get; set; } = 20;

    public double GlassExtraThickness { get; set; }
}

public sealed record DockThemeOption(
    string Id,
    string DisplayName);
