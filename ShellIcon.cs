using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlueDock;

public static class ShellIcon
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiPidl = 0x000000008;

    private const uint SiigbfBiggerSizeOk = 0x00000001;
    private const uint SiigbfThumbnailOnly = 0x00000008;
    private const uint SiigbfScaleUp = 0x00000100;

    public static bool TryGetShortcutLaunchInfo(
        string shortcutPath,
        out string targetPath,
        out string arguments,
        out string workingDirectory)
    {
        targetPath =
            string.Empty;

        arguments =
            string.Empty;

        workingDirectory =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                shortcutPath) ||
            !shortcutPath.EndsWith(
                ".lnk",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(
                shortcutPath))
        {
            return false;
        }

        try
        {
            ShellLink shellLink =
                (ShellLink)new CShellLink();

            IPersistFile persistFile =
                (IPersistFile)shellLink;

            persistFile.Load(
                shortcutPath,
                0);

            shellLink.Resolve(
                IntPtr.Zero,
                0x0001 |
                0x0008);

            StringBuilder targetPathBuilder =
                new(
                    32768);

            shellLink.GetPath(
                targetPathBuilder,
                targetPathBuilder.Capacity,
                IntPtr.Zero,
                0);

            StringBuilder argumentsBuilder =
                new(
                    32768);

            shellLink.GetArguments(
                argumentsBuilder,
                argumentsBuilder.Capacity);

            StringBuilder workingDirectoryBuilder =
                new(
                    32768);

            shellLink.GetWorkingDirectory(
                workingDirectoryBuilder,
                workingDirectoryBuilder.Capacity);

            targetPath =
                Environment.ExpandEnvironmentVariables(
                    targetPathBuilder.ToString());

            arguments =
                Environment.ExpandEnvironmentVariables(
                    argumentsBuilder.ToString());

            workingDirectory =
                Environment.ExpandEnvironmentVariables(
                    workingDirectoryBuilder.ToString());

            return
                !string.IsNullOrWhiteSpace(
                    targetPath);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException(
                "Shortcut.Resolve",
                ex);

            targetPath =
                string.Empty;

            arguments =
                string.Empty;

            workingDirectory =
                string.Empty;

            return false;
        }
    }

    public static BitmapSource? GetIcon(
        string path)
    {
        return GetIcon(
            path,
            useFilePreview: false);
    }

    public static BitmapSource? GetIcon(
        string path,
        bool useFilePreview)
    {
        return GetIcon(
            path,
            useFilePreview,
            DockSettings.ShortcutOverlayDefault);
    }

    public static BitmapSource? GetIcon(
        string path,
        bool useFilePreview,
        string shortcutOverlayMode)
    {
        if (useFilePreview &&
            (System.IO.File.Exists(path) ||
             System.IO.Directory.Exists(path)) &&
            !path.EndsWith(
                ".lnk",
                StringComparison.OrdinalIgnoreCase))
        {
            BitmapSource? preview =
                GetThumbnail(
                    path,
                    256);

            if (preview is not null)
            {
                return preview;
            }
        }

        bool isInternetShortcut =
            path.EndsWith(
                ".url",
                StringComparison.OrdinalIgnoreCase);

        bool isShellShortcut =
            path.EndsWith(
                ".lnk",
                StringComparison.OrdinalIgnoreCase);

        if (isInternetShortcut ||
            isShellShortcut)
        {
            if (string.Equals(
                    shortcutOverlayMode,
                    DockSettings.ShortcutOverlayNone,
                    StringComparison.OrdinalIgnoreCase))
            {
                BitmapSource? shortcutIcon =
                    isInternetShortcut
                        ? GetInternetShortcutBaseIcon(
                            path)
                        : GetShortcutTargetIcon(
                            path);

                DebugLog.Write(
                    "ShortcutOverlay",
                    $"Path={path}; Mode=None; CustomIcon={(shortcutIcon is not null)}; Type={(isInternetShortcut ? "URL" : "LNK")}");

                if (shortcutIcon is not null)
                {
                    return shortcutIcon;
                }
            }
            else if (string.Equals(
                         shortcutOverlayMode,
                         DockSettings.ShortcutOverlaySmall,
                         StringComparison.OrdinalIgnoreCase))
            {
                BitmapSource? shortcutIcon =
                    isInternetShortcut
                        ? GetInternetShortcutIconWithSmallOverlay(
                            path)
                        : GetShortcutIconWithSmallOverlay(
                            path);

                DebugLog.Write(
                    "ShortcutOverlay",
                    $"Path={path}; Mode=Small; CustomIcon={(shortcutIcon is not null)}; Type={(isInternetShortcut ? "URL" : "LNK")}");

                if (shortcutIcon is not null)
                {
                    return shortcutIcon;
                }
            }
        }

        return GetShellIcon(
            path);
    }

    private static BitmapSource? GetThumbnail(
        string path,
        int size)
    {
        try
        {
            Guid shellItemImageFactoryGuid =
                typeof(IShellItemImageFactory).GUID;

            int createResult =
                SHCreateItemFromParsingName(
                    path,
                    0,
                    ref shellItemImageFactoryGuid,
                    out IShellItemImageFactory? imageFactory);

            if (createResult < 0 ||
                imageFactory is null)
            {
                return null;
            }

            try
            {
                Size requestedSize =
                    new()
                    {
                        cx = size,
                        cy = size
                    };

                int imageResult =
                    imageFactory.GetImage(
                        requestedSize,
                        SiigbfThumbnailOnly |
                        SiigbfBiggerSizeOk |
                        SiigbfScaleUp,
                        out nint bitmapHandle);

                if (imageResult < 0 ||
                    bitmapHandle == 0)
                {
                    return null;
                }

                try
                {
                    BitmapSource source =
                        Imaging.CreateBitmapSourceFromHBitmap(
                            bitmapHandle,
                            0,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(
                        bitmapHandle);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(
                    imageFactory);
            }
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? GetInternetShortcutIconWithSmallOverlay(
        string path)
    {
        BitmapSource? baseIcon =
            GetInternetShortcutBaseIcon(
                path);

        BitmapSource? overlayIcon =
            GetLinkOverlayIcon();

        if (baseIcon is null ||
            overlayIcon is null)
        {
            return null;
        }

        const double overlayScale = 0.50;

        double width =
            baseIcon.PixelWidth;

        double height =
            baseIcon.PixelHeight;

        double overlayWidth =
            Math.Max(
                1,
                width *
                overlayScale);

        double overlayHeight =
            Math.Max(
                1,
                height *
                overlayScale);

        DrawingVisual visual =
            new();

        using (DrawingContext drawingContext =
               visual.RenderOpen())
        {
            drawingContext.DrawImage(
                baseIcon,
                new Rect(
                    0,
                    0,
                    width,
                    height));

            drawingContext.DrawImage(
                overlayIcon,
                new Rect(
                    0,
                    height - overlayHeight,
                    overlayWidth,
                    overlayHeight));
        }

        RenderTargetBitmap result =
            new(
                baseIcon.PixelWidth,
                baseIcon.PixelHeight,
                96,
                96,
                PixelFormats.Pbgra32);

        result.Render(
            visual);

        result.Freeze();

        return result;
    }

    private static BitmapSource? GetInternetShortcutBaseIcon(
        string path)
    {
        try
        {
            string? iconFile =
                null;

            int iconIndex =
                0;

            foreach (string line
                     in File.ReadLines(
                         path))
            {
                if (line.StartsWith(
                        "IconFile=",
                        StringComparison.OrdinalIgnoreCase))
                {
                    iconFile =
                        Environment.ExpandEnvironmentVariables(
                            line[
                                "IconFile=".Length..]
                                .Trim());

                    continue;
                }

                if (line.StartsWith(
                        "IconIndex=",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(
                        line[
                            "IconIndex=".Length..]
                            .Trim(),
                        out iconIndex);
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    iconFile) &&
                File.Exists(
                    iconFile))
            {
                BitmapSource? extractedIcon =
                    ExtractIcon(
                        iconFile,
                        iconIndex);

                if (extractedIcon is not null)
                {
                    DebugLog.Write(
                        "ShortcutOverlay",
                        $"URL base icon from IconFile; Shortcut={path}; IconFile={iconFile}; IconIndex={iconIndex}; Size={extractedIcon.PixelWidth}x{extractedIcon.PixelHeight}");

                    return extractedIcon;
                }
            }

            BitmapSource? shellIcon =
                GetShellIcon(
                    path);

            DebugLog.Write(
                "ShortcutOverlay",
                $"URL base icon fallback from shell; Shortcut={path}; IconFile={iconFile}; Size={(shellIcon is not null ? $"{shellIcon.PixelWidth}x{shellIcon.PixelHeight}" : "n/a")}");

            return shellIcon;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException(
                "ShortcutOverlay.URL",
                ex);

            return null;
        }
    }

    private static BitmapSource? GetShortcutIconWithSmallOverlay(
        string path)
    {
        BitmapSource? baseIcon =
            GetShortcutTargetIcon(
                path);

        BitmapSource? overlayIcon =
            GetLinkOverlayIcon();

        if (baseIcon is null ||
            overlayIcon is null)
        {
            return null;
        }

        const double overlayScale = 0.50;

        double width =
            baseIcon.PixelWidth;

        double height =
            baseIcon.PixelHeight;

        double overlayWidth =
            Math.Max(
                1,
                width *
                overlayScale);

        double overlayHeight =
            Math.Max(
                1,
                height *
                overlayScale);

        DrawingVisual visual =
            new();

        using (DrawingContext drawingContext =
               visual.RenderOpen())
        {
            drawingContext.DrawImage(
                baseIcon,
                new Rect(
                    0,
                    0,
                    width,
                    height));

            drawingContext.DrawImage(
                overlayIcon,
                new Rect(
                    0,
                    height - overlayHeight,
                    overlayWidth,
                    overlayHeight));
        }

        RenderTargetBitmap result =
            new(
                baseIcon.PixelWidth,
                baseIcon.PixelHeight,
                96,
                96,
                PixelFormats.Pbgra32);

        result.Render(
            visual);

        result.Freeze();

        return result;
    }

    private static BitmapSource? GetShortcutTargetIcon(
        string path)
    {
        try
        {
            ShellLink shellLink =
                (ShellLink)new CShellLink();

            IPersistFile persistFile =
                (IPersistFile)shellLink;

            persistFile.Load(
                path,
                0);

            StringBuilder targetPathBuilder =
                new(
                    32768);

            shellLink.GetPath(
                targetPathBuilder,
                targetPathBuilder.Capacity,
                IntPtr.Zero,
                0);

            string targetPath =
                targetPathBuilder.ToString();

            StringBuilder iconPathBuilder =
                new(
                    32768);

            shellLink.GetIconLocation(
                iconPathBuilder,
                iconPathBuilder.Capacity,
                out int iconIndex);

            string iconPath =
                Environment.ExpandEnvironmentVariables(
                    iconPathBuilder.ToString());

            bool iconPathIsShortcut =
                iconPath.EndsWith(
                    ".lnk",
                    StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(
                    iconPath) &&
                !iconPathIsShortcut &&
                File.Exists(
                    iconPath))
            {
                BitmapSource? extractedIcon =
                    ExtractIcon(
                        iconPath,
                        iconIndex);

                if (extractedIcon is not null)
                {
                    DebugLog.Write(
                        "ShortcutOverlay",
                        $"Base icon from IconLocation; Shortcut={path}; IconPath={iconPath}; IconIndex={iconIndex}; Size={extractedIcon.PixelWidth}x{extractedIcon.PixelHeight}");

                    return extractedIcon;
                }
            }

            targetPath =
                Environment.ExpandEnvironmentVariables(
                    targetPath);

            if (!string.IsNullOrWhiteSpace(
                    targetPath))
            {
                BitmapSource? targetIcon =
                    GetShellIcon(
                        targetPath);

                DebugLog.Write(
                    "ShortcutOverlay",
                    $"Base icon from target; Shortcut={path}; Target={targetPath}; IconPath={iconPath}; IconPathIsShortcut={iconPathIsShortcut}; Size={(targetIcon is not null ? $"{targetIcon.PixelWidth}x{targetIcon.PixelHeight}" : "n/a")}");

                if (targetIcon is not null)
                {
                    return targetIcon;
                }
            }

            shellLink.GetIDList(
                out IntPtr targetPidl);

            if (targetPidl != IntPtr.Zero)
            {
                try
                {
                    BitmapSource? targetIcon =
                        GetShellIconFromPidl(
                            targetPidl);

                    DebugLog.Write(
                        "ShortcutOverlay",
                        $"Base icon from target PIDL; Shortcut={path}; Size={(targetIcon is not null ? $"{targetIcon.PixelWidth}x{targetIcon.PixelHeight}" : "n/a")}");

                    if (targetIcon is not null)
                    {
                        return targetIcon;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(
                        targetPidl);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static BitmapSource? ExtractIcon(
        string iconPath,
        int iconIndex)
    {
        nint largeIcon;
        nint smallIcon;

        uint extracted =
            ExtractIconEx(
                iconPath,
                iconIndex,
                out largeIcon,
                out smallIcon,
                1);

        if (extracted == 0 ||
            largeIcon == 0)
        {
            if (smallIcon != 0)
            {
                DestroyIcon(
                    smallIcon);
            }

            return null;
        }

        try
        {
            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    largeIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();

            return source;
        }
        finally
        {
            DestroyIcon(
                largeIcon);

            if (smallIcon != 0)
            {
                DestroyIcon(
                    smallIcon);
            }
        }
    }

    private static BitmapSource? GetLinkOverlayIcon()
    {
        string shell32Path =
            Path.Combine(
                Environment.SystemDirectory,
                "shell32.dll");

        for (int iconIndex = 29;
             iconIndex <= 30;
             iconIndex++)
        {
            BitmapSource? overlayIcon =
                ExtractIcon(
                    shell32Path,
                    iconIndex);

            if (overlayIcon is not null)
            {
                return overlayIcon;
            }
        }

        return null;
    }

    private static BitmapSource? GetShellIconFromPidl(
        IntPtr pidl)
    {
        ShFileInfo fileInfo = new();

        nint result = SHGetFileInfo(
            pidl,
            0,
            ref fileInfo,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon |
            ShgfiLargeIcon |
            ShgfiPidl);

        if (result == 0 ||
            fileInfo.hIcon == 0)
        {
            return null;
        }

        try
        {
            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    fileInfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(
                fileInfo.hIcon);
        }
    }

    private static BitmapSource? GetShellIcon(
        string path)
    {
        ShFileInfo fileInfo = new();

        nint result = SHGetFileInfo(
            path,
            0,
            ref fileInfo,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiLargeIcon);

        if (result == 0 ||
            fileInfo.hIcon == 0)
        {
            return null;
        }

        try
        {
            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    fileInfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(
                fileInfo.hIcon);
        }
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            Size size,
            uint flags,
            out nint phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Size
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll")]
    private static extern nint SHGetFileInfo(
        IntPtr pidl,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        nint pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
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

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(
        nint hObject);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface ShellLink
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pszFile,
            int cch,
            IntPtr pfd,
            uint fFlags);

        void GetIDList(
            out IntPtr ppidl);

        void SetIDList(
            IntPtr pidl);

        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pszName,
            int cch);

        void SetDescription(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszName);

        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pszDir,
            int cch);

        void SetWorkingDirectory(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszDir);

        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pszArgs,
            int cch);

        void SetArguments(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszArgs);

        void GetHotkey(
            out short pwHotkey);

        void SetHotkey(
            short wHotkey);

        void GetShowCmd(
            out int piShowCmd);

        void SetShowCmd(
            int iShowCmd);

        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pszIconPath,
            int cch,
            out int piIcon);

        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszIconPath,
            int iIcon);

        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszPathRel,
            uint dwReserved);

        void Resolve(
            IntPtr hwnd,
            uint fFlags);

        void SetPath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszFile);
    }
}
