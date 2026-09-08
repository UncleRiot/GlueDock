using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlueDock;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using WinForms = System.Windows.Forms;

namespace GlueDock.DesktopOverlayWidget;

public sealed class DesktopOverlaySettingsControl : DockSettingsSectionControl, IDisposable
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

    private readonly GlueDockWidgetAppearance _appearance;
    private readonly Action<DesktopOverlaySettings> _settingsChanged;
    private readonly Action _openStorageFolder;
    private readonly Func<string, string> _localize;
    private readonly Action<EventHandler>? _unsubscribeLanguageChanged;
    private readonly DesktopOverlaySettings _settings;

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
    private readonly TextBlock _iconSizeLabel = new();
    private readonly ComboBox _iconSizeComboBox = new();
    private readonly CheckBox _showFilePreviewsCheckBox = new();
    private readonly TextBlock _zOrderLabel = new();
    private readonly ComboBox _zOrderComboBox = new();
    private readonly CheckBox _autoArrangeCheckBox = new();
    private readonly TextBlock _sortLabel = new();
    private readonly ComboBox _sortComboBox = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _resetButton = new();

    private bool _initializing = true;
    private bool _disposed;

    public DesktopOverlaySettingsControl(
        DesktopOverlaySettings settings,
        GlueDockWidgetAppearance appearance,
        Action<DesktopOverlaySettings> settingsChanged,
        Action openStorageFolder,
        Func<string, string> localize,
        Action<EventHandler>? subscribeLanguageChanged,
        Action<EventHandler>? unsubscribeLanguageChanged)
    {
        _settings =
            settings.Clone();

        _appearance =
            appearance;

        _settingsChanged =
            settingsChanged;

        _openStorageFolder =
            openStorageFolder;

        _localize =
            localize;

        _unsubscribeLanguageChanged =
            unsubscribeLanguageChanged;

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
            _iconSizeComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            _zOrderComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            _sortComboBox,
            palette);

        _borderColorPreview.BorderBrush =
            palette.ControlBorderBrush;

        PopulateThemeOptions();
        PopulateLocalizedOptions();
        LoadValues();
        RefreshLanguage();

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

        panel.Children.Add(
            CreateRow(
                _iconSizeLabel,
                _iconSizeComboBox));

        _showFilePreviewsCheckBox.Margin =
            new Thickness(
                0,
                2,
                0,
                DockDialogTheme.StandardRowBottomMargin);

        panel.Children.Add(
            _showFilePreviewsCheckBox);

        panel.Children.Add(
            CreateRow(
                _zOrderLabel,
                _zOrderComboBox));

        _autoArrangeCheckBox.Margin =
            new Thickness(
                0,
                2,
                0,
                DockDialogTheme.StandardRowBottomMargin);

        panel.Children.Add(
            _autoArrangeCheckBox);

        panel.Children.Add(
            CreateRow(
                _sortLabel,
                _sortComboBox));

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        _openFolderButton.Margin =
            new Thickness(
                0,
                0,
                8,
                0);

        buttons.Children.Add(
            _openFolderButton);

        buttons.Children.Add(
            _resetButton);

        panel.Children.Add(
            buttons);

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

        _iconSizeComboBox.SelectionChanged +=
            ControlChanged;

        _showFilePreviewsCheckBox.Checked +=
            ControlChanged;

        _showFilePreviewsCheckBox.Unchecked +=
            ControlChanged;

        _zOrderComboBox.SelectionChanged +=
            ControlChanged;

        _autoArrangeCheckBox.Checked +=
            ControlChanged;

        _autoArrangeCheckBox.Unchecked +=
            ControlChanged;

        _sortComboBox.SelectionChanged +=
            ControlChanged;

        _openFolderButton.Click +=
            OpenFolderButton_Click;

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

    private void PopulateLocalizedOptions()
    {
        string selectedIconSize =
            (_iconSizeComboBox.SelectedItem as Option)?.Id ??
            _settings.IconSizeMode;

        string selectedZOrder =
            (_zOrderComboBox.SelectedItem as Option)?.Id ??
            _settings.ZOrderMode;

        string selectedSort =
            (_sortComboBox.SelectedItem as Option)?.Id ??
            _settings.SortMode;

        _iconSizeComboBox.ItemsSource =
            new[]
            {
                new Option(
                    "Small",
                    _localize(
                        "Widget.DesktopOverlay.IconSize.Small")),
                new Option(
                    "Medium",
                    _localize(
                        "Widget.DesktopOverlay.IconSize.Medium")),
                new Option(
                    "Large",
                    _localize(
                        "Widget.DesktopOverlay.IconSize.Large"))
            };

        _zOrderComboBox.ItemsSource =
            new[]
            {
                new Option(
                    "Normal",
                    _localize(
                        "Widget.DesktopOverlay.ZOrder.Normal")),
                new Option(
                    "AlwaysOnTop",
                    _localize(
                        "Widget.DesktopOverlay.ZOrder.AlwaysOnTop")),
                new Option(
                    "Desktop",
                    _localize(
                        "Widget.DesktopOverlay.ZOrder.Desktop"))
            };

        _sortComboBox.ItemsSource =
            new[]
            {
                new Option(
                    "Name",
                    _localize(
                        "Widget.DesktopOverlay.Sort.Name")),
                new Option(
                    "Type",
                    _localize(
                        "Widget.DesktopOverlay.Sort.Type")),
                new Option(
                    "Date",
                    _localize(
                        "Widget.DesktopOverlay.Sort.Date"))
            };

        SelectOption(
            _iconSizeComboBox,
            selectedIconSize);

        SelectOption(
            _zOrderComboBox,
            selectedZOrder);

        SelectOption(
            _sortComboBox,
            selectedSort);
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

        UpdateBorderColorPreview();

        SelectOption(
            _iconSizeComboBox,
            _settings.IconSizeMode);

        _showFilePreviewsCheckBox.IsChecked =
            _settings.ShowFilePreviews;

        SelectOption(
            _zOrderComboBox,
            _settings.ZOrderMode);

        _autoArrangeCheckBox.IsChecked =
            _settings.AutoArrange;

        SelectOption(
            _sortComboBox,
            _settings.SortMode);

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

    private void PublishSettings()
    {
        UpdateValueTexts();

        if (_initializing)
        {
            return;
        }

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

        _settings.IconSizeMode =
            (_iconSizeComboBox.SelectedItem as Option)?.Id ??
            "Medium";

        _settings.ShowFilePreviews =
            _showFilePreviewsCheckBox.IsChecked ==
            true;

        _settings.ZOrderMode =
            (_zOrderComboBox.SelectedItem as Option)?.Id ??
            "Normal";

        _settings.AutoArrange =
            _autoArrangeCheckBox.IsChecked ==
            true;

        _settings.SortMode =
            (_sortComboBox.SelectedItem as Option)?.Id ??
            "Name";

        _settingsChanged(
            _settings.Clone());
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

        bool enabled =
            _borderEnabledCheckBox.IsChecked ==
            true;

        _borderColorPreview.Opacity =
            1.0;

        _borderColorButton.IsEnabled =
            enabled;
    }

    private void UpdateValueTexts()
    {
        _opacityValue.Text =
            $"{_opacitySlider.Value:0}%";

        _blurValue.Text =
            $"{_blurSlider.Value:0}";

        UpdateBorderColorPreview();
    }

    private void OpenFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _openStorageFolder();
    }

    private void ResetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _initializing =
            true;

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

        _settings.IconSizeMode =
            "Medium";

        _settings.ShowFilePreviews =
            true;

        _settings.ZOrderMode =
            "Normal";

        _settings.AutoArrange =
            false;

        _settings.SortMode =
            "Name";

        LoadValues();

        _initializing =
            false;

        PublishSettings();
    }

    private void RefreshLanguage()
    {
        _themeLabel.Text =
            _localize(
                "Settings.Theme");

        _opacityLabel.Text =
            _localize(
                "Settings.Opacity");

        _blurLabel.Text =
            _localize(
                "Settings.Blur");

        _borderEnabledCheckBox.Content =
            _localize(
                "Settings.ShowDockBorder");

        _borderColorLabel.Text =
            _localize(
                "Settings.DockBorderColor");

        _borderColorButton.Content =
            _localize(
                "Settings.ChooseColor");

        _iconSizeLabel.Text =
            _localize(
                "Widget.DesktopOverlay.Settings.IconSize");

        _showFilePreviewsCheckBox.Content =
            _localize(
                "Settings.ShowFilePreviews");

        _zOrderLabel.Text =
            _localize(
                "Widget.DesktopOverlay.Settings.ZOrder");

        _autoArrangeCheckBox.Content =
            _localize(
                "Widget.DesktopOverlay.Settings.AutoArrange");

        _sortLabel.Text =
            _localize(
                "Widget.DesktopOverlay.Settings.SortBy");

        _openFolderButton.Content =
            _localize(
                "Widget.DesktopOverlay.Settings.OpenFolder");

        _resetButton.Content =
            _localize(
                "Widget.DesktopOverlay.Settings.Reset");

        bool previousInitializing =
            _initializing;

        _initializing =
            true;

        PopulateLocalizedOptions();

        _initializing =
            previousInitializing;
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Invoke(
            RefreshLanguage);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _unsubscribeLanguageChanged?.Invoke(
            LanguageChanged);
    }
}
