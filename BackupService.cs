using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace GlueDock;

public static class BackupService
{
    private const int BackupFormatVersion = 1;

    private const string PendingRestoreDirectoryName =
        "GlueDock_Restore";

    private const string PendingRestoreFileName =
        "pending.zip";

    private static readonly string[] ManagedDirectories =
    [
        "GlueDock_Data",
        "GlueDock_Iconset",
        "GlueDock_Profiles",
        "GlueDock_Repository",
        "GlueDock_settings",
        "GlueDock_Themes",
        "GlueDock_Widgets"
    ];

    public static void CreateBackup(
        string backupPath)
    {
        if (string.IsNullOrWhiteSpace(
                backupPath))
        {
            throw new ArgumentException(
                "Backup path must not be empty.",
                nameof(backupPath));
        }

        string fullBackupPath =
            Path.GetFullPath(
                backupPath);

        string? backupDirectory =
            Path.GetDirectoryName(
                fullBackupPath);

        if (!string.IsNullOrWhiteSpace(
                backupDirectory))
        {
            Directory.CreateDirectory(
                backupDirectory);
        }

        string temporaryPath =
            fullBackupPath +
            ".tmp";

        try
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }

            using FileStream stream =
                File.Open(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None);

            using ZipArchive archive =
                new(
                    stream,
                    ZipArchiveMode.Create,
                    leaveOpen: false);

            BackupManifest manifest =
                new()
                {
                    FormatVersion =
                        BackupFormatVersion,
                    CreatedUtc =
                        DateTime.UtcNow,
                    SourceBaseDirectory =
                        Path.GetFullPath(
                            AppContext.BaseDirectory),
                    Directories =
                        ManagedDirectories
                            .Where(
                                directoryName =>
                                    Directory.Exists(
                                        Path.Combine(
                                            AppContext.BaseDirectory,
                                            directoryName)))
                            .ToList()
                };

            ZipArchiveEntry manifestEntry =
                archive.CreateEntry(
                    "manifest.json",
                    CompressionLevel.Optimal);

            using (Stream manifestStream =
                   manifestEntry.Open())
            {
                JsonSerializer.Serialize(
                    manifestStream,
                    manifest,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
            }

            foreach (string directoryName
                     in manifest.Directories)
            {
                string sourceDirectory =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        directoryName);

                AddDirectory(
                    archive,
                    sourceDirectory,
                    directoryName);
            }

            archive.Dispose();
            stream.Dispose();

            File.Move(
                temporaryPath,
                fullBackupPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    public static void QueueRestore(
        string backupPath)
    {
        ValidateBackup(
            backupPath);

        string restoreDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                PendingRestoreDirectoryName);

        Directory.CreateDirectory(
            restoreDirectory);

        string pendingPath =
            Path.Combine(
                restoreDirectory,
                PendingRestoreFileName);

        string temporaryPath =
            pendingPath +
            ".tmp";

        try
        {
            File.Copy(
                backupPath,
                temporaryPath,
                overwrite: true);

            File.Move(
                temporaryPath,
                pendingPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    public static BackupRestoreResult ApplyPendingRestore()
    {
        string restoreDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                PendingRestoreDirectoryName);

        string pendingPath =
            Path.Combine(
                restoreDirectory,
                PendingRestoreFileName);

        if (!File.Exists(
                pendingPath))
        {
            return BackupRestoreResult.NotPending;
        }

        string temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "GlueDock_Restore_" +
                Guid.NewGuid().ToString(
                    "N"));

        try
        {
            Directory.CreateDirectory(
                temporaryDirectory);

            BackupManifest manifest =
                ExtractBackup(
                    pendingPath,
                    temporaryDirectory);

            foreach (string directoryName
                     in ManagedDirectories)
            {
                string sourceDirectory =
                    Path.Combine(
                        temporaryDirectory,
                        directoryName);

                string targetDirectory =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        directoryName);

                if (Directory.Exists(
                        targetDirectory))
                {
                    Directory.Delete(
                        targetDirectory,
                        recursive: true);
                }

                if (Directory.Exists(
                        sourceDirectory))
                {
                    CopyDirectory(
                        sourceDirectory,
                        targetDirectory);
                }
            }

            RebaseManagedPaths(
                manifest.SourceBaseDirectory,
                AppContext.BaseDirectory);

            File.Delete(
                pendingPath);

            TryDeleteDirectory(
                restoreDirectory);

            return BackupRestoreResult.Success;
        }
        catch (Exception ex)
        {
            return new BackupRestoreResult(
                false,
                true,
                ex.Message);
        }
        finally
        {
            TryDeleteDirectory(
                temporaryDirectory);
        }
    }

