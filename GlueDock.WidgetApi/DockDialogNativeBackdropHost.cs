using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GlueDock;

// GlueDock UI rule: Settings/dialog windows that expose blur overrides must use this shared backdrop host.
// Do not add widget-specific native backdrop implementations for settings/dialog windows.
public sealed class DockDialogNativeBackdropHost : IDisposable
{
    private static void Log(
        string category,
        string message)
    {
    }

    private const int ErrorClassAlreadyExists = 1410;
    private const int WmWindowPosChanged = 0x0047;

    private const uint WsPopup = 0x80000000;

    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoRedirectionBitmap = 0x00200000;
    private const uint WsExNoActivate = 0x08000000;

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private const int SwHide = 0;

    private const int ENotImpl =
        unchecked((int)0x80004001);

    private const string WindowClassName =
        "GlueDock.NativeBackdropHost";

    private const string NativeBlurLibraryX64 =
        "GlueDock.NativeBlur.x64.dll";

    private const string NativeBlurLibraryX86 =
        "GlueDock.NativeBlur.x86.dll";

    private static readonly object ClassLock =
        new();

    private static readonly WindowProcDelegate WindowProc =
        WindowProcCore;

    private static bool _windowClassRegistered;
    private static object? _dispatcherQueueController;

    private Window? _owner;
    private DockSettingsSectionControl? _sectionOwner;

    private nint _ownerHandle;
    private HwndSource? _ownerHwndSource;
    private nint _backdropHandle;
    private nint _nativeBlurHost;

    private double _blurPercent;
    private bool _disposed;

    public DockDialogNativeBackdropHost(
        Window owner)
    {
        AttachOwner(
            owner);
    }

    public DockDialogNativeBackdropHost(
        DockSettingsSectionControl owner)
    {
        _sectionOwner =
            owner;

        _sectionOwner.StandaloneWindowCreated +=
            SectionOwner_StandaloneWindowCreated;

        _sectionOwner.Closed +=
            SectionOwner_Closed;

        if (_sectionOwner.StandaloneWindow is Window standaloneWindow)
        {
            AttachOwner(
                standaloneWindow);
        }
    }

    private void AttachOwner(
        Window owner)
    {
        if (ReferenceEquals(
                _owner,
                owner))
        {
            return;
        }

        DetachOwner();

        _owner =
            owner;

        _owner.SourceInitialized +=
            Owner_SourceInitialized;

        _owner.LocationChanged +=
            Owner_BoundsChanged;

        _owner.SizeChanged +=
            Owner_BoundsChanged;

        _owner.IsVisibleChanged +=
            Owner_IsVisibleChanged;

        _owner.Closed +=
            Owner_Closed;

        if (_blurPercent > 0)
        {
            SetBlur(
                _blurPercent);
        }
    }

    private void DetachOwner()
    {
        if (_owner is null)
        {
            return;
        }

        _owner.SourceInitialized -=
            Owner_SourceInitialized;

        _owner.LocationChanged -=
            Owner_BoundsChanged;

        _owner.SizeChanged -=
            Owner_BoundsChanged;

        _owner.IsVisibleChanged -=
            Owner_IsVisibleChanged;

        _owner.Closed -=
            Owner_Closed;

        if (_ownerHwndSource is not null)
        {
            _ownerHwndSource.RemoveHook(
                OwnerWndProc);

            _ownerHwndSource =
                null;
        }

        _owner =
            null;
        _ownerHandle =
            0;
    }

    private void SectionOwner_StandaloneWindowCreated(
        Window window)
    {
        AttachOwner(
            window);
    }

    private void SectionOwner_Closed(
        object? sender,
        EventArgs e)
    {
        Dispose();
    }

