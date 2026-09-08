using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlueDock;

namespace GlueDock.DesktopOverlayWidget;

public sealed class DesktopOverlayWidget : IGlueDockWidget
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented =
                true,
            PropertyNameCaseInsensitive =
                true
        };

    private readonly DesktopOverlaySettings _settings =
        new();

    private GlueDockWidgetContext? _context;
    private GlueDockWidgetAppearance? _hostAppearance;
    private DesktopOverlayWindow? _overlayWindow;
    private readonly List<Border> _iconTiles =
        new();

    private string _displayName =
        string.Empty;
    private string _description =
        string.Empty;

    public string DisplayName =>
        _displayName;

    public string DisplayNameKey =>
        "Widget.DesktopOverlay.DisplayName";

    public string Description =>
        _description;

    public string DescriptionKey =>
        "Widget.DesktopOverlay.Description";

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
        false;

    public bool HasCompanionView =>
        false;

    public event EventHandler? CompanionViewChanged
    {
        add
        {
        }

        remove
        {
        }
    }

    public void Initialize(
        GlueDockWidgetContext context)
    {
        _context =
            context;

        Directory.CreateDirectory(
            context.SettingsDirectory);

        Directory.CreateDirectory(
            GetStorageDirectory());

        _settings.CopyFrom(
            LoadSettings());

        context.SubscribeLanguageChanged?.Invoke(
            LanguageChanged);

        RefreshLocalizedMetadata();

        Log(
            $"Initialized; InstanceId={context.InstanceId}; Storage={GetStorageDirectory()}");
    }

    public FrameworkElement CreateView()
    {
        Grid root =
            new()
            {
                Width =
                    52,
                Height =
                    52,
                Background =
                    Brushes.Transparent
            };

        Grid icon =
            new()
            {
                Width =
                    34,
                Height =
                    34,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        for (int index = 0;
             index < 2;
             index++)
        {
            icon.RowDefinitions.Add(
                new RowDefinition());

            icon.ColumnDefinitions.Add(
                new ColumnDefinition());
        }

        _iconTiles.Clear();

        for (int index = 0;
             index < 4;
             index++)
        {
            Border tile =
                new()
                {
                    Margin =
                        new Thickness(
                            1.5),
                    CornerRadius =
                        new CornerRadius(
                            2),
                    BorderThickness =
                        new Thickness(
                            1),
                    BorderBrush =
                        _hostAppearance?.DockTextBrush.Clone() ??
                        Brushes.White,
                    Background =
                        Brushes.Transparent
                };

            Grid.SetRow(
                tile,
                index /
                2);

            Grid.SetColumn(
                tile,
                index %
                2);

            icon.Children.Add(
                tile);

            _iconTiles.Add(
                tile);
        }

        root.Children.Add(
            icon);

        root.AddHandler(
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(
                Root_PreviewMouseLeftButtonUp),
            true);

        return root;
    }

    private void Root_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Left)
        {
            return;
        }

        e.Handled =
            true;

        OpenOverlay();
    }

    public FrameworkElement? CreateCompanionView()
    {
        return null;
    }

    public void CloseCompanion()
    {
    }

    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _hostAppearance =
            appearance;

        foreach (Border tile in _iconTiles)
        {
            tile.BorderBrush =
                appearance.DockTextBrush.Clone();
        }

        if (string.IsNullOrWhiteSpace(
                _settings.ThemeName))
        {
            _settings.ThemeName =
                appearance.ThemeName;

            _settings.Opacity =
                Math.Clamp(
                    appearance.Opacity,
                    0.10,
                    1.00);

            _settings.BlurRadius =
                Math.Clamp(
                    appearance.BlurRadius,
                    0,
                    100);

            SaveSettings();
        }

        _overlayWindow?.ApplyHostAppearance(
            appearance);
    }

    public IReadOnlyList<GlueDockWidgetSettingsSection> CreateSettingsSections()
    {
        GlueDockWidgetAppearance appearance =
            _hostAppearance ??
            CreateFallbackAppearance();

        DesktopOverlaySettingsControl control =
            new(
                _settings.Clone(),
                appearance,
                CommitSettings,
                OpenStorageFolder,
                Localize,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged);

        return new[]
        {
            new GlueDockWidgetSettingsSection(
                "DesktopOverlay",
                "Settings.Navigation.WidgetDesktopOverlay",
                300,
                "General",
                "Settings.Tab.General",
                0,
                control,
                control.Dispose)
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

        GlueDockWidgetAppearance appearance =
            _hostAppearance ??
            CreateFallbackAppearance();

        DesktopOverlaySettingsControl control =
            new(
                _settings.Clone(),
                appearance,
                CommitSettings,
                OpenStorageFolder,
                Localize,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged)
            {
                Owner =
                    owner,
                Title =
                    Localize(
                        "Widget.DesktopOverlay.Settings.Title"),
                Width =
                    DockDialogTheme.DialogWidth,
                MinWidth =
                    DockDialogTheme.DialogMinWidth,
                SizeToContent =
                    System.Windows.SizeToContent.Height,
                ShowInTaskbar =
                    false,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        try
        {
            control.ShowDialog();
        }
        finally
        {
            control.Dispose();
        }
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

    private void OpenOverlay()
    {
        if (_context is null)
        {
            return;
        }

        if (_overlayWindow is not null)
        {
            if (_overlayWindow.IsVisible)
            {
                _overlayWindow.Close();

                return;
            }

            _overlayWindow.Show();

            return;
        }

        GlueDockWidgetAppearance appearance =
            _hostAppearance ??
            CreateFallbackAppearance();

        DesktopOverlayWindow window =
            new(
                _settings,
                GetStorageDirectory(),
                appearance,
                settings =>
                {
                    _settings.CopyFrom(
                        settings);

                    SaveSettings();
                },
                Localize,
                message =>
                    Log(
                        message),
                OpenOverlaySettings,
                _context.SubscribeLanguageChanged,
                _context.UnsubscribeLanguageChanged);

        _overlayWindow =
            window;

        window.Closed +=
            (_, _) =>
            {
                if (ReferenceEquals(
                        _overlayWindow,
                        window))
                {
                    _overlayWindow =
                        null;
                }
            };

        window.Show();

        Log(
            "Overlay opened.");
    }

    private void OpenOverlaySettings()
    {
        _context?.OpenSettingsSection?.Invoke(
            "General");
    }

    private void CommitSettings(
        DesktopOverlaySettings settings)
    {
        double? left =
            _settings.WindowLeft;

        double? top =
            _settings.WindowTop;

        double width =
            _settings.WindowWidth;

        double height =
            _settings.WindowHeight;

        Dictionary<string, DesktopOverlayGridPosition> positions =
            _settings.Positions;

        _settings.CopyFrom(
            settings);

        _settings.WindowLeft =
            left;

        _settings.WindowTop =
            top;

        _settings.WindowWidth =
            width;

        _settings.WindowHeight =
            height;

        if (_settings.Positions.Count == 0 &&
            positions.Count > 0)
        {
            _settings.Positions =
                positions;
        }

        SaveSettings();

        if (_overlayWindow is not null)
        {
            _overlayWindow.ApplySettings(
                _settings,
                _hostAppearance ??
                CreateFallbackAppearance());
        }

        Log(
            $"Settings changed; Theme={_settings.ThemeName}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; BorderEnabled={_settings.BorderEnabled?.ToString() ?? "null"}; BorderColor={_settings.BorderColor ?? "<null>"}; IconSize={_settings.IconSizeMode}; FilePreviews={_settings.ShowFilePreviews}; ZOrder={_settings.ZOrderMode}; AutoArrange={_settings.AutoArrange}; Sort={_settings.SortMode}");
    }

    private void OpenStorageFolder()
    {
        try
        {
            string directory =
                GetStorageDirectory();

            Directory.CreateDirectory(
                directory);

            Process.Start(
                new ProcessStartInfo(
                    "explorer.exe",
                    $"\"{directory}\"")
                {
                    UseShellExecute =
                        true
                });
        }
        catch
        {
        }
    }

    private DesktopOverlaySettings LoadSettings()
    {
        try
        {
            string path =
                GetSettingsPath();

            if (!File.Exists(
                    path))
            {
                return new DesktopOverlaySettings();
            }

            DesktopOverlaySettings? loaded =
                JsonSerializer.Deserialize<DesktopOverlaySettings>(
                    File.ReadAllText(
                        path),
                    JsonOptions);

            if (loaded is null)
            {
                return new DesktopOverlaySettings();
            }

            loaded.Positions ??=
                new Dictionary<string, DesktopOverlayGridPosition>(
                    StringComparer.OrdinalIgnoreCase);

            return loaded;
        }
        catch
        {
            return new DesktopOverlaySettings();
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
                    _settings,
                    JsonOptions));
        }
        catch
        {
        }
    }

    private string GetSettingsPath()
    {
        return Path.Combine(
            _context?.SettingsDirectory ??
            AppContext.BaseDirectory,
            "settings.json");
    }

    private string GetStorageDirectory()
    {
        return Path.Combine(
            _context?.SettingsDirectory ??
            AppContext.BaseDirectory,
            "DesktopItems");
    }

    private string Localize(
        string key)
    {
        string? value =
            _context?.Localize?.Invoke(
                key);

        return string.IsNullOrWhiteSpace(
                   value)
            ? key
            : value;
    }

    private void RefreshLocalizedMetadata()
    {
        _displayName =
            Localize(
                DisplayNameKey);

        _description =
            Localize(
                DescriptionKey);
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        RefreshLocalizedMetadata();
    }

    private void Log(
        string message)
    {
        _context?.Log?.Invoke(
            "Widget.DesktopOverlay",
            message);
    }

    private static GlueDockWidgetAppearance CreateFallbackAppearance()
    {
        return new GlueDockWidgetAppearance
        {
            ThemeName =
                "Default",
            DockBackgroundBrush =
                new SolidColorBrush(
                    Color.FromRgb(
                        0x12,
                        0x16,
                        0x1C)),
            DockItemBackgroundBrush =
                new SolidColorBrush(
                    Color.FromArgb(
                        0x22,
                        0xFF,
                        0xFF,
                        0xFF)),
            DockItemBorderBrush =
                new SolidColorBrush(
                    Color.FromArgb(
                        0x44,
                        0xFF,
                        0xFF,
                        0xFF)),
            DockTextBrush =
                Brushes.White,
            DockBorderEnabled =
                true,
            DockBorderColor =
                Colors.White,
            Opacity =
                0.85,
            BlurRadius =
                45,
            GlassSurfaceEnabled =
                false,
            GlassTopColor =
                Colors.Transparent,
            GlassBottomColor =
                Colors.Transparent,
            GlassHighlightColor =
                Colors.Transparent,
            GlassCornerRadius =
                20,
            GlassGradientReferenceHeight =
                88,
            AvailableThemes =
                Array.Empty<GlueDockWidgetThemeAppearance>()
        };
    }

    public void Dispose()
    {
        if (_context is not null)
        {
            _context.UnsubscribeLanguageChanged?.Invoke(
                LanguageChanged);
        }

        if (_overlayWindow is not null)
        {
            DesktopOverlayWindow window =
                _overlayWindow;

            _overlayWindow =
                null;

            if (window.IsLoaded)
            {
                window.Close();
            }
            else
            {
                window.Dispose();
            }
        }
    }
}
