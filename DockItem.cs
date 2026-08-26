using System.Text.Json.Serialization;
using System.Windows.Media;

namespace GlueDock;

public sealed class DockItem
{
    public required string Path { get; init; }

    public required string DisplayName { get; init; }

    [JsonIgnore]
    public ImageSource? Icon { get; set; }
}