    public bool SetBlur(
        double blurPercent)
    {
        _blurPercent =
            Math.Clamp(
                blurPercent,
                0,
                100);

        if (_blurPercent <= 0)
        {
            HideBackdrop();
            return true;
        }

        if (_owner is null)
        {
            return true;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(
                10,
                0,
                17134))
        {
            HideBackdrop();
            return false;
        }

        EnsureOwnerHandle();

        if (_ownerHandle == 0 ||
            !EnsureBackdropWindow())
        {
            HideBackdrop();
            return false;
        }

        if (!EnsureNativeBlurHost())
        {
            DestroyBackdrop();
            return false;
        }

        if (!UpdateBlurAmount())
        {
            DestroyBackdrop();
            return false;
        }

        Log(
            "Widget.Clock.CalendarBackdrop",
            $"Native backdrop active; OwnerHandle=0x{_ownerHandle:X}; BackdropHandle=0x{_backdropHandle:X}; NativeBlurHost=0x{_nativeBlurHost:X}; BlurPercent={_blurPercent:0.##}; NativeBlurAmount={GetBlurAmount():0.###}; OwnerVisible={_owner.IsVisible}");

        Sync();

        return true;
    }

    public void Sync()
    {
        if (_disposed ||
            _owner is null ||
            _blurPercent <= 0 ||
            !_owner.IsVisible)
        {
            HideBackdrop();
            return;
        }

        EnsureOwnerHandle();

        if (_ownerHandle == 0 ||
            !EnsureBackdropWindow())
        {
            return;
        }

        if (!GetWindowRect(
                _ownerHandle,
                out NativeRect rect))
        {
            return;
        }

        int width =
            Math.Max(
                1,
                rect.Right -
                rect.Left);

        int height =
            Math.Max(
                1,
                rect.Bottom -
                rect.Top);

        SetWindowPos(
            _backdropHandle,
            _ownerHandle,
            rect.Left,
            rect.Top,
            width,
            height,
            SwpNoActivate |
            SwpShowWindow);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_sectionOwner is not null)
        {
            _sectionOwner.StandaloneWindowCreated -=
                SectionOwner_StandaloneWindowCreated;

            _sectionOwner.Closed -=
                SectionOwner_Closed;

            _sectionOwner =
                null;
        }

