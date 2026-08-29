using System.IO;

namespace GlueDock;

public static class IconRepository
{
    public static string Import(
        string sourcePath)
    {
        string extension =
            Path.GetExtension(
                sourcePath);

        string fileName =
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        string relativePath =
            Path.Combine(
                "Icons",
                fileName);

        string destinationPath =
            GetAbsolutePath(
                relativePath);

        string? destinationDirectory =
            Path.GetDirectoryName(
                destinationPath);

        if (!string.IsNullOrWhiteSpace(
                destinationDirectory))
        {
            Directory.CreateDirectory(
                destinationDirectory);
        }

        File.Copy(
            sourcePath,
            destinationPath,
            overwrite: false);

        return relativePath;
    }

    public static string GetAbsolutePath(
        string relativePath)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Repository",
            relativePath);
    }
}
