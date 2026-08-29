using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private bool _initialBackdropReady;
    private bool _isSettingsPreviewApply;
    private Orientation? _itemsOrientation;
    private readonly DispatcherTimer _zoomHoverUnlockTimer;

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

        DataContext = this;

        _collapseTimer = new DispatcherTimer();
        _collapseTimer.Tick += CollapseTimer_Tick;

        _zoomHoverUnlockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _zoomHoverUnlockTimer.Tick += ZoomHoverUnlockTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;

        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;

        PreviewMouseLeftButtonDown += MainWindow_PreviewMouseLeftButtonDown;

        DragEnter += MainWindow_DragEnter;
        DragOver += MainWindow_DragOver;
        DragLeave += MainWindow_DragLeave;
        Drop += MainWindow_Drop;
        Closed += MainWindow_Closed;
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

        InitializeTrayIcon();

        UpdateItemsOrientation();
        ApplyAppearance();
        UpdateItemLabelVisibility();
        UpdateDockItemPreviews();
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

                _initialBackdropReady = true;

                _nativeBackdropHost.SetBlur(
                    _settings.BlurRadius);

                _nativeBackdropHost.Sync();
            },
            DispatcherPriority.Loaded);
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            UpdateTrayMenuLanguage();
            return;
        }

        string applicationIconPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "GlueDock.ico");

        Drawing.Icon applicationIcon =
            System.IO.File.Exists(
                applicationIconPath)
                ? new Drawing.Icon(
                    applicationIconPath)
                : Drawing.SystemIcons.Application;

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
        App.Language.LanguageChanged -=
            Language_LanguageChanged;

        DebugLog.Write(
            "Root",
            "Main window closed.");

        _openSubDock?.Close();
        _openSubDock = null;

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

    private void DockItem_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        ApplyDockItemHoverEffect(
            element);

        if (_isWindowDragging ||
            !item.IsSubmenu)
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

        ResetDockItemHoverEffect(
            element);

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

        _itemMouseDownItem = item;
        _itemMouseDownPoint =
            e.GetPosition(this);

        _dragPointerOffset =
            e.GetPosition(element);

        _suppressNextItemClick = false;
        e.Handled = true;
    }

    private void DockItem_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_itemMouseDownItem is null ||
            e.LeftButton !=
            MouseButtonState.Pressed)
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

        try
        {
            DragDrop.DoDragDrop(
                this,
                dragData,
                DragDropEffects.Copy);
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
            else
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
        bool externalDrag =
            !e.Data.GetDataPresent(
                InternalDragFormat);

        if (externalDrag)
        {
            StopExternalDragLeaveTimer();

            if (!_isExternalDragActive)
            {
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
            _collapseTimer.Stop();
            return;
        }

        if (_isExternalDragActive)
        {
            _isExternalDragActive = false;

            DebugLog.Write(
                "RootDrag",
                "External drag left root dock after debounce.");
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

            UpdateInternalDragPosition(
                draggedItem,
                pointerPosition);
        }
        else if (e.Data.GetDataPresent(
                     DataFormats.FileDrop))
        {
            FrameworkElement? submenuElement =
                FindSubmenuElementAtPosition(
                    e.GetPosition(
                        DockItemsControl));

            if (submenuElement?.DataContext is DockItem submenuItem)
            {
                OpenSubmenu(
                    submenuItem,
                    submenuElement);
            }
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
                    InternalDragFormat) is DockItem)
            {
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
                FrameworkElement? submenuElement =
                    FindSubmenuElementAtPosition(
                        e.GetPosition(
                            DockItemsControl));

                if (submenuElement?.DataContext is DockItem submenuItem)
                {
                    AddDroppedItemsToSubmenu(
                        paths,
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
                        $"External drop added directly to submenu; Name={submenuItem.DisplayName}; Items={submenuItem.Children.Count}");
                }
                else
                {
                    AddDroppedItems(
                        paths);
                }

                e.Effects =
                    DragDropEffects.Copy;

                e.Handled = true;
            }
        }
        finally
        {
            StopExternalDragLeaveTimer();

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

        DockItems.Remove(item);

        DeleteManagedContent(item);

        if (ReferenceEquals(
                _openSubDock?.Tag,
                item))
        {
            _openSubDock.Close();
        }

        UpdateWindowBounds();
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
                App.Language)
            {
                Owner = this
            };

        _settingsWindow.Closed +=
            SettingsWindow_Closed;

        _settingsWindow.Show();
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
            $"Apply live; Theme={_settings.ThemeName}; CollapseDisabled={_settings.CollapseDisabled}; Delay={_settings.CollapseDelayMilliseconds}; SubdockDelay={_settings.SubdockCollapseDelayMilliseconds}; Labels={_settings.ShowItemLabels}; Previews={_settings.ShowFilePreviews}; SmallShortcutOverlay={_settings.UseSmallShortcutOverlay}; Scale={_settings.DockScale:0.00}; ItemSpacing={_settings.ItemSpacing:0}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; Animation={_settings.AnimationStyle}; Hover={_settings.HoverEffect}");

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
        UpdateItemLabelVisibility();
        UpdateDockItemPreviews();
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
                    _settings.UseSmallShortcutOverlay);

        foreach (DockItem child in item.Children)
        {
            UpdateDockItemPreview(
                child);
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

            DockChrome.Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        expandedAlpha,
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

        LinearGradientBrush surfaceBrush =
            new(
                topColor,
                bottomColor,
                new Point(
                    0.5,
                    0),
                new Point(
                    0.5,
                    1))
            {
                Opacity =
                    Math.Clamp(
                        opacity,
                        0.10,
                        1.0)
            };

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

        DebugLog.Write(
            "AeroRender",
            $"Root; GlassSurfaceEnabled={theme.GlassSurfaceEnabled}; GlassTopColor={theme.GlassTopColor}; GlassBottomColor={theme.GlassBottomColor}; GlassHighlightColor={theme.GlassHighlightColor}; GlassCornerRadius={cornerRadius:0.##}; SurfaceOpacity={surfaceBrush.Opacity:0.##}; HighlightVisibility={DockGlassHighlight.Visibility}; BorderThickness={DockGlassHighlight.BorderThickness}; FrameStops={frameBrush.GradientStops.Count}; FrameStop0={frameBrush.GradientStops[0].Color}; FrameStop1={frameBrush.GradientStops[1].Color}; FrameStop2={frameBrush.GradientStops[2].Color}");
    }

    private void ResetGlassSurface()
    {
        DockChrome.CornerRadius =
            new CornerRadius(
                20);

        DockGlassSurface.Background =
            Brushes.Transparent;

        DockGlassSurface.Effect =
            null;

        DockGlassSurface.Visibility =
            Visibility.Collapsed;

        DockGlassHighlight.Background =
            Brushes.Transparent;

        DockGlassHighlight.BorderBrush =
            Brushes.Transparent;

        DockGlassHighlight.BorderThickness =
            new Thickness(
                0);

        DockGlassHighlight.Visibility =
            Visibility.Collapsed;
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
        DockItem submenu)
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
                    _dockItemStore.Import(
                        path);

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
        IEnumerable<string> paths)
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
                    _dockItemStore.Import(path);

                DockItems.Add(
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

        UpdateWindowBounds();

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();
            },
            DispatcherPriority.Loaded);
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
                DragDropEffects.Copy;

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
                    _settings.UseSmallShortcutOverlay)
        };
    }

    private DockItem CreateDockItem(
        DockEntrySettings entry)
    {
        DockItem item =
            new()
            {
                Id =
                    string.IsNullOrWhiteSpace(entry.Id)
                        ? Guid.NewGuid().ToString("N")
                        : entry.Id,
                Path = entry.Path ?? string.Empty,
                DisplayName = entry.DisplayName ?? string.Empty,
                IsSubmenu = entry.IsSubmenu,
                SubmenuIconRepositoryPath =
                    entry.SubmenuIconRepositoryPath ?? string.Empty,
                Icon =
                    entry.IsSubmenu
                        ? SubmenuIcon.Create(
                            _settings,
                            entry.SubmenuIconRepositoryPath ?? string.Empty)
                        : ShellIcon.GetIcon(
                            entry.Path ?? string.Empty,
                            _settings.ShowFilePreviews,
                            _settings.UseSmallShortcutOverlay)
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
            SubmenuIconRepositoryPath =
                item.SubmenuIconRepositoryPath,
            Children =
                item.Children
                    .Select(CreateSettingsEntry)
                    .ToList()
        };
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

        _zoomHoverUnlockTimer.Stop();
        _collapseAnimationRunning = false;

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.RenderTransform = Transform.Identity;

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

        ApplyAppearance();
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
        bool wasAnimatedCollapse =
            _collapseAnimationRunning;

        _collapseAnimationRunning = false;
        _isExpanded = false;

        LogRootState(
            "Complete collapse");

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.Opacity = 1;
        DockChrome.RenderTransform = Transform.Identity;

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

    private void PlayExpandAnimation()
    {
        string animationStyle =
            _settings.AnimationStyle ?? "Fade";

        TimeSpan duration =
            TimeSpan.FromMilliseconds(180);

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.RenderTransform =
            Transform.Identity;

        DockChrome.RenderTransformOrigin =
            new Point(0.5, 0.5);

        switch (animationStyle)
        {
            case "Slide":
            {
                TranslateTransform transform =
                    new();

                DockChrome.CacheMode =
                    new BitmapCache
                    {
                        SnapsToDevicePixels = true
                    };

                DockChrome.RenderTransform =
                    transform;

                double offset =
                    _settings.Edge switch
                    {
                        DockEdge.Left => -24,
                        DockEdge.Right => 24,
                        DockEdge.Top => -24,
                        _ => 24
                    };

                DoubleAnimation animation =
                    new(
                        offset,
                        0,
                        new Duration(
                            TimeSpan.FromMilliseconds(
                                220)))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
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
                        DockChrome.RenderTransform =
                            Transform.Identity;

                        DockChrome.CacheMode =
                            null;
                    };

                if (_settings.Edge is DockEdge.Left or DockEdge.Right)
                {
                    transform.BeginAnimation(
                        TranslateTransform.XProperty,
                        animation);
                }
                else
                {
                    transform.BeginAnimation(
                        TranslateTransform.YProperty,
                        animation);
                }

                break;
            }

            case "Zoom":
            {
                ScaleTransform transform =
                    new(
                        0.92,
                        0.92);

                DockChrome.RenderTransform =
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
                        DockChrome.RenderTransform =
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
                DockChrome.Opacity = 0;

                DoubleAnimation animation =
                    new(
                        0,
                        1,
                        new Duration(duration))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
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
                        DockChrome.BeginAnimation(
                            OpacityProperty,
                            null);

                        DockChrome.Opacity = 1;
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

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.RenderTransform =
            Transform.Identity;

        DockChrome.RenderTransformOrigin =
            new Point(0.5, 0.5);

        switch (animationStyle)
        {
            case "Slide":
            {
                TranslateTransform transform =
                    new();

                DockChrome.CacheMode =
                    new BitmapCache
                    {
                        SnapsToDevicePixels = true
                    };

                DockChrome.RenderTransform =
                    transform;

                double offset =
                    _settings.Edge switch
                    {
                        DockEdge.Left => -24,
                        DockEdge.Right => 24,
                        DockEdge.Top => -24,
                        _ => 24
                    };

                DoubleAnimation animation =
                    new(
                        0,
                        offset,
                        new Duration(
                            TimeSpan.FromMilliseconds(
                                200)))
                    {
                        FillBehavior =
                            FillBehavior.Stop,
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
                        DockChrome.RenderTransform =
                            Transform.Identity;

                        DockChrome.CacheMode =
                            null;

                        CompleteCollapse();
                    };

                if (_settings.Edge is DockEdge.Left or DockEdge.Right)
                {
                    transform.BeginAnimation(
                        TranslateTransform.XProperty,
                        animation);
                }
                else
                {
                    transform.BeginAnimation(
                        TranslateTransform.YProperty,
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

                DockChrome.RenderTransform =
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
                        DockChrome.RenderTransform =
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
                DockChrome.Opacity = 1;

                DoubleAnimation animation =
                    new(
                        1,
                        0,
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

                animation.Completed +=
                    (_, _) =>
                    {
                        DockChrome.BeginAnimation(
                            OpacityProperty,
                            null);

                        DockChrome.Opacity = 1;

                        CompleteCollapse();
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

        Resources["DockItemWidth"] =
            itemWidth;

        Resources["DockItemMargin"] =
            orientation == Orientation.Horizontal
                ? new Thickness(
                    0,
                    3,
                    0,
                    3)
                : new Thickness(
                    3,
                    itemSpacing / 2.0,
                    3,
                    itemSpacing / 2.0);

        DebugLog.Write(
            "ItemLayout",
            $"Root layout update; Edge={_settings.Edge}; Spacing={_settings.ItemSpacing:0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}; Orientation={orientation}; ItemsPanelChanged={_itemsOrientation != orientation}");

        if (_itemsOrientation == orientation)
        {
            return;
        }

        FrameworkElementFactory stackPanelFactory =
            new(
                typeof(StackPanel));

        stackPanelFactory.SetValue(
            StackPanel.OrientationProperty,
            orientation);

        DockItemsControl.ItemsPanel =
            new ItemsPanelTemplate(
                stackPanelFactory);

        _itemsOrientation =
            orientation;
    }

    private void UpdateWindowBounds()
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

        double length =
            Math.Max(
                MinimumLength * scale,
                (DockItems.Count *
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
                0.50,
                1.00);

        double expandedThickness =
            (ItemSlotLength * scale) +
            ((BaseExpandedThickness -
              ItemSlotLength) *
             scale *
             thicknessScale);

        double thickness =
            _isExpanded
                ? expandedThickness
                : Math.Clamp(
                    _settings.BarThickness,
                    2,
                    24);

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
                    workLeft +
                    (availableTravel * ratio);

                top =
                    workTop;
                break;

            default:
                left =
                    workLeft +
                    (availableTravel * ratio);

                top =
                    workBottom -
                    height;
                break;
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
        UpdateWindowBounds();
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

        SetWindowLongPtr(
            _windowHandle,
            GwlExStyle,
            new nint(
                exStyle.ToInt64() |
                WsExTopmost |
                WsExToolWindow));

        EnsureDockTopmost();
    }

    private void EnsureDockTopmost()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        bool result =
            SetWindowPos(
                _windowHandle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate |
                SwpShowWindow);

        DebugLog.Write(
            "ZOrder",
            $"Topmost restored; Success={result}; Edge={_settings.Edge}; Left={Left:0.0}; Top={Top:0.0}; Width={Width:0.0}; Height={Height:0.0}");
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
