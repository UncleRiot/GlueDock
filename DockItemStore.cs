using System.IO;

namespace GlueDock;

public sealed class DockItemStore
{
    private readonly string _itemsDirectory;

    public DockItemStore()
    {
        _itemsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Data",
            "Items");
    }

    public string Import(
        string sourcePath,
        bool moveSource = false)
    {
        if (!File.Exists(sourcePath) &&
            !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                "Die Quelle wurde nicht gefunden.",
                sourcePath);
        }

        Directory.CreateDirectory(
            _itemsDirectory);

        string itemDirectory =
            Path.Combine(
                _itemsDirectory,
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            itemDirectory);

        try
        {
            if (File.Exists(sourcePath))
            {
                string fileName =
                    Path.GetFileName(sourcePath);

                string destinationPath =
                    Path.Combine(
                        itemDirectory,
                        fileName);

                File.Copy(
                    sourcePath,
                    destinationPath,
                    overwrite: false);

                CopyFileAttributes(
                    sourcePath,
                    destinationPath);

                if (moveSource)
                {
                    File.Delete(
                        sourcePath);
                }

                return destinationPath;
            }

            string trimmedSourcePath =
                sourcePath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            string directoryName =
                Path.GetFileName(
                    trimmedSourcePath);

            if (string.IsNullOrWhiteSpace(
                    directoryName))
            {
                directoryName = "Folder";
            }

            string destinationDirectory =
                Path.Combine(
                    itemDirectory,
                    directoryName);

            CopyDirectory(
                sourcePath,
                destinationDirectory);

            if (moveSource)
            {
                Directory.Delete(
                    sourcePath,
                    recursive: true);
            }

            return destinationDirectory;
        }
        catch
        {
            TryDeleteDirectory(
                itemDirectory);

            throw;
        }
    }

    public bool IsManagedPath(
        string path)
    {
        string fullItemsDirectory =
            Path.GetFullPath(
                _itemsDirectory)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        string fullPath =
            Path.GetFullPath(path);

        return fullPath.StartsWith(
            fullItemsDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    public void DeleteManagedItem(
        string path)
    {
        if (!IsManagedPath(path))
        {
            return;
        }

        string? current =
            File.Exists(path)
                ? Path.GetDirectoryName(path)
                : path;

        if (string.IsNullOrWhiteSpace(
                current))
        {
            return;
        }

        string fullItemsDirectory =
            Path.GetFullPath(
                _itemsDirectory)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        DirectoryInfo? directory =
            new DirectoryInfo(current);

        while (directory.Parent is not null &&
               !string.Equals(
                   directory.Parent.FullName
                       .TrimEnd(
                           Path.DirectorySeparatorChar,
                           Path.AltDirectorySeparatorChar),
                   fullItemsDirectory,
                   StringComparison.OrdinalIgnoreCase))
        {
            directory = directory.Parent;
        }

        if (directory.Parent is null ||
            !string.Equals(
                directory.Parent.FullName
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                fullItemsDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDeleteDirectory(
            directory.FullName);
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        DirectoryInfo source =
            new(sourceDirectory);

        Directory.CreateDirectory(
            destinationDirectory);

        DirectoryInfo destination =
            new(destinationDirectory);

        destination.Attributes =
            source.Attributes;

        destination.CreationTimeUtc =
            source.CreationTimeUtc;

        destination.LastWriteTimeUtc =
            source.LastWriteTimeUtc;

        foreach (FileInfo file in source.GetFiles())
        {
            string destinationFile =
                Path.Combine(
                    destinationDirectory,
                    file.Name);

            file.CopyTo(
                destinationFile,
                overwrite: false);

            CopyFileAttributes(
                file.FullName,
                destinationFile);
        }

        foreach (DirectoryInfo directory in source.GetDirectories())
        {
            CopyDirectory(
                directory.FullName,
                Path.Combine(
                    destinationDirectory,
                    directory.Name));
        }
    }

    private static void CopyFileAttributes(
        string sourcePath,
        string destinationPath)
    {
        FileInfo source =
            new(sourcePath);

        FileInfo destination =
            new(destinationPath);

        destination.Attributes =
            source.Attributes;

        destination.CreationTimeUtc =
            source.CreationTimeUtc;

        destination.LastWriteTimeUtc =
            source.LastWriteTimeUtc;
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
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
}
