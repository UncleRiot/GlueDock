using System.Windows.Media;

namespace GlueNotes;

public sealed class GlueNotesAppearance
{
    public required Brush WindowBrush { get; init; }

    public required Brush SurfaceBrush { get; init; }

    public required Brush SurfaceHoverBrush { get; init; }

    public required Brush BorderBrush { get; init; }

    public required Brush TextBrush { get; init; }

    public required Brush MutedTextBrush { get; init; }

    public required Brush AccentBrush { get; init; }

    public required Brush SelectionBrush { get; init; }
}
