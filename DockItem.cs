using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace GlueDock;

public sealed class DockItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private string _displayName = string.Empty;

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

    public string SubmenuIconRepositoryPath { get; set; } = string.Empty;

    public ObservableCollection<DockItem> Children { get; } = [];

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
