using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlueDock;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using WinForms = System.Windows.Forms;

namespace GlueNotes;

public sealed class GlueNotesSettingsControl : DockSettingsSectionControl, IDisposable
{
    private sealed record Option(
        string Id,
        string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private readonly GlueNotesSettings _settings;
    private readonly GlueDockWidgetAppearance _appearance;
    private readonly LanguageService _language;
    private readonly Action<GlueNotesSettings> _commit;
    private readonly Action<EventHandler>? _unsubscribeLanguageChanged;
    private readonly Func<string>? _getLanguageCode;
    private readonly Func<string, string>? _hostLocalize;

    private readonly TextBlock _themeLabel = new();
    private readonly ComboBox _themeComboBox = new();
    private readonly TextBlock _opacityLabel = new();
    private readonly Slider _opacitySlider = new();
    private readonly TextBlock _opacityValue = new();
    private readonly TextBlock _blurLabel = new();
    private readonly Slider _blurSlider = new();
    private readonly TextBlock _blurValue = new();
    private readonly CheckBox _borderEnabledCheckBox = new();
    private readonly TextBlock _borderColorLabel = new();
    private readonly Border _borderColorPreview = new();
    private readonly Button _borderColorButton = new();
    private readonly TextBlock _defaultFontSizeLabel = new();
    private readonly ComboBox _defaultFontSizeComboBox = new();
    private readonly TextBlock _autoSaveDelayLabel = new();
    private readonly TextBox _autoSaveDelayTextBox = new();
    private readonly TextBlock _maxImageWidthLabel = new();
    private readonly TextBox _maxImageWidthTextBox = new();
    private readonly Button _resetButton = new();

    private bool _initializing = true;
    private bool _disposed;

    public GlueNotesSettingsControl(
        GlueNotesSettings settings,
        GlueDockWidgetAppearance appearance,
        LanguageService language,
        Action<GlueNotesSettings> commit,
        Action<EventHandler>? subscribeLanguageChanged,
        Action<EventHandler>? unsubscribeLanguageChanged,
        Func<string>? getLanguageCode,
        Func<string, string>? hostLocalize)
    {
        _settings =
            settings.Clone();

        _appearance =
            appearance;

        _language =
            language;

        _commit =
            commit;

        _unsubscribeLanguageChanged =
            unsubscribeLanguageChanged;

        _getLanguageCode =
            getLanguageCode;

        _hostLocalize =
            hostLocalize;

        Background =
            Brushes.Transparent;

        Content =
            BuildContent();

        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                appearance);

        DockDialogThemeService.ApplyComboBox(
            _themeComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            _defaultFontSizeComboBox,
            palette);

        _borderColorPreview.BorderBrush =
            palette.ControlBorderBrush;

        PopulateThemeOptions();
        LoadValues();
        ApplyLanguage();

        subscribeLanguageChanged?.Invoke(
            LanguageChanged);

        _initializing =
            false;
    }

