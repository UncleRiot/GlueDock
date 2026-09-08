using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GlueDock;
using IOPath = System.IO.Path;
using WpfPath = System.Windows.Shapes.Path;

namespace GlueDock.DriveUsageWidget;

public sealed class DriveUsageWidget : IGlueDockWidget
{
    private sealed class DriveRingVisual
    {
        public required string RootPath { get; init; }

        public required double RingSize { get; init; }

        public required double StrokeThickness { get; init; }

        public required Grid Root { get; init; }

        public required Ellipse SelectionFill { get; init; }

        public required Ellipse Track { get; init; }

        public required WpfPath Arc { get; init; }

        public required TextBlock Label { get; init; }
    }

    private readonly List<DriveRingVisual> _driveRingVisuals =
        new();

    private GlueDockWidgetContext? _context;
    private GlueDockWidgetAppearance? _hostAppearance;
    private readonly DriveUsageSettings _settings =
        new();
    private DispatcherTimer? _refreshTimer;
    private Grid? _mainRoot;
    private string? _selectedCompanionDriveRoot;
    private bool _companionClosedByUser;
    private Grid? _companionRoot;
    private TextBlock? _companionTotalText;
    private TextBlock? _companionUsedText;
    private TextBlock? _companionFreeText;
    private TextBlock? _companionPercentText;
    private Grid? _companionBarGrid;
    private Border? _companionBarTrack;
    private Border? _companionBarUsed;
    private ColumnDefinition? _companionBarUsedColumn;
    private ColumnDefinition? _companionBarFreeColumn;

    public string DisplayName =>
        "DriveUsage";

    public string Description =>
        "Displays free and used space for up to four drives.";

    public bool HasSettings =>
        true;

    public bool HasAlarm =>
        false;

    public bool HasTimer =>
        false;

    public bool HasStopwatch =>
        false;

    public bool HasCalendar =>
        false;

    public bool DisableDefaultHoverEffect =>
        _settings.DisableDefaultHoverEffect;

    public bool HasCompanionView =>
        !_companionClosedByUser &&
        !string.IsNullOrWhiteSpace(
            _selectedCompanionDriveRoot);

    public int CompanionSlotSpan =>
        2;

    public bool ShowCompanionAsSubDock =>
        true;

    public event EventHandler? CompanionViewChanged;

    public void Initialize(
        GlueDockWidgetContext context)
    {
        _context =
            context;

        _settings.CopyFrom(
            LoadSettings());

        if (_settings.SelectedDrives.Count == 0)
        {
            _settings.SelectedDrives =
                GetEligibleDrives()
                    .Select(
                        drive =>
                            drive.Name)
                    .Take(
                        4)
                    .ToList();

            SaveSettings();
        }

        Log(
            $"Initialized; InstanceId={context.InstanceId}; SelectedDrives={string.Join(",", _settings.SelectedDrives)}");
    }

    public FrameworkElement CreateView()
    {
        _mainRoot =
            new Grid
            {
                Width = 52,
                Height = 52,
                Background = Brushes.Transparent
            };

        RebuildMainView();
        RefreshDriveUsage();

        _refreshTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromSeconds(
                        10)
            };

        _refreshTimer.Tick +=
            RefreshTimer_Tick;

        _refreshTimer.Start();

