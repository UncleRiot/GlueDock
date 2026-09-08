using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GlueDock.DesktopOverlayWidget;

internal static class DesktopOverlayShellContextMenu
{
    private const uint CmfNormal = 0x00000000;
    private const uint CmfCanRename = 0x00000010;
    private const uint CmfItemMenu = 0x00000080;
    private const uint CmfExtendedVerbs = 0x00000100;
    private const uint GcsVerbW = 0x00000004;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const int SwShowNormal = 1;
    private const uint FirstCommandId = 1;
    private const uint LastCommandId = 0x7FFF;

    private static readonly Guid BhidSfUiObject =
        new(
            "3981E225-F559-11D3-8E3A-00C04F6837D5");

    private static readonly Guid IidIContextMenu =
        new(
            "000214E4-0000-0000-C000-000000000046");

    private static readonly Guid IidIShellFolder =
        new(
            "000214E6-0000-0000-C000-000000000046");

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct CmInvokeCommandInfo
    {
        public uint Size;
        public uint Mask;
        public nint Window;
        public nint Verb;
        public nint Parameters;
        public nint Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
    }

    [ComImport]
    [Guid(
        "B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        [PreserveSig]
        int BindToHandler(
            nint bindContext,
            ref Guid handlerId,
            ref Guid interfaceId,
            out nint result);
    }

    [ComImport]
    [Guid(
        "000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(
            nint hwnd,
            nint bindContext,
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string displayName,
            ref uint charactersEaten,
            out nint itemIdList,
            ref uint attributes);

        [PreserveSig]
        int EnumObjects(
            nint hwnd,
            uint flags,
            out nint enumItemIdList);

        [PreserveSig]
        int BindToObject(
            nint itemIdList,
            nint bindContext,
            ref Guid interfaceId,
            out nint result);

        [PreserveSig]
        int BindToStorage(
            nint itemIdList,
            nint bindContext,
            ref Guid interfaceId,
            out nint result);

        [PreserveSig]
        int CompareIDs(
            nint parameter,
            nint firstItemIdList,
            nint secondItemIdList);

        [PreserveSig]
        int CreateViewObject(
            nint owner,
            ref Guid interfaceId,
            out nint result);
    }

    [ComImport]
    [Guid(
        "000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(
            nint menu,
            uint indexMenu,
            uint firstCommandId,
            uint lastCommandId,
            uint flags);

        [PreserveSig]
        int InvokeCommand(
            ref CmInvokeCommandInfo commandInfo);

        [PreserveSig]
        int GetCommandString(
            nuint commandOffset,
            uint type,
            nint reserved,
            nint name,
            uint maxCharacters);
    }

    [ComImport]
    [Guid(
        "000214F4-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(
            nint menu,
            uint indexMenu,
            uint firstCommandId,
            uint lastCommandId,
            uint flags);

        [PreserveSig]
        int InvokeCommand(
            ref CmInvokeCommandInfo commandInfo);

        [PreserveSig]
        int GetCommandString(
            nuint commandOffset,
            uint type,
            nint reserved,
            nint name,
            uint maxCharacters);

        [PreserveSig]
        int HandleMenuMsg(
            uint message,
            nint wParam,
            nint lParam);
    }

    [ComImport]
    [Guid(
        "BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(
            nint menu,
            uint indexMenu,
            uint firstCommandId,
            uint lastCommandId,
            uint flags);

        [PreserveSig]
        int InvokeCommand(
            ref CmInvokeCommandInfo commandInfo);

        [PreserveSig]
        int GetCommandString(
            nuint commandOffset,
            uint type,
            nint reserved,
            nint name,
            uint maxCharacters);

        [PreserveSig]
        int HandleMenuMsg(
            uint message,
            nint wParam,
            nint lParam);

        [PreserveSig]
        int HandleMenuMsg2(
            uint message,
            nint wParam,
            nint lParam,
            out nint result);
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string name,
        nint bindContext,
        out nint itemIdList,
        uint attributesIn,
        out uint attributesOut);

    [DllImport(
        "shell32.dll")]
    private static extern int SHBindToObject(
        nint shellFolder,
        nint itemIdList,
        nint bindContext,
        ref Guid interfaceId,
        out nint result);

    [DllImport(
        "shell32.dll")]
    private static extern int SHCreateShellItemArrayFromIDLists(
        uint itemCount,
        [In] nint[] itemIdLists,
        [MarshalAs(
            UnmanagedType.Interface)]
        out IShellItemArray shellItemArray);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(
        UnmanagedType.Bool)]
    private static extern bool DestroyMenu(
        nint menu);

