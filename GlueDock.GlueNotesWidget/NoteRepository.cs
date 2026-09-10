using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace GlueNotes;

public sealed class NoteRepository
{
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
        ObservableCollection<NoteSummary> result =
            new();

        foreach (string filePath in
                 Directory.EnumerateFiles(
                     RootDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                NoteData? note =
                    JsonSerializer.Deserialize<NoteData>(
                        File.ReadAllText(
                            filePath),
                        _jsonOptions);

                if (note is null)
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
                            note.UpdatedAtUtc
                    });
            }
            catch
            {
                // A malformed note must not prevent the remaining notes from loading.
            }
        }

        return
            new ObservableCollection<NoteSummary>(
                result.OrderByDescending(
                    note =>
                        note.UpdatedAtUtc));
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
        NoteData note)
    {
        Directory.CreateDirectory(
            RootDirectory);

        note.UpdatedAtUtc =
            DateTime.UtcNow;

        File.WriteAllText(
            GetNoteFilePath(
                note.Id),
            JsonSerializer.Serialize(
                note,
                _jsonOptions));
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
