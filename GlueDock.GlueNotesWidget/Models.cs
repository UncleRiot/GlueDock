using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace GlueNotes;

public sealed class NoteSummary : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private DateTime _updatedAtUtc;
    private Guid _groupId;
    private int _sortOrder;
    private bool _isActive;
    private bool _isDropPreviewBefore;
    private bool _isDropPreviewAfter;

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
            OnPropertyChanged(nameof(UpdatedAtHint));
        }
    }

    public Guid GroupId
    {
        get => _groupId;
        set
        {
            if (_groupId == value)
            {
                return;
            }

            _groupId = value;
            OnPropertyChanged();
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            if (_sortOrder == value)
            {
                return;
            }

            _sortOrder = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveTextDecorations));
        }
    }

    [JsonIgnore]
    public TextDecorationCollection? ActiveTextDecorations =>
        IsActive
            ? TextDecorations.Underline
            : null;

    [JsonIgnore]
    public bool IsDropPreviewBefore
    {
        get => _isDropPreviewBefore;
        set
        {
            if (_isDropPreviewBefore == value)
            {
                return;
            }

            _isDropPreviewBefore = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsDropPreviewAfter
    {
        get => _isDropPreviewAfter;
        set
        {
            if (_isDropPreviewAfter == value)
            {
                return;
            }

            _isDropPreviewAfter = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public DateTime UpdatedAtLocal =>
        UpdatedAtUtc.ToLocalTime();

    [JsonIgnore]
    public string UpdatedAtHint =>
        UpdatedAtLocal.ToString(
            "d",
            CultureInfo.CurrentCulture);

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

public sealed class NoteGroupData
{
    public Guid Id { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public string ColorHex { get; set; } =
        "#FF808080";

    public int SortOrder { get; set; }
}

public sealed class NoteGroupViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _colorHex = "#FF808080";
    private int _sortOrder;
    private bool _isActive;
    private bool _isDropPreviewBefore;
    private bool _isDropPreviewAfter;

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex == value)
            {
                return;
            }

            _colorHex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorBrush));
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            if (_sortOrder == value)
            {
                return;
            }

            _sortOrder = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsDropPreviewBefore
    {
        get => _isDropPreviewBefore;
        set
        {
            if (_isDropPreviewBefore == value)
            {
                return;
            }

            _isDropPreviewBefore = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsDropPreviewAfter
    {
        get => _isDropPreviewAfter;
        set
        {
            if (_isDropPreviewAfter == value)
            {
                return;
            }

            _isDropPreviewAfter = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public Brush ColorBrush
    {
        get
        {
            try
            {
                return
                    (Brush)new BrushConverter()
                        .ConvertFromString(
                            ColorHex)!;
            }
            catch
            {
                return
                    new SolidColorBrush(
                        SystemColors.HighlightColor);
            }
        }
    }

    [JsonIgnore]
    public ObservableCollection<NoteSummary> Notes { get; } =
        new();

    public NoteGroupData ToData()
    {
        return
            new NoteGroupData
            {
                Id =
                    Id,
                Name =
                    Name,
                ColorHex =
                    ColorHex,
                SortOrder =
                    SortOrder
            };
    }

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

    public Guid GroupId { get; set; }

    public int SortOrder { get; set; }

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

    public string? FontFamilyName { get; set; }

    public string? ForegroundHex { get; set; }

    public string? BackgroundHex { get; set; }
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
