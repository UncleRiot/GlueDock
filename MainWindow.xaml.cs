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
    private const int WsExToolWindow = 0x00000080;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeTransientWindow = 3;

    private const uint MonitorDefaultToNearest = 2;

    private readonly SettingsStore _settingsStore = new();
    private readonly DockItemStore _dockItemStore = new();
    private readonly DispatcherTimer _collapseTimer;

    private DockSettings _settings = new();
    private nint _windowHandle;
    private bool _isExpanded;
    private bool _isWindowDragging;
    private bool _isExternalDragActive;
    private bool _suppressNextItemClick;

    private Point _itemMouseDownPoint;
    private DockItem? _itemMouseDownItem;
    private SettingsWindow? _settingsWindow;
    private WinForms.NotifyIcon? _notifyIcon;
    private bool _collapseAnimationRunning;
    private readonly DispatcherTimer _zoomHoverUnlockTimer;

    public ObservableCollection<DockItem> DockItems { get; } = [];

    public MainWindow()
    {
        InitializeComponent();

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

        _collapseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.CollapseDelayMilliseconds));

        bool migratedItems = false;

        foreach (string path in _settings.Items.ToList())
        {
            if (!File.Exists(path) &&
                !Directory.Exists(path))
            {
                continue;
            }

            string managedPath =
                path;

            if (!_dockItemStore.IsManagedPath(path))
            {
                try
                {
                    managedPath =
                        _dockItemStore.Import(path);

                    migratedItems = true;
                }
                catch
                {
                    managedPath = path;
                }
            }

            DockItems.Add(
                CreateDockItem(managedPath));
        }

        if (migratedItems)
        {
            SaveSettings();
        }

        StartupManager.SetStartWithWindows(
            _settings.StartWithWindows);

        InitializeTrayIcon();

        UpdateItemsOrientation();
        ApplyAppearance();

        if (_settings.CollapseDisabled)
        {
            Expand();
        }
        else
        {
            Collapse(immediate: true);
        }
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon =
            new WinForms.NotifyIcon
            {
                Text = "GlueDock",
                Visible = true,
                Icon = Drawing.SystemIcons.Application
            };

        WinForms.ContextMenuStrip menu =
            new();

        WinForms.ToolStripMenuItem showItem =
            new("Anzeigen");

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
            new("Einstellungen");

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
            new("Beenden");

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
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;

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
    }

    private void MainWindow_Closed(
        object? sender,
        EventArgs e)
    {
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
        if (_settings.CollapseDisabled ||
            _isWindowDragging ||
            _isExternalDragActive)
        {
            return;
        }

        RestartCollapseTimer();
    }

    private void CollapseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _collapseTimer.Stop();

        if (!_settings.CollapseDisabled &&
            !_isWindowDragging &&
            !_isExternalDragActive &&
            !IsMouseOver)
        {
            Collapse(immediate: false);
        }
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
        _isWindowDragging = true;

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
        SaveSettings();

        e.Handled = true;
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

        dragData.SetData(
            DataFormats.FileDrop,
            new[]
            {
                draggedItem.Path
            });

        DragDrop.DoDragDrop(
            this,
            dragData,
            DragDropEffects.Copy);
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
            LaunchDockItem(item);
        }

        e.Handled = true;
    }

    private void MainWindow_DragEnter(
        object sender,
        DragEventArgs e)
    {
        _isExternalDragActive =
            !e.Data.GetDataPresent(
                InternalDragFormat);

        _collapseTimer.Stop();
        Expand();

        SetDropEffect(e);
    }

    private void MainWindow_DragOver(
        object sender,
        DragEventArgs e)
    {
        SetDropEffect(e);
    }

    private void MainWindow_DragLeave(
        object sender,
        DragEventArgs e)
    {
        _isExternalDragActive = false;

        if (!_settings.CollapseDisabled &&
            !IsMouseOver)
        {
            RestartCollapseTimer();
        }
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
                ReorderDockItem(
                    draggedItem,
                    e.OriginalSource as DependencyObject);

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
                AddDroppedItems(paths);

                e.Effects =
                    DragDropEffects.Copy;

                e.Handled = true;
            }
        }
        finally
        {
            _isExternalDragActive = false;
            SaveSettings();

            if (!_settings.CollapseDisabled &&
                !IsMouseOver)
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

        _dockItemStore.DeleteManagedItem(
            item.Path);

        UpdateWindowBounds();
        SaveSettings();
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
                ApplySettingsLive)
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

    private void ApplySettingsLive()
    {
        _collapseTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.CollapseDelayMilliseconds));

        StartupManager.SetStartWithWindows(
            _settings.StartWithWindows);

        ApplyAppearance();

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
    }

    private void ApplyAppearance()
    {
        Color expandedColor =
            Color.FromRgb(
                0x12,
                0x16,
                0x1C);

        byte expandedAlpha =
            (byte)Math.Clamp(
                Math.Round(
                    _settings.Opacity * 255),
                0,
                255);

        if (_isExpanded)
        {
            DockChrome.Background =
                new SolidColorBrush(
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

                DockChrome.BorderBrush =
                    new SolidColorBrush(
                        Color.FromArgb(
                            0xFF,
                            borderColor.R,
                            borderColor.G,
                            borderColor.B));

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

    private void Exit_Click(
        object sender,
        RoutedEventArgs e)
    {
        Application.Current.Shutdown();
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
                    $"Das Objekt konnte nicht in GlueDock gespeichert werden.\n\n{path}\n\n{ex.Message}",
                    "GlueDock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        UpdateWindowBounds();
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

    private static DockItem CreateDockItem(
        string path)
    {
        string trimmedPath =
            path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        string displayName =
            Directory.Exists(path)
                ? Path.GetFileName(
                    trimmedPath)
                : Path.GetFileNameWithoutExtension(
                    path);

        if (string.IsNullOrWhiteSpace(
                displayName))
        {
            displayName =
                Path.GetFileName(path);
        }

        return new DockItem
        {
            Path = path,
            DisplayName = displayName,
            Icon = ShellIcon.GetIcon(path)
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
                $"Das Objekt konnte nicht gestartet werden.\n\n{item.Path}\n\n{ex.Message}",
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Expand()
    {
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

        DockChrome.BeginAnimation(
            OpacityProperty,
            null);

        DockChrome.Opacity = 1;
        DockChrome.RenderTransform = Transform.Identity;

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

                animation.Completed +=
                    (_, _) =>
                    {
                        DockChrome.RenderTransform =
                            Transform.Identity;
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
                        DockChrome.RenderTransform =
                            Transform.Identity;

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

    private void UpdateItemsOrientation()
    {
        Orientation orientation =
            _settings.Edge is
                DockEdge.Left or
                DockEdge.Right
                ? Orientation.Vertical
                : Orientation.Horizontal;

        FrameworkElementFactory stackPanelFactory =
            new(
                typeof(StackPanel));

        stackPanelFactory.SetValue(
            StackPanel.OrientationProperty,
            orientation);

        DockItemsControl.ItemsPanel =
            new ItemsPanelTemplate(
                stackPanelFactory);
    }

    private void UpdateWindowBounds()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        MonitorInfo monitor =
            GetCurrentMonitorInfo();

        bool vertical =
            _settings.Edge is
                DockEdge.Left or
                DockEdge.Right;

        double monitorWidth =
            monitor.rcMonitor.Right -
            monitor.rcMonitor.Left;

        double monitorHeight =
            monitor.rcMonitor.Bottom -
            monitor.rcMonitor.Top;

        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        double length =
            Math.Max(
                MinimumLength * scale,
                (DockItems.Count *
                 ItemSlotLength * scale) +
                (DockPaddingLength * scale));

        length =
            vertical
                ? Math.Min(
                    length,
                    monitorHeight * 0.82)
                : Math.Min(
                    length,
                    monitorWidth * 0.82);

        double thickness =
            _isExpanded
                ? BaseExpandedThickness * scale
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
                    monitorHeight - height)
                : Math.Max(
                    0,
                    monitorWidth - width);

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
                left = monitor.rcMonitor.Left;
                top =
                    monitor.rcMonitor.Top +
                    (availableTravel * ratio);
                break;

            case DockEdge.Right:
                left =
                    monitor.rcMonitor.Right -
                    width;

                top =
                    monitor.rcMonitor.Top +
                    (availableTravel * ratio);
                break;

            case DockEdge.Top:
                left =
                    monitor.rcMonitor.Left +
                    (availableTravel * ratio);

                top =
                    monitor.rcMonitor.Top;
                break;

            default:
                left =
                    monitor.rcMonitor.Left +
                    (availableTravel * ratio);

                top =
                    monitor.rcMonitor.Bottom -
                    height;
                break;
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private void SnapToNearestEdge()
    {
        MonitorInfo monitor =
            GetCurrentMonitorInfo();

        double leftDistance =
            Math.Abs(
                Left -
                monitor.rcMonitor.Left);

        double rightDistance =
            Math.Abs(
                (Left + ActualWidth) -
                monitor.rcMonitor.Right);

        double topDistance =
            Math.Abs(
                Top -
                monitor.rcMonitor.Top);

        double bottomDistance =
            Math.Abs(
                (Top + ActualHeight) -
                monitor.rcMonitor.Bottom);

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

        double monitorLength =
            vertical
                ? monitor.rcMonitor.Bottom -
                  monitor.rcMonitor.Top
                : monitor.rcMonitor.Right -
                  monitor.rcMonitor.Left;

        double dockLength =
            vertical
                ? ActualHeight
                : ActualWidth;

        double availableTravel =
            Math.Max(
                0,
                monitorLength - dockLength);

        double axisPosition =
            vertical
                ? Top - monitor.rcMonitor.Top
                : Left - monitor.rcMonitor.Left;

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

    private void SaveSettings()
    {
        _settings.Items =
            DockItems
                .Select(
                    item => item.Path)
                .ToList();

        _settingsStore.Save(
            _settings);
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
                WsExToolWindow));
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