    private FrameworkElement BuildContent()
    {
        StackPanel panel =
            new()
            {
                Margin =
                    new Thickness(
                        0)
            };

        panel.Children.Add(
            CreateRow(
                _themeLabel,
                _themeComboBox));

        _opacitySlider.Minimum = 10;
        _opacitySlider.Maximum = 100;
        _opacitySlider.Width =
            DockDialogTheme.SliderWidth;
        _opacitySlider.TickFrequency = 1;
        _opacitySlider.IsSnapToTickEnabled = true;

        panel.Children.Add(
            CreateSliderRow(
                _opacityLabel,
                _opacitySlider,
                _opacityValue));

        _blurSlider.Minimum = 0;
        _blurSlider.Maximum = 100;
        _blurSlider.Width =
            DockDialogTheme.SliderWidth;
        _blurSlider.TickFrequency = 1;
        _blurSlider.IsSnapToTickEnabled = true;

        panel.Children.Add(
            CreateSliderRow(
                _blurLabel,
                _blurSlider,
                _blurValue));

        _borderEnabledCheckBox.Margin =
            new Thickness(
                0,
                2,
                0,
                DockDialogTheme.StandardRowBottomMargin);

        panel.Children.Add(
            _borderEnabledCheckBox);

        StackPanel borderColorControls =
            new()
            {
                Orientation =
                    Orientation.Horizontal
            };

        _borderColorPreview.Width =
            DockDialogTheme.ColorPreviewWidth;

        _borderColorPreview.Height =
            DockDialogTheme.ColorPreviewHeight;

        _borderColorPreview.CornerRadius =
            DockDialogTheme.StandardCornerRadius;

        _borderColorPreview.BorderThickness =
            new Thickness(
                1);

        _borderColorPreview.VerticalAlignment =
            VerticalAlignment.Center;

        _borderColorButton.Width =
            DockDialogTheme.ColorButtonWidth;

        _borderColorButton.Height =
            DockDialogTheme.StandardControlHeight;

        _borderColorButton.Margin =
            new Thickness(
                10,
                0,
                0,
                0);

        borderColorControls.Children.Add(
            _borderColorPreview);

        borderColorControls.Children.Add(
            _borderColorButton);

        panel.Children.Add(
            CreateRow(
                _borderColorLabel,
                borderColorControls));

        _defaultFontSizeComboBox.Width =
            112;

        _defaultFontSizeComboBox.HorizontalAlignment =
            HorizontalAlignment.Left;

        _defaultFontSizeComboBox.ItemsSource =
            new double[]
            {
                12,
                14,
                16,
                18,
                22,
                28,
                36
            };

        panel.Children.Add(
            CreateRow(
                _defaultFontSizeLabel,
                _defaultFontSizeComboBox));

        _autoSaveDelayTextBox.Width =
            112;

        _autoSaveDelayTextBox.HorizontalAlignment =
            HorizontalAlignment.Left;

        panel.Children.Add(
            CreateRow(
                _autoSaveDelayLabel,
                _autoSaveDelayTextBox));

        _maxImageWidthTextBox.Width =
            112;

        _maxImageWidthTextBox.HorizontalAlignment =
            HorizontalAlignment.Left;

        panel.Children.Add(
            CreateRow(
                _maxImageWidthLabel,
                _maxImageWidthTextBox));

        _resetButton.Margin =
            new Thickness(
                0,
                8,
                0,
                0);

        panel.Children.Add(
            _resetButton);

        _themeComboBox.SelectionChanged +=
            ControlChanged;

        _opacitySlider.ValueChanged +=
            ControlChanged;

        _blurSlider.ValueChanged +=
            ControlChanged;

        _borderEnabledCheckBox.Checked +=
            ControlChanged;

        _borderEnabledCheckBox.Unchecked +=
            ControlChanged;

        _borderColorButton.Click +=
            BorderColorButton_Click;

        _defaultFontSizeComboBox.SelectionChanged +=
            ControlChanged;

        _autoSaveDelayTextBox.LostKeyboardFocus +=
            NumericTextBox_LostKeyboardFocus;

        _maxImageWidthTextBox.LostKeyboardFocus +=
            NumericTextBox_LostKeyboardFocus;

        _resetButton.Click +=
            ResetButton_Click;

        return panel;
    }

    private static Grid CreateRow(
        TextBlock label,
        FrameworkElement control)
    {
        Grid grid =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        DockDialogTheme.StandardRowBottomMargin)
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        label.VerticalAlignment =
            VerticalAlignment.Center;

        control.MinHeight =
            DockDialogTheme.StandardControlHeight;

        Grid.SetColumn(
            label,
            0);

        Grid.SetColumn(
            control,
            1);

        grid.Children.Add(
            label);

        grid.Children.Add(
            control);