    [DllImport(
        "user32.dll")]
    private static extern uint TrackPopupMenuEx(
        nint menu,
        uint flags,
        int x,
        int y,
        nint owner,
        nint parameters);

    [DllImport(
        "user32.dll")]
    [return: MarshalAs(
        UnmanagedType.Bool)]
    private static extern bool GetCursorPos(
        out NativePoint point);

    public static bool TryShow(
        Window owner,
        IReadOnlyList<string> paths,
        Func<string, bool> tryHandleCanonicalVerb,
        Action<string> log)
    {
        string[] existingPaths =
            paths
                .Where(
                    path =>
                        System.IO.File.Exists(
                            path) ||
                        System.IO.Directory.Exists(
                            path))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (existingPaths.Length == 0)
        {
            return false;
        }

        nint ownerHandle =
            new WindowInteropHelper(
                owner).Handle;

        if (ownerHandle == 0)
        {
            return false;
        }

        List<nint> itemIdLists =
            new(
                existingPaths.Length);

        IShellItemArray? shellItemArray =
            null;

        object? contextMenuObject =
            null;

        nint menu =
            0;

        HwndSource? source =
            HwndSource.FromHwnd(
                ownerHandle);

        HwndSourceHook? hook =
            null;

        try
        {
            foreach (string path in existingPaths)
            {
                int parseResult =
                    SHParseDisplayName(
                        path,
                        0,
                        out nint itemIdList,
                        0,
                        out _);

                if (parseResult < 0 ||
                    itemIdList == 0)
                {
                    log(
                        $"Shell context menu parse failed; Item={System.IO.Path.GetFileName(path)}; HRESULT=0x{parseResult:X8}");

                    return false;
                }

                itemIdLists.Add(
                    itemIdList);
            }

            int arrayResult =
                SHCreateShellItemArrayFromIDLists(
                    (uint)itemIdLists.Count,
                    itemIdLists.ToArray(),
                    out shellItemArray);

            if (arrayResult < 0)
            {
                log(
                    $"Shell context menu item array failed; HRESULT=0x{arrayResult:X8}");

                return false;
            }

            Guid handlerId =
                BhidSfUiObject;

            Guid interfaceId =
                IidIContextMenu;

            int bindResult =
                shellItemArray.BindToHandler(
                    0,
                    ref handlerId,
                    ref interfaceId,
                    out nint contextMenuPointer);

            if (bindResult < 0 ||
                contextMenuPointer == 0)
            {
                log(
                    $"Shell context menu bind failed; HRESULT=0x{bindResult:X8}");

                return false;
            }

            try
            {
                contextMenuObject =
                    Marshal.GetObjectForIUnknown(
                        contextMenuPointer);
            }
            finally
            {
                Marshal.Release(
                    contextMenuPointer);
            }

            if (contextMenuObject is not IContextMenu contextMenu)
            {
                return false;
            }

            menu =
                CreatePopupMenu();

            if (menu == 0)
            {
                return false;
            }

            uint queryFlags =
                CmfNormal |
                CmfCanRename |
                CmfItemMenu;

            if ((Keyboard.Modifiers &
                 ModifierKeys.Shift) !=
                0)
            {
                queryFlags |=
                    CmfExtendedVerbs;
            }

            int queryResult =
                contextMenu.QueryContextMenu(
                    menu,
                    0,
                    FirstCommandId,
                    LastCommandId,
                    queryFlags);

            if (queryResult < 0)
            {
                log(
                    $"Shell context menu query failed; HRESULT=0x{queryResult:X8}");

                return false;
            }

            IContextMenu3? contextMenu3 =
                contextMenuObject as IContextMenu3;

            IContextMenu2? contextMenu2 =
                contextMenuObject as IContextMenu2;

            if (source is not null &&
                (contextMenu3 is not null ||
                 contextMenu2 is not null))
            {
                hook =
                    (
                        nint hwnd,
                        int message,
                        nint wParam,
                        nint lParam,
                        ref bool handled) =>
                    {
                        if (contextMenu3 is not null)
                        {
                            int result =
                                contextMenu3.HandleMenuMsg2(
                                    (uint)message,
                                    wParam,
                                    lParam,
                                    out nint menuResult);

                            if (result >= 0)
                            {
                                handled =
                                    true;

                                return menuResult;
                            }
                        }
                        else if (contextMenu2 is not null)
                        {
                            int result =
                                contextMenu2.HandleMenuMsg(
                                    (uint)message,
                                    wParam,
                                    lParam);

                            if (result >= 0)
                            {
                                handled =
                                    true;

                                return 0;
                            }
                        }

                        return 0;
                    };

                source.AddHook(
                    hook);
            }

            if (!GetCursorPos(
                    out NativePoint point))
            {
                return false;
            }

            uint command =
                TrackPopupMenuEx(
                    menu,
                    TpmRightButton |
                    TpmReturnCmd,
                    point.X,
                    point.Y,
                    ownerHandle,
                    0);

            if (command == 0)
            {
                return true;
            }

            nuint commandOffset =
                command -
                FirstCommandId;

            string canonicalVerb =
                GetCanonicalVerb(
                    contextMenu,
                    commandOffset);

            if (!string.IsNullOrWhiteSpace(
                    canonicalVerb) &&
                tryHandleCanonicalVerb(
                    canonicalVerb))
            {
                log(
                    $"Shell context menu command handled by overlay; Verb={canonicalVerb}; Items={existingPaths.Length}");

                return true;
            }

            CmInvokeCommandInfo commandInfo =
                new()
                {
                    Size =
                        (uint)Marshal.SizeOf<CmInvokeCommandInfo>(),
                    Window =
                        ownerHandle,
                    Verb =
                        (nint)commandOffset,
                    Show =
                        SwShowNormal
                };

            int invokeResult =
                contextMenu.InvokeCommand(
                    ref commandInfo);

            log(
                $"Shell context menu command invoked; Verb={canonicalVerb}; Items={existingPaths.Length}; HRESULT=0x{invokeResult:X8}");

            return invokeResult >= 0;
        }
        catch (Exception exception)
        {
            log(
                $"Shell context menu failed; {exception.GetType().Name}: {exception.Message}");

            return false;
        }
        finally
        {
            if (source is not null &&
                hook is not null)
            {
                source.RemoveHook(
                    hook);
            }

            if (menu != 0)
            {
                DestroyMenu(
                    menu);
            }

            if (contextMenuObject is not null &&
                Marshal.IsComObject(
                    contextMenuObject))
            {
                Marshal.FinalReleaseComObject(
                    contextMenuObject);
            }

            if (shellItemArray is not null &&
                Marshal.IsComObject(
                    shellItemArray))
            {
                Marshal.FinalReleaseComObject(
                    shellItemArray);
            }

            foreach (nint itemIdList in itemIdLists)
            {
                Marshal.FreeCoTaskMem(
                    itemIdList);
            }
        }
    }

