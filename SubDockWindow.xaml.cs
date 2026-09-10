using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;

namespace GlueDock;

public partial class SubDockWindow : Window
{
    private const string InternalDragFormat = "GlueDock.InternalDockItem";

    private readonly DockItem _submenu;
    private readonly DockSettings _settings;
    private readonly DockItemStore _dockItemStore;
    private readonly Action _save;
    private readonly Action<DockItem, DockItem> _moveItemToSubmenu;
    private readonly Func<DockItem, DockItem, bool> _canMoveItemToSubmenu;
    private readonly DockEdge _rootEdge;
    private readonly DispatcherTimer _closeTimer;
    private readonly NativeBackdropHost _nativeBackdropHost;
    private FrameworkElement? _widgetCompanionContent;

    private bool _openSlidePrepared;
    private double _preparedOpenSlideX;
    private double _preparedOpenSlideY;

    private DockItem? _mouseDownItem;
    private Point _mouseDownPoint;
    private Point _dragPointerOffset;
    private bool _suppressClick;
    private DockItem? _activeInternalDragItem;
    private int _lastInternalDragIndex = -1;
    private SubDockWindow? _childWindow;
    private int _openContextMenuCount;
    private bool _isExternalDragActive;
    private DockItem? _externalDropPlaceholder;
    private Border? _externalDropSubmenuHighlightBorder;
    private Rect _lastAnchorScreenRect;
    private DpiScale _lastAnchorDpi;
    private bool _hasPositionAnchor;
    private bool _closeAnimationRunning;
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

    public ObservableCollection<DockItem> Items => _submenu.Children;

    public bool HasOpenChild => _childWindow?.IsVisible == true;

    public bool IsPointerOverDockChain =>
        IsMouseOver ||
        _isExternalDragActive ||
        _activeInternalDragItem is not null ||
        _openContextMenuCount > 0 ||
        _childWindow?.IsPointerOverDockChain == true;

    public event EventHandler? InteractionStateChanged;

    public event EventHandler? ExternalDragEnded;

    public SubDockWindow(
        DockItem submenu,
        DockSettings settings,
        DockItemStore dockItemStore,
        Action save,
        Action<DockItem, DockItem> moveItemToSubmenu,
        Func<DockItem, DockItem, bool> canMoveItemToSubmenu,
        DockEdge rootEdge)
    {
        _submenu = submenu;
        _settings = settings;
        _dockItemStore = dockItemStore;
        _save = save;
        _moveItemToSubmenu = moveItemToSubmenu;
        _canMoveItemToSubmenu = canMoveItemToSubmenu;
        _rootEdge = rootEdge;

        InitializeComponent();
        DataContext = this;

        Topmost =
            _settings.AlwaysOnTop;

        _nativeBackdropHost =
            new NativeBackdropHost(
                this);

        ApplyInitialSize();

        AddHandler(
            ContextMenuService.ContextMenuOpeningEvent,
            new ContextMenuEventHandler(
                SubDockWindow_ContextMenuOpening),
            true);

        AddHandler(
            ContextMenuService.ContextMenuClosingEvent,
            new ContextMenuEventHandler(
                SubDockWindow_ContextMenuClosing),
            true);

        AddHandler(
            ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler(
                SubDockWindow_ToolTipOpening),
            true);

        _closeTimer =
            new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(
                    Math.Max(
                        0,
                        _settings.SubdockCollapseDelayMilliseconds))
            };

        _closeTimer.Tick += CloseTimer_Tick;

        Loaded += SubDockWindow_Loaded;

        SubDockGlassFlames.SizeChanged +=
            SubDockGlassFlames_SizeChanged;

        SubDockGlassStars.SizeChanged +=
            SubDockGlassStars_SizeChanged;

        MouseEnter +=
            (_, _) =>
            {
                _closeTimer.Stop();

                InteractionStateChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            };

        MouseLeave +=
            (_, _) =>
            {
                _closeTimer.Stop();

                InteractionStateChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            };

        DragEnter += SubDockWindow_DragEnter;
        DragOver += SubDockWindow_DragOver;
        DragLeave += SubDockWindow_DragLeave;
        Drop += SubDockWindow_Drop;

