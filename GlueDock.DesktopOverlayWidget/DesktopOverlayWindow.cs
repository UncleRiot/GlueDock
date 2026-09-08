﻿using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using GlueDock;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace GlueDock.DesktopOverlayWidget;

public sealed class DesktopOverlayWindow : Window, IDisposable
{
    private const string InternalDragFormat =
        "GlueDock.DesktopOverlay.InternalItems";

    private const string GroupMarkerFileName =
        ".gluedock-group.json";

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private const uint WmNcLButtonDown = 0x00A1;
    private const int HtBottomRight = 17;
    private const uint SpiGetIconMetrics = 0x002D;
    private const uint LvmFirst = 0x1000;
    private const uint LvmGetItemSpacing = LvmFirst + 51;
    private const uint FoDelete = 0x0003;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofWantNukeWarning = 0x4000;

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct NativeLogFont
    {
        public int Height;
        public int Width;
        public int Escapement;
        public int Orientation;
        public int Weight;
        public byte Italic;
        public byte Underline;
        public byte StrikeOut;
        public byte CharSet;
        public byte OutPrecision;
        public byte ClipPrecision;
        public byte Quality;
        public byte PitchAndFamily;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 32)]
        public string FaceName;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct NativeIconMetrics
    {
        public uint Size;
        public int HorizontalSpacing;
        public int VerticalSpacing;
        public int TitleWrap;
        public NativeLogFont Font;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativeShellFileOperation
    {
        public nint Window;
        public uint Function;
        public nint From;
        public nint To;
        public ushort Flags;

        [MarshalAs(
            UnmanagedType.Bool)]
        public bool AnyOperationsAborted;

        public nint NameMappings;
        public nint ProgressTitle;
    }

    [DllImport(
        "user32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(
        UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref NativeIconMetrics metrics,
        uint flags);

    [DllImport(
        "shell32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHFileOperation(
        ref NativeShellFileOperation operation);

    private delegate bool EnumWindowsProc(
        nint hWnd,
        nint lParam);

    [DllImport(
        "user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        nint lParam);

    [DllImport(
        "user32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint FindWindowEx(
        nint hWndParent,
        nint hWndChildAfter,
        string? lpszClass,
        string? lpszWindow);

    [DllImport(
        "user32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint SendMessage(
        nint hWnd,
        uint message,
        nint wParam,
        nint lParam);

    [DllImport(
        "user32.dll")]
    [return: MarshalAs(
        UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport(
        "user32.dll")]
    private static extern uint GetDpiForWindow(
        nint hWnd);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    private readonly DesktopOverlaySettings _settings;
    private readonly string _storageDirectory;
    private readonly Action<DesktopOverlaySettings> _saveSettings;
    private readonly Func<string, string> _localize;
    private readonly Action<string> _log;
    private readonly Action _openSettings;
    private readonly Action<EventHandler>? _unsubscribeLanguageChanged;
    private readonly DockDialogNativeBackdropHost _nativeBackdropHost;
    private readonly Border _windowBorder;
    private readonly Border _glassSurfaceBorder;
    private readonly Border _glassHighlightBorder;
    private readonly TextBlock _titleText;
    private readonly Canvas _canvas;
    private readonly ScrollViewer _scrollViewer;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _saveBoundsTimer;
    private readonly DispatcherTimer _resizeRefreshTimer;
    private readonly Thumb _resizeGrip;

    private GlueDockWidgetAppearance _hostAppearance;
    private readonly HashSet<string> _selectedPaths =
        new(
            StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Border> _itemBorders =
        new(
            StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TextBlock> _itemLabels =
        new(
            StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _orderedRootPaths =
        new();

    private Point _dragStart;
    private string? _dragCandidatePath;
    private FrameworkElement? _dragVisual;
    private double _dragVisualAnchorOffsetX;
    private double _dragVisualAnchorOffsetY;
    private Point _selectionStart;
    private bool _selectionInProgress;
    private Border? _selectionRectangle;
    private readonly HashSet<string> _selectionBasePaths =
        new(
            StringComparer.OrdinalIgnoreCase);
    private bool _dragInProgress;
    private bool _applyingBounds;
    private bool _interactiveSizeMove;
    private bool _interactiveResizeDetected;
    private readonly Dictionary<string, Point> _resizeStartItemCanvasPositions =
        new(
            StringComparer.OrdinalIgnoreCase);
    private Point _resizeStartScrollOffset;
    private string? _hoveredPath;
    private TextBox? _inlineRenameTextBox;
    private string? _inlineRenamePath;
    private TextBlock? _inlineRenameLabel;
    private bool _disposed;
    private Popup? _groupPopup;
    private HwndSource? _hwndSource;

    public DesktopOverlayWindow(
        DesktopOverlaySettings settings,
        string storageDirectory,
        GlueDockWidgetAppearance appearance,
        Action<DesktopOverlaySettings> saveSettings,
        Func<string, string> localize,
        Action<string> log,
        Action openSettings,
        Action<EventHandler>? subscribeLanguageChanged,
        Action<EventHandler>? unsubscribeLanguageChanged)
    {
        _settings =
            settings.Clone();

        _storageDirectory =
            storageDirectory;

        _hostAppearance =
            appearance;

        _saveSettings =
            saveSettings;

        _localize =
            localize;

        _log =
            log;

        _openSettings =
            openSettings;

        _unsubscribeLanguageChanged =
            unsubscribeLanguageChanged;

        Directory.CreateDirectory(
            _storageDirectory);

        Title =
            _localize(
                "Widget.DesktopOverlay.Window.Title");

        WindowStyle =
            WindowStyle.None;

        AllowsTransparency =
            true;

        Background =
            Brushes.Transparent;

        ResizeMode =
            ResizeMode.CanResize;

        WindowChrome.SetWindowChrome(
            this,
            new WindowChrome
            {
                CaptionHeight =
                    30,
                ResizeBorderThickness =
                    new Thickness(
                        6),
                CornerRadius =
                    new CornerRadius(
                        0),
                GlassFrameThickness =
                    new Thickness(
                        0),
                UseAeroCaptionButtons =
                    false
            });

        ShowInTaskbar =
            false;

        MinWidth =
            360;

        MinHeight =
            280;

        Width =
            double.IsFinite(
                _settings.WindowWidth)
                ? Math.Max(
                    MinWidth,
                    _settings.WindowWidth)
                : 720;

        Height =
            double.IsFinite(
                _settings.WindowHeight)
                ? Math.Max(
                    MinHeight,
                    _settings.WindowHeight)
                : 520;

        if (_settings.WindowLeft is double savedLeft &&
            _settings.WindowTop is double savedTop &&
            double.IsFinite(
                savedLeft) &&
            double.IsFinite(
                savedTop))
        {
            WindowStartupLocation =
                WindowStartupLocation.Manual;

            Left =
                savedLeft;

            Top =
                savedTop;
        }
        else
        {
            WindowStartupLocation =
                WindowStartupLocation.CenterScreen;
        }

        _windowBorder =
            new Border();

        _glassSurfaceBorder =
            new Border
            {
                IsHitTestVisible = false
            };

        _glassHighlightBorder =
            new Border
            {
                IsHitTestVisible = false
            };

        _titleText =
            new TextBlock
            {
                FontWeight =
                    FontWeights.SemiBold,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        4,
                        0,
                        0,
                        0)
            };

        Button closeButton =
            new()
            {
                Content =
                    "×",
                Width =
                    DockDialogTheme.CompactButtonWidth,
                Height =
                    DockDialogTheme.CompactButtonHeight,
                Padding =
                    new Thickness(
                        0),
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Center,
                ToolTip =
                    _localize(
                        "Settings.Close")
            };

        WindowChrome.SetIsHitTestVisibleInChrome(
            closeButton,
            true);

        closeButton.Click +=
            (_, _) =>
                Close();

        Grid titleBar =
            new()
            {
                Height =
                    30,
                Margin =
                    new Thickness(
                        10,
                        2,
                        8,
                        0),
                Background =
                    Brushes.Transparent
            };

        titleBar.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        titleBar.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        Grid.SetColumn(
            _titleText,
            0);

        Grid.SetColumn(
            closeButton,
            1);

        titleBar.Children.Add(
            _titleText);

        titleBar.Children.Add(
            closeButton);

        WindowChrome.SetIsHitTestVisibleInChrome(
            titleBar,
            true);

        titleBar.MouseLeftButtonDown +=
            TitleBar_MouseLeftButtonDown;

        titleBar.ContextMenu =
            CreateTitleBarContextMenu();

        _canvas =
            new Canvas
            {
                Background =
                    Brushes.Transparent,
                AllowDrop =
                    true,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Top
            };

        _canvas.PreviewMouseLeftButtonDown +=
            Canvas_PreviewMouseLeftButtonDown;

        _canvas.PreviewMouseMove +=
            Canvas_PreviewMouseMove;

        _canvas.PreviewMouseLeftButtonUp +=
            Canvas_PreviewMouseLeftButtonUp;

        _canvas.PreviewMouseRightButtonUp +=
            Canvas_PreviewMouseRightButtonUp;

        _canvas.Drop +=
            Canvas_Drop;

        _canvas.DragOver +=
            Canvas_DragOver;

        _scrollViewer =
            new ScrollViewer
            {
                Content =
                    _canvas,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Hidden,
                HorizontalContentAlignment =
                    HorizontalAlignment.Left,
                VerticalContentAlignment =
                    VerticalAlignment.Top,
                CanContentScroll =
                    false,
                FocusVisualStyle =
                    null,
                Margin =
                    new Thickness(
                        8,
                        0,
                        8,
                        8),
                Background =
                    Brushes.Transparent
            };

        ControlTemplate resizeGripTemplate =
            new(
                typeof(Thumb));

        FrameworkElementFactory resizeGripBorder =
            new(
                typeof(Border));

        resizeGripBorder.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(
                Control.BackgroundProperty));

        resizeGripBorder.SetValue(
            Border.BorderBrushProperty,
            Brushes.Transparent);

        resizeGripBorder.SetValue(
            Border.BorderThicknessProperty,
            new Thickness(
                0));

        resizeGripTemplate.VisualTree =
            resizeGripBorder;

        _resizeGrip =
            new Thumb
            {
                Width =
                    44,
                Height =
                    44,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                Cursor =
                    Cursors.SizeNWSE,
                Background =
                    Brushes.Transparent,
                BorderBrush =
                    Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        0),
                Margin =
                    new Thickness(
                        0),
                Template =
                    resizeGripTemplate
            };

        WindowChrome.SetIsHitTestVisibleInChrome(
            _resizeGrip,
            true);

        _resizeGrip.PreviewMouseLeftButtonDown +=
            ResizeGrip_PreviewMouseLeftButtonDown;

        Grid content =
            new();

        content.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        content.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid.SetRow(
            titleBar,
            0);

        Grid.SetRow(
            _scrollViewer,
            1);

        content.Children.Add(
            titleBar);

        content.Children.Add(
            _scrollViewer);

        Grid chrome =
            new();

        chrome.Children.Add(
            _glassSurfaceBorder);

        chrome.Children.Add(
            _glassHighlightBorder);

        chrome.Children.Add(
            content);

        chrome.Children.Add(
            _resizeGrip);

        _windowBorder.Child =
            chrome;

        Content =
            _windowBorder;

        _nativeBackdropHost =
            new DockDialogNativeBackdropHost(
                this);

        _refreshTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        250)
            };

        _refreshTimer.Tick +=
            RefreshTimer_Tick;

        _saveBoundsTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        350)
            };

        _saveBoundsTimer.Tick +=
            SaveBoundsTimer_Tick;

        _resizeRefreshTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        140)
            };

        _resizeRefreshTimer.Tick +=
            ResizeRefreshTimer_Tick;

        _watcher =
            new FileSystemWatcher(
                _storageDirectory)
            {
                IncludeSubdirectories =
                    true,
                NotifyFilter =
                    NotifyFilters.FileName |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.LastWrite,
                EnableRaisingEvents =
                    true
            };

        _watcher.Created +=
            StorageChanged;

        _watcher.Deleted +=
            StorageChanged;

        _watcher.Renamed +=
            StorageChanged;

        _watcher.Changed +=
            StorageChanged;

        subscribeLanguageChanged?.Invoke(
            LanguageChanged);

        SourceInitialized +=
            DesktopOverlayWindow_SourceInitialized;

        Loaded +=
            DesktopOverlayWindow_Loaded;

        Closed +=
            DesktopOverlayWindow_Closed;

        LocationChanged +=
            BoundsChanged;

        SizeChanged +=
            BoundsChanged;

        Activated +=
            DesktopOverlayWindow_Activated;

        PreviewKeyDown +=
            DesktopOverlayWindow_PreviewKeyDown;

        ApplyAppearance();
        RefreshLanguage();
    }

    public void ApplySettings(
        DesktopOverlaySettings settings,
        GlueDockWidgetAppearance appearance)
    {
        bool refreshItems =
            RequiresItemRefresh(
                settings);

        bool materialChanged =
            !string.Equals(
                _settings.ThemeName,
                settings.ThemeName,
                StringComparison.OrdinalIgnoreCase) ||
            Math.Abs(
                _settings.Opacity -
                settings.Opacity) >
            0.0001 ||
            Math.Abs(
                _settings.BlurRadius -
                settings.BlurRadius) >
            0.0001;

        bool borderChanged =
            _settings.BorderEnabled !=
            settings.BorderEnabled ||
            !string.Equals(
                _settings.BorderColor,
                settings.BorderColor,
                StringComparison.OrdinalIgnoreCase);

        _settings.CopyFrom(
            settings);

        _hostAppearance =
            appearance;

        GlueDockWidgetAppearance effectiveAppearance =
            CreateEffectiveAppearance();

        if (materialChanged)
        {
            ApplyAppearance();
        }
        else if (borderChanged)
        {
            ApplyBorderAppearance(
                effectiveAppearance);

            LogAppearanceState(
                "BorderOnlyApply",
                effectiveAppearance);
        }

        ApplyZOrder();

        if (refreshItems)
        {
            _log(
                $"Settings apply requires item refresh; IconSize={_settings.IconSizeMode}; FilePreviews={_settings.ShowFilePreviews}; AutoArrange={_settings.AutoArrange}; Sort={_settings.SortMode}");

            RefreshItems();

            return;
        }

        if (materialChanged)
        {
            foreach (TextBlock label in _itemLabels.Values)
            {
                label.Foreground =
                    effectiveAppearance.DockTextBrush.Clone();
            }

            UpdateSelectionVisuals();
        }

        _log(
            $"Settings apply skipped item refresh; MaterialChanged={materialChanged}; BorderChanged={borderChanged}; Theme={_settings.ThemeName}; BorderEnabled={_settings.BorderEnabled?.ToString() ?? "null"}; BorderColor={_settings.BorderColor ?? "<null>"}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; ZOrder={_settings.ZOrderMode}; Positions={_settings.Positions.Count}");
    }

    private bool RequiresItemRefresh(
        DesktopOverlaySettings settings)
    {
        return
            !string.Equals(
                _settings.IconSizeMode,
                settings.IconSizeMode,
                StringComparison.OrdinalIgnoreCase) ||
            _settings.ShowFilePreviews !=
            settings.ShowFilePreviews ||
            _settings.AutoArrange !=
            settings.AutoArrange ||
            !string.Equals(
                _settings.SortMode,
                settings.SortMode,
                StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _hostAppearance =
            appearance;

        ApplyAppearance();
        RefreshItems();
    }

    private void ResizeGrip_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Left)
        {
            return;
        }

        nint handle =
            new WindowInteropHelper(
                this).Handle;

        if (handle == 0)
        {
            _log(
                "Resize grip native start skipped; Handle=0");

            return;
        }

        e.Handled =
            true;

        _log(
            $"Resize grip native start requested; Handle=0x{handle:X}; Direction=BottomRight; Window={ActualWidth:0.0}x{ActualHeight:0.0}");

        ReleaseCapture();

        SendMessage(
            handle,
            WmNcLButtonDown,
            HtBottomRight,
            0);
    }

    private void DesktopOverlayWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        nint handle =
            new WindowInteropHelper(
                this).Handle;

        _hwndSource =
            HwndSource.FromHwnd(
                handle);

        _hwndSource?.AddHook(
            WndProc);
    }

    private nint WndProc(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        switch (message)
        {
            case WmEnterSizeMove:
                _interactiveSizeMove =
                    true;

                _interactiveResizeDetected =
                    false;

                _resizeRefreshTimer.Stop();
                _saveBoundsTimer.Stop();

                CaptureResizeItemBaseline();

                LogResizeState(
                    "Interactive size/move started",
                    false);

                LogResizeItemPositions(
                    "Interactive size/move started");

                break;

            case WmExitSizeMove:
                bool wasInteractiveResize =
                    _interactiveResizeDetected;

                _interactiveSizeMove =
                    false;

                _interactiveResizeDetected =
                    false;

                _resizeRefreshTimer.Stop();
                _saveBoundsTimer.Stop();

                Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(
                        () =>
                        {
                            if (_disposed)
                            {
                                return;
                            }

                            if (wasInteractiveResize)
                            {
                                UpdateViewportForCurrentItems();
                            }

                            _windowBorder.InvalidateVisual();
                            _glassSurfaceBorder.InvalidateVisual();
                            _glassHighlightBorder.InvalidateVisual();
                            _canvas.InvalidateVisual();
                            InvalidateVisual();
                            UpdateLayout();

                            Dispatcher.BeginInvoke(
                                DispatcherPriority.ContextIdle,
                                new Action(
                                    () =>
                                    {
                                        if (_disposed)
                                        {
                                            return;
                                        }

                                        if (wasInteractiveResize)
                                        {
                                            _nativeBackdropHost.SetBlur(
                                                Math.Clamp(
                                                    _settings.BlurRadius,
                                                    0,
                                                    100));
                                        }

                                        _nativeBackdropHost.Sync();
                                        QueueBoundsSave();

                                        LogResizeState(
                                            "Interactive size/move finished",
                                            wasInteractiveResize);

                                        LogResizeItemPositions(
                                            "Interactive size/move finished");

                                        _resizeStartItemCanvasPositions.Clear();
                                    }));
                        }));

                break;
        }

        return 0;
    }

    private void DesktopOverlayWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        EnsureWindowOnScreen();
        RefreshItems();
        ApplyZOrder();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    _nativeBackdropHost.SetBlur(
                        Math.Clamp(
                            _settings.BlurRadius,
                            0,
                            100));

                    _nativeBackdropHost.Sync();

                    LogAppearanceState(
                        "Loaded",
                        CreateEffectiveAppearance());
                }));
    }

    private void EnsureWindowOnScreen()
    {
        Rect virtualScreen =
            new(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

        Rect current =
            new(
                Left,
                Top,
                ActualWidth > 0
                    ? ActualWidth
                    : Width,
                ActualHeight > 0
                    ? ActualHeight
                    : Height);

        if (virtualScreen.IntersectsWith(
                current))
        {
            return;
        }

        _applyingBounds =
            true;

        Left =
            SystemParameters.WorkArea.Left +
            32;

        Top =
            SystemParameters.WorkArea.Top +
            32;

        _applyingBounds =
            false;
    }

    private void BoundsChanged(
        object? sender,
        EventArgs e)
    {
        QueueBoundsSave();
    }

    private void BoundsChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        QueueBoundsSave();

        _log(
            $"SizeChanged; Interactive={_interactiveSizeMove}; ResizeDetected={_interactiveResizeDetected}; Previous={e.PreviousSize.Width:0.0}x{e.PreviousSize.Height:0.0}; New={e.NewSize.Width:0.0}x{e.NewSize.Height:0.0}; Actual={ActualWidth:0.0}x{ActualHeight:0.0}; MouseCaptured={Mouse.Captured?.GetType().Name ?? "<null>"}");

        if (_interactiveSizeMove)
        {
            LogResizeItemPositions(
                "SizeChanged");
        }

        if (_interactiveSizeMove)
        {
            if (!_interactiveResizeDetected)
            {
                _interactiveResizeDetected =
                    true;

                _log(
                    $"Interactive resize detected; NativeBackdropLiveSync=True; Window={ActualWidth:0.0}x{ActualHeight:0.0}; Previous={e.PreviousSize.Width:0.0}x{e.PreviousSize.Height:0.0}; New={e.NewSize.Width:0.0}x{e.NewSize.Height:0.0}");
            }

            return;
        }

        _resizeRefreshTimer.Stop();
        _resizeRefreshTimer.Start();
    }

    private void ResizeRefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _resizeRefreshTimer.Stop();

        if (_interactiveSizeMove)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(
                () =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    UpdateViewportForCurrentItems();
                    _nativeBackdropHost.Sync();

                    LogResizeState(
                        "Resize settled",
                        false);
                }));
    }

    private void UpdateViewportForCurrentItems()
    {
        if (_disposed)
        {
            return;
        }

        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        _scrollViewer.HorizontalScrollBarVisibility =
            ScrollBarVisibility.Hidden;

        _scrollViewer.VerticalScrollBarVisibility =
            ScrollBarVisibility.Hidden;

        _scrollViewer.UpdateLayout();

        double viewportWidth =
            Math.Max(
                cellWidth,
                _scrollViewer.ActualWidth > 1
                    ? _scrollViewer.ActualWidth
                    : Math.Max(
                        1,
                        ActualWidth -
                        20));

        double viewportHeight =
            Math.Max(
                cellHeight,
                _scrollViewer.ActualHeight > 1
                    ? _scrollViewer.ActualHeight
                    : Math.Max(
                        1,
                        ActualHeight -
                        60));

        double requiredWidth =
            cellWidth;

        double requiredHeight =
            cellHeight;

        foreach (Border item in _itemBorders.Values)
        {
            double left =
                Canvas.GetLeft(
                    item);

            double top =
                Canvas.GetTop(
                    item);

            if (!double.IsFinite(
                    left))
            {
                left =
                    0;
            }

            if (!double.IsFinite(
                    top))
            {
                top =
                    0;
            }

            requiredWidth =
                Math.Max(
                    requiredWidth,
                    left +
                    cellWidth);

            requiredHeight =
                Math.Max(
                    requiredHeight,
                    top +
                    cellHeight);
        }

        bool needsHorizontalScroll =
            requiredWidth >
            viewportWidth;

        bool needsVerticalScroll =
            requiredHeight >
            viewportHeight;

        _scrollViewer.HorizontalScrollBarVisibility =
            needsHorizontalScroll
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Hidden;

        _scrollViewer.VerticalScrollBarVisibility =
            needsVerticalScroll
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Hidden;

        _canvas.Width =
            needsHorizontalScroll
                ? requiredWidth
                : Math.Max(
                    cellWidth,
                    viewportWidth);

        _canvas.Height =
            needsVerticalScroll
                ? requiredHeight
                : Math.Max(
                    cellHeight,
                    viewportHeight);

        _scrollViewer.UpdateLayout();

        _log(
            $"Viewport recalculated without item rebuild; Items={_itemBorders.Count}; Grid={cellWidth:0.0}x{cellHeight:0.0}; Viewport={viewportWidth:0.0}x{viewportHeight:0.0}; Required={requiredWidth:0.0}x{requiredHeight:0.0}; HScroll={needsHorizontalScroll}; VScroll={needsVerticalScroll}; Canvas={_canvas.ActualWidth:0.0}x{_canvas.ActualHeight:0.0}");
    }

    private void CaptureResizeItemBaseline()
    {
        _resizeStartItemCanvasPositions.Clear();

        foreach (KeyValuePair<string, Border> pair in _itemBorders)
        {
            double left =
                Canvas.GetLeft(
                    pair.Value);

            double top =
                Canvas.GetTop(
                    pair.Value);

            _resizeStartItemCanvasPositions[pair.Key] =
                new Point(
                    double.IsNaN(
                        left)
                        ? 0
                        : left,
                    double.IsNaN(
                        top)
                        ? 0
                        : top);
        }

        _resizeStartScrollOffset =
            new Point(
                _scrollViewer.HorizontalOffset,
                _scrollViewer.VerticalOffset);
    }

    private void LogResizeItemPositions(
        string phase)
    {
        _log(
            $"ResizeItemSnapshot; Phase={phase}; Items={_itemBorders.Count}; Window={ActualWidth:0.0}x{ActualHeight:0.0}; Canvas={_canvas.ActualWidth:0.0}x{_canvas.ActualHeight:0.0}; Viewport={_scrollViewer.ViewportWidth:0.0}x{_scrollViewer.ViewportHeight:0.0}; Extent={_scrollViewer.ExtentWidth:0.0}x{_scrollViewer.ExtentHeight:0.0}; ScrollOffset={_scrollViewer.HorizontalOffset:0.0},{_scrollViewer.VerticalOffset:0.0}; ScrollDelta={_scrollViewer.HorizontalOffset - _resizeStartScrollOffset.X:0.0},{_scrollViewer.VerticalOffset - _resizeStartScrollOffset.Y:0.0}");

        foreach (KeyValuePair<string, Border> pair in _itemBorders.OrderBy(
                     item =>
                         item.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            double canvasLeft =
                Canvas.GetLeft(
                    pair.Value);

            double canvasTop =
                Canvas.GetTop(
                    pair.Value);

            if (double.IsNaN(
                    canvasLeft))
            {
                canvasLeft =
                    0;
            }

            if (double.IsNaN(
                    canvasTop))
            {
                canvasTop =
                    0;
            }

            Point baseline =
                _resizeStartItemCanvasPositions.TryGetValue(
                    pair.Key,
                    out Point baselinePosition)
                    ? baselinePosition
                    : new Point(
                        canvasLeft,
                        canvasTop);

            string storedGrid =
                _settings.Positions.TryGetValue(
                    pair.Key,
                    out DesktopOverlayGridPosition? storedPosition)
                    ? $"{storedPosition.Column},{storedPosition.Row}"
                    : "<none>";

            string canvasVisual =
                "<unavailable>";

            string windowVisual =
                "<unavailable>";

            try
            {
                Point positionInCanvas =
                    pair.Value.TranslatePoint(
                        new Point(
                            0,
                            0),
                        _canvas);

                Point positionInWindow =
                    pair.Value.TranslatePoint(
                        new Point(
                            0,
                            0),
                        this);

                canvasVisual =
                    $"{positionInCanvas.X:0.0},{positionInCanvas.Y:0.0}";

                windowVisual =
                    $"{positionInWindow.X:0.0},{positionInWindow.Y:0.0}";
            }
            catch
            {
            }

            _log(
                $"ResizeItem; Phase={phase}; Item={Path.GetFileName(pair.Key)}; CanvasLeftTop={canvasLeft:0.0},{canvasTop:0.0}; CanvasDelta={canvasLeft - baseline.X:0.0},{canvasTop - baseline.Y:0.0}; VisualInCanvas={canvasVisual}; VisualInWindow={windowVisual}; StoredGrid={storedGrid}; Actual={pair.Value.ActualWidth:0.0}x{pair.Value.ActualHeight:0.0}");
        }
    }

    private void LogResizeState(
        string phase,
        bool resize)
    {
        _log(
            $"{phase}; Resize={resize}; Window={ActualWidth:0.0}x{ActualHeight:0.0}; WindowVisible={IsVisible}; WindowActive={IsActive}; WindowHitTest={IsHitTestVisible}; WindowBorder={_windowBorder.ActualWidth:0.0}x{_windowBorder.ActualHeight:0.0}; WindowBorderHitTest={_windowBorder.IsHitTestVisible}; GlassSurface={_glassSurfaceBorder.Visibility}/{_glassSurfaceBorder.ActualWidth:0.0}x{_glassSurfaceBorder.ActualHeight:0.0}/HitTest={_glassSurfaceBorder.IsHitTestVisible}; GlassHighlight={_glassHighlightBorder.Visibility}/{_glassHighlightBorder.ActualWidth:0.0}x{_glassHighlightBorder.ActualHeight:0.0}/HitTest={_glassHighlightBorder.IsHitTestVisible}; Canvas={_canvas.ActualWidth:0.0}x{_canvas.ActualHeight:0.0}/HitTest={_canvas.IsHitTestVisible}; Viewport={_scrollViewer.ViewportWidth:0.0}x{_scrollViewer.ViewportHeight:0.0}; Extent={_scrollViewer.ExtentWidth:0.0}x{_scrollViewer.ExtentHeight:0.0}; Scrollable={_scrollViewer.ScrollableWidth:0.0}x{_scrollViewer.ScrollableHeight:0.0}; ResizeGrip={_resizeGrip.Visibility}/{_resizeGrip.ActualWidth:0.0}x{_resizeGrip.ActualHeight:0.0}/HitTest={_resizeGrip.IsHitTestVisible}; MouseCaptured={Mouse.Captured?.GetType().Name ?? "<null>"}");
    }

    private void QueueBoundsSave()
    {
        if (_applyingBounds ||
            _interactiveSizeMove ||
            WindowState !=
            WindowState.Normal)
        {
            return;
        }

        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Start();
    }

    private void SaveBoundsTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _saveBoundsTimer.Stop();

        if (WindowState !=
            WindowState.Normal)
        {
            return;
        }

        _settings.WindowLeft =
            Left;

        _settings.WindowTop =
            Top;

        _settings.WindowWidth =
            ActualWidth;

        _settings.WindowHeight =
            ActualHeight;

        SaveSettings();
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1 ||
            e.ChangedButton !=
            MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private ContextMenu CreateTitleBarContextMenu()
    {
        ContextMenu menu =
            new();

        ApplyContextMenuAppearance(
            menu);

        MenuItem settingsItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Settings.Title")
            };

        settingsItem.Click +=
            (_, _) =>
                _openSettings();

        menu.Items.Add(
            settingsItem);

        return menu;
    }

    private void DesktopOverlayWindow_Activated(
        object? sender,
        EventArgs e)
    {
        if (string.Equals(
                _settings.ZOrderMode,
                "Desktop",
                StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(
                    LowerToDesktopLayer));
        }
    }

    private void ApplyZOrder()
    {
        Topmost =
            string.Equals(
                _settings.ZOrderMode,
                "AlwaysOnTop",
                StringComparison.OrdinalIgnoreCase);

        if (string.Equals(
                _settings.ZOrderMode,
                "Desktop",
                StringComparison.OrdinalIgnoreCase))
        {
            LowerToDesktopLayer();
        }
    }

    private void LowerToDesktopLayer()
    {
        if (!IsLoaded)
        {
            return;
        }

        nint handle =
            new WindowInteropHelper(
                this).Handle;

        if (handle == 0)
        {
            return;
        }

        nint desktopHost =
            FindDesktopHostWindow();

        if (desktopHost == 0)
        {
            return;
        }

        SetWindowPos(
            handle,
            desktopHost,
            0,
            0,
            0,
            0,
            SwpNoMove |
            SwpNoSize |
            SwpNoActivate);
    }

    private static nint FindDesktopHostWindow()
    {
        nint desktopHost =
            0;

        EnumWindows(
            (window, _) =>
            {
                nint shellView =
                    FindWindowEx(
                        window,
                        0,
                        "SHELLDLL_DefView",
                        null);

                if (shellView == 0)
                {
                    return true;
                }

                desktopHost =
                    window;

                return false;
            },
            0);

        return desktopHost;
    }

    private void ShowDragVisual(
        IReadOnlyList<string> paths,
        string primaryPath,
        Point position)
    {
        RemoveDragVisual();

        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        List<(string Path, double Left, double Top)> items =
            new();

        foreach (string path in paths)
        {
            if (!_itemBorders.TryGetValue(
                    path,
                    out Border? border))
            {
                continue;
            }

            double left =
                Canvas.GetLeft(
                    border);

            double top =
                Canvas.GetTop(
                    border);

            if (double.IsNaN(
                    left) ||
                double.IsNaN(
                    top))
            {
                continue;
            }

            items.Add(
                (
                    path,
                    left,
                    top
                ));
        }

        if (items.Count == 0 &&
            _itemBorders.TryGetValue(
                primaryPath,
                out Border? primaryBorder))
        {
            double left =
                Canvas.GetLeft(
                    primaryBorder);

            double top =
                Canvas.GetTop(
                    primaryBorder);

            items.Add(
                (
                    primaryPath,
                    double.IsNaN(
                        left)
                        ? 0
                        : left,
                    double.IsNaN(
                        top)
                        ? 0
                        : top
                ));
        }

        if (items.Count == 0)
        {
            return;
        }

        double minimumLeft =
            items.Min(
                item =>
                    item.Left);

        double minimumTop =
            items.Min(
                item =>
                    item.Top);

        double maximumRight =
            items.Max(
                item =>
                    item.Left +
                    cellWidth);

        double maximumBottom =
            items.Max(
                item =>
                    item.Top +
                    cellHeight);

        Canvas visual =
            new()
            {
                Width =
                    maximumRight -
                    minimumLeft,
                Height =
                    maximumBottom -
                    minimumTop,
                IsHitTestVisible =
                    false,
                Opacity =
                    0.68
            };

        foreach ((string itemPath, double left, double top) in items)
        {
            FrameworkElement itemVisual =
                CreateDragItemVisual(
                    itemPath,
                    cellWidth,
                    cellHeight);

            Canvas.SetLeft(
                itemVisual,
                left -
                minimumLeft);

            Canvas.SetTop(
                itemVisual,
                top -
                minimumTop);

            visual.Children.Add(
                itemVisual);
        }

        (string Path, double Left, double Top) primary =
            items.FirstOrDefault(
                item =>
                    string.Equals(
                        item.Path,
                        primaryPath,
                        StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(
                primary.Path))
        {
            primary =
                items[0];
        }

        _dragVisualAnchorOffsetX =
            primary.Left -
            minimumLeft +
            cellWidth /
            2.0;

        _dragVisualAnchorOffsetY =
            primary.Top -
            minimumTop +
            cellHeight /
            2.0;

        _dragVisual =
            visual;

        Panel.SetZIndex(
            visual,
            5000);

        _canvas.Children.Add(
            visual);

        UpdateDragVisual(
            position);
    }

    private FrameworkElement CreateDragItemVisual(
        string path,
        double cellWidth,
        double cellHeight)
    {
        double iconSize =
            GetIconSize();

        FrameworkElement icon =
            IsGroupDirectory(
                path)
                ? CreateGroupPreview(
                    path,
                    iconSize)
                : CreateIconImage(
                    path,
                    iconSize);

        TextBlock label =
            new()
            {
                Text =
                    GetDisplayName(
                        path),
                Width =
                    cellWidth,
                FontFamily =
                    SystemFonts.IconFontFamily,
                FontSize =
                    SystemFonts.IconFontSize,
                FontWeight =
                    SystemFonts.IconFontWeight,
                FontStyle =
                    SystemFonts.IconFontStyle,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                TextTrimming =
                    TextTrimming.CharacterEllipsis,
                MaxHeight =
                    Math.Max(
                        28,
                        SystemFonts.IconFontSize *
                        2.8),
                Foreground =
                    CreateEffectiveAppearance().DockTextBrush,
                Background =
                    Brushes.Transparent,
                Margin =
                    new Thickness(
                        0,
                        3,
                        0,
                        0)
            };

        StackPanel stack =
            new()
            {
                Width =
                    cellWidth,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        stack.Children.Add(
            icon);

        stack.Children.Add(
            label);

        return new Border
        {
            Width =
                cellWidth,
            Height =
                cellHeight,
            Padding =
                new Thickness(
                    0,
                    4,
                    0,
                    2),
            Background =
                Brushes.Transparent,
            BorderBrush =
                Brushes.Transparent,
            BorderThickness =
                new Thickness(
                    1),
            CornerRadius =
                DockDialogTheme.StandardCornerRadius,
            IsHitTestVisible =
                false,
            Child =
                stack
        };
    }

    private void UpdateDragVisual(
        Point position)
    {
        if (_dragVisual is null)
        {
            return;
        }

        Canvas.SetLeft(
            _dragVisual,
            position.X -
            _dragVisualAnchorOffsetX);

        Canvas.SetTop(
            _dragVisual,
            position.Y -
            _dragVisualAnchorOffsetY);
    }

    private void RemoveDragVisual()
    {
        if (_dragVisual is null)
        {
            return;
        }

        _canvas.Children.Remove(
            _dragVisual);

        _dragVisual =
            null;

        _dragVisualAnchorOffsetX =
            0;

        _dragVisualAnchorOffsetY =
            0;
    }

    private static Brush CreateResizeGripBrush(
        Brush sourceBrush)
    {
        Brush lineBrush =
            sourceBrush.Clone();

        lineBrush.Opacity =
            0.62;

        Geometry geometry =
            Geometry.Parse(
                "M 24,44 L 44,24 M 31,44 L 44,31 M 38,44 L 44,38");

        GeometryDrawing drawing =
            new(
                null,
                new Pen(
                    lineBrush,
                    1.0)
                {
                    StartLineCap =
                        PenLineCap.Round,
                    EndLineCap =
                        PenLineCap.Round
                },
                geometry);

        DrawingGroup drawingGroup =
            new();

        drawingGroup.Children.Add(
            drawing);

        drawingGroup.ClipGeometry =
            new RectangleGeometry(
                new Rect(
                    0,
                    0,
                    44,
                    44),
                11,
                11);

        return new DrawingBrush(
            drawingGroup)
        {
            Stretch =
                Stretch.Fill,
            AlignmentX =
                AlignmentX.Right,
            AlignmentY =
                AlignmentY.Bottom,
            Viewbox =
                new Rect(
                    0,
                    0,
                    44,
                    44),
            ViewboxUnits =
                BrushMappingMode.Absolute,
            Viewport =
                new Rect(
                    0,
                    0,
                    1,
                    1),
            ViewportUnits =
                BrushMappingMode.RelativeToBoundingBox,
            TileMode =
                TileMode.None
        };
    }

    private void ApplyAppearance()
    {
        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        LogAppearanceState(
            "BeforeApply",
            appearance);

        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                appearance);

        DockDialogThemeService.ApplyScrollViewer(
            _scrollViewer,
            palette);

        DockDialogThemeService.ApplyDialogWindowMaterial(
            _windowBorder,
            _glassSurfaceBorder,
            _glassHighlightBorder,
            _nativeBackdropHost,
            appearance,
            appearance.DockBackgroundBrush,
            Math.Clamp(
                _settings.Opacity,
                0.10,
                1.00),
            Math.Clamp(
                _settings.BlurRadius,
                0,
                100));

        ApplyBorderAppearance(
            appearance);

        Foreground =
            appearance.DockTextBrush;

        Brush titleTextBrush =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                    appearance.DockBackgroundBrush,
                    appearance.DockBackgroundBrush,
                    appearance.DockTextBrush)
                .Clone();

        titleTextBrush.Opacity =
            0.72;

        _titleText.Foreground =
            titleTextBrush;

        _resizeGrip.Background =
            CreateResizeGripBrush(
                appearance.DockTextBrush);

        _resizeGrip.BorderBrush =
            Brushes.Transparent;

        _resizeGrip.BorderThickness =
            new Thickness(
                0);

        LogAppearanceState(
            "AfterApply",
            appearance);
    }

    private void ApplyBorderAppearance(
        GlueDockWidgetAppearance appearance)
    {
        DockDialogThemeService.ApplyDialogWindowBorder(
            _windowBorder,
            appearance,
            preserveLayoutSpace:
                true);
    }

    private void LogAppearanceState(
        string phase,
        GlueDockWidgetAppearance appearance)
    {
        _log(
            $"Appearance {phase}; Theme={appearance.ThemeName}; SettingsTheme={_settings.ThemeName}; BorderEnabledSetting={_settings.BorderEnabled?.ToString() ?? "null"}; EffectiveBorderEnabled={appearance.DockBorderEnabled}; BorderColorSetting={_settings.BorderColor ?? "<null>"}; EffectiveBorderColor={appearance.DockBorderColor}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; GlassSurfaceEnabled={appearance.GlassSurfaceEnabled}; GlassTop={appearance.GlassTopColor}; GlassBottom={appearance.GlassBottomColor}; GlassHighlight={appearance.GlassHighlightColor}; WindowBorderThickness={_windowBorder.BorderThickness}; WindowBorderBrush={DescribeBrush(_windowBorder.BorderBrush)}; WindowBackground={DescribeBrush(_windowBorder.Background)}; GlassSurfaceVisibility={_glassSurfaceBorder.Visibility}; GlassSurfaceBackground={DescribeBrush(_glassSurfaceBorder.Background)}; GlassHighlightVisibility={_glassHighlightBorder.Visibility}; GlassHighlightBackground={DescribeBrush(_glassHighlightBorder.Background)}; GlassHighlightBorder={DescribeBrush(_glassHighlightBorder.BorderBrush)}");
    }

    private static string DescribeBrush(
        Brush? brush)
    {
        if (brush is null)
        {
            return "<null>";
        }

        if (brush is SolidColorBrush solidColorBrush)
        {
            return
                $"Solid({solidColorBrush.Color},Opacity={solidColorBrush.Opacity:0.###})";
        }

        if (brush is LinearGradientBrush linearGradientBrush)
        {
            string stops =
                string.Join(
                    ",",
                    linearGradientBrush.GradientStops.Select(
                        stop =>
                            $"{stop.Offset:0.###}:{stop.Color}"));

            return
                $"Linear(Opacity={linearGradientBrush.Opacity:0.###};Stops={stops})";
        }

        return
            $"{brush.GetType().Name}(Opacity={brush.Opacity:0.###})";
    }

    private GlueDockWidgetAppearance CreateEffectiveAppearance()
    {
        GlueDockWidgetThemeAppearance? selectedTheme =
            _hostAppearance.AvailableThemes
                .FirstOrDefault(
                    theme =>
                        string.Equals(
                            theme.ThemeName,
                            _settings.ThemeName,
                            StringComparison.OrdinalIgnoreCase));

        if (selectedTheme is null)
        {
            return new GlueDockWidgetAppearance
            {
                ThemeName =
                    _hostAppearance.ThemeName,
                DockBackgroundBrush =
                    _hostAppearance.DockBackgroundBrush.Clone(),
                DockItemBackgroundBrush =
                    _hostAppearance.DockItemBackgroundBrush.Clone(),
                DockItemBorderBrush =
                    _hostAppearance.DockItemBorderBrush.Clone(),
                DockTextBrush =
                    _hostAppearance.DockTextBrush.Clone(),
                DockBorderEnabled =
                    _settings.BorderEnabled ??
                    _hostAppearance.DockBorderEnabled,
                DockBorderColor =
                    GetEffectiveBorderColor(),
                Opacity =
                    Math.Clamp(
                        _settings.Opacity,
                        0.10,
                        1.00),
                BlurRadius =
                    Math.Clamp(
                        _settings.BlurRadius,
                        0,
                        100),
                GlassSurfaceEnabled =
                    _hostAppearance.GlassSurfaceEnabled,
                GlassTopColor =
                    _hostAppearance.GlassTopColor,
                GlassBottomColor =
                    _hostAppearance.GlassBottomColor,
                GlassHighlightColor =
                    _hostAppearance.GlassHighlightColor,
                GlassCornerRadius =
                    _hostAppearance.GlassCornerRadius,
                GlassGradientReferenceHeight =
                    Math.Max(
                        1,
                        ActualHeight),
                AvailableThemes =
                    _hostAppearance.AvailableThemes
            };
        }

        return new GlueDockWidgetAppearance
        {
            ThemeName =
                selectedTheme.ThemeName,
            DockBackgroundBrush =
                selectedTheme.DockBackgroundBrush.Clone(),
            DockItemBackgroundBrush =
                selectedTheme.DockItemBackgroundBrush.Clone(),
            DockItemBorderBrush =
                selectedTheme.DockItemBorderBrush.Clone(),
            DockTextBrush =
                selectedTheme.DockTextBrush.Clone(),
            DockBorderEnabled =
                _settings.BorderEnabled ??
                _hostAppearance.DockBorderEnabled,
            DockBorderColor =
                GetEffectiveBorderColor(),
            Opacity =
                Math.Clamp(
                    _settings.Opacity,
                    0.10,
                    1.00),
            BlurRadius =
                Math.Clamp(
                    _settings.BlurRadius,
                    0,
                    100),
            GlassSurfaceEnabled =
                selectedTheme.GlassSurfaceEnabled,
            GlassTopColor =
                selectedTheme.GlassTopColor,
            GlassBottomColor =
                selectedTheme.GlassBottomColor,
            GlassHighlightColor =
                selectedTheme.GlassHighlightColor,
            GlassCornerRadius =
                selectedTheme.GlassCornerRadius,
            GlassGradientReferenceHeight =
                Math.Max(
                    1,
                    ActualHeight),
            AvailableThemes =
                _hostAppearance.AvailableThemes
        };
    }

    private Color GetEffectiveBorderColor()
    {
        if (!string.IsNullOrWhiteSpace(
                _settings.BorderColor))
        {
            try
            {
                return
                    (Color)ColorConverter.ConvertFromString(
                        _settings.BorderColor);
            }
            catch
            {
            }
        }

        return
            _hostAppearance.DockBorderColor;
    }

    private void RefreshLanguage()
    {
        string title =
            _localize(
                "Widget.DesktopOverlay.Window.Title");

        Title =
            title;

        _titleText.Text =
            title;
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Invoke(
            () =>
            {
                RefreshLanguage();
                RefreshItems();
            });
    }

    private void StorageChanged(
        object sender,
        FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Start();
                }));
    }

    private void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _refreshTimer.Stop();
        RefreshItems();
    }

    private static bool TryGetDesktopListViewSpacing(
        out double width,
        out double height)
    {
        width =
            0;

        height =
            0;

        nint listView =
            0;

        EnumWindows(
            (window, _) =>
            {
                nint shellView =
                    FindWindowEx(
                        window,
                        0,
                        "SHELLDLL_DefView",
                        null);

                if (shellView == 0)
                {
                    return true;
                }

                nint candidate =
                    FindWindowEx(
                        shellView,
                        0,
                        "SysListView32",
                        "FolderView");

                if (candidate == 0)
                {
                    candidate =
                        FindWindowEx(
                            shellView,
                            0,
                            "SysListView32",
                            null);
                }

                if (candidate == 0)
                {
                    return true;
                }

                listView =
                    candidate;

                return false;
            },
            0);

        if (listView == 0)
        {
            return false;
        }

        long packedSpacing =
            SendMessage(
                    listView,
                    LvmGetItemSpacing,
                    0,
                    0)
                .ToInt64();

        int widthPixels =
            (int)(
                packedSpacing &
                0xFFFF);

        int heightPixels =
            (int)(
                (packedSpacing >>
                 16) &
                0xFFFF);

        if (widthPixels <= 0 ||
            heightPixels <= 0)
        {
            return false;
        }

        uint desktopDpi =
            GetDpiForWindow(
                listView);

        double dpiScale =
            desktopDpi > 0
                ? desktopDpi /
                  96.0
                : 1.0;

        width =
            widthPixels /
            dpiScale;

        height =
            heightPixels /
            dpiScale;

        return width > 0 &&
               height > 0;
    }

    private (double Width, double Height) GetDesktopGridCellSize()
    {
        if (TryGetDesktopListViewSpacing(
                out double desktopWidth,
                out double desktopHeight))
        {
            return
                (
                    desktopWidth,
                    desktopHeight
                );
        }

        double fallbackWidth =
            Math.Max(
                1,
                SystemParameters.IconHorizontalSpacing);

        double fallbackHeight =
            Math.Max(
                1,
                SystemParameters.IconVerticalSpacing);

        NativeIconMetrics metrics =
            new()
            {
                Size =
                    (uint)Marshal.SizeOf<NativeIconMetrics>(),
                Font =
                    new NativeLogFont
                    {
                        FaceName =
                            string.Empty
                    }
            };

        if (!SystemParametersInfo(
                SpiGetIconMetrics,
                metrics.Size,
                ref metrics,
                0) ||
            metrics.HorizontalSpacing <= 0 ||
            metrics.VerticalSpacing <= 0)
        {
            return
                (
                    fallbackWidth,
                    fallbackHeight
                );
        }

        DpiScale dpi =
            VisualTreeHelper.GetDpi(
                this);

        double scaleX =
            dpi.DpiScaleX > 0
                ? dpi.DpiScaleX
                : 1.0;

        double scaleY =
            dpi.DpiScaleY > 0
                ? dpi.DpiScaleY
                : 1.0;

        return
            (
                Math.Max(
                    1,
                    metrics.HorizontalSpacing /
                    scaleX),
                Math.Max(
                    1,
                    metrics.VerticalSpacing /
                    scaleY)
            );
    }

    private void RefreshItems()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(
                RefreshItems);

            return;
        }

        if (_disposed)
        {
            return;
        }

        Directory.CreateDirectory(
            _storageDirectory);

        _scrollViewer.HorizontalScrollBarVisibility =
            ScrollBarVisibility.Hidden;

        _scrollViewer.VerticalScrollBarVisibility =
            ScrollBarVisibility.Hidden;

        _scrollViewer.UpdateLayout();

        string[] rootPaths =
            Directory.GetFileSystemEntries(
                    _storageDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(
                    path =>
                        !string.Equals(
                            Path.GetFileName(
                                path),
                            GroupMarkerFileName,
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        HashSet<string> current =
            new(
                rootPaths,
                StringComparer.OrdinalIgnoreCase);

        _selectedPaths.RemoveWhere(
            path =>
                !current.Contains(
                    path));

        CleanupPositions(
            current);

        IReadOnlyList<string> ordered =
            OrderPaths(
                rootPaths);

        _orderedRootPaths.Clear();
        _orderedRootPaths.AddRange(
            ordered);

        _canvas.Children.Clear();
        _itemBorders.Clear();
        _itemLabels.Clear();

        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        double viewportWidth =
            Math.Max(
                cellWidth,
                _scrollViewer.ActualWidth > 1
                    ? _scrollViewer.ActualWidth
                    : Math.Max(
                        1,
                        ActualWidth -
                        20));

        double viewportHeight =
            Math.Max(
                cellHeight,
                _scrollViewer.ActualHeight > 1
                    ? _scrollViewer.ActualHeight
                    : Math.Max(
                        1,
                        ActualHeight -
                        60));

        int visibleRows =
            Math.Max(
                1,
                (int)Math.Floor(
                    viewportHeight /
                    cellHeight));

        HashSet<(int Column, int Row)> occupied =
            new();

        int maxColumn =
            Math.Max(
                0,
                (int)Math.Floor(
                    viewportWidth /
                    cellWidth) -
                1);

        for (int index = 0;
             index < ordered.Count;
             index++)
        {
            string path =
                ordered[index];

            DesktopOverlayGridPosition position =
                ResolvePosition(
                    path,
                    index,
                    visibleRows,
                    maxColumn,
                    occupied);

            occupied.Add(
                (
                    position.Column,
                    position.Row
                ));

            Border item =
                CreateRootItem(
                    path,
                    cellWidth,
                    cellHeight);

            _itemBorders[path] =
                item;

            Canvas.SetLeft(
                item,
                position.Column *
                cellWidth);

            Canvas.SetTop(
                item,
                position.Row *
                cellHeight);

            _canvas.Children.Add(
                item);
        }

        int extentColumns =
            occupied.Count == 0
                ? 1
                : occupied.Max(
                      cell =>
                          cell.Column) +
                  1;

        int extentRows =
            occupied.Count == 0
                ? 1
                : occupied.Max(
                      cell =>
                          cell.Row) +
                  1;

        double requiredWidth =
            extentColumns *
            cellWidth;

        double requiredHeight =
            extentRows *
            cellHeight;

        bool needsHorizontalScroll =
            requiredWidth >
            viewportWidth;

        bool needsVerticalScroll =
            requiredHeight >
            viewportHeight;

        _scrollViewer.HorizontalScrollBarVisibility =
            needsHorizontalScroll
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Hidden;

        _scrollViewer.VerticalScrollBarVisibility =
            needsVerticalScroll
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Hidden;

        _canvas.Width =
            needsHorizontalScroll
                ? requiredWidth
                : Math.Max(
                    cellWidth,
                    viewportWidth);

        _canvas.Height =
            needsVerticalScroll
                ? requiredHeight
                : Math.Max(
                    cellHeight,
                    viewportHeight);

        bool usesDesktopListViewSpacing =
            TryGetDesktopListViewSpacing(
                out double desktopGridWidth,
                out double desktopGridHeight);

        _log(
            $"RefreshItems; Items={ordered.Count}; Grid={cellWidth:0.0}x{cellHeight:0.0}; GridSource={(usesDesktopListViewSpacing ? $"DesktopListView({desktopGridWidth:0.0}x{desktopGridHeight:0.0})" : "SystemMetrics")}; Dpi={VisualTreeHelper.GetDpi(this).PixelsPerDip:0.###}; Viewport={viewportWidth:0.0}x{viewportHeight:0.0}; Required={requiredWidth:0.0}x{requiredHeight:0.0}; HScroll={needsHorizontalScroll}; VScroll={needsVerticalScroll}; Window={ActualWidth:0.0}x{ActualHeight:0.0}");

        UpdateSelectionVisuals();
    }

    private IReadOnlyList<string> OrderPaths(
        IEnumerable<string> paths)
    {
        if (!_settings.AutoArrange)
        {
            return paths
                .OrderBy(
                    path =>
                        _settings.Positions.TryGetValue(
                            GetRelativeKey(
                                path),
                            out DesktopOverlayGridPosition? position)
                            ? position.Column
                            : int.MaxValue)
                .ThenBy(
                    path =>
                        _settings.Positions.TryGetValue(
                            GetRelativeKey(
                                path),
                            out DesktopOverlayGridPosition? position)
                            ? position.Row
                            : int.MaxValue)
                .ThenBy(
                    GetDisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return _settings.SortMode switch
        {
            "Type" =>
                paths
                    .OrderBy(
                        GetSortType,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(
                        GetDisplayName,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
            "Date" =>
                paths
                    .OrderByDescending(
                        GetLastWriteTime)
                    .ThenBy(
                        GetDisplayName,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
            _ =>
                paths
                    .OrderBy(
                        GetDisplayName,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
        };
    }

    private DesktopOverlayGridPosition ResolvePosition(
        string path,
        int index,
        int visibleRows,
        int maxColumn,
        HashSet<(int Column, int Row)> occupied)
    {
        string key =
            GetRelativeKey(
                path);

        if (!_settings.AutoArrange &&
            _settings.Positions.TryGetValue(
                key,
                out DesktopOverlayGridPosition? stored))
        {
            int column =
                Math.Max(
                    0,
                    stored.Column);

            int row =
                Math.Max(
                    0,
                    stored.Row);

            if (!occupied.Contains(
                    (
                        column,
                        row
                    )))
            {
                return new DesktopOverlayGridPosition
                {
                    Column =
                        column,
                    Row =
                        row
                };
            }
        }

        int candidateIndex =
            _settings.AutoArrange
                ? index
                : 0;

        while (true)
        {
            int column =
                candidateIndex /
                visibleRows;

            int row =
                candidateIndex %
                visibleRows;

            if (!occupied.Contains(
                    (
                        column,
                        row
                    )))
            {
                DesktopOverlayGridPosition result =
                    new()
                    {
                        Column =
                            column,
                        Row =
                            row
                    };

                if (!_settings.AutoArrange)
                {
                    _settings.Positions[key] =
                        result;
                }

                return result;
            }

            candidateIndex++;
        }
    }

    private static double GetCollapsedLabelMaxHeight(
        double cellHeight,
        double iconSize)
    {
        return Math.Max(
            SystemFonts.IconFontSize,
            cellHeight -
            iconSize -
            3);
    }

    private Border CreateRootItem(
        string path,
        double cellWidth,
        double cellHeight)
    {
        double iconSize =
            GetIconSize();

        bool isGroup =
            IsGroupDirectory(
                path);

        FrameworkElement icon =
            isGroup
                ? CreateGroupPreview(
                    path,
                    iconSize)
                : CreateIconImage(
                    path,
                    iconSize);

        TextBlock label =
            new()
            {
                Text =
                    GetDisplayName(
                        path),
                Width =
                    cellWidth,
                FontFamily =
                    SystemFonts.IconFontFamily,
                FontSize =
                    SystemFonts.IconFontSize,
                FontWeight =
                    SystemFonts.IconFontWeight,
                FontStyle =
                    SystemFonts.IconFontStyle,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                TextTrimming =
                    TextTrimming.CharacterEllipsis,
                MaxHeight =
                    GetCollapsedLabelMaxHeight(
                        cellHeight,
                        iconSize),
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                Margin =
                    new Thickness(
                        0,
                        3,
                        0,
                        0),
                Foreground =
                    CreateEffectiveAppearance().DockTextBrush
            };

        Canvas itemCanvas =
            new()
            {
                Width =
                    cellWidth,
                Height =
                    cellHeight,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                VerticalAlignment =
                    VerticalAlignment.Top,
                ClipToBounds =
                    false
            };

        Canvas.SetLeft(
            icon,
            Math.Max(
                0,
                (cellWidth - iconSize) /
                2));

        Canvas.SetTop(
            icon,
            0);

        Canvas.SetLeft(
            label,
            0);

        Canvas.SetTop(
            label,
            iconSize +
            3);

        itemCanvas.Children.Add(
            icon);

        itemCanvas.Children.Add(
            label);

        Border border =
            new()
            {
                Width =
                    cellWidth,
                Height =
                    cellHeight,
                MinHeight =
                    cellHeight,
                Padding =
                    new Thickness(
                        0,
                        4,
                        0,
                        2),
                Background =
                    Brushes.Transparent,
                BorderBrush =
                    Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        1),
                CornerRadius =
                    DockDialogTheme.StandardCornerRadius,
                Child =
                    itemCanvas,
                Tag =
                    path,
                AllowDrop =
                    true,
                ClipToBounds =
                    false
            };

        border.PreviewMouseLeftButtonDown +=
            Item_PreviewMouseLeftButtonDown;

        border.PreviewMouseLeftButtonUp +=
            Item_PreviewMouseLeftButtonUp;

        border.PreviewMouseRightButtonDown +=
            Item_PreviewMouseRightButtonDown;

        border.PreviewMouseRightButtonUp +=
            Item_PreviewMouseRightButtonUp;

        border.MouseEnter +=
            Item_MouseEnter;

        border.MouseLeave +=
            Item_MouseLeave;

        border.PreviewMouseMove +=
            Item_PreviewMouseMove;

        border.MouseLeftButtonDown +=
            Item_MouseDoubleClick;

        border.Drop +=
            Item_Drop;

        border.DragOver +=
            Item_DragOver;

        if (isGroup)
        {
            border.ContextMenu =
                CreateItemContextMenu(
                    path,
                    true);
        }

        _itemLabels[path] =
            label;

        return border;
    }

    private FrameworkElement CreateIconImage(
        string path,
        double iconSize)
    {
        bool isShortcut =
            path.EndsWith(
                ".lnk",
                StringComparison.OrdinalIgnoreCase);

        BitmapSource? source =
            DesktopOverlayShellIcon.GetIcon(
                path,
                _settings.ShowFilePreviews,
                _log,
                includeShortcutOverlay:
                    !isShortcut);

        Image image =
            new()
            {
                Width =
                    iconSize,
                Height =
                    iconSize,
                Stretch =
                    Stretch.Uniform,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                SnapsToDevicePixels =
                    true,
                UseLayoutRounding =
                    true
            };

        if (source is not null)
        {
            image.Source =
                source;
        }

        if (!isShortcut)
        {
            return image;
        }

        BitmapSource? overlaySource =
            DesktopOverlayShellIcon.GetLinkOverlayIcon();

        if (overlaySource is null)
        {
            _log(
                $"Shortcut overlay unavailable; Item={Path.GetFileName(path)}; BasePixels={(source is null ? "none" : $"{source.PixelWidth}x{source.PixelHeight}")}; Dpi={VisualTreeHelper.GetDpi(this).DpiScaleX:0.00}");

            return image;
        }

        double overlayWidth =
            Math.Min(
                iconSize,
                SystemParameters.SmallIconWidth);

        double overlayHeight =
            Math.Min(
                iconSize,
                SystemParameters.SmallIconHeight);

        Image overlay =
            new()
            {
                Source =
                    overlaySource,
                Width =
                    overlayWidth,
                Height =
                    overlayHeight,
                Stretch =
                    Stretch.Uniform,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                IsHitTestVisible =
                    false,
                SnapsToDevicePixels =
                    true,
                UseLayoutRounding =
                    true
            };

        Grid iconRoot =
            new()
            {
                Width =
                    iconSize,
                Height =
                    iconSize,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                ClipToBounds =
                    false,
                SnapsToDevicePixels =
                    true,
                UseLayoutRounding =
                    true
            };

        iconRoot.Children.Add(
            image);

        iconRoot.Children.Add(
            overlay);

        _log(
            $"Shortcut icon composed as separate WPF layers; Item={Path.GetFileName(path)}; IconDip={iconSize:0.0}; BasePixels={(source is null ? "none" : $"{source.PixelWidth}x{source.PixelHeight}")}; OverlayPixels={overlaySource.PixelWidth}x{overlaySource.PixelHeight}; OverlayDip={overlayWidth:0.0}x{overlayHeight:0.0}; Dpi={VisualTreeHelper.GetDpi(this).DpiScaleX:0.00}");

        return iconRoot;
    }

    private FrameworkElement CreateGroupPreview(
        string groupPath,
        double iconSize)
    {
        Grid grid =
            new()
            {
                Width =
                    iconSize,
                Height =
                    iconSize,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        for (int index = 0;
             index < 2;
             index++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition());

            grid.ColumnDefinitions.Add(
                new ColumnDefinition());
        }

        string[] children =
            GetGroupChildren(
                    groupPath)
                .Take(
                    4)
                .ToArray();

        for (int index = 0;
             index < children.Length;
             index++)
        {
            FrameworkElement image =
                CreateIconImage(
                    children[index],
                    Math.Max(
                        12,
                        iconSize /
                        2 -
                        2));

            Grid.SetRow(
                image,
                index /
                2);

            Grid.SetColumn(
                image,
                index %
                2);

            grid.Children.Add(
                image);
        }

        return grid;
    }

    private double GetIconSize()
    {
        return _settings.IconSizeMode switch
        {
            "Small" =>
                32,
            "Large" =>
                64,
            _ =>
                48
        };
    }

    private void DesktopOverlayWindow_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (_inlineRenameTextBox is not null)
        {
            return;
        }

        if (e.Key ==
            Key.Escape)
        {
            e.Handled =
                true;

            Close();

            return;
        }

        if (e.Key ==
            Key.Delete)
        {
            if (_selectedPaths.Count ==
                0)
            {
                return;
            }

            e.Handled =
                true;

            DeleteSelectedPathsToRecycleBin();

            return;
        }

        if (e.Key !=
            Key.F2 ||
            _selectedPaths.Count !=
            1)
        {
            return;
        }

        string path =
            _selectedPaths.First();

        if (!File.Exists(
                path) &&
            !Directory.Exists(
                path))
        {
            return;
        }

        e.Handled =
            true;

        RenamePath(
            path);
    }

    private void Item_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string path)
        {
            return;
        }

        _hoveredPath =
            path;

        UpdateSelectionVisuals();
    }

    private void Item_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string path ||
            !string.Equals(
                _hoveredPath,
                path,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _hoveredPath =
            null;

        UpdateSelectionVisuals();
    }

    private void Item_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string path)
        {
            return;
        }

        if (!_selectedPaths.Contains(
                path))
        {
            _selectedPaths.Clear();

            _selectedPaths.Add(
                path);

            UpdateSelectionVisuals();
        }
    }

    private void Canvas_PreviewMouseRightButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Right)
        {
            return;
        }

        DependencyObject? current =
            e.OriginalSource as DependencyObject;

        while (current is not null &&
               !ReferenceEquals(
                   current,
                   _canvas))
        {
            if (current is Border border &&
                border.Tag is string path &&
                _itemBorders.TryGetValue(
                    path,
                    out Border? itemBorder) &&
                ReferenceEquals(
                    border,
                    itemBorder))
            {
                return;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        _selectedPaths.Clear();

        UpdateSelectionVisuals();

        if (DesktopOverlayShellContextMenu.TryShowFolderBackground(
                this,
                _storageDirectory,
                _log))
        {
            e.Handled =
                true;
        }
    }

    private void Item_PreviewMouseRightButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string path ||
            IsGroupDirectory(
                path))
        {
            return;
        }

        string[] paths =
            _selectedPaths
                .Where(
                    selectedPath =>
                        !IsGroupDirectory(
                            selectedPath))
                .ToArray();

        if (paths.Length == 0 ||
            !paths.Any(
                selectedPath =>
                    string.Equals(
                        selectedPath,
                        path,
                        StringComparison.OrdinalIgnoreCase)))
        {
            paths =
                new[]
                {
                    path
                };
        }

        bool shown =
            DesktopOverlayShellContextMenu.TryShow(
                this,
                paths,
                canonicalVerb =>
                {
                    if (!string.Equals(
                            canonicalVerb,
                            "rename",
                            StringComparison.OrdinalIgnoreCase) ||
                        paths.Length !=
                        1)
                    {
                        return false;
                    }

                    RenamePath(
                        paths[0]);

                    return true;
                },
                _log);

        if (shown)
        {
            e.Handled =
                true;

            return;
        }

        ContextMenu fallbackMenu =
            CreateItemContextMenu(
                path,
                false);

        border.ContextMenu =
            fallbackMenu;

        fallbackMenu.IsOpen =
            true;

        e.Handled =
            true;
    }

    private void Item_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string path)
        {
            return;
        }

        _dragStart =
            e.GetPosition(
                _canvas);

        _dragCandidatePath =
            path;

        if ((Keyboard.Modifiers &
             ModifierKeys.Control) !=
            0)
        {
            if (!_selectedPaths.Add(
                    path))
            {
                _selectedPaths.Remove(
                    path);
            }
        }
        else if ((Keyboard.Modifiers &
                  ModifierKeys.Shift) !=
                 0 &&
                 _selectedPaths.Count > 0)
        {
            int anchorIndex =
                _orderedRootPaths.FindIndex(
                    item =>
                        _selectedPaths.Contains(
                            item));

            int currentIndex =
                _orderedRootPaths.FindIndex(
                    item =>
                        string.Equals(
                            item,
                            path,
                            StringComparison.OrdinalIgnoreCase));

            if (anchorIndex >= 0 &&
                currentIndex >= 0)
            {
                _selectedPaths.Clear();

                int first =
                    Math.Min(
                        anchorIndex,
                        currentIndex);

                int last =
                    Math.Max(
                        anchorIndex,
                        currentIndex);

                for (int index = first;
                     index <= last;
                     index++)
                {
                    _selectedPaths.Add(
                        _orderedRootPaths[index]);
                }
            }
        }
        else if (!_selectedPaths.Contains(
                     path))
        {
            _selectedPaths.Clear();

            _selectedPaths.Add(
                path);
        }

        UpdateSelectionVisuals();
    }

    private void Item_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Border border &&
            border.Tag is string path &&
            string.Equals(
                _dragCandidatePath,
                path,
                StringComparison.OrdinalIgnoreCase) &&
            IsGroupDirectory(
                path) &&
            (Keyboard.Modifiers &
             (ModifierKeys.Control |
              ModifierKeys.Shift)) ==
            ModifierKeys.None)
        {
            OpenGroupPopup(
                border,
                path);
        }

        _dragCandidatePath =
            null;
    }

    private void Item_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_dragInProgress ||
            e.LeftButton !=
            MouseButtonState.Pressed ||
            string.IsNullOrWhiteSpace(
                _dragCandidatePath))
        {
            return;
        }

        Point current =
            e.GetPosition(
                _canvas);

        if (Math.Abs(
                current.X -
                _dragStart.X) <
            SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(
                current.Y -
                _dragStart.Y) <
            SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        string[] paths =
            _selectedPaths.Contains(
                _dragCandidatePath)
                ? _selectedPaths.ToArray()
                : new[]
                {
                    _dragCandidatePath
                };

        DataObject data =
            new();

        data.SetData(
            InternalDragFormat,
            paths);

        data.SetData(
            DataFormats.FileDrop,
            paths);

        _dragInProgress =
            true;

        ShowDragVisual(
            paths,
            _dragCandidatePath,
            current);

        _log(
            $"Internal drag started; Items={paths.Length}; Primary={Path.GetFileName(_dragCandidatePath)}");

        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                data,
                DragDropEffects.Copy |
                DragDropEffects.Move |
                DragDropEffects.Link);
        }
        finally
        {
            RemoveDragVisual();

            _dragInProgress =
                false;

            _dragCandidatePath =
                null;

            _log(
                "Internal drag finished.");
        }
    }

    private void Item_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount !=
            2 ||
            sender is not Border border ||
            border.Tag is not string path)
        {
            return;
        }

        e.Handled =
            true;

        if (IsGroupDirectory(
                path))
        {
            OpenGroupPopup(
                border,
                path);

            return;
        }

        OpenPath(
            path);
    }

    private void Item_DragOver(
        object sender,
        DragEventArgs e)
    {
        UpdateDragVisual(
            e.GetPosition(
                _canvas));

        if (sender is not Border border ||
            border.Tag is not string targetPath)
        {
            return;
        }

        if (!CanAcceptDrop(
                e.Data,
                targetPath))
        {
            return;
        }

        if (e.Data.GetDataPresent(
                DataFormats.FileDrop) &&
            e.Data.GetData(
                DataFormats.FileDrop) is
                string[] paths)
        {
            e.Effects =
                GetExternalDropEffect(
                    e,
                    paths);
        }
        else
        {
            e.Effects =
                DragDropEffects.Move;
        }

        e.Handled =
            true;
    }

    private void Item_Drop(
        object sender,
        DragEventArgs e)
    {
        if (sender is not Border border ||
            border.Tag is not string targetPath)
        {
            return;
        }

        e.Handled =
            true;

        try
        {
            if (e.Data.GetDataPresent(
                    InternalDragFormat) &&
                e.Data.GetData(
                    InternalDragFormat) is
                    string[] internalPaths)
            {
                if (internalPaths.Any(
                        path =>
                            string.Equals(
                                path,
                                targetPath,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    _log(
                        $"Internal drop over dragged item treated as canvas move; Target={Path.GetFileName(targetPath)}; Items={internalPaths.Length}");

                    HandleInternalDropOnCanvas(
                        internalPaths,
                        e.GetPosition(
                            _canvas));

                    return;
                }

                HandleInternalDropOnItem(
                    internalPaths,
                    targetPath);

                return;
            }

            if (e.Data.GetDataPresent(
                    DataFormats.FileDrop) &&
                e.Data.GetData(
                    DataFormats.FileDrop) is
                    string[] externalPaths)
            {
                IReadOnlyList<string> imported =
                    ImportExternalItems(
                        externalPaths,
                        GetExternalDropEffect(
                            e,
                            externalPaths));

                if (imported.Count > 0)
                {
                    AddItemsToTarget(
                        imported,
                        targetPath);
                }

                RefreshItems();
            }
        }
        catch
        {
        }
    }

    private bool CanAcceptDrop(
        IDataObject data,
        string targetPath)
    {
        if (data.GetDataPresent(
                InternalDragFormat) &&
            data.GetData(
                InternalDragFormat) is
                string[] paths)
        {
            if (paths.Any(
                    path =>
                        string.Equals(
                            path,
                            targetPath,
                            StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return paths.Length > 0;
        }

        return data.GetDataPresent(
            DataFormats.FileDrop);
    }

    private void HandleInternalDropOnItem(
        IEnumerable<string> sourcePaths,
        string targetPath)
    {
        string[] sourcePathArray =
            sourcePaths
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (sourcePathArray.Any(
                path =>
                    string.Equals(
                        path,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase)))
        {
            _log(
                $"Internal item drop ignored; TargetIsDraggedItem=True; Target={Path.GetFileName(targetPath)}; Items={sourcePathArray.Length}");

            return;
        }

        List<string> paths =
            sourcePathArray
                .Where(
                    path =>
                        File.Exists(
                            path) ||
                        Directory.Exists(
                            path))
                .Where(
                    path =>
                        !string.Equals(
                            path,
                            targetPath,
                            StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (paths.Count == 0)
        {
            return;
        }

        _log(
            $"Internal item drop accepted for grouping; Target={Path.GetFileName(targetPath)}; Items={paths.Count}");

        AddItemsToTarget(
            paths,
            targetPath);

        _selectedPaths.Clear();
        RefreshItems();
    }

    private void AddItemsToTarget(
        IReadOnlyList<string> sourcePaths,
        string targetPath)
    {
        if (IsGroupDirectory(
                targetPath))
        {
            foreach (string sourcePath in sourcePaths)
            {
                MoveIntoDirectory(
                    sourcePath,
                    targetPath);
            }

            return;
        }

        string groupPath =
            CreateUniqueDirectory(
                _storageDirectory,
                _localize(
                    "Widget.DesktopOverlay.Group.DefaultName"));

        WriteGroupMarker(
            groupPath);

        MoveIntoDirectory(
            targetPath,
            groupPath);

        foreach (string sourcePath in sourcePaths)
        {
            if (File.Exists(
                    sourcePath) ||
                Directory.Exists(
                    sourcePath))
            {
                MoveIntoDirectory(
                    sourcePath,
                    groupPath);
            }
        }
    }

    private void Canvas_DragOver(
        object sender,
        DragEventArgs e)
    {
        UpdateDragVisual(
            e.GetPosition(
                _canvas));

        if (e.Data.GetDataPresent(
                InternalDragFormat))
        {
            e.Effects =
                DragDropEffects.Move;

            e.Handled =
                true;

            return;
        }

        if (e.Data.GetDataPresent(
                DataFormats.FileDrop) &&
            e.Data.GetData(
                DataFormats.FileDrop) is
                string[] paths)
        {
            e.Effects =
                GetExternalDropEffect(
                    e,
                    paths);

            e.Handled =
                true;
        }
    }

    private void Canvas_Drop(
        object sender,
        DragEventArgs e)
    {
        e.Handled =
            true;

        try
        {
            Point dropPoint =
                e.GetPosition(
                    _canvas);

            if (e.Data.GetDataPresent(
                    InternalDragFormat) &&
                e.Data.GetData(
                    InternalDragFormat) is
                    string[] internalPaths)
            {
                HandleInternalDropOnCanvas(
                    internalPaths,
                    dropPoint);

                return;
            }

            if (e.Data.GetDataPresent(
                    DataFormats.FileDrop) &&
                e.Data.GetData(
                    DataFormats.FileDrop) is
                    string[] externalPaths)
            {
                IReadOnlyList<string> imported =
                    ImportExternalItems(
                        externalPaths,
                        GetExternalDropEffect(
                            e,
                            externalPaths));

                PositionImportedItems(
                    imported,
                    dropPoint);

                RefreshItems();
            }
        }
        catch
        {
        }
    }

    private void HandleInternalDropOnCanvas(
        IEnumerable<string> sourcePaths,
        Point dropPoint)
    {
        List<string> resultingRootPaths =
            new();

        HashSet<string> sourceGroups =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (string sourcePath in sourcePaths
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(
                    sourcePath) &&
                !Directory.Exists(
                    sourcePath))
            {
                continue;
            }

            string? parent =
                Path.GetDirectoryName(
                    sourcePath);

            if (parent is not null &&
                !PathsEqual(
                    parent,
                    _storageDirectory))
            {
                if (IsGroupDirectory(
                        parent))
                {
                    sourceGroups.Add(
                        parent);
                }

                string moved =
                    MoveIntoDirectory(
                        sourcePath,
                        _storageDirectory);

                resultingRootPaths.Add(
                    moved);
            }
            else
            {
                resultingRootPaths.Add(
                    sourcePath);
            }
        }

        foreach (string groupPath in sourceGroups)
        {
            DissolveGroupIfNeeded(
                groupPath);
        }

        PositionInternalItems(
            resultingRootPaths,
            dropPoint,
            _dragCandidatePath);

        _selectedPaths.Clear();

        foreach (string path in resultingRootPaths)
        {
            _selectedPaths.Add(
                path);
        }

        SaveSettings();
        RefreshItems();
    }

    private void PositionInternalItems(
        IReadOnlyList<string> paths,
        Point dropPoint,
        string? primaryPath)
    {
        if (_settings.AutoArrange ||
            paths.Count == 0)
        {
            return;
        }

        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        Dictionary<string, DesktopOverlayGridPosition> sourcePositions =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            string key =
                GetRelativeKey(
                    path);

            if (_settings.Positions.TryGetValue(
                    key,
                    out DesktopOverlayGridPosition? stored))
            {
                sourcePositions[path] =
                    new DesktopOverlayGridPosition
                    {
                        Column =
                            stored.Column,
                        Row =
                            stored.Row
                    };

                continue;
            }

            if (_itemBorders.TryGetValue(
                    path,
                    out Border? border))
            {
                double left =
                    Canvas.GetLeft(
                        border);

                double top =
                    Canvas.GetTop(
                        border);

                if (!double.IsNaN(
                        left) &&
                    !double.IsNaN(
                        top))
                {
                    sourcePositions[path] =
                        new DesktopOverlayGridPosition
                        {
                            Column =
                                Math.Max(
                                    0,
                                    (int)Math.Round(
                                        left /
                                        cellWidth)),
                            Row =
                                Math.Max(
                                    0,
                                    (int)Math.Round(
                                        top /
                                        cellHeight))
                        };
                }
            }
        }

        string? anchorPath =
            !string.IsNullOrWhiteSpace(
                primaryPath) &&
            paths.Any(
                path =>
                    string.Equals(
                        path,
                        primaryPath,
                        StringComparison.OrdinalIgnoreCase))
                ? primaryPath
                : paths.FirstOrDefault(
                    sourcePositions.ContainsKey);

        if (string.IsNullOrWhiteSpace(
                anchorPath) ||
            !sourcePositions.TryGetValue(
                anchorPath,
                out DesktopOverlayGridPosition? anchorPosition))
        {
            PositionImportedItems(
                paths,
                dropPoint);

            return;
        }

        double requestedAnchorLeft =
            dropPoint.X -
            cellWidth /
            2.0;

        double requestedAnchorTop =
            dropPoint.Y -
            cellHeight /
            2.0;

        int requestedAnchorColumn =
            Math.Max(
                0,
                (int)Math.Round(
                    requestedAnchorLeft /
                    cellWidth,
                    MidpointRounding.AwayFromZero));

        int requestedAnchorRow =
            Math.Max(
                0,
                (int)Math.Round(
                    requestedAnchorTop /
                    cellHeight,
                    MidpointRounding.AwayFromZero));

        _log(
            $"Internal grid snap; Mouse={dropPoint.X:0.0},{dropPoint.Y:0.0}; AnchorTopLeft={requestedAnchorLeft:0.0},{requestedAnchorTop:0.0}; Cell={cellWidth:0.0}x{cellHeight:0.0}; Source={anchorPosition.Column},{anchorPosition.Row}; Target={requestedAnchorColumn},{requestedAnchorRow}; Items={paths.Count}");

        HashSet<string> movingKeys =
            paths
                .Select(
                    GetRelativeKey)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        HashSet<(int Column, int Row)> occupied =
            _settings.Positions
                .Where(
                    pair =>
                        !movingKeys.Contains(
                            pair.Key))
                .Select(
                    pair =>
                        (
                            pair.Value.Column,
                            pair.Value.Row
                        ))
                .ToHashSet();

        int deltaColumn =
            requestedAnchorColumn -
            anchorPosition.Column;

        int deltaRow =
            requestedAnchorRow -
            anchorPosition.Row;

        int minimumColumn =
            sourcePositions.Values.Min(
                position =>
                    position.Column +
                    deltaColumn);

        int minimumRow =
            sourcePositions.Values.Min(
                position =>
                    position.Row +
                    deltaRow);

        if (minimumColumn < 0)
        {
            deltaColumn -=
                minimumColumn;
        }

        if (minimumRow < 0)
        {
            deltaRow -=
                minimumRow;
        }

        bool conflicts =
            sourcePositions.Values.Any(
                position =>
                    occupied.Contains(
                        (
                            position.Column +
                            deltaColumn,
                            position.Row +
                            deltaRow
                        )));

        while (conflicts)
        {
            deltaRow++;

            conflicts =
                sourcePositions.Values.Any(
                    position =>
                        occupied.Contains(
                            (
                                position.Column +
                                deltaColumn,
                                position.Row +
                                deltaRow
                            )));
        }

        foreach (string path in paths)
        {
            if (!sourcePositions.TryGetValue(
                    path,
                    out DesktopOverlayGridPosition? sourcePosition))
            {
                continue;
            }

            _settings.Positions[GetRelativeKey(
                path)] =
                new DesktopOverlayGridPosition
                {
                    Column =
                        sourcePosition.Column +
                        deltaColumn,
                    Row =
                        sourcePosition.Row +
                        deltaRow
                };
        }

        IReadOnlyList<string> unpositioned =
            paths
                .Where(
                    path =>
                        !sourcePositions.ContainsKey(
                            path))
                .ToList();

        if (unpositioned.Count > 0)
        {
            PositionImportedItems(
                unpositioned,
                dropPoint);
        }
        else
        {
            SaveSettings();
        }
    }

    private void PositionImportedItems(
        IReadOnlyList<string> paths,
        Point dropPoint)
    {
        if (_settings.AutoArrange ||
            paths.Count == 0)
        {
            return;
        }

        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        int baseColumn =
            Math.Max(
                0,
                (int)Math.Round(
                    dropPoint.X /
                    cellWidth));

        int baseRow =
            Math.Max(
                0,
                (int)Math.Round(
                    dropPoint.Y /
                    cellHeight));

        HashSet<(int Column, int Row)> occupied =
            _settings.Positions
                .Where(
                    pair =>
                        !paths.Any(
                            path =>
                                string.Equals(
                                    pair.Key,
                                    GetRelativeKey(
                                        path),
                                    StringComparison.OrdinalIgnoreCase)))
                .Select(
                    pair =>
                        (
                            pair.Value.Column,
                            pair.Value.Row
                        ))
                .ToHashSet();

        int offset =
            0;

        foreach (string path in paths)
        {
            int column =
                baseColumn;

            int row =
                baseRow +
                offset;

            while (occupied.Contains(
                       (
                           column,
                           row
                       )))
            {
                row++;
            }

            _settings.Positions[GetRelativeKey(
                path)] =
                new DesktopOverlayGridPosition
                {
                    Column =
                        column,
                    Row =
                        row
                };

            occupied.Add(
                (
                    column,
                    row
                ));

            offset++;
        }

        SaveSettings();
    }

    private DragDropEffects GetExternalDropEffect(
        DragEventArgs e,
        IReadOnlyList<string> paths)
    {
        bool ctrl =
            (e.KeyStates &
             DragDropKeyStates.ControlKey) !=
            0;

        bool shift =
            (e.KeyStates &
             DragDropKeyStates.ShiftKey) !=
            0;

        if (ctrl &&
            e.AllowedEffects.HasFlag(
                DragDropEffects.Copy))
        {
            return DragDropEffects.Copy;
        }

        if (shift &&
            e.AllowedEffects.HasFlag(
                DragDropEffects.Move))
        {
            return DragDropEffects.Move;
        }

        bool sameVolume =
            paths.Count > 0 &&
            paths.All(
                path =>
                    string.Equals(
                        Path.GetPathRoot(
                            path),
                        Path.GetPathRoot(
                            _storageDirectory),
                        StringComparison.OrdinalIgnoreCase));

        if (sameVolume &&
            e.AllowedEffects.HasFlag(
                DragDropEffects.Move))
        {
            return DragDropEffects.Move;
        }

        return e.AllowedEffects.HasFlag(
                   DragDropEffects.Copy)
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
    }

    private IReadOnlyList<string> ImportExternalItems(
        IEnumerable<string> sourcePaths,
        DragDropEffects effect)
    {
        List<string> imported =
            new();

        foreach (string sourcePath in sourcePaths)
        {
            if (!File.Exists(
                    sourcePath) &&
                !Directory.Exists(
                    sourcePath))
            {
                continue;
            }

            if (IsPathInsideStorage(
                    sourcePath))
            {
                continue;
            }

            string destination =
                GetUniqueDestinationPath(
                    _storageDirectory,
                    Path.GetFileName(
                        sourcePath));

            if (effect ==
                DragDropEffects.Move)
            {
                MovePath(
                    sourcePath,
                    destination);
            }
            else
            {
                CopyPath(
                    sourcePath,
                    destination);
            }

            imported.Add(
                destination);
        }

        return imported;
    }

    private void Canvas_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource !=
            _canvas ||
            e.ChangedButton !=
            MouseButton.Left)
        {
            return;
        }

        _selectionStart =
            e.GetPosition(
                _canvas);

        _selectionInProgress =
            true;

        _selectionBasePaths.Clear();

        if ((Keyboard.Modifiers &
             ModifierKeys.Control) !=
            0)
        {
            foreach (string path in _selectedPaths)
            {
                _selectionBasePaths.Add(
                    path);
            }
        }
        else
        {
            _selectedPaths.Clear();
        }

        RemoveSelectionRectangle();

        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        Brush selectionFill =
            appearance.DockItemBackgroundBrush.Clone();

        selectionFill.Opacity =
            Math.Min(
                0.35,
                selectionFill.Opacity);

        Brush selectionBorder =
            appearance.DockItemBorderBrush.Clone();

        selectionBorder.Opacity =
            Math.Max(
                0.65,
                selectionBorder.Opacity);

        _selectionRectangle =
            new Border
            {
                Background =
                    selectionFill,
                BorderBrush =
                    selectionBorder,
                BorderThickness =
                    new Thickness(
                        1),
                IsHitTestVisible =
                    false
            };

        Panel.SetZIndex(
            _selectionRectangle,
            4000);

        _canvas.Children.Add(
            _selectionRectangle);

        _canvas.CaptureMouse();

        UpdateSelectionVisuals();

        e.Handled =
            true;
    }

    private void Canvas_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_selectionInProgress ||
            e.LeftButton !=
            MouseButtonState.Pressed ||
            _selectionRectangle is null)
        {
            return;
        }

        Point current =
            e.GetPosition(
                _canvas);

        double left =
            Math.Min(
                _selectionStart.X,
                current.X);

        double top =
            Math.Min(
                _selectionStart.Y,
                current.Y);

        double width =
            Math.Abs(
                current.X -
                _selectionStart.X);

        double height =
            Math.Abs(
                current.Y -
                _selectionStart.Y);

        Canvas.SetLeft(
            _selectionRectangle,
            left);

        Canvas.SetTop(
            _selectionRectangle,
            top);

        _selectionRectangle.Width =
            width;

        _selectionRectangle.Height =
            height;

        Rect selectionBounds =
            new(
                left,
                top,
                width,
                height);

        _selectedPaths.Clear();

        foreach (string path in _selectionBasePaths)
        {
            _selectedPaths.Add(
                path);
        }

        foreach (KeyValuePair<string, Border> pair in _itemBorders)
        {
            double itemLeft =
                Canvas.GetLeft(
                    pair.Value);

            double itemTop =
                Canvas.GetTop(
                    pair.Value);

            Rect itemBounds =
                new(
                    double.IsNaN(
                        itemLeft)
                        ? 0
                        : itemLeft,
                    double.IsNaN(
                        itemTop)
                        ? 0
                        : itemTop,
                    pair.Value.ActualWidth > 0
                        ? pair.Value.ActualWidth
                        : pair.Value.Width,
                    pair.Value.ActualHeight > 0
                        ? pair.Value.ActualHeight
                        : pair.Value.Height);

            if (selectionBounds.IntersectsWith(
                    itemBounds))
            {
                _selectedPaths.Add(
                    pair.Key);
            }
        }

        UpdateSelectionVisuals();

        e.Handled =
            true;
    }

    private void Canvas_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_selectionInProgress ||
            e.ChangedButton !=
            MouseButton.Left)
        {
            return;
        }

        _selectionInProgress =
            false;

        if (_canvas.IsMouseCaptured)
        {
            _canvas.ReleaseMouseCapture();
        }

        RemoveSelectionRectangle();

        _log(
            $"Selection rectangle finished; Selected={_selectedPaths.Count}");

        e.Handled =
            true;
    }

    private void RemoveSelectionRectangle()
    {
        if (_selectionRectangle is null)
        {
            return;
        }

        _canvas.Children.Remove(
            _selectionRectangle);

        _selectionRectangle =
            null;
    }

    private void UpdateSelectionVisuals()
    {
        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        Brush hoverBackground =
            appearance.DockItemBackgroundBrush.Clone();

        hoverBackground.Opacity *=
            0.55;

        Brush hoverBorder =
            appearance.DockItemBorderBrush.Clone();

        hoverBorder.Opacity *=
            0.55;

        var gridCell =
            GetDesktopGridCellSize();

        double iconSize =
            GetIconSize();

        foreach (KeyValuePair<string, Border> pair in _itemBorders)
        {
            bool selected =
                _selectedPaths.Contains(
                    pair.Key);

            bool hovered =
                string.Equals(
                    _hoveredPath,
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase);

            pair.Value.Height =
                gridCell.Height;

            pair.Value.CornerRadius =
                selected ||
                hovered
                    ? new CornerRadius(
                        2)
                    : DockDialogTheme.StandardCornerRadius;

            pair.Value.Background =
                selected
                    ? appearance.DockItemBackgroundBrush.Clone()
                    : hovered
                        ? hoverBackground.Clone()
                        : Brushes.Transparent;

            pair.Value.BorderBrush =
                selected
                    ? appearance.DockItemBorderBrush.Clone()
                    : hovered
                        ? hoverBorder.Clone()
                        : Brushes.Transparent;

            Panel.SetZIndex(
                pair.Value,
                selected
                    ? 1000
                    : hovered
                        ? 500
                        : 0);

            if (_itemLabels.TryGetValue(
                    pair.Key,
                    out TextBlock? label))
            {
                label.TextTrimming =
                    selected
                        ? TextTrimming.None
                        : TextTrimming.CharacterEllipsis;

                label.MaxHeight =
                    selected
                        ? double.PositiveInfinity
                        : GetCollapsedLabelMaxHeight(
                            gridCell.Height,
                            iconSize);

                label.Background =
                    Brushes.Transparent;

                if (selected)
                {
                    label.Measure(
                        new Size(
                            label.Width,
                            double.PositiveInfinity));

                    double labelTop =
                        Canvas.GetTop(
                            label);

                    if (double.IsNaN(
                            labelTop))
                    {
                        labelTop =
                            iconSize +
                            3;
                    }

                    pair.Value.Height =
                        Math.Max(
                            gridCell.Height,
                            pair.Value.Padding.Top +
                            labelTop +
                            label.DesiredSize.Height +
                            pair.Value.Padding.Bottom);

                    _log(
                        $"Selected label visual; Item={Path.GetFileName(pair.Key)}; Cell={gridCell.Width:0.0}x{gridCell.Height:0.0}; IconDip={iconSize:0.0}; LabelWidth={label.Width:0.0}; LabelDesiredHeight={label.DesiredSize.Height:0.0}; SelectionHeight={pair.Value.Height:0.0}; MaxHeight={label.MaxHeight}");
                }
            }
        }
    }

    private ContextMenu CreateItemContextMenu(
        string path,
        bool isGroup)
    {
        ContextMenu menu =
            new();

        ApplyContextMenuAppearance(
            menu);

        MenuItem openItem =
            new()
            {
                Header =
                    _localize(
                        isGroup
                            ? "Widget.DesktopOverlay.Context.OpenGroup"
                            : "Widget.DesktopOverlay.Context.Open")
            };

        openItem.Click +=
            (_, _) =>
            {
                if (isGroup &&
                    _itemBorders.TryGetValue(
                        path,
                        out Border? border))
                {
                    OpenGroupPopup(
                        border,
                        path);
                }
                else
                {
                    OpenPath(
                        path);
                }
            };

        menu.Items.Add(
            openItem);

        MenuItem renameItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Rename")
            };

        renameItem.Click +=
            (_, _) =>
                RenamePath(
                    path);

        menu.Items.Add(
            renameItem);

        if (isGroup)
        {
            MenuItem ungroupItem =
                new()
                {
                    Header =
                        _localize(
                            "Widget.DesktopOverlay.Context.Ungroup")
                };

            ungroupItem.Click +=
                (_, _) =>
                    Ungroup(
                        path);

            menu.Items.Add(
                ungroupItem);
        }

        MenuItem openFolderItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.OpenFolder")
            };

        openFolderItem.Click +=
            (_, _) =>
                OpenContainingFolder(
                    path);

        menu.Items.Add(
            openFolderItem);

        if (!isGroup)
        {
            MenuItem propertiesItem =
                new()
                {
                    Header =
                        _localize(
                            "Widget.DesktopOverlay.Context.Properties")
                };

            propertiesItem.Click +=
                (_, _) =>
                    OpenProperties(
                        path);

            menu.Items.Add(
                propertiesItem);
        }

        menu.Items.Add(
            new Separator());

        MenuItem deleteItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Delete")
            };

        deleteItem.Click +=
            (_, _) =>
                DeletePathWithConfirmation(
                    path);

        menu.Items.Add(
            deleteItem);

        return menu;
    }

    private void ApplyContextMenuAppearance(
        ContextMenu menu)
    {
        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        menu.Background =
            appearance.DockBackgroundBrush.Clone();

        menu.Foreground =
            appearance.DockTextBrush.Clone();

        menu.BorderBrush =
            appearance.DockItemBorderBrush.Clone();
    }

    private void OpenGroupPopup(
        Border placementTarget,
        string groupPath)
    {
        _groupPopup?.IsOpen =
            false;

        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        StackPanel panel =
            new();

        TextBox nameBox =
            new()
            {
                Text =
                    Path.GetFileName(
                        groupPath),
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10),
                MinHeight =
                    DockDialogTheme.StandardControlHeight
            };

        nameBox.KeyDown +=
            (_, e) =>
            {
                if (e.Key ==
                    Key.Enter)
                {
                    string? renamed =
                        RenamePathCore(
                            groupPath,
                            nameBox.Text);

                    if (!string.IsNullOrWhiteSpace(
                            renamed))
                    {
                        groupPath =
                            renamed;
                    }

                    Keyboard.ClearFocus();
                    e.Handled =
                        true;
                }
            };

        nameBox.LostKeyboardFocus +=
            (_, _) =>
            {
                string? renamed =
                    RenamePathCore(
                        groupPath,
                        nameBox.Text);

                if (!string.IsNullOrWhiteSpace(
                        renamed))
                {
                    groupPath =
                        renamed;
                }
            };

        panel.Children.Add(
            nameBox);

        WrapPanel itemsPanel =
            new()
            {
                ItemWidth =
                    GetDesktopGridCellSize().Width,
                ItemHeight =
                    GetDesktopGridCellSize().Height,
                AllowDrop =
                    true
            };

        itemsPanel.Drop +=
            (_, e) =>
            {
                e.Handled =
                    true;

                if (e.Data.GetDataPresent(
                        InternalDragFormat) &&
                    e.Data.GetData(
                        InternalDragFormat) is
                        string[] paths)
                {
                    foreach (string sourcePath in paths)
                    {
                        if (File.Exists(
                                sourcePath) ||
                            Directory.Exists(
                                sourcePath))
                        {
                            MoveIntoDirectory(
                                sourcePath,
                                groupPath);
                        }
                    }

                    _groupPopup!.IsOpen =
                        false;

                    RefreshItems();
                }
                else if (e.Data.GetDataPresent(
                             DataFormats.FileDrop) &&
                         e.Data.GetData(
                             DataFormats.FileDrop) is
                             string[] externalPaths)
                {
                    IReadOnlyList<string> imported =
                        ImportExternalItems(
                            externalPaths,
                            GetExternalDropEffect(
                                e,
                                externalPaths));

                    foreach (string importedPath in imported)
                    {
                        MoveIntoDirectory(
                            importedPath,
                            groupPath);
                    }

                    _groupPopup!.IsOpen =
                        false;

                    RefreshItems();
                }
            };

        itemsPanel.DragOver +=
            (_, e) =>
            {
                if (e.Data.GetDataPresent(
                        InternalDragFormat) ||
                    e.Data.GetDataPresent(
                        DataFormats.FileDrop))
                {
                    e.Effects =
                        DragDropEffects.Move;

                    e.Handled =
                        true;
                }
            };

        foreach (string childPath in GetGroupChildren(
                     groupPath))
        {
            itemsPanel.Children.Add(
                CreateGroupChildItem(
                    childPath,
                    groupPath));
        }

        panel.Children.Add(
            itemsPanel);

        Border popupBorder =
            new()
            {
                Background =
                    DockDialogThemeService.CreateDialogSurfaceBrush(
                        appearance.DockBackgroundBrush,
                        Math.Clamp(
                            _settings.Opacity,
                            0.10,
                            1.00)),
                BorderBrush =
                    appearance.DockItemBorderBrush.Clone(),
                BorderThickness =
                    new Thickness(
                        1),
                CornerRadius =
                    new CornerRadius(
                        appearance.GlassCornerRadius),
                Padding =
                    new Thickness(
                        12),
                Child =
                    panel,
                MaxWidth =
                    520,
                MaxHeight =
                    440
            };

        if (Resources[typeof(TextBox)] is Style textBoxStyle)
        {
            nameBox.Style =
                textBoxStyle;
        }

        _groupPopup =
            new Popup
            {
                PlacementTarget =
                    placementTarget,
                Placement =
                    PlacementMode.Right,
                AllowsTransparency =
                    true,
                StaysOpen =
                    false,
                Child =
                    popupBorder,
                IsOpen =
                    true
            };
    }

    private FrameworkElement CreateGroupChildItem(
        string childPath,
        string groupPath)
    {
        (double cellWidth, double cellHeight) =
            GetDesktopGridCellSize();

        TextBlock label =
            new()
            {
                Text =
                    GetDisplayName(
                        childPath),
                FontFamily =
                    SystemFonts.IconFontFamily,
                FontSize =
                    SystemFonts.IconFontSize,
                FontWeight =
                    SystemFonts.IconFontWeight,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                TextTrimming =
                    TextTrimming.CharacterEllipsis,
                Foreground =
                    CreateEffectiveAppearance().DockTextBrush
            };

        StackPanel content =
            new();

        content.Children.Add(
            CreateIconImage(
                childPath,
                GetIconSize()));

        content.Children.Add(
            label);

        Border border =
            new()
            {
                Width =
                    cellWidth,
                Height =
                    cellHeight,
                Padding =
                    new Thickness(
                        4),
                Background =
                    Brushes.Transparent,
                CornerRadius =
                    DockDialogTheme.StandardCornerRadius,
                Child =
                    content,
                Tag =
                    childPath
            };

        Point dragStart =
            default;

        border.PreviewMouseLeftButtonDown +=
            (_, e) =>
                dragStart =
                    e.GetPosition(
                        border);

        border.PreviewMouseMove +=
            (_, e) =>
            {
                if (e.LeftButton !=
                    MouseButtonState.Pressed)
                {
                    return;
                }

                Point current =
                    e.GetPosition(
                        border);

                if (Math.Abs(
                        current.X -
                        dragStart.X) <
                    SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(
                        current.Y -
                        dragStart.Y) <
                    SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }

                DataObject data =
                    new();

                data.SetData(
                    InternalDragFormat,
                    new[]
                    {
                        childPath
                    });

                data.SetData(
                    DataFormats.FileDrop,
                    new[]
                    {
                        childPath
                    });

                _groupPopup!.IsOpen =
                    false;

                DragDrop.DoDragDrop(
                    border,
                    data,
                    DragDropEffects.Copy |
                    DragDropEffects.Move |
                    DragDropEffects.Link);

                DissolveGroupIfNeeded(
                    groupPath);

                RefreshItems();
            };

        border.MouseLeftButtonDown +=
            (_, e) =>
            {
                if (e.ClickCount !=
                    2)
                {
                    return;
                }

                e.Handled =
                    true;

                OpenPath(
                    childPath);
            };

        border.ContextMenu =
            CreateGroupChildContextMenu(
                childPath,
                groupPath);

        return border;
    }

    private ContextMenu CreateGroupChildContextMenu(
        string childPath,
        string groupPath)
    {
        ContextMenu menu =
            new();

        ApplyContextMenuAppearance(
            menu);

        MenuItem openItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Open")
            };

        openItem.Click +=
            (_, _) =>
                OpenPath(
                    childPath);

        menu.Items.Add(
            openItem);

        MenuItem renameItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Rename")
            };

        renameItem.Click +=
            (_, _) =>
            {
                RenamePath(
                    childPath);

                _groupPopup!.IsOpen =
                    false;
            };

        menu.Items.Add(
            renameItem);

        MenuItem removeItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.RemoveFromGroup")
            };

        removeItem.Click +=
            (_, _) =>
            {
                MoveIntoDirectory(
                    childPath,
                    _storageDirectory);

                DissolveGroupIfNeeded(
                    groupPath);

                _groupPopup!.IsOpen =
                    false;

                RefreshItems();
            };

        menu.Items.Add(
            removeItem);

        MenuItem propertiesItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Properties")
            };

        propertiesItem.Click +=
            (_, _) =>
                OpenProperties(
                    childPath);

        menu.Items.Add(
            propertiesItem);

        menu.Items.Add(
            new Separator());

        MenuItem deleteItem =
            new()
            {
                Header =
                    _localize(
                        "Widget.DesktopOverlay.Context.Delete")
            };

        deleteItem.Click +=
            (_, _) =>
            {
                DeletePathWithConfirmation(
                    childPath);

                DissolveGroupIfNeeded(
                    groupPath);

                _groupPopup!.IsOpen =
                    false;

                RefreshItems();
            };

        menu.Items.Add(
            deleteItem);

        return menu;
    }

    private void RenamePath(
        string path)
    {
        if (!File.Exists(
                path) &&
            !Directory.Exists(
                path))
        {
            return;
        }

        if (_itemLabels.TryGetValue(
                path,
                out TextBlock? label) &&
            VisualTreeHelper.GetParent(
                label) is Canvas itemCanvas)
        {
            BeginInlineRename(
                path,
                label,
                itemCanvas,
                null);

            return;
        }

        DesktopOverlayRenameWindow dialog =
            new(
                GetDisplayName(
                    path),
                CreateEffectiveAppearance(),
                _localize)
            {
                Owner =
                    this
            };

        if (dialog.ShowDialog() !=
            true)
        {
            return;
        }

        RenamePathCore(
            path,
            dialog.ResultName);
    }

    private void BeginInlineRename(
        string path,
        TextBlock label,
        Canvas itemCanvas,
        string? initialText)
    {
        if (_inlineRenameTextBox is not null)
        {
            if (string.Equals(
                    _inlineRenamePath,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                _inlineRenameTextBox.Focus();

                return;
            }

            CancelInlineRename();
        }

        double labelTop =
            Canvas.GetTop(
                label);

        if (double.IsNaN(
                labelTop))
        {
            labelTop =
                GetIconSize() +
                3;
        }

        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance();

        TextBox editor =
            new()
            {
                Text =
                    initialText ??
                    label.Text,
                Width =
                    label.Width,
                FontFamily =
                    label.FontFamily,
                FontSize =
                    label.FontSize,
                FontWeight =
                    label.FontWeight,
                FontStyle =
                    label.FontStyle,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                AcceptsReturn =
                    false,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                Foreground =
                    label.Foreground.Clone(),
                Background =
                    appearance.DockBackgroundBrush.Clone(),
                BorderBrush =
                    appearance.DockItemBorderBrush.Clone(),
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        1,
                        0,
                        1,
                        0),
                VerticalContentAlignment =
                    VerticalAlignment.Center
            };

        editor.PreviewKeyDown +=
            InlineRenameTextBox_PreviewKeyDown;

        editor.LostKeyboardFocus +=
            InlineRenameTextBox_LostKeyboardFocus;

        Canvas.SetLeft(
            editor,
            0);

        Canvas.SetTop(
            editor,
            labelTop);

        Panel.SetZIndex(
            editor,
            2000);

        _inlineRenameTextBox =
            editor;

        _inlineRenamePath =
            path;

        _inlineRenameLabel =
            label;

        label.Visibility =
            Visibility.Hidden;

        itemCanvas.Children.Add(
            editor);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(
                () =>
                {
                    if (!ReferenceEquals(
                            _inlineRenameTextBox,
                            editor))
                    {
                        return;
                    }

                    editor.Focus();

                    string extension =
                        Directory.Exists(
                            path)
                            ? string.Empty
                            : Path.GetExtension(
                                path);

                    int selectionLength =
                        !string.IsNullOrWhiteSpace(
                            extension) &&
                        editor.Text.EndsWith(
                            extension,
                            StringComparison.OrdinalIgnoreCase)
                            ? Math.Max(
                                0,
                                editor.Text.Length -
                                extension.Length)
                            : editor.Text.Length;

                    editor.Select(
                        0,
                        selectionLength);
                }));
    }

    private void InlineRenameTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key ==
            Key.Enter)
        {
            e.Handled =
                true;

            CommitInlineRename();

            return;
        }

        if (e.Key ==
            Key.Escape)
        {
            e.Handled =
                true;

            CancelInlineRename();
        }
    }

    private void InlineRenameTextBox_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (!ReferenceEquals(
                sender,
                _inlineRenameTextBox))
        {
            return;
        }

        CommitInlineRename();
    }

    private void CommitInlineRename()
    {
        TextBox? editor =
            _inlineRenameTextBox;

        string? path =
            _inlineRenamePath;

        if (editor is null ||
            string.IsNullOrWhiteSpace(
                path))
        {
            return;
        }

        string requestedName =
            editor.Text;

        CancelInlineRename();

        string? renamedPath =
            RenamePathCore(
                path,
                requestedName);

        if (renamedPath is not null ||
            (!File.Exists(
                 path) &&
             !Directory.Exists(
                 path)) ||
            !_itemLabels.TryGetValue(
                path,
                out TextBlock? label) ||
            VisualTreeHelper.GetParent(
                label) is not Canvas itemCanvas)
        {
            return;
        }

        BeginInlineRename(
            path,
            label,
            itemCanvas,
            requestedName);
    }

    private void CancelInlineRename()
    {
        TextBox? editor =
            _inlineRenameTextBox;

        TextBlock? label =
            _inlineRenameLabel;

        _inlineRenameTextBox =
            null;

        _inlineRenamePath =
            null;

        _inlineRenameLabel =
            null;

        if (label is not null)
        {
            label.Visibility =
                Visibility.Visible;
        }

        if (editor is not null &&
            VisualTreeHelper.GetParent(
                editor) is Panel parent)
        {
            parent.Children.Remove(
                editor);
        }
    }

    private string? RenamePathCore(
        string path,
        string requestedName)
    {
        if (!File.Exists(
                path) &&
            !Directory.Exists(
                path))
        {
            return null;
        }

        string name =
            requestedName.Trim();

        if (string.IsNullOrWhiteSpace(
                name) ||
            name.IndexOfAny(
                Path.GetInvalidFileNameChars()) >=
            0)
        {
            return null;
        }

        string parent =
            Path.GetDirectoryName(
                path) ??
            _storageDirectory;

        bool isDirectory =
            Directory.Exists(
                path);

        string extension =
            isDirectory
                ? string.Empty
                : Path.GetExtension(
                    path);

        string oldFileName =
            Path.GetFileName(
                path);

        if (!isDirectory &&
            !name.EndsWith(
                extension,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                extension))
        {
            name +=
                extension;
        }

        string destination =
            Path.Combine(
                parent,
                name);

        if (PathsEqual(
                path,
                destination))
        {
            return path;
        }

        if (File.Exists(
                destination) ||
            Directory.Exists(
                destination))
        {
            return null;
        }

        string oldKey =
            IsRootPath(
                path)
                ? GetRelativeKey(
                    path)
                : string.Empty;

        if (isDirectory)
        {
            Directory.Move(
                path,
                destination);
        }
        else
        {
            File.Move(
                path,
                destination);
        }

        if (_selectedPaths.Remove(
                path))
        {
            _selectedPaths.Add(
                destination);
        }

        if (string.Equals(
                _hoveredPath,
                path,
                StringComparison.OrdinalIgnoreCase))
        {
            _hoveredPath =
                destination;
        }

        if (!string.IsNullOrWhiteSpace(
                oldKey) &&
            _settings.Positions.Remove(
                oldKey,
                out DesktopOverlayGridPosition? position))
        {
            _settings.Positions[GetRelativeKey(
                destination)] =
                position;

            SaveSettings();
        }

        RefreshItems();

        return destination;
    }

    private void DeleteSelectedPathsToRecycleBin()
    {
        string[] paths =
            _selectedPaths
                .Where(
                    path =>
                        File.Exists(
                            path) ||
                        Directory.Exists(
                            path))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (paths.Length ==
            0)
        {
            return;
        }

        nint sourceList =
            0;

        try
        {
            string nativeSourceList =
                string.Join(
                    '\0',
                    paths) +
                "\0\0";

            sourceList =
                Marshal.StringToCoTaskMemUni(
                    nativeSourceList);

            NativeShellFileOperation operation =
                new()
                {
                    Window =
                        new WindowInteropHelper(
                            this).Handle,
                    Function =
                        FoDelete,
                    From =
                        sourceList,
                    Flags =
                        FofAllowUndo |
                        FofWantNukeWarning
                };

            int result =
                SHFileOperation(
                    ref operation);

            if (result !=
                0 ||
                operation.AnyOperationsAborted)
            {
                _log(
                    $"Shell recycle operation not completed; Result={result}; Aborted={operation.AnyOperationsAborted}; Items={paths.Length}");

                return;
            }

            foreach (string path in paths)
            {
                _settings.Positions.Remove(
                    GetRelativeKey(
                        path));
            }

            _selectedPaths.Clear();

            SaveSettings();
            RefreshItems();

            _log(
                $"Shell recycle operation completed; Items={paths.Length}");
        }
        catch (Exception exception)
        {
            _log(
                $"Shell recycle operation failed; {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (sourceList !=
                0)
            {
                Marshal.FreeCoTaskMem(
                    sourceList);
            }
        }
    }

    private void DeletePathWithConfirmation(
        string path)
    {
        MessageBoxResult result =
            MessageBox.Show(
                this,
                string.Format(
                    _localize(
                        "Widget.DesktopOverlay.Delete.Confirm"),
                    GetDisplayName(
                        path)),
                _localize(
                    "Widget.DesktopOverlay.Delete.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        DeletePath(
            path);

        _settings.Positions.Remove(
            GetRelativeKey(
                path));

        SaveSettings();
        RefreshItems();
    }

    private static void DeletePath(
        string path)
    {
        if (File.Exists(
                path))
        {
            File.Delete(
                path);

            return;
        }

        if (Directory.Exists(
                path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }

    private void Ungroup(
        string groupPath)
    {
        if (!IsGroupDirectory(
                groupPath))
        {
            return;
        }

        foreach (string child in GetGroupChildren(
                     groupPath)
                     .ToArray())
        {
            MoveIntoDirectory(
                child,
                _storageDirectory);
        }

        TryDeleteGroupDirectory(
            groupPath);

        RefreshItems();
    }

    private void DissolveGroupIfNeeded(
        string groupPath)
    {
        if (!IsGroupDirectory(
                groupPath))
        {
            return;
        }

        string[] children =
            GetGroupChildren(
                    groupPath)
                .ToArray();

        if (children.Length > 1)
        {
            return;
        }

        if (children.Length == 1)
        {
            MoveIntoDirectory(
                children[0],
                _storageDirectory);
        }

        TryDeleteGroupDirectory(
            groupPath);
    }

    private static void TryDeleteGroupDirectory(
        string groupPath)
    {
        try
        {
            string marker =
                Path.Combine(
                    groupPath,
                    GroupMarkerFileName);

            if (File.Exists(
                    marker))
            {
                File.Delete(
                    marker);
            }

            if (Directory.Exists(
                    groupPath) &&
                Directory.GetFileSystemEntries(
                        groupPath)
                    .Length ==
                0)
            {
                Directory.Delete(
                    groupPath);
            }
        }
        catch
        {
        }
    }

    private IEnumerable<string> GetGroupChildren(
        string groupPath)
    {
        if (!Directory.Exists(
                groupPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFileSystemEntries(
                groupPath,
                "*",
                SearchOption.TopDirectoryOnly)
            .Where(
                path =>
                    !string.Equals(
                        Path.GetFileName(
                            path),
                        GroupMarkerFileName,
                        StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                GetDisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool IsGroupDirectory(
        string path)
    {
        return Directory.Exists(
                   path) &&
               File.Exists(
                   Path.Combine(
                       path,
                       GroupMarkerFileName));
    }

    private static void WriteGroupMarker(
        string groupPath)
    {
        string markerPath =
            Path.Combine(
                groupPath,
                GroupMarkerFileName);

        File.WriteAllText(
            markerPath,
            "{}");

        try
        {
            File.SetAttributes(
                markerPath,
                File.GetAttributes(
                    markerPath) |
                FileAttributes.Hidden);
        }
        catch
        {
        }
    }

    private string MoveIntoDirectory(
        string sourcePath,
        string destinationDirectory)
    {
        if (!File.Exists(
                sourcePath) &&
            !Directory.Exists(
                sourcePath))
        {
            return sourcePath;
        }

        string? oldGroup =
            Path.GetDirectoryName(
                sourcePath);

        string destination =
            GetUniqueDestinationPath(
                destinationDirectory,
                Path.GetFileName(
                    sourcePath));

        if (PathsEqual(
                sourcePath,
                destination))
        {
            return sourcePath;
        }

        string oldKey =
            IsRootPath(
                sourcePath)
                ? GetRelativeKey(
                    sourcePath)
                : string.Empty;

        MovePath(
            sourcePath,
            destination);

        if (!string.IsNullOrWhiteSpace(
                oldKey))
        {
            _settings.Positions.Remove(
                oldKey);
        }

        if (oldGroup is not null &&
            IsGroupDirectory(
                oldGroup) &&
            !PathsEqual(
                oldGroup,
                destinationDirectory))
        {
            DissolveGroupIfNeeded(
                oldGroup);
        }

        SaveSettings();

        return destination;
    }

    private static void MovePath(
        string sourcePath,
        string destinationPath)
    {
        try
        {
            if (Directory.Exists(
                    sourcePath))
            {
                Directory.Move(
                    sourcePath,
                    destinationPath);
            }
            else
            {
                File.Move(
                    sourcePath,
                    destinationPath);
            }
        }
        catch (IOException)
        {
            CopyPath(
                sourcePath,
                destinationPath);

            DeletePath(
                sourcePath);
        }
    }

    private static void CopyPath(
        string sourcePath,
        string destinationPath)
    {
        if (Directory.Exists(
                sourcePath))
        {
            CopyDirectory(
                sourcePath,
                destinationPath);

            return;
        }

        File.Copy(
            sourcePath,
            destinationPath,
            overwrite: false);
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        Directory.CreateDirectory(
            destinationDirectory);

        foreach (string file in Directory.GetFiles(
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
                overwrite: false);
        }

        foreach (string directory in Directory.GetDirectories(
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

    private static string GetUniqueDestinationPath(
        string directory,
        string fileName)
    {
        string candidate =
            Path.Combine(
                directory,
                fileName);

        if (!File.Exists(
                candidate) &&
            !Directory.Exists(
                candidate))
        {
            return candidate;
        }

        string baseName =
            Path.GetFileNameWithoutExtension(
                fileName);

        string extension =
            Path.GetExtension(
                fileName);

        int index =
            2;

        while (true)
        {
            candidate =
                Path.Combine(
                    directory,
                    $"{baseName} ({index}){extension}");

            if (!File.Exists(
                    candidate) &&
                !Directory.Exists(
                    candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string CreateUniqueDirectory(
        string parent,
        string baseName)
    {
        string candidate =
            Path.Combine(
                parent,
                baseName);

        int index =
            2;

        while (Directory.Exists(
                   candidate) ||
               File.Exists(
                   candidate))
        {
            candidate =
                Path.Combine(
                    parent,
                    $"{baseName} {index}");

            index++;
        }

        Directory.CreateDirectory(
            candidate);

        return candidate;
    }

    private void OpenPath(
        string path)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(
                    path)
                {
                    UseShellExecute =
                        true
                });
        }
        catch
        {
        }
    }

    private void OpenContainingFolder(
        string path)
    {
        try
        {
            string target =
                Directory.Exists(
                    path)
                    ? path
                    : Path.GetDirectoryName(
                          path) ??
                      _storageDirectory;

            Process.Start(
                new ProcessStartInfo(
                    "explorer.exe",
                    $"\"{target}\"")
                {
                    UseShellExecute =
                        true
                });
        }
        catch
        {
        }
    }

    private static void OpenProperties(
        string path)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(
                    path)
                {
                    Verb =
                        "properties",
                    UseShellExecute =
                        true
                });
        }
        catch
        {
        }
    }

    private string GetDisplayName(
        string path)
    {
        if (IsGroupDirectory(
                path))
        {
            return Path.GetFileName(
                path);
        }

        return DesktopOverlayShellIcon.GetDisplayName(
                   path) ??
               Path.GetFileName(
                   path);
    }

    private static string GetSortType(
        string path)
    {
        if (Directory.Exists(
                path))
        {
            return string.Empty;
        }

        return Path.GetExtension(
            path);
    }

    private static DateTime GetLastWriteTime(
        string path)
    {
        try
        {
            return File.GetLastWriteTime(
                path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private string GetRelativeKey(
        string path)
    {
        return Path.GetRelativePath(
                _storageDirectory,
                path)
            .Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar);
    }

    private bool IsRootPath(
        string path)
    {
        string? parent =
            Path.GetDirectoryName(
                path);

        return parent is not null &&
               PathsEqual(
                   parent,
                   _storageDirectory);
    }

    private bool IsPathInsideStorage(
        string path)
    {
        try
        {
            string root =
                Path.GetFullPath(
                    _storageDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            string candidate =
                Path.GetFullPath(
                    path);

            return candidate.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(
                    left)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                Path.GetFullPath(
                    right)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private void CleanupPositions(
        HashSet<string> currentRootPaths)
    {
        HashSet<string> keys =
            currentRootPaths
                .Select(
                    GetRelativeKey)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        string[] stale =
            _settings.Positions.Keys
                .Where(
                    key =>
                        !keys.Contains(
                            key))
                .ToArray();

        if (stale.Length == 0)
        {
            return;
        }

        foreach (string key in stale)
        {
            _settings.Positions.Remove(
                key);
        }

        SaveSettings();
    }

    private void SaveSettings()
    {
        _saveSettings(
            _settings.Clone());
    }

    private void DesktopOverlayWindow_Closed(
        object? sender,
        EventArgs e)
    {
        SaveBoundsTimer_Tick(
            null,
            EventArgs.Empty);

        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        if (_canvas.IsMouseCaptured)
        {
            _canvas.ReleaseMouseCapture();
        }

        RemoveSelectionRectangle();
        RemoveDragVisual();

        _groupPopup?.SetCurrentValue(
            Popup.IsOpenProperty,
            false);

        _refreshTimer.Stop();
        _saveBoundsTimer.Stop();
        _resizeRefreshTimer.Stop();

        _watcher.EnableRaisingEvents =
            false;

        _watcher.Dispose();

        _unsubscribeLanguageChanged?.Invoke(
            LanguageChanged);

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(
                WndProc);

            _hwndSource =
                null;
        }

        _nativeBackdropHost.Dispose();
    }
}