        return grid;
    }

    private static Grid CreateSliderRow(
        TextBlock label,
        Slider slider,
        TextBlock value)
    {
        Grid grid =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        DockDialogTheme.StandardRowBottomMargin)
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.SliderColumnWidth
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        58)
            });

        label.VerticalAlignment =
            VerticalAlignment.Center;

        slider.VerticalAlignment =
            VerticalAlignment.Center;

        value.VerticalAlignment =
            VerticalAlignment.Center;

        value.Margin =
            new Thickness(
                10,
                0,
                0,
                0);

        Grid.SetColumn(
            label,
            0);

        Grid.SetColumn(
            slider,
            1);

        Grid.SetColumn(
            value,
            2);

        grid.Children.Add(
            label);

        grid.Children.Add(
            slider);

        grid.Children.Add(
            value);

        return grid;
    }

    private void PopulateThemeOptions()
    {
        List<Option> options =
            _appearance.AvailableThemes
                .Select(
                    theme =>
                        new Option(
                            theme.ThemeName,
                            theme.DisplayName))
                .ToList();

        if (options.Count == 0)
        {
            options.Add(
                new Option(
                    _appearance.ThemeName,
                    _appearance.ThemeName));
        }

        _themeComboBox.ItemsSource =
            options;
    }

    private void LoadValues()
    {
        SelectOption(
            _themeComboBox,
            string.IsNullOrWhiteSpace(
                _settings.ThemeName)
                ? _appearance.ThemeName
                : _settings.ThemeName);

        _opacitySlider.Value =
            Math.Clamp(
                _settings.Opacity * 100.0,
                10,
                100);

        _blurSlider.Value =
            Math.Clamp(
                _settings.BlurRadius,
                0,
                100);

        _borderEnabledCheckBox.IsChecked =
            _settings.BorderEnabled ??
            _appearance.DockBorderEnabled;

        _defaultFontSizeComboBox.SelectedItem =
            _defaultFontSizeComboBox.Items
                .Cast<double>()
                .Contains(
                    _settings.DefaultFontSize)
                ? _settings.DefaultFontSize
                : 14d;

        _autoSaveDelayTextBox.Text =
            _settings.AutoSaveDelayMilliseconds.ToString(
                CultureInfo.InvariantCulture);

        _maxImageWidthTextBox.Text =
            _settings.MaxImageWidth.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

        UpdateValueTexts();
    }

    private static void SelectOption(
        ComboBox comboBox,
        string id)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is Option option &&
                string.Equals(
                    option.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem =
                    option;

                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex =
                0;
        }
    }

    private void ControlChanged(
        object sender,
        RoutedEventArgs e)
    {
        PublishSettings();
    }

    private void ControlChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        PublishSettings();
    }

    private void ControlChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        PublishSettings();
    }

    private void NumericTextBox_LostKeyboardFocus(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        PublishSettings();
    }

    private void PublishSettings()
    {
        UpdateValueTexts();

        if (_initializing)
        {
            return;
        }

        if (!TryReadPositiveInt(
                _autoSaveDelayTextBox.Text,
                out int autoSaveDelayMilliseconds) ||
            !TryReadPositiveDouble(
                _maxImageWidthTextBox.Text,
                out double maxImageWidth))
        {
            LoadNumericValues();
            return;
        }

        _settings.SettingsVersion =
            GlueNotesSettings.CurrentVersion;

        _settings.ThemeName =
            (_themeComboBox.SelectedItem as Option)?.Id ??
            _appearance.ThemeName;

        _settings.Opacity =
            Math.Clamp(
                _opacitySlider.Value / 100.0,
                0.10,
                1.00);

        _settings.BlurRadius =
            Math.Clamp(
                _blurSlider.Value,
                0,
                100);

        _settings.BorderEnabled =
            _borderEnabledCheckBox.IsChecked ==
            true;

        if (string.IsNullOrWhiteSpace(
                _settings.BorderColor))
        {
            _settings.BorderColor =
                _appearance.DockBorderColor.ToString();
        }

        _settings.DefaultFontSize =
            _defaultFontSizeComboBox.SelectedItem is double fontSize
                ? fontSize
                : 14d;

        _settings.AutoSaveDelayMilliseconds =
            autoSaveDelayMilliseconds;

        _settings.MaxImageWidth =
            maxImageWidth;

        _commit(
            _settings.Clone());
    }

    private void LoadNumericValues()
    {
        _autoSaveDelayTextBox.Text =
            _settings.AutoSaveDelayMilliseconds.ToString(
                CultureInfo.InvariantCulture);

        _maxImageWidthTextBox.Text =
            _settings.MaxImageWidth.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
    }

    private static bool TryReadPositiveInt(
        string text,
        out int value)
    {
        return
            int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value) &&
            value > 0;
    }

    private static bool TryReadPositiveDouble(
        string text,
        out double value)
    {
        return
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            double.IsFinite(
                value) &&
            value > 0;
    }

    private void BorderColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color current =
            GetEffectiveBorderColor();

        using WinForms.ColorDialog dialog =
            new()
            {
                Color =
                    System.Drawing.Color.FromArgb(
                        current.A,
                        current.R,
                        current.G,
                        current.B),
                FullOpen =
                    true
            };

        if (dialog.ShowDialog() !=
            WinForms.DialogResult.OK)
        {
            return;
        }

        _settings.BorderColor =
            Color.FromArgb(
                    dialog.Color.A,
                    dialog.Color.R,
                    dialog.Color.G,
                    dialog.Color.B)
                .ToString();

        UpdateBorderColorPreview();
        PublishSettings();
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
            _appearance.DockBorderColor;
    }

    private void UpdateBorderColorPreview()
    {
        Color color =
            GetEffectiveBorderColor();

        _borderColorPreview.Background =
            new SolidColorBrush(
                color);

        _borderColorButton.IsEnabled =
            _borderEnabledCheckBox.IsChecked ==
            true;
    }

    private void UpdateValueTexts()
    {
        _opacityValue.Text =
            $"{_opacitySlider.Value:0}%";

        _blurValue.Text =
            $"{_blurSlider.Value:0}";

        UpdateBorderColorPreview();
    }

    private void ResetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _initializing =
            true;

        _settings.SettingsVersion =
            GlueNotesSettings.CurrentVersion;

        _settings.ThemeName =
            _appearance.ThemeName;

        _settings.Opacity =
            Math.Clamp(
                _appearance.Opacity,
                0.10,
                1.00);

        _settings.BlurRadius =
            Math.Clamp(
                _appearance.BlurRadius,
                0,
                100);

        _settings.BorderEnabled =
            _appearance.DockBorderEnabled;

        _settings.BorderColor =
            _appearance.DockBorderColor.ToString();

        _settings.DefaultFontSize =
            14;

        _settings.AutoSaveDelayMilliseconds =
            650;

        _settings.MaxImageWidth =
            420;

        LoadValues();

        _initializing =
            false;

        PublishSettings();
    }

    private void ApplyLanguage()
    {
        _language.SetLanguageCode(
            _getLanguageCode?.Invoke());

        _themeLabel.Text =
            HostLocalize(
                "Settings.Theme",
                _language["Settings.Theme"]);

        _opacityLabel.Text =
            HostLocalize(
                "Settings.Opacity",
                _language["Settings.Opacity"]);

        _blurLabel.Text =
            HostLocalize(
                "Settings.Blur",
                _language["Settings.Blur"]);

        _borderEnabledCheckBox.Content =
            HostLocalize(
                "Settings.ShowDockBorder",
                _language["Settings.ShowBorder"]);

        _borderColorLabel.Text =
            HostLocalize(
                "Settings.DockBorderColor",
                _language["Settings.BorderColor"]);

        _borderColorButton.Content =
            HostLocalize(
                "Settings.ChooseColor",
                _language["Settings.ChooseColor"]);

        _defaultFontSizeLabel.Text =
            _language["Settings.DefaultFontSize"];

        _autoSaveDelayLabel.Text =
            _language["Settings.AutoSaveDelay"];

        _maxImageWidthLabel.Text =
            _language["Settings.MaxImageWidth"];

        _resetButton.Content =
            _language["Settings.Reset"];
    }

    private string HostLocalize(
        string key,
        string fallback)
    {
        if (_hostLocalize is null)
        {
            return fallback;
        }

        string value =
            _hostLocalize(
                key);

        return
            string.IsNullOrWhiteSpace(
                value) ||
            string.Equals(
                value,
                key,
                StringComparison.Ordinal)
                ? fallback
                : value;
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        ApplyLanguage();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _themeComboBox.SelectionChanged -=
            ControlChanged;

        _opacitySlider.ValueChanged -=
            ControlChanged;

        _blurSlider.ValueChanged -=
            ControlChanged;

        _borderEnabledCheckBox.Checked -=
            ControlChanged;

        _borderEnabledCheckBox.Unchecked -=
            ControlChanged;

        _borderColorButton.Click -=
            BorderColorButton_Click;

        _defaultFontSizeComboBox.SelectionChanged -=
            ControlChanged;

        _autoSaveDelayTextBox.LostKeyboardFocus -=
            NumericTextBox_LostKeyboardFocus;

        _maxImageWidthTextBox.LostKeyboardFocus -=
            NumericTextBox_LostKeyboardFocus;

        _resetButton.Click -=
            ResetButton_Click;

        _unsubscribeLanguageChanged?.Invoke(
            LanguageChanged);
    }
}
