using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;


using Application = System.Windows.Application;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MenuItem = System.Windows.Controls.MenuItem;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;


namespace GlueDock;

public partial class MainWindow : Window
{
    private const string InternalDragFormat = "GlueDock.InternalDockItem";

    private const double MinimumLength = 96;
    private const double BaseExpandedThickness = 88;
    private const double ItemSlotLength = 64;
    private const double DockPaddingLength = 20;

    private const int GwlExStyle = -20;
    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;

    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeTransientWindow = 3;

    private const uint MonitorDefaultToNearest = 2;

    private readonly SettingsStore _settingsStore = new();
    private readonly DockItemStore _dockItemStore = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly NativeBackdropHost _nativeBackdropHost;

    private DockSettings _settings = new();
    private nint _windowHandle;
    private bool _isExpanded;
    private bool _isWindowDragging;
    private bool _isExternalDragActive;
    private bool _suppressNextItemClick;

    private Point _itemMouseDownPoint;
    private DockItem? _itemMouseDownItem;
    private DockItem? _activeInternalDragItem;
    private DockItem? _externalDropPlaceholder;
    private DockItem? _externalDropSubmenuTarget;
    private Border? _externalDropSubmenuHighlightBorder;
    private Point _dragPointerOffset;
    private int _lastInternalDragIndex = -1;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private Task<GitHubUpdateResult>? _updateCheckTask;
    private SubDockWindow? _openSubDock;
    private DockItem? _hoverSubmenuItem;
    private FrameworkElement? _hoverSubmenuAnchor;
    private DockItem? _pinnedSubmenuItem;
    private DispatcherTimer? _submenuHoverCloseTimer;
    private int _openContextMenuCount;
    private DispatcherTimer? _externalDragLeaveTimer;
    private bool _closingSubDockForRootCollapse;
    private WinForms.NotifyIcon? _notifyIcon;
    private bool _collapseAnimationRunning;
    private bool _syncBackdropOpacityWithDockChrome;
    private bool _slideExpandPreparing;
    private string _backdropOpacitySyncPhase = string.Empty;
    private int _backdropOpacitySyncFrame;
    private bool _initialBackdropReady;
    private bool _isSettingsPreviewApply;
    private Orientation? _itemsOrientation;
    private double _itemsPanelItemWidth = double.NaN;
    private int _itemsPanelMaxColumns = -1;
    private readonly DispatcherTimer _zoomHoverUnlockTimer;
    private readonly DispatcherTimer _expandHoverStabilizeTimer;
    private readonly Dictionary<IGlueDockWidget, DockItem> _widgetSourceItems =
        new();
    private readonly Dictionary<IGlueDockWidget, DockItem> _widgetCompanionItems =
        new();
    private DispatcherTimer? _glassGradientDebugTimer;
    private Stopwatch? _glassGradientDebugStopwatch;
    private LinearGradientBrush? _glassGradientDebugBrush;
    private long _glassGradientDebugLastElapsedMilliseconds;
    private double? _glassGradientDebugLastAngleDegrees;
    private double? _glassGradientDebugLastVisualAngleDegrees;
    private double _glassGradientDebugDurationMilliseconds;
    private double[] _glassGradientDebugKeyFrameProgresses = [];
    private int _glassGradientDebugSequence;
    private long _glassGradientDebugLastRenderElapsedMilliseconds;
    private double? _glassGradientDebugLastRenderVisualAngleDegrees;
    private int _glassGradientDebugRenderFrameIndex;
    private string? _glassGradientAnimationSignature;
    private string? _glassFlameAnimationSignature;
    private string? _glassStarAnimationSignature;
    private bool _naturalFireRenderingActive;
    private WriteableBitmap? _naturalFireBitmap;
    private byte[]? _naturalFireHeat;
    private byte[]? _naturalFireNextHeat;
    private int[]? _naturalFirePixels;
    private int[]? _naturalFirePalette;
    private int _naturalFireGridWidth;
    private int _naturalFireGridHeight;
    private int _naturalFireFrameIndex;
    private DateTime _naturalFireLastFrameUtc;
    private DateTime _naturalFireLastDebugUtc;

    public ObservableCollection<DockItem> DockItems { get; } = [];

    public MainWindow()
    {
        InitializeComponent();

        _nativeBackdropHost =
            new NativeBackdropHost(
                this);

        AddHandler(
            ContextMenuService.ContextMenuOpeningEvent,
            new ContextMenuEventHandler(
                MainWindow_ContextMenuOpening),
            true);

        AddHandler(
            ContextMenuService.ContextMenuClosingEvent,
            new ContextMenuEventHandler(
                MainWindow_ContextMenuClosing),
            true);

        AddHandler(
            ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler(
                MainWindow_ToolTipOpening),
            true);

        DataContext = this;

        DockItems.CollectionChanged +=
            DockItems_CollectionChanged;

        _collapseTimer = new DispatcherTimer();
        _collapseTimer.Tick += CollapseTimer_Tick;

        _zoomHoverUnlockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _zoomHoverUnlockTimer.Tick += ZoomHoverUnlockTimer_Tick;

        _expandHoverStabilizeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _expandHoverStabilizeTimer.Tick += ExpandHoverStabilizeTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        ContentRendered += MainWindow_ContentRendered;

        DockGlassFlames.SizeChanged +=
            DockGlassFlames_SizeChanged;

        if (FindName("DockGlassStars") is Canvas dockGlassStars)
        {
            dockGlassStars.SizeChanged +=
                DockGlassStars_SizeChanged;
        }

        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;

        PreviewMouseLeftButtonDown += MainWindow_PreviewMouseLeftButtonDown;

        DragEnter += MainWindow_DragEnter;
        DragOver += MainWindow_DragOver;
        DragLeave += MainWindow_DragLeave;
        Drop += MainWindow_Drop;
        Closed += MainWindow_Closed;
    }

    private void DockItems_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        List<DockItem> addedItems =
            e.NewItems?
                .OfType<DockItem>()
                .Where(
                    item =>
                        !item.IsRuntimeOnly)
                .ToList() ??
            new List<DockItem>();

        List<DockItem> removedItems =
            e.OldItems?
                .OfType<DockItem>()
                .Where(
                    item =>
                        !item.IsRuntimeOnly)
                .ToList() ??
            new List<DockItem>();

