// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

using GlueDock;

namespace GlueDock.DriveUsageWidget;

// GlueDock UI rule: This settings window uses the dock-wide DockDialogTheme / DockDialogThemeService.
// Do not add dialog-local replacements for standard buttons, inputs, ComboBoxes, contrast or popup styling.
public partial class DriveUsageSettingsWindow : DockSettingsSectionControl
{
    private sealed record DriveOption(
        string DisplayName,
        string RootPath)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private readonly ComboBox[] _driveComboBoxes;
    private readonly IReadOnlyList<DriveOption> _driveOptions;
    private readonly GlueDockWidgetAppearance? _appearance;
    private readonly Action<DriveUsageSettings>? _previewChanged;
    private readonly Func<string, string> _localize;
    private readonly Action<EventHandler>? _subscribeLanguageChanged;
    private readonly Action<EventHandler>? _unsubscribeLanguageChanged;
    private readonly DockDialogNativeBackdropHost _nativeBackdropHost;
    private bool _isInitializing = true;
    private string _ringColor;

    public DriveUsageSettingsWindow(
        DriveUsageSettings settings,
        GlueDockWidgetAppearance? appearance,
        Action<DriveUsageSettings>? previewChanged,
        Func<string, string> localize,
        Action<EventHandler>? subscribeLanguageChanged,
        Action<EventHandler>? unsubscribeLanguageChanged)
    {
        InitializeComponent();

        _nativeBackdropHost =
            new DockDialogNativeBackdropHost(
                this);

        _appearance =
            appearance;

        _previewChanged =
            previewChanged;

        _localize =
            localize;

        _subscribeLanguageChanged =
            subscribeLanguageChanged;

        _unsubscribeLanguageChanged =
            unsubscribeLanguageChanged;

        _subscribeLanguageChanged?.Invoke(
            LanguageChanged);

        Closed +=
            (_, _) =>
                _unsubscribeLanguageChanged?.Invoke(
                    LanguageChanged);

        _ringColor =
            settings.RingColor;

        _driveComboBoxes =
            new[]
            {
                Drive1ComboBox,
                Drive2ComboBox,
                Drive3ComboBox,
                Drive4ComboBox
            };

        _driveOptions =
            BuildDriveOptions();

        foreach (ComboBox comboBox in
                 _driveComboBoxes)
        {
            comboBox.ItemsSource =
                _driveOptions;
        }

        string[] selectedDrives =
            settings.SelectedDrives
                .Where(
                    drive =>
                        !string.IsNullOrWhiteSpace(
                            drive))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    4)
                .ToArray();

        for (int index = 0;
             index < _driveComboBoxes.Length;
             index++)
        {
            _driveComboBoxes[index].SelectedValue =
                index < selectedDrives.Length
                    ? selectedDrives[index]
                    : string.Empty;

            if (_driveComboBoxes[index].SelectedIndex < 0)
            {
                _driveComboBoxes[index].SelectedValue =
                    string.Empty;
            }
        }

        RingScaleSlider.Value =
            settings.RingScalePercent;

        RingThicknessSlider.Value =
            settings.RingThicknessPercent;

        CompanionScaleSlider.Value =
            settings.CompanionScalePercent;

        SettingsOpacityUseGeneralCheckBox.IsChecked =
            !settings.SettingsOpacityOverrideEnabled;

        SettingsOpacitySlider.Value =
            Math.Clamp(
                (settings.SettingsOpacityOverrideEnabled
                    ? settings.SettingsOpacity
                    : appearance?.Opacity ??
                      settings.SettingsOpacity) *
                100.0,
                10,
                100);

        SettingsBlurUseGeneralCheckBox.IsChecked =
            !settings.SettingsBlurOverrideEnabled;

        SettingsBlurSlider.Value =
            Math.Clamp(
                settings.SettingsBlurOverrideEnabled
                    ? settings.SettingsBlurRadius
                    : appearance?.BlurRadius ??
                      settings.SettingsBlurRadius,
                0,
                100);