    public static bool TryShowFolderBackground(
        Window owner,
        string folderPath,
        Action<string> log)
    {
        if (!Directory.Exists(
                folderPath))
        {
            return false;
        }

        nint ownerHandle =
            new WindowInteropHelper(
                owner).Handle;

        if (ownerHandle == 0)
        {
            return false;
        }

        nint itemIdList =
            0;

        object? shellFolderObject =
            null;

        object? contextMenuObject =
            null;

        nint menu =
            0;

        HwndSource? source =
            HwndSource.FromHwnd(
                ownerHandle);

        HwndSourceHook? hook =
            null;

        try
        {
            int parseResult =
                SHParseDisplayName(
                    folderPath,
                    0,
                    out itemIdList,
                    0,
                    out _);

            if (parseResult < 0 ||
                itemIdList == 0)
            {
                log(
                    $"Shell folder background parse failed; Folder={folderPath}; HRESULT=0x{parseResult:X8}");

                return false;
            }

            Guid shellFolderInterfaceId =
                IidIShellFolder;

            int bindResult =
                SHBindToObject(
                    0,
                    itemIdList,
                    0,
                    ref shellFolderInterfaceId,
                    out nint shellFolderPointer);

            if (bindResult < 0 ||
                shellFolderPointer == 0)
            {
                log(
                    $"Shell folder background bind failed; Folder={folderPath}; HRESULT=0x{bindResult:X8}");

                return false;
            }

            try
            {
                shellFolderObject =
                    Marshal.GetObjectForIUnknown(
                        shellFolderPointer);
            }
            finally
            {
                Marshal.Release(
                    shellFolderPointer);
            }

            if (shellFolderObject is not IShellFolder shellFolder)
            {
                return false;
            }

            Guid contextMenuInterfaceId =
                IidIContextMenu;

            int createResult =
                shellFolder.CreateViewObject(
                    ownerHandle,
                    ref contextMenuInterfaceId,
                    out nint contextMenuPointer);

            if (createResult < 0 ||
                contextMenuPointer == 0)
            {
                log(
                    $"Shell folder background context menu creation failed; Folder={folderPath}; HRESULT=0x{createResult:X8}");

                return false;
            }

            try
            {
                contextMenuObject =
                    Marshal.GetObjectForIUnknown(
                        contextMenuPointer);
            }
            finally
            {
                Marshal.Release(
                    contextMenuPointer);
            }

            if (contextMenuObject is not IContextMenu contextMenu)
            {
                return false;
            }

            menu =
                CreatePopupMenu();

            if (menu == 0)
            {
                return false;
            }

            uint queryFlags =
                CmfNormal;

            if ((Keyboard.Modifiers &
                 ModifierKeys.Shift) !=
                0)
            {
                queryFlags |=
                    CmfExtendedVerbs;
            }

            int queryResult =
                contextMenu.QueryContextMenu(
                    menu,
                    0,
                    FirstCommandId,
                    LastCommandId,
                    queryFlags);

            if (queryResult < 0)
            {
                log(
                    $"Shell folder background context menu query failed; Folder={folderPath}; HRESULT=0x{queryResult:X8}");

                return false;
            }

            IContextMenu3? contextMenu3 =
                contextMenuObject as IContextMenu3;

            IContextMenu2? contextMenu2 =
                contextMenuObject as IContextMenu2;

            if (source is not null &&
                (contextMenu3 is not null ||
                 contextMenu2 is not null))
            {
                hook =
                    (
                        nint hwnd,
                        int message,
                        nint wParam,
                        nint lParam,
                        ref bool handled) =>
                    {
                        if (contextMenu3 is not null)
                        {
                            int result =
                                contextMenu3.HandleMenuMsg2(
                                    (uint)message,
                                    wParam,
                                    lParam,
                                    out nint menuResult);

                            if (result >= 0)
                            {
                                handled =
                                    true;

                                return menuResult;
                            }
                        }
                        else if (contextMenu2 is not null)
                        {
                            int result =
                                contextMenu2.HandleMenuMsg(
                                    (uint)message,
                                    wParam,
                                    lParam);

                            if (result >= 0)
                            {
                                handled =
                                    true;

                                return 0;
                            }
                        }

                        return 0;
                    };

                source.AddHook(
                    hook);
            }

            if (!GetCursorPos(
                    out NativePoint point))
            {
                return false;
            }

            uint command =
                TrackPopupMenuEx(
                    menu,
                    TpmRightButton |
                    TpmReturnCmd,
                    point.X,
                    point.Y,
                    ownerHandle,
                    0);

            if (command == 0)
            {
                return true;
            }

            nuint commandOffset =
                command -
                FirstCommandId;

            string canonicalVerb =
                GetCanonicalVerb(
                    contextMenu,
                    commandOffset);

            CmInvokeCommandInfo commandInfo =
                new()
                {
                    Size =
                        (uint)Marshal.SizeOf<CmInvokeCommandInfo>(),
                    Window =
                        ownerHandle,
                    Verb =
                        (nint)commandOffset,
                    Show =
                        SwShowNormal
                };

            int invokeResult =
                contextMenu.InvokeCommand(
                    ref commandInfo);

            log(
                $"Shell folder background command invoked; Verb={canonicalVerb}; Folder={folderPath}; HRESULT=0x{invokeResult:X8}");

            return invokeResult >= 0;
        }
        catch (Exception exception)
        {
            log(
                $"Shell folder background context menu failed; {exception.GetType().Name}: {exception.Message}");

            return false;
        }
        finally
        {
            if (source is not null &&
                hook is not null)
            {
                source.RemoveHook(
                    hook);
            }

            if (menu != 0)
            {
                DestroyMenu(
                    menu);
            }

            if (contextMenuObject is not null &&
                Marshal.IsComObject(
                    contextMenuObject))
            {
                Marshal.FinalReleaseComObject(
                    contextMenuObject);
            }

            if (shellFolderObject is not null &&
                Marshal.IsComObject(
                    shellFolderObject))
            {
                Marshal.FinalReleaseComObject(
                    shellFolderObject);
            }

            if (itemIdList != 0)
            {
                Marshal.FreeCoTaskMem(
                    itemIdList);
            }
        }
    }

    private static string GetCanonicalVerb(
        IContextMenu contextMenu,
        nuint commandOffset)
    {
        const uint characterCount =
            260;

        nint buffer =
            Marshal.AllocHGlobal(
                checked(
                    (int)characterCount *
                    sizeof(char)));

        try
        {
            int result =
                contextMenu.GetCommandString(
                    commandOffset,
                    GcsVerbW,
                    0,
                    buffer,
                    characterCount);

            if (result < 0)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(
                       buffer) ??
                   string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }
}
