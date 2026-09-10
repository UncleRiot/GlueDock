using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace GlueNotes;

public sealed class NoteSummary : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private DateTime _updatedAtUtc;

    public Guid Id { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            OnPropertyChanged();
        }
    }

    public DateTime UpdatedAtUtc
    {
        get => _updatedAtUtc;
        set
        {
            if (_updatedAtUtc == value)
            {
                return;
            }

            _updatedAtUtc = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdatedAtLocal));
        }
    }

    [JsonIgnore]
    public DateTime UpdatedAtLocal =>
        UpdatedAtUtc.ToLocalTime();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

public sealed class NoteData
{
    public Guid Id { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<NoteBlockData> Blocks { get; set; } =
        new();
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(
    typeof(ParagraphBlockData),
    "paragraph")]
[JsonDerivedType(
    typeof(ImageBlockData),
    "image")]
[JsonDerivedType(
    typeof(CheckListBlockData),
    "checklist")]
public abstract class NoteBlockData
{
}

public sealed class ParagraphBlockData : NoteBlockData
{
    public List<TextRunData> Runs { get; set; } =
        new();

    public List<NoteInlineData> Inlines { get; set; } =
        new();
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(
    typeof(TextRunData),
    "text")]
[JsonDerivedType(
    typeof(ImageInlineData),
    "image")]
public abstract class NoteInlineData
{
}

public sealed class TextRunData : NoteInlineData
{
    public string Text { get; set; } =
        string.Empty;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public double FontSize { get; set; } =
        14;

    public string? ForegroundHex { get; set; }
}

public sealed class ImageInlineData : NoteInlineData
{
    public string AssetRelativePath { get; set; } =
        string.Empty;

    public double DisplayWidth { get; set; }
}

public sealed class ImageBlockData : NoteBlockData
{
    public string AssetRelativePath { get; set; } =
        string.Empty;

    public double DisplayWidth { get; set; } =
        420;
}

public sealed class CheckListBlockData : NoteBlockData
{
    public List<CheckListItemData> Items { get; set; } =
        new();
}

public sealed class CheckListItemData
{
    public bool IsChecked { get; set; }

    public string Text { get; set; } =
        string.Empty;
}
