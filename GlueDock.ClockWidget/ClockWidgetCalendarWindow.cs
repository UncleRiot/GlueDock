using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetCalendarWindow : Window
{
    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo(
            "de-DE");

    private Brush WindowBackgroundBrush;
    private Brush PanelBackgroundBrush;
    private Brush BorderBrushValue;
    private Brush TextBrush;
    private Brush MutedTextBrush;
    private SolidColorBrush _eventBrush;
    private readonly Color _generalEventColor;
    private readonly ClockWidgetSettings _settings;
    private readonly Action _saveOverrideSettings;
    private readonly Action _openOverrideSettings;
    private GlueDockWidgetAppearance? _hostAppearance;
    private readonly List<ClockWidgetCalendarEventEntry> _events;
    private readonly Action _saveState;
    private readonly TextBlock _monthYearText;
    private readonly TextBlock _selectedDateText;
    private readonly Grid _daysGrid;
    private readonly StackPanel _eventsPanel;
    private readonly TextBox _eventTimeTextBox;
    private readonly TextBox _eventTitleTextBox;
    private readonly CheckBox _eventAllDayCheckBox;
    private readonly TextBox _eventDurationTextBox;
    private readonly Button _saveEventButton;
    private readonly Button _deleteEventButton;
    private readonly Border _windowBorder;
    private readonly Border _glassSurfaceBorder;
    private readonly Border _glassHighlightBorder;
    private readonly ClockWidgetNativeBackdropHost _nativeBackdropHost;
    private bool _initialBackdropReady;
    private double _pendingBlurRadius;
    private DateTime _displayMonth;
    private DateTime _selectedDate;
    private string? _selectedEventId;

    public ClockWidgetCalendarWindow(
        List<ClockWidgetCalendarEventEntry> events,
        Action saveState,
        Color eventColor,
        Brush windowBackgroundBrush,
        Brush panelBackgroundBrush,
        Brush borderBrush,
        Brush textBrush,
        ClockWidgetSettings settings,
        Action saveOverrideSettings,
        Action openOverrideSettings,
        string? selectedEventId = null)
    {
        _events =
            events;

        _saveState =
            saveState;

        WindowBackgroundBrush =
            windowBackgroundBrush;

        PanelBackgroundBrush =
            panelBackgroundBrush;

        BorderBrushValue =
            borderBrush;

        TextBrush =
            textBrush;

        MutedTextBrush =
            textBrush.Clone();

        MutedTextBrush.Opacity =
            0.7;

        _generalEventColor =
            eventColor;

        _eventBrush =
            new SolidColorBrush(
                eventColor);

        _settings =
            settings;

        _settings.CalendarOverrides ??=
            new ClockWidgetCalendarOverrideSettings();

        _saveOverrideSettings =
            saveOverrideSettings;

        _openOverrideSettings =
            openOverrideSettings;

        _selectedEventId =
            selectedEventId;

        _displayMonth =
            new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1);

        _selectedDate =
            DateTime.Today;

        if (!string.IsNullOrWhiteSpace(
                _selectedEventId))
        {
            ClockWidgetCalendarEventEntry? selectedEvent =
                _events.FirstOrDefault(
                    calendarEvent =>
                        calendarEvent.Id ==
                        _selectedEventId);

            if (selectedEvent is not null)
            {
                _selectedDate =
                    selectedEvent.StartAt.Date;

                _displayMonth =
                    new DateTime(
                        selectedEvent.StartAt.Year,
                        selectedEvent.StartAt.Month,
                        1);
            }
        }

        Title = "Clock widget calendar";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        Rect workArea =
            SystemParameters.WorkArea;

        Width =
            Math.Clamp(
                workArea.Width * 0.155,
                390,
                460);

        Height =
            Math.Clamp(
                workArea.Height * 0.52,
                540,
                610);

        _windowBorder =
            new()
            {
                CornerRadius =
                    new CornerRadius(
                        20),
                Background =
                    WindowBackgroundBrush,
                BorderBrush =
                    BorderBrushValue,
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        0)
            };

        _windowBorder.PreviewMouseLeftButtonDown +=
            WindowBorder_PreviewMouseLeftButtonDown;

        _windowBorder.ContextMenu =
            CreateCalendarContextMenu();

        _glassSurfaceBorder =
            new()
            {
                CornerRadius =
                    new CornerRadius(
                        20),
                Background =
                    Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

        _glassHighlightBorder =
            new()
            {
                CornerRadius =
                    new CornerRadius(
                        20),
                Background =
                    Brushes.Transparent,
                BorderBrush =
                    Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        0),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        14)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        Grid titleBar =
            CreateTitleBar();

        Grid monthNavigation =
            CreateMonthNavigation();

        Grid weekdays =
            CreateWeekdayHeader();

        _daysGrid =
            CreateDaysGrid();

        Border eventSection =
            CreateEventSection();

        Grid.SetRow(
            titleBar,
            0);

        Grid.SetRow(
            monthNavigation,
            1);

        Grid.SetRow(
            weekdays,
            2);

        Grid.SetRow(
            _daysGrid,
            3);

        Grid.SetRow(
            eventSection,
            4);

        root.Children.Add(
            titleBar);

        root.Children.Add(
            monthNavigation);

        root.Children.Add(
            weekdays);

        root.Children.Add(
            _daysGrid);

        root.Children.Add(
            eventSection);

        Grid windowChrome =
            new();

        windowChrome.Children.Add(
            _glassSurfaceBorder);

        windowChrome.Children.Add(
            _glassHighlightBorder);

        windowChrome.Children.Add(
            root);

        _windowBorder.Child =
            windowChrome;

        Content =
            _windowBorder;

        _nativeBackdropHost =
            new ClockWidgetNativeBackdropHost(
                this);

        _monthYearText =
            (TextBlock)monthNavigation.Tag;

        _selectedDateText =
            (TextBlock)eventSection.Tag;

        StackPanel eventContent =
            (StackPanel)eventSection.Child;

        _eventsPanel =
            (StackPanel)eventContent.Children[1];

        Grid editorGrid =
            (Grid)eventContent.Children[2];

        _saveEventButton =
            (Button)editorGrid.Children[2];

        _deleteEventButton =
            (Button)editorGrid.Children[1];

        _eventTitleTextBox =
            (TextBox)editorGrid.Children[3];

        Grid scheduleGrid =
            (Grid)eventContent.Children[3];

        _eventTimeTextBox =
            (TextBox)scheduleGrid.Children[1];

        _eventDurationTextBox =
            (TextBox)scheduleGrid.Children[3];

        _eventAllDayCheckBox =
            (CheckBox)scheduleGrid.Children[4];

        _eventAllDayCheckBox.Checked +=
            EventAllDayCheckBox_Changed;

        _eventAllDayCheckBox.Unchecked +=
            EventAllDayCheckBox_Changed;

        Loaded +=
            (_, _) =>
            {
                ClockWidgetLog.Write(
                    "Widget.Clock.CalendarWindow",
                    $"Opened; Left={Left:0.0}; Top={Top:0.0}; Width={ActualWidth:0.0}; Height={ActualHeight:0.0}");

                if (!IsVisible)
                {
                    return;
                }

                _initialBackdropReady = true;

                bool blurApplied =
                    _nativeBackdropHost.SetBlur(
                        _pendingBlurRadius);

                _nativeBackdropHost.Sync();

                ClockWidgetLog.Write(
                    "Widget.Clock.CalendarBackdrop",
                    $"Initial blur initialized on load; Blur={_pendingBlurRadius:0.##}; Applied={blurApplied}; Width={ActualWidth:0.0}; Height={ActualHeight:0.0}");
            };

        Closed +=
            (_, _) =>
                ClockWidgetLog.Write(
                    "Widget.Clock.CalendarWindow",
                    $"Closed; Left={Left:0.0}; Top={Top:0.0}");

        UpdateCalendarView();
        UpdateEventView();
    }

    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _hostAppearance =
            appearance;

        GlueDockWidgetAppearance effectiveAppearance =
            CreateEffectiveAppearance(
                appearance);

        SolidColorBrush oldEventBrush =
            _eventBrush;

        UpdateEventBrush();

        Brush oldWindowBackgroundBrush =
            WindowBackgroundBrush;

        Brush oldPanelBackgroundBrush =
            PanelBackgroundBrush;

        Brush oldBorderBrush =
            BorderBrushValue;

        Brush oldTextBrush =
            TextBrush;

        Brush oldMutedTextBrush =
            MutedTextBrush;

        WindowBackgroundBrush =
            effectiveAppearance.DockBackgroundBrush.Clone();

        PanelBackgroundBrush =
            effectiveAppearance.DockItemBackgroundBrush.Clone();

        BorderBrushValue =
            effectiveAppearance.DockItemBorderBrush.Clone();

        TextBrush =
            effectiveAppearance.DockTextBrush.Clone();

        MutedTextBrush =
            TextBrush.Clone();

        MutedTextBrush.Opacity =
            0.7;

        ReplaceBrushes(
            _windowBorder,
            oldWindowBackgroundBrush,
            oldPanelBackgroundBrush,
            oldBorderBrush,
            oldTextBrush,
            oldMutedTextBrush);

        ReplaceEventBrushes(
            _windowBorder,
            oldEventBrush);

        ApplyHostWindowMaterial(
            effectiveAppearance);

        UpdateCalendarView();
        UpdateEventView();

        _pendingBlurRadius =
            effectiveAppearance.BlurRadius;

        bool opacityApplied =
            _nativeBackdropHost.SetOpacity(
                effectiveAppearance.Opacity);

        bool blurApplied =
            _nativeBackdropHost.SetBlur(
                _pendingBlurRadius);

        _nativeBackdropHost.Sync();

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarTheme",
            $"Calendar updated; Theme={effectiveAppearance.ThemeName}; Opacity={effectiveAppearance.Opacity:0.00}; BackdropOpacityApplied={opacityApplied}; Blur={effectiveAppearance.BlurRadius:0.##}; BackdropReady={_initialBackdropReady}; BlurApplied={blurApplied}; GlassSurfaceEnabled={effectiveAppearance.GlassSurfaceEnabled}; GlassTopColor={effectiveAppearance.GlassTopColor}; GlassBottomColor={effectiveAppearance.GlassBottomColor}; GlassHighlightColor={effectiveAppearance.GlassHighlightColor}; GlassCornerRadius={effectiveAppearance.GlassCornerRadius:0.##}");
    }

    private void ApplyHostWindowMaterial(
        GlueDockWidgetAppearance appearance)
    {
        double cornerRadius =
            Math.Clamp(
                appearance.GlassCornerRadius,
                0,
                80);

        double borderThickness =
            _settings.CalendarOverrides.BorderOverrideEnabled
                ? Math.Clamp(
                    _settings.CalendarOverrides.BorderThickness,
                    0,
                    12)
                : appearance.DockBorderEnabled
                    ? 1
                    : 0;

        _windowBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        _glassSurfaceBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        _glassHighlightBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        if (appearance.DockBorderEnabled)
        {
            if (appearance.GlassSurfaceEnabled)
            {
                LinearGradientBrush dockBorderBrush =
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

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0xC8,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x78,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0.55));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x28,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        1));

                _windowBorder.BorderBrush =
                    dockBorderBrush;
            }
            else
            {
                _windowBorder.BorderBrush =
                    new SolidColorBrush(
                        Color.FromArgb(
                            0xFF,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B));
            }

            _windowBorder.BorderThickness =
                new Thickness(
                    borderThickness);
        }
        else
        {
            _windowBorder.BorderBrush =
                Brushes.Transparent;

            _windowBorder.BorderThickness =
                new Thickness(
                    0);
        }

        if (!appearance.GlassSurfaceEnabled)
        {
            _windowBorder.Background =
                WindowBackgroundBrush;

            _glassSurfaceBorder.Background =
                Brushes.Transparent;

            _glassSurfaceBorder.Visibility =
                Visibility.Collapsed;

            _glassHighlightBorder.Background =
                Brushes.Transparent;

            _glassHighlightBorder.BorderBrush =
                Brushes.Transparent;

            _glassHighlightBorder.BorderThickness =
                new Thickness(
                    0);

            _glassHighlightBorder.Visibility =
                Visibility.Collapsed;

            ClockWidgetLog.Write(
                "Widget.Clock.CalendarAeroRender",
                $"GlassSurfaceEnabled=False; Theme={appearance.ThemeName}; WindowBackground={WindowBackgroundBrush}; Opacity={appearance.Opacity:0.00}; Blur={appearance.BlurRadius:0.##}; DockBorderEnabled={appearance.DockBorderEnabled}; DockBorderColor={appearance.DockBorderColor}; BorderThickness={_windowBorder.BorderThickness}");

            return;
        }

        _windowBorder.Background =
            Brushes.Transparent;

        double gradientReferenceHeight =
            Math.Max(
                1,
                appearance.GlassGradientReferenceHeight);

        LinearGradientBrush surfaceBrush =
            new(
                appearance.GlassTopColor,
                appearance.GlassBottomColor,
                new Point(
                    0,
                    0),
                new Point(
                    0,
                    gradientReferenceHeight))
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                Opacity =
                    Math.Clamp(
                        appearance.Opacity,
                        0.10,
                        1.0)
            };

        LinearGradientBrush highlightBrush =
            new()
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                StartPoint =
                    new Point(
                        0,
                        0),
                EndPoint =
                    new Point(
                        0,
                        gradientReferenceHeight)
            };

        highlightBrush.GradientStops.Add(
            new GradientStop(
                appearance.GlassHighlightColor,
                0));

        highlightBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        LinearGradientBrush frameBrush =
            new()
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                StartPoint =
                    new Point(
                        0,
                        0),
                EndPoint =
                    new Point(
                        0,
                        gradientReferenceHeight)
            };

        frameBrush.GradientStops.Add(
            new GradientStop(
                appearance.GlassHighlightColor,
                0));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    (byte)(appearance.GlassHighlightColor.A * 0.35),
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                1));

        _glassSurfaceBorder.Background =
            surfaceBrush;

        _glassSurfaceBorder.Visibility =
            Visibility.Visible;

        _glassHighlightBorder.Background =
            highlightBrush;

        _glassHighlightBorder.BorderBrush =
            frameBrush;

        _glassHighlightBorder.BorderThickness =
            new Thickness(
                1);

        _glassHighlightBorder.Visibility =
            Visibility.Visible;

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarAeroRender",
            $"GlassSurfaceEnabled=True; Theme={appearance.ThemeName}; GlassTopColor={appearance.GlassTopColor}; GlassBottomColor={appearance.GlassBottomColor}; GlassHighlightColor={appearance.GlassHighlightColor}; GlassCornerRadius={cornerRadius:0.##}; SurfaceOpacity={surfaceBrush.Opacity:0.##}; Blur={appearance.BlurRadius:0.##}; GradientMapping={surfaceBrush.MappingMode}; GradientReferenceHeight={gradientReferenceHeight:0.##}; HighlightVisibility={_glassHighlightBorder.Visibility}; FrameStops={frameBrush.GradientStops.Count}; FrameStop0={frameBrush.GradientStops[0].Color}; FrameStop1={frameBrush.GradientStops[1].Color}; FrameStop2={frameBrush.GradientStops[2].Color}; DockBorderEnabled={appearance.DockBorderEnabled}; DockBorderColor={appearance.DockBorderColor}; BorderThickness={_windowBorder.BorderThickness}");
    }

    private void ReplaceBrushes(
        DependencyObject root,
        Brush oldWindowBackgroundBrush,
        Brush oldPanelBackgroundBrush,
        Brush oldBorderBrush,
        Brush oldTextBrush,
        Brush oldMutedTextBrush)
    {
        if (root is Control control)
        {
            if (ReferenceEquals(
                    control.Background,
                    oldWindowBackgroundBrush))
            {
                control.Background =
                    WindowBackgroundBrush;
            }
            else if (ReferenceEquals(
                         control.Background,
                         oldPanelBackgroundBrush))
            {
                control.Background =
                    PanelBackgroundBrush;
            }

            if (ReferenceEquals(
                    control.BorderBrush,
                    oldBorderBrush))
            {
                control.BorderBrush =
                    BorderBrushValue;
            }

            if (ReferenceEquals(
                    control.Foreground,
                    oldTextBrush))
            {
                control.Foreground =
                    TextBrush;
            }
            else if (ReferenceEquals(
                         control.Foreground,
                         oldMutedTextBrush))
            {
                control.Foreground =
                    MutedTextBrush;
            }
        }

        if (root is TextBlock textBlock)
        {
            if (ReferenceEquals(
                    textBlock.Foreground,
                    oldTextBrush))
            {
                textBlock.Foreground =
                    TextBrush;
            }
            else if (ReferenceEquals(
                         textBlock.Foreground,
                         oldMutedTextBrush))
            {
                textBlock.Foreground =
                    MutedTextBrush;
            }
        }

        if (root is Border border)
        {
            if (ReferenceEquals(
                    border.Background,
                    oldWindowBackgroundBrush))
            {
                border.Background =
                    WindowBackgroundBrush;
            }
            else if (ReferenceEquals(
                         border.Background,
                         oldPanelBackgroundBrush))
            {
                border.Background =
                    PanelBackgroundBrush;
            }

            if (ReferenceEquals(
                    border.BorderBrush,
                    oldBorderBrush))
            {
                border.BorderBrush =
                    BorderBrushValue;
            }
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int childIndex = 0;
             childIndex < childCount;
             childIndex++)
        {
            ReplaceBrushes(
                VisualTreeHelper.GetChild(
                    root,
                    childIndex),
                oldWindowBackgroundBrush,
                oldPanelBackgroundBrush,
                oldBorderBrush,
                oldTextBrush,
                oldMutedTextBrush);
        }
    }

    private void ReplaceEventBrushes(
        DependencyObject root,
        Brush oldEventBrush)
    {
        if (root is Control control)
        {
            if (ReferenceEquals(
                    control.Background,
                    oldEventBrush))
            {
                control.Background =
                    _eventBrush;
            }

            if (ReferenceEquals(
                    control.BorderBrush,
                    oldEventBrush))
            {
                control.BorderBrush =
                    _eventBrush;
            }

            if (ReferenceEquals(
                    control.Foreground,
                    oldEventBrush))
            {
                control.Foreground =
                    _eventBrush;
            }
        }

        if (root is TextBlock textBlock &&
            ReferenceEquals(
                textBlock.Foreground,
                oldEventBrush))
        {
            textBlock.Foreground =
                _eventBrush;
        }

        if (root is Border border)
        {
            if (ReferenceEquals(
                    border.Background,
                    oldEventBrush))
            {
                border.Background =
                    _eventBrush;
            }

            if (ReferenceEquals(
                    border.BorderBrush,
                    oldEventBrush))
            {
                border.BorderBrush =
                    _eventBrush;
            }
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int childIndex = 0;
             childIndex < childCount;
             childIndex++)
        {
            ReplaceEventBrushes(
                VisualTreeHelper.GetChild(
                    root,
                    childIndex),
                oldEventBrush);
        }
    }

    private Grid CreateTitleBar()
    {
        Grid titleBar =
            new()
            {
                Margin =
                    new Thickness(
                        6,
                        2,
                        2,
                        8),
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
                Width = GridLength.Auto
            });

        TextBlock title =
            new()
            {
                Text =
                    DateTime.Today.ToString(
                        "dddd, d. MMMM",
                        GermanCulture),
                Foreground =
                    TextBrush,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

        Button closeButton =
            CreateFlatButton(
                "×",
                32,
                30);

        closeButton.FontSize = 18;

        closeButton.Click +=
            (_, _) =>
                Close();

        titleBar.MouseLeftButtonDown +=
            TitleBar_MouseLeftButtonDown;

        Grid.SetColumn(
            title,
            0);

        Grid.SetColumn(
            closeButton,
            1);

        titleBar.Children.Add(
            title);

        titleBar.Children.Add(
            closeButton);

        return titleBar;
    }
    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed ||
            IsInteractiveElement(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarWindow",
            $"Drag start; Zone=Header; Left={Left:0.0}; Top={Top:0.0}");

        DragMove();

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarWindow",
            $"Drag end; Zone=Header; Left={Left:0.0}; Top={Top:0.0}");

        e.Handled =
            true;
    }

    private void WindowBorder_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed ||
            sender is not Border windowBorder ||
            IsInteractiveElement(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

        Point position =
            e.GetPosition(
                windowBorder);

        const double dragEdge = 18.0;
        const double dragHeader = 52.0;

        bool isLeftEdge =
            position.X <=
            dragEdge;

        bool isRightEdge =
            position.X >=
            windowBorder.ActualWidth -
            dragEdge;

        bool isTopEdge =
            position.Y <=
            dragHeader;

        bool isBottomEdge =
            position.Y >=
            windowBorder.ActualHeight -
            dragEdge;

        if (!isLeftEdge &&
            !isRightEdge &&
            !isTopEdge &&
            !isBottomEdge)
        {
            return;
        }

        string zone =
            isTopEdge
                ? "Top"
                : isBottomEdge
                    ? "Bottom"
                    : isLeftEdge
                        ? "Left"
                        : "Right";

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarWindow",
            $"Drag start; Zone={zone}; X={position.X:0.0}; Y={position.Y:0.0}; Left={Left:0.0}; Top={Top:0.0}");

        DragMove();

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarWindow",
            $"Drag end; Zone={zone}; Left={Left:0.0}; Top={Top:0.0}");

        e.Handled =
            true;
    }

    private static bool IsInteractiveElement(
        DependencyObject? source)
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is Button ||
                current is TextBox ||
                current is ComboBox ||
                current is CheckBox ||
                current is MenuItem)
            {
                return true;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return false;
    }

    private Grid CreateMonthNavigation()
    {
        Grid navigation =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        8)
            };

        navigation.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        42)
            });

        navigation.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        navigation.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        36)
            });

        navigation.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        42)
            });

        Button previousButton =
            CreateFlatButton(
                "‹",
                36,
                34);

        Button todayButton =
            CreateFlatButton(
                "⌾",
                32,
                30);

        Button nextButton =
            CreateFlatButton(
                "›",
                36,
                34);

        previousButton.FontSize = 24;
        todayButton.FontSize = 17;
        nextButton.FontSize = 24;

        previousButton.ToolTip = "Previous month";
        todayButton.ToolTip = "Today";
        nextButton.ToolTip = "Next month";

        TextBlock monthYearText =
            new()
            {
                Foreground =
                    TextBrush,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

        previousButton.Click +=
            (_, _) =>
            {
                _displayMonth =
                    _displayMonth.AddMonths(
                        -1);

                UpdateCalendarView();
            };

        todayButton.Click +=
            (_, _) =>
            {
                _displayMonth =
                    new DateTime(
                        DateTime.Today.Year,
                        DateTime.Today.Month,
                        1);

                UpdateCalendarView();
            };

        nextButton.Click +=
            (_, _) =>
            {
                _displayMonth =
                    _displayMonth.AddMonths(
                        1);

                UpdateCalendarView();
            };

        Grid.SetColumn(
            previousButton,
            0);

        Grid.SetColumn(
            monthYearText,
            1);

        Grid.SetColumn(
            todayButton,
            2);

        Grid.SetColumn(
            nextButton,
            3);

        navigation.Children.Add(
            previousButton);

        navigation.Children.Add(
            monthYearText);

        navigation.Children.Add(
            todayButton);

        navigation.Children.Add(
            nextButton);

        navigation.Tag =
            monthYearText;

        return navigation;
    }

    private Grid CreateWeekdayHeader()
    {
        Grid weekdays =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        4)
            };

        for (int column = 0;
             column < 7;
             column++)
        {
            weekdays.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });
        }

        string[] labels =
        {
            "Mo",
            "Di",
            "Mi",
            "Do",
            "Fr",
            "Sa",
            "So"
        };

        for (int column = 0;
             column < labels.Length;
             column++)
        {
            TextBlock label =
                new()
                {
                    Text =
                        labels[column],
                    Foreground =
                        MutedTextBrush,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin =
                        new Thickness(
                            0,
                            3,
                            0,
                            3)
                };

            Grid.SetColumn(
                label,
                column);

            weekdays.Children.Add(
                label);
        }

        return weekdays;
    }

    private static Grid CreateDaysGrid()
    {
        Grid grid =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        8)
            };

        for (int column = 0;
             column < 7;
             column++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });
        }

        for (int row = 0;
             row < 6;
             row++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });
        }

        return grid;
    }

    private Border CreateEventSection()
    {
        Border border =
            new()
            {
                Background =
                    PanelBackgroundBrush,
                BorderBrush =
                    BorderBrushValue,
                BorderThickness =
                    new Thickness(
                        1),
                CornerRadius =
                    new CornerRadius(
                        12),
                Padding =
                    new Thickness(
                        10),
                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        0)
            };

        StackPanel content =
            new();

        TextBlock selectedDateText =
            new()
            {
                Foreground =
                    TextBrush,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        6)
            };

        StackPanel eventsPanel =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        6)
            };

        Grid editorGrid =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        6)
            };

        editorGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        31)
            });

        editorGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        31)
            });

        editorGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        31)
            });

        editorGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Button newButton =
            CreateFlatButton(
                "+",
                27,
                27);

        newButton.ToolTip =
            "New";

        Button deleteButton =
            CreateFlatButton(
                "−",
                27,
                27);

        deleteButton.ToolTip =
            "Delete";

        deleteButton.IsEnabled =
            false;

        Button saveButton =
            CreateAccentButton(
                "💾");

        saveButton.Width =
            27;

        saveButton.Height =
            27;

        saveButton.Content =
            new TextBlock
            {
                Text = "💾",
                Margin =
                    new Thickness(
                        -1,
                        0,
                        0,
                        0),
                FontSize = 11,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        saveButton.ToolTip =
            "Add event";

        TextBox titleTextBox =
            CreateTextBox(
                string.Empty);

        titleTextBox.Margin =
            new Thickness(
                0,
                0,
                0,
                0);

        titleTextBox.ToolTip =
            "Event title";

        newButton.Click +=
            (_, _) =>
                BeginNewEvent();

        saveButton.Click +=
            (_, _) =>
                SaveEvent();

        deleteButton.Click +=
            (_, _) =>
                DeleteSelectedEvent();

        Grid.SetColumn(
            newButton,
            0);

        Grid.SetColumn(
            deleteButton,
            1);

        Grid.SetColumn(
            saveButton,
            2);

        Grid.SetColumn(
            titleTextBox,
            3);

        editorGrid.Children.Add(
            newButton);

        editorGrid.Children.Add(
            deleteButton);

        editorGrid.Children.Add(
            saveButton);

        editorGrid.Children.Add(
            titleTextBox);

        Grid scheduleGrid =
            new()
            {
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        0)
            };

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        24)
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        64)
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        28)
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        50)
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        scheduleGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        40)
            });

        TextBlock startTimeSymbol =
            new()
            {
                Text = "◷",
                Foreground =
                    TextBrush,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Start time"
            };

        TextBox timeTextBox =
            CreateTextBox(
                "09:00");

        timeTextBox.Width =
            60;

        timeTextBox.MaxLength =
            5;

        timeTextBox.ToolTip =
            "Time (HH:mm)";

        TextBlock durationSymbol =
            new()
            {
                Text = "⏱",
                Foreground =
                    TextBrush,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Duration in minutes"
            };

        TextBox durationTextBox =
            CreateTextBox(
                "15");

        durationTextBox.Width =
            46;

        durationTextBox.ToolTip =
            "Duration in minutes";

        CheckBox allDayCheckBox =
            new()
            {
                Content = "All day",
                Foreground =
                    TextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0)
            };

        TextBlock overrideSettingsGear =
            new()
            {
                Text = "\uE713",
                FontFamily =
                    new FontFamily(
                        "Segoe MDL2 Assets"),
                FontSize = 14,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                TextAlignment =
                    TextAlignment.Center
            };

        Button overrideSettingsButton =
            CreateFlatButton(
                string.Empty,
                30,
                26);

        overrideSettingsButton.Content =
            overrideSettingsGear;

        overrideSettingsButton.Margin =
            new Thickness(
                2,
                0,
                2,
                0);

        overrideSettingsButton.HorizontalAlignment =
            HorizontalAlignment.Right;

        overrideSettingsButton.ToolTip =
            "Calendar settings (overrides)";

        overrideSettingsButton.Click +=
            (_, _) =>
                OpenCalendarOverrideSettings();

        Grid.SetColumn(
            startTimeSymbol,
            0);

        Grid.SetColumn(
            timeTextBox,
            1);

        Grid.SetColumn(
            durationSymbol,
            2);

        Grid.SetColumn(
            durationTextBox,
            3);

        Grid.SetColumn(
            allDayCheckBox,
            4);

        Grid.SetColumn(
            overrideSettingsButton,
            6);

        scheduleGrid.Children.Add(
            startTimeSymbol);

        scheduleGrid.Children.Add(
            timeTextBox);

        scheduleGrid.Children.Add(
            durationSymbol);

        scheduleGrid.Children.Add(
            durationTextBox);

        scheduleGrid.Children.Add(
            allDayCheckBox);

        scheduleGrid.Children.Add(
            overrideSettingsButton);

        content.Children.Add(
            selectedDateText);

        content.Children.Add(
            eventsPanel);

        content.Children.Add(
            editorGrid);

        content.Children.Add(
            scheduleGrid);

        border.Child =
            content;

        border.Tag =
            selectedDateText;

        return border;
    }

    private void UpdateCalendarView()
    {
        _monthYearText.Text =
            _displayMonth.ToString(
                "MMMM yyyy",
                GermanCulture);

        _daysGrid.Children.Clear();

        DateTime firstDay =
            new(
                _displayMonth.Year,
                _displayMonth.Month,
                1);

        int mondayBasedOffset =
            ((int)firstDay.DayOfWeek + 6) %
            7;

        DateTime gridStart =
            firstDay.AddDays(
                -mondayBasedOffset);

        for (int index = 0;
             index < 42;
             index++)
        {
            DateTime date =
                gridStart.AddDays(
                    index);

            Button dayButton =
                CreateDayButton(
                    date);

            Grid.SetRow(
                dayButton,
                index / 7);

            Grid.SetColumn(
                dayButton,
                index % 7);

            _daysGrid.Children.Add(
                dayButton);
        }
    }
    private Button CreateDayButton(
        DateTime date)
    {
        bool isCurrentMonth =
            date.Month == _displayMonth.Month;

        bool isToday =
            date.Date == DateTime.Today;

        bool isSelected =
            date.Date == _selectedDate.Date;

        bool hasEvents =
            _events.Any(
                calendarEvent =>
                    calendarEvent.StartAt.Date ==
                    date.Date);

        StackPanel content =
            new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        TextBlock number =
            new()
            {
                Text =
                    date.Day.ToString(
                        CultureInfo.InvariantCulture),
                Foreground =
                    isToday
                        ? Brushes.Black
                        : isCurrentMonth
                            ? TextBrush
                            : MutedTextBrush,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

        content.Children.Add(
            number);

        TextBlock marker =
            new()
            {
                Text =
                    hasEvents
                        ? "•"
                        : string.Empty,
                Foreground =
                    _eventBrush,
                FontSize = 12,
                Height = 8,
                Margin =
                    new Thickness(
                        0,
                        -3,
                        0,
                        0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

        content.Children.Add(
            marker);

        Button button =
            new()
            {
                Content =
                    content,
                Background =
                    isToday
                        ? _eventBrush
                        : Brushes.Transparent,
                Foreground =
                    TextBrush,
                BorderBrush =
                    hasEvents
                        ? _eventBrush
                        : isSelected
                            ? TextBrush
                            : Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        hasEvents || isSelected
                            ? 1
                            : 0),
                Margin =
                    new Thickness(
                        3),
                Padding =
                    new Thickness(
                        0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor =
                    Cursors.Hand,
                Tag =
                    date
            };

        button.Template =
            CreateRoundedButtonTemplate(
                18);

        button.Click +=
            DayButton_Click;

        return button;
    }

    private void DayButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not DateTime date)
        {
            return;
        }

        _selectedDate =
            date.Date;

        if (_selectedDate.Month !=
            _displayMonth.Month ||
            _selectedDate.Year !=
            _displayMonth.Year)
        {
            _displayMonth =
                new DateTime(
                    _selectedDate.Year,
                    _selectedDate.Month,
                    1);
        }

        _selectedEventId =
            null;

        UpdateCalendarView();
        UpdateEventView();
    }

    private void UpdateEventView()
    {
        _selectedDateText.Text =
            _selectedDate.ToString(
                "dddd, dd.MM.yyyy",
                GermanCulture);

        _eventsPanel.Children.Clear();

        List<ClockWidgetCalendarEventEntry> dayEvents =
            _events
                .Where(
                    calendarEvent =>
                        calendarEvent.StartAt.Date ==
                        _selectedDate.Date)
                .OrderBy(
                    calendarEvent =>
                        calendarEvent.StartAt)
                .ToList();

        if (dayEvents.Count == 0)
        {
            _eventsPanel.Children.Add(
                new TextBlock
                {
                    Text = "No events",
                    Foreground =
                        MutedTextBrush,
                    FontSize = 12,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            2)
                });
        }
        else
        {
            int visibleCount =
                Math.Min(
                    dayEvents.Count,
                    3);

            for (int index = 0;
                 index < visibleCount;
                 index++)
            {
                ClockWidgetCalendarEventEntry calendarEvent =
                    dayEvents[index];

                string eventSchedule;

                if (calendarEvent.IsAllDay)
                {
                    eventSchedule =
                        "All day";
                }
                else
                {
                    int durationMinutes =
                        Math.Max(
                            1,
                            calendarEvent.DurationMinutes);

                    DateTime endAt =
                        calendarEvent.StartAt.AddMinutes(
                            durationMinutes);

                    eventSchedule =
                        $"{calendarEvent.StartAt:HH:mm}-{endAt:HH:mm} ({durationMinutes} min)";
                }

                Button eventButton =
                    CreateFlatButton(
                        $"{eventSchedule}  {calendarEvent.Title}",
                        double.NaN,
                        28);

                eventButton.HorizontalContentAlignment =
                    HorizontalAlignment.Left;

                eventButton.Padding =
                    new Thickness(
                        14,
                        0,
                        8,
                        0);

                eventButton.Tag =
                    calendarEvent.Id;

                eventButton.Click +=
                    EventButton_Click;

                ContextMenu eventContextMenu =
                    new();

                MenuItem deleteMenuItem =
                    new()
                    {
                        Header = "Delete",
                        Tag =
                            calendarEvent.Id
                    };

                deleteMenuItem.Click +=
                    DeleteEventMenuItem_Click;

                eventContextMenu.Items.Add(
                    deleteMenuItem);

                eventButton.ContextMenu =
                    eventContextMenu;

                _eventsPanel.Children.Add(
                    eventButton);
            }

            if (dayEvents.Count > visibleCount)
            {
                _eventsPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"+ {dayEvents.Count - visibleCount} more",
                        Foreground =
                            MutedTextBrush,
                        FontSize = 11,
                        Margin =
                            new Thickness(
                                14,
                                2,
                                0,
                                0)
                    });
            }
        }

        ClockWidgetCalendarEventEntry? selectedEvent =
            _events.FirstOrDefault(
                calendarEvent =>
                    calendarEvent.Id ==
                    _selectedEventId);

        if (selectedEvent is null ||
            selectedEvent.StartAt.Date !=
            _selectedDate.Date)
        {
            _selectedEventId =
                null;

            _eventTimeTextBox.Text =
                "09:00";

            _eventTitleTextBox.Text =
                string.Empty;

            _eventAllDayCheckBox.IsChecked =
                false;

            _eventDurationTextBox.Text =
                "15";

            UpdateScheduleEditorState();

            _saveEventButton.ToolTip =
                "Add event";

            _deleteEventButton.IsEnabled =
                false;
        }
        else
        {
            _eventTimeTextBox.Text =
                selectedEvent.StartAt.ToString(
                    "HH:mm",
                    CultureInfo.InvariantCulture);

            _eventTitleTextBox.Text =
                selectedEvent.Title;

            _eventAllDayCheckBox.IsChecked =
                selectedEvent.IsAllDay;

            _eventDurationTextBox.Text =
                Math.Max(
                        15,
                        selectedEvent.DurationMinutes)
                    .ToString(
                        CultureInfo.InvariantCulture);

            UpdateScheduleEditorState();

            _saveEventButton.ToolTip =
                "Save event";

            _deleteEventButton.IsEnabled =
                true;
        }
    }

    private void EventButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string eventId)
        {
            return;
        }

        _selectedEventId =
            eventId;

        UpdateEventView();

        _eventTitleTextBox.Focus();
        _eventTitleTextBox.SelectAll();
    }

    private void DeleteEventMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not string eventId)
        {
            return;
        }

        ClockWidgetCalendarEventEntry? calendarEvent =
            _events.FirstOrDefault(
                item =>
                    item.Id ==
                    eventId);

        if (calendarEvent is null)
        {
            return;
        }

        MessageBoxResult result =
            MessageBox.Show(
                this,
                $"Delete event \"{calendarEvent.Title}\"?",
                "Delete event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteEvent(
            calendarEvent);
    }

    private void DeleteEvent(
        ClockWidgetCalendarEventEntry calendarEvent)
    {
        string eventId =
            calendarEvent.Id;

        _events.Remove(
            calendarEvent);

        if (_selectedEventId ==
            eventId)
        {
            _selectedEventId =
                null;
        }

        _saveState();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Calendar event removed; Id={eventId}");

        UpdateCalendarView();
        UpdateEventView();
    }

    private void BeginNewEvent()
    {
        _selectedEventId =
            null;

        _eventTimeTextBox.Text =
            "09:00";

        _eventTitleTextBox.Text =
            string.Empty;

        _eventAllDayCheckBox.IsChecked =
            false;

        _eventDurationTextBox.Text =
            "15";

        UpdateScheduleEditorState();

        _saveEventButton.ToolTip =
            "Add event";

        _deleteEventButton.IsEnabled =
            false;

        _eventTitleTextBox.Focus();
    }

    private void EventAllDayCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateScheduleEditorState();
    }

    private void UpdateScheduleEditorState()
    {
        bool isAllDay =
            _eventAllDayCheckBox.IsChecked ==
            true;

        _eventTimeTextBox.IsEnabled =
            !isAllDay;

        _eventDurationTextBox.IsEnabled =
            !isAllDay;
    }

    private void SaveEvent()
    {
        string title =
            _eventTitleTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                title))
        {
            _eventTitleTextBox.Focus();
            return;
        }

        bool isAllDay =
            _eventAllDayCheckBox.IsChecked ==
            true;

        TimeSpan time =
            TimeSpan.Zero;

        int durationMinutes =
            0;

        if (!isAllDay)
        {
            if (!TimeSpan.TryParseExact(
                    _eventTimeTextBox.Text.Trim(),
                    "hh\\:mm",
                    CultureInfo.InvariantCulture,
                    out time) ||
                time < TimeSpan.Zero ||
                time >= TimeSpan.FromDays(
                    1))
            {
                _eventTimeTextBox.Focus();
                _eventTimeTextBox.SelectAll();
                return;
            }

            if (!int.TryParse(
                    _eventDurationTextBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out durationMinutes) ||
                durationMinutes <= 0)
            {
                _eventDurationTextBox.Focus();
                _eventDurationTextBox.SelectAll();
                return;
            }
        }

        DateTime startAt =
            isAllDay
                ? _selectedDate.Date
                : _selectedDate.Date.Add(
                    time);

        ClockWidgetCalendarEventEntry? selectedEvent =
            _events.FirstOrDefault(
                calendarEvent =>
                    calendarEvent.Id ==
                    _selectedEventId);

        if (selectedEvent is null)
        {
            selectedEvent =
                new ClockWidgetCalendarEventEntry
                {
                    StartAt =
                        startAt,
                    IsAllDay =
                        isAllDay,
                    DurationMinutes =
                        durationMinutes,
                    Title =
                        title
                };

            _events.Add(
                selectedEvent);

            _selectedEventId =
                selectedEvent.Id;

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Calendar event added; Id={selectedEvent.Id}; StartAt={selectedEvent.StartAt:O}; IsAllDay={selectedEvent.IsAllDay}; DurationMinutes={selectedEvent.DurationMinutes}; Title={selectedEvent.Title}");
        }
        else
        {
            selectedEvent.StartAt =
                startAt;

            selectedEvent.IsAllDay =
                isAllDay;

            selectedEvent.DurationMinutes =
                durationMinutes;

            selectedEvent.Title =
                title;

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Calendar event updated; Id={selectedEvent.Id}; StartAt={selectedEvent.StartAt:O}; IsAllDay={selectedEvent.IsAllDay}; DurationMinutes={selectedEvent.DurationMinutes}; Title={selectedEvent.Title}");
        }

        _saveState();

        UpdateCalendarView();
        UpdateEventView();
    }

    private void DeleteSelectedEvent()
    {
        ClockWidgetCalendarEventEntry? selectedEvent =
            _events.FirstOrDefault(
                calendarEvent =>
                    calendarEvent.Id ==
                    _selectedEventId);

        if (selectedEvent is null)
        {
            return;
        }

        MessageBoxResult result =
            MessageBox.Show(
                this,
                $"Delete event \"{selectedEvent.Title}\"?",
                "Delete event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteEvent(
            selectedEvent);
    }
    private static Brush GetContrastingTextBrush(
        Brush backgroundBrush,
        Brush fallbackBrush)
    {
        if (backgroundBrush is not SolidColorBrush solidColorBrush)
        {
            return fallbackBrush;
        }

        Color color =
            solidColorBrush.Color;

        static double ToLinear(
            byte channel)
        {
            double value =
                channel /
                255.0;

            return value <= 0.04045
                ? value / 12.92
                : Math.Pow(
                    (value + 0.055) /
                    1.055,
                    2.4);
        }

        double luminance =
            0.2126 *
            ToLinear(
                color.R) +
            0.7152 *
            ToLinear(
                color.G) +
            0.0722 *
            ToLinear(
                color.B);

        double whiteContrast =
            1.05 /
            (luminance + 0.05);

        double blackContrast =
            (luminance + 0.05) /
            0.05;

        return whiteContrast >= blackContrast
            ? Brushes.White
            : Brushes.Black;
    }

    private static void ApplyDurationComboBoxTheme(
        ComboBox comboBox,
        Brush backgroundBrush,
        Brush foregroundBrush,
        Brush borderBrush)
    {
        comboBox.Background =
            backgroundBrush;

        comboBox.Foreground =
            foregroundBrush;

        comboBox.BorderBrush =
            borderBrush;

        comboBox.ItemContainerStyle =
            CreateDurationComboBoxItemStyle(
                backgroundBrush,
                foregroundBrush);

        comboBox.Resources[SystemColors.WindowBrushKey] =
            backgroundBrush;

        comboBox.Resources[SystemColors.WindowTextBrushKey] =
            foregroundBrush;

        comboBox.Resources[SystemColors.ControlBrushKey] =
            backgroundBrush;

        comboBox.Resources[SystemColors.ControlTextBrushKey] =
            foregroundBrush;

        comboBox.Resources[SystemColors.HighlightBrushKey] =
            borderBrush;

        comboBox.Resources[SystemColors.HighlightTextBrushKey] =
            foregroundBrush;
    }

    private static Style CreateDurationComboBoxItemStyle(
        Brush backgroundBrush,
        Brush foregroundBrush)
    {
        Style style =
            new(
                typeof(
                    ComboBoxItem));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                backgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                foregroundBrush));

        style.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(
                    6,
                    3,
                    6,
                    3)));

        return style;
    }

    private ContextMenu CreateCalendarContextMenu()
    {
        ContextMenu contextMenu =
            new();

        MenuItem settingsItem =
            new()
            {
                Header =
                    "Calendar settings (overrides)"
            };

        settingsItem.Click +=
            (_, _) =>
                OpenCalendarOverrideSettings();

        contextMenu.Items.Add(
            settingsItem);

        return contextMenu;
    }

    private void OpenCalendarOverrideSettings()
    {
        _openOverrideSettings();
    }

    private GlueDockWidgetAppearance CreateEffectiveAppearance(
        GlueDockWidgetAppearance hostAppearance)
    {
        ClockWidgetCalendarOverrideSettings overrides =
            _settings.CalendarOverrides;

        GlueDockWidgetThemeAppearance? selectedTheme =
            null;

        if (overrides.ThemeOverrideEnabled &&
            !string.IsNullOrWhiteSpace(
                overrides.ThemeName))
        {
            selectedTheme =
                hostAppearance.AvailableThemes.FirstOrDefault(
                    theme =>
                        string.Equals(
                            theme.ThemeName,
                            overrides.ThemeName,
                            StringComparison.OrdinalIgnoreCase));
        }

        Brush dockBackgroundBrush =
            selectedTheme?.DockBackgroundBrush.Clone() ??
            hostAppearance.DockBackgroundBrush.Clone();

        Brush itemBackgroundBrush =
            selectedTheme?.DockItemBackgroundBrush.Clone() ??
            hostAppearance.DockItemBackgroundBrush.Clone();

        Brush itemBorderBrush =
            selectedTheme?.DockItemBorderBrush.Clone() ??
            hostAppearance.DockItemBorderBrush.Clone();

        Brush textBrush =
            selectedTheme?.DockTextBrush.Clone() ??
            hostAppearance.DockTextBrush.Clone();

        double opacity =
            overrides.OpacityOverrideEnabled
                ? Math.Clamp(
                    overrides.Opacity,
                    0.10,
                    1.00)
                : selectedTheme?.Opacity ??
                  hostAppearance.Opacity;

        double blurRadius =
            overrides.BlurOverrideEnabled
                ? Math.Clamp(
                    overrides.BlurRadius,
                    0,
                    100)
                : selectedTheme?.BlurRadius ??
                  hostAppearance.BlurRadius;

        Color glassTopColor =
            selectedTheme?.GlassTopColor ??
            hostAppearance.GlassTopColor;

        Color glassBottomColor =
            selectedTheme?.GlassBottomColor ??
            hostAppearance.GlassBottomColor;

        if (overrides.OpacityOverrideEnabled)
        {
            glassTopColor =
                Color.FromArgb(
                    0xFF,
                    glassTopColor.R,
                    glassTopColor.G,
                    glassTopColor.B);

            glassBottomColor =
                Color.FromArgb(
                    0xFF,
                    glassBottomColor.R,
                    glassBottomColor.G,
                    glassBottomColor.B);
        }

        if (dockBackgroundBrush is SolidColorBrush solidDockBackgroundBrush)
        {
            Color color =
                solidDockBackgroundBrush.Color;

            dockBackgroundBrush =
                new SolidColorBrush(
                    Color.FromArgb(
                        (byte)Math.Clamp(
                            Math.Round(
                                opacity * 255),
                            0,
                            255),
                        color.R,
                        color.G,
                        color.B));
        }

        if (overrides.TextColorOverrideEnabled &&
            TryParseColor(
                overrides.TextColor,
                out Color textColor))
        {
            textBrush =
                new SolidColorBrush(
                    textColor);
        }

        bool borderEnabled =
            hostAppearance.DockBorderEnabled;

        Color borderColor =
            hostAppearance.DockBorderColor;

        if (overrides.BorderOverrideEnabled)
        {
            borderEnabled =
                Math.Clamp(
                    overrides.BorderThickness,
                    0,
                    12) > 0;

            if (TryParseColor(
                    overrides.BorderColor,
                    out Color overrideBorderColor))
            {
                borderColor =
                    overrideBorderColor;
            }
        }

        return new GlueDockWidgetAppearance
        {
            ThemeName =
                selectedTheme?.ThemeName ??
                hostAppearance.ThemeName,
            DockBackgroundBrush =
                dockBackgroundBrush,
            DockItemBackgroundBrush =
                itemBackgroundBrush,
            DockItemBorderBrush =
                itemBorderBrush,
            DockTextBrush =
                textBrush,
            DockBorderEnabled =
                borderEnabled,
            DockBorderColor =
                borderColor,
            Opacity =
                opacity,
            BlurRadius =
                blurRadius,
            GlassSurfaceEnabled =
                selectedTheme?.GlassSurfaceEnabled ??
                hostAppearance.GlassSurfaceEnabled,
            GlassTopColor =
                glassTopColor,
            GlassBottomColor =
                glassBottomColor,
            GlassHighlightColor =
                selectedTheme?.GlassHighlightColor ??
                hostAppearance.GlassHighlightColor,
            GlassCornerRadius =
                overrides.CornerRadiusOverrideEnabled
                    ? Math.Clamp(
                        overrides.CornerRadius,
                        0,
                        80)
                    : selectedTheme?.GlassCornerRadius ??
                      hostAppearance.GlassCornerRadius,
            GlassGradientReferenceHeight =
                selectedTheme?.GlassGradientReferenceHeight ??
                hostAppearance.GlassGradientReferenceHeight,
            AvailableThemes =
                hostAppearance.AvailableThemes
        };
    }

    private void UpdateEventBrush()
    {
        Color eventColor =
            _generalEventColor;

        if (_settings.CalendarOverrides.EventColorOverrideEnabled &&
            TryParseColor(
                _settings.CalendarOverrides.EventColor,
                out Color overrideEventColor))
        {
            eventColor =
                overrideEventColor;
        }

        _eventBrush =
            new SolidColorBrush(
                eventColor);
    }

    private static bool TryParseColor(
        string? value,
        out Color color)
    {
        try
        {
            object? converted =
                ColorConverter.ConvertFromString(
                    value);

            if (converted is Color parsedColor)
            {
                color =
                    parsedColor;

                return true;
            }
        }
        catch
        {
        }

        color =
            Colors.Transparent;

        return false;
    }

    private TextBox CreateTextBox(
        string text)
    {
        return new TextBox
        {
            Text =
                text,
            Height = 30,
            Margin =
                new Thickness(
                    2),
            Padding =
                new Thickness(
                    7,
                    4,
                    7,
                    4),
            Background =
                PanelBackgroundBrush,
            Foreground =
                TextBrush,
            CaretBrush =
                _eventBrush,
            BorderBrush =
                BorderBrushValue,
            BorderThickness =
                new Thickness(
                    1),
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }
    private Button CreateFlatButton(
        string text,
        double width,
        double height)
    {
        Button button =
            new()
            {
                Content =
                    text,
                Height =
                    height,
                Margin =
                    new Thickness(
                        2),
                Padding =
                    new Thickness(
                        8,
                        0,
                        8,
                        0),
                Background =
                    Brushes.Transparent,
                Foreground =
                    _eventBrush,
                BorderBrush =
                    _eventBrush,
                BorderThickness =
                    new Thickness(
                        1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor =
                    Cursors.Hand
            };

        if (!double.IsNaN(
                width))
        {
            button.Width =
                width;
        }

        button.Template =
            CreateRoundedButtonTemplate(
                8);

        return button;
    }
    private Button CreateAccentButton(
        string text)
    {
        Button button =
            CreateFlatButton(
                text,
                double.NaN,
                30);

        button.Background =
            _eventBrush;

        button.Foreground =
            Brushes.Black;

        button.BorderBrush =
            _eventBrush;

        return button;
    }

    private static ControlTemplate CreateRoundedButtonTemplate(
        double cornerRadius)
    {
        FrameworkElementFactory border =
            new(
                typeof(
                    Border));

        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(
                "Background")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        border.SetBinding(
            Border.BorderBrushProperty,
            new System.Windows.Data.Binding(
                "BorderBrush")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        border.SetBinding(
            Border.BorderThicknessProperty,
            new System.Windows.Data.Binding(
                "BorderThickness")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(
                cornerRadius));

        FrameworkElementFactory contentPresenter =
            new(
                typeof(
                    ContentPresenter));

        contentPresenter.SetBinding(
            ContentPresenter.ContentProperty,
            new System.Windows.Data.Binding(
                "Content")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        contentPresenter.SetBinding(
            ContentPresenter.HorizontalAlignmentProperty,
            new System.Windows.Data.Binding(
                "HorizontalContentAlignment")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        contentPresenter.SetBinding(
            ContentPresenter.VerticalAlignmentProperty,
            new System.Windows.Data.Binding(
                "VerticalContentAlignment")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        contentPresenter.SetBinding(
            ContentPresenter.MarginProperty,
            new System.Windows.Data.Binding(
                "Padding")
            {
                RelativeSource =
                    System.Windows.Data.RelativeSource.TemplatedParent
            });

        border.AppendChild(
            contentPresenter);

        return new ControlTemplate(
            typeof(
                Button))
        {
            VisualTree =
                border
        };
    }
}
