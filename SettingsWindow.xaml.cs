// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.

using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;


namespace GlueDock;

public partial class SettingsWindow : Window
{
    private readonly DockSettings _settings;
    private readonly Action _settingsChanged;
    private readonly Action _itemSpacingChanged;
    private readonly Action _settingsPreviewChanged;
    private readonly Action<string> _addWidget;
    private readonly Func<string, int> _getWidgetAddCount;
    private readonly LanguageService _language;
    private GlueDockWidgetAppearance _appearance;
    private readonly DockDialogNativeBackdropHost _nativeBackdropHost;
    private readonly ProfileStore _profileStore = new();
    private readonly List<GlueDockWidgetSettingsSection> _widgetSettingsSections = [];
    private readonly Dictionary<System.Windows.Controls.TextBlock, string> _widgetNavigationLocalizationKeys = [];
    private readonly List<System.Windows.Controls.TextBlock> _widgetInstanceLabels = [];
    private readonly Dictionary<GlueDockWidgetSettingsSection, IGlueDockWidget> _widgetSettingsSectionOwners = [];
    private readonly Dictionary<IGlueDockWidget, Dictionary<string, WidgetSettingsNavigationTarget>> _widgetSettingsNavigationTargets = [];
    private DockSettings? _profilePreviewSnapshot;
    private bool _isLoading;
    private bool _isLoadingProfiles;
    private Action? _clearSettingsSearchTargetHighlight;

    public SettingsWindow(
        DockSettings settings,
        Action settingsChanged,
        Action itemSpacingChanged,
        Action settingsPreviewChanged,
        Action<string> addWidget,
        Func<string, int> getWidgetAddCount,
        LanguageService language,
        GlueDockWidgetAppearance appearance,
        IReadOnlyList<IGlueDockWidget> widgets)
    {
        _settings = settings;
        _settingsChanged = settingsChanged;
        _itemSpacingChanged = itemSpacingChanged;
        _settingsPreviewChanged = settingsPreviewChanged;
        _addWidget = addWidget;
        _getWidgetAddCount = getWidgetAddCount;
        _language = language;
        _appearance = appearance;
        _isLoading = true;

        InitializeComponent();

        SettingsHubVersionText.Text =
            GitHubUpdateService.GetApplicationVersionText();

        _nativeBackdropHost =
            new DockDialogNativeBackdropHost(
                this);

        ApplySettingsHubAppearance();

        _language.LanguageChanged +=
            Language_LanguageChanged;

        Closed +=
            SettingsWindow_Closed;

        LoadSettings();
        ReloadProfiles();
        ReloadAvailableWidgets();
        LoadWidgetSettingsSections(
            widgets);
        ApplyLanguage();

        SelectNavigationItemForTabIndex(
            Math.Max(
                0,
                SettingsTabControl.SelectedIndex));
    }

    public void ApplyAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _appearance =
            appearance;

