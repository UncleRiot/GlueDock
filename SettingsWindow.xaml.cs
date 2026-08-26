using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;


namespace GlueDock;

public partial class SettingsWindow : Window
{
    private readonly DockSettings _settings;
    private readonly Action _settingsChanged;
    private bool _isLoading;

    public SettingsWindow(
        DockSettings settings,
        Action settingsChanged)
    {
        _settings = settings;
        _settingsChanged = settingsChanged;
        _isLoading = true;

        InitializeComponent();

        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        CollapseDisabledCheckBox.IsChecked = _settings.CollapseDisabled;
        CollapseDelaySlider.Value = _settings.CollapseDelayMilliseconds;
        DockBorderEnabledCheckBox.IsChecked = _settings.DockBorderEnabled;
        BarThicknessSlider.Value = _settings.BarThickness;
        DockScaleSlider.Value = _settings.DockScale;
        OpacitySlider.Value = _settings.Opacity;

        foreach (System.Windows.Controls.ComboBoxItem item in AnimationStyleComboBox.Items)
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    _settings.AnimationStyle,
                    StringComparison.OrdinalIgnoreCase))
            {
                AnimationStyleComboBox.SelectedItem = item;
                break;
            }
        }

        if (AnimationStyleComboBox.SelectedIndex < 0)
        {
            AnimationStyleComboBox.SelectedIndex = 0;
        }

        UpdateColorPreview();
        UpdateDockBorderColorPreview();
        UpdateDockBorderControls();
        UpdateValueTexts();

        _isLoading = false;
    }

    private void StartWithWindowsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.StartWithWindows =
            StartWithWindowsCheckBox.IsChecked == true;

        StartupManager.SetStartWithWindows(
            _settings.StartWithWindows);

        ApplyLive();
    }

    private void CollapseDisabledCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.CollapseDisabled =
            CollapseDisabledCheckBox.IsChecked == true;

        ApplyLive();
    }

    private void CollapseDelaySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.CollapseDelayMilliseconds =
            (int)Math.Round(
                CollapseDelaySlider.Value);

        UpdateValueTexts();
        ApplyLive();
    }

    private void ChooseBarColor_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color currentColor;

        try
        {
            currentColor =
                (Color)ColorConverter.ConvertFromString(
                    _settings.BarColor);
        }
        catch
        {
            currentColor =
                Color.FromRgb(
                    0x12,
                    0x16,
                    0x1C);
        }

        ColorPickerWindow colorPicker =
            new(currentColor)
            {
                Owner = this
            };

        if (colorPicker.ShowDialog() != true)
        {
            return;
        }

        Color selectedColor =
            colorPicker.SelectedColor;

        _settings.BarColor =
            $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

        UpdateColorPreview();
        ApplyLive();
    }

    private void DockBorderEnabledCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.DockBorderEnabled =
            DockBorderEnabledCheckBox.IsChecked == true;

        UpdateDockBorderControls();
        ApplyLive();
    }

    private void ChooseDockBorderColor_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color currentColor;

        try
        {
            currentColor =
                (Color)ColorConverter.ConvertFromString(
                    _settings.DockBorderColor);
        }
        catch
        {
            currentColor =
                Color.FromRgb(
                    0xFF,
                    0xFF,
                    0xFF);
        }

        ColorPickerWindow colorPicker =
            new(currentColor)
            {
                Owner = this
            };

        if (colorPicker.ShowDialog() != true)
        {
            return;
        }

        Color selectedColor =
            colorPicker.SelectedColor;

        _settings.DockBorderColor =
            $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

        UpdateDockBorderColorPreview();
        ApplyLive();
    }

    private void BarThicknessSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.BarThickness =
            BarThicknessSlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void DockScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.DockScale =
            DockScaleSlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void OpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.Opacity =
            OpacitySlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void AnimationStyleComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            AnimationStyleComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item)
        {
            return;
        }

        _settings.AnimationStyle =
            item.Tag?.ToString() ?? "Fade";

        ApplyLive();
    }

    private void UpdateColorPreview()
    {
        try
        {
            Color color =
                (Color)ColorConverter.ConvertFromString(
                    _settings.BarColor);

            BarColorPreview.Background =
                new SolidColorBrush(color);
        }
        catch
        {
            BarColorPreview.Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        0x12,
                        0x16,
                        0x1C));
        }
    }

    private void UpdateDockBorderColorPreview()
    {
        try
        {
            Color color =
                (Color)ColorConverter.ConvertFromString(
                    _settings.DockBorderColor);

            DockBorderColorPreview.Background =
                new SolidColorBrush(color);
        }
        catch
        {
            DockBorderColorPreview.Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        0xFF,
                        0xFF,
                        0xFF));
        }
    }

    private void UpdateDockBorderControls()
    {
        bool enabled =
            DockBorderEnabledCheckBox.IsChecked == true;

        DockBorderColorPreview.Opacity =
            enabled
                ? 1
                : 0.35;

        DockBorderColorButton.IsEnabled =
            enabled;
    }

    private void UpdateValueTexts()
    {
        CollapseDelayText.Text =
            $"{_settings.CollapseDelayMilliseconds} ms";

        BarThicknessText.Text =
            $"{_settings.BarThickness:0} px";

        DockScaleText.Text =
            $"{_settings.DockScale * 100:0}%";

        OpacityText.Text =
            $"{_settings.Opacity * 100:0}%";
    }

    private void ApplyLive()
    {
        _settingsChanged();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
