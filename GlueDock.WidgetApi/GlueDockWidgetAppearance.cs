using System.Windows.Media;

namespace GlueDock;

public sealed class GlueDockWidgetAppearance
{
    public required string ThemeName { get; init; }

    public required Brush DockBackgroundBrush { get; init; }

    public required Brush DockItemBackgroundBrush { get; init; }

    public required Brush DockItemBorderBrush { get; init; }

    public required Brush DockTextBrush { get; init; }

    public required bool DockBorderEnabled { get; init; }

    public required Color DockBorderColor { get; init; }

    public required double Opacity { get; init; }

    public required double BlurRadius { get; init; }

    public required bool GlassSurfaceEnabled { get; init; }

    public required Color GlassTopColor { get; init; }

    public required Color GlassBottomColor { get; init; }

    public required Color GlassHighlightColor { get; init; }

    public required double GlassCornerRadius { get; init; }

    public double GlassGradientReferenceHeight { get; init; } = 88;

    public IReadOnlyList<GlueDockWidgetThemeAppearance> AvailableThemes { get; init; } =
        Array.Empty<GlueDockWidgetThemeAppearance>();
}

public sealed class GlueDockWidgetThemeAppearance
{
    public required string ThemeName { get; init; }

    public required string DisplayName { get; init; }

    public required Brush DockBackgroundBrush { get; init; }

    public required Brush DockItemBackgroundBrush { get; init; }

    public required Brush DockItemBorderBrush { get; init; }

    public required Brush DockTextBrush { get; init; }

    public required double Opacity { get; init; }

    public required double BlurRadius { get; init; }

    public required bool GlassSurfaceEnabled { get; init; }

    public required Color GlassTopColor { get; init; }

    public required Color GlassBottomColor { get; init; }

    public required Color GlassHighlightColor { get; init; }

    public required double GlassCornerRadius { get; init; }

    public double GlassGradientReferenceHeight { get; init; } = 88;
}