        Closed +=
            (_, _) =>
            {
                LogSubDockState(
                    "Closed");

                _closeTimer.Stop();
                _childWindow?.Close();
                StopNaturalFireHeatField();
            };
    }

    public SubDockWindow(
        FrameworkElement companionContent,
        DockSettings settings,
        DockItemStore dockItemStore,
        Action save,
        Action<DockItem, DockItem> moveItemToSubmenu,
        Func<DockItem, DockItem, bool> canMoveItemToSubmenu,
        DockEdge rootEdge)
        : this(
            new DockItem
            {
                IsSubmenu = true
            },
            settings,
            dockItemStore,
            save,
            moveItemToSubmenu,
            canMoveItemToSubmenu,
            rootEdge)
    {
        _widgetCompanionContent =
            companionContent;

        SubDockItemsControl.Visibility =
            Visibility.Collapsed;

        SubDockCompanionHost.Content =
            companionContent;

        SubDockCompanionHost.Visibility =
            Visibility.Visible;

        SubDockContentGrid.ContextMenu =
            null;

        AllowDrop =
            false;

        ApplyAppearance();
    }

    private System.Windows.Size GetWidgetCompanionWindowSize()
    {
        if (_widgetCompanionContent is null)
        {
            return new System.Windows.Size(
                Width,
                Height);
        }

        _widgetCompanionContent.Measure(
            new System.Windows.Size(
                double.PositiveInfinity,
                double.PositiveInfinity));

        double contentWidth =
            _widgetCompanionContent.DesiredSize.Width;

        double contentHeight =
            _widgetCompanionContent.DesiredSize.Height;

        if (!double.IsNaN(
                _widgetCompanionContent.Width) &&
            _widgetCompanionContent.Width > 0)
        {
            contentWidth =
                Math.Max(
                    contentWidth,
                    _widgetCompanionContent.Width);
        }

        if (!double.IsNaN(
                _widgetCompanionContent.Height) &&
            _widgetCompanionContent.Height > 0)
        {
            contentHeight =
                Math.Max(
                    contentHeight,
                    _widgetCompanionContent.Height);
        }

        Thickness padding =
            SubDockChrome.Padding;

        Thickness borderThickness =
            SubDockChrome.BorderThickness;

        return new System.Windows.Size(
            Math.Max(
                1,
                contentWidth +
                padding.Left +
                padding.Right +
                borderThickness.Left +
                borderThickness.Right),
            Math.Max(
                1,
                contentHeight +
                padding.Top +
                padding.Bottom +
                borderThickness.Top +
                borderThickness.Bottom));
    }

    private void ApplyWidgetCompanionSize()
    {
        if (_widgetCompanionContent is null)
        {
            return;
        }

        System.Windows.Size windowSize =
            GetWidgetCompanionWindowSize();

        Width =
            windowSize.Width;

        Height =
            windowSize.Height;
    }

    private double GetEffectiveSubDockScale()
    {
        return
            _settings.SubDockScale ??
            _settings.DockScale;
    }

    private double GetEffectiveSubDockItemSpacing()
    {
        return
            _settings.SubDockItemSpacing ??
            _settings.ItemSpacing;
    }

    private double GetEffectiveSubDockThicknessScale()
    {
        return
            _settings.SubDockThicknessScale ??
            _settings.DockThicknessScale;
    }

    private string GetEffectiveSubDockThemeName()
    {
        if (string.Equals(
                _settings.SubDockThemeMode,
                DockSettings.SubDockThemeModeOverride,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                _settings.SubDockThemeName))
        {
            return
                _settings.SubDockThemeName;
        }

        return
            _settings.ThemeName;
    }

    private DockTheme LoadEffectiveSubDockTheme()
    {
        DockTheme theme =
            DockThemeService.Load(
                GetEffectiveSubDockThemeName());

        if (string.Equals(
                _settings.SubDockThemeMode,
                DockSettings.SubDockThemeModeInheritAppearance,
                StringComparison.OrdinalIgnoreCase))
        {
            theme.GlassGradientAnimationEnabled =
                false;

            theme.GlassFlameEffectEnabled =
                false;

            theme.GlassFlameBaseGlowEnabled =
                false;

            theme.GlassStarEffectEnabled =
                false;
        }

        return
            theme;
    }

    private void ApplyInitialSize()
    {
        const double minimumLength = 96;
        const double baseExpandedThickness = 88;
        const double itemSlotLength = 64;
        const double dockPaddingLength = 20;

        double itemSpacing =
            Math.Clamp(
                GetEffectiveSubDockItemSpacing(),
                -8,
                42);

        double itemLength =
            58 +
            itemSpacing;

        double scale =
            Math.Clamp(
                GetEffectiveSubDockScale(),
                0.60,
                2.00);

        double thicknessScale =
            Math.Clamp(
                GetEffectiveSubDockThicknessScale(),
                0.30,
                1.50);

        double minimumContentThickness =
            (_settings.ShowItemLabels
                ? itemSlotLength
                : 56) *
            scale;

        double expandedThickness =
            Math.Max(
                minimumContentThickness,
                baseExpandedThickness *
                scale *
                thicknessScale);

        int maxColumns =
            Math.Clamp(
                _settings.SubDockMaxColumns,
                1,
                50);

        int rowCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    Items.Count /
                    (double)maxColumns));

        int visibleColumns =
            Math.Max(
                1,
                Math.Min(
                    Items.Count,
                    maxColumns));

        Width =
            Math.Max(
                minimumLength,
                (visibleColumns *
                 itemLength *
                 scale) +
                dockPaddingLength +
                2);

        Height =
            expandedThickness +
            ((rowCount - 1) *
             itemSlotLength *
             scale);
    }

    private static double SnapToDevicePixel(
        double value,
        double dpiScale)
    {
        if (dpiScale <= 0)
        {
            return value;
        }

        return
            Math.Round(
                value * dpiScale,
                MidpointRounding.AwayFromZero) /
            dpiScale;
    }

    public void PositionNextTo(
        Rect anchorScreenRect,
        DpiScale anchorDpi)
    {
        _lastAnchorScreenRect =
            anchorScreenRect;

        _lastAnchorDpi =
            anchorDpi;

        _hasPositionAnchor = true;

        const double minimumLength = 96;
        const double baseExpandedThickness = 88;
        const double itemSlotLength = 64;
        const double dockPaddingLength = 20;
        const double edgeGap = 6;

        int anchorPixelX =
            (int)Math.Round(
                (anchorScreenRect.Left +
                 (anchorScreenRect.Width / 2)) *
                anchorDpi.DpiScaleX);

        int anchorPixelY =
            (int)Math.Round(
                (anchorScreenRect.Top +
                 (anchorScreenRect.Height / 2)) *
                anchorDpi.DpiScaleY);

        System.Windows.Forms.Screen screen =
            System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(
                    anchorPixelX,
                    anchorPixelY));

        System.Drawing.Rectangle pixelArea =
            screen.WorkingArea;

        Rect area =
            new(
                pixelArea.Left /
                anchorDpi.DpiScaleX,
                pixelArea.Top /
                anchorDpi.DpiScaleY,
                pixelArea.Width /
                anchorDpi.DpiScaleX,
                pixelArea.Height /
                anchorDpi.DpiScaleY);

        double itemSpacing =
            Math.Clamp(
                GetEffectiveSubDockItemSpacing(),
                -8,
                42);

        double itemLength =
            58 +
            itemSpacing;

        double scale =
            Math.Clamp(
                GetEffectiveSubDockScale(),
                0.60,
                2.00);

        DockTheme theme =
            LoadEffectiveSubDockTheme();

        int maxColumns =
            Math.Clamp(
                _settings.SubDockMaxColumns,
                1,
                50);

        int maximumColumnsForScreen =
            Math.Max(
                1,
                (int)Math.Floor(
                    Math.Max(
                        0,
                        area.Width -
                        dockPaddingLength -
                        2) /
                    Math.Max(
                        1,
                        itemLength *
                        scale)));

        int visibleColumns =
            Math.Max(
                1,
                Math.Min(
                    Math.Min(
                        Items.Count,
                        maxColumns),
                    maximumColumnsForScreen));

        int rowCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    Items.Count /
                    (double)visibleColumns));

        SubDockItemsControl.Width =
            itemLength *
            visibleColumns;

        double width =
            Math.Max(
                minimumLength,
                (visibleColumns *
                 itemLength *
                 scale) +
                dockPaddingLength +
                2);

        double thicknessScale =
            Math.Clamp(
                GetEffectiveSubDockThicknessScale(),
                0.30,
                1.50);

        double minimumContentThickness =
            (_settings.ShowItemLabels
                ? itemSlotLength
                : 56) *
            scale;

        double height =
            Math.Max(
                minimumContentThickness,
                baseExpandedThickness *
                scale *
                thicknessScale) +
            ((rowCount - 1) *
             itemSlotLength *
             scale);

        if (_widgetCompanionContent is not null)
        {
            System.Windows.Size companionWindowSize =
                GetWidgetCompanionWindowSize();

            width =
                companionWindowSize.Width;

            height =
                companionWindowSize.Height;
        }

        width =
            Math.Min(
                width,
                area.Width);

        height =
            Math.Min(
                height,
                area.Height);

        width =
            SnapToDevicePixel(
                width,
                anchorDpi.DpiScaleX);

        height =
            SnapToDevicePixel(
                height,
                anchorDpi.DpiScaleY);

        Width =
            width;

        Height =
            height;

        double left;
        double top;

        if (_rootEdge is
            DockEdge.Top or
            DockEdge.Bottom)
        {
            left =
                anchorScreenRect.Left +
                (anchorScreenRect.Width / 2) -
                (width / 2);

            if (left < area.Left)
            {
                left =
                    area.Left;
            }
            else if (left + width > area.Right)
            {
                left =
                    area.Right -
                    width;
            }

            double preferredTop =
                _rootEdge == DockEdge.Top
                    ? anchorScreenRect.Bottom + edgeGap
                    : anchorScreenRect.Top - height - edgeGap;

            double alternateTop =
                _rootEdge == DockEdge.Top
                    ? anchorScreenRect.Top - height - edgeGap
                    : anchorScreenRect.Bottom + edgeGap;

            bool preferredFits =
                preferredTop >= area.Top &&
                preferredTop + height <= area.Bottom;

            bool alternateFits =
                alternateTop >= area.Top &&
                alternateTop + height <= area.Bottom;

            if (preferredFits)
            {
                top =
                    preferredTop;
            }
            else if (alternateFits)
            {
                top =
                    alternateTop;
            }
            else
            {
                top =
                    Math.Clamp(
                        preferredTop,
                        area.Top,
                        Math.Max(
                            area.Top,
                            area.Bottom - height));
            }
        }
        else
        {
            top =
                anchorScreenRect.Top +
                (anchorScreenRect.Height / 2) -
                (height / 2);

            top =
                Math.Clamp(
                    top,
                    area.Top,
                    Math.Max(
                        area.Top,
                        area.Bottom - height));

            double preferredLeft =
                _rootEdge == DockEdge.Left
                    ? anchorScreenRect.Right + edgeGap
                    : anchorScreenRect.Left - width - edgeGap;

            double alternateLeft =
                _rootEdge == DockEdge.Left
                    ? anchorScreenRect.Left - width - edgeGap
                    : anchorScreenRect.Right + edgeGap;

            bool preferredFits =
                preferredLeft >= area.Left &&
                preferredLeft + width <= area.Right;

            bool alternateFits =
                alternateLeft >= area.Left &&
                alternateLeft + width <= area.Right;

            if (preferredFits)
            {
                left =
                    preferredLeft;
            }
            else if (alternateFits)
            {
                left =
                    alternateLeft;
            }
            else
            {
                left =
                    Math.Clamp(
                        preferredLeft,
                        area.Left,
                        Math.Max(
                            area.Left,
                            area.Right - width));
            }
        }

        left =
            SnapToDevicePixel(
                left,
                anchorDpi.DpiScaleX);

        top =
            SnapToDevicePixel(
                top,
                anchorDpi.DpiScaleY);

        Left =
            left;

        Top =
            top;

        LogSubDockState(
            "PositionNextTo completed");
    }

    private void SubDockWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        LogSubDockState(
            "Loaded");

        NewSubmenuContextMenuItem.Header = App.Language["Submenu.New"];
        ApplyAppearance();
        UpdateItemLabelVisibility();
        ApplySubDockTooltipSetting();

        PlayOpenAnimation();
    }

    private void UpdateItemsPanelWidth()
    {
        int maxColumns =
            Math.Clamp(
                _settings.SubDockMaxColumns,
                1,
                50);

        int visibleColumns =
            Math.Max(
                1,
                Math.Min(
                    Items.Count,
                    maxColumns));

        double itemSpacing =
            Math.Clamp(
                GetEffectiveSubDockItemSpacing(),
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        SubDockItemsControl.Width =
            itemWidth *
            visibleColumns;
    }

    public void RefreshItemsLayout()
    {
        UpdateItemsPanelWidth();

        if (_hasPositionAnchor)
        {
            PositionNextTo(
                _lastAnchorScreenRect,
                _lastAnchorDpi);
        }
        else
        {
            ApplyInitialSize();
        }

        InvalidateMeasure();
        InvalidateArrange();
        UpdateLayout();

        UpdateItemLabelVisibility();

        _childWindow?.RefreshItemsLayout();
    }

    public void ClearInternalDragPreview()
    {
        HideInternalDragGhost();

        RemoveExternalDropPlaceholder(
            refreshLayout: true);

        ClearExternalDropSubmenuHighlight();
    }

    public void ApplyItemSpacingLive(
        Rect anchorScreenRect,
        DpiScale anchorDpi)
    {
        double itemSpacing =
            Math.Clamp(
                GetEffectiveSubDockItemSpacing(),
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        Resources["SubDockItemWidth"] =
            itemWidth;

        Resources["SubDockItemHeight"] =
            _settings.ShowItemLabels
                ? 58.0
                : 54.0;

        double itemVerticalMargin =
            _settings.ShowItemLabels
                ? 3
                : 0;

        Resources["SubDockItemMargin"] =
            new Thickness(
                0,
                itemVerticalMargin,
                0,
                itemVerticalMargin);

        UpdateItemsPanelWidth();

        DebugLog.Write(
            "ItemLayout",
            $"SubDock spacing-only update; Name={_submenu.DisplayName}; Spacing={GetEffectiveSubDockItemSpacing():0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}");

        PositionNextTo(
            anchorScreenRect,
            anchorDpi);

        InvalidateMeasure();
        InvalidateArrange();
        UpdateLayout();

        UpdateItemLabelVisibility();

        _childWindow?.RefreshItemsLayout();
    }

    public void ApplySettingsLive()
    {
        Topmost =
            _settings.AlwaysOnTop;

        _closeTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.SubdockCollapseDelayMilliseconds));

        ApplyAppearance();
        ApplyWidgetAppearance(
            Items);
        RefreshItemsLayout();
        ApplySubDockTooltipSetting();
        RefreshItemHoverEffects();
        _childWindow?.ApplySettingsLive();
    }

    private void ApplySubDockTooltipSetting()
    {
        ToolTipService.SetIsEnabled(
            SubDockItemsControl,
            _settings.ShowSubDockTooltips);

        for (int index = 0;
             index < SubDockItemsControl.Items.Count;
             index++)
        {
            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromIndex(
                    index) is not DependencyObject container)
            {
                continue;
            }

            Border? border =
                FindVisualChild<Border>(
                    container,
                    "SubDockItemBorder");

            if (border is null)
            {
                continue;
            }

            ApplyToolTipSettingToVisualTree(
                border,
                _settings.ShowSubDockTooltips);
        }

        DebugLog.Write(
            "ToolTip",
            $"Applied; Scope=SubDock; Name={_submenu.DisplayName}; Enabled={_settings.ShowSubDockTooltips}; Items={SubDockItemsControl.Items.Count}");
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

    private GlueDockWidgetAppearance CreateWidgetAppearance()
    {
        string themeName =
            GetEffectiveSubDockThemeName();

        DockTheme theme =
            LoadEffectiveSubDockTheme();

        bool isMica =
            string.Equals(
                themeName,
                "Mica",
                StringComparison.OrdinalIgnoreCase);

        return
            new GlueDockWidgetAppearance
            {
                ThemeName =
                    themeName,
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
                    88,
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
                        88
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
        string themeName =
            GetEffectiveSubDockThemeName();

        DockTheme theme =
            LoadEffectiveSubDockTheme();

        bool isMica =
            string.Equals(
                themeName,
                "Mica",
                StringComparison.OrdinalIgnoreCase);

        double effectiveBlurRadius =
            isMica
                ? 0
                : _settings.BlurRadius;

        _nativeBackdropHost.SetBlur(
            effectiveBlurRadius);

        Color background =
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

        byte alpha =
            (byte)Math.Clamp(
                Math.Round(
                    effectiveOpacity * 255),
                0,
                255);

        DebugLog.Write(
            "Material",
            $"SubDock; Name={_submenu.DisplayName}; Theme={themeName}; Mica={isMica}; UserOpacity={_settings.Opacity:0.00}; EffectiveOpacity={effectiveOpacity:0.00}; UserBlur={_settings.BlurRadius:0.##}; EffectiveBlur={effectiveBlurRadius:0.##}");

        Resources["DockBackgroundBrush"] =
            new SolidColorBrush(
                Color.FromArgb(
                    alpha,
                    background.R,
                    background.G,
                    background.B));

        System.Windows.Media.Brush itemBackgroundBrush =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBackgroundColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        System.Windows.Media.Brush itemBorderBrush =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBorderColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        System.Windows.Media.Brush textBrush =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.TextColor,
                    Colors.White));

        Resources["SubDockItemBackgroundBrush"] =
            itemBackgroundBrush;

        Resources["SubDockItemBorderBrush"] =
            itemBorderBrush;

        Resources["SubDockTextBrush"] =
            textBrush;

        Resources["DockItemBackgroundBrush"] =
            itemBackgroundBrush;

        Resources["DockItemBorderBrush"] =
            itemBorderBrush;

        Resources["DockTextBrush"] =
            textBrush;

        Resources["SubDockIndicatorBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.SubmenuIndicatorColor,
                    Colors.White));

        ApplyGlassSurface(
            theme,
            effectiveOpacity,
            effectiveBlurRadius);

        SubDockChrome.Background =
            theme.GlassSurfaceEnabled
                ? Brushes.Transparent
                : new SolidColorBrush(
                    Color.FromArgb(
                        alpha,
                        background.R,
                        background.G,
                        background.B));

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
                    Colors.White;
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

                SubDockChrome.BorderBrush =
                    borderBrush;
            }
            else
            {
                SubDockChrome.BorderBrush =
                    new SolidColorBrush(
                        borderColor);
            }

            SubDockChrome.BorderThickness =
                new Thickness(1);
        }
        else
        {
            SubDockChrome.BorderBrush =
                Brushes.Transparent;

            SubDockChrome.BorderThickness =
                new Thickness(0);
        }

        double itemSpacing =
            Math.Clamp(
                GetEffectiveSubDockItemSpacing(),
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        Resources["SubDockItemWidth"] =
            itemWidth;

        DebugLog.Write(
            "ItemLayout",
            $"SubDock layout update; Name={_submenu.DisplayName}; Spacing={GetEffectiveSubDockItemSpacing():0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}");

        Resources["SubDockItemHeight"] =
            _settings.ShowItemLabels
                ? 58.0
                : 54.0;

        double itemVerticalMargin =
            _settings.ShowItemLabels
                ? 3
                : 0;

        Resources["SubDockItemMargin"] =
            new Thickness(
                0,
                itemVerticalMargin,
                0,
                itemVerticalMargin);

        double scale =
            Math.Clamp(
                GetEffectiveSubDockScale(),
                0.60,
                2.00);

        double thicknessScale =
            Math.Clamp(
                GetEffectiveSubDockThicknessScale(),
                0.30,
                1.50);

        double basePadding =
            10 * scale;

        double minimumContentThickness =
            (_settings.ShowItemLabels
                ? 64
                : 56) *
            scale;

        double expandedThickness =
            Math.Max(
                minimumContentThickness,
                88 *
                scale *
                thicknessScale);

        double thicknessPadding =
            Math.Max(
                0,
                (expandedThickness -
                 minimumContentThickness) /
                2.0);

        Thickness chromePadding =
            new(
                basePadding,
                thicknessPadding,
                basePadding,
                thicknessPadding);

        SubDockChrome.Padding =
            chromePadding;

        SubDockGlassSurface.Margin =
            new Thickness(
                -chromePadding.Left,
                -chromePadding.Top,
                -chromePadding.Right,
                -chromePadding.Bottom);

        SubDockGlassHighlight.Margin =
            SubDockGlassSurface.Margin;

        SubDockGlassFlames.Margin =
            SubDockGlassSurface.Margin;

        SubDockGlassStars.Margin =
            SubDockGlassSurface.Margin;

        SubDockItemsControl.LayoutTransform =
            new ScaleTransform(
                scale,
                scale);

        ApplyWidgetCompanionSize();

        if (_widgetCompanionContent is not null &&
            _hasPositionAnchor)
        {
            PositionNextTo(
                _lastAnchorScreenRect,
                _lastAnchorDpi);
        }
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

        SubDockChrome.CornerRadius =
            new CornerRadius(
                cornerRadius);

        SubDockGlassSurface.CornerRadius =
            new CornerRadius(
                cornerRadius);

        SubDockGlassHighlight.CornerRadius =
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
                SubDockGlassSurface.ActualWidth,
                SubDockChrome.ActualWidth);

        double gradientSurfaceHeight =
            Math.Max(
                SubDockGlassSurface.ActualHeight,
                SubDockChrome.ActualHeight);

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
            SubDockGlassSurface.Background
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
                "SubDock REUSE; Existing animation kept running.");
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

        SubDockGlassSurface.Background =
            surfaceBrush;

        ApplyGlassFlameEffect(
            theme);

        ApplyGlassStarEffect(
            theme);

        if (!reuseGradientAnimation)
        {
            StartGlassGradientDebugSampling(
                theme,
                surfaceBrush);
        }

        SubDockGlassSurface.Effect =
            null;

        SubDockGlassSurface.Visibility =
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

        SubDockGlassHighlight.Background =
            highlightBrush;

        SubDockGlassHighlight.BorderBrush =
            frameBrush;

        SubDockGlassHighlight.BorderThickness =
            new Thickness(
                1);

        SubDockGlassHighlight.Visibility =
            Visibility.Visible;

        DebugLog.Write(
            "AeroRender",
            $"SubDock; Name={_submenu.DisplayName}; GlassSurfaceEnabled={theme.GlassSurfaceEnabled}; GlassTopColor={theme.GlassTopColor}; GlassBottomColor={theme.GlassBottomColor}; GlassHighlightColor={theme.GlassHighlightColor}; GlassCornerRadius={cornerRadius:0.##}; SurfaceOpacity={surfaceBrush.Opacity:0.##}; HighlightVisibility={SubDockGlassHighlight.Visibility}; BorderThickness={SubDockGlassHighlight.BorderThickness}; FrameStops={frameBrush.GradientStops.Count}; FrameStop0={frameBrush.GradientStops[0].Color}; FrameStop1={frameBrush.GradientStops[1].Color}; FrameStop2={frameBrush.GradientStops[2].Color}");
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
            $"SubDock START; Sequence={_glassGradientDebugSequence}; Name={_submenu.DisplayName}; Theme={theme.Name}; Mode={theme.GlassGradientAnimationMode}; RotationDegrees={theme.GlassGradientAnimationRotationDegrees:0.###}; DurationSeconds={theme.GlassGradientAnimationDurationSeconds:0.###}; AutoReverse={theme.GlassGradientAnimationAutoReverse}; Easing={theme.GlassGradientAnimationEasing}; EasingMode={theme.GlassGradientAnimationEasingMode}; AspectCorrect={theme.GlassGradientAnimationAspectCorrect}; SurfaceSize=({SubDockGlassSurface.ActualWidth:0.###},{SubDockGlassSurface.ActualHeight:0.###}); Start=({theme.GlassGradientStartX:0.######},{theme.GlassGradientStartY:0.######}); End=({theme.GlassGradientEndX:0.######},{theme.GlassGradientEndY:0.######}); ToStart=({theme.GlassGradientAnimationToStartX:0.######},{theme.GlassGradientAnimationToStartY:0.######}); ToEnd=({theme.GlassGradientAnimationToEndX:0.######},{theme.GlassGradientAnimationToEndY:0.######}); KeyFrames={keyFrames}");

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
                SubDockGlassSurface.ActualWidth,
                1.0);

        double surfaceHeight =
            Math.Max(
                SubDockGlassSurface.ActualHeight,
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
            $"SubDock RENDER; Sequence={_glassGradientDebugSequence}; Name={_submenu.DisplayName}; Frame={_glassGradientDebugRenderFrameIndex}; RenderingTimeMs={(renderingTime.HasValue ? renderingTime.Value.TotalMilliseconds.ToString("0.###") : "n/a")}; ElapsedMs={elapsedMilliseconds}; DeltaMs={deltaMilliseconds}; CycleProgress={cycleProgress:0.######}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); Start=({startPoint.X:0.######},{startPoint.Y:0.######}); End=({endPoint.X:0.######},{endPoint.Y:0.######}); VisualVectorLength={visualVectorLength:0.######}; VisualAngleDegrees={visualAngleDegrees:0.###}; VisualAngularDeltaDegrees={(visualAngularDeltaDegrees.HasValue ? visualAngularDeltaDegrees.Value.ToString("0.###") : "n/a")}; VisualAngularSpeedDegreesPerSecond={(visualAngularSpeedDegreesPerSecond.HasValue ? visualAngularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}; HasAnimatedProperties={_glassGradientDebugBrush.HasAnimatedProperties}; SurfaceVisible={SubDockGlassSurface.IsVisible}; SurfaceOpacity={SubDockGlassSurface.Opacity:0.###}");

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
                SubDockGlassSurface.ActualWidth,
                1.0);

        double surfaceHeight =
            Math.Max(
                SubDockGlassSurface.ActualHeight,
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
            $"SubDock SAMPLE; Sequence={_glassGradientDebugSequence}; Name={_submenu.DisplayName}; ElapsedMs={elapsedMilliseconds}; DeltaMs={deltaMilliseconds}; CycleProgress={cycleProgress:0.######}; Segment={activeSegment}; SurfaceSize=({SubDockGlassSurface.ActualWidth:0.###},{SubDockGlassSurface.ActualHeight:0.###}); Start=({startPoint.X:0.######},{startPoint.Y:0.######}); End=({endPoint.X:0.######},{endPoint.Y:0.######}); VectorLength={vectorLength:0.######}; AngleDegrees={angleDegrees:0.###}; AngularDeltaDegrees={(angularDeltaDegrees.HasValue ? angularDeltaDegrees.Value.ToString("0.###") : "n/a")}; AngularSpeedDegreesPerSecond={(angularSpeedDegreesPerSecond.HasValue ? angularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}; VisualAngleDegrees={visualAngleDegrees:0.###}; VisualAngularDeltaDegrees={(visualAngularDeltaDegrees.HasValue ? visualAngularDeltaDegrees.Value.ToString("0.###") : "n/a")}; VisualAngularSpeedDegreesPerSecond={(visualAngularSpeedDegreesPerSecond.HasValue ? visualAngularSpeedDegreesPerSecond.Value.ToString("0.###") : "n/a")}");

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

    private void SubDockGlassStars_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 1 ||
            e.NewSize.Height <= 1)
        {
            return;
        }

        DockTheme theme =
            LoadEffectiveSubDockTheme();

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
        Canvas starCanvas =
            SubDockGlassStars;

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
                SubDockChrome.ActualWidth);

        double surfaceHeight =
            Math.Max(
                starCanvas.ActualHeight,
                SubDockChrome.ActualHeight);

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

    private void SubDockGlassFlames_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 1 ||
            e.NewSize.Height <= 1)
        {
            return;
        }

        DockTheme theme =
            LoadEffectiveSubDockTheme();

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
            SubDockGlassFlames;

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
            Math.Max(
                flameCanvas.ActualWidth,
                SubDockChrome.ActualWidth);

        double surfaceHeight =
            Math.Max(
                flameCanvas.ActualHeight,
                SubDockChrome.ActualHeight);

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
            $"SubDock MASK; Theme={theme.Name}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); TopFadeStartRatio={topFadeStartRatio:0.###}; TopFadeEndRatio={topFadeEndRatio:0.###}; ClipToBounds={flameCanvas.ClipToBounds}");

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
                    $"SubDock NATURAL_FORMULA; Theme={theme.Name}; X={layer.X:0.###}; Lifetime={durationSeconds:0.###}; Delay={delaySeconds:0.###}; Rise={effectiveRiseDistance:0.###}; Width={flameWidth:0.###}; Height={flameHeight:0.###}; YKeyFrames=[(0,0),(0.22,{-effectiveRiseDistance * 0.16:0.###}),(0.58,{-effectiveRiseDistance * 0.54:0.###}),(1,{-effectiveRiseDistance:0.###})]; ScaleXKeyFrames=[(0,{maximumScaleX * 0.78:0.###}),(0.14,{maximumScaleX:0.###}),(0.52,{minimumScaleX + ((maximumScaleX - minimumScaleX) * 0.42):0.###}),(0.82,{minimumScaleX * 0.52:0.###}),(1,{Math.Max(0.12, minimumScaleX * 0.24):0.###})]; ScaleYKeyFrames=[(0,{minimumScaleY * 0.72:0.###}),(0.24,{maximumScaleY:0.###}),(0.63,{minimumScaleY + ((maximumScaleY - minimumScaleY) * 0.58):0.###}),(1,{minimumScaleY * 0.48:0.###})]; OpacityKeyFrames=[(0,0),(0.07,{maximumOpacity * (0.74 - (flickerRatio * 0.12)):0.###}),(0.20,{maximumOpacity:0.###}),(0.52,{maximumOpacity * (0.78 - (flickerRatio * 0.18)):0.###}),(0.78,{maximumOpacity * 0.34:0.###}),(1,0)]");
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
                $"SubDock LAYER; Theme={theme.Name}; MotionMode={layer.MotionMode}; X={layer.X:0.###}; Width={flameWidth:0.###}; Height={flameHeight:0.###}; CanvasTop={Canvas.GetTop(flame):0.###}; RiseDistance={riseDistance:0.###}; DriftDistance={driftDistance:0.###}; DurationSeconds={durationSeconds:0.###}; DelaySeconds={delaySeconds:0.###}; Opacity={layer.Opacity:0.###}; SurfaceHeight={surfaceHeight:0.###}; ClipToBounds={flameCanvas.ClipToBounds}");

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
                new Rect(
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
            $"SubDock START; Theme={theme.Name}; SurfaceSize=({surfaceWidth:0.###},{surfaceHeight:0.###}); Grid={_naturalFireGridWidth}x{_naturalFireGridHeight}; Formula=bottom heat source -> weighted heat from rows below + lateral jitter - altitude cooling; Palette={tipColor}/{midColor}/{coreColor}; MaxOpacity={maximumOpacity:0.###}");
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
            $"SubDock FRAME; Frame={_naturalFireFrameIndex}; DeltaMs={elapsedMilliseconds:0.###}; ActiveCells={activeCells}/{_naturalFireHeat.Length}; AverageHeat={(double)totalHeat / _naturalFireHeat.Length:0.###}; MaxHeat={maximumHeat}; BottomAverage={bottomAverage:0.###}; MiddleAverage={middleAverage:0.###}; TopAverage={topAverage:0.###}");
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

        SubDockChrome.CornerRadius =
            new CornerRadius(
                20);

        SubDockGlassSurface.Background =
            Brushes.Transparent;

        SubDockGlassSurface.Effect =
            null;

        SubDockGlassSurface.Visibility =
            Visibility.Collapsed;

        SubDockGlassFlames.Children.Clear();
        SubDockGlassFlames.Visibility =
            Visibility.Collapsed;

        _glassFlameAnimationSignature =
            null;

        SubDockGlassStars.Children.Clear();
        SubDockGlassStars.Visibility =
            Visibility.Collapsed;

        _glassStarAnimationSignature =
            null;

        SubDockGlassHighlight.Background =
            Brushes.Transparent;

        SubDockGlassHighlight.BorderBrush =
            Brushes.Transparent;

        SubDockGlassHighlight.BorderThickness =
            new Thickness(
                0);

        SubDockGlassHighlight.Visibility =
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

    private void UpdateItemLabelVisibility()
    {
        DebugLog.Write(
            "Labels",
            $"Update submenu labels; Name={_submenu.DisplayName}; Enabled={_settings.ShowItemLabels}; Items={SubDockItemsControl.Items.Count}");

        Dispatcher.BeginInvoke(
            () =>
            {
                for (int index = 0;
                     index < SubDockItemsControl.Items.Count;
                     index++)
                {
                    if (SubDockItemsControl.ItemContainerGenerator.ContainerFromIndex(index)
                        is not FrameworkElement container)
                    {
                        continue;
                    }

                    TextBlock? label =
                        FindVisualChild<TextBlock>(
                            container,
                            "SubDockItemLabel");

                    if (label is null)
                    {
                        continue;
                    }

                    if (container.DataContext is DockItem dockItem &&
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
                }

                SubDockItemsControl.UpdateLayout();

                Dispatcher.BeginInvoke(
                    () =>
                    {
                        SubDockItemsControl.UpdateLayout();

                        for (int index = 0;
                             index < SubDockItemsControl.Items.Count;
                             index++)
                        {
                            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromIndex(index)
                                is not FrameworkElement container)
                            {
                                continue;
                            }

                            TextBlock? label =
                                FindVisualChild<TextBlock>(
                                    container,
                                    "SubDockItemLabel");

                            if (label is null ||
                                label.Visibility != Visibility.Visible)
                            {
                                continue;
                            }

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
                                        SubDockItemsControl);

                                Point labelPosition =
                                    label.TranslatePoint(
                                        new Point(0, 0),
                                        SubDockItemsControl);

                                DebugLog.Write(
                                    "ItemLayout",
                                    $"SubDock; Name={_submenu.DisplayName}; Index={index}; Spacing={GetEffectiveSubDockItemSpacing():0.##}; ContainerX={itemPosition.X:0.##}; ContainerY={itemPosition.Y:0.##}; ContainerActualWidth={container.ActualWidth:0.##}; ContainerActualHeight={container.ActualHeight:0.##}; LabelX={labelPosition.X:0.##}; LabelY={labelPosition.Y:0.##}; LabelActualWidth={label.ActualWidth:0.##}; LabelActualHeight={label.ActualHeight:0.##}; NaturalTextWidth={formattedText.WidthIncludingTrailingWhitespace:0.##}; AvailableTextWidth={availableWidth:0.##}; TextAlignment={label.TextAlignment}");
                            }
                            catch (Exception exception)
                            {
                                DebugLog.WriteException(
                                    "ItemLayout",
                                    exception);
                            }
                        }
                    },
                    DispatcherPriority.Render);
            },
            DispatcherPriority.Loaded);
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent,
        string name)
        where T : FrameworkElement
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (int index = 0;
             index < childCount;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);

            if (child is T element &&
                element.Name == name)
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

        item.NotifyWidgetStateChanged();

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

        if (FindContextMenuItem(
                contextMenu.Items,
                "DisableAutohideMenuItem") is MenuItem disableAutohideMenuItem)
        {
            disableAutohideMenuItem.Header =
                App.Language["Context.DisableAutohide"];

            disableAutohideMenuItem.IsChecked =
                _settings.CollapseDisabled;
        }
    }

    private void DisableAutohideMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        _settings.CollapseDisabled =
            menuItem.IsChecked;

        _save();
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

    private void SubDockWindow_ToolTipOpening(
        object sender,
        ToolTipEventArgs e)
    {
        if (_settings.ShowSubDockTooltips)
        {
            return;
        }

        e.Handled =
            true;

        DebugLog.Write(
            "ToolTip",
            $"Suppressed; Scope=SubDock; Name={_submenu.DisplayName}");
    }

    private void SubDockWindow_ContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        _openContextMenuCount++;

        LogSubDockState(
            "Context menu opening");

        _closeTimer.Stop();

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void SubDockWindow_ContextMenuClosing(
        object sender,
        ContextMenuEventArgs e)
    {
        if (_openContextMenuCount > 0)
        {
            _openContextMenuCount--;
        }

        LogSubDockState(
            "Context menu closing");

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);

        _closeTimer.Stop();
    }

    private void CloseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        LogSubDockState(
            "Close timer tick");

        _closeTimer.Stop();

        if (_activeInternalDragItem is not null ||
            IsPointerOverDockChain)
        {
            return;
        }

        CloseAnimated();
    }

    public void CloseAnimated()
    {
        if (_closeAnimationRunning)
        {
            return;
        }

        if (!IsLoaded ||
            !IsVisible)
        {
            Close();
            return;
        }

        _closeAnimationRunning = true;

        PlayCloseAnimation();
    }

    private void PrepareOpenSlideStartState()
    {
        bool opensBelowAnchor =
            _hasPositionAnchor &&
            Top >=
            _lastAnchorScreenRect.Bottom;

        bool opensAboveAnchor =
            _hasPositionAnchor &&
            Top + Height <=
            _lastAnchorScreenRect.Top;

        bool opensRightOfAnchor =
            _hasPositionAnchor &&
            Left >=
            _lastAnchorScreenRect.Right;

        bool opensLeftOfAnchor =
            _hasPositionAnchor &&
            Left + Width <=
            _lastAnchorScreenRect.Left;

        double horizontalTravel =
            _hasPositionAnchor
                ? Math.Max(
                    24,
                    _lastAnchorScreenRect.Width)
                : 24;

        double verticalTravel =
            _hasPositionAnchor
                ? Math.Max(
                    24,
                    _lastAnchorScreenRect.Height)
                : 24;

        double startX =
            0;

        double startY =
            0;

        if (opensBelowAnchor)
        {
            startY =
                -verticalTravel;
        }
        else if (opensAboveAnchor)
        {
            startY =
                verticalTravel;
        }
        else if (opensRightOfAnchor)
        {
            startX =
                -horizontalTravel;
        }
        else if (opensLeftOfAnchor)
        {
            startX =
                horizontalTravel;
        }
        else
        {
            switch (_rootEdge)
            {
                case DockEdge.Left:
                    startX =
                        -horizontalTravel;
                    break;

                case DockEdge.Right:
                    startX =
                        horizontalTravel;
                    break;

                case DockEdge.Top:
                    startY =
                        -verticalTravel;
                    break;

                default:
                    startY =
                        verticalTravel;
                    break;
            }
        }

        _preparedOpenSlideX =
            startX;

        _preparedOpenSlideY =
            startY;

        _openSlidePrepared =
            true;

        SubDockChrome.RenderTransform =
            new TranslateTransform(
                startX,
                startY);

        SubDockChrome.CacheMode =
            null;
    }

    private void PlayOpenAnimation()
    {
        string animationStyle =
            _settings.AnimationStyle ?? "Fade";

        TimeSpan duration =
            TimeSpan.FromMilliseconds(
                180);

        SubDockChrome.BeginAnimation(
            OpacityProperty,
            null);

        SubDockChrome.Opacity =
            1;

        if (!_openSlidePrepared ||
            !string.Equals(
                animationStyle,
                "Slide",
                StringComparison.OrdinalIgnoreCase))
        {
            SubDockChrome.RenderTransform =
                Transform.Identity;
        }

        SubDockChrome.RenderTransformOrigin =
            new Point(
                0.5,
                0.5);

        SubDockChrome.CacheMode =
            null;

        switch (animationStyle)
        {
            case "Slide":
            {
                TranslateTransform transform;
                double startX;
                double startY;

                if (_openSlidePrepared &&
                    SubDockChrome.RenderTransform is
                        TranslateTransform preparedTransform)
                {
                    transform =
                        preparedTransform;

                    startX =
                        _preparedOpenSlideX;

                    startY =
                        _preparedOpenSlideY;

                    _openSlidePrepared =
                        false;
                }
                else
                {
                    PrepareOpenSlideStartState();

                    transform =
                        (TranslateTransform)SubDockChrome.RenderTransform;

                    startX =
                        _preparedOpenSlideX;

                    startY =
                        _preparedOpenSlideY;

                    _openSlidePrepared =
                        false;
                }

                DoubleAnimation animation =
                    new(
                        startX != 0
                            ? startX
                            : startY,
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
                        SubDockChrome.RenderTransform =
                            Transform.Identity;
                    };

                if (startX != 0)
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

                SubDockChrome.RenderTransform =
                    transform;

                DoubleAnimation scaleXAnimation =
                    new(
                        0.92,
                        1.0,
                        new Duration(
                            duration))
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
                        new Duration(
                            duration))
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
                        SubDockChrome.RenderTransform =
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
                SubDockChrome.Opacity =
                    0;

                DoubleAnimation animation =
                    new(
                        0,
                        1,
                        new Duration(
                            duration))
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
                        SubDockChrome.BeginAnimation(
                            OpacityProperty,
                            null);

                        SubDockChrome.Opacity =
                            1;
                    };

                SubDockChrome.BeginAnimation(
                    OpacityProperty,
                    animation);

                break;
            }
        }
    }

    private void PlayCloseAnimation()
    {
        string animationStyle =
            _settings.AnimationStyle ?? "Fade";

        TimeSpan duration =
            TimeSpan.FromMilliseconds(
                150);

        SubDockChrome.BeginAnimation(
            OpacityProperty,
            null);

        SubDockChrome.Opacity =
            1;

        SubDockChrome.RenderTransform =
            Transform.Identity;

        SubDockChrome.RenderTransformOrigin =
            new Point(
                0.5,
                0.5);

        SubDockChrome.CacheMode =
            null;

        void CompleteClose()
        {
            SubDockChrome.BeginAnimation(
                OpacityProperty,
                null);

            SubDockChrome.Opacity =
                1;

            SubDockChrome.RenderTransform =
                Transform.Identity;

            SubDockChrome.CacheMode =
                null;

            _closeAnimationRunning =
                false;

            Close();
        }

        switch (animationStyle)
        {
            case "Slide":
            {
                TranslateTransform transform =
                    new();

                SubDockChrome.RenderTransform =
                    transform;

                bool opensBelowAnchor =
                    _hasPositionAnchor &&
                    Top >=
                    _lastAnchorScreenRect.Bottom;

                bool opensAboveAnchor =
                    _hasPositionAnchor &&
                    Top + Height <=
                    _lastAnchorScreenRect.Top;

                bool opensRightOfAnchor =
                    _hasPositionAnchor &&
                    Left >=
                    _lastAnchorScreenRect.Right;

                bool opensLeftOfAnchor =
                    _hasPositionAnchor &&
                    Left + Width <=
                    _lastAnchorScreenRect.Left;

                double horizontalTravel =
                    _hasPositionAnchor
                        ? Math.Max(
                            24,
                            _lastAnchorScreenRect.Width)
                        : 24;

                double verticalTravel =
                    _hasPositionAnchor
                        ? Math.Max(
                            24,
                            _lastAnchorScreenRect.Height)
                        : 24;

                double targetX =
                    0;

                double targetY =
                    0;

                if (opensBelowAnchor)
                {
                    targetY =
                        -verticalTravel;
                }
                else if (opensAboveAnchor)
                {
                    targetY =
                        verticalTravel;
                }
                else if (opensRightOfAnchor)
                {
                    targetX =
                        -horizontalTravel;
                }
                else if (opensLeftOfAnchor)
                {
                    targetX =
                        horizontalTravel;
                }
                else
                {
                    switch (_rootEdge)
                    {
                        case DockEdge.Left:
                            targetX =
                                -horizontalTravel;
                            break;

                        case DockEdge.Right:
                            targetX =
                                horizontalTravel;
                            break;

                        case DockEdge.Top:
                            targetY =
                                -verticalTravel;
                            break;

                        default:
                            targetY =
                                verticalTravel;
                            break;
                    }
                }

                DoubleAnimation animation =
                    new(
                        0,
                        targetX != 0
                            ? targetX
                            : targetY,
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
                        CompleteClose();
                    };

                if (targetX != 0)
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

                SubDockChrome.RenderTransform =
                    transform;

                DoubleAnimation scaleXAnimation =
                    new(
                        1.0,
                        0.92,
                        new Duration(
                            duration))
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
                        new Duration(
                            duration))
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
                        CompleteClose();
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
                DoubleAnimation animation =
                    new(
                        1,
                        0,
                        new Duration(
                            duration))
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
                        CompleteClose();
                    };

                SubDockChrome.BeginAnimation(
                    OpacityProperty,
                    animation);

                break;
            }
        }
    }

    private void ApplyItemHoverEffect(
        FrameworkElement element)
    {
        if (element is not Border border)
        {
            return;
        }

        ResetItemHoverEffect(
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

                if (Resources["SubDockTextBrush"] is SolidColorBrush glowBrush)
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
                        "SubDockItemHighlight");

                if (highlight is not null)
                {
                    highlight.Background =
                        Resources["SubDockItemBackgroundBrush"] as System.Windows.Media.Brush ??
                        Brushes.Transparent;

                    highlight.BorderBrush =
                        Resources["SubDockItemBorderBrush"] as System.Windows.Media.Brush ??
                        Brushes.Transparent;
                }

                break;
            }
        }
    }

    private static void ResetItemHoverEffect(
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
                "SubDockItemHighlight");

        if (highlight is not null)
        {
            highlight.Background =
                Brushes.Transparent;

            highlight.BorderBrush =
                Brushes.Transparent;
        }
    }

    private void RefreshItemHoverEffects()
    {
        for (int index = 0;
             index < SubDockItemsControl.Items.Count;
             index++)
        {
            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromIndex(index)
                is not DependencyObject container)
            {
                continue;
            }

            Border? border =
                FindVisualChild<Border>(
                    container,
                    "SubDockItemBorder");

            if (border is null)
            {
                continue;
            }

            ResetItemHoverEffect(
                border);

            if (border.IsMouseOver)
            {
                ApplyItemHoverEffect(
                    border);
            }
        }
    }

    private void SubDockItem_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        LogSubDockState(
            "Item MouseEnter");

        _closeTimer.Stop();

        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        ApplyToolTipSettingToVisualTree(
            element,
            _settings.ShowSubDockTooltips);

        if (!item.DisableDefaultHoverEffect)
        {
            ApplyItemHoverEffect(
                element);
        }

        if (!item.IsSubmenu ||
            _settings.SubdockOpenOnClickOnly)
        {
            return;
        }

        OpenChildSubmenu(
            item,
            element);
    }

    private void SubDockItem_MouseLeave(
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
            ResetItemHoverEffect(
                element);
        }
    }

    private void SubDockItem_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            return;
        }

        _mouseDownItem = item;
        _mouseDownPoint = e.GetPosition(this);
        _dragPointerOffset =
            e.GetPosition(
                element);

        _suppressClick = false;
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

    private void SubDockItem_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not FrameworkElement draggedElement)
        {
            return;
        }

        if (_mouseDownItem is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_mouseDownItem.IsWidget &&
            _mouseDownItem.IsWidgetDragDropLocked)
        {
            return;
        }

        Point current = e.GetPosition(this);

        if (Math.Abs(current.X - _mouseDownPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _mouseDownPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DockItem draggedItem = _mouseDownItem;
        _mouseDownItem = null;
        _suppressClick = true;

        DataObject dragData = new();
        dragData.SetData(InternalDragFormat, draggedItem);

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

        _closeTimer.Stop();

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);

        _lastInternalDragIndex =
            Items.IndexOf(
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
                Items.Contains(
                    draggedItem))
            {
                Items.Remove(
                    draggedItem);

                _dockItemStore.DeleteManagedItem(
                    draggedItem.Path);

                _save();
                RefreshItemsLayout();

                DebugLog.Write(
                    "SubDockDrop",
                    $"Item moved out of subdock; Name={draggedItem.DisplayName}; Path={draggedItem.Path}");
            }
        }
        finally
        {
            SetInternalDragItemVisibility(
                draggedItem,
                visible: true);

            ResetInternalDragItemTransforms();

            HideInternalDragGhost();

            RemoveExternalDropPlaceholder();

            _childWindow?.ClearInternalDragPreview();

            _activeInternalDragItem = null;
            _lastInternalDragIndex = -1;

            InteractionStateChanged?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    private void ShowInternalDragGhost(
        DockItem draggedItem)
    {
        SubDockDragGhostImage.Source =
            draggedItem.Icon;

        SubDockDragGhostLabel.Text =
            draggedItem.DisplayName;

        SubDockDragGhostLabel.Visibility =
            _settings.ShowItemLabels
                ? Visibility.Visible
                : Visibility.Collapsed;

        SubDockDragGhost.Visibility =
            Visibility.Visible;
    }

    private void UpdateInternalDragVisual(
        Point pointerPosition)
    {
        if (SubDockDragGhost.Visibility !=
            Visibility.Visible)
        {
            return;
        }

        Point pointerInGrid =
            SubDockItemsControl.TranslatePoint(
                pointerPosition,
                SubDockContentGrid);

        double targetX =
            pointerInGrid.X -
            _dragPointerOffset.X;

        double targetY =
            pointerInGrid.Y -
            _dragPointerOffset.Y;

        SubDockDragGhost.RenderTransform =
            new TranslateTransform(
                targetX,
                targetY);
    }

    private void HideInternalDragGhost()
    {
        SubDockDragGhost.Visibility =
            Visibility.Collapsed;

        SubDockDragGhost.RenderTransform =
            Transform.Identity;

        SubDockDragGhostImage.Source = null;
        SubDockDragGhostLabel.Text = string.Empty;
    }

    private void UpdateInternalDragPosition(
        DockItem draggedItem,
        Point pointerPosition)
    {
        int currentIndex =
            Items.IndexOf(
                draggedItem);

        if (currentIndex < 0 ||
            Items.Count <= 1)
        {
            return;
        }

        double axisLength =
            SubDockItemsControl.ActualWidth;

        if (axisLength <= 0)
        {
            return;
        }

        double slotLength =
            axisLength /
            Items.Count;

        if (slotLength <= 0)
        {
            return;
        }

        int targetIndex =
            Math.Clamp(
                (int)Math.Floor(
                    pointerPosition.X /
                    slotLength),
                0,
                Items.Count - 1);

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
            pointerPosition.X <
            targetSlotCenter + hysteresis)
        {
            return;
        }

        if (targetIndex < currentIndex &&
            pointerPosition.X >
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
             index < Items.Count;
             index++)
        {
            DockItem item =
                Items[index];

            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    item) is not FrameworkElement container)
            {
                continue;
            }

            oldPositions[item] =
                container.TranslatePoint(
                    new Point(0, 0),
                    SubDockItemsControl);
        }

        ResetInternalDragItemTransforms();

        Items.Move(
            currentIndex,
            targetIndex);

        SubDockItemsControl.UpdateLayout();

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
             index < Items.Count;
             index++)
        {
            DockItem item =
                Items[index];

            if (!oldPositions.TryGetValue(
                    item,
                    out Point oldPosition) ||
                SubDockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    item) is not FrameworkElement container)
            {
                continue;
            }

            Point newPosition =
                container.TranslatePoint(
                    new Point(0, 0),
                    SubDockItemsControl);

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
        if (SubDockItemsControl.ItemContainerGenerator.ContainerFromItem(
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
             index < SubDockItemsControl.Items.Count;
             index++)
        {
            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromIndex(
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

    private void SubDockItem_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not DockItem item)
        {
            _mouseDownItem = null;
            return;
        }

        DockItem? pressedItem = _mouseDownItem;
        _mouseDownItem = null;

        if (_suppressClick)
        {
            _suppressClick = false;
            e.Handled = true;
            return;
        }

        if (!ReferenceEquals(pressedItem, item))
        {
            return;
        }

        if (item.IsSubmenu)
        {
            OpenChildSubmenu(item, element);
        }
        else if (!item.IsWidget)
        {
            LaunchDockItem(item);
        }

        e.Handled = true;
    }

    private void OpenChildSubmenu(
        DockItem item,
        FrameworkElement anchor)
    {
        DebugLog.Write(
            "SubDock",
            $"Child submenu open requested; Parent={_submenu.DisplayName}; Child={item.DisplayName}; ChildCount={item.Children.Count}");

        _closeTimer.Stop();

        if (_childWindow?.IsVisible == true &&
            ReferenceEquals(
                _childWindow.Tag,
                item))
        {
            return;
        }

        _childWindow?.Close();

        Rect anchorRect =
            GetScreenRect(
                anchor);

        DpiScale anchorDpi =
            VisualTreeHelper.GetDpi(
                anchor);

        _childWindow =
            new SubDockWindow(
                item,
                _settings,
                _dockItemStore,
                _save,
                _moveItemToSubmenu,
                _canMoveItemToSubmenu,
                _rootEdge)
            {
                Tag = item
            };

        _childWindow.InteractionStateChanged +=
            ChildWindow_InteractionStateChanged;

        _childWindow.ExternalDragEnded +=
            ChildWindow_ExternalDragEnded;

        _childWindow.Closed +=
            (_, _) =>
            {
                if (_childWindow is not null)
                {
                    _childWindow.InteractionStateChanged -=
                        ChildWindow_InteractionStateChanged;

                    _childWindow.ExternalDragEnded -=
                        ChildWindow_ExternalDragEnded;
                }

                _childWindow = null;

                InteractionStateChanged?.Invoke(
                    this,
                    EventArgs.Empty);

                _closeTimer.Stop();
            };

        _childWindow.PositionNextTo(
            anchorRect,
            anchorDpi);

        _childWindow.Show();

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void ChildWindow_InteractionStateChanged(
        object? sender,
        EventArgs e)
    {
        _closeTimer.Stop();

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void ChildWindow_ExternalDragEnded(
        object? sender,
        EventArgs e)
    {
        ExternalDragEnded?.Invoke(
            this,
            EventArgs.Empty);
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

        Items.Add(
            new DockItem
            {
                IsSubmenu = true,
                DisplayName = dialog.SubmenuName,
                Icon = SubmenuIcon.Create(_settings)
            });

        _save();

        if (_hasPositionAnchor)
        {
            PositionNextTo(
                _lastAnchorScreenRect,
                _lastAnchorDpi);
        }
        else
        {
            ApplyInitialSize();
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();
            },
            DispatcherPriority.Loaded);
    }

    private void RenameItem_Click(
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

        _save();
    }

    private void ChangeItemIcon_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.IsRuntimeOnly ||
            item.IsWidget)
        {
            return;
        }

        Microsoft.Win32.OpenFileDialog dialog =
            new()
            {
                Title =
                    "Change icon",
                Filter =
                    "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp|GIF|*.gif|Icon|*.ico"
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

            _save();
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

    private void UseDefaultItemIcon_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item ||
            item.IsRuntimeOnly ||
            item.IsWidget)
        {
            return;
        }

        item.SubmenuIconRepositoryPath =
            string.Empty;

        item.Icon =
            item.IsSubmenu
                ? SubmenuIcon.Create(
                    _settings)
                : ShellIcon.GetIcon(
                    item.Path,
                    _settings.ShowFilePreviews,
                    _settings.ShortcutOverlayMode);

        _save();
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

        _save();

        DebugLog.Write(
            "Widget",
            $"Drag/drop lock changed in subdock; Item={item.DisplayName}; Id={item.Id}; Locked={item.IsWidgetDragDropLocked}");
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

        item.WidgetInstance.OpenSettings(
            this);

        item.NotifyWidgetStateChanged();
        RefreshItemHoverEffects();

        DebugLog.Write(
            "Widget",
            $"Settings closed; Item={item.DisplayName}; Id={item.Id}; DisableDefaultHoverEffect={item.DisableDefaultHoverEffect}");
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

        DebugLog.Write(
            "Widget",
            $"Timer closed; Item={item.DisplayName}; Id={item.Id}");
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

        DebugLog.Write(
            "Widget",
            $"Stopwatch closed; Item={item.DisplayName}; Id={item.Id}");
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

        DebugLog.Write(
            "Widget",
            $"Calendar closed; Item={item.DisplayName}; Id={item.Id}");
    }

    private void RemoveItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not DockItem item)
        {
            return;
        }

        DebugLog.Write(
            "SubDock",
            $"Remove item; Submenu={_submenu.DisplayName}; Item={item.DisplayName}; IsSubmenu={item.IsSubmenu}");

        Items.Remove(item);
        DeleteManagedContent(item);
        _save();

        RefreshItemsLayout();

        _openContextMenuCount = 0;

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);

        Dispatcher.BeginInvoke(
            () =>
            {
                _closeTimer.Stop();

                InteractionStateChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            },
            DispatcherPriority.ContextIdle);
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

    private void SubDockWindow_DragEnter(
        object sender,
        DragEventArgs e)
    {
        bool externalDrag =
            !e.Data.GetDataPresent(
                InternalDragFormat);

        SetExternalDragActive(
            externalDrag);

        if (!externalDrag &&
            e.Data.GetData(
                InternalDragFormat) is DockItem draggedItem)
        {
            ShowInternalDragGhost(
                draggedItem);

            if (!Items.Contains(
                    draggedItem))
            {
                UpdateInternalCrossDockPlaceholder(
                    e.GetPosition(
                        SubDockItemsControl),
                    draggedItem);
            }
        }

        if (externalDrag &&
            e.Data.GetDataPresent(
                DataFormats.FileDrop) &&
            e.Data.GetData(
                DataFormats.FileDrop) is string[] paths)
        {
            UpdateExternalDropPlaceholder(
                e.GetPosition(
                    SubDockItemsControl),
                paths);
        }

        LogSubDockState(
            "DragEnter");

        _closeTimer.Stop();
        SetDropEffect(e);
    }

    private void SubDockWindow_DragOver(
        object sender,
        DragEventArgs e)
    {
        bool externalDrag =
            e.Data.GetDataPresent(
                DataFormats.FileDrop) &&
            !e.Data.GetDataPresent(
                InternalDragFormat);

        SetExternalDragActive(
            externalDrag);

        if (e.Data.GetDataPresent(
                InternalDragFormat) &&
            e.Data.GetData(
                InternalDragFormat) is DockItem draggedItem)
        {
            Point pointerPosition =
                e.GetPosition(
                    SubDockItemsControl);

            if (SubDockDragGhost.Visibility !=
                Visibility.Visible)
            {
                ShowInternalDragGhost(
                    draggedItem);
            }

            UpdateInternalDragVisual(
                pointerPosition);

            if (Items.Contains(
                    draggedItem))
            {
                UpdateInternalDragPosition(
                    draggedItem,
                    pointerPosition);
            }
            else
            {
                UpdateInternalCrossDockPlaceholder(
                    pointerPosition,
                    draggedItem);
            }
        }
        else if (externalDrag &&
                 e.Data.GetData(
                     DataFormats.FileDrop) is string[] paths)
        {
            Point pointerPosition =
                e.GetPosition(
                    SubDockItemsControl);

            FrameworkElement? submenuElement =
                FindSubmenuElementAtPosition(
                    pointerPosition);

            if (submenuElement?.DataContext is DockItem submenuItem &&
                !ReferenceEquals(
                    submenuItem,
                    _externalDropPlaceholder))
            {
                SetExternalDropSubmenuHighlight(
                    submenuElement);

                OpenChildSubmenu(
                    submenuItem,
                    submenuElement);
            }
            else
            {
                ClearExternalDropSubmenuHighlight();
            }

            UpdateExternalDropPlaceholder(
                pointerPosition,
                paths);
        }

        SetDropEffect(e);
    }
    private void SubDockWindow_DragLeave(
        object sender,
        DragEventArgs e)
    {
        Point pointerPosition =
            e.GetPosition(
                this);

        bool pointerStillInside =
            pointerPosition.X >= 0 &&
            pointerPosition.Y >= 0 &&
            pointerPosition.X <= ActualWidth &&
            pointerPosition.Y <= ActualHeight;

        if (pointerStillInside)
        {
            return;
        }

        if (e.Data.GetDataPresent(
                InternalDragFormat))
        {
            if (e.Data.GetData(
                    InternalDragFormat) is DockItem draggedItem &&
                !Items.Contains(
                    draggedItem))
            {
                HideInternalDragGhost();
            }

            RemoveExternalDropPlaceholder(
                refreshLayout: false);

            ClearExternalDropSubmenuHighlight();
            return;
        }

        RemoveExternalDropPlaceholder();
        ClearExternalDropSubmenuHighlight();

        Dispatcher.BeginInvoke(
            () =>
            {
                if (_childWindow?.IsPointerOverDockChain == true)
                {
                    return;
                }

                SetExternalDragActive(
                    false);
            },
            DispatcherPriority.Input);
    }

    private FrameworkElement? FindSubmenuElementAtPosition(
        Point pointerPosition)
    {
        HitTestResult? hit =
            VisualTreeHelper.HitTest(
                SubDockItemsControl,
                pointerPosition);

        DependencyObject? current =
            hit?.VisualHit;

        while (current is not null &&
               !ReferenceEquals(
                   current,
                   SubDockItemsControl))
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

    private void SubDockWindow_Drop(
        object sender,
        DragEventArgs e)
    {
        LogSubDockState(
            "Drop");

        try
        {
            if (e.Data.GetDataPresent(InternalDragFormat) &&
                e.Data.GetData(InternalDragFormat) is DockItem draggedItem)
            {
                if (Items.Contains(
                        draggedItem))
                {
                    _save();

                    e.Effects =
                        DragDropEffects.Copy;

                    e.Handled = true;
                    return;
                }

                if (_canMoveItemToSubmenu(draggedItem, _submenu))
                {
                    int insertionIndex =
                        _externalDropPlaceholder is null
                            ? Items.Count
                            : Math.Max(
                                0,
                                Items.IndexOf(
                                    _externalDropPlaceholder));

                    RemoveExternalDropPlaceholder(
                        refreshLayout: false);

                    _moveItemToSubmenu(draggedItem, _submenu);

                    int currentIndex =
                        Items.IndexOf(
                            draggedItem);

                    if (currentIndex >= 0)
                    {
                        insertionIndex =
                            Math.Clamp(
                                insertionIndex,
                                0,
                                Items.Count - 1);

                        if (currentIndex !=
                            insertionIndex)
                        {
                            Items.Move(
                                currentIndex,
                                insertionIndex);
                        }
                    }

                    EnsureSubDockRowCapacity();
                    _save();
                    RefreshItemsLayout();

                    DebugLog.Write(
                        "SubDockDrop",
                        $"Internal item moved; Name={_submenu.DisplayName}; Item={draggedItem.DisplayName}; Index={Items.IndexOf(draggedItem)}; Items={Items.Count}; Columns={_settings.SubDockMaxColumns}; Rows={_settings.SubDockMaxRows}");

                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }

                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                bool moveSource =
                    (e.KeyStates &
                     DragDropKeyStates.ShiftKey) != 0;

                int insertionIndex =
                    _externalDropPlaceholder is null
                        ? Items.Count
                        : Math.Max(
                            0,
                            Items.IndexOf(
                                _externalDropPlaceholder));

                RemoveExternalDropPlaceholder(
                    refreshLayout: false);

                AddDroppedItems(
                    paths,
                    moveSource,
                    insertionIndex);

                e.Effects =
                    moveSource
                        ? DragDropEffects.Move
                        : DragDropEffects.Copy;

                e.Handled = true;
            }
        }
        finally
        {
            RemoveExternalDropPlaceholder();
            ClearExternalDropSubmenuHighlight();

            SetExternalDragActive(
                false);
        }
    }
    private void UpdateInternalCrossDockPlaceholder(
        Point pointerPosition,
        DockItem draggedItem)
    {
        if (_externalDropPlaceholder is null)
        {
            _externalDropPlaceholder =
                new DockItem
                {
                    DisplayName =
                        draggedItem.DisplayName,
                    IsRuntimeOnly = true,
                    Icon =
                        draggedItem.Icon
                };

            Items.Add(
                _externalDropPlaceholder);

            RefreshItemsLayout();

            SetInternalDragItemVisibility(
                _externalDropPlaceholder,
                visible: false);

            DebugLog.Write(
                "SubDockDrag",
                $"Internal preview added; Name={_submenu.DisplayName}; Item={draggedItem.DisplayName}; Items={Items.Count}");
        }

        int currentPlaceholderIndex =
            Items.IndexOf(
                _externalDropPlaceholder);

        if (currentPlaceholderIndex < 0)
        {
            return;
        }

        int targetIndex =
            GetExternalDropTargetIndex(
                pointerPosition);

        if (currentPlaceholderIndex <
            targetIndex)
        {
            targetIndex--;
        }

        targetIndex =
            Math.Clamp(
                targetIndex,
                0,
                Items.Count - 1);

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
            visible: false);

        UpdateItemLabelVisibility();

        DebugLog.Write(
            "SubDockDrag",
            $"Internal preview moved; Name={_submenu.DisplayName}; Item={draggedItem.DisplayName}; From={currentPlaceholderIndex}; To={targetIndex}");
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

            string displayName =
                string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    previewPath))
            {
                string trimmedPath =
                    previewPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

                displayName =
                    Directory.Exists(
                        previewPath)
                        ? Path.GetFileName(
                            trimmedPath)
                        : Path.GetFileNameWithoutExtension(
                            previewPath);

                if (string.IsNullOrWhiteSpace(
                        displayName))
                {
                    displayName =
                        Path.GetFileName(
                            previewPath);
                }
            }

            _externalDropPlaceholder =
                new DockItem
                {
                    DisplayName =
                        string.IsNullOrWhiteSpace(
                            displayName)
                            ? "Drop position"
                            : displayName,
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

            Items.Add(
                _externalDropPlaceholder);

            RefreshItemsLayout();

            DebugLog.Write(
                "SubDockDrag",
                $"External preview added; Name={_submenu.DisplayName}; Preview={_externalDropPlaceholder.DisplayName}; Items={Items.Count}");
        }

        int currentPlaceholderIndex =
            Items.IndexOf(
                _externalDropPlaceholder);

        if (currentPlaceholderIndex < 0)
        {
            return;
        }

        int targetIndex =
            GetExternalDropTargetIndex(
                pointerPosition);

        if (currentPlaceholderIndex <
            targetIndex)
        {
            targetIndex--;
        }

        targetIndex =
            Math.Clamp(
                targetIndex,
                0,
                Items.Count - 1);

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

        UpdateItemLabelVisibility();

        DebugLog.Write(
            "SubDockDrag",
            $"External preview moved; Name={_submenu.DisplayName}; From={currentPlaceholderIndex}; To={targetIndex}");
    }

    private int GetExternalDropTargetIndex(
        Point pointerPosition)
    {
        List<(
            DockItem Item,
            FrameworkElement Container,
            Point Position,
            double CenterY)> candidates =
            [];

        foreach (DockItem item in Items)
        {
            if (ReferenceEquals(
                    item,
                    _externalDropPlaceholder))
            {
                continue;
            }

            if (SubDockItemsControl.ItemContainerGenerator.ContainerFromItem(
                    item) is not FrameworkElement container ||
                container.ActualWidth <= 0 ||
                container.ActualHeight <= 0)
            {
                continue;
            }

            Point position =
                container.TranslatePoint(
                    new Point(0, 0),
                    SubDockItemsControl);

            candidates.Add(
                (
                    item,
                    container,
                    position,
                    position.Y +
                    (container.ActualHeight / 2)
                ));
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        double nearestCenterY =
            candidates
                .OrderBy(
                    candidate =>
                        Math.Abs(
                            candidate.CenterY -
                            pointerPosition.Y))
                .First()
                .CenterY;

        List<(
            DockItem Item,
            FrameworkElement Container,
            Point Position,
            double CenterY)> rowCandidates =
            candidates
                .Where(
                    candidate =>
                        Math.Abs(
                            candidate.CenterY -
                            nearestCenterY) <
                        8)
                .OrderBy(
                    candidate =>
                        candidate.Position.X)
                .ToList();

        foreach (var candidate in rowCandidates)
        {
            double centerX =
                candidate.Position.X +
                (candidate.Container.ActualWidth / 2);

            if (pointerPosition.X <
                centerX)
            {
                return Math.Max(
                    0,
                    Items.IndexOf(
                        candidate.Item));
            }
        }

        DockItem lastItem =
            rowCandidates[^1].Item;

        return Math.Min(
            Items.Count,
            Items.IndexOf(
                lastItem) +
            1);
    }

    private void RemoveExternalDropPlaceholder(
        bool refreshLayout = true)
    {
        if (_externalDropPlaceholder is null)
        {
            return;
        }

        Items.Remove(
            _externalDropPlaceholder);

        _externalDropPlaceholder =
            null;

        if (refreshLayout)
        {
            RefreshItemsLayout();
        }

        DebugLog.Write(
            "SubDockDrag",
            $"External preview removed; Name={_submenu.DisplayName}; Items={Items.Count}; RefreshLayout={refreshLayout}");
    }

    private void SetExternalDropSubmenuHighlight(
        FrameworkElement element)
    {
        Border? border =
            FindAncestorBorder(
                element,
                "SubDockItemBorder");

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
            Resources["SubDockTextBrush"] as System.Windows.Media.Brush ??
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

    private void AddDroppedItems(
        IEnumerable<string> paths,
        bool moveSource,
        int insertionIndex)
    {
        int targetIndex =
            Math.Clamp(
                insertionIndex,
                0,
                Items.Count);

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

                Items.Insert(
                    targetIndex,
                    CreateFileDockItem(
                        managedPath));

                targetIndex++;
            }
            catch (Exception ex)
            {
                DebugLog.WriteException(
                    "SubDock.Import",
                    ex);

                MessageBox.Show(
                    $"{App.Language["Message.ImportFailed"]}\n\n{path}\n\n{ex.Message}",
                    App.Language["App.Name"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        EnsureSubDockRowCapacity();
        _save();

        UpdateItemsPanelWidth();

        if (_hasPositionAnchor)
        {
            PositionNextTo(
                _lastAnchorScreenRect,
                _lastAnchorDpi);
        }
        else
        {
            ApplyInitialSize();
        }

        InvalidateMeasure();
        InvalidateArrange();
        UpdateLayout();

        LogSubDockState(
            "Resized immediately after drop");

        Dispatcher.BeginInvoke(
            () =>
            {
                UpdateItemLabelVisibility();

                DebugLog.Write(
                    "Labels",
                    $"Submenu labels refreshed after drop; Name={_submenu.DisplayName}; Items={Items.Count}");
            },
            DispatcherPriority.Loaded);
    }

    private void EnsureSubDockRowCapacity()
    {
        int maxColumns =
            Math.Clamp(
                _settings.SubDockMaxColumns,
                1,
                50);

        int requiredRows =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    Items.Count /
                    (double)maxColumns));

        int currentRows =
            Math.Clamp(
                _settings.SubDockMaxRows,
                1,
                50);

        if (requiredRows <= currentRows)
        {
            return;
        }

        _settings.SubDockMaxRows =
            Math.Min(
                50,
                requiredRows);

        ShowLayoutAdjustmentNotice(
            string.Format(
                App.Language["Message.LayoutRowsAdjusted"],
                _settings.SubDockMaxRows,
                Items.Count));

        DebugLog.Write(
            "ItemLayout",
            $"SubDock rows auto-adjusted; Name={_submenu.DisplayName}; Items={Items.Count}; Columns={maxColumns}; Rows={_settings.SubDockMaxRows}");
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

    private DockItem CreateFileDockItem(
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
                    _settings.ShortcutOverlayMode)
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
                "SubDock.LaunchAsAdministrator",
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
                    : Path.GetDirectoryName(item.Path);

            ProcessStartInfo startInfo =
                new()
                {
                    FileName = item.Path,
                    UseShellExecute = true
                };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException(
                "SubDock.Launch",
                ex);

            MessageBox.Show(
                $"{App.Language["Message.LaunchFailed"]}\n\n{item.Path}\n\n{ex.Message}",
                App.Language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static Rect GetScreenRect(
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

        return new Rect(
            topLeft.X,
            topLeft.Y,
            element.ActualWidth,
            element.ActualHeight);
    }

    private void SetExternalDragActive(
        bool active)
    {
        if (_isExternalDragActive == active)
        {
            return;
        }

        _isExternalDragActive = active;

        DebugLog.Write(
            "SubDockDrag",
            $"ExternalDrag={active}; Name={_submenu.DisplayName}");

        _closeTimer.Stop();

        InteractionStateChanged?.Invoke(
            this,
            EventArgs.Empty);

        if (!active)
        {
            RemoveExternalDropPlaceholder();
            ClearExternalDropSubmenuHighlight();

            ExternalDragEnded?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    private void LogSubDockState(
        string eventName)
    {
        DebugLog.Write(
            "SubDockState",
            $"{eventName}; Name={_submenu.DisplayName}; MouseOver={IsMouseOver}; ExternalDrag={_isExternalDragActive}; CloseTimer={_closeTimer.IsEnabled}; ContextMenus={_openContextMenuCount}; ChildOpen={HasOpenChild}; ChainActive={IsPointerOverDockChain}; Left={Left:0.0}; Top={Top:0.0}; Width={Width:0.0}; Height={Height:0.0}; Items={Items.Count}");
    }

    private static void SetDropEffect(
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(InternalDragFormat))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects =
                (e.KeyStates &
                 DragDropKeyStates.ShiftKey) != 0
                    ? DragDropEffects.Move
                    : DragDropEffects.Copy;

            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }
}