    public static void ValidateBackup(
        string backupPath)
    {
        if (string.IsNullOrWhiteSpace(
                backupPath) ||
            !File.Exists(
                backupPath))
        {
            throw new FileNotFoundException(
                "The selected backup file was not found.",
                backupPath);
        }

        using ZipArchive archive =
            ZipFile.OpenRead(
                backupPath);

        BackupManifest manifest =
            ReadManifest(
                archive);

        ValidateManifest(
            manifest);

        foreach (ZipArchiveEntry entry
                 in archive.Entries)
        {
            ValidateEntryPath(
                entry.FullName);
        }
    }

    private static BackupManifest ExtractBackup(
        string backupPath,
        string destinationDirectory)
    {
        using ZipArchive archive =
            ZipFile.OpenRead(
                backupPath);

        BackupManifest manifest =
            ReadManifest(
                archive);

        ValidateManifest(
            manifest);

        string fullDestinationDirectory =
            Path.GetFullPath(
                destinationDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        foreach (ZipArchiveEntry entry
                 in archive.Entries)
        {
            ValidateEntryPath(
                entry.FullName);

            if (string.Equals(
                    entry.FullName,
                    "manifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destinationPath =
                Path.GetFullPath(
                    Path.Combine(
                        destinationDirectory,
                        entry.FullName));

            if (!destinationPath.StartsWith(
                    fullDestinationDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The backup contains an invalid path.");
            }

            if (string.IsNullOrEmpty(
                    entry.Name))
            {
                Directory.CreateDirectory(
                    destinationPath);

                continue;
            }

            string? destinationParent =
                Path.GetDirectoryName(
                    destinationPath);

            if (!string.IsNullOrWhiteSpace(
                    destinationParent))
            {
                Directory.CreateDirectory(
                    destinationParent);
            }

            entry.ExtractToFile(
                destinationPath,
                overwrite: true);
        }

        return manifest;
    }

    private static BackupManifest ReadManifest(
        ZipArchive archive)
    {
        ZipArchiveEntry? manifestEntry =
            archive.GetEntry(
                "manifest.json");

        if (manifestEntry is null)
        {
            throw new InvalidDataException(
                "The selected file is not a GlueDock backup.");
        }

        using Stream stream =
            manifestEntry.Open();

        BackupManifest? manifest =
            JsonSerializer.Deserialize<BackupManifest>(
                stream);

        return manifest ??
               throw new InvalidDataException(
                   "The backup manifest is invalid.");
    }

    private static void ValidateManifest(
        BackupManifest manifest)
    {
        if (manifest.FormatVersion !=
            BackupFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported GlueDock backup format: {manifest.FormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.SourceBaseDirectory))
        {
            throw new InvalidDataException(
                "The backup does not contain a valid source directory.");
        }

        if (manifest.Directories.Any(
                directoryName =>
                    !ManagedDirectories.Contains(
                        directoryName,
                        StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "The backup contains an unsupported GlueDock directory.");
        }
    }

    private static void ValidateEntryPath(
        string entryPath)
    {
        string normalized =
            entryPath.Replace(
                '\\',
                '/');

        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            return;
        }

        if (normalized.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "../",
                StringComparison.Ordinal) ||
            normalized.Equals(
                "..",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The backup contains an invalid path.");
        }

        string topLevel =
            normalized.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ??
            string.Empty;

        if (!string.Equals(
                topLevel,
                "manifest.json",
                StringComparison.OrdinalIgnoreCase) &&
            !ManagedDirectories.Contains(
                topLevel,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The backup contains an unsupported file.");
        }
    }

    private static void AddDirectory(
        ZipArchive archive,
        string sourceDirectory,
        string archiveDirectory)
    {
        archive.CreateEntry(
            archiveDirectory.TrimEnd('/') + "/");

        foreach (string directory
                 in Directory.GetDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    directory);

            string entryName =
                NormalizeArchivePath(
                    Path.Combine(
                        archiveDirectory,
                        relativePath))
                .TrimEnd(
                    '/')
                + "/";

            archive.CreateEntry(
                entryName);
        }

        foreach (string file
                 in Directory.GetFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    file);

            string entryName =
                NormalizeArchivePath(
                    Path.Combine(
                        archiveDirectory,
                        relativePath));

            archive.CreateEntryFromFile(
                file,
                entryName,
                CompressionLevel.Optimal);
        }
    }

    private static string NormalizeArchivePath(
        string path)
    {
        return path.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        Directory.CreateDirectory(
            destinationDirectory);

        foreach (string file
                 in Directory.GetFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            File.Copy(
                file,
                Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(
                        file)),
                overwrite: true);
        }

        foreach (string directory
                 in Directory.GetDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(
                directory,
                Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(
                        directory)));
        }
    }

    private static void RebaseManagedPaths(
        string sourceBaseDirectory,
        string targetBaseDirectory)
    {
        string settingsPath =
            Path.Combine(
                targetBaseDirectory,
                "GlueDock_settings",
                "settings.json");

        if (!File.Exists(
                settingsPath))
        {
            return;
        }

        DockSettings? settings;

        try
        {
            settings =
                JsonSerializer.Deserialize<DockSettings>(
                    File.ReadAllText(
                        settingsPath));
        }
        catch
        {
            return;
        }

        if (settings is null)
        {
            return;
        }

        foreach (DockEntrySettings entry
                 in settings.DockItems)
        {
            RebaseEntry(
                entry,
                sourceBaseDirectory,
                targetBaseDirectory);
        }

        for (int index = 0;
             index < settings.Items.Count;
             index++)
        {
            settings.Items[index] =
                RebasePath(
                    settings.Items[index],
                    sourceBaseDirectory,
                    targetBaseDirectory);
        }

        string json =
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            settingsPath,
            json);
    }

