using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlueDock.DesktopOverlayWidget;

internal static class DesktopOverlayShellIcon
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiDisplayName = 0x000000200;
    private const uint ShgfiAddOverlays = 0x000000020;
    private const uint ShgfiOverlayIndex = 0x000000040;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint ShgfiShellIconSize = 0x000000004;
    private const uint ShgsiIcon = 0x000000100;
    private const uint ShgsiSmallIcon = 0x000000001;
    private const uint SiidLink = 29;
    private const int ShilExtraLarge = 0x2;
    private const uint IldTransparent = 0x00000001;
    private const uint IldPreserveAlpha = 0x00001000;
    private const uint SiigbfBiggerSizeOk = 0x00000001;
    private const uint SiigbfThumbnailOnly = 0x00000008;
    private const uint SiigbfScaleUp = 0x00000100;

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(nint hbmImage, nint hbmMask, out int pi);

        [PreserveSig]
        int ReplaceIcon(int i, nint hicon, out int pi);

        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);

        [PreserveSig]
        int Replace(int i, nint hbmImage, nint hbmMask);

        [PreserveSig]
        int AddMasked(nint hbmImage, uint crMask, out int pi);

        [PreserveSig]
        int Draw(nint pimldp);

        [PreserveSig]
        int Remove(int i);

        [PreserveSig]
        int GetIcon(int i, uint flags, out nint picon);

        [PreserveSig]
        int GetImageInfo(int i, nint pImageInfo);

        [PreserveSig]
        int Copy(int iDst, nint punkSrc, int iSrc, uint uFlags);

        [PreserveSig]
        int Merge(
            int i1,
            nint punk2,
            int i2,
            int dx,
            int dy,
            ref Guid riid,
            out nint ppv);

        [PreserveSig]
        int Clone(ref Guid riid, out nint ppv);

        [PreserveSig]
        int GetImageRect(int i, nint prc);

        [PreserveSig]
        int GetIconSize(out int cx, out int cy);

        [PreserveSig]
        int SetIconSize(int cx, int cy);

        [PreserveSig]
        int GetImageCount(out int pi);

        [PreserveSig]
        int SetImageCount(uint uNewCount);

        [PreserveSig]
        int SetBkColor(uint clrBk, out uint pclr);

        [PreserveSig]
        int GetBkColor(out uint pclr);

        [PreserveSig]
        int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

        [PreserveSig]
        int EndDrag();

        [PreserveSig]
        int DragEnter(nint hwndLock, int x, int y);

        [PreserveSig]
        int DragLeave(nint hwndLock);

        [PreserveSig]
        int DragMove(int x, int y);

        [PreserveSig]
        int SetDragCursorImage(
            nint punk,
            int iDrag,
            int dxHotspot,
            int dyHotspot);

        [PreserveSig]
        int DragShowNolock(int fShow);

        [PreserveSig]
        int GetDragImage(
            nint ppt,
            nint pptHotspot,
            ref Guid riid,
            out nint ppv);

        [PreserveSig]
        int GetItemFlags(int i, out uint dwFlags);

        [PreserveSig]
        int GetOverlayImage(int iOverlay, out int piIndex);
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShStockIconInfo
    {
        public uint cbSize;
        public nint hIcon;
        public int iSysImageIndex;
        public int iIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true)]
    private static extern int SHGetStockIconInfo(
        uint siid,
        uint uFlags,
        ref ShStockIconInfo psii);

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport(
        "shell32.dll",
        PreserveSig = true)]
    private static extern int SHGetImageList(
        int iImageList,
        ref Guid riid,
        out nint ppvObj);

    [DllImport(
        "comctl32.dll",
        SetLastError = true)]
    private static extern nint ImageList_GetIcon(
        nint himl,
        int i,
        uint flags);

    [DllImport(
        "comctl32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImageList_GetIconSize(
        nint himl,
        out int cx,
        out int cy);

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        nint pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    [DllImport(
        "gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(
        nint hObject);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(
        nint hIcon);


    public static string? GetDisplayName(
        string path)
    {
        try
        {
            nint result =
                SHGetFileInfo(
                    path,
                    0,
                    out ShFileInfo info,
                    (uint)Marshal.SizeOf<ShFileInfo>(),
                    ShgfiDisplayName);

            if (result == 0 ||
                string.IsNullOrWhiteSpace(
                    info.szDisplayName))
            {
                return null;
            }

            return info.szDisplayName;
        }
        catch
        {
            return null;
        }
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

    private static BitmapSource? GetShellImageListIcon(
        string path,
        bool includeOverlays,
        Action<string>? log)
    {
        try
        {
            nint imageList =
                SHGetFileInfo(
                    path,
                    0,
                    out ShFileInfo info,
                    (uint)Marshal.SizeOf<ShFileInfo>(),
                    ShgfiIcon |
                    ShgfiLargeIcon |
                    ShgfiShellIconSize |
                    ShgfiSysIconIndex |
                    (includeOverlays
                        ? ShgfiOverlayIndex
                        : 0));

            if (imageList == 0)
            {
                log?.Invoke(
                    $"Desktop shell icon lookup failed; Item={Path.GetFileName(path)}");

                return null;
            }

            if (info.hIcon != 0)
            {
                DestroyIcon(
                    info.hIcon);
            }

            int imageIndex =
                info.iIcon &
                0x00FFFFFF;

            int overlayIndex =
                (info.iIcon >>
                 24) &
                0xFF;

            uint drawFlags =
                IldTransparent |
                IldPreserveAlpha;

            if (includeOverlays &&
                overlayIndex > 0)
            {
                drawFlags |=
                    (uint)(
                        (overlayIndex &
                         0x0F) <<
                        8);
            }

            nint iconHandle =
                ImageList_GetIcon(
                    imageList,
                    imageIndex,
                    drawFlags);

            if (iconHandle == 0)
            {
                log?.Invoke(
                    $"Desktop shell image-list icon unavailable; Item={Path.GetFileName(path)}; BaseIndex={imageIndex}; OverlaySlot={overlayIndex}");

                return null;
            }

            try
            {
                BitmapSource source =
                    Imaging.CreateBitmapSourceFromHIcon(
                        iconHandle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                source.Freeze();

                string imageListSize =
                    ImageList_GetIconSize(
                        imageList,
                        out int listWidth,
                        out int listHeight)
                        ? $"{listWidth}x{listHeight}"
                        : "n/a";

                log?.Invoke(
                    $"Desktop shell icon composed by shell-sized system image list; Item={Path.GetFileName(path)}; BaseIndex={imageIndex}; OverlaySlot={overlayIndex}; ImageListSize={imageListSize}; ResultPixels={source.PixelWidth}x{source.PixelHeight}");

                return source;
            }
            finally
            {
                DestroyIcon(
                    iconHandle);
            }
        }
        catch (Exception exception)
        {
            log?.Invoke(
                $"Desktop shell icon composition failed; Item={Path.GetFileName(path)}; Error={exception.GetType().Name}: {exception.Message}");

            return null;
        }
    }

    private static BitmapSource? GetImageListBitmap(
        nint imageList,
        int imageIndex)
    {
        nint iconHandle =
            ImageList_GetIcon(
                imageList,
                imageIndex,
                IldTransparent |
                IldPreserveAlpha);

        if (iconHandle == 0)
        {
            return null;
        }

        try
        {
            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    iconHandle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();

            return source;
        }
        finally
        {
            DestroyIcon(
                iconHandle);
        }
    }

    private static BitmapSource ComposeIconAndOverlay(
        BitmapSource baseIcon,
        BitmapSource overlayIcon)
    {
        int pixelWidth =
            Math.Max(
                baseIcon.PixelWidth,
                overlayIcon.PixelWidth);

        int pixelHeight =
            Math.Max(
                baseIcon.PixelHeight,
                overlayIcon.PixelHeight);

        double dpiX =
            baseIcon.DpiX > 0
                ? baseIcon.DpiX
                : 96.0;

        double dpiY =
            baseIcon.DpiY > 0
                ? baseIcon.DpiY
                : 96.0;

        double width =
            pixelWidth *
            96.0 /
            dpiX;

        double height =
            pixelHeight *
            96.0 /
            dpiY;

        DrawingVisual visual =
            new();

        using (DrawingContext drawingContext =
               visual.RenderOpen())
        {
            Rect destination =
                new(
                    0,
                    0,
                    width,
                    height);

            drawingContext.DrawImage(
                baseIcon,
                destination);

            drawingContext.DrawImage(
                overlayIcon,
                destination);
        }

        RenderTargetBitmap result =
            new(
                pixelWidth,
                pixelHeight,
                dpiX,
                dpiY,
                PixelFormats.Pbgra32);

        result.Render(
            visual);

        result.Freeze();

        return result;
    }

    public static BitmapSource? GetLinkOverlayIcon()
    {
        try
        {
            ShStockIconInfo info =
                new()
                {
                    cbSize =
                        (uint)Marshal.SizeOf<ShStockIconInfo>()
                };

            int result =
                SHGetStockIconInfo(
                    SiidLink,
                    ShgsiIcon |
                    ShgsiSmallIcon,
                    ref info);

            if (result < 0 ||
                info.hIcon == 0)
            {
                return null;
            }

            try
            {
                BitmapSource source =
                    Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                source.Freeze();

                return source;
            }
            finally
            {
                DestroyIcon(
                    info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? GetIcon(
        string path,
        bool useFilePreview = false,
        Action<string>? log = null,
        bool includeShortcutOverlay = true)
    {
        if (useFilePreview &&
            (File.Exists(
                 path) ||
             Directory.Exists(
                 path)) &&
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

        return GetShellImageListIcon(
            path,
            includeOverlays:
                includeShortcutOverlay,
            log);
    }
}
