using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlueDock;

namespace GlueNotes;

public sealed class GlueNotesWidget : IGlueDockWidget
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented =
                true,
            PropertyNameCaseInsensitive =
                true
        };

    private readonly GlueNotesSettings _settings =
        new();

    private readonly LanguageService _language =
        new();

    private GlueDockWidgetContext? _context;
    private GlueDockWidgetAppearance? _hostAppearance;
    private NoteRepository? _repository;
    private MainWindow? _editorWindow;
    private FrameworkElement? _view;
    private readonly List<Border> _iconLines =
        new();

    public string DisplayName =>
        "GlueNotes";

    public string DisplayNameKey =>
        string.Empty;

    public string Description =>
        _language["Widget.Description"];

    public string DescriptionKey =>
        string.Empty;

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

        _settings.CopyFrom(
            LoadSettings());

        _language.SetLanguageCode(
            context.GetLanguageCode?.Invoke());

        _repository =
            new NoteRepository(
                Path.Combine(
                    context.SettingsDirectory,
                    "Notes"));

        context.SubscribeLanguageChanged?.Invoke(
            LanguageChanged);

        Log(
            $"Initialized; InstanceId={context.InstanceId}; Notes={_repository.RootDirectory}");
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
                    Brushes.Transparent,
                ToolTip =
                    _language["Widget.ToolTip"]
            };

        Border note =
            new()
            {
                Width =
                    30,
                Height =
                    34,
                CornerRadius =
                    new CornerRadius(
                        3),
                BorderThickness =
                    new Thickness(
                        1.5),
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Background =
                    Brushes.Transparent
            };

        StackPanel lines =
            new()
            {
                Margin =
                    new Thickness(
                        6,
                        8,
                        6,
                        6)
            };

        _iconLines.Clear();

        for (int index = 0;
             index < 3;
             index++)
        {
            Border line =
                new()
                {
                    Height =
                        1.5,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            index == 2
                                ? 6
                                : 0,
                            5),
                    CornerRadius =
                        new CornerRadius(
                            1)
                };

            lines.Children.Add(
                line);

            _iconLines.Add(
                line);
        }

        note.Child =
            lines;

        root.Children.Add(
            note);

        root.AddHandler(
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(
                Root_PreviewMouseLeftButtonUp),
            true);

        _view =
            root;

        ApplyViewAppearance(
            note);

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

        OpenEditor();
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

            _settings.BorderEnabled =
                appearance.DockBorderEnabled;

            _settings.BorderColor =
                appearance.DockBorderColor.ToString();

            _settings.SettingsVersion =
                GlueNotesSettings.CurrentVersion;

            SaveSettings();
        }

        if (_view is Grid root &&
            root.Children
                .OfType<Border>()
                .FirstOrDefault() is Border note)
        {
            ApplyViewAppearance(
                note);
        }

        _editorWindow?.ApplyHostAppearance(
            CreateEffectiveAppearance());
    }

    public IReadOnlyList<GlueDockWidgetSettingsSection> CreateSettingsSections()
    {
        GlueDockWidgetAppearance appearance =
            _hostAppearance ??
            CreateFallbackAppearance();

        GlueNotesSettingsControl control =
            new(
                _settings,
                appearance,
                _language,
                CommitSettings,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged,
                _context?.GetLanguageCode,
                _context?.Localize);

        return new[]
        {
            new GlueDockWidgetSettingsSection(
                "GlueNotes",
                "GlueNotes",
                400,
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
        _context?.OpenSettingsSection?.Invoke(
            "General");
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

    private void OpenEditor()
    {
        if (_editorWindow is not null)
        {
            if (_editorWindow.WindowState ==
                WindowState.Minimized)
            {
                _editorWindow.WindowState =
                    WindowState.Normal;
            }

            _editorWindow.Activate();
            return;
        }

        if (_repository is null)
        {
            return;
        }

        MainWindow window;

        if (_hostAppearance is not null)
        {
            window =
                new MainWindow(
                    _repository,
                    _language,
                    _settings,
                    CreateEffectiveAppearance(),
                    () =>
                        _context?.OpenSettingsSection?.Invoke(
                            "General"),
                    _context?.Localize);
        }
        else
        {
            window =
                new MainWindow(
                    _repository,
                    _language,
                    _settings,
                    null);
        }

        _editorWindow =
            window;

        window.Closed +=
            (_, _) =>
            {
                if (ReferenceEquals(
                        _editorWindow,
                        window))
                {
                    _editorWindow =
                        null;
                }
            };

        window.Show();

        Log(
            "Editor opened.");
    }

    private void CommitSettings(
        GlueNotesSettings settings)
    {
        _settings.CopyFrom(
            settings);

        SaveSettings();

        if (_hostAppearance is not null)
        {
            _editorWindow?.ApplySettings(
                _settings,
                CreateEffectiveAppearance());
        }

        Log(
            $"Settings changed; Theme={_settings.ThemeName}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0}; BorderEnabled={_settings.BorderEnabled?.ToString() ?? "null"}; BorderColor={_settings.BorderColor ?? "<null>"}; DefaultFontSize={_settings.DefaultFontSize:0.##}; AutoSaveDelayMilliseconds={_settings.AutoSaveDelayMilliseconds}; MaxImageWidth={_settings.MaxImageWidth:0.##}");
    }

    private GlueNotesSettings LoadSettings()
    {
        try
        {
            string path =
                GetSettingsPath();

            if (!File.Exists(
                    path))
            {
                return new GlueNotesSettings();
            }

            GlueNotesSettings? loaded =
                JsonSerializer.Deserialize<GlueNotesSettings>(
                    File.ReadAllText(
                        path),
                    JsonOptions);

            if (loaded is null)
            {
                return new GlueNotesSettings();
            }

            if (!new double[]
                {
                    12,
                    14,
                    16,
                    18,
                    22,
                    28,
                    36
                }
                .Contains(
                    loaded.DefaultFontSize))
            {
                loaded.DefaultFontSize =
                    14;
            }

            loaded.Opacity =
                Math.Clamp(
                    loaded.Opacity,
                    0.10,
                    1.00);

            loaded.BlurRadius =
                Math.Clamp(
                    loaded.BlurRadius,
                    0,
                    100);

            if (loaded.AutoSaveDelayMilliseconds <=
                0)
            {
                loaded.AutoSaveDelayMilliseconds =
                    650;
            }

            if (!double.IsFinite(
                    loaded.MaxImageWidth) ||
                loaded.MaxImageWidth <=
                0)
            {
                loaded.MaxImageWidth =
                    420;
            }

            loaded.SettingsVersion =
                GlueNotesSettings.CurrentVersion;

            return loaded;
        }
        catch
        {
            return new GlueNotesSettings();
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

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        _language.SetLanguageCode(
            _context?.GetLanguageCode?.Invoke());

        if (_view is not null)
        {
            _view.ToolTip =
                _language["Widget.ToolTip"];
        }

        _editorWindow?.SetLanguageCode(
            _context?.GetLanguageCode?.Invoke());
    }

    private void ApplyViewAppearance(
        Border note)
    {
        Brush textBrush =
            _hostAppearance?.DockTextBrush.Clone() ??
            Brushes.White;

        note.BorderBrush =
            textBrush;

        foreach (Border line in
                 _iconLines)
        {
            line.Background =
                textBrush.Clone();
        }
    }

    private GlueDockWidgetAppearance CreateEffectiveAppearance()
    {
        GlueDockWidgetAppearance hostAppearance =
            _hostAppearance ??
            CreateFallbackAppearance();

        GlueDockWidgetThemeAppearance? selectedTheme =
            hostAppearance.AvailableThemes
                .FirstOrDefault(
                    theme =>
                        string.Equals(
                            theme.ThemeName,
                            _settings.ThemeName,
                            StringComparison.OrdinalIgnoreCase));

        Brush backgroundBrush =
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

        return new GlueDockWidgetAppearance
        {
            ThemeName =
                selectedTheme?.ThemeName ??
                hostAppearance.ThemeName,
            DockBackgroundBrush =
                backgroundBrush,
            DockItemBackgroundBrush =
                itemBackgroundBrush,
            DockItemBorderBrush =
                itemBorderBrush,
            DockTextBrush =
                textBrush,
            DockBorderEnabled =
                _settings.BorderEnabled ??
                hostAppearance.DockBorderEnabled,
            DockBorderColor =
                GetEffectiveBorderColor(
                    hostAppearance),
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
                selectedTheme?.GlassSurfaceEnabled ??
                hostAppearance.GlassSurfaceEnabled,
            GlassTopColor =
                selectedTheme?.GlassTopColor ??
                hostAppearance.GlassTopColor,
            GlassBottomColor =
                selectedTheme?.GlassBottomColor ??
                hostAppearance.GlassBottomColor,
            GlassHighlightColor =
                selectedTheme?.GlassHighlightColor ??
                hostAppearance.GlassHighlightColor,
            GlassCornerRadius =
                selectedTheme?.GlassCornerRadius ??
                hostAppearance.GlassCornerRadius,
            GlassGradientReferenceHeight =
                selectedTheme?.GlassGradientReferenceHeight ??
                hostAppearance.GlassGradientReferenceHeight,
            AvailableThemes =
                hostAppearance.AvailableThemes
        };
    }

    private Color GetEffectiveBorderColor(
        GlueDockWidgetAppearance appearance)
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
            appearance.DockBorderColor;
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

    private static GlueNotesAppearance CreateEditorAppearance(
        GlueDockWidgetAppearance appearance)
    {
        Brush textBrush =
            appearance.DockTextBrush.Clone();

        Brush mutedTextBrush =
            appearance.DockTextBrush.Clone();

        mutedTextBrush.Opacity =
            0.65;

        return new GlueNotesAppearance
        {
            WindowBrush =
                appearance.DockBackgroundBrush.Clone(),
            SurfaceBrush =
                appearance.DockItemBackgroundBrush.Clone(),
            SurfaceHoverBrush =
                appearance.DockItemBackgroundBrush.Clone(),
            BorderBrush =
                appearance.DockItemBorderBrush.Clone(),
            TextBrush =
                textBrush,
            MutedTextBrush =
                mutedTextBrush,
            AccentBrush =
                appearance.DockItemBorderBrush.Clone(),
            SelectionBrush =
                appearance.DockItemBackgroundBrush.Clone()
        };
    }

    private void Log(
        string message)
    {
        _context?.Log?.Invoke(
            "Widget.GlueNotes",
            message);
    }

    public void Dispose()
    {
        if (_context is not null)
        {
            _context.UnsubscribeLanguageChanged?.Invoke(
                LanguageChanged);
        }

        if (_view is Grid root)
        {
            root.RemoveHandler(
                UIElement.PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(
                    Root_PreviewMouseLeftButtonUp));
        }

        if (_editorWindow is not null)
        {
            MainWindow window =
                _editorWindow;

            _editorWindow =
                null;

            window.Close();
        }
    }
}