        if (addedItems.Count == 0 &&
            removedItems.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                foreach (DockItem item in removedItems)
                {
                    DetachWidgetCompanion(
                        item);
                }

                foreach (DockItem item in addedItems)
                {
                    if (DockItems.Contains(
                            item))
                    {
                        AttachWidgetCompanion(
                            item);
                    }
                }
            },
            DispatcherPriority.Background);
    }

    private void AttachWidgetCompanion(
        DockItem item)
    {
        IGlueDockWidget? widget =
            item.WidgetInstance;

        if (widget is null ||
            _widgetSourceItems.ContainsKey(
                widget))
        {
            return;
        }

        _widgetSourceItems[widget] =
            item;

        widget.CompanionViewChanged +=
            Widget_CompanionViewChanged;

        UpdateWidgetCompanion(
            widget);
    }

    private void DetachWidgetCompanion(
        DockItem item)
    {
        IGlueDockWidget? widget =
            item.WidgetInstance;

        if (widget is null ||
            !_widgetSourceItems.Remove(
                widget))
        {
            return;
        }

        widget.CompanionViewChanged -=
            Widget_CompanionViewChanged;

        if (widget.ShowCompanionAsSubDock &&
            ReferenceEquals(
                _openSubDock?.Tag,
                item))
        {
            _openSubDock?.Close();
            widget.CloseCompanion();
        }

        if (_widgetCompanionItems.Remove(
                widget,
                out DockItem? companion))
        {
            DockItems.Remove(
                companion);
        }
    }

    private void Widget_CompanionViewChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is not IGlueDockWidget widget)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
                UpdateWidgetCompanion(
                    widget),
            DispatcherPriority.Background);
    }

    private void UpdateWidgetCompanion(
        IGlueDockWidget widget)
    {
        if (!_widgetSourceItems.TryGetValue(
                widget,
                out DockItem? sourceItem))
        {
            return;
        }

        sourceItem.NotifyWidgetStateChanged();

        if (widget.ShowCompanionAsSubDock)
        {
            UpdateWidgetSubDockCompanion(
                widget,
                sourceItem);

            return;
        }

        bool currentlyVisible =
            _widgetCompanionItems.TryGetValue(
                widget,
                out DockItem? companion);

        if (!widget.HasCompanionView)
        {
            if (currentlyVisible &&
                companion is not null)
            {
                _widgetCompanionItems.Remove(
                    widget);

                DockItems.Remove(
                    companion);

                UpdateItemsOrientation();
                UpdateWindowBoundsPreservingHorizontalLeft();
                UpdateItemLabelVisibility();

                DebugLog.Write(
                    "Widget",
                    $"Companion removed; Item={sourceItem.DisplayName}; Id={sourceItem.Id}");
            }

            return;
        }

        if (currentlyVisible &&
            companion is not null)
        {
            companion.SlotSpan =
                Math.Clamp(
                    widget.CompanionSlotSpan,
                    1,
                    4);

            UpdateItemsOrientation();
            UpdateWindowBoundsPreservingHorizontalLeft();
            return;
        }

        FrameworkElement? companionView =
            widget.CreateCompanionView();

        if (companionView is null)
        {
            return;
        }

        DockItem runtimeItem =
            new()
            {
                DisplayName =
                    sourceItem.DisplayName +
                    " timer",
                IsRuntimeOnly = true,
                SlotSpan =
                    Math.Clamp(
                        widget.CompanionSlotSpan,
                        1,
                        4),
                WidgetView =
                    companionView
            };

        _widgetCompanionItems[widget] =
            runtimeItem;

        int sourceIndex =
            DockItems.IndexOf(
                sourceItem);

        if (sourceIndex >= 0)
        {
            DockItems.Insert(
                sourceIndex + 1,
                runtimeItem);
        }
        else
        {
            DockItems.Add(
                runtimeItem);
        }

        UpdateItemsOrientation();
        UpdateWindowBoundsPreservingHorizontalLeft();
        UpdateItemLabelVisibility();

        DebugLog.Write(
            "Widget",
            $"Companion added; Item={sourceItem.DisplayName}; Id={sourceItem.Id}; DockItems={DockItems.Count}");
    }

    private void UpdateWidgetSubDockCompanion(
        IGlueDockWidget widget,
        DockItem sourceItem)
    {
        if (!widget.HasCompanionView)
        {
            if (_openSubDock?.IsVisible == true &&
                ReferenceEquals(
                    _openSubDock.Tag,
                    sourceItem))
            {
                _openSubDock.CloseAnimated();
            }

            return;
        }

        if (_openSubDock?.IsVisible == true &&
            ReferenceEquals(
                _openSubDock.Tag,
                sourceItem))
        {
            _collapseTimer.Stop();
            return;
        }

        FrameworkElement? companionView =
            widget.CreateCompanionView();

        if (companionView is null)
        {
            return;
        }

        FrameworkElement? anchor =
            sourceItem.WidgetView;

        if (anchor is null ||
            !anchor.IsVisible)
        {
            anchor =
                DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    sourceItem) as FrameworkElement;
        }

        if (anchor is null)
        {
            return;
        }

        _collapseTimer.Stop();
        _openSubDock?.Close();

        System.Windows.Rect anchorRect =
            GetScreenRect(
                anchor);

        DpiScale anchorDpi =
            VisualTreeHelper.GetDpi(
                this);

        SubDockWindow companionWindow =
            new(
                companionView,
                _settings,
                _dockItemStore,
                SaveSettings,
                MoveItemToSubmenu,
                CanMoveItemToSubmenu,
                _settings.Edge)
            {
                Tag =
                    sourceItem
            };

        _openSubDock =
            companionWindow;

        companionWindow.InteractionStateChanged +=
            OpenSubDock_InteractionStateChanged;

        companionWindow.ExternalDragEnded +=
            OpenSubDock_ExternalDragEnded;

        companionWindow.Closed +=
            (_, _) =>
            {
                companionWindow.InteractionStateChanged -=
                    OpenSubDock_InteractionStateChanged;

                companionWindow.ExternalDragEnded -=
                    OpenSubDock_ExternalDragEnded;

                if (ReferenceEquals(
                        _openSubDock,
                        companionWindow))
                {
                    _openSubDock =
                        null;
                }

                if (widget.HasCompanionView)
                {
                    widget.CloseCompanion();
                }

                if (!_closingSubDockForRootCollapse &&
                    !_settings.CollapseDisabled &&
                    _openContextMenuCount == 0 &&
                    !IsMouseOver &&
                    !_collapseTimer.IsEnabled)
                {
                    RestartCollapseTimer();
                }
            };

        companionWindow.PositionNextTo(
            anchorRect,
            anchorDpi);

        companionWindow.Show();

        DebugLog.Write(
            "Widget",
            $"Companion subdock shown; Item={sourceItem.DisplayName}; Id={sourceItem.Id}; Left={companionWindow.Left:0.0}; Top={companionWindow.Top:0.0}; Width={companionWindow.Width:0.0}; Height={companionWindow.Height:0.0}");
    }

    private void UpdateWindowBoundsPreservingHorizontalLeft()
    {
        UpdateWindowBounds(
            preserveHorizontalLeft: true);

        DebugLog.Write(
            "RootBounds",
            $"Companion horizontal left preserved; Left={Left:0.0}; Width={Width:0.0}; Edge={_settings.Edge}");
    }

    private void MainWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        _windowHandle =
            new WindowInteropHelper(this).Handle;

        ApplyNativeWindowStyle();
    }

    private void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _settings = _settingsStore.Load();

        System.Diagnostics.Debug.WriteLine(
            $"GlueDock StartupDiagnostics: DebugLoggingEnabled={_settings.DebugLoggingEnabled}; LogFile={DebugLog.ActiveLogPath}; BaseDirectory={AppContext.BaseDirectory}; CurrentDirectory={Environment.CurrentDirectory}");

        DebugLog.SetMaximumActiveLogFileSizeMegabytes(
            _settings.DebugLogMaxSizeMegabytes);

        DebugLog.SetEnabled(
            _settings.DebugLoggingEnabled);

        DebugLog.Write(
            "Root",
            $"Loaded; Edge={_settings.Edge}; Ratio={_settings.EdgePositionRatio:0.000}; CollapseDisabled={_settings.CollapseDisabled}; Delay={_settings.CollapseDelayMilliseconds}; Labels={_settings.ShowItemLabels}; Scale={_settings.DockScale:0.00}");

        App.Language.Load(
            _settings.LanguageCode);

        _updateCheckTask =
            GitHubUpdateService.CheckForUpdateAsync();

        _ = _updateCheckTask.ContinueWith(
            task =>
            {
                if (task.Status ==
                    TaskStatus.RanToCompletion)
                {
                    GitHubUpdateResult result =
                        task.Result;

                    DebugLog.Write(
                        "Update",
                        $"Startup check completed; Available={result.UpdateAvailable}; Latest={result.LatestVersion}; Error={result.ErrorKind}");
                }
            },
            TaskScheduler.Default);

        App.Language.LanguageChanged +=
            Language_LanguageChanged;

        ApplyLanguage();

        _collapseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.CollapseDelayMilliseconds));

        bool migratedItems = false;

        if (_settings.DockItems.Count > 0)
        {
            foreach (DockEntrySettings entry in _settings.DockItems)
            {
                DockItems.Add(CreateDockItem(entry));
            }
        }
        else
        {
            foreach (string path in _settings.Items.ToList())
            {
                if (!File.Exists(path) &&
                    !Directory.Exists(path))
                {
                    continue;
                }

                string managedPath = path;

                if (!_dockItemStore.IsManagedPath(path))
                {
                    try
                    {
                        managedPath = _dockItemStore.Import(path);
                        migratedItems = true;
                    }
                    catch
                    {
                        managedPath = path;
                    }
                }

                DockItems.Add(CreateDockItem(managedPath));
            }
        }

        if (migratedItems ||
            (_settings.DockItems.Count == 0 &&
             DockItems.Count > 0))
        {
            SaveSettings();
        }

        StartupManager.SetStartWithWindows(
            _settings.StartWithWindows);

        EnsureDockTopmost();

        InitializeTrayIcon();

        EnsureDockTopmost();
        UpdateItemsOrientation();
        ApplyAppearance();

        _settingsWindow?.ApplyAppearance(
            CreateWidgetAppearance());

        ApplyWidgetAppearance(
            DockItems);
        UpdateItemLabelVisibility();
        UpdateDockItemPreviews();
        ApplyRootDockTooltipSetting();
        _openSubDock?.ApplySettingsLive();

        if (_settings.CollapseDisabled)
        {
            Expand();
        }
        else
        {
            Collapse(immediate: true);
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                EnsureDockTopmost();
                UpdateWindowBounds();
            },
            DispatcherPriority.Loaded);
    }

    private void MainWindow_ContentRendered(
        object? sender,
        EventArgs e)
    {
        ContentRendered -=
            MainWindow_ContentRendered;

        Dispatcher.BeginInvoke(
            async () =>
            {
                EnsureDockTopmost();
                UpdateWindowBounds();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        650));

                _initialBackdropReady = true;

                _nativeBackdropHost.SetBlur(
                    _settings.BlurRadius);

                _nativeBackdropHost.Sync();

                DebugLog.Write(
                    "Backdrop",
                    $"Startup blur initialized after compositor settle delay; Blur={_settings.BlurRadius:0.##}; Width={Width:0.0}; Height={Height:0.0}");
            },
            DispatcherPriority.ContextIdle);
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            UpdateTrayMenuLanguage();
            return;
        }

        Drawing.Icon applicationIcon =
            Drawing.SystemIcons.Application;

        Stream? applicationIconStream =
            Application.GetResourceStream(
                new Uri(
                    "pack://application:,,,/Resources/GlueDock.ico",
                    UriKind.Absolute))
                ?.Stream;

        if (applicationIconStream is not null)
        {
            using (applicationIconStream)
            using (Drawing.Icon resourceIcon =
                   new(
                       applicationIconStream))
            {
                applicationIcon =
                    (Drawing.Icon)resourceIcon.Clone();
            }
        }

        _notifyIcon =
            new WinForms.NotifyIcon
            {
                Text = App.Language["App.Name"],
                Visible = true,
                Icon = applicationIcon
            };

        _notifyIcon.DoubleClick +=
            (_, _) =>
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        Show();
                        Activate();
                        Expand();
                    });
            };

        UpdateTrayMenuLanguage();
    }

    private void UpdateTrayMenuLanguage()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        WinForms.ContextMenuStrip? oldMenu =
            _notifyIcon.ContextMenuStrip;

        WinForms.ContextMenuStrip menu =
            new();

        WinForms.ToolStripMenuItem showItem =
            new(App.Language["Context.Show"]);

        showItem.Click +=
            (_, _) =>
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        Show();
                        Activate();
                        Expand();
                    });
            };

        WinForms.ToolStripMenuItem settingsItem =
            new(App.Language["Context.Settings"]);

        settingsItem.Click +=
            (_, _) =>
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        OpenSettings_Click(
                            this,
                            new RoutedEventArgs());
                    });
            };

        WinForms.ToolStripMenuItem aboutItem =
            new(App.Language["Context.About"]);

        aboutItem.Click +=
            (_, _) =>
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        About_Click(
                            this,
                            new RoutedEventArgs());
                    });
            };

        WinForms.ToolStripMenuItem exitItem =
            new(App.Language["Context.Exit"]);

        exitItem.Click +=
            (_, _) =>
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        Application.Current.Shutdown();
                    });
            };

        menu.Items.Add(showItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(
            new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip =
            menu;

        oldMenu?.Dispose();
    }

    private void Language_LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Invoke(
            () =>
            {
                ApplyLanguage();
                UpdateTrayMenuLanguage();
            });
    }

    private void ApplyLanguage()
    {
        Title =
            App.Language["App.Name"];

        NewSubmenuContextMenuItem.Header =
            App.Language["Submenu.New"];

        DisableAutohideContextMenuItem.Header =
            App.Language["Context.DisableAutohide"];

        SettingsContextMenuItem.Header =
            App.Language["Context.Settings"];

        AboutContextMenuItem.Header =
            App.Language["Context.About"];

        ExitContextMenuItem.Header =
            App.Language["Context.Exit"];

        if (ContextMenu is not null)
        {
            ContextMenu.Language =
                System.Windows.Markup.XmlLanguage.GetLanguage(
                    System.Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag);
        }
    }

    private void MainWindow_Closed(
        object? sender,
        EventArgs e)
    {
        DockItems.CollectionChanged -=
            DockItems_CollectionChanged;

        foreach (IGlueDockWidget widget in
                 _widgetSourceItems.Keys.ToList())
        {
            widget.CompanionViewChanged -=
                Widget_CompanionViewChanged;
        }

        _widgetSourceItems.Clear();
        _widgetCompanionItems.Clear();

        App.Language.LanguageChanged -=
            Language_LanguageChanged;

        DebugLog.Write(
            "Root",
            "Main window closed.");

        _openSubDock?.Close();
        _openSubDock = null;

        StopNaturalFireHeatField();

        DebugLog.Shutdown();

        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void MainWindow_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        DebugLog.Write(
            "Root",
            "MouseEnter");

        _collapseTimer.Stop();

        if (_settings.CollapseDisabled)
        {
            return;
        }

        if (_collapseAnimationRunning ||
            _zoomHoverUnlockTimer.IsEnabled)
        {
            return;
        }

        Expand();
    }

    private void ZoomHoverUnlockTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _zoomHoverUnlockTimer.Stop();

        if (!_settings.CollapseDisabled &&
            !_isWindowDragging &&
            !_isExternalDragActive &&
            IsMouseOver)
        {
            Expand();
        }
    }

    private void MainWindow_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        LogRootState(
            "MouseLeave");

        if (_expandHoverStabilizeTimer.IsEnabled)
        {
            _collapseTimer.Stop();

            DebugLog.Write(
                "Root",
                "Transient MouseLeave ignored while expand hover stabilizes.");

            return;
        }

        if (_settings.CollapseDisabled ||
            _isWindowDragging ||
            _isExternalDragActive ||
            _activeInternalDragItem is not null ||
            _openContextMenuCount > 0)
        {
            _collapseTimer.Stop();
            return;
        }

        if (_openSubDock?.IsPointerOverDockChain == true)
        {
            _collapseTimer.Stop();
            return;
        }

        RestartCollapseTimer();
    }

    private void ExpandHoverStabilizeTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _expandHoverStabilizeTimer.Stop();

        if (_settings.CollapseDisabled ||
            _isWindowDragging ||
            _isExternalDragActive ||
            _activeInternalDragItem is not null ||
            _openContextMenuCount > 0 ||
            IsMouseOver ||
            _openSubDock?.IsPointerOverDockChain == true)
        {
            return;
        }

        RestartCollapseTimer();
    }

    private void CollapseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        LogRootState(
            "Collapse timer tick");

        _collapseTimer.Stop();

        if (_settings.CollapseDisabled ||
            _isWindowDragging ||
            _isExternalDragActive ||
            _activeInternalDragItem is not null ||
            _openContextMenuCount > 0 ||
            IsMouseOver ||
            _openSubDock?.IsPointerOverDockChain == true)
        {
            return;
        }

        if (_openSubDock is not null)
        {
            DebugLog.Write(
                "SubDock",
                "Closing complete submenu chain before root collapse.");

            _closingSubDockForRootCollapse = true;

            try
            {
                _openSubDock.Close();
                _openSubDock = null;
            }
            finally
            {
                _closingSubDockForRootCollapse = false;
            }
        }

        _collapseTimer.Stop();

        Collapse(immediate: false);
    }

    private void RestartCollapseTimer()
    {
        _collapseTimer.Stop();
        _collapseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.CollapseDelayMilliseconds));
        _collapseTimer.Start();

        LogRootState(
            "Collapse timer restarted");
    }

    private void MainWindow_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (FindDockItem(
                e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (e.LeftButton !=
            MouseButtonState.Pressed)
        {
            return;
        }

        _collapseTimer.Stop();

        if (_openSubDock is not null)
        {
            _openSubDock.Close();
            _openSubDock = null;
        }

        _isWindowDragging = true;

        LogRootState(
            "Root drag started");

        try
        {
            DragMove();
        }
        catch
        {
        }
        finally
        {
            _isWindowDragging = false;
        }

        SnapToNearestEdge();

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();

                DebugLog.Write(
                    "Labels",
                    "Root labels refreshed after dock move.");
            },
            DispatcherPriority.Loaded);

        LogRootState(
            "Root drag finished");

        SaveSettings();

        e.Handled = true;
    }

    private void ApplyDockItemHoverEffect(
        FrameworkElement element)
    {
        if (element is not Border border)
        {
            return;
        }

        ResetDockItemHoverEffect(
            border);

        string hoverEffect =
            _settings.HoverEffect ?? "None";

        switch (hoverEffect)
        {
            case "Zoom":
            {
                border.RenderTransformOrigin =
                    new Point(
                        0.5,
                        0.5);

                ScaleTransform transform =
                    new(
                        1.0,
                        1.0);

                border.RenderTransform =
                    transform;

                DoubleAnimation animation =
                    new(
                        1.0,
                        1.12,
                        new Duration(
                            TimeSpan.FromMilliseconds(
                                120)))
                    {
                        EasingFunction =
                            new CubicEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };

                transform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    animation);

                transform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    animation);

                break;
            }

            case "Glow":
            {
                Color glowColor =
                    Colors.White;

                if (Resources["DockTextBrush"] is SolidColorBrush glowBrush)
                {
                    glowColor =
                        glowBrush.Color;
                }

                border.Effect =
                    new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color =
                            glowColor,
                        BlurRadius =
                            18,
                        ShadowDepth =
                            0,
                        Opacity =
                            0.85
                    };

                break;
            }

            case "Highlight":
            {
                Border? highlight =
                    FindVisualChild<Border>(
                        border,
                        "DockItemHighlight");

                if (highlight is not null)
                {
                    highlight.Background =
                        Resources["DockItemBackgroundBrush"] as System.Windows.Media.Brush ??
                        Brushes.Transparent;

                    highlight.BorderBrush =
                        Resources["DockItemBorderBrush"] as System.Windows.Media.Brush ??
                        Brushes.Transparent;
                }

                break;
            }
        }
    }

    private static void ResetDockItemHoverEffect(
        FrameworkElement element)
    {
        if (element is not Border border)
        {
            return;
        }

        if (border.RenderTransform is ScaleTransform transform)
        {
            transform.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                null);

            transform.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                null);
        }

        border.RenderTransform =
            Transform.Identity;

        border.Effect =
            null;

        border.Background =
            Brushes.Transparent;

        border.BorderBrush =
            Brushes.Transparent;

        Border? highlight =
            FindVisualChild<Border>(
                border,
                "DockItemHighlight");

        if (highlight is not null)
        {
            highlight.Background =
                Brushes.Transparent;

            highlight.BorderBrush =
                Brushes.Transparent;
        }
    }

    private void RefreshDockItemHoverEffects()
    {
        for (int index = 0;
             index < DockItemsControl.Items.Count;
             index++)
        {
            if (DockItemsControl.ItemContainerGenerator.ContainerFromIndex(index)
                is not DependencyObject container)
            {
                continue;
            }

            Border? border =
                FindVisualChild<Border>(
                    container,
                    "DockItemBorder");

            if (border is null)
            {
                continue;
            }

            ResetDockItemHoverEffect(
                border);

            if (border.IsMouseOver)
            {
                ApplyDockItemHoverEffect(
                    border);
            }
        }
    }

    private void DockItem_ToolTipOpening(
        object sender,
        ToolTipEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item ||
            !item.IsRuntimeOnly ||
            item.WidgetView?.ToolTip is null)
        {
            return;
        }

        element.ToolTip =
            item.WidgetView.ToolTip;
    }

    private void DockItem_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        if (item.IsRuntimeOnly &&
            item.WidgetView?.ToolTip is not null)
        {
            element.ToolTip =
                item.WidgetView.ToolTip;
        }

        ApplyToolTipSettingToVisualTree(
            element,
            _settings.ShowRootDockTooltips);

        if (!item.DisableDefaultHoverEffect)
        {
            ApplyDockItemHoverEffect(
                element);
        }

        if (_isWindowDragging ||
            !item.IsSubmenu ||
            _settings.SubdockOpenOnClickOnly)
        {
            return;
        }

        if (_pinnedSubmenuItem is not null &&
            !ReferenceEquals(
                _pinnedSubmenuItem,
                item))
        {
            return;
        }

        StopSubmenuHoverCloseTimer();

        _hoverSubmenuItem = item;
        _hoverSubmenuAnchor = element;

        OpenSubmenu(
            item,
            element);
    }

    private void DockItem_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        if (!item.DisableDefaultHoverEffect)
        {
            ResetDockItemHoverEffect(
                element);
        }

        if (!item.IsSubmenu ||
            !ReferenceEquals(
                _hoverSubmenuItem,
                item) ||
            ReferenceEquals(
                _pinnedSubmenuItem,
                item))
        {
            return;
        }

        StartSubmenuHoverCloseTimer();
    }

    private void StartSubmenuHoverCloseTimer()
    {
        _submenuHoverCloseTimer ??=
            new DispatcherTimer();

        _submenuHoverCloseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.SubdockCollapseDelayMilliseconds));

        _submenuHoverCloseTimer.Tick -=
            SubmenuHoverCloseTimer_Tick;

        _submenuHoverCloseTimer.Tick +=
            SubmenuHoverCloseTimer_Tick;

        _submenuHoverCloseTimer.Stop();
        _submenuHoverCloseTimer.Start();
    }

    private void StopSubmenuHoverCloseTimer()
    {
        _submenuHoverCloseTimer?.Stop();
    }

    private void SubmenuHoverCloseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        StopSubmenuHoverCloseTimer();

        if (_pinnedSubmenuItem is not null ||
            _isExternalDragActive ||
            _activeInternalDragItem is not null ||
            _hoverSubmenuAnchor?.IsMouseOver == true ||
            _openSubDock?.IsPointerOverDockChain == true)
        {
            return;
        }

        if (_openSubDock?.IsVisible == true &&
            _hoverSubmenuItem is not null &&
            ReferenceEquals(
                _openSubDock.Tag,
                _hoverSubmenuItem))
        {
            DebugLog.Write(
                "SubDock",
                $"Closing hover-opened submenu; Name={_hoverSubmenuItem.DisplayName}");

            _openSubDock.CloseAnimated();
        }

        _hoverSubmenuItem = null;
        _hoverSubmenuAnchor = null;
    }

    private void DockItem_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        if (item.IsRuntimeOnly)
        {
            return;
        }

        _itemMouseDownItem = item;
        _itemMouseDownPoint =
            e.GetPosition(this);

        _dragPointerOffset =
            e.GetPosition(element);

        _suppressNextItemClick = false;
        e.Handled = true;
    }

    private static void SetShellObjectOffsets(
        DataObject dragData,
        FrameworkElement draggedElement)
    {
        Point groupOrigin =
            draggedElement.PointToScreen(
                new Point(
                    0,
                    0));

        byte[] shellObjectOffsets =
            new byte[16];

        Buffer.BlockCopy(
            BitConverter.GetBytes(
                (int)Math.Round(
                    groupOrigin.X)),
            0,
            shellObjectOffsets,
            0,
            4);

        Buffer.BlockCopy(
            BitConverter.GetBytes(
                (int)Math.Round(
                    groupOrigin.Y)),
            0,
            shellObjectOffsets,
            4,
            4);

        dragData.SetData(
            "Shell Object Offsets",
            new MemoryStream(
                shellObjectOffsets));
    }

    private void DockItem_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not FrameworkElement draggedElement)
        {
            return;
        }

        if (_itemMouseDownItem is null ||
            e.LeftButton !=
            MouseButtonState.Pressed)
        {
            return;
        }

        if (_itemMouseDownItem.IsWidget &&
            _itemMouseDownItem.IsWidgetDragDropLocked)
        {
            return;
        }

        Point current =
            e.GetPosition(this);

        if (Math.Abs(
                current.X -
                _itemMouseDownPoint.X) <
            SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(
                current.Y -
                _itemMouseDownPoint.Y) <
            SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DockItem draggedItem =
            _itemMouseDownItem;

        _suppressNextItemClick = true;
        _itemMouseDownItem = null;

        DataObject dragData =
            new();

        dragData.SetData(
            InternalDragFormat,
            draggedItem);

        if (!draggedItem.IsSubmenu &&
            !string.IsNullOrWhiteSpace(draggedItem.Path))
        {
            dragData.SetData(
                DataFormats.FileDrop,
                new[]
                {
                    draggedItem.Path
                });

            SetShellObjectOffsets(
                dragData,
                draggedElement);

            dragData.SetData(
                "Preferred DropEffect",
                new MemoryStream(
                    BitConverter.GetBytes(
                        (int)((Keyboard.Modifiers &
                               ModifierKeys.Shift) != 0
                            ? DragDropEffects.Move
                            : DragDropEffects.Copy))));
        }

        _activeInternalDragItem =
            draggedItem;

        _lastInternalDragIndex =
            DockItems.IndexOf(
                draggedItem);

        ShowInternalDragGhost(
            draggedItem);

        SetInternalDragItemVisibility(
            draggedItem,
            visible: false);

        DragDropEffects completedEffect =
            DragDropEffects.None;

        try
        {
            completedEffect =
                DragDrop.DoDragDrop(
                    this,
                    dragData,
                    DragDropEffects.Copy |
                    DragDropEffects.Move);

            if (completedEffect ==
                    DragDropEffects.Move &&
                DockItems.Contains(
                    draggedItem))
            {
                DockItems.Remove(
                    draggedItem);

                _dockItemStore.DeleteManagedItem(
                    draggedItem.Path);

                SaveSettings();
                UpdateItemsOrientation();
                UpdateWindowBounds();
                UpdateItemLabelVisibility();

                DebugLog.Write(
                    "RootDrag",
                    $"Item moved out of root dock; Name={draggedItem.DisplayName}; Path={draggedItem.Path}");
            }
        }
        finally
        {
            SetInternalDragItemVisibility(
                draggedItem,
                visible: true);

            ResetInternalDragItemTransforms();

            HideInternalDragGhost();
            _activeInternalDragItem = null;
            _lastInternalDragIndex = -1;

            _openSubDock?.ClearInternalDragPreview();

            if (!_settings.CollapseDisabled &&
                !_isWindowDragging &&
                !_isExternalDragActive &&
                _openContextMenuCount == 0 &&
                !IsMouseOver &&
                _openSubDock?.IsPointerOverDockChain != true)
            {
                RestartCollapseTimer();
            }
        }
    }

    private void DockItem_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            _itemMouseDownItem = null;
            return;
        }

        if (item.IsRuntimeOnly)
        {
            _itemMouseDownItem = null;
            return;
        }

        DockItem? pressedItem =
            _itemMouseDownItem;

        _itemMouseDownItem = null;

        if (_suppressNextItemClick)
        {
            _suppressNextItemClick = false;
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(
                pressedItem,
                item))
        {
            if (item.IsSubmenu)
            {
                StopSubmenuHoverCloseTimer();

                if (ReferenceEquals(
                        _pinnedSubmenuItem,
                        item) &&
                    _openSubDock?.IsVisible == true &&
                    ReferenceEquals(
                        _openSubDock.Tag,
                        item))
                {
                    DebugLog.Write(
                        "SubDock",
                        $"Unpin and close submenu by click; Name={item.DisplayName}");

                    _pinnedSubmenuItem = null;
                    _hoverSubmenuItem = null;
                    _hoverSubmenuAnchor = null;

                    _openSubDock.CloseAnimated();
                }
                else
                {
                    _hoverSubmenuItem = item;
                    _hoverSubmenuAnchor = element;
                    _pinnedSubmenuItem = item;

                    OpenSubmenu(
                        item,
                        element);

                    DebugLog.Write(
                        "SubDock",
                        $"Submenu pinned by click; Name={item.DisplayName}");
                }
            }
            else if (!item.IsWidget)
            {
                LaunchDockItem(item);
            }
        }

        e.Handled = true;
    }

    private void MainWindow_DragEnter(
        object sender,
        DragEventArgs e)
    {
        StopExternalDragLeaveTimer();

        bool externalDrag =
            !e.Data.GetDataPresent(
                InternalDragFormat);

        if (!externalDrag &&
            _activeInternalDragItem is not null)
        {
            ShowInternalDragGhost(
                _activeInternalDragItem);
        }

        if (externalDrag)
        {
            StopExternalDragLeaveTimer();

            if (!_isExternalDragActive)
            {
                _externalDropSubmenuTarget =
                    null;

                ClearExternalDropSubmenuHighlight();

                _isExternalDragActive = true;

                DebugLog.Write(
                    "RootDrag",
                    "External drag entered root dock.");

                Expand();
            }

            _collapseTimer.Stop();
        }

        SetDropEffect(e);
    }

    private void StartExternalDragLeaveTimer()
    {
        _externalDragLeaveTimer ??=
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        120)
            };

        _externalDragLeaveTimer.Tick -=
            ExternalDragLeaveTimer_Tick;

        _externalDragLeaveTimer.Tick +=
            ExternalDragLeaveTimer_Tick;

        _externalDragLeaveTimer.Stop();
        _externalDragLeaveTimer.Start();
    }

    private void StopExternalDragLeaveTimer()
    {
        _externalDragLeaveTimer?.Stop();
    }

    private void ExternalDragLeaveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        StopExternalDragLeaveTimer();

        if (_openSubDock?.IsPointerOverDockChain == true)
        {
            RemoveExternalDropPlaceholder();

            _collapseTimer.Stop();
            return;
        }

        if (_isExternalDragActive)
        {
            _isExternalDragActive = false;
            RemoveExternalDropPlaceholder();
            ClearExternalDropSubmenuHighlight();

            DebugLog.Write(
                "RootDrag",
                "External drag left root dock after debounce.");
        }
        else if (_externalDropPlaceholder is not null)
        {
            RemoveExternalDropPlaceholder();
            ClearExternalDropSubmenuHighlight();

            DebugLog.Write(
                "RootDrag",
                "Internal cross-dock drag left root dock after debounce.");
        }

        if (!_settings.CollapseDisabled &&
            _openContextMenuCount == 0 &&
            !IsMouseOver &&
            _openSubDock?.IsPointerOverDockChain != true)
        {
            RestartCollapseTimer();
        }
    }
    private void MainWindow_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                InternalDragFormat) &&
            e.Data.GetData(
                InternalDragFormat) is DockItem draggedItem)
        {
            Point pointerPosition =
                e.GetPosition(
                    DockItemsControl);

            UpdateInternalDragVisual(
                pointerPosition);

            FrameworkElement? submenuElement =
                FindInternalSubmenuDropElementAtPosition(
                    pointerPosition);

            if (submenuElement?.DataContext is DockItem submenuItem &&
                CanMoveItemToSubmenu(
                    draggedItem,
                    submenuItem))
            {
                _externalDropSubmenuTarget =
                    submenuItem;

                SetExternalDropSubmenuHighlight(
                    submenuElement);

                OpenSubmenu(
                    submenuItem,
                    submenuElement);
            }
            else
            {
                _externalDropSubmenuTarget =
                    null;

                ClearExternalDropSubmenuHighlight();

                if (DockItems.Contains(
                        draggedItem))
                {
                    UpdateInternalDragPosition(
                        draggedItem,
                        pointerPosition);
                }
                else
                {
                    string[] paths =
                        string.IsNullOrWhiteSpace(
                            draggedItem.Path)
                            ? []
                            : [draggedItem.Path];

                    UpdateExternalDropPlaceholder(
                        pointerPosition,
                        paths);
                }
            }
        }
        else if (e.Data.GetDataPresent(
                     DataFormats.FileDrop) &&
                 e.Data.GetData(
                     DataFormats.FileDrop) is string[] paths)
        {
            Point pointerPosition =
                e.GetPosition(
                    DockItemsControl);

            FrameworkElement? submenuElement =
                FindSubmenuElementAtPosition(
                    pointerPosition);

            if (submenuElement?.DataContext is DockItem submenuItem)
            {
                _externalDropSubmenuTarget =
                    submenuItem;

                SetExternalDropSubmenuHighlight(
                    submenuElement);

                OpenSubmenu(
                    submenuItem,
                    submenuElement);
            }
            else
            {
                HitTestResult? hit =
                    VisualTreeHelper.HitTest(
                        DockItemsControl,
                        pointerPosition);

                DockItem? targetItem =
                    FindDockItem(
                        hit?.VisualHit);

                if (targetItem is not null &&
                    !ReferenceEquals(
                        targetItem,
                        _externalDropPlaceholder))
                {
                    _externalDropSubmenuTarget =
                        null;

                    ClearExternalDropSubmenuHighlight();
                }
            }

            UpdateExternalDropPlaceholder(
                pointerPosition,
                paths);
        }

        SetDropEffect(e);
    }

    private void MainWindow_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                InternalDragFormat))
        {
            HideInternalDragGhost();
            StartExternalDragLeaveTimer();
            return;
        }

        StartExternalDragLeaveTimer();
    }
    private void MainWindow_Drop(
        object sender,
        DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(
                    InternalDragFormat) &&
                e.Data.GetData(
                    InternalDragFormat) is DockItem draggedItem)
            {
                FrameworkElement? submenuElement =
                    FindInternalSubmenuDropElementAtPosition(
                        e.GetPosition(
                            DockItemsControl));

                DockItem? submenuItem =
                    submenuElement?.DataContext as DockItem ??
                    _externalDropSubmenuTarget;

                if (submenuItem is not null &&
                    submenuItem.IsSubmenu &&
                    CanMoveItemToSubmenu(
                        draggedItem,
                        submenuItem))
                {
                    RemoveExternalDropPlaceholder();

                    MoveItemToSubmenu(
                        draggedItem,
                        submenuItem);

                    if (_openSubDock?.IsVisible == true &&
                        ReferenceEquals(
                            _openSubDock.Tag,
                            submenuItem))
                    {
                        _openSubDock.RefreshItemsLayout();
                    }

                    DebugLog.Write(
                        "RootDrag",
                        $"Internal item moved directly to submenu; Item={draggedItem.DisplayName}; Submenu={submenuItem.DisplayName}; Items={submenuItem.Children.Count}");
                }
                else if (!DockItems.Contains(
                             draggedItem))
                {
                    int insertionIndex =
                        _externalDropPlaceholder is null
                            ? DockItems.Count
                            : Math.Max(
                                0,
                                DockItems.IndexOf(
                                    _externalDropPlaceholder));

                    RemoveExternalDropPlaceholder();

                    if (TryRemoveDockItem(
                            DockItems,
                            draggedItem))
                    {
                        insertionIndex =
                            Math.Clamp(
                                insertionIndex,
                                0,
                                DockItems.Count);

                        DockItems.Insert(
                            insertionIndex,
                            draggedItem);

                        EnsureRootRowCapacity();
                        UpdateItemsOrientation();
                        UpdateWindowBounds();
                        UpdateItemLabelVisibility();

                        _openSubDock?.RefreshItemsLayout();

                        DebugLog.Write(
                            "RootDrag",
                            $"Internal item moved from subdock to root dock; Name={draggedItem.DisplayName}; Index={insertionIndex}");
                    }
                }

                e.Effects =
                    DragDropEffects.Copy;

                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(
                    DataFormats.FileDrop) &&
                e.Data.GetData(
                    DataFormats.FileDrop) is string[] paths)
            {
                bool moveSource =
                    (e.KeyStates &
                     DragDropKeyStates.ShiftKey) != 0;

                int insertionIndex =
                    _externalDropPlaceholder is null
                        ? DockItems.Count
                        : Math.Max(
                            0,
                            DockItems.IndexOf(
                                _externalDropPlaceholder));

                FrameworkElement? submenuElement =
                    FindSubmenuElementAtPosition(
                        e.GetPosition(
                            DockItemsControl));

                DockItem? submenuItem =
                    submenuElement?.DataContext as DockItem ??
                    _externalDropSubmenuTarget;

                RemoveExternalDropPlaceholder();

                if (submenuItem is not null &&
                    submenuItem.IsSubmenu)
                {
                    AddDroppedItemsToSubmenu(
                        paths,
                        submenuItem,
                        moveSource);

                    if (_openSubDock?.IsVisible == true &&
                        ReferenceEquals(
                            _openSubDock.Tag,
                            submenuItem))
                    {
                        _openSubDock.RefreshItemsLayout();
                    }

                    DebugLog.Write(
                        "RootDrag",
                        $"External drop added directly to submenu; Name={submenuItem.DisplayName}; Items={submenuItem.Children.Count}; Move={moveSource}");
                }
                else
                {
                    AddDroppedItems(
                        paths,
                        insertionIndex,
                        moveSource);
                }

                e.Effects =
                    moveSource
                        ? DragDropEffects.Move
                        : DragDropEffects.Copy;

                e.Handled = true;
            }
        }
        finally
        {
            StopExternalDragLeaveTimer();
            RemoveExternalDropPlaceholder();

            _externalDropSubmenuTarget =
                null;

            ClearExternalDropSubmenuHighlight();

            _isExternalDragActive = false;

            DebugLog.Write(
                "RootDrag",
                "External/internal drop finalized on root dock.");

            SaveSettings();

            if (!_settings.CollapseDisabled &&
                _openContextMenuCount == 0 &&
                !IsMouseOver &&
                _openSubDock?.IsPointerOverDockChain != true)
            {
                RestartCollapseTimer();
            }
        }
    }

    private void RemoveDockItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item)
        {
            return;
        }

        DetachWidgetCompanion(
            item);

        DockItems.Remove(item);

        DeleteManagedContent(item);

        if (ReferenceEquals(
                _openSubDock?.Tag,
                item))
        {
            _openSubDock.Close();
        }

        UpdateItemsOrientation();
        UpdateWindowBounds();
        UpdateItemLabelVisibility();
        SaveSettings();

        _openContextMenuCount = 0;

        Dispatcher.BeginInvoke(
            () =>
            {
                if (!_settings.CollapseDisabled &&
                    _openSubDock?.IsVisible != true &&
                    !IsMouseOver)
                {
                    _collapseTimer.Stop();
                    RestartCollapseTimer();
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private void RenameDockItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item)
        {
            return;
        }

        SubmenuNameWindow dialog =
            new(
                item.DisplayName,
                "Rename")
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        item.DisplayName =
            dialog.SubmenuName;

        SaveSettings();
    }

    private void ChangeDockItemIcon_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            !item.IsSubmenu)
        {
            return;
        }

        string iconSetDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Iconset");

        Directory.CreateDirectory(
            iconSetDirectory);

        Microsoft.Win32.OpenFileDialog dialog =
            new()
            {
                Title =
                    "Change icon",
                Filter =
                    "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp|GIF|*.gif|Icon|*.ico",
                InitialDirectory =
                    iconSetDirectory
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            item.SubmenuIconRepositoryPath =
                IconRepository.Import(
                    dialog.FileName);

            item.Icon =
                SubmenuIcon.Create(
                    _settings,
                    item.SubmenuIconRepositoryPath);

            SaveSettings();
        }
        catch
        {
            MessageBox.Show(
                this,
                App.Language["Message.SubmenuIconImportFailed"],
                App.Language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UseDefaultDockItemIcon_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            !item.IsSubmenu)
        {
            return;
        }

        item.SubmenuIconRepositoryPath =
            string.Empty;

        item.Icon =
            SubmenuIcon.Create(
                _settings);

        SaveSettings();
    }

    private void NewSubmenu_Click(
        object sender,
        RoutedEventArgs e)
    {
        SubmenuNameWindow dialog =
            new()
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        DockItems.Add(
            new DockItem
            {
                IsSubmenu = true,
                DisplayName = dialog.SubmenuName,
                Icon = SubmenuIcon.Create(_settings)
            });

        UpdateWindowBounds();
        SaveSettings();

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();
            },
            DispatcherPriority.Loaded);
    }

    private void OpenSubDock_ExternalDragEnded(
        object? sender,
        EventArgs e)
    {
        StopExternalDragLeaveTimer();
        RemoveExternalDropPlaceholder();
        ClearExternalDropSubmenuHighlight();

        if (_isExternalDragActive)
        {
            _isExternalDragActive = false;

            DebugLog.Write(
                "RootDrag",
                "External drag ended in submenu chain.");
        }

        _collapseTimer.Stop();

        Dispatcher.BeginInvoke(
            () =>
            {
                if (_settings.CollapseDisabled ||
                    _isWindowDragging ||
                    _isExternalDragActive ||
                    _activeInternalDragItem is not null ||
                    _openContextMenuCount > 0 ||
                    IsMouseOver ||
                    _openSubDock?.IsPointerOverDockChain == true)
                {
                    _collapseTimer.Stop();
                    return;
                }

                RestartCollapseTimer();
            },
            DispatcherPriority.Input);
    }

    private void OpenSubDock_InteractionStateChanged(
        object? sender,
        EventArgs e)
    {
        LogRootState(
            "Subdock interaction state changed");

        if (_pinnedSubmenuItem is null &&
            _hoverSubmenuItem is not null)
        {
            if (_hoverSubmenuAnchor?.IsMouseOver == true ||
                _openSubDock?.IsPointerOverDockChain == true)
            {
                StopSubmenuHoverCloseTimer();
            }
            else
            {
                StartSubmenuHoverCloseTimer();
            }
        }

        if (_settings.CollapseDisabled ||
            _isWindowDragging ||
            _isExternalDragActive ||
            _activeInternalDragItem is not null ||
            _openContextMenuCount > 0)
        {
            _collapseTimer.Stop();
            return;
        }

        if (IsMouseOver ||
            _openSubDock?.IsPointerOverDockChain == true)
        {
            _collapseTimer.Stop();
            return;
        }

        RestartCollapseTimer();
    }

    private void OpenSubmenu(
        DockItem item,
        FrameworkElement anchor)
    {
        if (_isWindowDragging)
        {
            return;
        }

        if (_openSubDock?.IsVisible == true &&
            ReferenceEquals(
                _openSubDock.Tag,
                item))
        {
            _collapseTimer.Stop();
            return;
        }

        DebugLog.Write(
            "SubDock",
            $"Root submenu open; Name={item.DisplayName}; ChildCount={item.Children.Count}");

        _collapseTimer.Stop();
        _openSubDock?.Close();

        System.Windows.Rect anchorRect;

        DpiScale anchorDpi =
            VisualTreeHelper.GetDpi(
                this);

        if (_settings.Edge is
            DockEdge.Top or
            DockEdge.Bottom)
        {
            anchorRect =
                new System.Windows.Rect(
                    Left,
                    Top,
                    ActualWidth > 0
                        ? ActualWidth
                        : Width,
                    ActualHeight > 0
                        ? ActualHeight
                        : Height);
        }
        else
        {
            anchorRect =
                GetScreenRect(
                    anchor);
        }

        _openSubDock =
            new SubDockWindow(
                item,
                _settings,
                _dockItemStore,
                SaveSettings,
                MoveItemToSubmenu,
                CanMoveItemToSubmenu,
                _settings.Edge)
            {
                Tag = item
            };

        _openSubDock.InteractionStateChanged +=
            OpenSubDock_InteractionStateChanged;

        _openSubDock.ExternalDragEnded +=
            OpenSubDock_ExternalDragEnded;

        _openSubDock.Closed +=
            (_, _) =>
            {
                DebugLog.Write(
                    "SubDock",
                    $"Root submenu closed; Name={item.DisplayName}");

                if (_openSubDock is not null)
                {
                    _openSubDock.InteractionStateChanged -=
                        OpenSubDock_InteractionStateChanged;

                    _openSubDock.ExternalDragEnded -=
                        OpenSubDock_ExternalDragEnded;
                }

                _openSubDock = null;

                if (ReferenceEquals(
                        _hoverSubmenuItem,
                        item))
                {
                    _hoverSubmenuItem = null;
                    _hoverSubmenuAnchor = null;
                }

                if (ReferenceEquals(
                        _pinnedSubmenuItem,
                        item))
                {
                    _pinnedSubmenuItem = null;
                }

                StopSubmenuHoverCloseTimer();

                if (!_closingSubDockForRootCollapse &&
                    !_settings.CollapseDisabled &&
                    _openContextMenuCount == 0 &&
                    !IsMouseOver &&
                    !_collapseTimer.IsEnabled)
                {
                    RestartCollapseTimer();
                }
            };

        _openSubDock.PositionNextTo(
            anchorRect,
            anchorDpi);

        _openSubDock.Show();

        DebugLog.Write(
            "SubDock",
            $"Root submenu shown; Name={item.DisplayName}; Left={_openSubDock.Left:0.0}; Top={_openSubDock.Top:0.0}; Width={_openSubDock.Width:0.0}; Height={_openSubDock.Height:0.0}");
    }

    private bool CanMoveItemToSubmenu(
        DockItem item,
        DockItem targetSubmenu)
    {
        if (ReferenceEquals(item, targetSubmenu))
        {
            return false;
        }

        if (item.IsSubmenu &&
            ContainsDockItem(item, targetSubmenu))
        {
            return false;
        }

        return true;
    }

    private void MoveItemToSubmenu(
        DockItem item,
        DockItem targetSubmenu)
    {
        if (!CanMoveItemToSubmenu(item, targetSubmenu))
        {
            return;
        }

        if (!TryRemoveDockItem(DockItems, item))
        {
            return;
        }

        targetSubmenu.Children.Add(item);

        UpdateWindowBounds();
        SaveSettings();
    }

    private static bool TryRemoveDockItem(
        System.Collections.ObjectModel.ObservableCollection<DockItem> items,
        DockItem target)
    {
        if (items.Remove(target))
        {
            return true;
        }

        foreach (DockItem item in items)
        {
            if (item.IsSubmenu &&
                TryRemoveDockItem(item.Children, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDockItem(
        DockItem root,
        DockItem target)
    {
        foreach (DockItem child in root.Children)
        {
            if (ReferenceEquals(child, target) ||
                (child.IsSubmenu &&
                 ContainsDockItem(child, target)))
            {
                return true;
            }
        }

        return false;
    }

    private void DeleteManagedContent(
        DockItem item)
    {
        item.DisposeWidget();

        if (item.IsSubmenu)
        {
            foreach (DockItem child in item.Children.ToList())
            {
                DeleteManagedContent(child);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Path))
        {
            _dockItemStore.DeleteManagedItem(item.Path);
        }
    }

    private static System.Windows.Rect GetScreenRect(
        FrameworkElement element)
    {
        Point topLeftPixels =
            element.PointToScreen(
                new Point(0, 0));

        DpiScale dpi =
            VisualTreeHelper.GetDpi(
                element);

        Point topLeft =
            new(
                topLeftPixels.X /
                dpi.DpiScaleX,
                topLeftPixels.Y /
                dpi.DpiScaleY);

        return new System.Windows.Rect(
            topLeft.X,
            topLeft.Y,
            element.ActualWidth,
            element.ActualHeight);
    }

    private void DockItemContextMenu_Opened(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ContextMenu contextMenu ||
            contextMenu.PlacementTarget is not FrameworkElement placementTarget ||
            placementTarget.DataContext is not DockItem item)
        {
            return;
        }

        if (item.IsRuntimeOnly)
        {
            foreach (object menuEntry in contextMenu.Items)
            {
                if (menuEntry is MenuItem menuItem)
                {
                    menuItem.Visibility =
                        string.Equals(
                            menuItem.Name,
                            "CloseWidgetCompanionMenuItem",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            menuItem.Name,
                            "DockItemDisableAutohideMenuItem",
                            StringComparison.Ordinal)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
                else if (menuEntry is Separator separator)
                {
                    separator.Visibility =
                        Visibility.Collapsed;
                }
            }

            if (FindContextMenuItem(
                    contextMenu.Items,
                    "DockItemDisableAutohideMenuItem") is MenuItem runtimeDisableAutohideMenuItem)
            {
                runtimeDisableAutohideMenuItem.Header =
                    App.Language["Context.DisableAutohide"];

                runtimeDisableAutohideMenuItem.IsChecked =
                    _settings.CollapseDisabled;
            }

            return;
        }

        item.NotifyWidgetStateChanged();

        if (FindContextMenuItem(
                contextMenu.Items,
                "DockItemAlwaysOnTopMenuItem") is MenuItem alwaysOnTopMenuItem)
        {
            alwaysOnTopMenuItem.IsChecked =
                _settings.AlwaysOnTop;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "DockItemDisableAutohideMenuItem") is MenuItem disableAutohideMenuItem)
        {
            disableAutohideMenuItem.Header =
                App.Language["Context.DisableAutohide"];

            disableAutohideMenuItem.IsChecked =
                _settings.CollapseDisabled;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "StartAsAdministratorMenuItem") is MenuItem startAsAdministratorMenuItem)
        {
            startAsAdministratorMenuItem.Visibility =
                CanStartDockItemAsAdministrator(
                    item)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetActionsMenuItem") is MenuItem widgetMenuItem)
        {
            widgetMenuItem.Header =
                $"Widget: {item.DisplayName}";

            widgetMenuItem.Visibility =
                item.IsWidget
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetDragDropLockMenuItem") is MenuItem dragDropLockMenuItem)
        {
            dragDropLockMenuItem.Header =
                App.Language["Context.WidgetDragDropLock"];

            dragDropLockMenuItem.IsChecked =
                item.IsWidgetDragDropLocked;

            dragDropLockMenuItem.Visibility =
                item.IsWidget
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetSettingsMenuItem") is MenuItem settingsMenuItem)
        {
            settingsMenuItem.Visibility =
                item.HasWidgetSettings
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetAlarmMenuItem") is MenuItem alarmMenuItem)
        {
            alarmMenuItem.Visibility =
                item.HasWidgetAlarm
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetTimerMenuItem") is MenuItem timerMenuItem)
        {
            timerMenuItem.Visibility =
                item.HasWidgetTimer
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetStopwatchMenuItem") is MenuItem stopwatchMenuItem)
        {
            stopwatchMenuItem.Visibility =
                item.HasWidgetStopwatch
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "WidgetCalendarMenuItem") is MenuItem calendarMenuItem)
        {
            calendarMenuItem.Visibility =
                item.HasWidgetCalendar
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    private static MenuItem? FindContextMenuItem(
        ItemCollection items,
        string name)
    {
        foreach (object entry in items)
        {
            if (entry is not MenuItem menuItem)
            {
                continue;
            }

            if (string.Equals(
                    menuItem.Name,
                    name,
                    StringComparison.Ordinal))
            {
                return menuItem;
            }

            MenuItem? nested =
                FindContextMenuItem(
                    menuItem.Items,
                    name);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void CloseWidgetCompanion_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            !item.IsRuntimeOnly)
        {
            return;
        }

        IGlueDockWidget? widget =
            _widgetCompanionItems
                .FirstOrDefault(
                    entry =>
                        ReferenceEquals(
                            entry.Value,
                            item))
                .Key;

        if (widget is null)
        {
            return;
        }

        widget.CloseCompanion();

        _widgetCompanionItems.Remove(
            widget);

        DockItems.Remove(
            item);

        UpdateItemsOrientation();
        UpdateWindowBounds();
        UpdateItemLabelVisibility();

        DebugLog.Write(
            "Widget",
            $"Companion closed by user; Item={item.DisplayName}; DockItems={DockItems.Count}");
    }

    private void MainWindow_ToolTipOpening(
        object sender,
        ToolTipEventArgs e)
    {
        if (_settings.ShowRootDockTooltips)
        {
            return;
        }

        e.Handled =
            true;

        DebugLog.Write(
            "ToolTip",
            "Suppressed; Scope=Root");
    }

    private void MainWindow_ContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        _openContextMenuCount++;

        LogRootState(
            "Context menu opening");

        _collapseTimer.Stop();

        if (!_isExpanded)
        {
            Expand();
        }
    }

    private void MainWindow_ContextMenuClosing(
        object sender,
        ContextMenuEventArgs e)
    {
        if (_openContextMenuCount > 0)
        {
            _openContextMenuCount--;
        }

        LogRootState(
            "Context menu closing");

        if (_openContextMenuCount == 0 &&
            !_settings.CollapseDisabled &&
            _openSubDock?.IsVisible != true &&
            !IsMouseOver)
        {
            RestartCollapseTimer();
        }
    }

    private void WidgetDragDropLockMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            !item.IsWidget)
        {
            return;
        }

        item.IsWidgetDragDropLocked =
            menuItem.IsChecked;

        SaveSettings();

        DebugLog.Write(
            "Widget",
            $"Drag/drop lock changed; Item={item.DisplayName}; Id={item.Id}; Locked={item.IsWidgetDragDropLocked}");
    }

    private void OpenWidgetSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.WidgetInstance is null ||
            !item.WidgetInstance.HasSettings)
        {
            return;
        }

        OpenWidgetSettingsSection(
            item.WidgetInstance,
            "General");

        item.NotifyWidgetStateChanged();
        RefreshDockItemHoverEffects();

        DebugLog.Write(
            "Widget",
            $"Settings opened in Settings Hub; Item={item.DisplayName}; Id={item.Id}; Section=General; DisableDefaultHoverEffect={item.DisableDefaultHoverEffect}");
    }

    private void OpenWidgetAlarm_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.WidgetInstance is null ||
            !item.WidgetInstance.HasAlarm)
        {
            return;
        }

        item.WidgetInstance.OpenAlarm(
            this);

        item.NotifyWidgetStateChanged();

        DebugLog.Write(
            "Widget",
            $"Alarm closed; Item={item.DisplayName}; Id={item.Id}; Active={item.IsWidgetAlarmActive}");
    }

    private void OpenWidgetTimer_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.WidgetInstance is null ||
            !item.WidgetInstance.HasTimer)
        {
            return;
        }

        item.WidgetInstance.OpenTimer(
            this);

        item.NotifyWidgetStateChanged();

        DebugLog.Write(
            "Widget",
            $"Timer companion requested; Item={item.DisplayName}; Id={item.Id}; Active={item.IsWidgetTimerActive}");
    }

    private void OpenWidgetStopwatch_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.WidgetInstance is null ||
            !item.WidgetInstance.HasStopwatch)
        {
            return;
        }

        item.WidgetInstance.OpenStopwatch(
            this);

        item.NotifyWidgetStateChanged();

        DebugLog.Write(
            "Widget",
            $"Stopwatch companion requested; Item={item.DisplayName}; Id={item.Id}; Active={item.IsWidgetStopwatchActive}");
    }

    private void OpenWidgetCalendar_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.WidgetInstance is null ||
            !item.WidgetInstance.HasCalendar)
        {
            return;
        }

        item.WidgetInstance.ApplyHostAppearance(
            CreateWidgetAppearance());

        item.WidgetInstance.OpenCalendar(
            this);

        item.NotifyWidgetStateChanged();

        DebugLog.Write(
            "Widget",
            $"Calendar closed; Item={item.DisplayName}; Id={item.Id}; Active={item.IsWidgetCalendarActive}");
    }

    private void RootContextMenu_Opened(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ContextMenu contextMenu)
        {
            return;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "AlwaysOnTopContextMenuItem") is MenuItem alwaysOnTopMenuItem)
        {
            alwaysOnTopMenuItem.IsChecked =
                _settings.AlwaysOnTop;
        }

        if (FindContextMenuItem(
                contextMenu.Items,
                "DisableAutohideContextMenuItem") is MenuItem disableAutohideMenuItem)
        {
            disableAutohideMenuItem.IsChecked =
                _settings.CollapseDisabled;
        }
    }

    private void AlwaysOnTopContextMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        _settings.AlwaysOnTop =
            menuItem.IsChecked;

        ApplyNativeWindowStyle();
        SaveSettings();

        DebugLog.Write(
            "ZOrder",
            $"Always on top changed from root context menu; Enabled={_settings.AlwaysOnTop}");
    }

    private void DisableAutohideContextMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        _settings.CollapseDisabled =
            menuItem.IsChecked;

        if (_settings.CollapseDisabled)
        {
            _collapseTimer.Stop();
            Expand();
        }
        else if (!IsMouseOver)
        {
            RestartCollapseTimer();
        }

        SaveSettings();

        DebugLog.Write(
            "Autohide",
            $"Disable autohide changed from root context menu; Disabled={_settings.CollapseDisabled}");
    }

    private void OpenWidgetSettingsSection(
        IGlueDockWidget widget,
        string sectionId)
    {
        if (_settingsWindow is null)
        {
            OpenSettings_Click(
                this,
                new RoutedEventArgs());
        }

        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.OpenWidgetSettingsSection(
            widget,
            sectionId);

        _settingsWindow.Activate();
    }

    private void OpenSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow =
            new SettingsWindow(
                _settings,
                ApplySettingsLive,
                ApplyItemSpacingLive,
                ApplySettingsPreview,
                AddWidgetFromSettings,
                GetWidgetAddCount,
                App.Language,
                CreateWidgetAppearance(),
                GetSettingsWidgetInstances(
                    DockItems))
            {
                Owner =
                    _settings.AlwaysOnTop
                        ? null
                        : this,
                WindowStartupLocation =
                    _settings.AlwaysOnTop
                        ? WindowStartupLocation.CenterScreen
                        : WindowStartupLocation.CenterOwner
            };

        _settingsWindow.Closed +=
            SettingsWindow_Closed;

        _settingsWindow.Show();
    }

    private static IReadOnlyList<IGlueDockWidget> GetSettingsWidgetInstances(
        IEnumerable<DockItem> items)
    {
        List<IGlueDockWidget> widgets =
            [];

        HashSet<IGlueDockWidget> seen =
            new();

        CollectSettingsWidgetInstances(
            items,
            widgets,
            seen);

        return widgets;
    }

    private static void CollectSettingsWidgetInstances(
        IEnumerable<DockItem> items,
        List<IGlueDockWidget> widgets,
        HashSet<IGlueDockWidget> seen)
    {
        foreach (DockItem item in items)
        {
            if (!item.IsRuntimeOnly &&
                item.WidgetInstance is IGlueDockWidget widget &&
                seen.Add(
                    widget))
            {
                widgets.Add(
                    widget);
            }

            if (item.Children.Count > 0)
            {
                CollectSettingsWidgetInstances(
                    item.Children,
                    widgets,
                    seen);
            }
        }
    }

    private int GetWidgetAddCount(
        string assemblyPath)
    {
        return CountWidgetInstances(
            DockItems,
            assemblyPath);
    }

    private static int CountWidgetInstances(
        IEnumerable<DockItem> items,
        string assemblyPath)
    {
        int count =
            0;

        string fullAssemblyPath =
            Path.GetFullPath(
                assemblyPath);

        foreach (DockItem item in items)
        {
            if (!item.IsRuntimeOnly &&
                item.IsWidget &&
                !string.IsNullOrWhiteSpace(
                    item.Path) &&
                string.Equals(
                    Path.GetFullPath(
                        item.Path),
                    fullAssemblyPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }

            if (item.Children.Count > 0)
            {
                count +=
                    CountWidgetInstances(
                        item.Children,
                        fullAssemblyPath);
            }
        }

        return count;
    }

    private void AddWidgetFromSettings(
        string path)
    {
        DockItem item =
            CreateDockItem(
                path);

        if (!item.IsWidget)
        {
            MessageBox.Show(
                this,
                App.Language["Message.WidgetIncompatible"],
                App.Language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DebugLog.Write(
                "Widget",
                $"Rejected from settings; Path={path}; Reason=Incompatible");

            return;
        }

        DockItems.Add(
            item);

        item.WidgetInstance?.ApplyHostAppearance(
            CreateWidgetAppearance());

        EnsureRootRowCapacity();
        UpdateItemsOrientation();
        UpdateWindowBounds();
        UpdateItemLabelVisibility();
        SaveSettings();

        DebugLog.Write(
            "Widget",
            $"Added from settings; Path={item.Path}; Name={item.DisplayName}; Id={item.Id}");
    }

    private void SettingsWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _settingsWindow = null;
        SaveSettings();
    }

    private void ApplyItemSpacingLive()
    {
        DebugLog.Write(
            "Settings",
            $"Apply item spacing live; ItemSpacing={_settings.ItemSpacing:0.##}");

        UpdateItemsOrientation();
        UpdateWindowBounds();
        UpdateItemLabelVisibility();

        if (_openSubDock?.IsVisible == true)
        {
            DpiScale anchorDpi =
                VisualTreeHelper.GetDpi(
                    this);

            System.Windows.Rect anchorRect =
                new(
                    Left,
                    Top,
                    ActualWidth > 0
                        ? ActualWidth
                        : Width,
                    ActualHeight > 0
                        ? ActualHeight
                        : Height);

            _openSubDock.ApplyItemSpacingLive(
                anchorRect,
                anchorDpi);
        }

        SaveSettings();
    }

    private void ApplySettingsPreview()
    {
        _isSettingsPreviewApply = true;

        try
        {
            ApplySettingsLive();
        }
        finally
        {
            _isSettingsPreviewApply = false;
        }
    }

    private void ApplySettingsLive()
    {
        if (!_isSettingsPreviewApply)
        {
            DebugLog.SetEnabled(
                _settings.DebugLoggingEnabled);
        }

        DebugLog.Write(
            "Settings",
            $"Apply live; Theme={_settings.ThemeName}; CollapseDisabled={_settings.CollapseDisabled}; Delay={_settings.CollapseDelayMilliseconds}; SubdockDelay={_settings.SubdockCollapseDelayMilliseconds}; SubdockClickOnly={_settings.SubdockOpenOnClickOnly}; RootTooltips={_settings.ShowRootDockTooltips}; SubDockTooltips={_settings.ShowSubDockTooltips}; Labels={_settings.ShowItemLabels}; Previews={_settings.ShowFilePreviews}; ShortcutOverlayMode={_settings.ShortcutOverlayMode}; Scale={_settings.DockScale:0.00}; ItemSpacing={_settings.ItemSpacing:0}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; Animation={_settings.AnimationStyle}; Hover={_settings.HoverEffect}");

        _collapseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.CollapseDelayMilliseconds));

        if (_submenuHoverCloseTimer is not null)
        {
            _submenuHoverCloseTimer.Interval =
                TimeSpan.FromMilliseconds(
                    Math.Max(
                        0,
                        _settings.SubdockCollapseDelayMilliseconds));
        }

        if (!_isSettingsPreviewApply)
        {
            StartupManager.SetStartWithWindows(
                _settings.StartWithWindows);
        }

        UpdateItemsOrientation();
        ApplyAppearance();

        _settingsWindow?.ApplyAppearance(
            CreateWidgetAppearance());

        ApplyWidgetAppearance(
            DockItems);
        UpdateItemLabelVisibility();
        UpdateDockItemPreviews();
        ApplyRootDockTooltipSetting();
        RefreshDockItemHoverEffects();

        if (_settings.CollapseDisabled)
        {
            _collapseTimer.Stop();
            Expand();
        }
        else if (!IsMouseOver)
        {
            RestartCollapseTimer();
        }

        if (_settings.SubdockOpenOnClickOnly &&
            _pinnedSubmenuItem is null &&
            _openSubDock?.IsVisible == true)
        {
            _hoverSubmenuItem = null;
            _hoverSubmenuAnchor = null;
            StopSubmenuHoverCloseTimer();
            _openSubDock.CloseAnimated();
        }

        if (_openSubDock?.IsVisible == true)
        {
            _openSubDock.ApplySettingsLive();

            if (_settings.Edge is
                DockEdge.Top or
                DockEdge.Bottom)
            {
                DpiScale anchorDpi =
                    VisualTreeHelper.GetDpi(
                        this);

                System.Windows.Rect anchorRect =
                    new(
                        Left,
                        Top,
                        ActualWidth > 0
                            ? ActualWidth
                            : Width,
                        ActualHeight > 0
                            ? ActualHeight
                            : Height);

                _openSubDock.PositionNextTo(
                    anchorRect,
                    anchorDpi);
            }
        }

        if (!_isSettingsPreviewApply)
        {
            SaveSettings();
        }
    }

    private void ApplyRootDockTooltipSetting()
    {
        ToolTipService.SetIsEnabled(
            DockItemsControl,
            _settings.ShowRootDockTooltips);

        for (int index = 0;
             index < DockItemsControl.Items.Count;
             index++)
        {
            if (DockItemsControl.ItemContainerGenerator.ContainerFromIndex(
                    index) is not DependencyObject container)
            {
                continue;
            }

            Border? border =
                FindVisualChild<Border>(
                    container,
                    "DockItemBorder");

            if (border is null)
            {
                continue;
            }

            ApplyToolTipSettingToVisualTree(
                border,
                _settings.ShowRootDockTooltips);
        }

        DebugLog.Write(
            "ToolTip",
            $"Applied; Scope=Root; Enabled={_settings.ShowRootDockTooltips}; Items={DockItemsControl.Items.Count}");
    }

    private static void ApplyToolTipSettingToVisualTree(
        DependencyObject root,
        bool enabled)
    {
        if (root is FrameworkElement frameworkElement)
        {
            ToolTipService.SetIsEnabled(
                frameworkElement,
                enabled);
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int index = 0;
             index < childCount;
             index++)
        {
            ApplyToolTipSettingToVisualTree(
                VisualTreeHelper.GetChild(
                    root,
                    index),
                enabled);
        }
    }

    private void UpdateDockItemPreviews()
    {
        foreach (DockItem item in DockItems)
        {
            UpdateDockItemPreview(
                item);
        }
    }

    private void UpdateDockItemPreview(
        DockItem item)
    {
        item.Icon =
            item.IsSubmenu
                ? SubmenuIcon.Create(
                    _settings,
                    item.SubmenuIconRepositoryPath)
                : ShellIcon.GetIcon(
                    item.Path,
                    _settings.ShowFilePreviews,
                    _settings.ShortcutOverlayMode);

        foreach (DockItem child in item.Children)
        {
            UpdateDockItemPreview(
                child);
        }
    }

    private GlueDockWidgetAppearance CreateWidgetAppearance()
    {
        DockTheme theme =
            DockThemeService.Load(
                _settings.ThemeName);

        bool isMica =
            string.Equals(
                _settings.ThemeName,
                "Mica",
                StringComparison.OrdinalIgnoreCase);

        return
            new GlueDockWidgetAppearance
            {
                ThemeName =
                    _settings.ThemeName,
                DockBackgroundBrush =
                    (Resources["DockBackgroundBrush"] as System.Windows.Media.Brush ??
                     System.Windows.Media.Brushes.Transparent).Clone(),
                DockItemBackgroundBrush =
                    (Resources["DockItemBackgroundBrush"] as System.Windows.Media.Brush ??
                     System.Windows.Media.Brushes.Transparent).Clone(),
                DockItemBorderBrush =
                    (Resources["DockItemBorderBrush"] as System.Windows.Media.Brush ??
                     System.Windows.Media.Brushes.Transparent).Clone(),
                DockTextBrush =
                    (Resources["DockTextBrush"] as System.Windows.Media.Brush ??
                     System.Windows.Media.Brushes.White).Clone(),
                DockBorderEnabled =
                    _settings.DockBorderEnabled,
                DockBorderColor =
                    ParseThemeColor(
                        _settings.DockBorderColor,
                        Colors.White),
                Opacity =
                    isMica
                        ? 0.72 +
                          (Math.Clamp(
                              _settings.Opacity,
                              0,
                              1) * 0.28)
                        : _settings.Opacity,
                BlurRadius =
                    isMica
                        ? 0
                        : _settings.BlurRadius,
                GlassSurfaceEnabled =
                    theme.GlassSurfaceEnabled,
                GlassTopColor =
                    ParseThemeColor(
                        theme.GlassTopColor,
                        Color.FromArgb(
                            0xD0,
                            0xE8,
                            0xF2,
                            0xEC)),
                GlassBottomColor =
                    ParseThemeColor(
                        theme.GlassBottomColor,
                        Color.FromArgb(
                            0x80,
                            0xA8,
                            0xBE,
                            0xB4)),
                GlassHighlightColor =
                    ParseThemeColor(
                        theme.GlassHighlightColor,
                        Color.FromArgb(
                            0xB0,
                            0xFF,
                            0xFF,
                            0xFF)),
                GlassCornerRadius =
                    Math.Clamp(
                        theme.GlassCornerRadius,
                        0,
                        80),
                GlassGradientReferenceHeight =
                    BaseExpandedThickness,
                AvailableThemes =
                    CreateWidgetThemeAppearances()
            };
    }

    private IReadOnlyList<GlueDockWidgetThemeAppearance> CreateWidgetThemeAppearances()
    {
        List<GlueDockWidgetThemeAppearance> appearances =
            new();

        foreach (DockThemeOption option in
                 DockThemeService.GetAvailableThemes())
        {
            DockTheme theme =
                DockThemeService.Load(
                    option.Id);

            bool isMica =
                string.Equals(
                    option.Id,
                    "Mica",
                    StringComparison.OrdinalIgnoreCase);

            double effectiveOpacity =
                isMica
                    ? 0.72 +
                      (Math.Clamp(
                          _settings.Opacity,
                          0,
                          1) * 0.28)
                    : _settings.Opacity;

            double effectiveBlurRadius =
                isMica
                    ? 0
                    : _settings.BlurRadius;

            Color backgroundColor =
                ParseThemeColor(
                    theme.DockBackgroundColor,
                    Color.FromRgb(
                        0x12,
                        0x16,
                        0x1C));

            byte backgroundAlpha =
                (byte)Math.Clamp(
                    Math.Round(
                        effectiveOpacity * 255),
                    0,
                    255);

            appearances.Add(
                new GlueDockWidgetThemeAppearance
                {
                    ThemeName =
                        option.Id,
                    DisplayName =
                        option.DisplayName,
                    DockBackgroundBrush =
                        new SolidColorBrush(
                            Color.FromArgb(
                                backgroundAlpha,
                                backgroundColor.R,
                                backgroundColor.G,
                                backgroundColor.B)),
                    DockItemBackgroundBrush =
                        new SolidColorBrush(
                            ParseThemeColor(
                                theme.ItemBackgroundColor,
                                Color.FromArgb(
                                    0x22,
                                    0xFF,
                                    0xFF,
                                    0xFF))),
                    DockItemBorderBrush =
                        new SolidColorBrush(
                            ParseThemeColor(
                                theme.ItemBorderColor,
                                Color.FromArgb(
                                    0x22,
                                    0xFF,
                                    0xFF,
                                    0xFF))),
                    DockTextBrush =
                        new SolidColorBrush(
                            ParseThemeColor(
                                theme.TextColor,
                                Colors.White)),
                    Opacity =
                        effectiveOpacity,
                    BlurRadius =
                        effectiveBlurRadius,
                    GlassSurfaceEnabled =
                        theme.GlassSurfaceEnabled,
                    GlassTopColor =
                        ParseThemeColor(
                            theme.GlassTopColor,
                            Color.FromArgb(
                                0xD0,
                                0xE8,
                                0xF2,
                                0xEC)),
                    GlassBottomColor =
                        ParseThemeColor(
                            theme.GlassBottomColor,
                            Color.FromArgb(
                                0x80,
                                0xA8,
                                0xBE,
                                0xB4)),
                    GlassHighlightColor =
                        ParseThemeColor(
                            theme.GlassHighlightColor,
                            Color.FromArgb(
                                0xB0,
                                0xFF,
                                0xFF,
                                0xFF)),
                    GlassCornerRadius =
                        Math.Clamp(
                            theme.GlassCornerRadius,
                            0,
                            80),
                    GlassGradientReferenceHeight =
                        BaseExpandedThickness
                });
        }

        return appearances;
    }

    private void ApplyWidgetAppearance(
        IEnumerable<DockItem> items)
    {
        GlueDockWidgetAppearance appearance =
            CreateWidgetAppearance();

        foreach (DockItem item in items)
        {
            item.WidgetInstance?.ApplyHostAppearance(
                appearance);

            if (item.Children.Count > 0)
            {
                ApplyWidgetAppearance(
                    item.Children);
            }
        }
    }

    private void ApplyAppearance()
    {
        DockTheme theme =
            DockThemeService.Load(
                _settings.ThemeName);

        bool isMica =
            string.Equals(
                _settings.ThemeName,
                "Mica",
                StringComparison.OrdinalIgnoreCase);

        double effectiveBlurRadius =
            isMica
                ? 0
                : _settings.BlurRadius;

        if (_initialBackdropReady)
        {
            _nativeBackdropHost.SetBlur(
                effectiveBlurRadius);
        }

        Color expandedColor =
            ParseThemeColor(
                theme.DockBackgroundColor,
                Color.FromRgb(
                    0x12,
                    0x16,
                    0x1C));

        double effectiveOpacity =
            isMica
                ? 0.72 +
                  (Math.Clamp(
                      _settings.Opacity,
                      0,
                      1) * 0.28)
                : _settings.Opacity;

        byte expandedAlpha =
            (byte)Math.Clamp(
                Math.Round(
                    effectiveOpacity * 255),
                0,
                255);

        DebugLog.Write(
            "Material",
            $"Root; Theme={_settings.ThemeName}; Mica={isMica}; UserOpacity={_settings.Opacity:0.00}; EffectiveOpacity={effectiveOpacity:0.00}; UserBlur={_settings.BlurRadius:0.##}; EffectiveBlur={effectiveBlurRadius:0.##}");

        Resources["DockBackgroundBrush"] =
            new SolidColorBrush(
                Color.FromArgb(
                    expandedAlpha,
                    expandedColor.R,
                    expandedColor.G,
                    expandedColor.B));

        Resources["DockItemBackgroundBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBackgroundColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        Resources["DockItemBorderBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBorderColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        Resources["DockTextBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.TextColor,
                    Colors.White));

        Resources["DragGhostBackgroundBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.DragGhostBackgroundColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        Resources["DragGhostBorderBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.DragGhostBorderColor,
                    Color.FromArgb(
                        0x66,
                        0xFF,
                        0xFF,
                        0xFF)));

        if (_isExpanded)
        {
            double scale =
                Math.Clamp(
                    _settings.DockScale,
                    0.60,
                    2.00);

            double thicknessScale =
                Math.Clamp(
                    _settings.DockThicknessScale,
                    0.30,
                    1.50);

            double basePadding =
                10 * scale;

            double itemSpacing =
                Math.Clamp(
                    _settings.ItemSpacing,
                    -8,
                    42);

            double minimumContentThickness =
                (_settings.Edge is
                    DockEdge.Left or
                    DockEdge.Right
                    ? 58 + itemSpacing + 6
                    : _settings.ShowItemLabels
                        ? ItemSlotLength
                        : 56) *
                scale;

            double expandedThickness =
                Math.Max(
                    minimumContentThickness,
                    BaseExpandedThickness *
                    scale *
                    thicknessScale);

            double thicknessPadding =
                Math.Max(
                    0,
                    (expandedThickness -
                     minimumContentThickness) /
                    2.0);

            Thickness chromePadding =
                _settings.Edge is
                    DockEdge.Left or
                    DockEdge.Right
                    ? new Thickness(
                        thicknessPadding,
                        basePadding,
                        thicknessPadding,
                        basePadding)
                    : new Thickness(
                        basePadding,
                        thicknessPadding,
                        basePadding,
                        thicknessPadding);

            DockChrome.Padding =
                chromePadding;

            DockGlassSurface.Margin =
                new Thickness(
                    -chromePadding.Left,
                    -chromePadding.Top,
                    -chromePadding.Right,
                    -chromePadding.Bottom);

            DockGlassHighlight.Margin =
                DockGlassSurface.Margin;

            DockGlassFlames.Margin =
                DockGlassSurface.Margin;

            if (FindName("DockGlassStars") is Canvas dockGlassStars)
            {
                dockGlassStars.Margin =
                    DockGlassSurface.Margin;
            }

            ApplyGlassSurface(
                theme,
                effectiveOpacity,
                effectiveBlurRadius);

            DockChrome.Background =
                theme.GlassSurfaceEnabled
                    ? Brushes.Transparent
                    : new SolidColorBrush(
                        Color.FromArgb(
                            expandedAlpha,
                            expandedColor.R,
                            expandedColor.G,
                            expandedColor.B));

            if (_settings.DockBorderEnabled)
            {
                Color borderColor;

                try
                {
                    borderColor =
                        (Color)ColorConverter.ConvertFromString(
                            _settings.DockBorderColor);
                }
                catch
                {
                    borderColor =
                        Color.FromRgb(
                            0xFF,
                            0xFF,
                            0xFF);
                }

                if (theme.GlassSurfaceEnabled)
                {
                    LinearGradientBrush borderBrush =
                        new()
                        {
                            StartPoint =
                                new Point(
                                    0.5,
                                    0),
                            EndPoint =
                                new Point(
                                    0.5,
                                    1)
                        };

                    borderBrush.GradientStops.Add(
                        new GradientStop(
                            Color.FromArgb(
                                0xC8,
                                borderColor.R,
                                borderColor.G,
                                borderColor.B),
                            0));

                    borderBrush.GradientStops.Add(
                        new GradientStop(
                            Color.FromArgb(
                                0x78,
                                borderColor.R,
                                borderColor.G,
                                borderColor.B),
                            0.55));

                    borderBrush.GradientStops.Add(
                        new GradientStop(
                            Color.FromArgb(
                                0x28,
                                borderColor.R,
                                borderColor.G,
                                borderColor.B),
                            1));

                    DockChrome.BorderBrush =
                        borderBrush;
                }
                else
                {
                    DockChrome.BorderBrush =
                        new SolidColorBrush(
                            Color.FromArgb(
                                0xFF,
                                borderColor.R,
                                borderColor.G,
                                borderColor.B));
                }

                DockChrome.BorderThickness =
                    new Thickness(1);
            }
            else
            {
                DockChrome.BorderBrush =
                    Brushes.Transparent;

                DockChrome.BorderThickness =
                    new Thickness(0);
            }
        }
        else
        {
            ResetGlassSurface();

            Color collapsedColor;

            try
            {
                collapsedColor =
                    (Color)ColorConverter.ConvertFromString(
                        _settings.BarColor);
            }
            catch
            {
                collapsedColor =
                    Color.FromRgb(
                        0x12,
                        0x16,
                        0x1C);
            }

            byte collapsedAlpha =
                (byte)Math.Clamp(
                    Math.Round(
                        Math.Clamp(
                            _settings.CollapsedBarOpacity,
                            0,
                            1) * 255),
                    0,
                    255);

            DockChrome.Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        collapsedAlpha,
                        collapsedColor.R,
                        collapsedColor.G,
                        collapsedColor.B));

            DockChrome.BorderBrush =
                Brushes.Transparent;

            DockChrome.BorderThickness =
                new Thickness(0);
        }

        ApplyDockScale();
        UpdateWindowBounds();
    }

    private void ApplyGlassSurface(
        DockTheme theme,
        double opacity,
        double blurRadius)
    {
        if (!theme.GlassSurfaceEnabled)
        {
            ResetGlassSurface();
            return;
        }

        double cornerRadius =
            Math.Clamp(
                theme.GlassCornerRadius,
                0,
                80);

        DockChrome.CornerRadius =
            new CornerRadius(
                cornerRadius);

        DockGlassSurface.CornerRadius =
            new CornerRadius(
                cornerRadius);

        DockGlassHighlight.CornerRadius =
            new CornerRadius(
                cornerRadius);

        Color topColor =
            ParseThemeColor(
                theme.GlassTopColor,
                Color.FromArgb(
                    0xD0,
                    0xE8,
                    0xF2,
                    0xEC));

        Color bottomColor =
            ParseThemeColor(
                theme.GlassBottomColor,
                Color.FromArgb(
                    0x80,
                    0xA8,
                    0xBE,
                    0xB4));

        double gradientSurfaceWidth =
            Math.Max(
                DockGlassSurface.ActualWidth,
                DockChrome.ActualWidth);

        double gradientSurfaceHeight =
            Math.Max(
                DockGlassSurface.ActualHeight,
                DockChrome.ActualHeight);

        (Point gradientStartPoint, Point gradientEndPoint) =
            GetGlassGradientAnimationPoints(
                new Point(
                    theme.GlassGradientStartX,
                    theme.GlassGradientStartY),
                new Point(
                    theme.GlassGradientEndX,
                    theme.GlassGradientEndY),
                theme.GlassGradientAnimationAspectCorrect,
                gradientSurfaceWidth,
                gradientSurfaceHeight);

        string gradientAnimationSignature =
            CreateGlassGradientAnimationSignature(
                theme,
                topColor,
                bottomColor);

        LinearGradientBrush? existingSurfaceBrush =
            DockGlassSurface.Background
                as LinearGradientBrush;

        bool reuseGradientAnimation =
            theme.GlassGradientAnimationEnabled &&
            string.Equals(
                _glassGradientAnimationSignature,
                gradientAnimationSignature,
                StringComparison.Ordinal) &&
            existingSurfaceBrush is not null;

        LinearGradientBrush surfaceBrush;

        if (reuseGradientAnimation)
        {
            surfaceBrush =
                existingSurfaceBrush!;

            surfaceBrush.Opacity =
                Math.Clamp(
                    opacity,
                    0.10,
                    1.0);

            DebugLog.Write(
                "ThemeGradientAnimation",
                "Root REUSE; Existing animation kept running.");
        }
        else
        {
            surfaceBrush =
                new LinearGradientBrush(
                    topColor,
                    bottomColor,
                    gradientStartPoint,
                    gradientEndPoint)
                {
                    Opacity =
                        Math.Clamp(
                            opacity,
                            0.10,
                            1.0)
                };

        if (theme.GlassGradientAnimationEnabled)
        {
            Duration animationDuration =
                new(
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            theme.GlassGradientAnimationDurationSeconds,
                            0.1,
                            120)));

            if (string.Equals(
                    theme.GlassGradientAnimationMode,
                    "RotateTransform",
                    StringComparison.OrdinalIgnoreCase))
            {
                RotateTransform rotationTransform =
                    new(
                        0,
                        gradientSurfaceWidth / 2.0,
                        gradientSurfaceHeight / 2.0);

                surfaceBrush.Transform =
                    rotationTransform;

                DoubleAnimation rotationAnimation =
                    new(
                        0,
                        theme.GlassGradientAnimationRotationDegrees,
                        animationDuration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        EasingFunction =
                            CreateGlassGradientEasingFunction(
                                theme),
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                rotationTransform.BeginAnimation(
                    RotateTransform.AngleProperty,
                    rotationAnimation);
            }
            else if (theme.GlassGradientAnimationKeyFrames.Count >= 2)
            {
                PointAnimationUsingKeyFrames startPointAnimation =
                    new()
                    {
                        Duration =
                            animationDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                PointAnimationUsingKeyFrames endPointAnimation =
                    new()
                    {
                        Duration =
                            animationDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                foreach (DockThemeGradientKeyFrame keyFrame
                    in theme.GlassGradientAnimationKeyFrames
                        .OrderBy(
                            keyFrame =>
                                keyFrame.Progress))
                {
                    KeyTime keyTime =
                        KeyTime.FromPercent(
                            Math.Clamp(
                                keyFrame.Progress,
                                0,
                                1));

                    IEasingFunction? easingFunction =
                        CreateGlassGradientEasingFunction(
                            theme);

                    (Point keyFrameStartPoint, Point keyFrameEndPoint) =
                        GetGlassGradientAnimationPoints(
                            new Point(
                                keyFrame.StartX,
                                keyFrame.StartY),
                            new Point(
                                keyFrame.EndX,
                                keyFrame.EndY),
                            theme.GlassGradientAnimationAspectCorrect,
                            gradientSurfaceWidth,
                            gradientSurfaceHeight);

                    if (easingFunction is null)
                    {
                        startPointAnimation.KeyFrames.Add(
                            new LinearPointKeyFrame(
                                keyFrameStartPoint,
                                keyTime));

                        endPointAnimation.KeyFrames.Add(
                            new LinearPointKeyFrame(
                                keyFrameEndPoint,
                                keyTime));
                    }
                    else
                    {
                        startPointAnimation.KeyFrames.Add(
                            new EasingPointKeyFrame(
                                keyFrameStartPoint,
                                keyTime,
                                easingFunction));

                        endPointAnimation.KeyFrames.Add(
                            new EasingPointKeyFrame(
                                keyFrameEndPoint,
                                keyTime,
                                CreateGlassGradientEasingFunction(
                                    theme)));
                    }
                }

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.StartPointProperty,
                    startPointAnimation);

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.EndPointProperty,
                    endPointAnimation);
            }
            else
            {
                IEasingFunction? easingFunction =
                    CreateGlassGradientEasingFunction(
                        theme);

                (Point gradientTargetStartPoint, Point gradientTargetEndPoint) =
                    GetGlassGradientAnimationPoints(
                        new Point(
                            theme.GlassGradientAnimationToStartX,
                            theme.GlassGradientAnimationToStartY),
                        new Point(
                            theme.GlassGradientAnimationToEndX,
                            theme.GlassGradientAnimationToEndY),
                        theme.GlassGradientAnimationAspectCorrect,
                        gradientSurfaceWidth,
                        gradientSurfaceHeight);

                PointAnimation startPointAnimation =
                    new(
                        gradientStartPoint,
                        gradientTargetStartPoint,
                        animationDuration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        EasingFunction =
                            easingFunction,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                PointAnimation endPointAnimation =
                    new(
                        gradientEndPoint,
                        gradientTargetEndPoint,
                        animationDuration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        EasingFunction =
                            easingFunction,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.StartPointProperty,
                    startPointAnimation);

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.EndPointProperty,
                    endPointAnimation);
            }
        }

        _glassGradientAnimationSignature =
            theme.GlassGradientAnimationEnabled
                ? gradientAnimationSignature
                : null;
        }

        Color highlightColor =
            ParseThemeColor(
                theme.GlassHighlightColor,
                Color.FromArgb(
                    0xB0,
                    0xFF,
                    0xFF,
                    0xFF));

        LinearGradientBrush highlightBrush =
            new()
            {
                StartPoint =
                    new Point(
                        0.5,
                        0),
                EndPoint =
                    new Point(
                        0.5,
                        1)
            };

        highlightBrush.GradientStops.Add(
            new GradientStop(
                highlightColor,
                0));

        highlightBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    highlightColor.R,
                    highlightColor.G,
                    highlightColor.B),
                0.55));

        Color shadowColor =
            ParseThemeColor(
                theme.GlassShadowColor,
                Color.FromRgb(
                    0x0B,
                    0x17,
                    0x20));

        DockGlassSurface.Background =
            surfaceBrush;

        if (!reuseGradientAnimation)
        {
            StartGlassGradientDebugSampling(
                theme,
                surfaceBrush);
        }

        DockGlassSurface.Effect =
            null;

        DockGlassSurface.Visibility =
            Visibility.Visible;

        LinearGradientBrush frameBrush =
            new()
            {
                StartPoint =
                    new Point(
                        0.5,
                        0),
                EndPoint =
                    new Point(
                        0.5,
                        1)
            };

        frameBrush.GradientStops.Add(
            new GradientStop(
                highlightColor,
                0));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    (byte)(highlightColor.A * 0.35),
                    highlightColor.R,
                    highlightColor.G,
                    highlightColor.B),
                0.55));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    highlightColor.R,
                    highlightColor.G,
                    highlightColor.B),
                1));

        DockGlassHighlight.Background =
            highlightBrush;

        DockGlassHighlight.BorderBrush =
            frameBrush;

        DockGlassHighlight.BorderThickness =
            new Thickness(
                1);

        DockGlassHighlight.Visibility =
            Visibility.Visible;

        Dispatcher.BeginInvoke(
            () =>
            {
                if (!_isExpanded)
                {
                    return;
                }

                DockTheme currentTheme =
                    DockThemeService.Load(
                        _settings.ThemeName);

                ApplyGlassFlameEffect(
                    currentTheme);

                ApplyGlassStarEffect(
                    currentTheme);
            },
            DispatcherPriority.Loaded);

        DebugLog.Write(
            "AeroRender",
            $"Root; GlassSurfaceEnabled={theme.GlassSurfaceEnabled}; GlassTopColor={theme.GlassTopColor}; GlassBottomColor={theme.GlassBottomColor}; GlassHighlightColor={theme.GlassHighlightColor}; GlassCornerRadius={cornerRadius:0.##}; SurfaceOpacity={surfaceBrush.Opacity:0.##}; HighlightVisibility={DockGlassHighlight.Visibility}; BorderThickness={DockGlassHighlight.BorderThickness}; FrameStops={frameBrush.GradientStops.Count}; FrameStop0={frameBrush.GradientStops[0].Color}; FrameStop1={frameBrush.GradientStops[1].Color}; FrameStop2={frameBrush.GradientStops[2].Color}");
    }

    private void StartGlassGradientDebugSampling(
        DockTheme theme,
        LinearGradientBrush surfaceBrush)
    {
        StopGlassGradientDebugSampling();

        if (!DebugLog.IsEnabled ||
            !theme.GlassGradientAnimationEnabled)
        {
            return;
        }

        _glassGradientDebugSequence++;

        _glassGradientDebugLastRenderElapsedMilliseconds =
            0;

        _glassGradientDebugLastRenderVisualAngleDegrees =
            null;

        _glassGradientDebugRenderFrameIndex =
            0;

        _glassGradientDebugBrush =
            surfaceBrush;

        _glassGradientDebugStopwatch =
            Stopwatch.StartNew();

        _glassGradientDebugLastElapsedMilliseconds =
            0;

        _glassGradientDebugLastAngleDegrees =
            null;

        _glassGradientDebugLastVisualAngleDegrees =
            null;

        _glassGradientDebugDurationMilliseconds =
            Math.Clamp(
                theme.GlassGradientAnimationDurationSeconds,
                0.1,
                120) *
            1000.0;

        _glassGradientDebugKeyFrameProgresses =
            theme.GlassGradientAnimationKeyFrames
                .OrderBy(
                    keyFrame =>
                        keyFrame.Progress)
                .Select(
                    keyFrame =>
                        Math.Clamp(
                            keyFrame.Progress,
                            0,
                            1))
                .ToArray();

        string keyFrames =
            theme.GlassGradientAnimationKeyFrames.Count == 0
                ? "(none)"
                : string.Join(
                    " | ",
                    theme.GlassGradientAnimationKeyFrames
                        .OrderBy(
                            keyFrame =>
                                keyFrame.Progress)
                        .Select(
                            keyFrame =>
                                $"{keyFrame.Progress:0.######}:({keyFrame.StartX:0.######},{keyFrame.StartY:0.######})->({keyFrame.EndX:0.######},{keyFrame.EndY:0.######})"));

        DebugLog.Write(
            "ThemeGradientAnimation",
            $"Root START; Sequence={_glassGradientDebugSequence}; Theme={theme.Name}; Mode={theme.GlassGradientAnimationMode}; RotationDegrees={theme.GlassGradientAnimationRotationDegrees:0.###}; DurationSeconds={theme.GlassGradientAnimationDurationSeconds:0.###}; AutoReverse={theme.GlassGradientAnimationAutoReverse}; Easing={theme.GlassGradientAnimationEasing}; EasingMode={theme.GlassGradientAnimationEasingMode}; AspectCorrect={theme.GlassGradientAnimationAspectCorrect}; SurfaceSize=({DockGlassSurface.ActualWidth:0.###},{DockGlassSurface.ActualHeight:0.###}); Start=({theme.GlassGradientStartX:0.######},{theme.GlassGradientStartY:0.######}); End=({theme.GlassGradientEndX:0.######},{theme.GlassGradientEndY:0.######}); ToStart=({theme.GlassGradientAnimationToStartX:0.######},{theme.GlassGradientAnimationToStartY:0.######}); ToEnd=({theme.GlassGradientAnimationToEndX:0.######},{theme.GlassGradientAnimationToEndY:0.######}); KeyFrames={keyFrames}");

        _glassGradientDebugTimer =
            new DispatcherTimer(
                DispatcherPriority.Background,
                Dispatcher)
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        100)
            };

        _glassGradientDebugTimer.Tick +=
            GlassGradientDebugTimer_Tick;

        _glassGradientDebugTimer.Start();

        CompositionTarget.Rendering +=
            GlassGradientDebugRendering;
    }

    private void StopGlassGradientDebugSampling()
    {
        CompositionTarget.Rendering -=
            GlassGradientDebugRendering;

        if (_glassGradientDebugTimer is not null)
        {
            _glassGradientDebugTimer.Stop();

            _glassGradientDebugTimer.Tick -=
                GlassGradientDebugTimer_Tick;

            _glassGradientDebugTimer =
                null;
        }

        _glassGradientDebugStopwatch?.Stop();

        _glassGradientDebugStopwatch =
            null;

        _glassGradientDebugBrush =
            null;

        _glassGradientDebugLastElapsedMilliseconds =
            0;

        _glassGradientDebugLastAngleDegrees =
            null;

        _glassGradientDebugLastVisualAngleDegrees =
            null;

        _glassGradientDebugDurationMilliseconds =
            0;

        _glassGradientDebugKeyFrameProgresses =
            [];

        _glassGradientDebugLastRenderElapsedMilliseconds =
            0;

        _glassGradientDebugLastRenderVisualAngleDegrees =
            null;

        _glassGradientDebugRenderFrameIndex =
            0;
    }

    private void GlassGradientDebugRendering(
        object? sender,
        EventArgs e)
    {
        if (!DebugLog.IsEnabled)
        {
            StopGlassGradientDebugSampling();
            return;
        }

        if (_glassGradientDebugBrush is null ||
            _glassGradientDebugStopwatch is null)
        {
            StopGlassGradientDebugSampling();
            return;
        }

        long elapsedMilliseconds =
            _glassGradientDebugStopwatch.ElapsedMilliseconds;

        long deltaMilliseconds =
            elapsedMilliseconds -
            _glassGradientDebugLastRenderElapsedMilliseconds;

        Point startPoint =
            _glassGradientDebugBrush.StartPoint;

        Point endPoint =
            _glassGradientDebugBrush.EndPoint;

        double vectorX =
            endPoint.X -
            startPoint.X;

        double vectorY =
            endPoint.Y -
            startPoint.Y;

        double surfaceWidth =
            Math.Max(
                DockGlassSurface.ActualWidth,
                1.0);

        double surfaceHeight =
            Math.Max(
                DockGlassSurface.ActualHeight,
                1.0);

        Point visualStartPoint =
            new(
                startPoint.X *
                surfaceWidth,
                startPoint.Y *
                surfaceHeight);

        Point visualEndPoint =
            new(
                endPoint.X *
                surfaceWidth,
                endPoint.Y *
                surfaceHeight);

        if (_glassGradientDebugBrush.Transform is not null &&
            !_glassGradientDebugBrush.Transform.Value.IsIdentity)
        {
            visualStartPoint =
                _glassGradientDebugBrush.Transform.Transform(
                    visualStartPoint);

            visualEndPoint =
                _glassGradientDebugBrush.Transform.Transform(
                    visualEndPoint);
        }

        double visualVectorX =
            visualEndPoint.X -
            visualStartPoint.X;

        double visualVectorY =
            visualEndPoint.Y -
            visualStartPoint.Y;

        double visualVectorLength =
            Math.Sqrt(
                (visualVectorX * visualVectorX) +
                (visualVectorY * visualVectorY));

        double visualAngleDegrees =
            Math.Atan2(
                visualVectorY,
                visualVectorX) *
            180.0 /
            Math.PI;

        if (visualAngleDegrees < 0)
        {
            visualAngleDegrees +=
                360.0;
        }

        double? visualAngularDeltaDegrees =
            null;

        double? visualAngularSpeedDegreesPerSecond =
            null;

        if (_glassGradientDebugLastRenderVisualAngleDegrees.HasValue &&
            deltaMilliseconds > 0)
        {
            double visualDelta =
                visualAngleDegrees -
                _glassGradientDebugLastRenderVisualAngleDegrees.Value;

            while (visualDelta > 180.0)
            {
                visualDelta -=
                    360.0;
            }

            while (visualDelta < -180.0)
            {
                visualDelta +=
                    360.0;
            }

            visualAngularDeltaDegrees =
                visualDelta;

            visualAngularSpeedDegreesPerSecond =
                visualDelta /
                (deltaMilliseconds / 1000.0);
        }

        double cycleProgress =
            _glassGradientDebugDurationMilliseconds > 0
                ? (elapsedMilliseconds %
                    _glassGradientDebugDurationMilliseconds) /
                    _glassGradientDebugDurationMilliseconds
                : 0;

        TimeSpan? renderingTime =
            e is RenderingEventArgs renderingEventArgs
                ? renderingEventArgs.RenderingTime
                : null;

        _glassGradientDebugRenderFrameIndex++;

        DebugLog.Write(
            "ThemeGradientRender",
            $"Root RENDER; Sequence={_glassGradientDebugSequence}; Frame={_glassGradientDebugRenderFrameIndex}; RenderingTimeMs={(renderingTime.HasValue ? renderingTime.Value.TotalMilliseconds.ToString("0.###") : "n/a")}; ElapsedMs={elapsedMilliseconds}; DeltaMs={deltaMilliseconds}; CycleProgress={cycleProgress:0.######}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); Start=({startPoint.X:0.######},{startPoint.Y:0.######}); End=({endPoint.X:0.######},{endPoint.Y:0.######}); VisualVectorLength={visualVectorLength:0.######}; VisualAngleDegrees={visualAngleDegrees:0.###}; VisualAngularDeltaDegrees={(visualAngularDeltaDegrees.HasValue ? visualAngularDeltaDegrees.Value.ToString("0.###") : "n/a")}; VisualAngularSpeedDegreesPerSecond={(visualAngularSpeedDegreesPerSecond.HasValue ? visualAngularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}; HasAnimatedProperties={_glassGradientDebugBrush.HasAnimatedProperties}; SurfaceVisible={DockGlassSurface.IsVisible}; SurfaceOpacity={DockGlassSurface.Opacity:0.###}");

        _glassGradientDebugLastRenderElapsedMilliseconds =
            elapsedMilliseconds;

        _glassGradientDebugLastRenderVisualAngleDegrees =
            visualAngleDegrees;
    }

    private void GlassGradientDebugTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (!DebugLog.IsEnabled)
        {
            StopGlassGradientDebugSampling();
            return;
        }

        if (_glassGradientDebugBrush is null ||
            _glassGradientDebugStopwatch is null)
        {
            StopGlassGradientDebugSampling();
            return;
        }

        long elapsedMilliseconds =
            _glassGradientDebugStopwatch.ElapsedMilliseconds;

        long deltaMilliseconds =
            elapsedMilliseconds -
            _glassGradientDebugLastElapsedMilliseconds;

        Point startPoint =
            _glassGradientDebugBrush.StartPoint;

        Point endPoint =
            _glassGradientDebugBrush.EndPoint;

        double vectorX =
            endPoint.X -
            startPoint.X;

        double vectorY =
            endPoint.Y -
            startPoint.Y;

        double vectorLength =
            Math.Sqrt(
                (vectorX * vectorX) +
                (vectorY * vectorY));

        double angleDegrees =
            Math.Atan2(
                vectorY,
                vectorX) *
            180.0 /
            Math.PI;

        if (angleDegrees < 0)
        {
            angleDegrees +=
                360.0;
        }

        double surfaceWidth =
            Math.Max(
                DockGlassSurface.ActualWidth,
                1.0);

        double surfaceHeight =
            Math.Max(
                DockGlassSurface.ActualHeight,
                1.0);

        Point visualStartPoint =
            new(
                startPoint.X *
                surfaceWidth,
                startPoint.Y *
                surfaceHeight);

        Point visualEndPoint =
            new(
                endPoint.X *
                surfaceWidth,
                endPoint.Y *
                surfaceHeight);

        if (_glassGradientDebugBrush.Transform is not null &&
            !_glassGradientDebugBrush.Transform.Value.IsIdentity)
        {
            visualStartPoint =
                _glassGradientDebugBrush.Transform.Transform(
                    visualStartPoint);

            visualEndPoint =
                _glassGradientDebugBrush.Transform.Transform(
                    visualEndPoint);
        }

        double visualVectorX =
            visualEndPoint.X -
            visualStartPoint.X;

        double visualVectorY =
            visualEndPoint.Y -
            visualStartPoint.Y;

        double visualAngleDegrees =
            Math.Atan2(
                visualVectorY,
                visualVectorX) *
            180.0 /
            Math.PI;

        if (visualAngleDegrees < 0)
        {
            visualAngleDegrees +=
                360.0;
        }

        double? angularDeltaDegrees =
            null;

        double? angularSpeedDegreesPerSecond =
            null;

        if (_glassGradientDebugLastAngleDegrees.HasValue &&
            deltaMilliseconds > 0)
        {
            double delta =
                angleDegrees -
                _glassGradientDebugLastAngleDegrees.Value;

            while (delta > 180.0)
            {
                delta -=
                    360.0;
            }

            while (delta < -180.0)
            {
                delta +=
                    360.0;
            }

            angularDeltaDegrees =
                delta;

            angularSpeedDegreesPerSecond =
                delta /
                (deltaMilliseconds / 1000.0);
        }

        double? visualAngularDeltaDegrees =
            null;

        double? visualAngularSpeedDegreesPerSecond =
            null;

        if (_glassGradientDebugLastVisualAngleDegrees.HasValue &&
            deltaMilliseconds > 0)
        {
            double visualDelta =
                visualAngleDegrees -
                _glassGradientDebugLastVisualAngleDegrees.Value;

            while (visualDelta > 180.0)
            {
                visualDelta -=
                    360.0;
            }

            while (visualDelta < -180.0)
            {
                visualDelta +=
                    360.0;
            }

            visualAngularDeltaDegrees =
                visualDelta;

            visualAngularSpeedDegreesPerSecond =
                visualDelta /
                (deltaMilliseconds / 1000.0);
        }

        double cycleProgress =
            _glassGradientDebugDurationMilliseconds > 0
                ? (elapsedMilliseconds %
                    _glassGradientDebugDurationMilliseconds) /
                    _glassGradientDebugDurationMilliseconds
                : 0;

        string activeSegment =
            "direct";

        if (_glassGradientDebugKeyFrameProgresses.Length >= 2)
        {
            int segmentIndex =
                _glassGradientDebugKeyFrameProgresses.Length -
                2;

            for (int index = 0;
                 index <
                 _glassGradientDebugKeyFrameProgresses.Length - 1;
                 index++)
            {
                if (cycleProgress <=
                    _glassGradientDebugKeyFrameProgresses[index + 1])
                {
                    segmentIndex =
                        index;

                    break;
                }
            }

            activeSegment =
                $"{segmentIndex}:{_glassGradientDebugKeyFrameProgresses[segmentIndex]:0.######}->{_glassGradientDebugKeyFrameProgresses[segmentIndex + 1]:0.######}";
        }

        DebugLog.Write(
            "ThemeGradientAnimation",
            $"Root SAMPLE; Sequence={_glassGradientDebugSequence}; ElapsedMs={elapsedMilliseconds}; DeltaMs={deltaMilliseconds}; CycleProgress={cycleProgress:0.######}; Segment={activeSegment}; SurfaceSize=({DockGlassSurface.ActualWidth:0.###},{DockGlassSurface.ActualHeight:0.###}); Start=({startPoint.X:0.######},{startPoint.Y:0.######}); End=({endPoint.X:0.######},{endPoint.Y:0.######}); VectorLength={vectorLength:0.######}; AngleDegrees={angleDegrees:0.###}; AngularDeltaDegrees={(angularDeltaDegrees.HasValue ? angularDeltaDegrees.Value.ToString("0.###") : "n/a")}; AngularSpeedDegreesPerSecond={(angularSpeedDegreesPerSecond.HasValue ? angularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}; VisualAngleDegrees={visualAngleDegrees:0.###}; VisualAngularDeltaDegrees={(visualAngularDeltaDegrees.HasValue ? visualAngularDeltaDegrees.Value.ToString("0.###") : "n/a")}; VisualAngularSpeedDegreesPerSecond={(visualAngularSpeedDegreesPerSecond.HasValue ? visualAngularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}");

        _glassGradientDebugLastElapsedMilliseconds =
            elapsedMilliseconds;

        _glassGradientDebugLastAngleDegrees =
            angleDegrees;

        _glassGradientDebugLastVisualAngleDegrees =
            visualAngleDegrees;
    }

    private static string CreateGlassGradientAnimationSignature(
        DockTheme theme,
        Color topColor,
        Color bottomColor)
    {
        string keyFrameSignature =
            string.Join(
                ";",
                theme.GlassGradientAnimationKeyFrames
                    .OrderBy(
                        keyFrame =>
                            keyFrame.Progress)
                    .Select(
                        keyFrame =>
                            string.Join(
                                ",",
                                keyFrame.Progress.ToString(
                                    "R",
                                    System.Globalization.CultureInfo.InvariantCulture),
                                keyFrame.StartX.ToString(
                                    "R",
                                    System.Globalization.CultureInfo.InvariantCulture),
                                keyFrame.StartY.ToString(
                                    "R",
                                    System.Globalization.CultureInfo.InvariantCulture),
                                keyFrame.EndX.ToString(
                                    "R",
                                    System.Globalization.CultureInfo.InvariantCulture),
                                keyFrame.EndY.ToString(
                                    "R",
                                    System.Globalization.CultureInfo.InvariantCulture))));

        return string.Join(
            "|",
            theme.Name,
            topColor.ToString(),
            bottomColor.ToString(),
            theme.GlassGradientStartX.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientStartY.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientEndX.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientEndY.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationEnabled.ToString(),
            theme.GlassGradientAnimationDurationSeconds.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationAutoReverse.ToString(),
            theme.GlassGradientAnimationEasing,
            theme.GlassGradientAnimationEasingMode,
            theme.GlassGradientAnimationMode,
            theme.GlassGradientAnimationRotationDegrees.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationAspectCorrect.ToString(),
            theme.GlassGradientAnimationToStartX.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationToStartY.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationToEndX.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            theme.GlassGradientAnimationToEndY.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            keyFrameSignature);
    }

    private static (Point StartPoint, Point EndPoint) GetGlassGradientAnimationPoints(
        Point startPoint,
        Point endPoint,
        bool aspectCorrect,
        double surfaceWidth,
        double surfaceHeight)
    {
        if (!aspectCorrect ||
            surfaceWidth <= 0 ||
            surfaceHeight <= 0)
        {
            return (
                startPoint,
                endPoint);
        }

        double vectorX =
            endPoint.X -
            startPoint.X;

        double vectorY =
            endPoint.Y -
            startPoint.Y;

        double vectorLength =
            Math.Sqrt(
                (vectorX * vectorX) +
                (vectorY * vectorY));

        if (vectorLength <=
            double.Epsilon)
        {
            return (
                startPoint,
                endPoint);
        }

        double directionX =
            vectorX /
            vectorLength;

        double directionY =
            vectorY /
            vectorLength;

        double projectedSurfaceLength =
            (Math.Abs(directionX) *
             surfaceWidth) +
            (Math.Abs(directionY) *
             surfaceHeight);

        double correctedVectorX =
            projectedSurfaceLength *
            directionX /
            surfaceWidth;

        double correctedVectorY =
            projectedSurfaceLength *
            directionY /
            surfaceHeight;

        double centerX =
            (startPoint.X +
             endPoint.X) /
            2.0;

        double centerY =
            (startPoint.Y +
             endPoint.Y) /
            2.0;

        return (
            new Point(
                centerX -
                (correctedVectorX / 2.0),
                centerY -
                (correctedVectorY / 2.0)),
            new Point(
                centerX +
                (correctedVectorX / 2.0),
                centerY +
                (correctedVectorY / 2.0)));
    }

    private static IEasingFunction? CreateGlassGradientEasingFunction(
        DockTheme theme)
    {
        EasingFunctionBase? easingFunction =
            theme.GlassGradientAnimationEasing
                .Trim()
                .ToLowerInvariant() switch
                {
                    "" => null,
                    "linear" => null,
                    "back" => new BackEase(),
                    "bounce" => new BounceEase(),
                    "circle" => new CircleEase(),
                    "cubic" => new CubicEase(),
                    "elastic" => new ElasticEase(),
                    "exponential" => new ExponentialEase(),
                    "power" => new PowerEase(),
                    "quadratic" => new QuadraticEase(),
                    "quartic" => new QuarticEase(),
                    "quintic" => new QuinticEase(),
                    "sine" => new SineEase(),
                    _ => null
                };

        if (easingFunction == null)
        {
            return null;
        }

        if (Enum.TryParse(
                theme.GlassGradientAnimationEasingMode,
                true,
                out EasingMode easingMode))
        {
            easingFunction.EasingMode =
                easingMode;
        }
        else
        {
            easingFunction.EasingMode =
                EasingMode.EaseInOut;
        }

        return easingFunction;
    }

    private void DockGlassStars_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 1 ||
            e.NewSize.Height <= 1 ||
            sender is not Canvas starCanvas ||
            starCanvas.Visibility != Visibility.Visible)
        {
            return;
        }

        DockTheme theme =
            DockThemeService.Load(
                _settings.ThemeName);

        if (!theme.GlassStarEffectEnabled ||
            theme.GlassStarLayers.Count == 0)
        {
            return;
        }

        ApplyGlassStarEffect(
            theme);
    }

    private void ApplyGlassStarEffect(
        DockTheme theme)
    {
        if (FindName("DockGlassStars") is not Canvas starCanvas)
        {
            return;
        }

        if (!theme.GlassStarEffectEnabled ||
            theme.GlassStarLayers.Count == 0)
        {
            starCanvas.Children.Clear();
            starCanvas.Visibility =
                Visibility.Collapsed;

            _glassStarAnimationSignature =
                null;

            return;
        }

        double surfaceWidth =
            Math.Max(
                starCanvas.ActualWidth,
                DockChrome.ActualWidth);

        double surfaceHeight =
            Math.Max(
                starCanvas.ActualHeight,
                DockChrome.ActualHeight);

        if (surfaceWidth <= 1 ||
            surfaceHeight <= 1)
        {
            return;
        }

        string starAnimationSignature =
            string.Join(
                "|",
                theme.GlassStarLayers.Select(
                    star =>
                        $"{star.X:0.####},{star.Y:0.####},{star.Size:0.####},{star.Color},{star.MinimumOpacity:0.####},{star.MaximumOpacity:0.####},{star.DurationSeconds:0.####},{star.DelaySeconds:0.####},{star.GlowRadius:0.####},{star.MinimumScale:0.####},{star.MaximumScale:0.####},{star.DriftXRatio:0.####},{star.DriftYRatio:0.####}")) +
            $"|{surfaceWidth:0.##}x{surfaceHeight:0.##}";

        if (string.Equals(
                _glassStarAnimationSignature,
                starAnimationSignature,
                StringComparison.Ordinal) &&
            starCanvas.Visibility ==
                Visibility.Visible &&
            starCanvas.Children.Count ==
                theme.GlassStarLayers.Count)
        {
            return;
        }

        starCanvas.Children.Clear();

        foreach (DockThemeStarLayer star
            in theme.GlassStarLayers)
        {
            double size =
                Math.Clamp(
                    star.Size,
                    0.5,
                    12);

            Color starColor =
                ParseThemeColor(
                    star.Color,
                    Colors.White);

            RadialGradientBrush starBrush =
                new()
                {
                    Center =
                        new Point(
                            0.5,
                            0.5),
                    GradientOrigin =
                        new Point(
                            0.5,
                            0.5),
                    RadiusX =
                        0.5,
                    RadiusY =
                        0.5
                };

            starBrush.GradientStops.Add(
                new GradientStop(
                    starColor,
                    0));

            starBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        (byte)Math.Round(
                            starColor.A * 0.62),
                        starColor.R,
                        starColor.G,
                        starColor.B),
                    0.34));

            starBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        0,
                        starColor.R,
                        starColor.G,
                        starColor.B),
                    1));

            System.Windows.Shapes.Ellipse starElement =
                new()
                {
                    Width =
                        size,
                    Height =
                        size,
                    Fill =
                        starBrush,
                    Opacity =
                        Math.Clamp(
                            star.MinimumOpacity,
                            0,
                            1),
                    RenderTransformOrigin =
                        new Point(
                            0.5,
                            0.5),
                    IsHitTestVisible =
                        false
                };

            double glowRadius =
                Math.Clamp(
                    star.GlowRadius,
                    0,
                    16);

            if (glowRadius > 0)
            {
                starElement.Effect =
                    new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius =
                            glowRadius
                    };
            }

            double minimumScale =
                Math.Clamp(
                    star.MinimumScale,
                    0.2,
                    2.5);

            double maximumScale =
                Math.Clamp(
                    star.MaximumScale,
                    minimumScale,
                    3.0);

            ScaleTransform scaleTransform =
                new(
                    minimumScale,
                    minimumScale);

            TranslateTransform translateTransform =
                new();

            TransformGroup transformGroup =
                new();

            transformGroup.Children.Add(
                scaleTransform);

            transformGroup.Children.Add(
                translateTransform);

            starElement.RenderTransform =
                transformGroup;

            Canvas.SetLeft(
                starElement,
                (surfaceWidth *
                 Math.Clamp(
                     star.X,
                     0,
                     1)) -
                (size / 2));

            Canvas.SetTop(
                starElement,
                (surfaceHeight *
                 Math.Clamp(
                     star.Y,
                     0,
                     1)) -
                (size / 2));

            Duration duration =
                new(
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            star.DurationSeconds,
                            0.6,
                            30)));

            TimeSpan beginTime =
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        star.DelaySeconds,
                        0,
                        30));

            double minimumOpacity =
                Math.Clamp(
                    star.MinimumOpacity,
                    0,
                    1);

            double maximumOpacity =
                Math.Clamp(
                    star.MaximumOpacity,
                    minimumOpacity,
                    1);

            DoubleAnimation opacityAnimation =
                new(
                    minimumOpacity,
                    maximumOpacity,
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                };

            starElement.BeginAnimation(
                UIElement.OpacityProperty,
                opacityAnimation);

            DoubleAnimation scaleAnimation =
                new(
                    minimumScale,
                    maximumScale,
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                };

            scaleTransform.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                scaleAnimation);

            scaleTransform.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                scaleAnimation);

            double driftX =
                surfaceWidth *
                Math.Clamp(
                    star.DriftXRatio,
                    -0.10,
                    0.10);

            double driftY =
                surfaceHeight *
                Math.Clamp(
                    star.DriftYRatio,
                    -0.10,
                    0.10);

            if (Math.Abs(driftX) > 0.001)
            {
                DoubleAnimation driftXAnimation =
                    new(
                        0,
                        driftX,
                        duration)
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                translateTransform.BeginAnimation(
                    TranslateTransform.XProperty,
                    driftXAnimation);
            }

            if (Math.Abs(driftY) > 0.001)
            {
                DoubleAnimation driftYAnimation =
                    new(
                        0,
                        driftY,
                        duration)
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                translateTransform.BeginAnimation(
                    TranslateTransform.YProperty,
                    driftYAnimation);
            }

            starCanvas.Children.Add(
                starElement);
        }

        starCanvas.Visibility =
            Visibility.Visible;

        _glassStarAnimationSignature =
            starAnimationSignature;
    }

    private void DockGlassFlames_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 1 ||
            e.NewSize.Height <= 1 ||
            DockGlassFlames.Visibility != Visibility.Visible)
        {
            return;
        }

        DockTheme theme =
            DockThemeService.Load(
                _settings.ThemeName);

        if (!theme.GlassFlameEffectEnabled ||
            theme.GlassFlameLayers.Count == 0)
        {
            return;
        }

        ApplyGlassFlameEffect(
            theme);
    }

    private void ApplyGlassFlameEffect(
        DockTheme theme)
    {
        Canvas flameCanvas =
            DockGlassFlames;

        if (!theme.GlassFlameEffectEnabled ||
            theme.GlassFlameLayers.Count == 0)
        {
            StopNaturalFireHeatField();

            flameCanvas.Children.Clear();
            flameCanvas.Visibility =
                Visibility.Collapsed;

            _glassFlameAnimationSignature =
                null;

            return;
        }

        double surfaceWidth =
            flameCanvas.ActualWidth > 1
                ? flameCanvas.ActualWidth
                : DockChrome.ActualWidth;

        double surfaceHeight =
            flameCanvas.ActualHeight > 1
                ? flameCanvas.ActualHeight
                : DockChrome.ActualHeight;

        if (surfaceWidth <= 1)
        {
            surfaceWidth = 320;
        }

        if (surfaceHeight <= 1)
        {
            surfaceHeight = 88;
        }

        bool naturalFireHeatField =
            theme.GlassFlameLayers.All(
                layer =>
                    string.Equals(
                        layer.MotionMode,
                        "NaturalFire",
                        StringComparison.OrdinalIgnoreCase));

        string flameAnimationSignature =
            $"{theme.GlassFlameBaseGlowEnabled},{theme.GlassFlameBaseGlowColor},{theme.GlassFlameBaseGlowHeightRatio:0.####},{theme.GlassFlameBaseGlowOpacity:0.####},{theme.GlassFlameTopFadeStartRatio:0.####},{theme.GlassFlameTopFadeEndRatio:0.####}|" +
            string.Join(
                "|",
                theme.GlassFlameLayers.Select(
                    layer =>
                        $"{layer.X:0.####},{layer.WidthRatio:0.####},{layer.HeightRatio:0.####},{layer.RiseRatio:0.####},{layer.DriftRatio:0.####},{layer.DurationSeconds:0.####},{layer.DelaySeconds:0.####},{layer.Opacity:0.####},{layer.TipColor},{layer.MidColor},{layer.CoreColor},{layer.MotionMode},{layer.BaseYRatio:0.####},{layer.MinimumScaleX:0.####},{layer.MaximumScaleX:0.####},{layer.MinimumScaleY:0.####},{layer.MaximumScaleY:0.####},{layer.SwayDegrees:0.####},{layer.FlickerRatio:0.####},{layer.BlurRadius:0.####},{layer.TipStop:0.####},{layer.MidStop:0.####},{layer.CoreStop:0.####}")) +
            $"|{surfaceWidth:0.##}x{surfaceHeight:0.##}";

        if (naturalFireHeatField)
        {
            ApplyNaturalFireHeatField(
                theme,
                flameCanvas,
                surfaceWidth,
                surfaceHeight,
                flameAnimationSignature);

            return;
        }

        StopNaturalFireHeatField();

        int expectedChildCount =
            theme.GlassFlameLayers.Count +
            (theme.GlassFlameBaseGlowEnabled
                ? 1
                : 0);

        if (string.Equals(
                _glassFlameAnimationSignature,
                flameAnimationSignature,
                StringComparison.Ordinal) &&
            flameCanvas.Visibility ==
                Visibility.Visible &&
            flameCanvas.Children.Count ==
                expectedChildCount)
        {
            return;
        }

        flameCanvas.Children.Clear();

        double topFadeStartRatio =
            Math.Clamp(
                theme.GlassFlameTopFadeStartRatio,
                0,
                0.95);

        double topFadeEndRatio =
            Math.Clamp(
                theme.GlassFlameTopFadeEndRatio,
                topFadeStartRatio + 0.01,
                1);

        LinearGradientBrush flameOpacityMask =
            new()
            {
                StartPoint =
                    new Point(
                        0.5,
                        0),
                EndPoint =
                    new Point(
                        0.5,
                        1)
            };

        flameOpacityMask.GradientStops.Add(
            new GradientStop(
                Colors.Transparent,
                0));

        flameOpacityMask.GradientStops.Add(
            new GradientStop(
                Colors.Transparent,
                topFadeStartRatio));

        flameOpacityMask.GradientStops.Add(
            new GradientStop(
                Colors.White,
                topFadeEndRatio));

        flameOpacityMask.GradientStops.Add(
            new GradientStop(
                Colors.White,
                1));

        flameCanvas.OpacityMask =
            flameOpacityMask;

        DebugLog.Write(
            "ThemeFlame",
            $"Root MASK; Theme={theme.Name}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); TopFadeStartRatio={topFadeStartRatio:0.###}; TopFadeEndRatio={topFadeEndRatio:0.###}; ClipToBounds={flameCanvas.ClipToBounds}");

        if (theme.GlassFlameBaseGlowEnabled)
        {
            double glowHeight =
                surfaceHeight *
                Math.Clamp(
                    theme.GlassFlameBaseGlowHeightRatio,
                    0.02,
                    1);

            Color baseGlowColor =
                ParseThemeColor(
                    theme.GlassFlameBaseGlowColor,
                    Color.FromArgb(
                        0x80,
                        0xFF,
                        0x5A,
                        0x00));

            LinearGradientBrush baseGlowBrush =
                new()
                {
                    StartPoint =
                        new Point(
                            0.5,
                            0),
                    EndPoint =
                        new Point(
                            0.5,
                            1)
                };

            baseGlowBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        0,
                        baseGlowColor.R,
                        baseGlowColor.G,
                        baseGlowColor.B),
                    0));

            baseGlowBrush.GradientStops.Add(
                new GradientStop(
                    baseGlowColor,
                    1));

            System.Windows.Shapes.Rectangle baseGlow =
                new()
                {
                    Width =
                        surfaceWidth,
                    Height =
                        glowHeight,
                    Fill =
                        baseGlowBrush,
                    Opacity =
                        Math.Clamp(
                            theme.GlassFlameBaseGlowOpacity,
                            0,
                            1),
                    IsHitTestVisible =
                        false
                };

            Canvas.SetLeft(
                baseGlow,
                0);

            Canvas.SetTop(
                baseGlow,
                surfaceHeight -
                glowHeight);

            flameCanvas.Children.Add(
                baseGlow);
        }

        foreach (DockThemeFlameLayer layer
            in theme.GlassFlameLayers)
        {
            double flameWidth =
                Math.Max(
                    6,
                    surfaceWidth *
                    Math.Clamp(
                        layer.WidthRatio,
                        0.005,
                        0.50));

            double flameHeight =
                Math.Max(
                    12,
                    surfaceHeight *
                    Math.Clamp(
                        layer.HeightRatio,
                        0.08,
                        2.50));

            double riseDistance =
                surfaceHeight *
                Math.Clamp(
                    layer.RiseRatio,
                    0,
                    1.50);

            double driftDistance =
                surfaceWidth *
                Math.Clamp(
                    layer.DriftRatio,
                    0,
                    0.25);

            double durationSeconds =
                Math.Clamp(
                    layer.DurationSeconds,
                    0.4,
                    20);

            double delaySeconds =
                Math.Clamp(
                    layer.DelaySeconds,
                    0,
                    20);

            Color tipColor =
                ParseThemeColor(
                    layer.TipColor,
                    Color.FromArgb(
                        0,
                        0xFF,
                        0x32,
                        0x00));

            Color midColor =
                ParseThemeColor(
                    layer.MidColor,
                    Color.FromArgb(
                        0xD8,
                        0xFF,
                        0x68,
                        0x00));

            Color coreColor =
                ParseThemeColor(
                    layer.CoreColor,
                    Color.FromArgb(
                        0xFF,
                        0xFF,
                        0xD4,
                        0x58));

            double tipStop =
                Math.Clamp(
                    layer.TipStop,
                    0,
                    1);

            double midStop =
                Math.Clamp(
                    layer.MidStop,
                    tipStop,
                    1);

            double coreStop =
                Math.Clamp(
                    layer.CoreStop,
                    midStop,
                    1);

            LinearGradientBrush flameBrush =
                new()
                {
                    StartPoint =
                        new Point(
                            0.5,
                            0),
                    EndPoint =
                        new Point(
                            0.5,
                            1)
                };

            flameBrush.GradientStops.Add(
                new GradientStop(
                    tipColor,
                    tipStop));

            flameBrush.GradientStops.Add(
                new GradientStop(
                    midColor,
                    midStop));

            flameBrush.GradientStops.Add(
                new GradientStop(
                    coreColor,
                    coreStop));

            bool lickMotion =
                string.Equals(
                    layer.MotionMode,
                    "Lick",
                    StringComparison.OrdinalIgnoreCase);

            bool fireplaceMotion =
                string.Equals(
                    layer.MotionMode,
                    "Fireplace",
                    StringComparison.OrdinalIgnoreCase);

            bool naturalFireMotion =
                string.Equals(
                    layer.MotionMode,
                    "NaturalFire",
                    StringComparison.OrdinalIgnoreCase);

            System.Windows.Shapes.Path flame =
                new()
                {
                    Width =
                        flameWidth,
                    Height =
                        flameHeight,
                    Stretch =
                        Stretch.Fill,
                    Fill =
                        flameBrush,
                    Data =
                        Geometry.Parse(
                            naturalFireMotion
                                ? "M 50,0 C 61,18 78,54 72,79 C 70,90 63,98 54,100 L 46,100 C 37,98 30,90 28,79 C 22,54 39,18 50,0 Z"
                                : fireplaceMotion
                                    ? "M 50,0 C 61,11 66,24 58,36 C 74,48 73,63 63,74 C 70,86 65,96 57,100 L 41,100 C 31,95 27,83 36,71 C 26,59 29,44 40,34 C 35,22 41,10 50,0 Z"
                                    : lickMotion
                                        ? "M 50,0 C 57,13 70,25 63,42 C 76,56 72,75 60,100 L 40,100 C 28,76 27,59 37,44 C 31,28 43,14 50,0 Z"
                                        : "M 50,0 C 62,18 78,31 68,49 C 84,63 80,82 60,100 L 40,100 C 20,82 16,63 32,49 C 22,31 38,18 50,0 Z"),
                    Opacity =
                        lickMotion ||
                        fireplaceMotion ||
                        naturalFireMotion
                            ? Math.Clamp(
                                layer.Opacity *
                                (1 -
                                 (Math.Clamp(
                                     layer.FlickerRatio,
                                     0,
                                     0.9) *
                                  0.5)),
                                0,
                                1)
                            : 0,
                    RenderTransformOrigin =
                        new Point(
                            0.5,
                            1),
                    IsHitTestVisible =
                        false
                };

            double blurRadius =
                Math.Clamp(
                    layer.BlurRadius,
                    0,
                    20);

            if (blurRadius > 0)
            {
                flame.Effect =
                    new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius =
                            blurRadius
                    };
            }

            double minimumScaleX =
                Math.Clamp(
                    layer.MinimumScaleX,
                    0.20,
                    2.50);

            double maximumScaleX =
                Math.Clamp(
                    layer.MaximumScaleX,
                    minimumScaleX,
                    2.50);

            double minimumScaleY =
                Math.Clamp(
                    layer.MinimumScaleY,
                    0.15,
                    2.50);

            double maximumScaleY =
                Math.Clamp(
                    layer.MaximumScaleY,
                    minimumScaleY,
                    2.50);

            ScaleTransform scaleTransform =
                lickMotion ||
                fireplaceMotion ||
                naturalFireMotion
                    ? new ScaleTransform(
                        minimumScaleX,
                        minimumScaleY)
                    : new ScaleTransform(
                        0.86,
                        0.94);

            RotateTransform rotateTransform =
                new();

            TranslateTransform translateTransform =
                new();

            TransformGroup transformGroup =
                new();

            transformGroup.Children.Add(
                scaleTransform);

            if (lickMotion ||
                fireplaceMotion ||
                naturalFireMotion)
            {
                transformGroup.Children.Add(
                    rotateTransform);
            }

            transformGroup.Children.Add(
                translateTransform);

            flame.RenderTransform =
                transformGroup;

            if (fireplaceMotion ||
                naturalFireMotion)
            {
                double requestedCenterX =
                    surfaceWidth *
                    Math.Clamp(
                        layer.X,
                        0,
                        1);

                double maximumRenderedHalfWidth =
                    (flameWidth / 2) *
                    maximumScaleX;

                double minimumCenterX =
                    maximumRenderedHalfWidth +
                    driftDistance;

                double maximumCenterX =
                    surfaceWidth -
                    maximumRenderedHalfWidth -
                    driftDistance;

                double flameCenterX =
                    minimumCenterX <= maximumCenterX
                        ? Math.Clamp(
                            requestedCenterX,
                            minimumCenterX,
                            maximumCenterX)
                        : surfaceWidth / 2;

                Canvas.SetLeft(
                    flame,
                    flameCenterX -
                    (flameWidth / 2));
            }
            else
            {
                Canvas.SetLeft(
                    flame,
                    (surfaceWidth *
                     Math.Clamp(
                         layer.X,
                         0,
                         1)) -
                    (flameWidth / 2));
            }

            Canvas.SetTop(
                flame,
                lickMotion ||
                fireplaceMotion ||
                naturalFireMotion
                    ? (surfaceHeight *
                       Math.Clamp(
                           layer.BaseYRatio,
                           0,
                           fireplaceMotion ||
                           naturalFireMotion
                               ? 1
                               : 1.20)) -
                      flameHeight
                    : surfaceHeight -
                      (flameHeight * 0.92));

            Duration flameDuration =
                new(
                    TimeSpan.FromSeconds(
                        durationSeconds));

            TimeSpan beginTime =
                TimeSpan.FromSeconds(
                    delaySeconds);

            if (naturalFireMotion)
            {
                double maximumOpacity =
                    Math.Clamp(
                        layer.Opacity,
                        0,
                        1);

                double flickerRatio =
                    Math.Clamp(
                        layer.FlickerRatio,
                        0,
                        0.9);

                double effectiveRiseDistance =
                    Math.Max(
                        riseDistance,
                        flameHeight * 0.65);

                DoubleAnimationUsingKeyFrames translateYAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                translateYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            0),
                        new KeySpline(
                            0.22,
                            0.0,
                            0.35,
                            1.0)));

                translateYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        -effectiveRiseDistance *
                        0.16,
                        KeyTime.FromPercent(
                            0.22),
                        new KeySpline(
                            0.22,
                            0.0,
                            0.35,
                            1.0)));

                translateYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        -effectiveRiseDistance *
                        0.54,
                        KeyTime.FromPercent(
                            0.58),
                        new KeySpline(
                            0.22,
                            0.0,
                            0.35,
                            1.0)));

                translateYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        -effectiveRiseDistance,
                        KeyTime.FromPercent(
                            1),
                        new KeySpline(
                            0.22,
                            0.0,
                            0.35,
                            1.0)));

                translateTransform.BeginAnimation(
                    TranslateTransform.YProperty,
                    translateYAnimation);

                double swayDistance =
                    Math.Max(
                        driftDistance,
                        flameWidth * 0.18);

                DoubleAnimationUsingKeyFrames translateXAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                translateXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            0),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                translateXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        swayDistance *
                        0.42,
                        KeyTime.FromPercent(
                            0.24),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                translateXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        -swayDistance *
                        0.68,
                        KeyTime.FromPercent(
                            0.53),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                translateXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        swayDistance,
                        KeyTime.FromPercent(
                            0.79),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                translateXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        swayDistance *
                        0.18,
                        KeyTime.FromPercent(
                            1),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                translateTransform.BeginAnimation(
                    TranslateTransform.XProperty,
                    translateXAnimation);

                DoubleAnimationUsingKeyFrames scaleXAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumScaleX *
                        0.78,
                        KeyTime.FromPercent(
                            0),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumScaleX,
                        KeyTime.FromPercent(
                            0.14),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        minimumScaleX +
                        ((maximumScaleX -
                          minimumScaleX) *
                         0.42),
                        KeyTime.FromPercent(
                            0.52),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        minimumScaleX *
                        0.52,
                        KeyTime.FromPercent(
                            0.82),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleXAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        Math.Max(
                            0.12,
                            minimumScaleX *
                            0.24),
                        KeyTime.FromPercent(
                            1),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                DoubleAnimationUsingKeyFrames scaleYAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        minimumScaleY *
                        0.72,
                        KeyTime.FromPercent(
                            0),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumScaleY,
                        KeyTime.FromPercent(
                            0.24),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        minimumScaleY +
                        ((maximumScaleY -
                          minimumScaleY) *
                         0.58),
                        KeyTime.FromPercent(
                            0.63),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleYAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        minimumScaleY *
                        0.48,
                        KeyTime.FromPercent(
                            1),
                        new KeySpline(
                            0.25,
                            0.1,
                            0.25,
                            1)));

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                double swayDegrees =
                    Math.Clamp(
                        layer.SwayDegrees,
                        0,
                        35);

                if (swayDegrees > 0)
                {
                    DoubleAnimationUsingKeyFrames swayAnimation =
                        new()
                        {
                            BeginTime =
                                beginTime,
                            Duration =
                                flameDuration,
                            RepeatBehavior =
                                RepeatBehavior.Forever
                        };

                    swayAnimation.KeyFrames.Add(
                        new SplineDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                0),
                            new KeySpline(
                                0.42,
                                0,
                                0.58,
                                1)));

                    swayAnimation.KeyFrames.Add(
                        new SplineDoubleKeyFrame(
                            swayDegrees *
                            0.34,
                            KeyTime.FromPercent(
                                0.26),
                            new KeySpline(
                                0.42,
                                0,
                                0.58,
                                1)));

                    swayAnimation.KeyFrames.Add(
                        new SplineDoubleKeyFrame(
                            -swayDegrees *
                            0.58,
                            KeyTime.FromPercent(
                                0.57),
                            new KeySpline(
                                0.42,
                                0,
                                0.58,
                                1)));

                    swayAnimation.KeyFrames.Add(
                        new SplineDoubleKeyFrame(
                            swayDegrees,
                            KeyTime.FromPercent(
                                0.82),
                            new KeySpline(
                                0.42,
                                0,
                                0.58,
                                1)));

                    swayAnimation.KeyFrames.Add(
                        new SplineDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                1),
                            new KeySpline(
                                0.42,
                                0,
                                0.58,
                                1)));

                    rotateTransform.BeginAnimation(
                        RotateTransform.AngleProperty,
                        swayAnimation);
                }

                DoubleAnimationUsingKeyFrames opacityAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            0),
                        new KeySpline(
                            0.2,
                            0,
                            0.2,
                            1)));

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumOpacity *
                        (0.74 -
                         (flickerRatio *
                          0.12)),
                        KeyTime.FromPercent(
                            0.07),
                        new KeySpline(
                            0.2,
                            0,
                            0.2,
                            1)));

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumOpacity,
                        KeyTime.FromPercent(
                            0.20),
                        new KeySpline(
                            0.2,
                            0,
                            0.2,
                            1)));

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumOpacity *
                        (0.78 -
                         (flickerRatio *
                          0.18)),
                        KeyTime.FromPercent(
                            0.52),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        maximumOpacity *
                        0.34,
                        KeyTime.FromPercent(
                            0.78),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                opacityAnimation.KeyFrames.Add(
                    new SplineDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            1),
                        new KeySpline(
                            0.42,
                            0,
                            0.58,
                            1)));

                flame.BeginAnimation(
                    UIElement.OpacityProperty,
                    opacityAnimation);

                DebugLog.Write(
                    "ThemeFlame",
                    $"Root NATURAL_FORMULA; Theme={theme.Name}; X={layer.X:0.###}; Lifetime={durationSeconds:0.###}; Delay={delaySeconds:0.###}; Rise={effectiveRiseDistance:0.###}; Width={flameWidth:0.###}; Height={flameHeight:0.###}; YKeyFrames=[(0,0),(0.22,{-effectiveRiseDistance * 0.16:0.###}),(0.58,{-effectiveRiseDistance * 0.54:0.###}),(1,{-effectiveRiseDistance:0.###})]; ScaleXKeyFrames=[(0,{maximumScaleX * 0.78:0.###}),(0.14,{maximumScaleX:0.###}),(0.52,{minimumScaleX + ((maximumScaleX - minimumScaleX) * 0.42):0.###}),(0.82,{minimumScaleX * 0.52:0.###}),(1,{Math.Max(0.12, minimumScaleX * 0.24):0.###})]; ScaleYKeyFrames=[(0,{minimumScaleY * 0.72:0.###}),(0.24,{maximumScaleY:0.###}),(0.63,{minimumScaleY + ((maximumScaleY - minimumScaleY) * 0.58):0.###}),(1,{minimumScaleY * 0.48:0.###})]; OpacityKeyFrames=[(0,0),(0.07,{maximumOpacity * (0.74 - (flickerRatio * 0.12)):0.###}),(0.20,{maximumOpacity:0.###}),(0.52,{maximumOpacity * (0.78 - (flickerRatio * 0.18)):0.###}),(0.78,{maximumOpacity * 0.34:0.###}),(1,0)]");
            }
            else if (fireplaceMotion)
            {
                double maximumOpacity =
                    Math.Clamp(
                        layer.Opacity,
                        0,
                        1);

                double flickerRatio =
                    Math.Clamp(
                        layer.FlickerRatio,
                        0,
                        0.9);

                DoubleAnimationUsingKeyFrames scaleYAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleY,
                        KeyTime.FromPercent(
                            0)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumScaleY,
                        KeyTime.FromPercent(
                            0.17)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleY +
                        ((maximumScaleY -
                          minimumScaleY) *
                         0.38),
                        KeyTime.FromPercent(
                            0.34)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleY +
                        ((maximumScaleY -
                          minimumScaleY) *
                         0.82),
                        KeyTime.FromPercent(
                            0.51)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleY +
                        ((maximumScaleY -
                          minimumScaleY) *
                         0.24),
                        KeyTime.FromPercent(
                            0.69)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumScaleY *
                        0.94,
                        KeyTime.FromPercent(
                            0.84)));

                scaleYAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleY,
                        KeyTime.FromPercent(
                            1)));

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                DoubleAnimationUsingKeyFrames scaleXAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumScaleX,
                        KeyTime.FromPercent(
                            0)));

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleX,
                        KeyTime.FromPercent(
                            0.21)));

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleX +
                        ((maximumScaleX -
                          minimumScaleX) *
                         0.68),
                        KeyTime.FromPercent(
                            0.43)));

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        minimumScaleX +
                        ((maximumScaleX -
                          minimumScaleX) *
                         0.18),
                        KeyTime.FromPercent(
                            0.66)));

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumScaleX *
                        0.92,
                        KeyTime.FromPercent(
                            0.82)));

                scaleXAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumScaleX,
                        KeyTime.FromPercent(
                            1)));

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                double swayDegrees =
                    Math.Clamp(
                        layer.SwayDegrees,
                        0,
                        35);

                if (swayDegrees > 0)
                {
                    DoubleAnimationUsingKeyFrames swayAnimation =
                        new()
                        {
                            BeginTime =
                                beginTime,
                            Duration =
                                flameDuration,
                            RepeatBehavior =
                                RepeatBehavior.Forever
                        };

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                0)));

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            -swayDegrees *
                            0.72,
                            KeyTime.FromPercent(
                                0.19)));

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            swayDegrees,
                            KeyTime.FromPercent(
                                0.37)));

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            -swayDegrees *
                            0.38,
                            KeyTime.FromPercent(
                                0.58)));

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            swayDegrees *
                            0.54,
                            KeyTime.FromPercent(
                                0.79)));

                    swayAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                1)));

                    rotateTransform.BeginAnimation(
                        RotateTransform.AngleProperty,
                        swayAnimation);
                }

                if (driftDistance > 0)
                {
                    DoubleAnimationUsingKeyFrames driftAnimation =
                        new()
                        {
                            BeginTime =
                                beginTime,
                            Duration =
                                flameDuration,
                            RepeatBehavior =
                                RepeatBehavior.Forever
                        };

                    driftAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                0)));

                    driftAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            -driftDistance,
                            KeyTime.FromPercent(
                                0.23)));

                    driftAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            driftDistance *
                            0.72,
                            KeyTime.FromPercent(
                                0.46)));

                    driftAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            -driftDistance *
                            0.36,
                            KeyTime.FromPercent(
                                0.71)));

                    driftAnimation.KeyFrames.Add(
                        new LinearDoubleKeyFrame(
                            0,
                            KeyTime.FromPercent(
                                1)));

                    translateTransform.BeginAnimation(
                        TranslateTransform.XProperty,
                        driftAnimation);
                }

                DoubleAnimationUsingKeyFrames flickerAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        (1 -
                         (flickerRatio *
                          0.35)),
                        KeyTime.FromPercent(
                            0)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity,
                        KeyTime.FromPercent(
                            0.13)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        (1 -
                         flickerRatio),
                        KeyTime.FromPercent(
                            0.29)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        0.96,
                        KeyTime.FromPercent(
                            0.47)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        (1 -
                         (flickerRatio *
                          0.62)),
                        KeyTime.FromPercent(
                            0.63)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity,
                        KeyTime.FromPercent(
                            0.81)));

                flickerAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        (1 -
                         (flickerRatio *
                          0.35)),
                        KeyTime.FromPercent(
                            1)));

                flame.BeginAnimation(
                    UIElement.OpacityProperty,
                    flickerAnimation);
            }
            else if (lickMotion)
            {
                IEasingFunction smoothEase =
                    new SineEase
                    {
                        EasingMode =
                            EasingMode.EaseInOut
                    };

                DoubleAnimation scaleYAnimation =
                    new(
                        minimumScaleY,
                        maximumScaleY,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.50)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            smoothEase
                    };

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                DoubleAnimation scaleXAnimation =
                    new(
                        maximumScaleX,
                        minimumScaleX,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.37)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                double swayDegrees =
                    Math.Clamp(
                        layer.SwayDegrees,
                        0,
                        35);

                if (swayDegrees > 0)
                {
                    DoubleAnimation swayAnimation =
                        new(
                            -swayDegrees,
                            swayDegrees,
                            new Duration(
                                TimeSpan.FromSeconds(
                                    durationSeconds *
                                    0.43)))
                        {
                            BeginTime =
                                beginTime,
                            AutoReverse =
                                true,
                            RepeatBehavior =
                                RepeatBehavior.Forever,
                            EasingFunction =
                                new SineEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    rotateTransform.BeginAnimation(
                        RotateTransform.AngleProperty,
                        swayAnimation);
                }

                if (driftDistance > 0)
                {
                    DoubleAnimation driftAnimation =
                        new(
                            -driftDistance,
                            driftDistance,
                            new Duration(
                                TimeSpan.FromSeconds(
                                    durationSeconds *
                                    0.61)))
                        {
                            BeginTime =
                                beginTime,
                            AutoReverse =
                                true,
                            RepeatBehavior =
                                RepeatBehavior.Forever,
                            EasingFunction =
                                new SineEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    translateTransform.BeginAnimation(
                        TranslateTransform.XProperty,
                        driftAnimation);
                }

                double maximumOpacity =
                    Math.Clamp(
                        layer.Opacity,
                        0,
                        1);

                double minimumOpacity =
                    maximumOpacity *
                    (1 -
                     Math.Clamp(
                         layer.FlickerRatio,
                         0,
                         0.9));

                DoubleAnimation flickerAnimation =
                    new(
                        minimumOpacity,
                        maximumOpacity,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.29)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                flame.BeginAnimation(
                    UIElement.OpacityProperty,
                    flickerAnimation);
            }
            else
            {
                DoubleAnimation riseAnimation =
                    new(
                        0,
                        -riseDistance,
                        flameDuration)
                    {
                        BeginTime =
                            beginTime,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                translateTransform.BeginAnimation(
                    TranslateTransform.YProperty,
                    riseAnimation);

                DoubleAnimation driftAnimation =
                    new(
                        -driftDistance,
                        driftDistance,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.34)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                translateTransform.BeginAnimation(
                    TranslateTransform.XProperty,
                    driftAnimation);

                DoubleAnimation scaleXAnimation =
                    new(
                        0.72,
                        1.18,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.27)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                DoubleAnimation scaleYAnimation =
                    new(
                        0.90,
                        1.08,
                        new Duration(
                            TimeSpan.FromSeconds(
                                durationSeconds *
                                0.41)))
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                scaleTransform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                double maximumOpacity =
                    Math.Clamp(
                        layer.Opacity,
                        0,
                        1);

                DoubleAnimationUsingKeyFrames opacityAnimation =
                    new()
                    {
                        BeginTime =
                            beginTime,
                        Duration =
                            flameDuration,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                opacityAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            0)));

                opacityAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity,
                        KeyTime.FromPercent(
                            0.12)));

                opacityAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity *
                        0.86,
                        KeyTime.FromPercent(
                            0.56)));

                opacityAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        maximumOpacity,
                        KeyTime.FromPercent(
                            0.76)));

                opacityAnimation.KeyFrames.Add(
                    new LinearDoubleKeyFrame(
                        0,
                        KeyTime.FromPercent(
                            1)));

                flame.BeginAnimation(
                    UIElement.OpacityProperty,
                    opacityAnimation);
            }

            DebugLog.Write(
                "ThemeFlame",
                $"Root LAYER; Theme={theme.Name}; MotionMode={layer.MotionMode}; X={layer.X:0.###}; Width={flameWidth:0.###}; Height={flameHeight:0.###}; CanvasTop={Canvas.GetTop(flame):0.###}; RiseDistance={riseDistance:0.###}; DriftDistance={driftDistance:0.###}; DurationSeconds={durationSeconds:0.###}; DelaySeconds={delaySeconds:0.###}; Opacity={layer.Opacity:0.###}; SurfaceHeight={surfaceHeight:0.###}; ClipToBounds={flameCanvas.ClipToBounds}");

            flameCanvas.Children.Add(
                flame);
        }

        flameCanvas.Visibility =
            Visibility.Visible;

        _glassFlameAnimationSignature =
            flameAnimationSignature;
    }

    private void ApplyNaturalFireHeatField(
        DockTheme theme,
        Canvas flameCanvas,
        double surfaceWidth,
        double surfaceHeight,
        string flameAnimationSignature)
    {
        if (string.Equals(
                _glassFlameAnimationSignature,
                flameAnimationSignature,
                StringComparison.Ordinal) &&
            _naturalFireRenderingActive &&
            _naturalFireBitmap is not null &&
            flameCanvas.Visibility ==
                Visibility.Visible)
        {
            return;
        }

        StopNaturalFireHeatField();

        flameCanvas.Children.Clear();
        flameCanvas.OpacityMask =
            null;

        double flameClipRadius =
            Math.Clamp(
                theme.GlassCornerRadius,
                0,
                Math.Min(
                    surfaceWidth,
                    surfaceHeight) /
                2);

        flameCanvas.Clip =
            new RectangleGeometry(
                new System.Windows.Rect(
                    0,
                    0,
                    surfaceWidth,
                    surfaceHeight),
                flameClipRadius,
                flameClipRadius);

        if (theme.GlassFlameBaseGlowEnabled)
        {
            double glowHeight =
                surfaceHeight *
                Math.Clamp(
                    theme.GlassFlameBaseGlowHeightRatio,
                    0.02,
                    1);

            Color baseGlowColor =
                ParseThemeColor(
                    theme.GlassFlameBaseGlowColor,
                    Color.FromArgb(
                        0x80,
                        0xFF,
                        0x5A,
                        0x00));

            LinearGradientBrush baseGlowBrush =
                new()
                {
                    StartPoint =
                        new Point(
                            0.5,
                            0),
                    EndPoint =
                        new Point(
                            0.5,
                            1)
                };

            baseGlowBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        0,
                        baseGlowColor.R,
                        baseGlowColor.G,
                        baseGlowColor.B),
                    0));

            baseGlowBrush.GradientStops.Add(
                new GradientStop(
                    baseGlowColor,
                    1));

            System.Windows.Shapes.Rectangle baseGlow =
                new()
                {
                    Width =
                        surfaceWidth,
                    Height =
                        glowHeight,
                    Fill =
                        baseGlowBrush,
                    Opacity =
                        Math.Clamp(
                            theme.GlassFlameBaseGlowOpacity,
                            0,
                            1),
                    IsHitTestVisible =
                        false
                };

            Canvas.SetLeft(
                baseGlow,
                0);

            Canvas.SetTop(
                baseGlow,
                surfaceHeight -
                glowHeight);

            flameCanvas.Children.Add(
                baseGlow);
        }

        DockThemeFlameLayer paletteLayer =
            theme.GlassFlameLayers
                .OrderByDescending(
                    layer =>
                    {
                        Color color =
                            ParseThemeColor(
                                layer.CoreColor,
                                Color.FromRgb(
                                    0xFF,
                                    0xD4,
                                    0x58));

                        return
                            color.R +
                            color.G +
                            color.B;
                    })
                .First();

        Color tipColor =
            ParseThemeColor(
                paletteLayer.TipColor,
                Color.FromArgb(
                    0,
                    0xB9,
                    0x1E,
                    0x00));

        Color midColor =
            ParseThemeColor(
                paletteLayer.MidColor,
                Color.FromArgb(
                    0xD0,
                    0xFF,
                    0x5A,
                    0x00));

        Color coreColor =
            ParseThemeColor(
                paletteLayer.CoreColor,
                Color.FromArgb(
                    0xFF,
                    0xFF,
                    0xD7,
                    0x66));

        double maximumOpacity =
            Math.Clamp(
                theme.GlassFlameLayers.Max(
                    layer => layer.Opacity),
                0.15,
                1);

        _naturalFireGridWidth =
            Math.Clamp(
                (int)Math.Round(
                    surfaceWidth / 3.0),
                96,
                320);

        _naturalFireGridHeight =
            Math.Clamp(
                (int)Math.Round(
                    surfaceHeight * 0.78),
                32,
                72);

        int pixelCount =
            _naturalFireGridWidth *
            _naturalFireGridHeight;

        _naturalFireHeat =
            new byte[pixelCount];

        _naturalFireNextHeat =
            new byte[pixelCount];

        _naturalFirePixels =
            new int[pixelCount];

        _naturalFirePalette =
            BuildNaturalFirePalette(
                tipColor,
                midColor,
                coreColor,
                maximumOpacity);

        _naturalFireFrameIndex =
            0;

        for (int index = 0;
             index < Math.Min(
                 42,
                 _naturalFireGridHeight);
             index++)
        {
            AdvanceNaturalFireHeatField();
        }

        _naturalFireBitmap =
            new WriteableBitmap(
                _naturalFireGridWidth,
                _naturalFireGridHeight,
                96,
                96,
                PixelFormats.Pbgra32,
                null);

        System.Windows.Controls.Image fireImage =
            new()
            {
                Width =
                    surfaceWidth,
                Height =
                    surfaceHeight,
                Source =
                    _naturalFireBitmap,
                Stretch =
                    Stretch.Fill,
                IsHitTestVisible =
                    false
            };

        RenderOptions.SetBitmapScalingMode(
            fireImage,
            BitmapScalingMode.HighQuality);

        Canvas.SetLeft(
            fireImage,
            0);

        Canvas.SetTop(
            fireImage,
            0);

        flameCanvas.Children.Add(
            fireImage);

        WriteNaturalFirePixels();

        flameCanvas.Visibility =
            Visibility.Visible;

        _glassFlameAnimationSignature =
            flameAnimationSignature;

        _naturalFireLastFrameUtc =
            DateTime.UtcNow;

        _naturalFireLastDebugUtc =
            _naturalFireLastFrameUtc;

        _naturalFireRenderingActive =
            true;

        CompositionTarget.Rendering +=
            NaturalFire_Rendering;

        DebugLog.Write(
            "ThemeFireField",
            $"Root START; Theme={theme.Name}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); Grid={_naturalFireGridWidth}x{_naturalFireGridHeight}; Formula=bottom heat source -> weighted heat from rows below + lateral jitter - altitude cooling; Palette={tipColor}/{midColor}/{coreColor}; MaxOpacity={maximumOpacity:0.###}");
    }

    private void NaturalFire_Rendering(
        object? sender,
        EventArgs e)
    {
        if (!_naturalFireRenderingActive ||
            _naturalFireBitmap is null ||
            _naturalFireHeat is null ||
            _naturalFireNextHeat is null ||
            _naturalFirePixels is null ||
            _naturalFirePalette is null)
        {
            return;
        }

        DateTime now =
            DateTime.UtcNow;

        double elapsedMilliseconds =
            (now -
             _naturalFireLastFrameUtc)
            .TotalMilliseconds;

        if (elapsedMilliseconds <
            28)
        {
            return;
        }

        _naturalFireLastFrameUtc =
            now;

        AdvanceNaturalFireHeatField();
        WriteNaturalFirePixels();

        if ((now -
             _naturalFireLastDebugUtc)
            .TotalSeconds <
            1)
        {
            return;
        }

        _naturalFireLastDebugUtc =
            now;

        int activeCells =
            0;

        long totalHeat =
            0;

        int maximumHeat =
            0;

        for (int index = 0;
             index < _naturalFireHeat.Length;
             index++)
        {
            int heat =
                _naturalFireHeat[index];

            if (heat > 20)
            {
                activeCells++;
            }

            totalHeat +=
                heat;

            maximumHeat =
                Math.Max(
                    maximumHeat,
                    heat);
        }

        int bottomStart =
            (_naturalFireGridHeight - 1) *
            _naturalFireGridWidth;

        int middleStart =
            (_naturalFireGridHeight / 2) *
            _naturalFireGridWidth;

        int topSampleRow =
            Math.Max(
                0,
                _naturalFireGridHeight / 6);

        int topStart =
            topSampleRow *
            _naturalFireGridWidth;

        double bottomAverage =
            0;

        double middleAverage =
            0;

        double topAverage =
            0;

        for (int x = 0;
             x < _naturalFireGridWidth;
             x++)
        {
            bottomAverage +=
                _naturalFireHeat[
                    bottomStart +
                    x];

            middleAverage +=
                _naturalFireHeat[
                    middleStart +
                    x];

            topAverage +=
                _naturalFireHeat[
                    topStart +
                    x];
        }

        bottomAverage /=
            _naturalFireGridWidth;

        middleAverage /=
            _naturalFireGridWidth;

        topAverage /=
            _naturalFireGridWidth;

        DebugLog.Write(
            "ThemeFireField",
            $"Root FRAME; Frame={_naturalFireFrameIndex}; DeltaMs={elapsedMilliseconds:0.###}; ActiveCells={activeCells}/{_naturalFireHeat.Length}; AverageHeat={(double)totalHeat / _naturalFireHeat.Length:0.###}; MaxHeat={maximumHeat}; BottomAverage={bottomAverage:0.###}; MiddleAverage={middleAverage:0.###}; TopAverage={topAverage:0.###}");
    }

    private void AdvanceNaturalFireHeatField()
    {
        if (_naturalFireHeat is null ||
            _naturalFireNextHeat is null ||
            _naturalFireGridWidth <= 0 ||
            _naturalFireGridHeight <= 2)
        {
            return;
        }

        int width =
            _naturalFireGridWidth;

        int height =
            _naturalFireGridHeight;

        int frame =
            ++_naturalFireFrameIndex;

        int bottomRow =
            height - 1;

        int secondBottomRow =
            height - 2;

        for (int x = 0;
             x < width;
             x++)
        {
            double broadWave =
                (Math.Sin(
                     (x * 0.12) +
                     (frame * 0.11)) +
                 1) *
                0.5;

            double fineWave =
                (Math.Sin(
                     (x * 0.31) -
                     (frame * 0.17)) +
                 1) *
                0.5;

            double flamePulse =
                Math.Pow(
                    (broadWave * 0.72) +
                    (fineWave * 0.28),
                    1.7);

            int noise =
                NaturalFireNoise(
                    x,
                    bottomRow,
                    frame) -
                128;

            int sourceHeat =
                Math.Clamp(
                    (int)Math.Round(
                        55 +
                        (flamePulse * 175) +
                        (noise * 0.18)),
                    35,
                    245);

            _naturalFireHeat[
                (bottomRow * width) +
                x] =
                (byte)sourceHeat;

            _naturalFireHeat[
                (secondBottomRow * width) +
                x] =
                (byte)Math.Max(
                    _naturalFireHeat[
                        (secondBottomRow * width) +
                        x],
                    (int)Math.Round(
                        sourceHeat *
                        0.72));
        }

        for (int y = 0;
             y < secondBottomRow;
             y++)
        {
            int belowRow =
                y + 1;

            int deeperRow =
                Math.Min(
                    bottomRow,
                    y + 2);

            double altitude =
                1 -
                (y /
                 (double)bottomRow);

            for (int x = 0;
                 x < width;
                 x++)
            {
                int noise =
                    NaturalFireNoise(
                        x,
                        y,
                        frame);

                int shift =
                    (noise % 5) -
                    2;

                int shiftedX =
                    Math.Clamp(
                        x + shift,
                        0,
                        width - 1);

                int neighborX =
                    Math.Clamp(
                        x +
                        (((noise >> 3) % 3) -
                         1),
                        0,
                        width - 1);

                int accumulatedHeat =
                    (_naturalFireHeat[
                         (belowRow * width) +
                         shiftedX] *
                     3) +
                    _naturalFireHeat[
                        (deeperRow * width) +
                        neighborX];

                int propagatedHeat =
                    accumulatedHeat /
                    4;

                int cooling =
                    2 +
                    (int)Math.Round(
                        altitude *
                        6) +
                    ((noise >> 5) & 3);

                _naturalFireNextHeat[
                    (y * width) +
                    x] =
                    (byte)Math.Max(
                        0,
                        propagatedHeat -
                        cooling);
            }
        }

        for (int x = 0;
             x < width;
             x++)
        {
            _naturalFireNextHeat[
                (secondBottomRow * width) +
                x] =
                _naturalFireHeat[
                    (secondBottomRow * width) +
                    x];

            _naturalFireNextHeat[
                (bottomRow * width) +
                x] =
                _naturalFireHeat[
                    (bottomRow * width) +
                    x];
        }

        byte[] swap =
            _naturalFireHeat;

        _naturalFireHeat =
            _naturalFireNextHeat;

        _naturalFireNextHeat =
            swap;

        Array.Clear(
            _naturalFireNextHeat,
            0,
            _naturalFireNextHeat.Length);
    }

    private void WriteNaturalFirePixels()
    {
        if (_naturalFireBitmap is null ||
            _naturalFireHeat is null ||
            _naturalFirePixels is null ||
            _naturalFirePalette is null)
        {
            return;
        }

        int width =
            _naturalFireGridWidth;

        int height =
            _naturalFireGridHeight;

        int bottomRow =
            Math.Max(
                1,
                height - 1);

        for (int y = 0;
             y < height;
             y++)
        {
            double verticalRatio =
                y /
                (double)bottomRow;

            double topFade =
                SmoothStep(
                    0.06,
                    0.38,
                    verticalRatio);

            int fade =
                (int)Math.Round(
                    topFade *
                    255);

            for (int x = 0;
                 x < width;
                 x++)
            {
                int index =
                    (y * width) +
                    x;

                int pixel =
                    _naturalFirePalette[
                        _naturalFireHeat[
                            index]];

                if (fade >= 255)
                {
                    _naturalFirePixels[
                        index] =
                        pixel;

                    continue;
                }

                int alpha =
                    (pixel >> 24) &
                    0xFF;

                int red =
                    (pixel >> 16) &
                    0xFF;

                int green =
                    (pixel >> 8) &
                    0xFF;

                int blue =
                    pixel &
                    0xFF;

                alpha =
                    (alpha *
                     fade +
                     127) /
                    255;

                red =
                    (red *
                     fade +
                     127) /
                    255;

                green =
                    (green *
                     fade +
                     127) /
                    255;

                blue =
                    (blue *
                     fade +
                     127) /
                    255;

                _naturalFirePixels[
                    index] =
                    (alpha << 24) |
                    (red << 16) |
                    (green << 8) |
                    blue;
            }
        }

        _naturalFireBitmap.WritePixels(
            new Int32Rect(
                0,
                0,
                width,
                height),
            _naturalFirePixels,
            width *
            sizeof(int),
            0);
    }

    private static int[] BuildNaturalFirePalette(
        Color tipColor,
        Color midColor,
        Color coreColor,
        double maximumOpacity)
    {
        int[] palette =
            new int[256];

        Color hotColor =
            Color.FromRgb(
                0xFF,
                (byte)Math.Max(
                    coreColor.G,
                    (byte)0xE1),
                (byte)Math.Max(
                    coreColor.B,
                    (byte)0x82));

        for (int heat = 0;
             heat < palette.Length;
             heat++)
        {
            double normalizedHeat =
                heat /
                255.0;

            if (normalizedHeat <=
                0.06)
            {
                palette[heat] =
                    0;

                continue;
            }

            Color color;
            double alphaRatio;

            if (normalizedHeat <
                0.42)
            {
                double progress =
                    SmoothStep(
                        0.06,
                        0.42,
                        normalizedHeat);

                color =
                    InterpolateNaturalFireColor(
                        tipColor,
                        midColor,
                        progress);

                alphaRatio =
                    progress *
                    0.58;
            }
            else if (normalizedHeat <
                     0.80)
            {
                double progress =
                    SmoothStep(
                        0.42,
                        0.80,
                        normalizedHeat);

                color =
                    InterpolateNaturalFireColor(
                        midColor,
                        coreColor,
                        progress);

                alphaRatio =
                    0.58 +
                    (progress *
                     0.36);
            }
            else
            {
                double progress =
                    SmoothStep(
                        0.80,
                        1,
                        normalizedHeat);

                color =
                    InterpolateNaturalFireColor(
                        coreColor,
                        hotColor,
                        progress);

                alphaRatio =
                    0.94 +
                    (progress *
                     0.06);
            }

            byte alpha =
                (byte)Math.Clamp(
                    (int)Math.Round(
                        255 *
                        maximumOpacity *
                        alphaRatio),
                    0,
                    255);

            int premultipliedRed =
                (color.R *
                 alpha +
                 127) /
                255;

            int premultipliedGreen =
                (color.G *
                 alpha +
                 127) /
                255;

            int premultipliedBlue =
                (color.B *
                 alpha +
                 127) /
                255;

            palette[heat] =
                (alpha << 24) |
                (premultipliedRed << 16) |
                (premultipliedGreen << 8) |
                premultipliedBlue;
        }

        return palette;
    }

    private static Color InterpolateNaturalFireColor(
        Color from,
        Color to,
        double progress)
    {
        double clampedProgress =
            Math.Clamp(
                progress,
                0,
                1);

        return
            Color.FromRgb(
                (byte)Math.Round(
                    from.R +
                    ((to.R -
                      from.R) *
                     clampedProgress)),
                (byte)Math.Round(
                    from.G +
                    ((to.G -
                      from.G) *
                     clampedProgress)),
                (byte)Math.Round(
                    from.B +
                    ((to.B -
                      from.B) *
                     clampedProgress)));
    }

    private static double SmoothStep(
        double edge0,
        double edge1,
        double value)
    {
        if (edge1 <=
            edge0)
        {
            return
                value >= edge1
                    ? 1
                    : 0;
        }

        double t =
            Math.Clamp(
                (value -
                 edge0) /
                (edge1 -
                 edge0),
                0,
                1);

        return
            t *
            t *
            (3 -
             (2 * t));
    }

    private static int NaturalFireNoise(
        int x,
        int y,
        int frame)
    {
        unchecked
        {
            uint value =
                (uint)(
                    (x * 374761393) ^
                    (y * 668265263) ^
                    (frame * 362437));

            value =
                (value ^
                 (value >> 13)) *
                1274126177u;

            value ^=
                value >> 16;

            return
                (int)(
                    value &
                    0xFF);
        }
    }

    private void StopNaturalFireHeatField()
    {
        if (_naturalFireRenderingActive)
        {
            CompositionTarget.Rendering -=
                NaturalFire_Rendering;
        }

        _naturalFireRenderingActive =
            false;

        _naturalFireBitmap =
            null;

        _naturalFireHeat =
            null;

        _naturalFireNextHeat =
            null;

        _naturalFirePixels =
            null;

        _naturalFirePalette =
            null;

        _naturalFireGridWidth =
            0;

        _naturalFireGridHeight =
            0;

        _naturalFireFrameIndex =
            0;

        _naturalFireLastFrameUtc =
            default;

        _naturalFireLastDebugUtc =
            default;
    }

    private void ResetGlassSurface()
    {
        StopNaturalFireHeatField();
        StopGlassGradientDebugSampling();

        _glassGradientAnimationSignature =
            null;

        DockChrome.CornerRadius =
            new CornerRadius(
                20);

        DockGlassSurface.Background =
            Brushes.Transparent;

        DockGlassSurface.Effect =
            null;

        DockGlassSurface.Visibility =
            Visibility.Hidden;

        DockGlassFlames.Children.Clear();
        DockGlassFlames.Visibility =
            Visibility.Hidden;

        _glassFlameAnimationSignature =
            null;

        if (FindName("DockGlassStars") is Canvas dockGlassStars)
        {
            dockGlassStars.Children.Clear();
            dockGlassStars.Visibility =
                Visibility.Hidden;
        }

        _glassStarAnimationSignature =
            null;

        DockGlassHighlight.Background =
            Brushes.Transparent;

        DockGlassHighlight.BorderBrush =
            Brushes.Transparent;

        DockGlassHighlight.BorderThickness =
            new Thickness(
                0);

        DockGlassHighlight.Visibility =
            Visibility.Hidden;
    }

    private static Color ParseThemeColor(
        string value,
        Color fallback)
    {
        try
        {
            return
                (Color)ColorConverter.ConvertFromString(
                    value);
        }
        catch
        {
            return fallback;
        }
    }

    private void ApplyDockScale()
    {
        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        DockItemsControl.LayoutTransform =
            new ScaleTransform(
                scale,
                scale);
    }

    private void About_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _updateCheckTask ??=
            GitHubUpdateService.CheckForUpdateAsync();

        _aboutWindow =
            new AboutWindow(
                _updateCheckTask)
            {
                Owner = this
            };

        _aboutWindow.Closed +=
            (_, _) =>
            {
                _aboutWindow = null;
            };

        _aboutWindow.Show();
    }

    private void Exit_Click(
        object sender,
        RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void AddDroppedItemsToSubmenu(
        IEnumerable<string> paths,
        DockItem submenu,
        bool moveSource)
    {
        foreach (string path in paths)
        {
            if (!File.Exists(path) &&
                !Directory.Exists(path))
            {
                continue;
            }

            try
            {
                string managedPath =
                    WidgetPluginLoader.IsRuntimeWidgetPath(
                        path)
                        ? path
                        : _dockItemStore.Import(
                            path,
                            moveSource);

                submenu.Children.Add(
                    CreateDockItem(
                        managedPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{App.Language["Message.ImportFailed"]}\n\n{path}\n\n{ex.Message}",
                    App.Language["App.Name"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        SaveSettings();
    }

    private void AddDroppedItems(
        IEnumerable<string> paths,
        int insertionIndex,
        bool moveSource)
    {
        int targetIndex =
            Math.Clamp(
                insertionIndex,
                0,
                DockItems.Count);

        foreach (string path in paths)
        {
            if (!File.Exists(path) &&
                !Directory.Exists(path))
            {
                continue;
            }

            try
            {
                string managedPath =
                    WidgetPluginLoader.IsRuntimeWidgetPath(
                        path)
                        ? path
                        : _dockItemStore.Import(
                            path,
                            moveSource);

                DockItems.Insert(
                    targetIndex,
                    CreateDockItem(
                        managedPath));

                targetIndex++;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{App.Language["Message.ImportFailed"]}\n\n{path}\n\n{ex.Message}",
                    App.Language["App.Name"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        EnsureRootRowCapacity();
        UpdateItemsOrientation();
        UpdateWindowBounds();

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();
            },
            DispatcherPriority.Loaded);
    }

    private void EnsureRootRowCapacity()
    {
        int maxColumns =
            Math.Clamp(
                _settings.RootMaxColumns,
                1,
                50);

        int occupiedSlots =
            Math.Max(
                1,
                DockItems.Sum(
                    item =>
                        Math.Clamp(
                            item.SlotSpan,
                            1,
                            4)));

        int requiredRows =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    occupiedSlots /
                    (double)maxColumns));

        int currentRows =
            Math.Clamp(
                _settings.RootMaxRows,
                1,
                50);

        if (requiredRows <= currentRows)
        {
            return;
        }

        _settings.RootMaxRows =
            Math.Min(
                50,
                requiredRows);

        SaveSettings();

        ShowLayoutAdjustmentNotice(
            string.Format(
                App.Language["Message.LayoutRowsAdjusted"],
                _settings.RootMaxRows,
                DockItems.Count));

        DebugLog.Write(
            "ItemLayout",
            $"Root rows auto-adjusted; Items={DockItems.Count}; Columns={maxColumns}; Rows={_settings.RootMaxRows}");
    }

    private async void ShowLayoutAdjustmentNotice(
        string text)
    {
        LayoutAdjustmentNoticeText.Text =
            text;

        LayoutAdjustmentNotice.Visibility =
            Visibility.Visible;

        DoubleAnimation fadeIn =
            new(
                0,
                1,
                TimeSpan.FromMilliseconds(
                    180));

        LayoutAdjustmentNotice.BeginAnimation(
            OpacityProperty,
            fadeIn);

        await Task.Delay(
            TimeSpan.FromSeconds(
                5));

        DoubleAnimation fadeOut =
            new(
                1,
                0,
                TimeSpan.FromMilliseconds(
                    220));

        fadeOut.Completed +=
            (_, _) =>
            {
                LayoutAdjustmentNotice.Visibility =
                    Visibility.Collapsed;

                LayoutAdjustmentNotice.BeginAnimation(
                    OpacityProperty,
                    null);

                LayoutAdjustmentNotice.Opacity =
                    0;
            };

        LayoutAdjustmentNotice.BeginAnimation(
            OpacityProperty,
            fadeOut);
    }

    private void UpdateExternalDropPlaceholder(
        Point pointerPosition,
        string[] paths)
    {
        if (_externalDropPlaceholder is null)
        {
            string previewPath =
                paths.FirstOrDefault(
                    path =>
                        File.Exists(path) ||
                        Directory.Exists(path)) ??
                string.Empty;

            _externalDropPlaceholder =
                new DockItem
                {
                    DisplayName =
                        string.IsNullOrWhiteSpace(
                            previewPath)
                            ? "Drop position"
                            : Directory.Exists(
                                previewPath)
                                ? Path.GetFileName(
                                    previewPath.TrimEnd(
                                        Path.DirectorySeparatorChar,
                                        Path.AltDirectorySeparatorChar))
                                : Path.GetFileNameWithoutExtension(
                                    previewPath),
                    IsRuntimeOnly = true,
                    Icon =
                        string.IsNullOrWhiteSpace(
                            previewPath)
                            ? null
                            : ShellIcon.GetIcon(
                                previewPath,
                                _settings.ShowFilePreviews,
                                _settings.ShortcutOverlayMode)
                };

            DockItems.Add(
                _externalDropPlaceholder);

            UpdateItemsOrientation();
            UpdateWindowBounds();
        }

        HitTestResult? hit =
            VisualTreeHelper.HitTest(
                DockItemsControl,
                pointerPosition);

        DockItem? targetItem =
            FindDockItem(
                hit?.VisualHit);

        if (targetItem is null ||
            ReferenceEquals(
                targetItem,
                _externalDropPlaceholder))
        {
            return;
        }

        int currentPlaceholderIndex =
            DockItems.IndexOf(
                _externalDropPlaceholder);

        int targetIndex =
            DockItems.IndexOf(
                targetItem);

        if (targetIndex < 0)
        {
            return;
        }

        if (targetItem.IsRuntimeOnly)
        {
            KeyValuePair<IGlueDockWidget, DockItem> companionEntry =
                _widgetCompanionItems
                    .FirstOrDefault(
                        entry =>
                            ReferenceEquals(
                                entry.Value,
                                targetItem));

            if (companionEntry.Key is not null &&
                _widgetSourceItems.TryGetValue(
                    companionEntry.Key,
                    out DockItem? sourceItem))
            {
                int sourceIndex =
                    DockItems.IndexOf(
                        sourceItem);

                if (sourceIndex >= 0)
                {
                    targetIndex =
                        sourceIndex;
                }
            }
        }

        if (DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                targetItem) is FrameworkElement targetContainer)
        {
            Point pointerInTarget =
                DockItemsControl.TranslatePoint(
                    pointerPosition,
                    targetContainer);

            bool vertical =
                _settings.Edge is
                    DockEdge.Left or
                    DockEdge.Right;

            double axisPosition =
                vertical
                    ? pointerInTarget.Y
                    : pointerInTarget.X;

            double axisLength =
                vertical
                    ? targetContainer.ActualHeight
                    : targetContainer.ActualWidth;

            if (axisLength > 0 &&
                axisPosition >=
                    axisLength / 2)
            {
                targetIndex++;

                if (targetItem.IsRuntimeOnly)
                {
                    targetIndex =
                        Math.Max(
                            targetIndex,
                            DockItems.IndexOf(
                                targetItem) +
                            1);
                }
            }
        }

        if (currentPlaceholderIndex >= 0 &&
            currentPlaceholderIndex <
                targetIndex)
        {
            targetIndex--;
        }

        targetIndex =
            Math.Clamp(
                targetIndex,
                0,
                DockItems.Count - 1);

        if (currentPlaceholderIndex ==
            targetIndex)
        {
            return;
        }

        AnimateInternalDragMove(
            _externalDropPlaceholder,
            currentPlaceholderIndex,
            targetIndex);

        SetInternalDragItemVisibility(
            _externalDropPlaceholder,
            visible: true);

        UpdateItemsOrientation();
        UpdateWindowBounds();
    }

    private void SetExternalDropSubmenuHighlight(
        FrameworkElement element)
    {
        Border? border =
            FindAncestorBorder(
                element,
                "DockItemBorder");

        if (ReferenceEquals(
                border,
                _externalDropSubmenuHighlightBorder))
        {
            return;
        }

        ClearExternalDropSubmenuHighlight();

        if (border is null)
        {
            return;
        }

        _externalDropSubmenuHighlightBorder =
            border;

        border.BorderBrush =
            Resources["DockTextBrush"] as System.Windows.Media.Brush ??
            Brushes.White;

        border.BorderThickness =
            new Thickness(
                2);
    }

    private void ClearExternalDropSubmenuHighlight()
    {
        if (_externalDropSubmenuHighlightBorder is null)
        {
            return;
        }

        _externalDropSubmenuHighlightBorder.BorderBrush =
            Brushes.Transparent;

        _externalDropSubmenuHighlightBorder.BorderThickness =
            new Thickness(
                1);

        _externalDropSubmenuHighlightBorder =
            null;
    }

    private static Border? FindAncestorBorder(
        DependencyObject? start,
        string borderName)
    {
        DependencyObject? current =
            start;

        while (current is not null)
        {
            if (current is Border border &&
                string.Equals(
                    border.Name,
                    borderName,
                    StringComparison.Ordinal))
            {
                return border;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    private void RemoveExternalDropPlaceholder()
    {
        if (_externalDropPlaceholder is null)
        {
            return;
        }

        DockItems.Remove(
            _externalDropPlaceholder);

        _externalDropPlaceholder =
            null;

        UpdateItemsOrientation();
        UpdateWindowBounds();
    }

    private void ShowInternalDragGhost(
        DockItem draggedItem)
    {
        DragGhostImage.Source =
            draggedItem.Icon;

        DragGhostLabel.Text =
            draggedItem.DisplayName;

        DragGhostLabel.Visibility =
            _settings.ShowItemLabels
                ? Visibility.Visible
                : Visibility.Collapsed;

        DragGhost.Visibility =
            Visibility.Visible;
    }

    private void HideInternalDragGhost()
    {
        DragGhost.Visibility =
            Visibility.Collapsed;

        DragGhost.RenderTransform =
            Transform.Identity;

        DragGhostImage.Source = null;
        DragGhostLabel.Text = string.Empty;
    }

    private FrameworkElement? FindSubmenuElementAtPosition(
        Point pointerPosition)
    {
        HitTestResult? hit =
            VisualTreeHelper.HitTest(
                DockItemsControl,
                pointerPosition);

        DependencyObject? current =
            hit?.VisualHit;

        while (current is not null &&
               !ReferenceEquals(
                   current,
                   DockItemsControl))
        {
            if (current is FrameworkElement element &&
                element.DataContext is DockItem item &&
                item.IsSubmenu)
            {
                return element;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    private FrameworkElement? FindInternalSubmenuDropElementAtPosition(
        Point pointerPosition)
    {
        FrameworkElement? submenuElement =
            FindSubmenuElementAtPosition(
                pointerPosition);

        if (submenuElement?.DataContext is not DockItem submenuItem)
        {
            return null;
        }

        if (DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                submenuItem) is not FrameworkElement container)
        {
            return null;
        }

        System.Windows.Controls.Image? icon =
            FindVisualChild<System.Windows.Controls.Image>(
                container,
                "DockItemIcon");

        if (icon is null ||
            icon.ActualWidth <= 0 ||
            icon.ActualHeight <= 0)
        {
            return null;
        }

        Point pointerInIcon =
            DockItemsControl.TranslatePoint(
                pointerPosition,
                icon);

        if (pointerInIcon.X < 0 ||
            pointerInIcon.Y < 0 ||
            pointerInIcon.X > icon.ActualWidth ||
            pointerInIcon.Y > icon.ActualHeight)
        {
            return null;
        }

        return submenuElement;
    }

    private void UpdateInternalDragVisual(
        Point pointerPosition)
    {
        if (DragGhost.Visibility !=
            Visibility.Visible)
        {
            return;
        }

        Point pointerInGrid =
            DockItemsControl.TranslatePoint(
                pointerPosition,
                DockContentGrid);

        double targetX =
            pointerInGrid.X -
            _dragPointerOffset.X;

        double targetY =
            pointerInGrid.Y -
            _dragPointerOffset.Y;

        DragGhost.RenderTransform =
            new TranslateTransform(
                targetX,
                targetY);
    }

    private void UpdateInternalDragPosition(
        DockItem draggedItem,
        Point pointerPosition)
    {
        int currentIndex =
            DockItems.IndexOf(
                draggedItem);

        if (currentIndex < 0 ||
            DockItems.Count <= 1)
        {
            return;
        }

        bool vertical =
            _settings.Edge is
                DockEdge.Left or
                DockEdge.Right;

        double axisLength =
            vertical
                ? DockItemsControl.ActualHeight
                : DockItemsControl.ActualWidth;

        if (axisLength <= 0)
        {
            return;
        }

        double pointerAxis =
            vertical
                ? pointerPosition.Y
                : pointerPosition.X;

        double slotLength =
            axisLength /
            DockItems.Count;

        if (slotLength <= 0)
        {
            return;
        }

        int targetIndex =
            Math.Clamp(
                (int)Math.Floor(
                    pointerAxis /
                    slotLength),
                0,
                DockItems.Count - 1);

        if (targetIndex ==
            _lastInternalDragIndex)
        {
            return;
        }

        double targetSlotStart =
            targetIndex *
            slotLength;

        double targetSlotCenter =
            targetSlotStart +
            (slotLength / 2);

        const double hysteresis = 6;

        if (targetIndex > currentIndex &&
            pointerAxis <
            targetSlotCenter + hysteresis)
        {
            return;
        }

        if (targetIndex < currentIndex &&
            pointerAxis >
            targetSlotCenter - hysteresis)
        {
            return;
        }

        AnimateInternalDragMove(
            draggedItem,
            currentIndex,
            targetIndex);

        _lastInternalDragIndex =
            targetIndex;
    }

    private void AnimateInternalDragMove(
        DockItem draggedItem,
        int currentIndex,
        int targetIndex)
    {
        Dictionary<DockItem, Point> oldPositions =
            [];

        for (int index = 0;
             index < DockItems.Count;
             index++)
        {
            DockItem item =
                DockItems[index];

            if (DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    item) is not FrameworkElement container)
            {
                continue;
            }

            oldPositions[item] =
                container.TranslatePoint(
                    new Point(0, 0),
                    DockItemsControl);
        }

        ResetInternalDragItemTransforms();

        DockItems.Move(
            currentIndex,
            targetIndex);

        DockItemsControl.UpdateLayout();

        Duration duration =
            new(
                TimeSpan.FromMilliseconds(
                    150));

        CubicEase easing =
            new()
            {
                EasingMode =
                    EasingMode.EaseOut
            };

        for (int index = 0;
             index < DockItems.Count;
             index++)
        {
            DockItem item =
                DockItems[index];

            if (!oldPositions.TryGetValue(
                    item,
                    out Point oldPosition) ||
                DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    item) is not FrameworkElement container)
            {
                continue;
            }

            Point newPosition =
                container.TranslatePoint(
                    new Point(0, 0),
                    DockItemsControl);

            double offsetX =
                oldPosition.X -
                newPosition.X;

            double offsetY =
                oldPosition.Y -
                newPosition.Y;

            if (Math.Abs(offsetX) < 0.1 &&
                Math.Abs(offsetY) < 0.1)
            {
                continue;
            }

            TranslateTransform transform =
                new();

            container.RenderTransform =
                transform;

            transform.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(
                    offsetX,
                    0,
                    duration)
                {
                    EasingFunction =
                        easing
                });

            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(
                    offsetY,
                    0,
                    duration)
                {
                    EasingFunction =
                        easing
                });
        }

        SetInternalDragItemVisibility(
            draggedItem,
            visible: false);
    }

    private void SetInternalDragItemVisibility(
        DockItem item,
        bool visible)
    {
        if (DockItemsControl.ItemContainerGenerator.ContainerFromItem(
                item) is not FrameworkElement container)
        {
            return;
        }

        container.Opacity =
            visible
                ? 1
                : 0;
    }

    private void ResetInternalDragItemTransforms()
    {
        for (int index = 0;
             index < DockItemsControl.Items.Count;
             index++)
        {
            if (DockItemsControl.ItemContainerGenerator.ContainerFromIndex(
                    index) is not FrameworkElement container)
            {
                continue;
            }

            container.RenderTransform =
                Transform.Identity;

            container.Opacity =
                1;
        }
    }

    private void ReorderDockItem(
        DockItem draggedItem,
        DependencyObject? originalSource)
    {
        DockItem? targetItem =
            FindDockItem(originalSource);

        int oldIndex =
            DockItems.IndexOf(
                draggedItem);

        if (oldIndex < 0)
        {
            return;
        }

        if (targetItem is null)
        {
            DockItems.Move(
                oldIndex,
                DockItems.Count - 1);

            return;
        }

        int targetIndex =
            DockItems.IndexOf(
                targetItem);

        if (targetIndex < 0 ||
            targetIndex == oldIndex)
        {
            return;
        }

        DockItems.Move(
            oldIndex,
            targetIndex);
    }

    private static DockItem? FindDockItem(
        DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is FrameworkElement element &&
                element.DataContext is DockItem item)
            {
                return item;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    private static void SetDropEffect(
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                InternalDragFormat))
        {
            e.Effects =
                DragDropEffects.Copy;

            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(
                DataFormats.FileDrop))
        {
            e.Effects =
                (e.KeyStates &
                 DragDropKeyStates.ShiftKey) != 0
                    ? DragDropEffects.Move
                    : DragDropEffects.Copy;

            e.Handled = true;
            return;
        }

        e.Effects =
            DragDropEffects.None;

        e.Handled = true;
    }

    private DockItem CreateDockItem(
        string path)
    {
        path =
            WidgetPluginLoader.ResolveRuntimeWidgetPath(
                path);

        string itemId =
            WidgetPluginLoader.ResolveExistingInstanceId(
                path,
                Guid.NewGuid().ToString(
                    "N"));

        if (WidgetPluginLoader.TryCreate(
                path,
                itemId,
                OpenWidgetSettingsSection,
                out IGlueDockWidget? widget,
                out FrameworkElement? widgetView))
        {
            return new DockItem
            {
                Id = itemId,
                Path = path,
                DisplayName =
                    string.IsNullOrWhiteSpace(widget?.DisplayName)
                        ? Path.GetFileNameWithoutExtension(path)
                        : widget.DisplayName,
                WidgetInstance = widget,
                WidgetView = widgetView
            };
        }

        string trimmedPath =
            path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        string displayName =
            Directory.Exists(path)
                ? Path.GetFileName(trimmedPath)
                : Path.GetFileNameWithoutExtension(path);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = Path.GetFileName(path);
        }

        return new DockItem
        {
            Path = path,
            DisplayName = displayName,
            Icon =
                ShellIcon.GetIcon(
                    path,
                    _settings.ShowFilePreviews,
                    _settings.ShortcutOverlayMode)
        };
    }

    private DockItem CreateDockItem(
        DockEntrySettings entry)
    {
        string path =
            WidgetPluginLoader.ResolveRuntimeWidgetPath(
                entry.Path ??
                string.Empty);

        IGlueDockWidget? widget = null;
        FrameworkElement? widgetView = null;

        string itemId =
            string.IsNullOrWhiteSpace(
                entry.Id)
                ? Guid.NewGuid().ToString(
                    "N")
                : entry.Id;

        if (!entry.IsSubmenu)
        {
            itemId =
                WidgetPluginLoader.ResolveExistingInstanceId(
                    path,
                    itemId);
        }

        bool isWidget =
            !entry.IsSubmenu &&
            WidgetPluginLoader.TryCreate(
                path,
                itemId,
                OpenWidgetSettingsSection,
                out widget,
                out widgetView);

        DockItem item =
            new()
            {
                Id = itemId,
                Path = path,
                DisplayName =
                    string.IsNullOrWhiteSpace(entry.DisplayName) &&
                    isWidget
                        ? widget?.DisplayName ?? string.Empty
                        : entry.DisplayName ?? string.Empty,
                IsSubmenu = entry.IsSubmenu,
                IsWidgetDragDropLocked =
                    entry.IsWidgetDragDropLocked,
                SubmenuIconRepositoryPath =
                    entry.SubmenuIconRepositoryPath ?? string.Empty,
                WidgetInstance = widget,
                WidgetView = widgetView,
                Icon =
                    entry.IsSubmenu
                        ? SubmenuIcon.Create(
                            _settings,
                            entry.SubmenuIconRepositoryPath ?? string.Empty)
                        : isWidget
                            ? null
                            : ShellIcon.GetIcon(
                                path,
                                _settings.ShowFilePreviews,
                                _settings.ShortcutOverlayMode)
            };

        foreach (DockEntrySettings child in entry.Children ?? [])
        {
            item.Children.Add(CreateDockItem(child));
        }

        return item;
    }

    private static DockEntrySettings CreateSettingsEntry(
        DockItem item)
    {
        return new DockEntrySettings
        {
            Id = item.Id,
            Path = item.Path,
            DisplayName = item.DisplayName,
            IsSubmenu = item.IsSubmenu,
            IsWidgetDragDropLocked =
                item.IsWidgetDragDropLocked,
            SubmenuIconRepositoryPath =
                item.SubmenuIconRepositoryPath,
            Children =
                item.Children
                    .Where(
                        child =>
                            !child.IsRuntimeOnly)
                    .Select(CreateSettingsEntry)
                    .ToList()
        };
    }

    private static bool CanStartDockItemAsAdministrator(
        DockItem item)
    {
        if (item.IsRuntimeOnly ||
            item.IsSubmenu ||
            item.IsWidget ||
            string.IsNullOrWhiteSpace(
                item.Path) ||
            !File.Exists(
                item.Path))
        {
            return false;
        }

        string launchPath =
            item.Path;

        if (item.Path.EndsWith(
                ".lnk",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!ShellIcon.TryGetShortcutLaunchInfo(
                    item.Path,
                    out string shortcutTargetPath,
                    out _,
                    out _) ||
                string.IsNullOrWhiteSpace(
                    shortcutTargetPath) ||
                !File.Exists(
                    shortcutTargetPath))
            {
                return false;
            }

            launchPath =
                shortcutTargetPath;
        }

        string extension =
            Path.GetExtension(
                launchPath);

        if (string.Equals(
                extension,
                ".bat",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                extension,
                ".cmd",
                StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(
                Path.Combine(
                    Environment.SystemDirectory,
                    "cmd.exe"));
        }

        try
        {
            ProcessStartInfo startInfo =
                new(
                    launchPath)
                {
                    UseShellExecute =
                        true
                };

            return startInfo.Verbs.Any(
                verb =>
                    string.Equals(
                        verb,
                        "runas",
                        StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void StartDockItemAsAdministrator_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            !CanStartDockItemAsAdministrator(
                item))
        {
            return;
        }

        LaunchDockItemAsAdministrator(
            item);
    }

    private static void LaunchDockItemAsAdministrator(
        DockItem item)
    {
        try
        {
            string launchPath =
                item.Path;

            string arguments =
                string.Empty;

            string? workingDirectory =
                Path.GetDirectoryName(
                    item.Path);

            if (item.Path.EndsWith(
                    ".lnk",
                    StringComparison.OrdinalIgnoreCase) &&
                ShellIcon.TryGetShortcutLaunchInfo(
                    item.Path,
                    out string shortcutTargetPath,
                    out string shortcutArguments,
                    out string shortcutWorkingDirectory))
            {
                launchPath =
                    shortcutTargetPath;

                arguments =
                    shortcutArguments;

                if (!string.IsNullOrWhiteSpace(
                        shortcutWorkingDirectory))
                {
                    workingDirectory =
                        shortcutWorkingDirectory;
                }
                else
                {
                    workingDirectory =
                        Path.GetDirectoryName(
                            launchPath);
                }
            }

            string extension =
                Path.GetExtension(
                    launchPath);

            ProcessStartInfo startInfo;

            if (string.Equals(
                    extension,
                    ".bat",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    extension,
                    ".cmd",
                    StringComparison.OrdinalIgnoreCase))
            {
                string commandProcessorPath =
                    Path.Combine(
                        Environment.SystemDirectory,
                        "cmd.exe");

                string scriptArguments =
                    string.IsNullOrWhiteSpace(
                        arguments)
                        ? $"\"\"{launchPath}\"\""
                        : $"\"\"{launchPath}\" {arguments}\"";

                startInfo =
                    new()
                    {
                        FileName =
                            commandProcessorPath,
                        Arguments =
                            $"/d /s /c {scriptArguments}",
                        Verb =
                            "runas",
                        UseShellExecute =
                            true
                    };
            }
            else
            {
                startInfo =
                    new()
                    {
                        FileName =
                            launchPath,
                        Arguments =
                            arguments,
                        Verb =
                            "runas",
                        UseShellExecute =
                            true
                    };
            }

            if (!string.IsNullOrWhiteSpace(
                    workingDirectory))
            {
                startInfo.WorkingDirectory =
                    workingDirectory;
            }

            Process.Start(
                startInfo);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException(
                "LaunchAsAdministrator",
                ex);

            MessageBox.Show(
                $"{App.Language["Message.LaunchFailed"]}\n\n{item.Path}\n\n{ex.Message}",
                App.Language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void LaunchDockItem(
        DockItem item)
    {
        try
        {
            string? workingDirectory =
                Directory.Exists(item.Path)
                    ? item.Path
                    : Path.GetDirectoryName(
                        item.Path);

            ProcessStartInfo startInfo =
                new()
                {
                    FileName = item.Path,
                    UseShellExecute = true
                };

            if (!string.IsNullOrWhiteSpace(
                    workingDirectory))
            {
                startInfo.WorkingDirectory =
                    workingDirectory;
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{App.Language["Message.LaunchFailed"]}\n\n{item.Path}\n\n{ex.Message}",
                App.Language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Expand()
    {
        LogRootState(
            "Expand requested");

        if (_isExpanded &&
            !_collapseAnimationRunning)
        {
            return;
        }

        bool fadeAnimation =
            !string.Equals(
                _settings.AnimationStyle,
                "Slide",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                _settings.AnimationStyle,
                "Zoom",
                StringComparison.OrdinalIgnoreCase);

        _zoomHoverUnlockTimer.Stop();
        _expandHoverStabilizeTimer.Stop();
        _expandHoverStabilizeTimer.Start();
        _collapseAnimationRunning = false;

        _slideExpandPreparing =
            string.Equals(
                _settings.AnimationStyle,
                "Slide",
                StringComparison.OrdinalIgnoreCase);

        DockContentGrid.BeginAnimation(
            OpacityProperty,
            null);

        DockContentGrid.Opacity = 1;
        DockContentGrid.RenderTransform = Transform.Identity;
        DockContentGrid.CacheMode = null;

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        if (fadeAnimation)
        {
            DockChrome.Opacity = 0;
            _nativeBackdropHost.SetOpacity(0);
        }
        else
        {
            DockChrome.Opacity = 1;
            _nativeBackdropHost.SetOpacity(1);
        }

        _isExpanded = true;

        DockContentGrid.Visibility =
            Visibility.Visible;

        DockChrome.Padding =
            new Thickness(
                10 * Math.Clamp(
                    _settings.DockScale,
                    0.60,
                    2.00));

        DockChrome.CornerRadius =
            new CornerRadius(
                20 * Math.Clamp(
                    _settings.DockScale,
                    0.60,
                    2.00));

        UpdateWindowBounds();
        UpdateLayout();

        ApplyAppearance();
        UpdateLayout();

        PlayExpandAnimation();
    }

    private void Collapse(
        bool immediate)
    {
        LogRootState(
            $"Collapse requested; Immediate={immediate}");

        if (_settings.CollapseDisabled)
        {
            Expand();
            return;
        }

        if (immediate)
        {
            CompleteCollapse();
            return;
        }

        if (_collapseAnimationRunning ||
            !_isExpanded)
        {
            return;
        }

        _collapseAnimationRunning = true;
        PlayCollapseAnimation();
    }

    private void CompleteCollapse()
    {
        if (_settings.CollapseDisabled)
        {
            Expand();
            return;
        }

        bool wasAnimatedCollapse =
            _collapseAnimationRunning;

        _collapseAnimationRunning = false;
        _isExpanded = false;

        LogRootState(
            "Complete collapse");

        DockContentGrid.BeginAnimation(
            OpacityProperty,
            null);

        DockContentGrid.Opacity = 1;
        DockContentGrid.RenderTransform = Transform.Identity;
        DockContentGrid.CacheMode = null;

        DockContentGrid.Visibility =
            Visibility.Collapsed;

        DockChrome.Padding =
            new Thickness(0);

        DockChrome.CornerRadius =
            new CornerRadius(
                Math.Max(
                    1,
                    _settings.BarThickness / 2));

        ApplyAppearance();

        if (wasAnimatedCollapse &&
            string.Equals(
                _settings.AnimationStyle,
                "Zoom",
                StringComparison.OrdinalIgnoreCase))
        {
            _zoomHoverUnlockTimer.Stop();
            _zoomHoverUnlockTimer.Start();
        }
    }

    private void StartBackdropOpacitySync(
        string phase)
    {
        if (_syncBackdropOpacityWithDockChrome)
        {
            StopBackdropOpacitySync();
        }

        _backdropOpacitySyncPhase =
            phase;

        _backdropOpacitySyncFrame =
            0;

        _syncBackdropOpacityWithDockChrome =
            true;

        DebugLog.Write(
            "Fade",
            $"START Phase={_backdropOpacitySyncPhase}; DockChromeOpacity={DockChrome.Opacity:0.000}");

        CompositionTarget.Rendering +=
            BackdropOpacitySync_Rendering;
    }

    private void StopBackdropOpacitySync()
    {
        if (!_syncBackdropOpacityWithDockChrome)
        {
            return;
        }

        CompositionTarget.Rendering -=
            BackdropOpacitySync_Rendering;

        DebugLog.Write(
            "Fade",
            $"STOP Phase={_backdropOpacitySyncPhase}; Frames={_backdropOpacitySyncFrame}; DockChromeOpacity={DockChrome.Opacity:0.000}");

        _syncBackdropOpacityWithDockChrome =
            false;

        _backdropOpacitySyncPhase =
            string.Empty;
    }

    private void BackdropOpacitySync_Rendering(
        object? sender,
        EventArgs e)
    {
        double dockChromeOpacity =
            Math.Clamp(
                DockChrome.Opacity,
                0,
                1);

        bool nativeBackdropUpdated =
            _nativeBackdropHost.SetOpacity(
                dockChromeOpacity);

        _backdropOpacitySyncFrame++;

        DebugLog.Write(
            "Fade",
            $"FRAME Phase={_backdropOpacitySyncPhase}; Frame={_backdropOpacitySyncFrame}; DockChromeOpacity={dockChromeOpacity:0.000}; NativeBackdropRequestedOpacity={dockChromeOpacity:0.000}; NativeBackdropUpdateSucceeded={nativeBackdropUpdated}");
    }

    private void PlayExpandAnimation()
    {
        string animationStyle =
            _settings.AnimationStyle ?? "Fade";

        TimeSpan duration =
            TimeSpan.FromMilliseconds(180);

        StopBackdropOpacitySync();

        DockContentGrid.BeginAnimation(
            OpacityProperty,
            null);

        DockContentGrid.Opacity = 1;
        DockContentGrid.RenderTransform = Transform.Identity;
        DockContentGrid.RenderTransformOrigin =
            new Point(0.5, 0.5);
        DockContentGrid.CacheMode = null;

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.Opacity = 1;

        switch (animationStyle)
        {
            case "Slide":
            {
                double startLeft =
                    Left;

                double startTop =
                    Top;

                double barThickness =
                    Math.Clamp(
                        _settings.BarThickness,
                        2,
                        24);

                double travel =
                    _settings.Edge is
                        DockEdge.Left or
                        DockEdge.Right
                        ? Math.Max(
                            0,
                            Width - barThickness)
                        : Math.Max(
                            0,
                            Height - barThickness);

                double targetLeft =
                    startLeft;

                double targetTop =
                    startTop;

                switch (_settings.Edge)
                {
                    case DockEdge.Left:
                        targetLeft +=
                            travel;
                        break;

                    case DockEdge.Right:
                        targetLeft -=
                            travel;
                        break;

                    case DockEdge.Top:
                        targetTop +=
                            travel;
                        break;

                    default:
                        targetTop -=
                            travel;
                        break;
                }

                _slideExpandPreparing =
                    false;

                if (_settings.Edge is
                    DockEdge.Left or
                    DockEdge.Right)
                {
                    BeginAnimation(
                        LeftProperty,
                        null);

                    Left =
                        startLeft;

                    DoubleAnimation animation =
                        new(
                            startLeft,
                            targetLeft,
                            new Duration(
                                TimeSpan.FromMilliseconds(
                                    220)))
                        {
                            FillBehavior =
                                FillBehavior.HoldEnd,
                            EasingFunction =
                                new CubicEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    animation.Completed +=
                        (_, _) =>
                        {
                            Left =
                                targetLeft;

                            BeginAnimation(
                                LeftProperty,
                                null);

                            _nativeBackdropHost.Sync();
                        };

                    BeginAnimation(
                        LeftProperty,
                        animation);
                }
                else
                {
                    BeginAnimation(
                        TopProperty,
                        null);

                    Top =
                        startTop;

                    DoubleAnimation animation =
                        new(
                            startTop,
                            targetTop,
                            new Duration(
                                TimeSpan.FromMilliseconds(
                                    220)))
                        {
                            FillBehavior =
                                FillBehavior.HoldEnd,
                            EasingFunction =
                                new CubicEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    animation.Completed +=
                        (_, _) =>
                        {
                            Top =
                                targetTop;

                            BeginAnimation(
                                TopProperty,
                                null);

                            _nativeBackdropHost.Sync();
                        };

                    BeginAnimation(
                        TopProperty,
                        animation);
                }

                break;
            }

            case "Zoom":
            {
                _slideExpandPreparing =
                    false;

                ScaleTransform transform =
                    new(
                        0.92,
                        0.92);

                DockContentGrid.RenderTransform =
                    transform;

                DoubleAnimation scaleXAnimation =
                    new(
                        0.92,
                        1.0,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
                        EasingFunction =
                            new CubicEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };

                DoubleAnimation scaleYAnimation =
                    new(
                        0.92,
                        1.0,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
                        EasingFunction =
                            new CubicEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };

                scaleYAnimation.Completed +=
                    (_, _) =>
                    {
                        DockContentGrid.RenderTransform =
                            Transform.Identity;
                    };

                transform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                transform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                break;
            }

            default:
            {
                _slideExpandPreparing =
                    false;

                DockChrome.Opacity =
                    0;

                _nativeBackdropHost.SetOpacity(
                    0);

                StartBackdropOpacitySync(
                    "Expand");

                DoubleAnimation animation =
                    new(
                        0,
                        1,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.HoldEnd,
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };

                animation.Completed +=
                    (_, _) =>
                    {
                        StopBackdropOpacitySync();

                        DockChrome.Opacity =
                            1;

                        DockChrome.BeginAnimation(
                            OpacityProperty,
                            null);

                        _nativeBackdropHost.SetOpacity(
                            1);
                    };

                DockChrome.BeginAnimation(
                    OpacityProperty,
                    animation);

                break;
            }
        }
    }

    private void PlayCollapseAnimation()
    {
        string animationStyle =
            _settings.AnimationStyle ?? "Fade";

        TimeSpan duration =
            TimeSpan.FromMilliseconds(150);

        StopBackdropOpacitySync();

        DockContentGrid.BeginAnimation(
            OpacityProperty,
            null);

        DockContentGrid.Opacity = 1;
        DockContentGrid.RenderTransform = Transform.Identity;
        DockContentGrid.RenderTransformOrigin =
            new Point(0.5, 0.5);
        DockContentGrid.CacheMode = null;

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.Opacity = 1;

        _nativeBackdropHost.SetOpacity(
            1);

        switch (animationStyle)
        {
            case "Slide":
            {
                double startLeft =
                    Left;

                double startTop =
                    Top;

                double barThickness =
                    Math.Clamp(
                        _settings.BarThickness,
                        2,
                        24);

                double travel =
                    _settings.Edge is
                        DockEdge.Left or
                        DockEdge.Right
                        ? Math.Max(
                            0,
                            Width - barThickness)
                        : Math.Max(
                            0,
                            Height - barThickness);

                double targetLeft =
                    startLeft;

                double targetTop =
                    startTop;

                switch (_settings.Edge)
                {
                    case DockEdge.Left:
                        targetLeft -=
                            travel;
                        break;

                    case DockEdge.Right:
                        targetLeft +=
                            travel;
                        break;

                    case DockEdge.Top:
                        targetTop -=
                            travel;
                        break;

                    default:
                        targetTop +=
                            travel;
                        break;
                }

                if (_settings.Edge is
                    DockEdge.Left or
                    DockEdge.Right)
                {
                    BeginAnimation(
                        LeftProperty,
                        null);

                    DoubleAnimation animation =
                        new(
                            startLeft,
                            targetLeft,
                            new Duration(
                                TimeSpan.FromMilliseconds(
                                    200)))
                        {
                            FillBehavior =
                                FillBehavior.HoldEnd,
                            EasingFunction =
                                new CubicEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    animation.Completed +=
                        (_, _) =>
                        {
                            CompleteCollapse();

                            BeginAnimation(
                                LeftProperty,
                                null);

                            _nativeBackdropHost.Sync();
                        };

                    BeginAnimation(
                        LeftProperty,
                        animation);
                }
                else
                {
                    BeginAnimation(
                        TopProperty,
                        null);

                    DoubleAnimation animation =
                        new(
                            startTop,
                            targetTop,
                            new Duration(
                                TimeSpan.FromMilliseconds(
                                    200)))
                        {
                            FillBehavior =
                                FillBehavior.HoldEnd,
                            EasingFunction =
                                new CubicEase
                                {
                                    EasingMode =
                                        EasingMode.EaseInOut
                                }
                        };

                    animation.Completed +=
                        (_, _) =>
                        {
                            CompleteCollapse();

                            BeginAnimation(
                                TopProperty,
                                null);

                            _nativeBackdropHost.Sync();
                        };

                    BeginAnimation(
                        TopProperty,
                        animation);
                }

                break;
            }

            case "Zoom":
            {
                ScaleTransform transform =
                    new(
                        1.0,
                        1.0);

                DockContentGrid.RenderTransform =
                    transform;

                DoubleAnimation scaleXAnimation =
                    new(
                        1.0,
                        0.92,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseIn
                            }
                    };

                DoubleAnimation scaleYAnimation =
                    new(
                        1.0,
                        0.92,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseIn
                            }
                    };

                scaleYAnimation.Completed +=
                    (_, _) =>
                    {
                        DockContentGrid.RenderTransform =
                            Transform.Identity;

                        CompleteCollapse();
                    };

                transform.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleXAnimation);

                transform.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleYAnimation);

                break;
            }

            default:
            {
                DockChrome.Opacity =
                    1;

                _nativeBackdropHost.SetOpacity(
                    1);

                StartBackdropOpacitySync(
                    "Collapse");

                DoubleAnimation animation =
                    new(
                        1,
                        0,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.HoldEnd,
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseIn
                            }
                    };

                animation.Completed +=
                    (_, _) =>
                    {
                        StopBackdropOpacitySync();

                        DockChrome.Opacity =
                            0;

                        DockChrome.BeginAnimation(
                            OpacityProperty,
                            null);

                        _nativeBackdropHost.SetOpacity(
                            0);

                        CompleteCollapse();

                        DockChrome.Opacity =
                            1;

                        _nativeBackdropHost.SetOpacity(
                            1);
                    };

                DockChrome.BeginAnimation(
                    OpacityProperty,
                    animation);

                break;
            }
        }
    }

    private void UpdateItemLabelVisibility()
    {
        DebugLog.Write(
            "Labels",
            $"Update root labels; Enabled={_settings.ShowItemLabels}; Items={DockItemsControl.Items.Count}");

        Dispatcher.BeginInvoke(
            () =>
            {
                DockItemsControl.UpdateLayout();

                foreach (object item in DockItemsControl.Items)
                {
                    if (DockItemsControl.ItemContainerGenerator.ContainerFromItem(item)
                        is not FrameworkElement container)
                    {
                        continue;
                    }

                    TextBlock? label =
                        FindVisualChild<TextBlock>(
                            container,
                            "DockItemLabel");

                    if (label is null)
                    {
                        continue;
                    }

                    if (item is DockItem dockItem &&
                        dockItem.IsWidget)
                    {
                        label.Visibility =
                            Visibility.Collapsed;

                        continue;
                    }

                    label.Visibility =
                        _settings.ShowItemLabels
                            ? Visibility.Visible
                            : Visibility.Collapsed;

                    if (_settings.ShowItemLabels)
                    {
                        double pixelsPerDip =
                            VisualTreeHelper.GetDpi(label).PixelsPerDip;

                        FormattedText formattedText =
                            new(
                                label.Text ?? string.Empty,
                                System.Globalization.CultureInfo.CurrentUICulture,
                                label.FlowDirection,
                                new Typeface(
                                    label.FontFamily,
                                    label.FontStyle,
                                    label.FontWeight,
                                    label.FontStretch),
                                label.FontSize,
                                label.Foreground,
                                pixelsPerDip);

                        double availableWidth =
                            Math.Max(
                                0,
                                container.ActualWidth -
                                label.Margin.Left -
                                label.Margin.Right);

                        bool centerText =
                            formattedText.WidthIncludingTrailingWhitespace <=
                            availableWidth;

                        label.TextAlignment =
                            centerText
                                ? TextAlignment.Center
                                : TextAlignment.Left;

                        try
                        {
                            Point itemPosition =
                                container.TranslatePoint(
                                    new Point(0, 0),
                                    DockItemsControl);

                            Point labelPosition =
                                label.TranslatePoint(
                                    new Point(0, 0),
                                    DockItemsControl);

                            DebugLog.Write(
                                "ItemLayout",
                                $"Root; Item={item}; Spacing={_settings.ItemSpacing:0.##}; ContainerX={itemPosition.X:0.##}; ContainerY={itemPosition.Y:0.##}; ContainerActualWidth={container.ActualWidth:0.##}; ContainerActualHeight={container.ActualHeight:0.##}; LabelX={labelPosition.X:0.##}; LabelY={labelPosition.Y:0.##}; LabelActualWidth={label.ActualWidth:0.##}; LabelActualHeight={label.ActualHeight:0.##}; NaturalTextWidth={formattedText.WidthIncludingTrailingWhitespace:0.##}; AvailableTextWidth={availableWidth:0.##}; TextAlignment={label.TextAlignment}");
                        }
                        catch (Exception exception)
                        {
                            DebugLog.WriteException(
                                "ItemLayout",
                                exception);
                        }
                    }
                }
            },
            DispatcherPriority.Loaded);
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent,
        string name)
        where T : FrameworkElement
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(parent);

        for (int index = 0;
             index < childCount;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);

            if (child is T element &&
                string.Equals(
                    element.Name,
                    name,
                    StringComparison.Ordinal))
            {
                return element;
            }

            T? nested =
                FindVisualChild<T>(
                    child,
                    name);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void UpdateItemsOrientation()
    {
        Orientation orientation =
            _settings.Edge is
                DockEdge.Left or
                DockEdge.Right
                ? Orientation.Vertical
                : Orientation.Horizontal;

        double itemSpacing =
            Math.Clamp(
                _settings.ItemSpacing,
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        double horizontalItemHeight =
            _settings.ShowItemLabels
                ? 58
                : 54;

        double horizontalItemVerticalMargin =
            _settings.ShowItemLabels
                ? 3
                : 0;

        Resources["DockItemWidth"] =
            itemWidth;

        foreach (DockItem item in DockItems)
        {
            int slotSpan =
                Math.Clamp(
                    item.SlotSpan,
                    1,
                    4);

            if (orientation ==
                Orientation.Horizontal)
            {
                item.LayoutWidth =
                    itemWidth *
                    slotSpan;

                item.LayoutHeight =
                    horizontalItemHeight;
            }
            else
            {
                item.LayoutWidth =
                    itemWidth;

                item.LayoutHeight =
                    (58 * slotSpan) +
                    (itemSpacing *
                     (slotSpan - 1));
            }
        }

        Resources["DockItemMargin"] =
            orientation == Orientation.Horizontal
                ? new Thickness(
                    0,
                    horizontalItemVerticalMargin,
                    0,
                    horizontalItemVerticalMargin)
                : new Thickness(
                    3,
                    itemSpacing / 2.0,
                    3,
                    itemSpacing / 2.0);

        int maxColumns =
            Math.Clamp(
                _settings.RootMaxColumns,
                1,
                50);

        bool itemsPanelChanged =
            _itemsOrientation != orientation ||
            !double.Equals(
                _itemsPanelItemWidth,
                itemWidth) ||
            _itemsPanelMaxColumns !=
            maxColumns;

        DebugLog.Write(
            "ItemLayout",
            $"Root layout update; Edge={_settings.Edge}; Spacing={_settings.ItemSpacing:0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}; Orientation={orientation}; MaxColumns={maxColumns}; ItemsPanelChanged={itemsPanelChanged}");

        if (itemsPanelChanged)
        {
            FrameworkElementFactory wrapPanelFactory =
                new(
                    typeof(WrapPanel));

            wrapPanelFactory.SetValue(
                WrapPanel.OrientationProperty,
                orientation);

            if (orientation ==
                Orientation.Horizontal)
            {
                wrapPanelFactory.SetValue(
                    FrameworkElement.WidthProperty,
                    itemWidth *
                    maxColumns);
            }
            else
            {
                wrapPanelFactory.SetValue(
                    FrameworkElement.HeightProperty,
                    itemWidth *
                    maxColumns);
            }

            DockItemsControl.ItemsPanel =
                new ItemsPanelTemplate(
                    wrapPanelFactory);

            _itemsPanelItemWidth =
                itemWidth;

            _itemsPanelMaxColumns =
                maxColumns;
        }

        _itemsOrientation =
            orientation;
    }

    private void UpdateWindowBounds(
        bool preserveHorizontalLeft = false)
    {
        if (_windowHandle == 0)
        {
            return;
        }

        MonitorInfo monitor =
            GetCurrentMonitorInfo();

        DpiScale dpi =
            VisualTreeHelper.GetDpi(
                this);

        double workLeft =
            monitor.rcWork.Left /
            dpi.DpiScaleX;

        double workTop =
            monitor.rcWork.Top /
            dpi.DpiScaleY;

        double workRight =
            monitor.rcWork.Right /
            dpi.DpiScaleX;

        double workBottom =
            monitor.rcWork.Bottom /
            dpi.DpiScaleY;

        bool vertical =
            _settings.Edge is
                DockEdge.Left or
                DockEdge.Right;

        double workAreaWidth =
            workRight -
            workLeft;

        double workAreaHeight =
            workBottom -
            workTop;

        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        double itemSpacing =
            Math.Clamp(
                _settings.ItemSpacing,
                -8,
                42);

        double itemSlotLength =
            58 +
            itemSpacing;

        int maxColumns =
            Math.Clamp(
                _settings.RootMaxColumns,
                1,
                50);

        int occupiedSlots =
            Math.Max(
                1,
                DockItems.Sum(
                    item =>
                        Math.Clamp(
                            item.SlotSpan,
                            1,
                            4)));

        int rowCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    occupiedSlots /
                    (double)maxColumns));

        int visibleColumns =
            Math.Max(
                1,
                Math.Min(
                    occupiedSlots,
                    maxColumns));

        double length =
            Math.Max(
                MinimumLength * scale,
                (visibleColumns *
                 itemSlotLength * scale) +
                (DockPaddingLength * scale));

        length =
            vertical
                ? Math.Min(
                    length,
                    workAreaHeight * 0.82)
                : Math.Min(
                    length,
                    workAreaWidth * 0.82);

        double thicknessScale =
            Math.Clamp(
                _settings.DockThicknessScale,
                0.30,
                1.50);

        double minimumContentThickness =
            (vertical
                ? itemSlotLength + 6
                : _settings.ShowItemLabels
                    ? ItemSlotLength
                    : 56) *
            scale;

        double expandedThickness =
            Math.Max(
                minimumContentThickness,
                BaseExpandedThickness *
                scale *
                thicknessScale);

        double additionalGridThickness =
            vertical
                ? itemSlotLength + 6
                : _settings.ShowItemLabels
                    ? ItemSlotLength
                    : 54;

        double expandedGridThickness =
            expandedThickness +
            ((rowCount - 1) *
             additionalGridThickness *
             scale);

        double collapsedThickness =
            Math.Clamp(
                _settings.BarThickness,
                2,
                24);

        bool keepExpandedSlideGeometry =
            string.Equals(
                _settings.AnimationStyle,
                "Slide",
                StringComparison.OrdinalIgnoreCase) &&
            (!_isExpanded ||
             _slideExpandPreparing);

        double thickness =
            _isExpanded ||
            keepExpandedSlideGeometry
                ? expandedGridThickness
                : collapsedThickness;

        double width =
            vertical
                ? thickness
                : length;

        double height =
            vertical
                ? length
                : thickness;

        double availableTravel =
            vertical
                ? Math.Max(
                    0,
                    workAreaHeight - height)
                : Math.Max(
                    0,
                    workAreaWidth - width);

        double ratio =
            Math.Clamp(
                _settings.EdgePositionRatio,
                0,
                1);

        double previousLeft =
            Left;

        bool preserveCurrentHorizontalLeft =
            preserveHorizontalLeft ||
            (!vertical &&
             _widgetCompanionItems.Count > 0);

        double left;
        double top;

        switch (_settings.Edge)
        {
            case DockEdge.Left:
                left = workLeft;
                top =
                    workTop +
                    (availableTravel * ratio);
                break;

            case DockEdge.Right:
                left =
                    workRight -
                    width;

                top =
                    workTop +
                    (availableTravel * ratio);
                break;

            case DockEdge.Top:
                left =
                    preserveCurrentHorizontalLeft
                        ? Math.Clamp(
                            previousLeft,
                            workLeft,
                            Math.Max(
                                workLeft,
                                workRight -
                                width))
                        : workLeft +
                          (availableTravel * ratio);

                top =
                    workTop;
                break;

            default:
                left =
                    preserveCurrentHorizontalLeft
                        ? Math.Clamp(
                            previousLeft,
                            workLeft,
                            Math.Max(
                                workLeft,
                                workRight -
                                width))
                        : workLeft +
                          (availableTravel * ratio);

                top =
                    workBottom -
                    height;
                break;
        }

        if (keepExpandedSlideGeometry)
        {
            double hiddenDistance =
                vertical
                    ? Math.Max(
                        0,
                        width - collapsedThickness)
                    : Math.Max(
                        0,
                        height - collapsedThickness);

            switch (_settings.Edge)
            {
                case DockEdge.Left:
                    left -=
                        hiddenDistance;
                    break;

                case DockEdge.Right:
                    left +=
                        hiddenDistance;
                    break;

                case DockEdge.Top:
                    top -=
                        hiddenDistance;
                    break;

                default:
                    top +=
                        hiddenDistance;
                    break;
            }
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        EnsureDockTopmost();

        DebugLog.Write(
            "RootBounds",
            $"Updated; Edge={_settings.Edge}; Expanded={_isExpanded}; Left={Left:0.0}; Top={Top:0.0}; Width={Width:0.0}; Height={Height:0.0}; Ratio={_settings.EdgePositionRatio:0.000}; DpiX={dpi.DpiScaleX:0.00}; DpiY={dpi.DpiScaleY:0.00}; WorkPixelLeft={monitor.rcWork.Left}; WorkPixelTop={monitor.rcWork.Top}; WorkPixelRight={monitor.rcWork.Right}; WorkPixelBottom={monitor.rcWork.Bottom}; WorkDipLeft={workLeft:0.0}; WorkDipTop={workTop:0.0}; WorkDipRight={workRight:0.0}; WorkDipBottom={workBottom:0.0}");
    }

    private void SnapToNearestEdge()
    {
        MonitorInfo monitor =
            GetCurrentMonitorInfo();

        DpiScale dpi =
            VisualTreeHelper.GetDpi(
                this);

        double workLeft =
            monitor.rcWork.Left /
            dpi.DpiScaleX;

        double workTop =
            monitor.rcWork.Top /
            dpi.DpiScaleY;

        double workRight =
            monitor.rcWork.Right /
            dpi.DpiScaleX;

        double workBottom =
            monitor.rcWork.Bottom /
            dpi.DpiScaleY;

        double leftDistance =
            Math.Abs(
                Left -
                workLeft);

        double rightDistance =
            Math.Abs(
                (Left + ActualWidth) -
                workRight);

        double topDistance =
            Math.Abs(
                Top -
                workTop);

        double bottomDistance =
            Math.Abs(
                (Top + ActualHeight) -
                workBottom);

        double minimum =
            Math.Min(
                Math.Min(
                    leftDistance,
                    rightDistance),
                Math.Min(
                    topDistance,
                    bottomDistance));

        DockEdge newEdge =
            minimum == leftDistance
                ? DockEdge.Left
                : minimum == rightDistance
                    ? DockEdge.Right
                    : minimum == topDistance
                        ? DockEdge.Top
                        : DockEdge.Bottom;

        _settings.Edge = newEdge;

        bool vertical =
            newEdge is
                DockEdge.Left or
                DockEdge.Right;

        double workAreaLength =
            vertical
                ? workBottom -
                  workTop
                : workRight -
                  workLeft;

        double dockLength =
            vertical
                ? ActualHeight
                : ActualWidth;

        double availableTravel =
            Math.Max(
                0,
                workAreaLength - dockLength);

        double axisPosition =
            vertical
                ? Top - workTop
                : Left - workLeft;

        _settings.EdgePositionRatio =
            availableTravel <= 0
                ? 0
                : Math.Clamp(
                    axisPosition /
                    availableTravel,
                    0,
                    1);

        UpdateItemsOrientation();
        ApplyAppearance();
    }

    private MonitorInfo GetCurrentMonitorInfo()
    {
        nint monitorHandle =
            MonitorFromWindow(
                _windowHandle,
                MonitorDefaultToNearest);

        MonitorInfo monitorInfo =
            new()
            {
                cbSize =
                    Marshal.SizeOf<MonitorInfo>()
            };

        GetMonitorInfo(
            monitorHandle,
            ref monitorInfo);

        return monitorInfo;
    }

    private void LogRootState(
        string eventName)
    {
        DebugLog.Write(
            "RootState",
            $"{eventName}; Edge={_settings.Edge}; Expanded={_isExpanded}; MouseOver={IsMouseOver}; WindowDragging={_isWindowDragging}; ExternalDrag={_isExternalDragActive}; CollapseAnimation={_collapseAnimationRunning}; CollapseTimer={_collapseTimer.IsEnabled}; ZoomUnlock={_zoomHoverUnlockTimer.IsEnabled}; ContextMenus={_openContextMenuCount}; SubDockOpen={_openSubDock?.IsVisible == true}; SubDockActive={_openSubDock?.IsPointerOverDockChain == true}; ClosingSubDockForCollapse={_closingSubDockForRootCollapse}; Left={Left:0.0}; Top={Top:0.0}; Width={Width:0.0}; Height={Height:0.0}; Labels={_settings.ShowItemLabels}");
    }

    private void SaveSettings()
    {
        _settings.DockItems =
            DockItems
                .Where(
                    item =>
                        !item.IsRuntimeOnly)
                .Select(CreateSettingsEntry)
                .ToList();

        _settings.Items = [];

        _settingsStore.Save(_settings);
    }

    private void ApplyNativeWindowStyle()
    {
        nint exStyle =
            GetWindowLongPtr(
                _windowHandle,
                GwlExStyle);

        long updatedStyle =
            exStyle.ToInt64() |
            WsExToolWindow;

        if (_settings.AlwaysOnTop)
        {
            updatedStyle |=
                WsExTopmost;
        }
        else
        {
            updatedStyle &=
                ~WsExTopmost;
        }

        SetWindowLongPtr(
            _windowHandle,
            GwlExStyle,
            new nint(
                updatedStyle));

        EnsureDockTopmost();
    }

    private void EnsureDockTopmost()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        Topmost =
            _settings.AlwaysOnTop;

        bool result =
            SetWindowPos(
                _windowHandle,
                _settings.AlwaysOnTop
                    ? HwndTopmost
                    : HwndNotTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate |
                SwpShowWindow);

        _nativeBackdropHost.Sync();

        DebugLog.Write(
            "ZOrder",
            $"Topmost applied; Enabled={_settings.AlwaysOnTop}; Success={result}; Edge={_settings.Edge}; Left={Left:0.0}; Top={Top:0.0}; Width={Width:0.0}; Height={Height:0.0}");
    }

    private void ApplyGlassBackdrop()
    {
        if (PresentationSource.FromVisual(this)
            is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor =
                Colors.Transparent;
        }

        Margins margins =
            new()
            {
                cxLeftWidth = -1
            };

        DwmExtendFrameIntoClientArea(
            _windowHandle,
            ref margins);

        int darkMode = 1;

        DwmSetWindowAttribute(
            _windowHandle,
            DwmwaUseImmersiveDarkMode,
            ref darkMode,
            sizeof(int));

        int cornerPreference =
            DwmWindowCornerPreferenceRound;

        DwmSetWindowAttribute(
            _windowHandle,
            DwmwaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));

        int backdropType =
            DwmSystemBackdropTypeTransientWindow;

        DwmSetWindowAttribute(
            _windowHandle,
            DwmwaSystemBackdropType,
            ref backdropType,
            sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(
        nint hwnd,
        uint dwFlags);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        nint hMonitor,
        ref MonitorInfo lpmi);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(
        nint hWnd,
        int nIndex);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(
        nint hWnd,
        int nIndex,
        nint dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(
        nint hWnd,
        ref Margins pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
