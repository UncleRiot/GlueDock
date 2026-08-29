using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    private DockItem? _mouseDownItem;
    private Point _mouseDownPoint;
    private Point _dragPointerOffset;
    private bool _suppressClick;
    private DockItem? _activeInternalDragItem;
    private int _lastInternalDragIndex = -1;
    private SubDockWindow? _childWindow;
    private int _openContextMenuCount;
    private bool _isExternalDragActive;
    private Rect _lastAnchorScreenRect;
    private DpiScale _lastAnchorDpi;
    private bool _hasPositionAnchor;
    private bool _closeAnimationRunning;

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
            };
    }

    private void ApplyInitialSize()
    {
        const double minimumLength = 96;
        const double baseExpandedThickness = 88;
        const double itemSlotLength = 64;
        const double dockPaddingLength = 20;

        double itemSpacing =
            Math.Clamp(
                _settings.ItemSpacing,
                -8,
                42);

        double itemLength =
            58 +
            itemSpacing;

        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        double thicknessScale =
            Math.Clamp(
                _settings.DockThicknessScale,
                0.50,
                1.00);

        double expandedThickness =
            (itemSlotLength * scale) +
            ((baseExpandedThickness -
              itemSlotLength) *
             scale *
             thicknessScale);

        Width =
            Math.Max(
                minimumLength,
                (Items.Count *
                 itemLength *
                 scale) +
                dockPaddingLength +
                2);

        Height =
            expandedThickness;
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

        double itemSpacing =
            Math.Clamp(
                _settings.ItemSpacing,
                -8,
                42);

        double itemLength =
            58 +
            itemSpacing;

        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        DockTheme theme =
            DockThemeService.Load(
                _settings.ThemeName);

        double width =
            Math.Max(
                minimumLength,
                (Items.Count *
                 itemLength *
                 scale) +
                dockPaddingLength +
                2);

        double thicknessScale =
            Math.Clamp(
                _settings.DockThicknessScale,
                0.50,
                1.00);

        double height =
            (itemSlotLength * scale) +
            ((baseExpandedThickness -
              itemSlotLength) *
             scale *
             thicknessScale);

        Width = width;
        Height = height;

        int anchorPixelX =
            (int)Math.Round(
                anchorScreenRect.X *
                anchorDpi.DpiScaleX);

        int anchorPixelY =
            (int)Math.Round(
                anchorScreenRect.Y *
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

            top =
                _rootEdge == DockEdge.Top
                    ? anchorScreenRect.Bottom + 6
                    : anchorScreenRect.Top - height - 6;
        }
        else
        {
            top =
                anchorScreenRect.Top +
                (anchorScreenRect.Height / 2) -
                (height / 2);

            left =
                _rootEdge == DockEdge.Left
                    ? anchorScreenRect.Right + 6
                    : anchorScreenRect.Left - width - 6;
        }

        left =
            Math.Clamp(
                left,
                area.Left,
                Math.Max(
                    area.Left,
                    area.Right - width));

        top =
            Math.Clamp(
                top,
                area.Top,
                Math.Max(
                    area.Top,
                    area.Bottom - height));

        Left = left;
        Top = top;

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
        PlayOpenAnimation();
    }

    public void RefreshItemsLayout()
    {
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

    public void ApplyItemSpacingLive(
        Rect anchorScreenRect,
        DpiScale anchorDpi)
    {
        double itemSpacing =
            Math.Clamp(
                _settings.ItemSpacing,
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        Resources["SubDockItemWidth"] =
            itemWidth;

        Resources["SubDockItemMargin"] =
            new Thickness(
                0,
                3,
                0,
                3);

        DebugLog.Write(
            "ItemLayout",
            $"SubDock spacing-only update; Name={_submenu.DisplayName}; Spacing={_settings.ItemSpacing:0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}");

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
        _closeTimer.Interval =
            TimeSpan.FromMilliseconds(
                Math.Max(
                    0,
                    _settings.SubdockCollapseDelayMilliseconds));

        ApplyAppearance();
        RefreshItemsLayout();
        RefreshItemHoverEffects();
        _childWindow?.ApplySettingsLive();
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
            $"SubDock; Name={_submenu.DisplayName}; Theme={_settings.ThemeName}; Mica={isMica}; UserOpacity={_settings.Opacity:0.00}; EffectiveOpacity={effectiveOpacity:0.00}; UserBlur={_settings.BlurRadius:0.##}; EffectiveBlur={effectiveBlurRadius:0.##}");

        Resources["SubDockItemBackgroundBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBackgroundColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        Resources["SubDockItemBorderBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.ItemBorderColor,
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)));

        Resources["SubDockTextBrush"] =
            new SolidColorBrush(
                ParseThemeColor(
                    theme.TextColor,
                    Colors.White));

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
                _settings.ItemSpacing,
                -8,
                42);

        double itemWidth =
            58 +
            itemSpacing;

        Resources["SubDockItemWidth"] =
            itemWidth;

        DebugLog.Write(
            "ItemLayout",
            $"SubDock layout update; Name={_submenu.DisplayName}; Spacing={_settings.ItemSpacing:0.##}; EffectiveSpacing={itemSpacing:0.##}; ItemWidth={itemWidth:0.##}");

        Resources["SubDockItemMargin"] =
            new Thickness(
                0,
                3,
                0,
                3);

        double scale =
            Math.Clamp(
                _settings.DockScale,
                0.60,
                2.00);

        SubDockChrome.Padding =
            new Thickness(
                10 * scale);

        SubDockItemsControl.LayoutTransform =
            new ScaleTransform(
                scale,
                scale);
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

        SubDockGlassSurface.Background =
            surfaceBrush;

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

    private void ResetGlassSurface()
    {
        SubDockChrome.CornerRadius =
            new CornerRadius(
                20);

        SubDockGlassSurface.Background =
            Brushes.Transparent;

        SubDockGlassSurface.Effect =
            null;

        SubDockGlassSurface.Visibility =
            Visibility.Collapsed;

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
                                    $"SubDock; Name={_submenu.DisplayName}; Index={index}; Spacing={_settings.ItemSpacing:0.##}; ContainerX={itemPosition.X:0.##}; ContainerY={itemPosition.Y:0.##}; ContainerActualWidth={container.ActualWidth:0.##}; ContainerActualHeight={container.ActualHeight:0.##}; LabelX={labelPosition.X:0.##}; LabelY={labelPosition.Y:0.##}; LabelActualWidth={label.ActualWidth:0.##}; LabelActualHeight={label.ActualHeight:0.##}; NaturalTextWidth={formattedText.WidthIncludingTrailingWhitespace:0.##}; AvailableTextWidth={availableWidth:0.##}; TextAlignment={label.TextAlignment}");
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

        SubDockChrome.RenderTransform =
            Transform.Identity;

        SubDockChrome.RenderTransformOrigin =
            new Point(
                0.5,
                0.5);

        switch (animationStyle)
        {
            case "Slide":
            {
                TranslateTransform transform =
                    new();

                SubDockChrome.CacheMode =
                    new BitmapCache
                    {
                        SnapsToDevicePixels = true
                    };

                SubDockChrome.RenderTransform =
                    transform;

                double offset =
                    _rootEdge switch
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
                        SubDockChrome.RenderTransform =
                            Transform.Identity;

                        SubDockChrome.CacheMode =
                            null;
                    };

                if (_rootEdge is
                    DockEdge.Left or
                    DockEdge.Right)
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

                SubDockChrome.CacheMode =
                    new BitmapCache
                    {
                        SnapsToDevicePixels = true
                    };

                SubDockChrome.RenderTransform =
                    transform;

                double offset =
                    _rootEdge switch
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
                        CompleteClose();
                    };

                if (_rootEdge is
                    DockEdge.Left or
                    DockEdge.Right)
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

        ApplyItemHoverEffect(
            element);

        if (!item.IsSubmenu)
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
        if (sender is not FrameworkElement element)
        {
            return;
        }

        ResetItemHoverEffect(
            element);
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

    private void SubDockItem_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_mouseDownItem is null ||
            e.LeftButton != MouseButtonState.Pressed)
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
        else
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
            !item.IsSubmenu)
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
            !item.IsSubmenu)
        {
            return;
        }

        item.SubmenuIconRepositoryPath =
            string.Empty;

        item.Icon =
            SubmenuIcon.Create(
                _settings);

        _save();
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
        SetExternalDragActive(
            !e.Data.GetDataPresent(
                InternalDragFormat));

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

            UpdateInternalDragVisual(
                pointerPosition);

            if (Items.Contains(
                    draggedItem))
            {
                UpdateInternalDragPosition(
                    draggedItem,
                    pointerPosition);
            }
        }
        else if (externalDrag)
        {
            FrameworkElement? submenuElement =
                FindSubmenuElementAtPosition(
                    e.GetPosition(
                        SubDockItemsControl));

            if (submenuElement?.DataContext is DockItem submenuItem)
            {
                OpenChildSubmenu(
                    submenuItem,
                    submenuElement);
            }
        }

        SetDropEffect(e);
    }

    private void SubDockWindow_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                InternalDragFormat))
        {
            return;
        }

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
                    _moveItemToSubmenu(draggedItem, _submenu);
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }

                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                AddDroppedItems(paths);
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
        finally
        {
            SetExternalDragActive(
                false);
        }
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
                string managedPath = _dockItemStore.Import(path);

                Items.Add(CreateFileDockItem(managedPath));
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
                    _settings.UseSmallShortcutOverlay)
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
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }
}
