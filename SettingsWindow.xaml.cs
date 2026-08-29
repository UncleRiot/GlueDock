using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;


namespace GlueDock;

public partial class SettingsWindow : Window
{
    private readonly DockSettings _settings;
    private readonly Action _settingsChanged;
    private readonly Action _itemSpacingChanged;
    private readonly Action _settingsPreviewChanged;
    private readonly LanguageService _language;
    private readonly ProfileStore _profileStore = new();
    private DockSettings? _profilePreviewSnapshot;
    private bool _isLoading;
    private bool _isLoadingProfiles;

    public SettingsWindow(
        DockSettings settings,
        Action settingsChanged,
        Action itemSpacingChanged,
        Action settingsPreviewChanged,
        LanguageService language)
    {
        _settings = settings;
        _settingsChanged = settingsChanged;
        _itemSpacingChanged = itemSpacingChanged;
        _settingsPreviewChanged = settingsPreviewChanged;
        _language = language;
        _isLoading = true;

        InitializeComponent();

        _language.LanguageChanged +=
            Language_LanguageChanged;

        Closed +=
            SettingsWindow_Closed;

        LoadSettings();
        ReloadProfiles();
        ApplyLanguage();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        CollapseDisabledCheckBox.IsChecked = _settings.CollapseDisabled;
        CollapseDelaySlider.Value = _settings.CollapseDelayMilliseconds;
        SubdockCollapseDelaySlider.Value = _settings.SubdockCollapseDelayMilliseconds;
        DockBorderEnabledCheckBox.IsChecked = _settings.DockBorderEnabled;
        BarThicknessSlider.Value = _settings.BarThickness;
        DockScaleSlider.Value = _settings.DockScale;
        ItemSpacingSlider.Value =
            Math.Clamp(
                _settings.ItemSpacing + 18.0,
                10.0,
                60.0);
        DockThicknessScaleSlider.Value = _settings.DockThicknessScale;
        ShowItemLabelsCheckBox.IsChecked = _settings.ShowItemLabels;
        ShowFilePreviewsCheckBox.IsChecked = _settings.ShowFilePreviews;
        UseSmallShortcutOverlayCheckBox.IsChecked = _settings.UseSmallShortcutOverlay;

        ReloadThemeOptions();

        UpdateSubmenuIconPreview();

        DebugLoggingCheckBox.IsChecked = _settings.DebugLoggingEnabled;
        DebugLogMaxSizeSlider.Value =
            Math.Clamp(
                _settings.DebugLogMaxSizeMegabytes,
                1,
                10);
        OpacitySlider.Value = _settings.Opacity;
        BlurSlider.Value = _settings.BlurRadius;

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

        foreach (System.Windows.Controls.ComboBoxItem item in HoverEffectComboBox.Items)
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    _settings.HoverEffect,
                    StringComparison.OrdinalIgnoreCase))
            {
                HoverEffectComboBox.SelectedItem = item;
                break;
            }
        }

        if (HoverEffectComboBox.SelectedIndex < 0)
        {
            HoverEffectComboBox.SelectedIndex = 0;
            _settings.HoverEffect = "None";
        }

        LanguageComboBox.ItemsSource =
            _language.GetAvailableLanguages();

        LanguageComboBox.SelectedValue =
            _settings.LanguageCode;

        if (LanguageComboBox.SelectedIndex < 0)
        {
            LanguageComboBox.SelectedValue = "EN";
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

        if (_profilePreviewSnapshot is null)
        {
            StartupManager.SetStartWithWindows(
                _settings.StartWithWindows);
        }

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

    private void SubdockCollapseDelaySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SubdockCollapseDelayMilliseconds =
            (int)Math.Round(
                SubdockCollapseDelaySlider.Value);

        UpdateValueTexts();
        ApplyLive();
    }

    private void ThemeComboBox_DropDownOpened(
        object sender,
        EventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        bool wasLoading =
            _isLoading;

        _isLoading = true;

        ReloadThemeOptions();

        _isLoading =
            wasLoading;
    }

    private void ThemeComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            ThemeComboBox.SelectedValue is not string themeName)
        {
            return;
        }

        _settings.ThemeName =
            themeName;

        ApplyLive();
    }

    private void ReloadThemeOptions()
    {
        IReadOnlyList<DockThemeOption> themes =
            DockThemeService.GetAvailableThemes();

        ThemeComboBox.ItemsSource =
            themes;

        ThemeComboBox.SelectedValue =
            _settings.ThemeName;

        if (ThemeComboBox.SelectedIndex >= 0)
        {
            return;
        }

        DockThemeOption? defaultTheme =
            themes.FirstOrDefault(
                theme =>
                    string.Equals(
                        theme.Id,
                        "Default",
                        StringComparison.OrdinalIgnoreCase));

        if (defaultTheme is not null)
        {
            ThemeComboBox.SelectedValue =
                defaultTheme.Id;

            _settings.ThemeName =
                defaultTheme.Id;

            return;
        }

        if (themes.Count > 0)
        {
            ThemeComboBox.SelectedIndex = 0;

            if (ThemeComboBox.SelectedValue is string firstThemeName)
            {
                _settings.ThemeName =
                    firstThemeName;
            }
        }
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

    private void ItemSpacingSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.ItemSpacing =
            ItemSpacingSlider.Value -
            18.0;

        UpdateValueTexts();

        if (_profilePreviewSnapshot is not null)
        {
            _settingsPreviewChanged();
        }
        else
        {
            _itemSpacingChanged();
        }
    }

    private void DockThicknessScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.DockThicknessScale =
            DockThicknessScaleSlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void ShowItemLabelsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.ShowItemLabels =
            ShowItemLabelsCheckBox.IsChecked == true;

        ApplyLive();
    }

    private void ShowFilePreviewsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.ShowFilePreviews =
            ShowFilePreviewsCheckBox.IsChecked == true;

        ApplyLive();
    }

    private void UseSmallShortcutOverlayCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.UseSmallShortcutOverlay =
            UseSmallShortcutOverlayCheckBox.IsChecked == true;

        ApplyLive();
    }

    private void SubmenuWindowsIconButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string iconPath =
            _settings.SubmenuWindowsIconPath;

        int iconIndex =
            _settings.SubmenuWindowsIconIndex;

        if (string.IsNullOrWhiteSpace(
                iconPath))
        {
            iconPath =
                System.IO.Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.System),
                    "imageres.dll");

            iconIndex = 0;
        }

        if (!SubmenuIcon.TryPickWindowsIcon(
                this,
                ref iconPath,
                ref iconIndex))
        {
            return;
        }

        _settings.SubmenuIconMode =
            "Windows";

        _settings.SubmenuWindowsIconPath =
            iconPath;

        _settings.SubmenuWindowsIconIndex =
            iconIndex;

        UpdateSubmenuIconPreview();
        ApplyLive();
    }

    private void SubmenuLoadImageButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog =
            new()
            {
                Title =
                    _language["Settings.SubmenuIcon.LoadImage"],
                Filter =
                    "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp|GIF|*.gif|Icon|*.ico"
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string repositoryPath =
                IconRepository.Import(
                    dialog.FileName);

            _settings.SubmenuIconMode =
                "Repository";

            _settings.SubmenuIconRepositoryPath =
                repositoryPath;

            UpdateSubmenuIconPreview();
            ApplyLive();
        }
        catch
        {
            System.Windows.MessageBox.Show(
                this,
                _language["Message.SubmenuIconImportFailed"],
                _language["App.Name"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SubmenuDefaultIconButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _settings.SubmenuIconMode =
            "Default";

        UpdateSubmenuIconPreview();
        ApplyLive();
    }

    private void UpdateSubmenuIconPreview()
    {
        SubmenuIconPreview.Source =
            SubmenuIcon.Create(
                _settings);

        if (string.Equals(
                _settings.SubmenuIconMode,
                "Windows",
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                _settings.SubmenuWindowsIconPath))
        {
            SubmenuWindowsIconSelectionText.Text =
                $"{System.IO.Path.GetFileName(_settings.SubmenuWindowsIconPath)}, #{_settings.SubmenuWindowsIconIndex}";
        }
        else
        {
            SubmenuWindowsIconSelectionText.Text =
                string.Empty;
        }
    }

    private void DebugLoggingCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.DebugLoggingEnabled =
            DebugLoggingCheckBox.IsChecked == true;

        if (_profilePreviewSnapshot is null)
        {
            DebugLog.SetEnabled(
                _settings.DebugLoggingEnabled);
        }

        ApplyLive();
    }

    private void DebugLogMaxSizeSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.DebugLogMaxSizeMegabytes =
            (int)Math.Round(
                DebugLogMaxSizeSlider.Value);

        if (_profilePreviewSnapshot is null)
        {
            DebugLog.SetMaximumActiveLogFileSizeMegabytes(
                _settings.DebugLogMaxSizeMegabytes);
        }

        UpdateValueTexts();
        ApplyLive();
    }

    private void BlurSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.BlurRadius =
            BlurSlider.Value;

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

    private void HoverEffectComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            HoverEffectComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item)
        {
            return;
        }

        _settings.HoverEffect =
            item.Tag?.ToString() ?? "None";

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

    private void LanguageComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            LanguageComboBox.SelectedValue is not string languageCode)
        {
            return;
        }

        _settings.LanguageCode =
            languageCode;

        _language.Load(
            languageCode);

        ApplyLive();
    }

    private void Language_LanguageChanged(
        object? sender,
        EventArgs e)
    {
        ApplyLanguage();
    }

    private void SettingsWindow_Closed(
        object? sender,
        EventArgs e)
    {
        CancelProfilePreview();

        _language.LanguageChanged -=
            Language_LanguageChanged;
    }

    private void ApplyLanguage()
    {
        Title =
            _language["Settings.Title"];

        GeneralTab.Header =
            _language["Settings.Tab.General"];

        AppearanceTab.Header =
            _language["Settings.Tab.Appearance"];

        BehaviorTab.Header =
            _language["Settings.Tab.Behavior"];

        DockTab.Header =
            _language["Settings.Tab.Dock"];

        ProfilesTab.Header =
            "Profiles";

        AdvancedTab.Header =
            _language["Settings.Tab.Advanced"];

        StartWithWindowsCheckBox.Content =
            _language["Settings.StartWithWindows"];

        CollapseDisabledCheckBox.Content =
            _language["Settings.DisableCollapse"];

        CollapseDelayLabel.Text =
            _language["Settings.CollapseDelay"];

        SubdockCollapseDelayLabel.Text =
            _language["Settings.SubdockCollapseDelay"];

        LanguageLabel.Text =
            _language["Settings.Language"];

        ThemeLabel.Text =
            _language["Settings.Theme"];

        EdgeBarColorLabel.Text =
            _language["Settings.EdgeBarColor"];

        BarColorButton.Content =
            _language["Settings.ChooseColor"];

        DockBorderEnabledCheckBox.Content =
            _language["Settings.ShowDockBorder"];

        DockBorderColorLabel.Text =
            _language["Settings.DockBorderColor"];

        DockBorderColorButton.Content =
            _language["Settings.ChooseColor"];

        EdgeBarThicknessLabel.Text =
            _language["Settings.EdgeBarThickness"];

        DockScaleLabel.Text =
            _language["Settings.DockScale"];

        ItemSpacingLabel.Text =
            _language["Settings.ItemSpacing"];

        DockThicknessScaleLabel.Text =
            _language["Settings.DockThicknessScale"];

        ShowItemLabelsCheckBox.Content =
            _language["Settings.ShowItemLabels"];

        ShowFilePreviewsCheckBox.Content =
            _language["Settings.ShowFilePreviews"];

        UseSmallShortcutOverlayCheckBox.Content =
            _language["Settings.UseSmallShortcutOverlay"];

        SubmenuIconLabel.Text =
            _language["Settings.SubmenuIcon"];

        SubmenuWindowsIconButton.Content =
            _language["Settings.SubmenuIcon.ChooseWindows"];

        SubmenuLoadImageButton.Content =
            _language["Settings.SubmenuIcon.LoadImage"];

        SubmenuDefaultIconButton.Content =
            _language["Settings.SubmenuIcon.UseDefault"];

        SubmenuRepositoryInfoText.Text =
            _language["Settings.SubmenuIcon.RepositoryInfo"];

        OpacityLabel.Text =
            _language["Settings.Opacity"];

        BlurLabel.Text =
            _language["Settings.Blur"];

        AnimationLabel.Text =
            _language["Settings.Animation"];

        HoverEffectLabel.Text =
            _language["Settings.HoverEffect"];

        ReservedBehaviorLabel.Text =
            _language["Settings.ReservedBehavior"];

        ReservedDockLabel.Text =
            _language["Settings.ReservedDock"];

        DebugLoggingCheckBox.Content =
            _language["Settings.DebugLogging"];

        DebugLoggingInfoText.Text =
            _language["Settings.DebugLoggingInfo"];

        ReservedAdvancedLabel.Text =
            _language["Settings.ReservedAdvanced"];

        CloseButton.Content =
            _language["Settings.Close"];
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

        SubdockCollapseDelayText.Text =
            $"{_settings.SubdockCollapseDelayMilliseconds} ms";

        BarThicknessText.Text =
            $"{_settings.BarThickness:0} px";

        DockScaleText.Text =
            $"{_settings.DockScale * 100:0}%";

        ItemSpacingText.Text =
            $"{Math.Clamp(_settings.ItemSpacing + 18.0, 10.0, 60.0):0} px";

        DockThicknessScaleText.Text =
            $"{_settings.DockThicknessScale * 100:0}%";

        OpacityText.Text =
            $"{_settings.Opacity * 100:0}%";

        BlurText.Text =
            $"{_settings.BlurRadius:0}%";

        DebugLogMaxSizeText.Text =
            $"{Math.Clamp(_settings.DebugLogMaxSizeMegabytes, 1, 10)} MB";
    }

    private void ApplyLive()
    {
        if (_profilePreviewSnapshot is not null)
        {
            _settingsPreviewChanged();
            return;
        }

        _settingsChanged();
    }

    private void ReloadProfiles(
        string? selectedProfile = null)
    {
        _isLoadingProfiles = true;

        try
        {
            ProfilesListBox.ItemsSource =
                _profileStore.GetProfileListEntries();

            if (!string.IsNullOrWhiteSpace(
                    selectedProfile))
            {
                ProfilesListBox.SelectedItem =
                    ProfilesListBox.Items
                        .Cast<ProfileStore.ProfileListEntry>()
                        .FirstOrDefault(
                            entry =>
                                string.Equals(
                                    entry.Name,
                                    selectedProfile,
                                    StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _isLoadingProfiles = false;
        }
    }

    private void ProfilesListBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingProfiles ||
            ProfilesListBox.SelectedItem is not ProfileStore.ProfileListEntry profileEntry)
        {
            return;
        }

        DockSettings? profileSettings =
            _profileStore.Load(
                profileEntry.Name);

        if (profileSettings is null)
        {
            return;
        }

        _profilePreviewSnapshot ??=
            _profileStore.CreateSnapshot(
                _settings);

        _profileStore.CopyProfileSettings(
            profileSettings,
            _settings);

        _language.Load(
            _settings.LanguageCode);

        LoadSettings();

        ApplyProfileButton.IsEnabled = true;
        CancelProfilePreviewButton.IsEnabled = true;

        DebugLog.Write(
            "Profiles",
            $"Preview; Name={profileEntry.Name}; Theme={_settings.ThemeName}; Opacity={_settings.Opacity:0.00}; Blur={_settings.BlurRadius:0.##}; Scale={_settings.DockScale:0.00}; ItemSpacing={_settings.ItemSpacing:0.##}");

        _settingsPreviewChanged();
    }

    private void NewProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SubmenuNameWindow nameWindow =
            new(
                "New Profile",
                "New profile")
            {
                Owner = this
            };

        if (nameWindow.ShowDialog() != true)
        {
            return;
        }

        string profileName =
            nameWindow.SubmenuName;

        if (_profileStore.GetProfileNames().Any(
                name =>
                    string.Equals(
                        name,
                        profileName,
                        StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                this,
                "A profile with this name already exists.",
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        _profileStore.Save(
            profileName,
            _settings);

        DebugLog.Write(
            "Profiles",
            $"Created; Name={profileName}");

        ReloadProfiles(
            profileName);
    }

    private void SaveProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileStore.ProfileListEntry profileEntry)
        {
            NewProfileButton_Click(
                sender,
                e);

            return;
        }

        _profileStore.Save(
            profileEntry.Name,
            _settings);

        DebugLog.Write(
            "Profiles",
            $"Saved; Name={profileEntry.Name}");

        ReloadProfiles(
            profileEntry.Name);
    }

    private void RenameProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileStore.ProfileListEntry profileEntry)
        {
            return;
        }

        string profileName =
            profileEntry.Name;

        SubmenuNameWindow nameWindow =
            new(
                profileName,
                "Rename profile")
            {
                Owner = this
            };

        if (nameWindow.ShowDialog() != true)
        {
            return;
        }

        string newProfileName =
            nameWindow.SubmenuName;

        if (!_profileStore.Rename(
                profileName,
                newProfileName))
        {
            MessageBox.Show(
                this,
                "The profile could not be renamed. The target name may already exist.",
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        DebugLog.Write(
            "Profiles",
            $"Renamed; OldName={profileName}; NewName={newProfileName}");

        ReloadProfiles(
            newProfileName);
    }

    private void DeleteProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileStore.ProfileListEntry profileEntry)
        {
            return;
        }

        string profileName =
            profileEntry.Name;

        MessageBoxResult result =
            MessageBox.Show(
                this,
                $"Delete profile \"{profileName}\"?",
                "GlueDock",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _profileStore.Delete(
            profileName);

        DebugLog.Write(
            "Profiles",
            $"Deleted; Name={profileName}");

        ReloadProfiles();
    }

    private void ApplyProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_profilePreviewSnapshot is null)
        {
            return;
        }

        string profileName =
            (ProfilesListBox.SelectedItem as ProfileStore.ProfileListEntry)?.Name ??
            string.Empty;

        _profilePreviewSnapshot = null;

        ApplyProfileButton.IsEnabled = false;
        CancelProfilePreviewButton.IsEnabled = false;

        DebugLog.Write(
            "Profiles",
            $"Applied; Name={profileName}");

        _settingsChanged();
    }

    private void CancelProfilePreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        CancelProfilePreview();
    }

    private void CancelProfilePreview()
    {
        if (_profilePreviewSnapshot is null)
        {
            return;
        }

        DockSettings snapshot =
            _profilePreviewSnapshot;

        _profilePreviewSnapshot = null;

        _profileStore.CopyProfileSettings(
            snapshot,
            _settings);

        _language.Load(
            _settings.LanguageCode);

        LoadSettings();

        _isLoadingProfiles = true;

        try
        {
            ProfilesListBox.SelectedItem = null;
        }
        finally
        {
            _isLoadingProfiles = false;
        }

        ApplyProfileButton.IsEnabled = false;
        CancelProfilePreviewButton.IsEnabled = false;

        DebugLog.Write(
            "Profiles",
            "Preview cancelled; previous settings restored.");

        _settingsPreviewChanged();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
