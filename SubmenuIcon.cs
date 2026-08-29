using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;

namespace GlueDock;

public static class SubmenuIcon
{
    private const uint ShgsiIcon = 0x000000100;
    private const uint ShgsiLargeIcon = 0x000000000;

    public static IReadOnlyList<SubmenuWindowsIconOption> WindowsIconOptions { get; } =
    [
        new("Folder", "Folder"),
        new("FolderOpen", "Open folder"),
        new("DocNoAssoc", "Document"),
        new("Application", "Application"),
        new("Computer", "Computer"),
        new("DriveFixed", "Drive"),
        new("Network", "Network"),
        new("Info", "Information"),
        new("Warning", "Warning"),
        new("Error", "Error"),
        new("Shield", "Shield")
    ];

    public static ImageSource Create(
        DockSettings settings,
        string submenuIconRepositoryPath)
    {
        ImageSource? itemImage =
            LoadRepositoryImage(
                submenuIconRepositoryPath);

        return itemImage ??
               Create(
                   settings);
    }

    public static ImageSource Create(
        DockSettings settings)
    {
        if (string.Equals(
                settings.SubmenuIconMode,
                "Repository",
                StringComparison.OrdinalIgnoreCase))
        {
            ImageSource? repositoryImage =
                LoadRepositoryImage(
                    settings.SubmenuIconRepositoryPath);

            if (repositoryImage is not null)
            {
                return repositoryImage;
            }
        }

        if (string.Equals(
                settings.SubmenuIconMode,
                "Windows",
                StringComparison.OrdinalIgnoreCase))
        {
            ImageSource? selectedWindowsIcon =
                LoadWindowsIcon(
                    settings.SubmenuWindowsIconPath,
                    settings.SubmenuWindowsIconIndex);

            if (selectedWindowsIcon is not null)
            {
                return selectedWindowsIcon;
            }

            ImageSource? legacyWindowsIcon =
                LoadWindowsIcon(
                    settings.SubmenuWindowsIcon);

            if (legacyWindowsIcon is not null)
            {
                return legacyWindowsIcon;
            }
        }

        return CreateDefault();
    }

    public static bool TryPickWindowsIcon(
        Window owner,
        ref string iconPath,
        ref int iconIndex)
    {
        const int pathCapacity = 1024;

        StringBuilder pathBuffer =
            new(
                pathCapacity);

        pathBuffer.Append(
            iconPath);

        WindowInteropHelper helper =
            new(owner);

        int selectedIndex =
            iconIndex;

        int result =
            PickIconDlg(
                helper.Handle,
                pathBuffer,
                (uint)pathCapacity,
                ref selectedIndex);

        if (result == 0)
        {
            return false;
        }

        iconPath =
            Environment.ExpandEnvironmentVariables(
                pathBuffer.ToString());

        iconIndex =
            selectedIndex;

        return true;
    }

    public static ImageSource? CreateWindowsPreview(
        string iconName)
    {
        return LoadWindowsIcon(
            iconName);
    }

    private static ImageSource CreateDefault()
    {
        string defaultIconPath =
            System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Iconset",
                "GlueDock_Iconset_menu_vivid_orange_96x96_transparent.gif");

