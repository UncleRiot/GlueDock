using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace GlueNotes;

public sealed class NoteRepository
{
    private const string GroupsFileName =
        "Groups.json";

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented =
                true
        };

    public string RootDirectory { get; }

    public string AssetsDirectory =>
        Path.Combine(
            RootDirectory,
            "Assets");

    private string GroupsFilePath =>
        Path.Combine(
            RootDirectory,
            GroupsFileName);

    public NoteRepository(
        string? rootDirectory = null)
    {
        RootDirectory =
            string.IsNullOrWhiteSpace(
                rootDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "GlueNotes")
                : Path.GetFullPath(
                    rootDirectory);

        Directory.CreateDirectory(
            RootDirectory);

        Directory.CreateDirectory(
            AssetsDirectory);
    }

    public ObservableCollection<NoteSummary> LoadSummaries()
    {
        List<NoteSummary> result =
            new();

        foreach (string filePath in
                 Directory.EnumerateFiles(
                     RootDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(
                    Path.GetFileName(
                        filePath),
                    GroupsFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                NoteData? note =
                    JsonSerializer.Deserialize<NoteData>(
                        File.ReadAllText(
                            filePath),
                        _jsonOptions);

                if (note is null ||
                    note.Id ==
                    Guid.Empty)
                {
                    continue;
                }

                result.Add(
                    new NoteSummary
                    {
                        Id =
                            note.Id,
                        Title =
                            note.Title,
                        UpdatedAtUtc =
                            note.UpdatedAtUtc,
                        GroupId =
                            note.GroupId,
                        SortOrder =
                            note.SortOrder
                    });
            }
            catch
            {
                // A malformed note must not prevent the remaining notes from loading.
            }
        }

        return
            new ObservableCollection<NoteSummary>(
                result
                    .OrderBy(
                        note =>
                            note.SortOrder)
                    .ThenByDescending(
                        note =>
                            note.UpdatedAtUtc));
    }

    public List<NoteGroupData> LoadGroups()
    {
        if (!File.Exists(
                GroupsFilePath))
        {
            return
                new List<NoteGroupData>();
        }

        try
        {
            return
                JsonSerializer.Deserialize<List<NoteGroupData>>(
                    File.ReadAllText(
                        GroupsFilePath),
                    _jsonOptions) ??
                new List<NoteGroupData>();
        }
        catch
        {
            return
                new List<NoteGroupData>();
        }
    }

    public void SaveGroups(
        IEnumerable<NoteGroupData> groups)
    {
        Directory.CreateDirectory(
            RootDirectory);

        File.WriteAllText(
            GroupsFilePath,
            JsonSerializer.Serialize(
                groups
                    .OrderBy(
                        group =>
                            group.SortOrder)
                    .ToList(),
                _jsonOptions));
    }

    public NoteData? Load(
        Guid id)
    {
        string filePath =
            GetNoteFilePath(
                id);

        if (!File.Exists(
                filePath))
        {
            return null;
        }

        return
            JsonSerializer.Deserialize<NoteData>(
                File.ReadAllText(
                    filePath),
                _jsonOptions);
    }

    public void Save(
        NoteData note,
        bool updateTimestamp = true)
    {
        Directory.CreateDirectory(
            RootDirectory);

        if (updateTimestamp)
        {
            note.UpdatedAtUtc =
                DateTime.UtcNow;
        }

        File.WriteAllText(
            GetNoteFilePath(
                note.Id),
            JsonSerializer.Serialize(
                note,
                _jsonOptions));
    }

    public void UpdateNoteLocation(
        Guid noteId,
        Guid groupId,
        int sortOrder)
    {
        NoteData? note =
            Load(
                noteId);

        if (note is null)
        {
            return;
        }

        note.GroupId =
            groupId;

        note.SortOrder =
            sortOrder;

        Save(
            note,
            false);
    }

    public void Delete(
        Guid noteId)
    {
        string filePath =
            GetNoteFilePath(
                noteId);

        if (File.Exists(
                filePath))
        {
            File.Delete(
                filePath);
        }

        string noteAssetDirectory =
            Path.Combine(
                AssetsDirectory,
                noteId.ToString(
                    "N"));

        if (Directory.Exists(
                noteAssetDirectory))
        {
            Directory.Delete(
                noteAssetDirectory,
                true);
        }
    }

    public string ImportClipboardImage(
        Guid noteId,
        BitmapSource bitmapSource)
    {
        string noteAssetDirectory =
            Path.Combine(
                AssetsDirectory,
                noteId.ToString(
                    "N"));

        Directory.CreateDirectory(
            noteAssetDirectory);

        string fileName =
            $"{Guid.NewGuid():N}.png";

        string destinationPath =
            Path.Combine(
                noteAssetDirectory,
                fileName);

        PngBitmapEncoder encoder =
            new();

        encoder.Frames.Add(
            BitmapFrame.Create(
                bitmapSource));

        using FileStream stream =
            File.Create(
                destinationPath);

        encoder.Save(
            stream);

        return
            Path.GetRelativePath(
                RootDirectory,
                destinationPath);
    }

    public string ImportImage(
        Guid noteId,
        string sourceFilePath)
    {
        string noteAssetDirectory =
            Path.Combine(
                AssetsDirectory,
                noteId.ToString(
                    "N"));

        Directory.CreateDirectory(
            noteAssetDirectory);

        string extension =
            Path.GetExtension(
                sourceFilePath);

        string fileName =
            $"{Guid.NewGuid():N}{extension}";

        string destinationPath =
            Path.Combine(
                noteAssetDirectory,
                fileName);

        File.Copy(
            sourceFilePath,
            destinationPath,
            overwrite:
                false);

        return
            Path.GetRelativePath(
                RootDirectory,
                destinationPath);
    }

    public string GetAbsoluteAssetPath(
        string relativePath)
    {
        return
            Path.GetFullPath(
                Path.Combine(
                    RootDirectory,
                    relativePath));
    }

    private string GetNoteFilePath(
        Guid id)
    {
        return
            Path.Combine(
                RootDirectory,
                $"{id:N}.json");
    }
}