    private static void RebaseEntry(
        DockEntrySettings entry,
        string sourceBaseDirectory,
        string targetBaseDirectory)
    {
        entry.Path =
            RebasePath(
                entry.Path,
                sourceBaseDirectory,
                targetBaseDirectory);

        foreach (DockEntrySettings child
                 in entry.Children)
        {
            RebaseEntry(
                child,
                sourceBaseDirectory,
                targetBaseDirectory);
        }
    }

    private static string RebasePath(
        string path,
        string sourceBaseDirectory,
        string targetBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return path;
        }

        try
        {
            string normalizedSourceBase =
                Path.GetFullPath(
                    sourceBaseDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            string normalizedPath =
                Path.GetFullPath(
                    path);

            string sourcePrefix =
                normalizedSourceBase +
                Path.DirectorySeparatorChar;

            if (!normalizedPath.StartsWith(
                    sourcePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            string relativePath =
                Path.GetRelativePath(
                    normalizedSourceBase,
                    normalizedPath);

            return Path.Combine(
                targetBaseDirectory,
                relativePath);
        }
        catch
        {
            return path;
        }
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class BackupManifest
    {
        public int FormatVersion { get; set; }

        public DateTime CreatedUtc { get; set; }

        public string SourceBaseDirectory { get; set; } =
            string.Empty;

        public List<string> Directories { get; set; } =
            [];
    }
}

public sealed record BackupRestoreResult(
    bool Restored,
    bool Pending,
    string ErrorMessage)
{
    public static BackupRestoreResult NotPending { get; } =
        new(
            false,
            false,
            string.Empty);

    public static BackupRestoreResult Success { get; } =
        new(
            true,
            false,
            string.Empty);
}