        DisableDefaultHoverEffectCheckBox.IsChecked =
            settings.DisableDefaultHoverEffect;

        SelectRingFillMode(
            settings.RingFillMode);

        RefreshLanguage();
        UpdateSettingsAppearanceControlState();
        ApplyAppearance();
        UpdateRingColorPreview();
        UpdateSliderValueTexts();

        _isInitializing =
            false;
    }

    public DriveUsageSettings Result { get; private set; } =
        new();

    public (
        FrameworkElement General,
        FrameworkElement Companion
    ) CreateEmbeddedSettingsContent()
    {
        SettingsContentPanel.Children.Remove(
            GeneralSettingsPanel);

        SettingsContentPanel.Children.Remove(
            CompanionSettingsPanel);

        DialogButtonsPanel.Visibility =
            Visibility.Collapsed;

        GeneralSettingsPanel.Margin =
            new Thickness(
                0);

        CompanionSettingsPanel.Margin =
            new Thickness(
                0);

        DockSettingsSectionControl generalSection =
            new()
            {
                Background =
                    Brushes.Transparent,
                Content =
                    GeneralSettingsPanel
            };

        DockSettingsSectionControl companionSection =
            new()
            {
                Background =
                    Brushes.Transparent,
                Content =
                    CompanionSettingsPanel
            };

        return (
            generalSection,
            companionSection);
    }

    private IReadOnlyList<DriveOption> BuildDriveOptions()
    {
        List<DriveOption> options =
            new()
            {
                new(
                    _localize(
                        "Widget.DriveUsage.Settings.None"),
                    string.Empty)
            };

        foreach (DriveInfo drive in
                 DriveInfo.GetDrives()
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
                         StringComparer.OrdinalIgnoreCase))
        {
            string displayName =
                string.IsNullOrWhiteSpace(
                    drive.VolumeLabel)
                    ? drive.Name
                    : $"{drive.Name}  {drive.VolumeLabel}";

            options.Add(
                new DriveOption(
                    displayName,
                    drive.Name));
        }

        return options;
    }

    private void ApplyAppearance()
    {
        Brush background =
            _appearance?.DockBackgroundBrush.Clone() ??
            new SolidColorBrush(
                Color.FromRgb(
                    0x12,
                    0x16,
                    0x1C));

        Brush border =
            _appearance?.DockItemBorderBrush.Clone() ??
            new SolidColorBrush(
                Color.FromArgb(
                    0x60,
                    0xFF,
                    0xFF,
                    0xFF));

        Brush text =
            _appearance?.DockTextBrush.Clone() ??
            Brushes.White;

        if (_appearance is not null)
        {
            DockDialogThemePalette uiPalette =
                DockDialogThemeService.ApplyWindowControlResources(
                    this,
                    _appearance);

            foreach (ComboBox comboBox in
                     _driveComboBoxes)
            {
                DockDialogThemeService.ApplyComboBox(
                    comboBox,
                    uiPalette);
            }

            DockDialogThemeService.ApplyComboBox(
                RingFillModeComboBox,
                uiPalette);
        }

        double cornerRadius =
            Math.Clamp(
                _appearance?.GlassCornerRadius ??
                20,
                0,
                80);

        WindowBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        WindowBorder.Background =
            DockDialogThemeService.CreateDialogSurfaceBrush(
                background,
                GetEffectiveSettingsOpacity());

        WindowBorder.BorderBrush =
            border;

        _nativeBackdropHost.SetBlur(
            GetEffectiveSettingsBlurRadius());

        RingColorPreview.BorderBrush =
            border;

        Foreground =
            text;
    }

    private void RefreshLanguage()
    {
        string title =
            _localize(
                "Widget.DriveUsage.Settings.Title");

        Title =
            title;

        TitleText.Text =
            title;

        CloseButton.Content =
            "×";

        SelectDrivesText.Text =
            _localize(
                "Widget.DriveUsage.Settings.SelectDrives");

        Drive1LabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.Drive1");

        Drive2LabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.Drive2");

        Drive3LabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.Drive3");

        Drive4LabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.Drive4");

        SettingsOpacityLabelText.Text =
            _localize(
                "Settings.Opacity");

        SettingsBlurLabelText.Text =
            _localize(
                "Settings.Blur");

        string useGeneralSettingText =
            _localize(
                "Settings.UseGeneralSetting");

        SettingsOpacityUseGeneralCheckBox.Content =
            useGeneralSettingText;

        SettingsBlurUseGeneralCheckBox.Content =
            useGeneralSettingText;

        RingAppearanceText.Text =
            _localize(
                "Widget.DriveUsage.Settings.RingAppearance");

        RingColorLabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.RingColor");

        RingScaleLabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.RingScale");

        RingThicknessLabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.RingThickness");

        RingFillLabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.RingFill");

        CompanionScaleLabelText.Text =
            _localize(
                "Widget.DriveUsage.Settings.CompanionScale");

        RingColorButton.Content =
            _localize(
                "Settings.ChooseColor");

        UsedRingFillModeItem.Content =
            _localize(
                "Widget.DriveUsage.Settings.Used");

        FreeRingFillModeItem.Content =
            _localize(
                "Widget.DriveUsage.Settings.Free");

        DisableDefaultHoverEffectCheckBox.Content =
            _localize(
                "Widget.DriveUsage.Settings.DisableDefaultHoverEffect");

        DefaultInfoText.Text =
            _localize(
                "Widget.DriveUsage.Settings.DefaultInfo");

        OkButton.Content =
            _localize(
                "ColorPicker.Ok");

        CancelButton.Content =
            _localize(
                "ColorPicker.Cancel");
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Invoke(
            RefreshLanguage);
    }

    private void SettingsOpacityUseGeneralCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateSettingsAppearanceControlState();

        if (!_isInitializing)
        {
            ApplyAppearance();
            NotifyPreviewChanged();
        }
    }

    private void SettingsOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSettingsAppearanceControlState();

        if (!_isInitializing &&
            SettingsOpacityUseGeneralCheckBox.IsChecked !=
            true)
        {
            ApplyAppearance();
            NotifyPreviewChanged();
        }
    }

    private void SettingsBlurUseGeneralCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateSettingsAppearanceControlState();

        if (!_isInitializing)
        {
            ApplyAppearance();
            NotifyPreviewChanged();
        }
    }

    private void SettingsBlurSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSettingsAppearanceControlState();

        if (!_isInitializing &&
            SettingsBlurUseGeneralCheckBox.IsChecked !=
            true)
        {
            ApplyAppearance();
            NotifyPreviewChanged();
        }
    }

    private void UpdateSettingsAppearanceControlState()
    {
        if (SettingsOpacitySlider is null ||
            SettingsOpacityValueText is null ||
            SettingsBlurSlider is null ||
            SettingsBlurValueText is null)
        {
            return;
        }

        bool opacityOverrideEnabled =
            SettingsOpacityUseGeneralCheckBox.IsChecked !=
            true;

        bool blurOverrideEnabled =
            SettingsBlurUseGeneralCheckBox.IsChecked !=
            true;

        SettingsOpacitySlider.IsEnabled =
            opacityOverrideEnabled;

        SettingsBlurSlider.IsEnabled =
            blurOverrideEnabled;

        double opacity =
            GetEffectiveSettingsOpacity();

        double blurRadius =
            GetEffectiveSettingsBlurRadius();

        if (!opacityOverrideEnabled)
        {
            SettingsOpacitySlider.Value =
                Math.Clamp(
                    opacity * 100.0,
                    10,
                    100);
        }

        if (!blurOverrideEnabled)
        {
            SettingsBlurSlider.Value =
                Math.Clamp(
                    blurRadius,
                    0,
                    100);
        }

        SettingsOpacityValueText.Text =
            $"{opacity * 100.0:0}%";

        SettingsBlurValueText.Text =
            $"{blurRadius:0}";
    }

    private double GetEffectiveSettingsOpacity()
    {
        bool overrideEnabled =
            SettingsOpacityUseGeneralCheckBox.IsChecked !=
            true;

        return overrideEnabled
            ? Math.Clamp(
                SettingsOpacitySlider.Value / 100.0,
                0.10,
                1.00)
            : Math.Clamp(
                _appearance?.Opacity ??
                SettingsOpacitySlider.Value / 100.0,
                0.10,
                1.00);
    }

    private double GetEffectiveSettingsBlurRadius()
    {
        bool overrideEnabled =
            SettingsBlurUseGeneralCheckBox.IsChecked !=
            true;

        return overrideEnabled
            ? Math.Clamp(
                SettingsBlurSlider.Value,
                0,
                100)
            : Math.Clamp(
                _appearance?.BlurRadius ??
                SettingsBlurSlider.Value,
                0,
                100);
    }

    private void RingColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color currentColor =
            ParseColor(
                _ringColor,
                Colors.Orange);

        using WinForms.ColorDialog dialog =
            new()
            {
                FullOpen = true,
                Color =
                    Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R,
                        currentColor.G,
                        currentColor.B)
            };

        if (dialog.ShowDialog() !=
            WinForms.DialogResult.OK)
        {
            return;
        }

        Drawing.Color selected =
            dialog.Color;

        _ringColor =
            $"#{selected.A:X2}{selected.R:X2}{selected.G:X2}{selected.B:X2}";

        UpdateRingColorPreview();
        NotifyPreviewChanged();
    }

    private void RingScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderValueTexts();
        NotifyPreviewChanged();
    }

    private void RingThicknessSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderValueTexts();
        NotifyPreviewChanged();
    }

    private void CompanionScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderValueTexts();
        NotifyPreviewChanged();
    }

    private void DriveComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        NotifyPreviewChanged();
    }

    private void DisableDefaultHoverEffectCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        NotifyPreviewChanged();
    }

    private void RingFillModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        NotifyPreviewChanged();
    }

    private void UpdateSliderValueTexts()
    {
        if (RingScaleValueText is not null)
        {
            RingScaleValueText.Text =
                $"{RingScaleSlider.Value:0}%";
        }

        if (RingThicknessValueText is not null)
        {
            RingThicknessValueText.Text =
                $"{RingThicknessSlider.Value:0}%";
        }

        if (CompanionScaleValueText is not null)
        {
            CompanionScaleValueText.Text =
                $"{CompanionScaleSlider.Value:0}%";
        }
    }

    private void UpdateRingColorPreview()
    {
        Color color =
            ParseColor(
                _ringColor,
                Colors.Orange);

        RingColorPreview.Background =
            new SolidColorBrush(
                color);

    }

    private void SelectRingFillMode(
        string value)
    {
        foreach (object item in
                 RingFillModeComboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                string.Equals(
                    comboBoxItem.Tag as string,
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                RingFillModeComboBox.SelectedItem =
                    comboBoxItem;

                return;
            }
        }

        RingFillModeComboBox.SelectedIndex =
            0;
    }

    private void NotifyPreviewChanged()
    {
        if (_isInitializing ||
            _previewChanged is null)
        {
            return;
        }

        List<string> selectedDrives =
            _driveComboBoxes
                .Select(
                    comboBox =>
                        comboBox.SelectedValue as string ??
                        string.Empty)
                .Where(
                    drive =>
                        !string.IsNullOrWhiteSpace(
                            drive))
                .ToList();

        if (selectedDrives.Count !=
            selectedDrives.Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count())
        {
            return;
        }

        _previewChanged(
            BuildCurrentSettings());
    }

    private DriveUsageSettings BuildCurrentSettings()
    {
        List<string> selectedDrives =
            _driveComboBoxes
                .Select(
                    comboBox =>
                        comboBox.SelectedValue as string ??
                        string.Empty)
                .Where(
                    drive =>
                        !string.IsNullOrWhiteSpace(
                            drive))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    4)
                .ToList();

        string ringFillMode =
            RingFillModeComboBox.SelectedItem is ComboBoxItem selectedItem &&
            string.Equals(
                selectedItem.Tag as string,
                "Free",
                StringComparison.OrdinalIgnoreCase)
                ? "Free"
                : "Used";

        return new DriveUsageSettings
        {
            SelectedDrives =
                selectedDrives,
            SettingsOpacityOverrideEnabled =
                SettingsOpacityUseGeneralCheckBox.IsChecked !=
                true,
            SettingsOpacity =
                Math.Clamp(
                    SettingsOpacitySlider.Value / 100.0,
                    0.10,
                    1.00),
            SettingsBlurOverrideEnabled =
                SettingsBlurUseGeneralCheckBox.IsChecked !=
                true,
            SettingsBlurRadius =
                Math.Clamp(
                    SettingsBlurSlider.Value,
                    0,
                    100),
            RingColor =
                _ringColor,
            RingScalePercent =
                RingScaleSlider.Value,
            RingThicknessPercent =
                RingThicknessSlider.Value,
            RingFillMode =
                ringFillMode,
            CompanionScalePercent =
                CompanionScaleSlider.Value,
            DisableDefaultHoverEffect =
                DisableDefaultHoverEffectCheckBox.IsChecked == true
        }.Clone();
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        List<string> selectedDrives =
            _driveComboBoxes
                .Select(
                    comboBox =>
                        comboBox.SelectedValue as string ??
                        string.Empty)
                .Where(
                    drive =>
                        !string.IsNullOrWhiteSpace(
                            drive))
                .ToList();

        if (selectedDrives.Count !=
            selectedDrives.Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count())
        {
            if (StandaloneWindow is Window ownerWindow)
            {
                MessageBox.Show(
                    ownerWindow,
                    _localize(
                        "Widget.DriveUsage.Settings.DuplicateDriveMessage"),
                    _localize(
                        "Widget.DriveUsage.Settings.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    _localize(
                        "Widget.DriveUsage.Settings.DuplicateDriveMessage"),
                    _localize(
                        "Widget.DriveUsage.Settings.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return;
        }

        string ringFillMode =
            RingFillModeComboBox.SelectedItem is ComboBoxItem selectedItem &&
            string.Equals(
                selectedItem.Tag as string,
                "Free",
                StringComparison.OrdinalIgnoreCase)
                ? "Free"
                : "Used";

        Result =
            new DriveUsageSettings
            {
                SelectedDrives =
                    selectedDrives
                        .Take(
                            4)
                        .ToList(),
                SettingsOpacityOverrideEnabled =
                    SettingsOpacityUseGeneralCheckBox.IsChecked !=
                    true,
                SettingsOpacity =
                    Math.Clamp(
                        SettingsOpacitySlider.Value / 100.0,
                        0.10,
                        1.00),
                SettingsBlurOverrideEnabled =
                    SettingsBlurUseGeneralCheckBox.IsChecked !=
                    true,
                SettingsBlurRadius =
                    Math.Clamp(
                        SettingsBlurSlider.Value,
                        0,
                        100),
                RingColor =
                    _ringColor,
                RingScalePercent =
                    RingScaleSlider.Value,
                RingThicknessPercent =
                    RingThicknessSlider.Value,
                RingFillMode =
                    ringFillMode,
                CompanionScalePercent =
                    CompanionScaleSlider.Value,
                DisableDefaultHoverEffect =
                    DisableDefaultHoverEffectCheckBox.IsChecked == true
            }.Clone();

        DialogResult =
            true;
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;
    }

    private void WindowBorder_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            IsInteractiveSource(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
    }

    private static bool IsInteractiveSource(
        DependencyObject? source)
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is ButtonBase ||
                current is Selector ||
                current is RangeBase ||
                current is TextBoxBase ||
                current is ScrollBar ||
                current is Thumb)
            {
                return true;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return false;
    }

    private static Color ParseColor(
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

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    // GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
}
