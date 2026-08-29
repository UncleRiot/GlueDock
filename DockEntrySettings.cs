namespace GlueDock;

public sealed class DockEntrySettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsSubmenu { get; set; }
    public string SubmenuIconRepositoryPath { get; set; } = string.Empty;
    public List<DockEntrySettings> Children { get; set; } = [];
}