        ApplySettingsHubAppearance();
    }

    private void ApplySettingsHubAppearance()
    {
        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                _appearance);

        DockDialogThemeService.ApplyDialogWindowMaterial(
            WindowBorder,
            GlassSurfaceBorder,
            GlassHighlightBorder,
            _nativeBackdropHost,
            _appearance,
            palette.WindowBackgroundBrush,
            Math.Clamp(
                _settings.SettingsDialogOpacity,
                0.10,
                1.00),
            _appearance.BlurRadius);

        SettingsHubRoot.SetValue(
            System.Windows.Documents.TextElement.ForegroundProperty,
            palette.TextBrush);

        SettingsNavigationPane.Background =
            palette.ControlBackgroundBrush;

        SettingsNavigationPane.BorderBrush =
            palette.ControlBorderBrush;

        GeneralDockSettingsHeaderText.Foreground =
            palette.TextBrush;

        DockDialogThemeService.ApplyListBox(
            SettingsNavigationListBox,
            palette,
            true);

        DockDialogThemeService.ApplyListBox(
            SettingsSearchResultsListBox,
            palette,
            false);

        DockDialogThemeService.ApplyListBox(
            AvailableWidgetsListBox,
            palette,
            false);

        DockDialogThemeService.ApplyListBox(
            ProfilesListBox,
            palette,
            false);

        DockDialogThemeService.ApplyComboBox(
            LanguageComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            ThemeComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            ShortcutArrowComboBox,
            palette);

        BarColorPreview.BorderBrush =
            palette.ControlBorderBrush;

        DockBorderColorPreview.BorderBrush =
            palette.ControlBorderBrush;

        SettingsSearchResultsBorder.Background =
            palette.PopupBackgroundBrush;

        SettingsSearchResultsBorder.BorderBrush =
            palette.ControlBorderBrush;
    }

    private void SettingsNavigationListBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            SettingsNavigationListBox.SelectedItem is not System.Windows.Controls.ListBoxItem item ||
            !int.TryParse(
                item.Tag?.ToString(),
                out int selectedIndex) ||
            selectedIndex < 0 ||
            selectedIndex >= SettingsTabControl.Items.Count)
        {
            return;
        }

        SettingsTabControl.SelectedIndex =
            selectedIndex;
    }

    private void SettingsTabControl_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(
                e.Source,
                SettingsTabControl) ||
            SettingsTabControl.SelectedIndex < 0)
        {
            return;
        }

        SelectNavigationItemForTabIndex(
            SettingsTabControl.SelectedIndex);
    }

    private void SelectNavigationItemForTabIndex(
        int tabIndex)
    {
        foreach (object navigationItem in
                 SettingsNavigationListBox.Items)
        {
            if (navigationItem is not System.Windows.Controls.ListBoxItem item ||
                !int.TryParse(
                    item.Tag?.ToString(),
                    out int itemTabIndex) ||
                itemTabIndex !=
                tabIndex)
            {
                continue;
            }

            SettingsNavigationListBox.SelectedItem =
                item;

            item.BringIntoView();

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(
                    () =>
                    {
                        SettingsNavigationListBox.ScrollIntoView(
                            item);

                        item.BringIntoView();
                    }));

            return;
        }
    }

    private void LoadWidgetSettingsSections(
        IReadOnlyList<IGlueDockWidget> widgets)
    {
        foreach (IGlueDockWidget widget in widgets)
        {
            IReadOnlyList<GlueDockWidgetSettingsSection> sections;

            try
            {
                sections =
                    widget.CreateSettingsSections();
            }
            catch (Exception ex)
            {
                DebugLog.Write(
                    "Settings",
                    $"Widget settings sections failed; Widget={widget.DisplayName}; Type={ex.GetType().Name}; Message={ex.Message}");

                continue;
            }

            _widgetSettingsSections.AddRange(
                sections);

            foreach (GlueDockWidgetSettingsSection section in
                     sections)
            {
                _widgetSettingsSectionOwners[section] =
                    widget;
            }
        }

        foreach (IGrouping<string, GlueDockWidgetSettingsSection> group in
                 _widgetSettingsSections
                     .GroupBy(
                         section =>
                             section.GroupId,
                         StringComparer.OrdinalIgnoreCase)
                     .OrderBy(
                         group =>
                             group.Min(
                                 section =>
                                     section.GroupOrder))
                     .ThenBy(
                         group =>
                             group.Key,
                         StringComparer.CurrentCultureIgnoreCase))
        {
            GlueDockWidgetSettingsSection firstGroupSection =
                group.First();

            System.Windows.Controls.TextBlock groupHeaderText =
                new()
                {
                    FontWeight =
                        FontWeights.SemiBold
                };

            System.Windows.Controls.Border groupHeaderBorder =
                new()
                {
                    BorderThickness =
                        new Thickness(
                            DockDialogTheme.SettingsHubWidgetGroupBorderThickness),
                    CornerRadius =
                        DockDialogTheme.StandardCornerRadius,
                    Padding =
                        new Thickness(
                            8,
                            5,
                            8,
                            5),
                    Child =
                        groupHeaderText
                };

            groupHeaderBorder.SetBinding(
                System.Windows.Controls.Border.BorderBrushProperty,
                new System.Windows.Data.Binding(
                    nameof(SettingsNavigationPane.BorderBrush))
                {
                    Source =
                        SettingsNavigationPane
                });

            System.Windows.Controls.ListBoxItem groupHeader =
                new()
                {
                    Content =
                        groupHeaderBorder,
                    IsHitTestVisible =
                        false,
                    Focusable =
                        false,
                    HorizontalContentAlignment =
                        System.Windows.HorizontalAlignment.Stretch,
                    Padding =
                        new Thickness(
                            0),
                    Margin =
                        new Thickness(
                            0,
                            10,
                            8,
                            0)
                };

            SettingsNavigationListBox.Items.Add(
                groupHeader);

            _widgetNavigationLocalizationKeys[groupHeaderText] =
                firstGroupSection.GroupTitleKey;

            List<IGrouping<string, GlueDockWidgetSettingsSection>> sectionGroups =
                group
                    .GroupBy(
                        section =>
                            section.SectionId,
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        sectionGroup =>
                            sectionGroup.Min(
                                section =>
                                    section.SectionOrder))
                    .ThenBy(
                        sectionGroup =>
                            sectionGroup.Key,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            for (int sectionGroupIndex = 0;
                 sectionGroupIndex < sectionGroups.Count;
                 sectionGroupIndex++)
            {
                IGrouping<string, GlueDockWidgetSettingsSection> sectionGroup =
                    sectionGroups[sectionGroupIndex];

                List<GlueDockWidgetSettingsSection> sectionList =
                    sectionGroup.ToList();

                GlueDockWidgetSettingsSection firstSection =
                    sectionList[0];

                (
                    FrameworkElement sectionContent,
                    System.Windows.Controls.ComboBox? instanceSelector
                ) =
                    CreateWidgetSettingsSectionContent(
                        sectionList);

                System.Windows.Controls.TabItem tabItem =
                    new()
                    {
                        Content =
                            sectionContent
                    };

                SettingsTabControl.Items.Add(
                    tabItem);

                int tabIndex =
                    SettingsTabControl.Items.IndexOf(
                        tabItem);

                for (int instanceIndex = 0;
                     instanceIndex < sectionList.Count;
                     instanceIndex++)
                {
                    GlueDockWidgetSettingsSection section =
                        sectionList[instanceIndex];

                    if (!_widgetSettingsSectionOwners.TryGetValue(
                            section,
                            out IGlueDockWidget? widget))
                    {
                        continue;
                    }

                    if (!_widgetSettingsNavigationTargets.TryGetValue(
                            widget,
                            out Dictionary<string, WidgetSettingsNavigationTarget>? widgetTargets))
                    {
                        widgetTargets =
                            new Dictionary<string, WidgetSettingsNavigationTarget>(
                                StringComparer.OrdinalIgnoreCase);

                        _widgetSettingsNavigationTargets[widget] =
                            widgetTargets;
                    }

                    widgetTargets[section.SectionId] =
                        new WidgetSettingsNavigationTarget(
                            tabIndex,
                            instanceSelector,
                            instanceIndex);
                }

                System.Windows.Controls.TextBlock navigationItemText =
                    new();

                System.Windows.Controls.Border navigationItemBorder =
                    new()
                    {
                        Padding =
                            new Thickness(
                                8,
                                5,
                                8,
                                5),
                        Margin =
                            new Thickness(
                                DockDialogTheme.SettingsHubWidgetSectionIndent,
                                0,
                                0,
                                0),
                        Child =
                            navigationItemText
                    };

                System.Windows.Controls.ListBoxItem navigationItem =
                    new()
                    {
                        Tag =
                            tabIndex.ToString(
                                System.Globalization.CultureInfo.InvariantCulture),
                        Content =
                            navigationItemBorder,
                        HorizontalContentAlignment =
                            System.Windows.HorizontalAlignment.Stretch,
                        Padding =
                            new Thickness(
                                0)
                    };

                SettingsNavigationListBox.Items.Add(
                    navigationItem);

                _widgetNavigationLocalizationKeys[navigationItemText] =
                    firstSection.SectionTitleKey;

                tabItem.Tag =
                    firstSection.SectionTitleKey;
            }
        }
    }

    private (
        FrameworkElement Content,
        System.Windows.Controls.ComboBox? InstanceSelector
    ) CreateWidgetSettingsSectionContent(
        IReadOnlyList<GlueDockWidgetSettingsSection> sections)
    {
        if (sections.Count ==
            1)
        {
            return (
                CreateWidgetSettingsScrollViewer(
                    sections[0].Content),
                null);
        }

        System.Windows.Controls.Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        18)
            };

        root.RowDefinitions.Add(
            new System.Windows.Controls.RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new System.Windows.Controls.RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        System.Windows.Controls.StackPanel selectorRow =
            new()
            {
                Orientation =
                    System.Windows.Controls.Orientation.Horizontal,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        System.Windows.Controls.TextBlock selectorLabel =
            new()
            {
                Text =
                    _language["Settings.WidgetSection.Instance"],
                FontWeight =
                    FontWeights.SemiBold,
                Width =
                    DockDialogTheme.LabelColumnWidth.Value,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        _widgetInstanceLabels.Add(
            selectorLabel);

        System.Windows.Controls.ComboBox selector =
            new()
            {
                Width =
                    120,
                HorizontalAlignment =
                    System.Windows.HorizontalAlignment.Left
            };

        for (int index = 0;
             index < sections.Count;
             index++)
        {
            selector.Items.Add(
                (index + 1)
                    .ToString(
                        System.Globalization.CultureInfo.CurrentCulture));
        }

        System.Windows.Controls.ContentControl contentHost =
            new();

        IReadOnlyList<FrameworkElement> instanceContents =
            sections
                .Select(
                    section =>
                        (FrameworkElement)CreateWidgetSettingsScrollViewer(
                            section.Content))
                .ToArray();

        selector.SelectionChanged +=
            (_, _) =>
            {
                int selectedIndex =
                    selector.SelectedIndex;

                contentHost.Content =
                    selectedIndex >= 0 &&
                    selectedIndex < sections.Count
                        ? instanceContents[selectedIndex]
                        : null;
            };

        selectorRow.Children.Add(
            selectorLabel);

        selectorRow.Children.Add(
            selector);

        System.Windows.Controls.Grid.SetRow(
            selectorRow,
            0);

        System.Windows.Controls.Grid.SetRow(
            contentHost,
            1);

        root.Children.Add(
            selectorRow);

        root.Children.Add(
            contentHost);

        selector.SelectedIndex =
            0;

        return (
            root,
            selector);
    }

    public bool OpenWidgetSettingsSection(
        IGlueDockWidget widget,
        string sectionId)
    {
        if (!_widgetSettingsNavigationTargets.TryGetValue(
                widget,
                out Dictionary<string, WidgetSettingsNavigationTarget>? widgetTargets) ||
            !widgetTargets.TryGetValue(
                sectionId,
                out WidgetSettingsNavigationTarget? target))
        {
            return false;
        }

        SettingsTabControl.SelectedIndex =
            target.TabIndex;

        if (target.InstanceSelector is not null)
        {
            target.InstanceSelector.SelectedIndex =
                target.InstanceIndex;
        }

        SelectNavigationItemForTabIndex(
            target.TabIndex);

        return true;
    }

    private sealed class WidgetSettingsNavigationTarget
    {
        public WidgetSettingsNavigationTarget(
            int tabIndex,
            System.Windows.Controls.ComboBox? instanceSelector,
            int instanceIndex)
        {
            TabIndex =
                tabIndex;
            InstanceSelector =
                instanceSelector;
            InstanceIndex =
                instanceIndex;
        }

        public int TabIndex { get; }

        public System.Windows.Controls.ComboBox? InstanceSelector { get; }

        public int InstanceIndex { get; }
    }

    private static System.Windows.Controls.ScrollViewer CreateWidgetSettingsScrollViewer(
        FrameworkElement content)
    {
        return new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility =
                System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility =
                System.Windows.Controls.ScrollBarVisibility.Disabled,
            Background =
                System.Windows.Media.Brushes.Transparent,
            Margin =
                new Thickness(
                    18),
            Content =
                content
        };
    }

    private void LoadSettings()
    {
        _isLoading = true;

        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        AlwaysOnTopCheckBox.IsChecked = _settings.AlwaysOnTop;
        CollapseDisabledCheckBox.IsChecked = _settings.CollapseDisabled;
        SubdockOpenOnClickOnlyCheckBox.IsChecked = _settings.SubdockOpenOnClickOnly;
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
        RootMaxColumnsTextBox.Text =
            Math.Clamp(
                _settings.RootMaxColumns,
                1,
                50)
                .ToString();
        RootMaxRowsTextBox.Text =
            Math.Clamp(
                _settings.RootMaxRows,
                1,
                50)
                .ToString();
        SubDockMaxColumnsTextBox.Text =
            Math.Clamp(
                _settings.SubDockMaxColumns,
                1,
                50)
                .ToString();
        SubDockMaxRowsTextBox.Text =
            Math.Clamp(
                _settings.SubDockMaxRows,
                1,
                50)
                .ToString();
        DockThicknessScaleSlider.Value = _settings.DockThicknessScale;
        SubDockScaleSlider.Value =
            Math.Clamp(
                _settings.SubDockScale ??
                _settings.DockScale,
                0.50,
                1.50);
        SubDockThicknessScaleSlider.Value =
            Math.Clamp(
                _settings.SubDockThicknessScale ??
                _settings.DockThicknessScale,
                0.30,
                1.50);
        SubDockItemSpacingSlider.Value =
            Math.Clamp(
                (_settings.SubDockItemSpacing ??
                 _settings.ItemSpacing) +
                18.0,
                10.0,
                60.0);

        foreach (System.Windows.Controls.ComboBoxItem item
            in SubDockThemeModeComboBox.Items)
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    _settings.SubDockThemeMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                SubDockThemeModeComboBox.SelectedItem =
                    item;
                break;
            }
        }

        if (SubDockThemeModeComboBox.SelectedIndex < 0)
        {
            SubDockThemeModeComboBox.SelectedItem =
                SubDockThemeInheritFullItem;

            _settings.SubDockThemeMode =
                DockSettings.SubDockThemeModeInheritFull;
        }

        ShowItemLabelsCheckBox.IsChecked = _settings.ShowItemLabels;
        ShowRootDockTooltipsCheckBox.IsChecked = _settings.ShowRootDockTooltips;
        ShowSubDockTooltipsCheckBox.IsChecked = _settings.ShowSubDockTooltips;
        ShowFilePreviewsCheckBox.IsChecked = _settings.ShowFilePreviews;
        foreach (System.Windows.Controls.ComboBoxItem item in ShortcutArrowComboBox.Items)
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    _settings.ShortcutOverlayMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                ShortcutArrowComboBox.SelectedItem = item;
                break;
            }
        }

        if (ShortcutArrowComboBox.SelectedIndex < 0)
        {
            ShortcutArrowComboBox.SelectedItem =
                ShortcutArrowDefaultItem;

            _settings.ShortcutOverlayMode =
                DockSettings.ShortcutOverlayDefault;
        }

        ReloadThemeOptions();
        UpdateSubDockThemeControls();

        UpdateSubmenuIconPreview();

        DebugLoggingCheckBox.IsChecked = _settings.DebugLoggingEnabled;
        DebugLogMaxSizeSlider.Value =
            Math.Clamp(
                _settings.DebugLogMaxSizeMegabytes,
                1,
                10);
        SettingsDialogOpacitySlider.Value =
            Math.Clamp(
                _settings.SettingsDialogOpacity,
                0.10,
                1.00);
        OpacitySlider.Value = _settings.Opacity;
        CollapsedBarOpacitySlider.Value = _settings.CollapsedBarOpacity;
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

    private void AlwaysOnTopCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.AlwaysOnTop =
            AlwaysOnTopCheckBox.IsChecked == true;

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

    private void SubdockOpenOnClickOnlyCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SubdockOpenOnClickOnly =
            SubdockOpenOnClickOnlyCheckBox.IsChecked == true;

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

    private void ShowRootDockTooltipsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.ShowRootDockTooltips =
            ShowRootDockTooltipsCheckBox.IsChecked == true;

        ApplyLive();
    }

    private void ShowSubDockTooltipsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.ShowSubDockTooltips =
            ShowSubDockTooltipsCheckBox.IsChecked == true;

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

    private void SubDockThemeModeComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            SubDockThemeModeComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item ||
            item.Tag is not string mode)
        {
            return;
        }

        _settings.SubDockThemeMode =
            mode;

        UpdateSubDockThemeControls();
        ApplyLive();
    }

    private void SubDockThemeComboBox_DropDownOpened(
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

    private void SubDockThemeComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            SubDockThemeComboBox.SelectedValue is not string themeName)
        {
            return;
        }

        _settings.SubDockThemeName =
            themeName;

        if (string.Equals(
                _settings.SubDockThemeMode,
                DockSettings.SubDockThemeModeOverride,
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyLive();
        }
    }

    private void ReloadThemeOptions()
    {
        IReadOnlyList<DockThemeOption> themes =
            DockThemeService.GetAvailableThemes();

        ThemeComboBox.ItemsSource =
            themes;

        ThemeComboBox.SelectedValue =
            _settings.ThemeName;

        if (ThemeComboBox.SelectedIndex < 0)
        {
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
            }
            else if (themes.Count > 0)
            {
                ThemeComboBox.SelectedIndex = 0;

                if (ThemeComboBox.SelectedValue is string firstThemeName)
                {
                    _settings.ThemeName =
                        firstThemeName;
                }
            }
        }

        SubDockThemeComboBox.ItemsSource =
            themes;

        SubDockThemeComboBox.SelectedValue =
            _settings.SubDockThemeName;

        if (SubDockThemeComboBox.SelectedIndex < 0)
        {
            DockThemeOption? defaultTheme =
                themes.FirstOrDefault(
                    theme =>
                        string.Equals(
                            theme.Id,
                            "Default",
                            StringComparison.OrdinalIgnoreCase));

            if (defaultTheme is not null)
            {
                SubDockThemeComboBox.SelectedValue =
                    defaultTheme.Id;

                _settings.SubDockThemeName =
                    defaultTheme.Id;
            }
            else if (themes.Count > 0)
            {
                SubDockThemeComboBox.SelectedIndex = 0;

                if (SubDockThemeComboBox.SelectedValue is string firstThemeName)
                {
                    _settings.SubDockThemeName =
                        firstThemeName;
                }
            }
        }
    }

    private void UpdateSubDockThemeControls()
    {
        bool isOverride =
            string.Equals(
                _settings.SubDockThemeMode,
                DockSettings.SubDockThemeModeOverride,
                StringComparison.OrdinalIgnoreCase);

        SubDockThemeComboBox.IsEnabled =
            isOverride;

        SubDockThemeOverrideLabel.Opacity =
            isOverride
                ? 1
                : 0.55;

        SubDockThemeComboBox.Opacity =
            isOverride
                ? 1
                : 0.55;
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

        // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
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

        Drawing.Color selectedColor =
            dialog.Color;

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

    private void SubDockScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SubDockScale =
            SubDockScaleSlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void SubDockThicknessScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SubDockThicknessScale =
            SubDockThicknessScaleSlider.Value;

        UpdateValueTexts();
        ApplyLive();
    }

    private void SubDockItemSpacingSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SubDockItemSpacing =
            SubDockItemSpacingSlider.Value -
            18.0;

        UpdateValueTexts();
        ApplyLive();
    }

    private void RootMaxColumnsTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading ||
            !TryGetLayoutValue(
                RootMaxColumnsTextBox,
                out int value))
        {
            return;
        }

        _settings.RootMaxColumns = value;
        ApplyLive();
    }

    private void RootMaxRowsTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading ||
            !TryGetLayoutValue(
                RootMaxRowsTextBox,
                out int value))
        {
            return;
        }

        _settings.RootMaxRows = value;
        ApplyLive();
    }

    private void SubDockMaxColumnsTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading ||
            !TryGetLayoutValue(
                SubDockMaxColumnsTextBox,
                out int value))
        {
            return;
        }

        _settings.SubDockMaxColumns = value;
        ApplyLive();
    }

    private void SubDockMaxRowsTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading ||
            !TryGetLayoutValue(
                SubDockMaxRowsTextBox,
                out int value))
        {
            return;
        }

        _settings.SubDockMaxRows = value;
        ApplyLive();
    }

    private void RootMaxColumnsUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            RootMaxColumnsTextBox,
            Math.Clamp(
                _settings.RootMaxColumns + 1,
                1,
                50));
    }

    private void RootMaxColumnsDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            RootMaxColumnsTextBox,
            Math.Clamp(
                _settings.RootMaxColumns - 1,
                1,
                50));
    }

    private void RootMaxRowsUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            RootMaxRowsTextBox,
            Math.Clamp(
                _settings.RootMaxRows + 1,
                1,
                50));
    }

    private void RootMaxRowsDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            RootMaxRowsTextBox,
            Math.Clamp(
                _settings.RootMaxRows - 1,
                1,
                50));
    }

    private void SubDockMaxColumnsUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            SubDockMaxColumnsTextBox,
            Math.Clamp(
                _settings.SubDockMaxColumns + 1,
                1,
                50));
    }

    private void SubDockMaxColumnsDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            SubDockMaxColumnsTextBox,
            Math.Clamp(
                _settings.SubDockMaxColumns - 1,
                1,
                50));
    }

    private void SubDockMaxRowsUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            SubDockMaxRowsTextBox,
            Math.Clamp(
                _settings.SubDockMaxRows + 1,
                1,
                50));
    }

    private void SubDockMaxRowsDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLayoutValue(
            SubDockMaxRowsTextBox,
            Math.Clamp(
                _settings.SubDockMaxRows - 1,
                1,
                50));
    }

    private void LayoutValueTextBox_LostKeyboardFocus(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        int value =
            ReferenceEquals(
                textBox,
                RootMaxColumnsTextBox)
                ? _settings.RootMaxColumns
                : ReferenceEquals(
                    textBox,
                    RootMaxRowsTextBox)
                    ? _settings.RootMaxRows
                    : ReferenceEquals(
                        textBox,
                        SubDockMaxColumnsTextBox)
                        ? _settings.SubDockMaxColumns
                        : _settings.SubDockMaxRows;

        SetLayoutValue(
            textBox,
            Math.Clamp(
                value,
                1,
                50));
    }

    private static bool TryGetLayoutValue(
        System.Windows.Controls.TextBox textBox,
        out int value)
    {
        if (!int.TryParse(
                textBox.Text,
                out value) ||
            value < 1 ||
            value > 50)
        {
            value = 0;
            return false;
        }

        return true;
    }

    private void SetLayoutValue(
        System.Windows.Controls.TextBox textBox,
        int value)
    {
        string text =
            Math.Clamp(
                value,
                1,
                50)
                .ToString();

        if (!string.Equals(
                textBox.Text,
                text,
                StringComparison.Ordinal))
        {
            textBox.Text = text;
        }
        else
        {
            if (ReferenceEquals(
                    textBox,
                    RootMaxColumnsTextBox))
            {
                _settings.RootMaxColumns = value;
            }
            else if (ReferenceEquals(
                         textBox,
                         RootMaxRowsTextBox))
            {
                _settings.RootMaxRows = value;
            }
            else if (ReferenceEquals(
                         textBox,
                         SubDockMaxColumnsTextBox))
            {
                _settings.SubDockMaxColumns = value;
            }
            else
            {
                _settings.SubDockMaxRows = value;
            }

            ApplyLive();
        }

        textBox.CaretIndex =
            textBox.Text.Length;
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

    private void ShortcutArrowComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            ShortcutArrowComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item ||
            item.Tag is not string shortcutOverlayMode)
        {
            return;
        }

        _settings.ShortcutOverlayMode =
            shortcutOverlayMode;

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

    private void SettingsDialogOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SettingsDialogOpacity =
            Math.Clamp(
                SettingsDialogOpacitySlider.Value,
                0.10,
                1.00);

        UpdateValueTexts();
        ApplySettingsHubAppearance();
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

    private void CollapsedBarOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.CollapsedBarOpacity =
            CollapsedBarOpacitySlider.Value;

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

    private void ReloadAvailableWidgets()
    {
        AvailableWidgetsListBox.ItemsSource =
            WidgetPluginLoader.GetAvailableWidgets()
                .Select(
                    widget =>
                        new WidgetPluginDisplayInfo(
                            widget,
                            _getWidgetAddCount(
                                widget.AssemblyPath),
                            string.IsNullOrWhiteSpace(
                                widget.DisplayNameKey)
                                ? widget.DisplayName
                                : _language[
                                    widget.DisplayNameKey],
                            string.IsNullOrWhiteSpace(
                                widget.DescriptionKey)
                                ? widget.Description
                                : _language[
                                    widget.DescriptionKey]))
                .ToList();

        AddSelectedWidgetButton.IsEnabled =
            AvailableWidgetsListBox.SelectedItem is WidgetPluginDisplayInfo;
    }

    private void AvailableWidgetsListBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        AddSelectedWidgetButton.IsEnabled =
            AvailableWidgetsListBox.SelectedItem is WidgetPluginDisplayInfo;
    }

    private void AddSelectedWidgetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AvailableWidgetsListBox.SelectedItem is not WidgetPluginDisplayInfo widgetInfo)
        {
            return;
        }

        int existingCount =
            _getWidgetAddCount(
                widgetInfo.AssemblyPath);

        if (existingCount > 0)
        {
            MessageBoxResult result =
                MessageBox.Show(
                    this,
                    string.Format(
                        _language["Settings.Widgets.DuplicatePrompt"],
                        existingCount),
                    _language["Settings.Widgets.DuplicateTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

            if (result !=
                MessageBoxResult.Yes)
            {
                return;
            }
        }

        _addWidget(
            widgetInfo.AssemblyPath);

        ReloadAvailableWidgets();
    }

    private void SettingsSearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        ClearSettingsSearchTargetHighlight();

        string searchText =
            SettingsSearchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                searchText))
        {
            SettingsSearchResultsListBox.ItemsSource =
                null;

            SettingsSearchResultsPopup.IsOpen =
                false;

            return;
        }

        List<SettingsSearchResult> results =
            [];

        foreach (object item in SettingsTabControl.Items)
        {
            if (item is not System.Windows.Controls.TabItem tabItem)
            {
                continue;
            }

            string tabText =
                tabItem.Header?.ToString() ??
                string.Empty;

            if (ContainsSearchText(
                    tabText,
                    searchText))
            {
                results.Add(
                    new SettingsSearchResult(
                        tabItem,
                        tabItem,
                        tabText,
                        tabText));
            }

            CollectSettingsSearchResults(
                tabItem.Content,
                tabItem,
                tabText,
                searchText,
                results);
        }

        foreach (SettingsSearchResult result in
                 results)
        {
            result.DisplayContent =
                CreateSettingsSearchResultDisplay(
                    result.DisplayText,
                    searchText);
        }

        SettingsSearchResultsListBox.ItemsSource =
            results;

        SettingsSearchResultsPopup.IsOpen =
            results.Count > 0;
    }

    private static System.Windows.Controls.TextBlock CreateSettingsSearchResultDisplay(
        string displayText,
        string searchText)
    {
        System.Windows.Controls.TextBlock textBlock =
            new()
            {
                TextWrapping =
                    TextWrapping.Wrap
            };

        ApplySettingsSearchHighlight(
            textBlock,
            displayText,
            searchText);

        return textBlock;
    }

    private static void ApplySettingsSearchHighlight(
        System.Windows.Controls.TextBlock textBlock,
        string displayText,
        string searchText)
    {
        textBlock.Inlines.Clear();

        int currentIndex =
            0;

        while (currentIndex <
               displayText.Length)
        {
            int matchIndex =
                displayText.IndexOf(
                    searchText,
                    currentIndex,
                    StringComparison.CurrentCultureIgnoreCase);

            if (matchIndex <
                0)
            {
                textBlock.Inlines.Add(
                    new System.Windows.Documents.Run(
                        displayText[currentIndex..]));

                break;
            }

            if (matchIndex >
                currentIndex)
            {
                textBlock.Inlines.Add(
                    new System.Windows.Documents.Run(
                        displayText[currentIndex..matchIndex]));
            }

            textBlock.Inlines.Add(
                new System.Windows.Documents.Run(
                    displayText.Substring(
                        matchIndex,
                        searchText.Length))
                {
                    Background =
                        DockDialogTheme.SettingsSearchHighlightBrush
                });

            currentIndex =
                matchIndex +
                searchText.Length;
        }
    }

    private void ApplySettingsSearchTargetHighlight(
        FrameworkElement target,
        string searchText)
    {
        ClearSettingsSearchTargetHighlight();

        if (string.IsNullOrWhiteSpace(
                searchText))
        {
            return;
        }

        if (target is System.Windows.Controls.TextBlock textBlock &&
            ContainsSearchText(
                textBlock.Text,
                searchText))
        {
            string originalText =
                textBlock.Text;

            ApplySettingsSearchHighlight(
                textBlock,
                originalText,
                searchText);

            _clearSettingsSearchTargetHighlight =
                () =>
                {
                    textBlock.Inlines.Clear();
                    textBlock.Text =
                        originalText;
                };

            return;
        }

        if (target is System.Windows.Controls.HeaderedContentControl headeredContentControl &&
            headeredContentControl.Header is string headerText &&
            ContainsSearchText(
                headerText,
                searchText))
        {
            System.Windows.Controls.TextBlock highlightedHeader =
                CreateSettingsSearchResultDisplay(
                    headerText,
                    searchText);

            headeredContentControl.Header =
                highlightedHeader;

            _clearSettingsSearchTargetHighlight =
                () =>
                {
                    if (ReferenceEquals(
                            headeredContentControl.Header,
                            highlightedHeader))
                    {
                        headeredContentControl.Header =
                            headerText;
                    }
                };

            return;
        }

        if (target is System.Windows.Controls.ContentControl contentControl &&
            contentControl.Content is string contentText &&
            ContainsSearchText(
                contentText,
                searchText))
        {
            System.Windows.Controls.TextBlock highlightedContent =
                CreateSettingsSearchResultDisplay(
                    contentText,
                    searchText);

            contentControl.Content =
                highlightedContent;

            _clearSettingsSearchTargetHighlight =
                () =>
                {
                    if (ReferenceEquals(
                            contentControl.Content,
                            highlightedContent))
                    {
                        contentControl.Content =
                            contentText;
                    }
                };
        }
    }

    private void ClearSettingsSearchTargetHighlight()
    {
        Action? clearHighlight =
            _clearSettingsSearchTargetHighlight;

        _clearSettingsSearchTargetHighlight =
            null;

        clearHighlight?.Invoke();
    }

    private static void CollectSettingsSearchResults(
        object? root,
        System.Windows.Controls.TabItem tabItem,
        string tabText,
        string searchText,
        List<SettingsSearchResult> results)
    {
        if (root is null)
        {
            return;
        }

        if (root is System.Windows.Controls.TextBlock textBlock &&
            ContainsSearchText(
                textBlock.Text,
                searchText))
        {
            AddSettingsSearchResult(
                results,
                tabItem,
                textBlock,
                tabText,
                textBlock.Text);
        }
        else if (root is System.Windows.Controls.HeaderedContentControl headeredContentControl &&
                 ContainsSearchText(
                     headeredContentControl.Header?.ToString(),
                     searchText))
        {
            AddSettingsSearchResult(
                results,
                tabItem,
                headeredContentControl,
                tabText,
                headeredContentControl.Header?.ToString());
        }
        else if (root is System.Windows.Controls.ContentControl contentControl &&
                 contentControl.Content is string contentText &&
                 ContainsSearchText(
                     contentText,
                     searchText))
        {
            AddSettingsSearchResult(
                results,
                tabItem,
                contentControl,
                tabText,
                contentText);
        }

        if (root is DependencyObject dependencyObject)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(
                         dependencyObject))
            {
                CollectSettingsSearchResults(
                    child,
                    tabItem,
                    tabText,
                    searchText,
                    results);
            }
        }
    }

    private static void AddSettingsSearchResult(
        List<SettingsSearchResult> results,
        System.Windows.Controls.TabItem tabItem,
        FrameworkElement target,
        string tabText,
        string? resultText)
    {
        if (string.IsNullOrWhiteSpace(
                resultText) ||
            results.Any(
                result =>
                    ReferenceEquals(
                        result.Target,
                        target) &&
                    string.Equals(
                        result.Text,
                        resultText,
                        StringComparison.CurrentCulture)))
        {
            return;
        }

        results.Add(
            new SettingsSearchResult(
                tabItem,
                target,
                tabText,
                resultText));
    }

    private void SettingsSearchResultsListBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SettingsSearchResultsListBox.SelectedItem is not SettingsSearchResult result)
        {
            return;
        }

        result.TabItem.IsSelected =
            true;

        string searchText =
            SettingsSearchTextBox.Text.Trim();

        Dispatcher.BeginInvoke(
            () =>
            {
                ApplySettingsSearchTargetHighlight(
                    result.Target,
                    searchText);

                result.Target.BringIntoView();

                if (result.Target.Focusable)
                {
                    result.Target.Focus();
                }
            });

        SettingsSearchResultsPopup.IsOpen =
            false;

        SettingsSearchResultsListBox.SelectedItem =
            null;
    }

    private static bool ContainsSearchText(
        string? value,
        string searchText)
    {
        return !string.IsNullOrWhiteSpace(
                   value) &&
               value.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase);
    }

    private void BrowseWidgetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string widgetDirectory =
            System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Widgets");

        System.IO.Directory.CreateDirectory(
            widgetDirectory);

        Microsoft.Win32.OpenFileDialog dialog =
            new()
            {
                Title =
                    _language["Settings.Widgets.Browse"],
                Filter =
                    "DLL files (*.dll)|*.dll",
                InitialDirectory =
                    widgetDirectory,
                CheckFileExists = true,
                Multiselect = false
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _addWidget(
            dialog.FileName);
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

        foreach (GlueDockWidgetSettingsSection section in
                 _widgetSettingsSections)
        {
            section.Dispose();
        }

        _widgetSettingsSections.Clear();

        _nativeBackdropHost.Dispose();
    }

    private void CreateBackupButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        CancelProfilePreview();

        _settingsChanged();

        SaveFileDialog dialog =
            new()
            {
                Title =
                    _language["Settings.BackupRestore.BackupDialogTitle"],
                Filter =
                    "GlueDock backup (*.zip)|*.zip",
                FileName =
                    $"GlueDock_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                AddExtension = true,
                DefaultExt = ".zip",
                OverwritePrompt = true
            };

        if (dialog.ShowDialog(
                this) != true)
        {
            return;
        }

        try
        {
            BackupService.CreateBackup(
                dialog.FileName);

            MessageBox.Show(
                this,
                _language["Settings.BackupRestore.BackupSuccess"],
                _language["Settings.BackupRestore.Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                string.Format(
                    _language["Settings.BackupRestore.BackupFailed"],
                    ex.Message),
                _language["Settings.BackupRestore.Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RestoreBackupButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenFileDialog dialog =
            new()
            {
                Title =
                    _language["Settings.BackupRestore.RestoreDialogTitle"],
                Filter =
                    "GlueDock backup (*.zip)|*.zip",
                CheckFileExists = true,
                Multiselect = false
            };

        if (dialog.ShowDialog(
                this) != true)
        {
            return;
        }

        MessageBoxResult confirmation =
            MessageBox.Show(
                this,
                _language["Settings.BackupRestore.RestoreConfirm"],
                _language["Settings.BackupRestore.Title"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        if (confirmation !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            BackupService.QueueRestore(
                dialog.FileName);

            MessageBox.Show(
                this,
                _language["Settings.BackupRestore.RestoreQueued"],
                _language["Settings.BackupRestore.Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                string.Format(
                    _language["Settings.BackupRestore.RestoreFailed"],
                    ex.Message),
                _language["Settings.BackupRestore.Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyLanguage()
    {
        ClearSettingsSearchTargetHighlight();

        Title =
            _language["Settings.Title"];

        SettingsHubTitleText.Text =
            Title;

        GeneralDockSettingsHeaderText.Text =
            _language["Settings.Navigation.GeneralDockSettings"];

        GeneralNavigationItem.Content =
            _language["Settings.Tab.General"];

        AppearanceNavigationItem.Content =
            _language["Settings.Tab.Appearance"];

        BehaviorNavigationItem.Content =
            _language["Settings.Tab.RootDock"];

        DockNavigationItem.Content =
            _language["Settings.Tab.SubDock"];

        ProfilesNavigationItem.Content =
            _language["Settings.Tab.Profiles"];

        BackupRestoreNavigationItem.Content =
            _language["Settings.BackupRestore.Title"];

        AdvancedNavigationItem.Content =
            _language["Settings.Tab.Advanced"];

        WidgetsNavigationItem.Content =
            _language["Settings.Tab.Widgets"];

        GeneralTab.Header =
            _language["Settings.Tab.General"];

        AppearanceTab.Header =
            _language["Settings.Tab.Appearance"];

        BehaviorTab.Header =
            _language["Settings.Tab.RootDock"];

        DockTab.Header =
            _language["Settings.Tab.SubDock"];

        ProfilesTab.Header =
            _language["Settings.Tab.Profiles"];

        BackupRestoreTab.Header =
            _language["Settings.BackupRestore.Title"];

        AdvancedTab.Header =
            _language["Settings.Tab.Advanced"];

        WidgetsTab.Header =
            _language["Settings.Tab.Widgets"];

        foreach (KeyValuePair<System.Windows.Controls.TextBlock, string> navigationEntry in
                 _widgetNavigationLocalizationKeys)
        {
            navigationEntry.Key.Text =
                _language[navigationEntry.Value];
        }

        foreach (System.Windows.Controls.TextBlock instanceLabel in
                 _widgetInstanceLabels)
        {
            instanceLabel.Text =
                _language["Settings.WidgetSection.Instance"];
        }

        foreach (object tabObject in
                 SettingsTabControl.Items)
        {
            if (tabObject is System.Windows.Controls.TabItem widgetTab &&
                widgetTab.Tag is string titleKey)
            {
                widgetTab.Header =
                    _language[titleKey];
            }
        }

        StartWithWindowsCheckBox.Content =
            _language["Settings.StartWithWindows"];

        AlwaysOnTopCheckBox.Content =
            _language["Settings.AlwaysOnTop"];

        CollapseDisabledCheckBox.Content =
            _language["Settings.DisableCollapse"];

        SubdockOpenOnClickOnlyCheckBox.Content =
            _language["Settings.SubdockOpenOnClickOnly"];

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

        CollapsedBarOpacityLabel.Text =
            _language["Settings.CollapsedBarOpacity"];

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

        RootLayoutLabel.Text =
            _language["Settings.RootLayout"];

        RootMaxColumnsLabel.Text =
            _language["Settings.MaxColumns"];

        RootMaxRowsLabel.Text =
            _language["Settings.MaxRows"];

        SubDockLayoutLabel.Text =
            _language["Settings.SubDockLayout"];

        SubDockThemeModeLabel.Text =
            _language["Settings.SubDockThemeMode"];

        SubDockThemeOverrideLabel.Text =
            _language["Settings.SubDockThemeOverride"];

        SubDockThemeInheritFullItem.Content =
            _language["Settings.SubDockThemeMode.InheritFull"];

        SubDockThemeInheritAppearanceItem.Content =
            _language["Settings.SubDockThemeMode.InheritAppearance"];

        SubDockThemeOverrideItem.Content =
            _language["Settings.SubDockThemeMode.Override"];

        SubDockScaleLabel.Text =
            _language["Settings.SubDockScale"];

        SubDockThicknessScaleLabel.Text =
            _language["Settings.SubDockThicknessScale"];

        SubDockItemSpacingLabel.Text =
            _language["Settings.SubDockItemSpacing"];

        SubDockMaxColumnsLabel.Text =
            _language["Settings.MaxColumns"];

        SubDockMaxRowsLabel.Text =
            _language["Settings.MaxRows"];

        DockThicknessScaleLabel.Text =
            _language["Settings.DockThicknessScale"];

        ShowItemLabelsCheckBox.Content =
            _language["Settings.ShowItemLabels"];

        ShowFilePreviewsCheckBox.Content =
            _language["Settings.ShowFilePreviews"];

        ShortcutArrowLabel.Text =
            _language["Settings.ShortcutArrow"];

        ShortcutArrowDefaultItem.Content =
            _language["Settings.ShortcutArrow.Default"];

        ShortcutArrowSmallItem.Content =
            _language["Settings.ShortcutArrow.Small"];

        ShortcutArrowNoneItem.Content =
            _language["Settings.ShortcutArrow.None"];

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

        SettingsDialogOpacityLabel.Text =
            _language["Settings.SettingsDialogOpacity"];

        OpacityLabel.Text =
            _language["Settings.Opacity"];

        BlurLabel.Text =
            _language["Settings.Blur"];

        AnimationLabel.Text =
            _language["Settings.Animation"];

        HoverEffectLabel.Text =
            _language["Settings.HoverEffect"];

        AnimationFadeItem.Content =
            _language["Settings.Animation.Fade"];

        AnimationSlideItem.Content =
            _language["Settings.Animation.Slide"];

        AnimationZoomItem.Content =
            _language["Settings.Animation.Zoom"];

        HoverNoneItem.Content =
            _language["Settings.HoverEffect.None"];

        HoverZoomItem.Content =
            _language["Settings.HoverEffect.Zoom"];

        HoverGlowItem.Content =
            _language["Settings.HoverEffect.Glow"];

        HoverHighlightItem.Content =
            _language["Settings.HoverEffect.Highlight"];

        ShowRootDockTooltipsCheckBox.Content =
            _language["Settings.ShowRootDockTooltips"];

        ShowSubDockTooltipsCheckBox.Content =
            _language["Settings.ShowSubDockTooltips"];

        AvailableWidgetsLabel.Text =
            _language["Settings.Widgets.Available"];

        WidgetNameColumn.Text =
            _language["Settings.Widgets.Column.Name"];

        WidgetDescriptionColumn.Text =
            _language["Settings.Widgets.Column.Description"];

        WidgetAddedColumn.Text =
            _language["Settings.Widgets.Column.Added"];

        AddSelectedWidgetButton.Content =
            _language["Settings.Widgets.AddSelected"];

        BrowseWidgetButton.Content =
            _language["Settings.Widgets.Browse"];

        ProfilesInfoText.Text =
            _language["Settings.Profiles.Info"];

        NewProfileButton.Content =
            _language["Settings.Profiles.New"];

        SaveProfileButton.Content =
            _language["Settings.Profiles.SaveCurrent"];

        RenameProfileButton.Content =
            _language["Settings.Profiles.Rename"];

        DeleteProfileButton.Content =
            _language["Settings.Profiles.Delete"];

        CancelProfilePreviewButton.Content =
            _language["Settings.Profiles.CancelPreview"];

        ApplyProfileButton.Content =
            _language["Settings.Profiles.Apply"];

        DebugLoggingCheckBox.Content =
            _language["Settings.DebugLogging"];

        DebugLoggingInfoText.Text =
            _language["Settings.DebugLoggingInfo"];

        DebugLogMaxSizeLabel.Text =
            _language["Settings.DebugLogMaxSize"];

        BackupRestoreInfoText.Text =
            _language["Settings.BackupRestore.Info"];

        CreateBackupButton.Content =
            _language["Settings.BackupRestore.Create"];

        RestoreBackupButton.Content =
            _language["Settings.BackupRestore.Restore"];

        CloseButton.Content =
            _language["Settings.Close"];

        ReloadAvailableWidgets();
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

        SubDockScaleText.Text =
            $"{Math.Clamp(_settings.SubDockScale ?? _settings.DockScale, 0.50, 1.50) * 100:0}%";

        SubDockThicknessScaleText.Text =
            $"{Math.Clamp(_settings.SubDockThicknessScale ?? _settings.DockThicknessScale, 0.30, 1.50) * 100:0}%";

        SubDockItemSpacingText.Text =
            $"{Math.Clamp((_settings.SubDockItemSpacing ?? _settings.ItemSpacing) + 18.0, 10.0, 60.0):0} px";

        SettingsDialogOpacityText.Text =
            $"{Math.Clamp(_settings.SettingsDialogOpacity, 0.10, 1.00) * 100:0}%";

        OpacityText.Text =
            $"{_settings.Opacity * 100:0}%";

        CollapsedBarOpacityText.Text =
            $"{_settings.CollapsedBarOpacity * 100:0}%";

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

public sealed record WidgetPluginDisplayInfo(
    WidgetPluginInfo Widget,
    int AddedCount,
    string DisplayName,
    string Description)
{

    public string DllName =>
        Widget.DllName;

    public string DllDisplay =>
        $"({Widget.DllName})";

    public string AddedDisplay =>
        AddedCount <= 0
            ? "No"
            : AddedCount == 1
                ? "Yes"
                : $"Yes ({AddedCount})";

    public string AssemblyPath =>
        Widget.AssemblyPath;
}

public sealed record SettingsSearchResult(
    System.Windows.Controls.TabItem TabItem,
    FrameworkElement Target,
    string TabText,
    string Text)
{
    public string DisplayText =>
        $"{TabText} | {Text}";

    public System.Windows.Controls.TextBlock? DisplayContent
    {
        get;
        set;
    }
}