        return _mainRoot;
    }

    public FrameworkElement? CreateCompanionView()
    {
        if (string.IsNullOrWhiteSpace(
                _selectedCompanionDriveRoot))
        {
            return null;
        }

        BuildCompanionView();
        UpdateCompanionView();

        return _companionRoot;
    }

    public void CloseCompanion()
    {
        _companionClosedByUser =
            true;

        _selectedCompanionDriveRoot =
            null;

        _companionRoot =
            null;

        ApplyAppearance();

        CompanionViewChanged?.Invoke(
            this,
            EventArgs.Empty);

        Log(
            "Companion closed by host.");
    }

    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _hostAppearance =
            appearance;

        ApplyAppearance();
        UpdateCompanionView();
    }

    public IReadOnlyList<GlueDockWidgetSettingsSection> CreateSettingsSections()
    {
        DriveUsageSettingsWindow settingsWindow =
            new(
                _settings.Clone(),
                _hostAppearance,
                CommitSettings,
                L,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged);

        (
            FrameworkElement generalContent,
            FrameworkElement companionContent
        ) =
            settingsWindow.CreateEmbeddedSettingsContent();

        return new[]
        {
            new GlueDockWidgetSettingsSection(
                "DriveUsage",
                "Settings.Navigation.WidgetDriveUsage",
                200,
                "General",
                "Settings.Tab.General",
                0,
                generalContent,
                settingsWindow.Close),
            new GlueDockWidgetSettingsSection(
                "DriveUsage",
                "Settings.Navigation.WidgetDriveUsage",
                200,
                "Companion",
                "Settings.WidgetSection.Companion",
                1,
                companionContent)
        };
    }

    public void OpenSettings(
        Window owner)
    {

        if (_context?.OpenSettingsSection is not null)
        {
            _context.OpenSettingsSection(
                "General");

            return;
        }

        DriveUsageSettings originalSettings =
            _settings.Clone();

        DriveUsageSettingsWindow dialog =
            new(
                originalSettings.Clone(),
                _hostAppearance,
                ApplySettingsPreview,
                L,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged)
            {
                Owner = owner
            };

        if (dialog.ShowDialog() != true)
        {
            _settings.CopyFrom(
                originalSettings);

            RebuildMainView();
            ApplyCompanionScale();
            RefreshDriveUsage();

            return;
        }

        CommitSettings(
            dialog.Result);
    }

    private void CommitSettings(
        DriveUsageSettings settings)
    {
        _settings.CopyFrom(
            settings);

        SaveSettings();

        if (!string.IsNullOrWhiteSpace(
                _selectedCompanionDriveRoot) &&
            !_settings.SelectedDrives.Contains(
                _selectedCompanionDriveRoot,
                StringComparer.OrdinalIgnoreCase))
        {
            _selectedCompanionDriveRoot =
                null;

            _companionClosedByUser =
                true;

            CompanionViewChanged?.Invoke(
                this,
                EventArgs.Empty);
        }

        RebuildMainView();
        ApplyCompanionScale();
        RefreshDriveUsage();

        Log(
            $"Settings saved; SelectedDrives={string.Join(",", _settings.SelectedDrives)}");
    }

    private void ApplySettingsPreview(
        DriveUsageSettings settings)
    {
        _settings.CopyFrom(
            settings);

        RebuildMainView();
        ApplyCompanionScale();
        RefreshDriveUsage();
    }

    public void OpenAlarm(
        Window owner)
    {
    }

    public void OpenTimer(
        Window owner)
    {
    }

    public void OpenStopwatch(
        Window owner)
    {
    }

    public void OpenCalendar(
        Window owner)
    {
    }

    private string L(
        string key)
    {
        return _context?.Localize?.Invoke(
                   key) ??
               key;
    }

    public void Dispose()
    {
        if (_refreshTimer is not null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -=
                RefreshTimer_Tick;

            _refreshTimer =
                null;
        }

        _driveRingVisuals.Clear();

        _mainRoot =
            null;

        _companionRoot =
            null;

        _companionTotalText =
            null;

        _companionUsedText =
            null;

        _companionFreeText =
            null;

        _companionPercentText =
            null;

        _companionBarGrid =
            null;

        _companionBarTrack =
            null;

        _companionBarUsed =
            null;

        _companionBarUsedColumn =
            null;

        _companionBarFreeColumn =
            null;
    }

    private void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        RefreshDriveUsage();
    }

    private void RebuildMainView()
    {
        if (_mainRoot is null)
        {
            return;
        }

        _mainRoot.Children.Clear();
        _mainRoot.RowDefinitions.Clear();
        _mainRoot.ColumnDefinitions.Clear();
        _driveRingVisuals.Clear();

        List<string> selectedDrives =
            _settings.SelectedDrives
                .Where(
                    drive =>
                        !string.IsNullOrWhiteSpace(
                            drive))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    4)
                .ToList();

        if (selectedDrives.Count == 0)
        {
            return;
        }

        if (selectedDrives.Count == 1)
        {
            DriveRingVisual visual =
                CreateDriveRing(
                    selectedDrives[0],
                    44,
                    4);

            _mainRoot.Children.Add(
                visual.Root);

            _driveRingVisuals.Add(
                visual);

            ApplyAppearance();

            return;
        }

        if (selectedDrives.Count == 2)
        {
            _mainRoot.ColumnDefinitions.Add(
                new ColumnDefinition());

            _mainRoot.ColumnDefinitions.Add(
                new ColumnDefinition());

            for (int index = 0;
                 index < selectedDrives.Count;
                 index++)
            {
                DriveRingVisual visual =
                    CreateDriveRing(
                        selectedDrives[index],
                        23,
                        2.6);

                Grid.SetColumn(
                    visual.Root,
                    index);

                _mainRoot.Children.Add(
                    visual.Root);

                _driveRingVisuals.Add(
                    visual);
            }

            ApplyAppearance();

            return;
        }

        _mainRoot.RowDefinitions.Add(
            new RowDefinition());

        _mainRoot.RowDefinitions.Add(
            new RowDefinition());

        _mainRoot.ColumnDefinitions.Add(
            new ColumnDefinition());

        _mainRoot.ColumnDefinitions.Add(
            new ColumnDefinition());

        for (int index = 0;
             index < selectedDrives.Count;
             index++)
        {
            DriveRingVisual visual =
                CreateDriveRing(
                    selectedDrives[index],
                    22,
                    2.4);

            Grid.SetRow(
                visual.Root,
                index / 2);

            Grid.SetColumn(
                visual.Root,
                index % 2);

            _mainRoot.Children.Add(
                visual.Root);

            _driveRingVisuals.Add(
                visual);
        }

        ApplyAppearance();
    }

    private DriveRingVisual CreateDriveRing(
        string rootPath,
        double ringSize,
        double strokeThickness)
    {
        double ringScale =
            1.10 *
            Math.Clamp(
                _settings.RingScalePercent,
                50,
                150) /
            100;

        double effectiveStrokeThickness =
            strokeThickness *
            Math.Clamp(
                _settings.RingThicknessPercent,
                50,
                160) /
            100;

        Grid root =
            new()
            {
                Width = ringSize,
                Height = ringSize,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin =
                    new Point(
                        0.5,
                        0.5),
                RenderTransform =
                    new ScaleTransform(
                        ringScale,
                        ringScale),
                Tag = rootPath,
                ToolTip = rootPath
            };

        Ellipse selectionFill =
            new()
            {
                Width =
                    Math.Max(
                        0,
                        ringSize -
                        (effectiveStrokeThickness * 2.6)),
                Height =
                    Math.Max(
                        0,
                        ringSize -
                        (effectiveStrokeThickness * 2.6)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

        Ellipse track =
            new()
            {
                Width = ringSize,
                Height = ringSize,
                StrokeThickness = effectiveStrokeThickness,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        WpfPath arc =
            new()
            {
                Width = ringSize,
                Height = ringSize,
                StrokeThickness = effectiveStrokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        string driveLabel =
            rootPath
                .TrimEnd(
                    IOPath.DirectorySeparatorChar,
                    IOPath.AltDirectorySeparatorChar);

        TextBlock label =
            new()
            {
                Text = driveLabel,
                FontSize =
                    ringSize >= 40
                        ? 11
                        : 7.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

        root.Children.Add(
            selectionFill);

        root.Children.Add(
            track);

        root.Children.Add(
            arc);

        root.Children.Add(
            label);

        root.AddHandler(
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(
                DriveRing_PreviewMouseLeftButtonUp),
            true);

        return new DriveRingVisual
        {
            RootPath = rootPath,
            RingSize = ringSize,
            StrokeThickness = effectiveStrokeThickness,
            Root = root,
            SelectionFill = selectionFill,
            Track = track,
            Arc = arc,
            Label = label
        };
    }

    private void DriveRing_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            sender is not Grid root ||
            root.Tag is not string rootPath)
        {
            return;
        }

        if (!TryGetDriveInfo(
                rootPath,
                out _))
        {
            return;
        }

        e.Handled =
            true;

        if (HasCompanionView &&
            string.Equals(
                _selectedCompanionDriveRoot,
                rootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            CloseCompanion();
            return;
        }

        _selectedCompanionDriveRoot =
            rootPath;

        _companionClosedByUser =
            false;

        ApplyAppearance();
        UpdateCompanionView();

        CompanionViewChanged?.Invoke(
            this,
            EventArgs.Empty);

        Log(
            $"Drive clicked; Root={rootPath}");
    }

    private void RefreshDriveUsage()
    {
        foreach (DriveRingVisual visual in
                 _driveRingVisuals)
        {
            if (!TryGetDriveInfo(
                    visual.RootPath,
                    out DriveInfo drive))
            {
                visual.Arc.Data =
                    null;

                continue;
            }

            double freeFraction =
                drive.TotalSize > 0
                    ? Math.Clamp(
                        (double)drive.AvailableFreeSpace /
                        drive.TotalSize,
                        0,
                        1)
                    : 0;

            double ringFraction =
                string.Equals(
                    _settings.RingFillMode,
                    "Free",
                    StringComparison.OrdinalIgnoreCase)
                    ? freeFraction
                    : 1 -
                      freeFraction;

            visual.Arc.Data =
                CreateArcGeometry(
                    visual.RingSize,
                    visual.StrokeThickness,
                    ringFraction);
        }

        UpdateCompanionView();
    }

    private void BuildCompanionView()
    {
        _companionRoot =
            new Grid
            {
                Width = 116,
                Height = 52,
                Background = Brushes.Transparent
            };

        _companionRoot.RowDefinitions.Add(
            new RowDefinition());

        _companionRoot.RowDefinitions.Add(
            new RowDefinition());

        _companionRoot.RowDefinitions.Add(
            new RowDefinition());

        _companionRoot.ColumnDefinitions.Add(
            new ColumnDefinition());

        _companionRoot.ColumnDefinitions.Add(
            new ColumnDefinition());

        _companionTotalText =
            CreateCompanionValueText();

        _companionUsedText =
            CreateCompanionValueText();

        _companionFreeText =
            CreateCompanionValueText();

        _companionPercentText =
            CreateCompanionValueText();

        _companionTotalText.HorizontalAlignment =
            HorizontalAlignment.Left;

        _companionUsedText.HorizontalAlignment =
            HorizontalAlignment.Left;

        _companionFreeText.HorizontalAlignment =
            HorizontalAlignment.Right;

        _companionPercentText.HorizontalAlignment =
            HorizontalAlignment.Center;

        _companionPercentText.VerticalAlignment =
            VerticalAlignment.Center;

        _companionPercentText.TextAlignment =
            TextAlignment.Center;

        _companionPercentText.FontWeight =
            FontWeights.SemiBold;

        Grid.SetRow(
            _companionTotalText,
            0);

        Grid.SetColumnSpan(
            _companionTotalText,
            2);

        Grid.SetRow(
            _companionUsedText,
            1);

        Grid.SetColumn(
            _companionUsedText,
            0);

        Grid.SetRow(
            _companionFreeText,
            1);

        Grid.SetColumn(
            _companionFreeText,
            1);

        _companionBarGrid =
            new Grid
            {
                Height = 10,
                Margin =
                    new Thickness(
                        0,
                        1,
                        0,
                        1),
                VerticalAlignment = VerticalAlignment.Center
            };

        _companionBarUsedColumn =
            new ColumnDefinition();

        _companionBarFreeColumn =
            new ColumnDefinition();

        _companionBarGrid.ColumnDefinitions.Add(
            _companionBarUsedColumn);

        _companionBarGrid.ColumnDefinitions.Add(
            _companionBarFreeColumn);

        _companionBarTrack =
            new Border
            {
                CornerRadius =
                    new CornerRadius(
                        3.5)
            };

        Grid.SetColumnSpan(
            _companionBarTrack,
            2);

        _companionBarUsed =
            new Border
            {
                CornerRadius =
                    new CornerRadius(
                        3.5)
            };

        Grid.SetColumn(
            _companionBarUsed,
            0);

        _companionBarGrid.Children.Add(
            _companionBarTrack);

        _companionBarGrid.Children.Add(
            _companionBarUsed);

        Grid.SetRow(
            _companionBarGrid,
            2);

        Grid.SetColumnSpan(
            _companionBarGrid,
            2);

        Grid.SetColumnSpan(
            _companionPercentText,
            2);

        _companionBarGrid.Children.Add(
            _companionPercentText);

        _companionRoot.Children.Add(
            _companionTotalText);

        _companionRoot.Children.Add(
            _companionUsedText);

        _companionRoot.Children.Add(
            _companionFreeText);

        _companionRoot.Children.Add(
            _companionBarGrid);

        ApplyCompanionScale();
        ApplyAppearance();
    }

    private static TextBlock CreateCompanionValueText()
    {
        return new TextBlock
        {
            FontSize = 9.2,
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private void ApplyCompanionScale()
    {
        double scale =
            Math.Clamp(
                _settings.CompanionScalePercent,
                50,
                150) /
            100;

        double textSize =
            9.2 *
            scale;

        if (_companionTotalText is not null)
        {
            _companionTotalText.FontSize =
                textSize;
        }

        if (_companionUsedText is not null)
        {
            _companionUsedText.FontSize =
                textSize;
        }

        if (_companionFreeText is not null)
        {
            _companionFreeText.FontSize =
                textSize;
        }

        if (_companionPercentText is not null)
        {
            _companionPercentText.FontSize =
                8.2 *
                scale;
        }

        if (_companionBarGrid is not null)
        {
            _companionBarGrid.Height =
                10 *
                scale;
        }
    }

    private void UpdateCompanionView()
    {
        if (_companionRoot is null ||
            string.IsNullOrWhiteSpace(
                _selectedCompanionDriveRoot) ||
            _companionTotalText is null ||
            _companionUsedText is null ||
            _companionFreeText is null ||
            _companionPercentText is null ||
            _companionBarUsedColumn is null ||
            _companionBarFreeColumn is null)
        {
            return;
        }

        if (!TryGetDriveInfo(
                _selectedCompanionDriveRoot,
                out DriveInfo drive))
        {
            _companionTotalText.Text =
                "T: —";

            _companionUsedText.Text =
                "U: —";

            _companionFreeText.Text =
                "F: —";

            _companionPercentText.Text =
                "—";

            _companionBarUsedColumn.Width =
                new GridLength(
                    0,
                    GridUnitType.Star);

            _companionBarFreeColumn.Width =
                new GridLength(
                    1,
                    GridUnitType.Star);

            _companionRoot.ToolTip =
                $"{_selectedCompanionDriveRoot}\nGesamt: —\nBelegt: —\nFrei: —";

            return;
        }

        long usedBytes =
            Math.Max(
                0,
                drive.TotalSize -
                drive.AvailableFreeSpace);

        double freeFraction =
            drive.TotalSize > 0
                ? Math.Clamp(
                    (double)drive.AvailableFreeSpace /
                    drive.TotalSize,
                    0,
                    1)
                : 0;

        double usedFraction =
            1 -
            freeFraction;

        _companionRoot.ToolTip =
            string.Create(
                CultureInfo.InvariantCulture,
                $"{drive.Name}\nGesamt: {FormatBytes(drive.TotalSize)}\nBelegt: {FormatBytes(usedBytes)} ({usedFraction * 100:0}%)\nFrei: {FormatBytes(drive.AvailableFreeSpace)} ({freeFraction * 100:0}%)");

        _companionTotalText.Text =
            $"T: {FormatBytes(drive.TotalSize)}";

        _companionUsedText.Text =
            $"U: {FormatBytes(usedBytes)}";

        _companionFreeText.Text =
            $"F: {FormatBytes(drive.AvailableFreeSpace)}";

        bool showFree =
            string.Equals(
                _settings.RingFillMode,
                "Free",
                StringComparison.OrdinalIgnoreCase);

        double selectedFraction =
            showFree
                ? freeFraction
                : usedFraction;

        _companionPercentText.Text =
            string.Create(
                CultureInfo.InvariantCulture,
                $"{selectedFraction * 100:0}%");

        _companionBarUsedColumn.Width =
            new GridLength(
                Math.Max(
                    selectedFraction,
                    0.0001),
                GridUnitType.Star);

        _companionBarFreeColumn.Width =
            new GridLength(
                Math.Max(
                    1 -
                    selectedFraction,
                    0.0001),
                GridUnitType.Star);
    }

    private void ApplyAppearance()
    {
        Brush foreground =
            _hostAppearance?.DockTextBrush ??
            Brushes.White;

        Color ringColor =
            ParseRingColor(
                _settings.RingColor,
                Colors.Orange);

        Brush ringBrush =
            new SolidColorBrush(
                ringColor);

        Brush trackBrush =
            foreground.Clone();

        trackBrush.Opacity =
            0.18;

        foreach (DriveRingVisual visual in
                 _driveRingVisuals)
        {
            bool isSelected =
                !string.IsNullOrWhiteSpace(
                    _selectedCompanionDriveRoot) &&
                string.Equals(
                    visual.RootPath,
                    _selectedCompanionDriveRoot,
                    StringComparison.OrdinalIgnoreCase);

            visual.SelectionFill.Fill =
                ringBrush;

            visual.SelectionFill.Opacity =
                isSelected
                    ? 0.42
                    : 0;

            visual.SelectionFill.Visibility =
                isSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            visual.Track.Stroke =
                trackBrush;

            visual.Arc.Stroke =
                ringBrush;

            visual.Label.Foreground =
                foreground;
        }

        if (_companionTotalText is not null)
        {
            _companionTotalText.Foreground =
                foreground;
        }

        if (_companionUsedText is not null)
        {
            _companionUsedText.Foreground =
                foreground;
        }

        if (_companionFreeText is not null)
        {
            _companionFreeText.Foreground =
                foreground;
        }

        if (_companionPercentText is not null)
        {
            _companionPercentText.Foreground =
                foreground;
        }

        if (_companionBarTrack is not null)
        {
            _companionBarTrack.Background =
                trackBrush;
        }

        if (_companionBarUsed is not null)
        {
            _companionBarUsed.Background =
                ringBrush;
        }
    }

    private static Geometry? CreateArcGeometry(
        double size,
        double strokeThickness,
        double fraction)
    {
        fraction =
            Math.Clamp(
                fraction,
                0,
                1);

        if (fraction <= 0)
        {
            return null;
        }

        double radius =
            Math.Max(
                0,
                (size -
                 strokeThickness) /
                2);

        Point center =
            new(
                size / 2,
                size / 2);

        if (fraction >= 0.9999)
        {
            return new EllipseGeometry(
                center,
                radius,
                radius);
        }

        double startAngle =
            -90;

        double endAngle =
            startAngle +
            fraction *
            360;

        Point startPoint =
            PointOnCircle(
                center,
                radius,
                startAngle);

        Point endPoint =
            PointOnCircle(
                center,
                radius,
                endAngle);

        PathFigure figure =
            new()
            {
                StartPoint = startPoint,
                IsClosed = false,
                IsFilled = false
            };

        figure.Segments.Add(
            new ArcSegment
            {
                Point = endPoint,
                Size =
                    new Size(
                        radius,
                        radius),
                RotationAngle = 0,
                IsLargeArc =
                    fraction > 0.5,
                SweepDirection =
                    SweepDirection.Clockwise,
                IsStroked = true
            });

        return new PathGeometry(
            new[]
            {
                figure
            });
    }

    private static Point PointOnCircle(
        Point center,
        double radius,
        double angleDegrees)
    {
        double angleRadians =
            angleDegrees *
            Math.PI /
            180;

        return new Point(
            center.X +
            radius *
            Math.Cos(
                angleRadians),
            center.Y +
            radius *
            Math.Sin(
                angleRadians));
    }

    private static bool TryGetDriveInfo(
        string rootPath,
        out DriveInfo drive)
    {
        DriveInfo? matchingDrive =
            DriveInfo.GetDrives()
                .FirstOrDefault(
                    candidate =>
                        candidate.DriveType !=
                        DriveType.Removable &&
                        candidate.IsReady &&
                        string.Equals(
                            candidate.Name,
                            rootPath,
                            StringComparison.OrdinalIgnoreCase));

        if (matchingDrive is null)
        {
            drive =
                null!;

            return false;
        }

        drive =
            matchingDrive;

        return true;
    }

    private static IReadOnlyList<DriveInfo> GetEligibleDrives()
    {
        return DriveInfo.GetDrives()
            .Where(
                drive =>
                    drive.DriveType !=
                    DriveType.Removable)
            .Where(
                drive =>
                    drive.IsReady)
            .OrderBy(
                drive =>
                    drive.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private DriveUsageSettings LoadSettings()
    {
        string settingsPath =
            GetSettingsPath();

        if (!File.Exists(
                settingsPath))
        {
            return new DriveUsageSettings();
        }

        try
        {
            DriveUsageSettings? loaded =
                JsonSerializer.Deserialize<DriveUsageSettings>(
                    File.ReadAllText(
                        settingsPath));

            DriveUsageSettings settings =
                loaded?.Clone() ??
                new DriveUsageSettings();

            if (Math.Abs(
                    settings.RingScalePercent -
                    110) <
                0.001)
            {
                settings.RingScalePercent =
                    100;
            }

            if (Math.Abs(
                    settings.CompanionScalePercent -
                    150) <
                0.001)
            {
                settings.CompanionScalePercent =
                    100;
            }

            return settings.Clone();
        }
        catch (Exception ex)
        {
            Log(
                $"Settings load failed; Error={ex.Message}");

            return new DriveUsageSettings();
        }
    }

    private void SaveSettings()
    {
        if (_context is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(
                _context.SettingsDirectory);

            File.WriteAllText(
                GetSettingsPath(),
                JsonSerializer.Serialize(
                    _settings.Clone(),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch (Exception ex)
        {
            Log(
                $"Settings save failed; Error={ex.Message}");
        }
    }

    private string GetSettingsPath()
    {
        if (_context is null)
        {
            return string.Empty;
        }

        return IOPath.Combine(
            _context.SettingsDirectory,
            "settings.json");
    }

    private static string FormatBytes(
        long bytes)
    {
        const double kibibyte =
            1024;

        const double mebibyte =
            kibibyte *
            1024;

        const double gibibyte =
            mebibyte *
            1024;

        const double tebibyte =
            gibibyte *
            1024;

        if (bytes >= tebibyte)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / tebibyte:0.##} TB");
        }

        if (bytes >= gibibyte)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / gibibyte:0.##} GB");
        }

        if (bytes >= mebibyte)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / mebibyte:0.##} MB");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{bytes / kibibyte:0.##} KB");
    }

    private static Color ParseRingColor(
        string value,
        Color fallback)
    {
        try
        {
            object? converted =
                ColorConverter.ConvertFromString(
                    value);

            return converted is Color color
                ? color
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void Log(
        string message)
    {
        _context?.Log?.Invoke(
            "Widget.DriveUsage",
            message);
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
}