        DetachOwner();
        DestroyBackdrop();
    }

    private void Owner_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        EnsureOwnerHandle();

        if (_blurPercent > 0)
        {
            SetBlur(
                _blurPercent);
        }
    }

    private void Owner_BoundsChanged(
        object? sender,
        EventArgs e)
    {
        Sync();
    }

    private void Owner_BoundsChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        Sync();
    }

    private void Owner_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        Sync();
    }

    private void Owner_Closed(
        object? sender,
        EventArgs e)
    {
        Dispose();
    }

    private void EnsureOwnerHandle()
    {
        if (_owner is null)
        {
            return;
        }

        if (_ownerHandle == 0)
        {
            _ownerHandle =
                new WindowInteropHelper(
                    _owner).Handle;
        }

        if (_ownerHandle == 0 ||
            _ownerHwndSource is not null)
        {
            return;
        }

        _ownerHwndSource =
            HwndSource.FromHwnd(
                _ownerHandle);

        _ownerHwndSource?.AddHook(
            OwnerWndProc);
    }

    private nint OwnerWndProc(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmWindowPosChanged &&
            !_disposed &&
            _blurPercent > 0)
        {
            Sync();
        }

        return 0;
    }

    private bool EnsureBackdropWindow()
    {
        if (_backdropHandle != 0)
        {
            return true;
        }

        if (!EnsureWindowClass())
        {
            return false;
        }

        nint moduleHandle =
            GetModuleHandle(
                null);

        _backdropHandle =
            CreateWindowEx(
                WsExTransparent |
                WsExToolWindow |
                WsExNoRedirectionBitmap |
                WsExNoActivate,
                WindowClassName,
                string.Empty,
                WsPopup,
                0,
                0,
                1,
                1,
                0,
                0,
                moduleHandle,
                0);

        return
            _backdropHandle != 0;
    }

    private bool EnsureNativeBlurHost()
    {
        if (_nativeBlurHost != 0)
        {
            return true;
        }

        if (_backdropHandle == 0 ||
            !EnsureDispatcherQueue())
        {
            return false;
        }

        try
        {
            _nativeBlurHost =
                NativeCreate(
                    _backdropHandle,
                    GetBlurAmount(),
                    out int hresult);

            if (_nativeBlurHost != 0)
            {
                return true;
            }

            Log(
                "Widget.Clock.CalendarBackdrop",
                $"Native blur host creation failed; HRESULT=0x{hresult:X8}");

            return false;
        }
        catch (Exception exception)
        {
            Log(
                "Widget.Clock.CalendarBackdrop",
                $"{exception.GetType().Name}: {exception.Message}");

            _nativeBlurHost = 0;
            return false;
        }
    }

    private bool UpdateBlurAmount()
    {
        if (_nativeBlurHost == 0)
        {
            return false;
        }

        try
        {
            int hresult =
                NativeSetBlurAmount(
                    _nativeBlurHost,
                    GetBlurAmount());

            if (hresult >= 0)
            {
                return true;
            }

            Log(
                "Widget.Clock.CalendarBackdrop",
                $"Native blur amount update failed; HRESULT=0x{hresult:X8}");

            return false;
        }
        catch (Exception exception)
        {
            Log(
                "Widget.Clock.CalendarBackdrop",
                $"{exception.GetType().Name}: {exception.Message}");

            return false;
        }
    }

    private float GetBlurAmount()
    {
        double blurPercent =
            Math.Clamp(
                _blurPercent,
                0,
                100);

        if (blurPercent <= 50)
        {
            double lowerRange =
                blurPercent /
                50.0;

            return
                (float)(
                    lowerRange *
                    lowerRange *
                    4.0);
        }

        if (blurPercent <= 75)
        {
            double middleRange =
                (blurPercent - 50.0) /
                25.0;

            return
                (float)(
                    4.0 +
                    middleRange *
                    middleRange *
                    8.0);
        }

        double upperRange =
            (blurPercent - 75.0) /
            25.0;

        return
            (float)(
                12.0 +
                upperRange *
                upperRange *
                28.0);
    }

    private void DisposeNativeBlurHost()
    {
        if (_nativeBlurHost == 0)
        {
            return;
        }

        try
        {
            NativeDestroy(
                _nativeBlurHost);
        }
        catch (Exception exception)
        {
            Log(
                "Widget.Clock.CalendarBackdrop",
                $"{exception.GetType().Name}: {exception.Message}");
        }

        _nativeBlurHost = 0;
    }

    private void HideBackdrop()
    {
        if (_backdropHandle == 0)
        {
            return;
        }

        ShowWindow(
            _backdropHandle,
            SwHide);
    }

    private void DestroyBackdrop()
    {
        DisposeNativeBlurHost();

        if (_backdropHandle == 0)
        {
            return;
        }

        ShowWindow(
            _backdropHandle,
            SwHide);

        DestroyWindow(
            _backdropHandle);

        _backdropHandle = 0;
    }

    private static bool EnsureDispatcherQueue()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread()
            is not null)
        {
            return true;
        }

        DispatcherQueueOptions options =
            new()
            {
                Size =
                    Marshal.SizeOf<DispatcherQueueOptions>(),
                ThreadType =
                    DispatcherQueueThreadType.Current,
                ApartmentType =
                    DispatcherQueueThreadApartmentType.Sta
            };

        int result =
            CreateDispatcherQueueController(
                options,
                out object controller);

        if (result < 0)
        {
            Log(
                "Widget.Clock.CalendarBackdrop",
                $"DispatcherQueue creation failed; HRESULT=0x{result:X8}");

            return false;
        }

        _dispatcherQueueController =
            controller;

        return true;
    }

    private static bool EnsureWindowClass()
    {
        lock (ClassLock)
        {
            if (_windowClassRegistered)
            {
                return true;
            }

            nint moduleHandle =
                GetModuleHandle(
                    null);

            WindowClass windowClass =
                new()
                {
                    Style = 0,
                    WindowProc =
                        Marshal.GetFunctionPointerForDelegate(
                            WindowProc),
                    ClassExtraBytes = 0,
                    WindowExtraBytes = 0,
                    Instance = moduleHandle,
                    Icon = 0,
                    Cursor = 0,
                    BackgroundBrush = 0,
                    MenuName = null,
                    ClassName = WindowClassName
                };

            ushort atom =
                RegisterClass(
                    ref windowClass);

            if (atom == 0)
            {
                int error =
                    Marshal.GetLastWin32Error();

                if (error != ErrorClassAlreadyExists)
                {
                    return false;
                }
            }

            _windowClassRegistered = true;
            return true;
        }
    }

    private static nint NativeCreate(
        nint hwnd,
        float blurAmount,
        out int hresult)
    {
        if (Environment.Is64BitProcess)
        {
            return
                NativeCreateX64(
                    hwnd,
                    blurAmount,
                    out hresult);
        }

        if (RuntimeInformation.ProcessArchitecture ==
            Architecture.X86)
        {
            return
                NativeCreateX86(
                    hwnd,
                    blurAmount,
                    out hresult);
        }

        hresult =
            ENotImpl;

        return
            0;
    }

    private static int NativeSetBlurAmount(
        nint host,
        float blurAmount)
    {
        if (Environment.Is64BitProcess)
        {
            return
                NativeSetBlurAmountX64(
                    host,
                    blurAmount);
        }

        if (RuntimeInformation.ProcessArchitecture ==
            Architecture.X86)
        {
            return
                NativeSetBlurAmountX86(
                    host,
                    blurAmount);
        }

        return
            ENotImpl;
    }

    private static void NativeDestroy(
        nint host)
    {
        if (Environment.Is64BitProcess)
        {
            NativeDestroyX64(
                host);

            return;
        }

        if (RuntimeInformation.ProcessArchitecture ==
            Architecture.X86)
        {
            NativeDestroyX86(
                host);
        }
    }

    private static nint WindowProcCore(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam)
    {
        return
            DefWindowProc(
                hwnd,
                message,
                wParam,
                lParam);
    }

    private enum DispatcherQueueThreadType
    {
        Dedicated = 1,
        Current = 2
    }

    private enum DispatcherQueueThreadApartmentType
    {
        None = 0,
        Asta = 1,
        Sta = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int Size;

        [MarshalAs(UnmanagedType.I4)]
        public DispatcherQueueThreadType ThreadType;

        [MarshalAs(UnmanagedType.I4)]
        public DispatcherQueueThreadApartmentType ApartmentType;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public nint WindowProc;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint BackgroundBrush;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? MenuName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate nint WindowProcDelegate(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam);

    [DllImport(
        "coremessaging.dll",
        EntryPoint = "CreateDispatcherQueueController")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        [MarshalAs(UnmanagedType.IUnknown)]
        out object dispatcherQueueController);

    [DllImport(
        NativeBlurLibraryX64,
        EntryPoint = "GlueDockBlur_Create",
        CallingConvention = CallingConvention.StdCall)]
    private static extern nint NativeCreateX64(
        nint hwnd,
        float blurAmount,
        out int hresult);

    [DllImport(
        NativeBlurLibraryX64,
        EntryPoint = "GlueDockBlur_SetAmount",
        CallingConvention = CallingConvention.StdCall)]
    private static extern int NativeSetBlurAmountX64(
        nint host,
        float blurAmount);

    [DllImport(
        NativeBlurLibraryX64,
        EntryPoint = "GlueDockBlur_Destroy",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void NativeDestroyX64(
        nint host);

    [DllImport(
        NativeBlurLibraryX86,
        EntryPoint = "GlueDockBlur_Create",
        CallingConvention = CallingConvention.StdCall)]
    private static extern nint NativeCreateX86(
        nint hwnd,
        float blurAmount,
        out int hresult);

    [DllImport(
        NativeBlurLibraryX86,
        EntryPoint = "GlueDockBlur_SetAmount",
        CallingConvention = CallingConvention.StdCall)]
    private static extern int NativeSetBlurAmountX86(
        nint host,
        float blurAmount);

    [DllImport(
        NativeBlurLibraryX86,
        EntryPoint = "GlueDockBlur_Destroy",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void NativeDestroyX86(
        nint host);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(
        string? moduleName);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern ushort RegisterClass(
        ref WindowClass windowClass);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(
        nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        nint hwnd,
        out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hwnd,
        nint hwndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        nint hwnd,
        int command);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam);
}