        if (System.IO.File.Exists(
                defaultIconPath))
        {
            try
            {
                using System.IO.FileStream stream =
                    System.IO.File.Open(
                        defaultIconPath,
                        System.IO.FileMode.Open,
                        System.IO.FileAccess.Read,
                        System.IO.FileShare.ReadWrite);

                BitmapImage image = new();

                image.BeginInit();
                image.CacheOption =
                    BitmapCacheOption.OnLoad;
                image.StreamSource =
                    stream;
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch
            {
            }
        }

        DrawingGroup group = new();

        group.Children.Add(
            new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(0xE4, 0xC0, 0x55)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xE5, 0x92)), 1),
                Geometry.Parse("M 3,10 L 14,10 L 18,15 L 37,15 L 37,33 L 3,33 Z")));

        group.Children.Add(
            new GeometryDrawing(
                new SolidColorBrush(Colors.White),
                null,
                Geometry.Parse("M 19,18 L 28,23 L 19,28 Z")));

        DrawingImage fallbackImage =
            new(group);

        fallbackImage.Freeze();

        return fallbackImage;
    }

    private static ImageSource? LoadRepositoryImage(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            return null;
        }

        string path =
            IconRepository.GetAbsolutePath(
                relativePath);

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        try
        {
            using System.IO.FileStream stream =
                System.IO.File.Open(
                    path,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite);

            BitmapImage image = new();

            image.BeginInit();
            image.CacheOption =
                BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadWindowsIcon(
        string iconPath,
        int iconIndex)
    {
        if (string.IsNullOrWhiteSpace(
                iconPath))
        {
            return null;
        }

        string expandedPath =
            Environment.ExpandEnvironmentVariables(
                iconPath);

        nint largeIcon = 0;
        nint smallIcon = 0;

        try
        {
            uint extracted =
                ExtractIconEx(
                    expandedPath,
                    iconIndex,
                    out largeIcon,
                    out smallIcon,
                    1);

            nint selectedIcon =
                largeIcon != 0
                    ? largeIcon
                    : smallIcon;

            if (extracted == 0 ||
                selectedIcon == 0)
            {
                return null;
            }

            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    selectedIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (largeIcon != 0)
            {
                DestroyIcon(
                    largeIcon);
            }

            if (smallIcon != 0 &&
                smallIcon != largeIcon)
            {
                DestroyIcon(
                    smallIcon);
            }
        }
    }

    private static ImageSource? LoadWindowsIcon(
        string iconName)
    {
        if (!TryGetStockIconId(
                iconName,
                out ShStockIconId stockIconId))
        {
            return null;
        }

        ShStockIconInfo iconInfo =
            new()
            {
                cbSize =
                    (uint)Marshal.SizeOf<ShStockIconInfo>()
            };

        int result =
            SHGetStockIconInfo(
                stockIconId,
                ShgsiIcon |
                ShgsiLargeIcon,
                ref iconInfo);

        if (result < 0 ||
            iconInfo.hIcon == 0)
        {
            return null;
        }

        try
        {
            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    iconInfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(
                iconInfo.hIcon);
        }
    }

    private static bool TryGetStockIconId(
        string iconName,
        out ShStockIconId stockIconId)
    {
        stockIconId =
            iconName switch
            {
                "Folder" => ShStockIconId.Folder,
                "FolderOpen" => ShStockIconId.FolderOpen,
                "DocNoAssoc" => ShStockIconId.DocNoAssoc,
                "Application" => ShStockIconId.Application,
                "Computer" => ShStockIconId.DesktopPc,
                "DriveFixed" => ShStockIconId.DriveFixed,
                "Network" => ShStockIconId.World,
                "Info" => ShStockIconId.Info,
                "Warning" => ShStockIconId.Warning,
                "Error" => ShStockIconId.Error,
                "Shield" => ShStockIconId.Shield,
                _ => ShStockIconId.Folder
            };

        return WindowsIconOptions.Any(
            option =>
                string.Equals(
                    option.Name,
                    iconName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private enum ShStockIconId : uint
    {
        DocNoAssoc = 0,
        Application = 2,
        Folder = 3,
        FolderOpen = 4,
        DriveFixed = 8,
        World = 13,
        Info = 79,
        Warning = 78,
        Error = 80,
        Shield = 77,
        DesktopPc = 94
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct ShStockIconInfo
    {
        public uint cbSize;
        public nint hIcon;
        public int iSysImageIndex;
        public int iIcon;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 260)]
        public string szPath;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(
        ShStockIconId siid,
        uint uFlags,
        ref ShStockIconInfo psii);

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int PickIconDlg(
        nint hwndOwner,
        StringBuilder pszIconPath,
        uint cchIconPath,
        ref int piIconIndex);

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string szFileName,
        int nIconIndex,
        out nint phiconLarge,
        out nint phiconSmall,
        uint nIcons);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(
        nint hIcon);
}

public sealed record SubmenuWindowsIconOption(
    string Name,
    string DisplayName);
