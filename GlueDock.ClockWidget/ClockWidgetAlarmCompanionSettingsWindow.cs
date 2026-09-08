using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using GlueDock;

namespace GlueDock.ClockWidget;

// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
// GlueDock rule: Alarm companion settings mirror the established Calendar override settings layout and override behavior where technically applicable.
public sealed class ClockWidgetAlarmCompanionSettingsWindow : DockSettingsSectionControl
{
    private readonly ClockWidgetSettings _settings;
    private readonly ClockWidgetRuntimeState _state;
    private readonly GlueDockWidgetAppearance _hostAppearance;
    private readonly Action _applyAndSave;
    private readonly Action _saveRuntimeState;
    private readonly Func<string, string> _localize;
    private readonly string _soundsDirectory;
    private readonly string _systemDefaultSound;

    private readonly Brush _windowBackgroundBrush;
    private readonly Brush _controlBackgroundBrush;
    private readonly Brush _controlBorderBrush;
    private readonly Brush _textBrush;

    private readonly Border _windowBorder;
    private readonly Border _glassSurfaceBorder;
    private readonly Border _glassHighlightBorder;
    private readonly ClockWidgetNativeBackdropHost _nativeBackdropHost;
    private readonly ScrollViewer _settingsScrollViewer;

    private readonly Slider _timeFontSizeSlider;
    private readonly TextBlock _timeFontSizeValueText;
    private readonly Slider _positionFontSizeSlider;
    private readonly TextBlock _positionFontSizeValueText;

    private readonly Border _timeColorPreview;
    private readonly Button _timeColorButton;
    private readonly Border _indexColorPreview;
    private readonly Button _indexColorButton;
    private readonly Border _alarmColorPreview;
    private readonly Button _alarmColorButton;
    private readonly ComboBox _alarmSoundComboBox;
    private readonly TextBlock _soundsFolderText;

    private readonly CheckBox _opacityUseGeneralCheckBox;
    private readonly Slider _opacitySlider;
    private readonly TextBlock _opacityValueText;

    private readonly CheckBox _blurUseGeneralCheckBox;
    private readonly Slider _blurSlider;
    private readonly TextBlock _blurValueText;

    private TextBlock _indexPositionLabelText = null!;
    private TextBlock _indexPositionValueText = null!;
    private Button _indexUpButton = null!;
    private Button _indexLeftButton = null!;
    private Button _indexRightButton = null!;
    private Button _indexDownButton = null!;

    private TextBlock _alarmsLabelText = null!;
    private ListBox _alarmList = null!;
    private TextBox _alarmNameTextBox = null!;
    private DatePicker _alarmDatePicker = null!;
    private TextBox _alarmTimeTextBox = null!;
    private CheckBox _alarmEnabledCheckBox = null!;
    private Button _newAlarmButton = null!;
    private Button _editAlarmButton = null!;
    private Button _deleteAlarmButton = null!;
    private Button _resetButton = null!;
    private TextBlock _titleText = null!;

    private bool _loading;
    private bool _initialBackdropReady;

    public ClockWidgetAlarmCompanionSettingsWindow(
        ClockWidgetSettings settings,
        ClockWidgetRuntimeState state,
        GlueDockWidgetAppearance hostAppearance,
        Action applyAndSave,
        Action saveRuntimeState,
        Func<string, string> localize,
        string soundsDirectory)
    {
        _settings =
            settings;

        _state =
            state;

        _hostAppearance =
            hostAppearance;

        _applyAndSave =
            applyAndSave;

        _saveRuntimeState =
            saveRuntimeState;

        _localize =
            localize;

        _soundsDirectory =
            soundsDirectory;

        _systemDefaultSound =
            _localize(
                "Widget.Clock.Settings.SystemDefaultSound");

        _windowBackgroundBrush =
            hostAppearance.DockBackgroundBrush.Clone();

        _controlBackgroundBrush =
            hostAppearance.DockItemBackgroundBrush.Clone();

        _controlBorderBrush =
            hostAppearance.DockItemBorderBrush.Clone();

        _textBrush =
            hostAppearance.DockTextBrush.Clone();

        Title =
            _localize(
                "Widget.Clock.AlarmCompanion.SettingsTitle");

        Width =
            Math.Min(
                440,
                Math.Max(
                    400,
                    SystemParameters.WorkArea.Width * 0.84));

        MinWidth =
            Math.Min(
                400,
                Width);

        SizeToContent =
            SizeToContent.Height;

        MaxHeight =
            SystemParameters.WorkArea.Height * 0.9;

        WindowStartupLocation =
            WindowStartupLocation.CenterScreen;

        ResizeMode =
            ResizeMode.NoResize;

        ShowInTaskbar =
            true;

        WindowStyle =
            WindowStyle.None;

        AllowsTransparency =
            true;

        Background =
            Brushes.Transparent;

        Foreground =
            _textBrush;

        _windowBorder =
            new Border
            {
                CornerRadius =
                    new CornerRadius(
                        Math.Max(
                            0,
                            hostAppearance.GlassCornerRadius)),
                BorderBrush =
                    _controlBorderBrush,
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        0)
            };

        _windowBorder.MouseLeftButtonDown +=
            WindowDragMouseLeftButtonDown;

        _glassSurfaceBorder =
            new Border
            {
                CornerRadius =
                    _windowBorder.CornerRadius,
                Background =
                    Brushes.Transparent,
                IsHitTestVisible =
                    false
            };

        _glassHighlightBorder =
            new Border
            {
                CornerRadius =
                    _windowBorder.CornerRadius,
                Background =
                    Brushes.Transparent,
                BorderBrush =
                    Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        0),
                IsHitTestVisible =
                    false
            };

        StackPanel content =
            new()
            {
                Margin =
                    new Thickness(
                        16,
                        8,
                        16,
                        16)
            };

        (_timeFontSizeSlider, _timeFontSizeValueText) =
            CreateSliderRow(
                content,
                _localize(
                    "Widget.Clock.AlarmCompanion.TimeFontSize"),
                10,
                20,
                1);

        (_positionFontSizeSlider, _positionFontSizeValueText) =
            CreateSliderRow(
                content,
                _localize(
                    "Widget.Clock.AlarmCompanion.IndexFontSize"),
                7,
                14,
                1);

        (_timeColorPreview, _timeColorButton) =
            CreateColorRow(
                content,
                _localize(
                    "Widget.Clock.AlarmCompanion.TimeColor"));

        (_indexColorPreview, _indexColorButton) =
            CreateColorRow(
                content,
                _localize(
                    "Widget.Clock.AlarmCompanion.IndexColor"));

        (_alarmColorPreview, _alarmColorButton) =
            CreateColorRow(
                content,
                _localize(
                    "Widget.Clock.AlarmCompanion.AlarmColor"));

        _alarmSoundComboBox =
            CreateSoundComboBox();

        AddComboBoxRow(
            content,
            _localize(
                "Widget.Clock.AlarmCompanion.Sound"),
            _alarmSoundComboBox);

        _soundsFolderText =
            CreateSoundsFolderText();

        content.Children.Add(
            _soundsFolderText);

        (_opacityUseGeneralCheckBox, _opacitySlider, _opacityValueText) =
            CreateAppearanceSliderRow(
                content,
                _localize(
                    "Settings.Opacity"),
                10,
                100,
                1);

        (_blurUseGeneralCheckBox, _blurSlider, _blurValueText) =
            CreateAppearanceSliderRow(
                content,
                _localize(
                    "Settings.Blur"),
                0,
                100,
                1);

        Grid indexPositionRow =
            CreateIndexPositionRow();

        _indexPositionLabelText =
            (TextBlock)indexPositionRow.Children[0];

        Grid indexButtonGrid =
            (Grid)indexPositionRow.Children[1];

        _indexUpButton =
            (Button)indexButtonGrid.Children[0];

        _indexLeftButton =
            (Button)indexButtonGrid.Children[1];

        _indexRightButton =
            (Button)indexButtonGrid.Children[2];

        _indexDownButton =
            (Button)indexButtonGrid.Children[3];

        _indexPositionValueText =
            (TextBlock)indexPositionRow.Children[2];

        content.Children.Add(
            indexPositionRow);

        _alarmsLabelText =
            new TextBlock
            {
                Foreground =
                    _textBrush,
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        6)
            };

        content.Children.Add(
            _alarmsLabelText);

        _alarmList =
            new ListBox
            {
                Height = 112,
                Background =
                    _controlBackgroundBrush,
                Foreground =
                    GetContrastingTextBrush(
                        _controlBackgroundBrush,
                        _textBrush),
                BorderBrush =
                    _controlBorderBrush,
                BorderThickness =
                    new Thickness(
                        1),
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        8)
            };

        _alarmList.SelectionChanged +=
            (_, _) =>
                LoadSelectedAlarmIntoEditor();

        content.Children.Add(
            _alarmList);

        Grid alarmEditor =
            CreateAlarmEditorGrid();

        content.Children.Add(
            alarmEditor);

        StackPanel alarmButtons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                Margin =
                    new Thickness(
                        0,
                        6,
                        0,
                        4)
            };

        _newAlarmButton =
            CreateThemedButton(
                string.Empty);

        _editAlarmButton =
            CreateThemedButton(
                string.Empty);

        _deleteAlarmButton =
            CreateThemedButton(
                string.Empty);

        _newAlarmButton.Width = 84;
        _editAlarmButton.Width = 84;
        _deleteAlarmButton.Width = 84;

        _editAlarmButton.Margin =
            new Thickness(
                6,
                0,
                0,
                0);

        _deleteAlarmButton.Margin =
            new Thickness(
                6,
                0,
                0,
                0);

        _newAlarmButton.Click +=
            (_, _) =>
                CreateNewAlarm();

        _editAlarmButton.Click +=
            (_, _) =>
                SaveSelectedAlarmEdits();

        _deleteAlarmButton.Click +=
            (_, _) =>
                DeleteSelectedAlarm();

        alarmButtons.Children.Add(
            _newAlarmButton);

        alarmButtons.Children.Add(
            _editAlarmButton);

        alarmButtons.Children.Add(
            _deleteAlarmButton);

        content.Children.Add(
            alarmButtons);

        _resetButton =
            CreateThemedButton(
                _localize(
                    "Widget.Clock.AlarmCompanion.Reset"));

        _resetButton.HorizontalAlignment =
            HorizontalAlignment.Left;

        _resetButton.Width =
            112;

        _resetButton.Margin =
            new Thickness(
                0,
                12,
                0,
                0);

        _resetButton.Click +=
            (_, _) =>
            {
                _settings.AlarmCompanionTimeFontSize = 15;
                _settings.AlarmCompanionPositionFontSize = 10;
                _settings.AlarmCompanionIndexOffsetX = 0;
                _settings.AlarmCompanionIndexOffsetY = 0;
                _settings.AlarmCompanionTimeColor = string.Empty;
                _settings.AlarmCompanionIndexColor = string.Empty;
                _settings.AlarmCompanionSettingsOpacityOverrideEnabled = false;
                _settings.AlarmCompanionSettingsBlurOverrideEnabled = false;

                RefreshControls();
                ApplyWindowMaterial();
                _applyAndSave();
            };

        content.Children.Add(
            _resetButton);

        _settingsScrollViewer =
            new ScrollViewer
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                Background =
                    Brushes.Transparent,
                Content =
                    content
            };

        Grid layout =
            new();

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid titleBar =
            CreateTitleBar();

        Grid.SetRow(
            titleBar,
            0);

        Grid.SetRow(
            _settingsScrollViewer,
            1);

        layout.Children.Add(
            titleBar);

        layout.Children.Add(
            _settingsScrollViewer);

        Grid chrome =
            new();

        chrome.Children.Add(
            _glassSurfaceBorder);

        chrome.Children.Add(
            _glassHighlightBorder);

        chrome.Children.Add(
            layout);

        _windowBorder.Child =
            chrome;

        Content =
            _windowBorder;

        _nativeBackdropHost =
            new ClockWidgetNativeBackdropHost(
                this);

        _timeFontSizeSlider.ValueChanged +=
            (_, _) =>
                SaveFontValues();

        _positionFontSizeSlider.ValueChanged +=
            (_, _) =>
                SaveFontValues();

        _timeColorButton.Click +=
            (_, _) =>
                ChooseTimeColor();

        _indexColorButton.Click +=
            (_, _) =>
                ChooseIndexColor();

        _alarmColorButton.Click +=
            (_, _) =>
                ChooseAlarmColor();

        _alarmSoundComboBox.SelectionChanged +=
            (_, _) =>
                SaveAlarmSoundValue();

        _opacityUseGeneralCheckBox.Checked +=
            (_, _) =>
                SaveAppearanceValues();

        _opacityUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SaveAppearanceValues();

        _opacitySlider.ValueChanged +=
            (_, _) =>
                SaveAppearanceValues();

        _blurUseGeneralCheckBox.Checked +=
            (_, _) =>
                SaveAppearanceValues();

        _blurUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SaveAppearanceValues();

        _blurSlider.ValueChanged +=
            (_, _) =>
                SaveAppearanceValues();

        Loaded +=
            async (_, _) =>
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        650));

                if (!IsVisible)
                {
                    return;
                }

                _initialBackdropReady =
                    true;

                _nativeBackdropHost.SetBlur(
                    GetEffectiveBlurRadius());

                _nativeBackdropHost.Sync();
            };

        RefreshControls();
        ApplyWindowMaterial();
    }

    public FrameworkElement CreateEmbeddedSettingsContent()
    {
        if (_settingsScrollViewer.Content is not FrameworkElement content)
        {
            throw new InvalidOperationException(
                "Settings content is not available for embedding.");
        }

        _settingsScrollViewer.Content =
            null;

        content.Margin =
            new Thickness(
                0);

        Content =
            content;

        return this;
    }


    private Grid CreateIndexPositionRow()
    {
        Grid row =
            CreateRowGrid(
                _localize(
                    "Widget.Clock.AlarmCompanion.IndexPosition"));

        Grid buttons =
            new()
            {
                Width = 78,
                Height = 62,
                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        buttons.RowDefinitions.Add(
            new RowDefinition());
        buttons.RowDefinitions.Add(
            new RowDefinition());
        buttons.RowDefinitions.Add(
            new RowDefinition());

        buttons.ColumnDefinitions.Add(
            new ColumnDefinition());
        buttons.ColumnDefinitions.Add(
            new ColumnDefinition());
        buttons.ColumnDefinitions.Add(
            new ColumnDefinition());

        Button up =
            CreatePositionButton(
                "↑",
                0,
                -1);

        Button left =
            CreatePositionButton(
                "←",
                -1,
                0);

        Button right =
            CreatePositionButton(
                "→",
                1,
                0);

        Button down =
            CreatePositionButton(
                "↓",
                0,
                1);

        Grid.SetRow(up, 0);
        Grid.SetColumn(up, 1);
        Grid.SetRow(left, 1);
        Grid.SetColumn(left, 0);
        Grid.SetRow(right, 1);
        Grid.SetColumn(right, 2);
        Grid.SetRow(down, 2);
        Grid.SetColumn(down, 1);

        buttons.Children.Add(up);
        buttons.Children.Add(left);
        buttons.Children.Add(right);
        buttons.Children.Add(down);

        TextBlock valueText =
            CreateValueText();

        Grid.SetColumn(
            buttons,
            1);

        Grid.SetColumn(
            valueText,
            2);

        row.Children.Add(
            buttons);

        row.Children.Add(
            valueText);

        return row;
    }

    private Button CreatePositionButton(
        string glyph,
        double deltaX,
        double deltaY)
    {
        Button button =
            CreateThemedButton(
                glyph);

        button.Width = 24;
        button.Height = 20;
        button.Padding =
            new Thickness(
                0);

        button.Click +=
            (_, _) =>
            {
                _settings.AlarmCompanionIndexOffsetX =
                    Math.Clamp(
                        _settings.AlarmCompanionIndexOffsetX +
                        deltaX,
                        -12,
                        12);

                _settings.AlarmCompanionIndexOffsetY =
                    Math.Clamp(
                        _settings.AlarmCompanionIndexOffsetY +
                        deltaY,
                        -12,
                        12);

                UpdateIndexPositionValue();
                _applyAndSave();
            };

        return button;
    }

    private Grid CreateAlarmEditorGrid()
    {
        Grid editor =
            new();

        editor.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        110)
            });

        editor.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        for (int index = 0; index < 4; index++)
        {
            editor.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });
        }

        _alarmNameTextBox =
            CreateThemedTextBox();

        _alarmDatePicker =
            new DatePicker
            {
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        2)
            };

        _alarmTimeTextBox =
            CreateThemedTextBox();

        _alarmTimeTextBox.MaxLength =
            5;

        _alarmEnabledCheckBox =
            new CheckBox
            {
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        4)
            };

        AddAlarmEditorRow(
            editor,
            0,
            "Widget.Clock.AlarmCompanion.Name",
            _alarmNameTextBox);

        AddAlarmEditorRow(
            editor,
            1,
            "Widget.Clock.AlarmCompanion.Date",
            _alarmDatePicker);

        AddAlarmEditorRow(
            editor,
            2,
            "Widget.Clock.AlarmCompanion.Time",
            _alarmTimeTextBox);

        AddAlarmEditorRow(
            editor,
            3,
            "Widget.Clock.AlarmCompanion.Enabled",
            _alarmEnabledCheckBox);

        return editor;
    }

    private TextBox CreateThemedTextBox()
    {
        return new TextBox
        {
            Background =
                _controlBackgroundBrush,
            Foreground =
                GetContrastingTextBrush(
                    _controlBackgroundBrush,
                    _textBrush),
            BorderBrush =
                _controlBorderBrush,
            BorderThickness =
                new Thickness(
                    1),
            Padding =
                new Thickness(
                    6,
                    3,
                    6,
                    3),
            Margin =
                new Thickness(
                    0,
                    2,
                    0,
                    2)
        };
    }

    private void AddAlarmEditorRow(
        Grid editor,
        int row,
        string localizationKey,
        FrameworkElement control)
    {
        TextBlock label =
            new()
            {
                Text =
                    _localize(
                        localizationKey),
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        label.Tag =
            localizationKey;

        Grid.SetRow(
            label,
            row);

        Grid.SetColumn(
            label,
            0);

        Grid.SetRow(
            control,
            row);

        Grid.SetColumn(
            control,
            1);

        editor.Children.Add(
            label);

        editor.Children.Add(
            control);
    }

    private void CreateNewAlarm()
    {
        DateTime triggerAt =
            DateTime.Now.AddMinutes(
                5);

        ClockWidgetAlarmEntry alarm =
            new()
            {
                TriggerAt =
                    new DateTime(
                        triggerAt.Year,
                        triggerAt.Month,
                        triggerAt.Day,
                        triggerAt.Hour,
                        triggerAt.Minute,
                        0),
                Note =
                    string.Empty,
                Enabled =
                    false
            };

        _state.Alarms.Add(
            alarm);

        _saveRuntimeState();
        RefreshAlarmList(
            alarm.Id);
    }

    private void SaveSelectedAlarmEdits()
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarmFromList();

        if (alarm is null)
        {
            return;
        }

        DateTime date =
            _alarmDatePicker.SelectedDate ??
            alarm.TriggerAt.Date;

        if (!TimeSpan.TryParseExact(
                _alarmTimeTextBox.Text,
                "hh\\:mm",
                CultureInfo.InvariantCulture,
                out TimeSpan time))
        {
            return;
        }

        alarm.Note =
            _alarmNameTextBox.Text.Trim();

        alarm.TriggerAt =
            date.Date.Add(
                time);

        alarm.Enabled =
            _alarmEnabledCheckBox.IsChecked ==
            true;

        _saveRuntimeState();
        RefreshAlarmList(
            alarm.Id);
    }

    private void DeleteSelectedAlarm()
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarmFromList();

        if (alarm is null)
        {
            return;
        }

        _state.Alarms.Remove(
            alarm);

        _saveRuntimeState();
        RefreshAlarmList();
    }

    private void RefreshAlarmList(
        string? selectedId = null)
    {
        _state.Alarms.Sort(
            static (left, right) =>
                left.TriggerAt.CompareTo(
                    right.TriggerAt));

        string? previousId =
            selectedId ??
            GetSelectedAlarmFromList()?.Id;

        _alarmList.Items.Clear();

        foreach (ClockWidgetAlarmEntry alarm in
                 _state.Alarms)
        {
            ListBoxItem item =
                new()
                {
                    Content =
                        FormatAlarmListItem(
                            alarm),
                    Tag =
                        alarm,
                    Foreground =
                        GetContrastingTextBrush(
                            _controlBackgroundBrush,
                            _textBrush)
                };

            _alarmList.Items.Add(
                item);

            if (string.Equals(
                    alarm.Id,
                    previousId,
                    StringComparison.Ordinal))
            {
                _alarmList.SelectedItem =
                    item;
            }
        }

        if (_alarmList.SelectedItem is null &&
            _alarmList.Items.Count > 0)
        {
            _alarmList.SelectedIndex =
                0;
        }

        LoadSelectedAlarmIntoEditor();
    }

    private string FormatAlarmListItem(
        ClockWidgetAlarmEntry alarm)
    {
        string stateText =
            _localize(
                alarm.Enabled
                    ? "Widget.Clock.AlarmCompanion.On"
                    : "Widget.Clock.AlarmCompanion.Off");

        string namePart =
            string.IsNullOrWhiteSpace(
                alarm.Note)
                ? string.Empty
                : $"  {alarm.Note}";

        return $"{stateText}  {alarm.TriggerAt:dd.MM.yyyy HH:mm}{namePart}";
    }

    private ClockWidgetAlarmEntry? GetSelectedAlarmFromList()
    {
        if (_alarmList.SelectedItem is ListBoxItem item &&
            item.Tag is ClockWidgetAlarmEntry alarm)
        {
            return alarm;
        }

        return null;
    }

    private void LoadSelectedAlarmIntoEditor()
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarmFromList();

        bool enabled =
            alarm is not null;

        _alarmNameTextBox.IsEnabled =
            enabled;

        _alarmDatePicker.IsEnabled =
            enabled;

        _alarmTimeTextBox.IsEnabled =
            enabled;

        _alarmEnabledCheckBox.IsEnabled =
            enabled;

        _editAlarmButton.IsEnabled =
            enabled;

        _deleteAlarmButton.IsEnabled =
            enabled;

        if (alarm is null)
        {
            _alarmNameTextBox.Text =
                string.Empty;

            _alarmDatePicker.SelectedDate =
                null;

            _alarmTimeTextBox.Text =
                string.Empty;

            _alarmEnabledCheckBox.IsChecked =
                false;

            return;
        }

        _alarmNameTextBox.Text =
            alarm.Note;

        _alarmDatePicker.SelectedDate =
            alarm.TriggerAt.Date;

        _alarmTimeTextBox.Text =
            alarm.TriggerAt.ToString(
                "HH:mm",
                CultureInfo.InvariantCulture);

        _alarmEnabledCheckBox.IsChecked =
            alarm.Enabled;
    }

    private void UpdateIndexPositionValue()
    {
        _indexPositionValueText.Text =
            $"{_settings.AlarmCompanionIndexOffsetX:0}, {_settings.AlarmCompanionIndexOffsetY:0} DIP";
    }

    public void RefreshAlarms()
    {
        RefreshAlarmList();
    }

    public void RefreshLanguage()
    {
        Title =
            _localize(
                "Widget.Clock.AlarmCompanion.SettingsTitle");

        _titleText.Text =
            Title;

        _indexPositionLabelText.Text =
            _localize(
                "Widget.Clock.AlarmCompanion.IndexPosition");

        _alarmsLabelText.Text =
            _localize(
                "Widget.Clock.AlarmCompanion.Alarms");

        _newAlarmButton.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.New");

        _editAlarmButton.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.Edit");

        _deleteAlarmButton.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.Delete");

        _resetButton.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.Reset");

        _opacityUseGeneralCheckBox.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.UseGeneralSetting");

        _blurUseGeneralCheckBox.Content =
            _localize(
                "Widget.Clock.AlarmCompanion.UseGeneralSetting");

        RefreshTaggedLocalization(
            this);

        RefreshAlarmList();
    }

    private void RefreshTaggedLocalization(
        DependencyObject root)
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int index = 0;
             index < childCount;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    root,
                    index);

            if (child is TextBlock textBlock &&
                textBlock.Tag is string localizationKey)
            {
                textBlock.Text =
                    _localize(
                        localizationKey);
            }

            RefreshTaggedLocalization(
                child);
        }
    }

    private (Slider Slider, TextBlock ValueText) CreateSliderRow(
        Panel parent,
        string label,
        double minimum,
        double maximum,
        double tickFrequency)
    {
        Grid row =
            CreateRowGrid(
                label);

        Slider slider =
            CreateThemedSlider(
                minimum,
                maximum,
                tickFrequency);

        TextBlock valueText =
            CreateValueText();

        Grid.SetColumn(
            slider,
            1);

        Grid.SetColumn(
            valueText,
            2);

        row.Children.Add(
            slider);

        row.Children.Add(
            valueText);

        parent.Children.Add(
            row);

        return (
            slider,
            valueText);
    }

    private (Border Preview, Button Button) CreateColorRow(
        Panel parent,
        string label)
    {
        Grid row =
            CreateRowGrid(
                label);

        Border preview =
            new()
            {
                Width =
                    DockDialogTheme.ColorPreviewWidth,
                Height =
                    DockDialogTheme.ColorPreviewHeight,
                CornerRadius =
                    DockDialogTheme.StandardCornerRadius,
                BorderBrush =
                    _controlBorderBrush,
                BorderThickness =
                    new Thickness(
                        1),
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Button button =
            CreateThemedButton(
                _localize(
                    "Settings.ChooseColor"));

        button.Width =
            DockDialogTheme.ColorButtonWidth;

        Grid.SetColumn(
            preview,
            1);

        Grid.SetColumn(
            button,
            2);

        row.Children.Add(
            preview);

        row.Children.Add(
            button);

        parent.Children.Add(
            row);

        return (
            preview,
            button);
    }

    private ComboBox CreateSoundComboBox()
    {
        Directory.CreateDirectory(
            _soundsDirectory);

        string[] soundFiles =
            Directory.GetFiles(
                    _soundsDirectory,
                    "*.mp3",
                    SearchOption.TopDirectoryOnly)
                .Select(
                    Path.GetFileName)
                .Where(
                    fileName =>
                        !string.IsNullOrWhiteSpace(
                            fileName))
                .Cast<string>()
                .OrderBy(
                    fileName =>
                        fileName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        ComboBox comboBox =
            new()
            {
                Width =
                    300,
                Height =
                    DockDialogTheme.StandardControlHeight,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                ItemsSource =
                    new[]
                    {
                        _systemDefaultSound
                    }
                    .Concat(
                        soundFiles)
                    .ToArray()
            };

        comboBox.SelectedItem =
            SelectSound(
                _settings.AlarmSoundFile,
                soundFiles);

        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                _hostAppearance);

        DockDialogThemeService.ApplyComboBox(
            comboBox,
            palette);

        return comboBox;
    }

    private void AddComboBoxRow(
        Panel parent,
        string label,
        ComboBox comboBox)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        8)
            };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                FontWeight =
                    FontWeights.SemiBold
            };

        Grid.SetColumn(
            labelText,
            0);

        Grid.SetColumn(
            comboBox,
            1);

        row.Children.Add(
            labelText);

        row.Children.Add(
            comboBox);

        parent.Children.Add(
            row);
    }

    private TextBlock CreateSoundsFolderText()
    {
        return new TextBlock
        {
            Text =
                string.Format(
                    _localize(
                        "Widget.Clock.Settings.Mp3Folder"),
                    _soundsDirectory),
            Foreground =
                _textBrush,
            TextWrapping =
                TextWrapping.Wrap,
            Opacity =
                0.72,
            Margin =
                new Thickness(
                    0,
                    4,
                    0,
                    8)
        };
    }

    private string SelectSound(
        string configuredFile,
        string[] availableFiles)
    {
        if (string.IsNullOrWhiteSpace(
                configuredFile))
        {
            return _systemDefaultSound;
        }

        return availableFiles.FirstOrDefault(
                   fileName =>
                       string.Equals(
                           fileName,
                           configuredFile,
                           StringComparison.OrdinalIgnoreCase)) ??
               _systemDefaultSound;
    }

    private (CheckBox UseGeneral, Slider Slider, TextBlock ValueText) CreateAppearanceSliderRow(
        Panel parent,
        string label,
        double minimum,
        double maximum,
        double tickFrequency)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        8)
            };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        DockDialogTheme.SliderWidth)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        58)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                FontWeight =
                    FontWeights.SemiBold
            };

        Slider slider =
            CreateThemedSlider(
                minimum,
                maximum,
                tickFrequency);

        TextBlock valueText =
            CreateValueText();

        CheckBox useGeneral =
            new()
            {
                Content =
                    _localize(
                        "Widget.Clock.AlarmCompanion.UseGeneralSetting"),
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                Margin =
                    new Thickness(
                        12,
                        0,
                        0,
                        0)
            };

        Grid.SetColumn(
            labelText,
            0);

        Grid.SetColumn(
            slider,
            1);

        Grid.SetColumn(
            valueText,
            2);

        Grid.SetColumn(
            useGeneral,
            3);

        row.Children.Add(
            labelText);

        row.Children.Add(
            slider);

        row.Children.Add(
            valueText);

        row.Children.Add(
            useGeneral);

        parent.Children.Add(
            row);

        return (
            useGeneral,
            slider,
            valueText);
    }

    private Grid CreateRowGrid(
        string label)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        8)
            };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        DockDialogTheme.SliderWidth)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        128)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                FontWeight =
                    FontWeights.SemiBold
            };

        Grid.SetColumn(
            labelText,
            0);

        row.Children.Add(
            labelText);

        return row;
    }

    private Slider CreateThemedSlider(
        double minimum,
        double maximum,
        double tickFrequency)
    {
        return new Slider
        {
            Width =
                DockDialogTheme.SliderWidth,
            Minimum =
                minimum,
            Maximum =
                maximum,
            TickFrequency =
                tickFrequency,
            IsSnapToTickEnabled =
                true,
            Foreground =
                _textBrush,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };
    }

    private TextBlock CreateValueText()
    {
        return new TextBlock
        {
            Width = 54,
            Margin =
                new Thickness(
                    8,
                    0,
                    0,
                    0),
            VerticalAlignment =
                VerticalAlignment.Center,
            Foreground =
                _textBrush
        };
    }

    private Grid CreateTitleBar()
    {
        Grid titleBar =
            new()
            {
                Margin =
                    new Thickness(
                        14,
                        10,
                        10,
                        0),
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
                Width =
                    GridLength.Auto
            });

        _titleText =
            new TextBlock
            {
                Text =
                    _localize(
                        "Widget.Clock.AlarmCompanion.SettingsTitle"),
                Foreground =
                    _textBrush,
                FontSize = 16,
                FontWeight =
                    FontWeights.SemiBold,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Button closeButton =
            CreateThemedButton(
                "×");

        closeButton.Width = 32;
        closeButton.Height = 30;
        closeButton.FontSize = 18;
        closeButton.Padding =
            new Thickness(
                0);

        closeButton.Click +=
            (_, _) =>
                Close();

        Grid.SetColumn(
            _titleText,
            0);

        Grid.SetColumn(
            closeButton,
            1);

        titleBar.Children.Add(
            _titleText);

        titleBar.Children.Add(
            closeButton);

        return titleBar;
    }

    private Button CreateThemedButton(
        object content)
    {
        Brush normalForeground =
            GetContrastingTextBrush(
                _controlBackgroundBrush,
                _textBrush);

        return new Button
        {
            Content =
                content,
            Height = 30,
            Padding =
                new Thickness(
                    8,
                    4,
                    8,
                    4),
            Background =
                _controlBackgroundBrush,
            Foreground =
                normalForeground,
            BorderBrush =
                _controlBorderBrush,
            BorderThickness =
                new Thickness(
                    1)
        };
    }

    private void RefreshControls()
    {
        _loading =
            true;

        try
        {
            _timeFontSizeSlider.Value =
                Math.Clamp(
                    _settings.AlarmCompanionTimeFontSize,
                    10,
                    20);

            _positionFontSizeSlider.Value =
                Math.Clamp(
                    _settings.AlarmCompanionPositionFontSize,
                    7,
                    14);

            _timeFontSizeValueText.Text =
                $"{_timeFontSizeSlider.Value:0} pt";

            _positionFontSizeValueText.Text =
                $"{_positionFontSizeSlider.Value:0} pt";

            Color timeColor =
                ParseColor(
                    _settings.AlarmCompanionTimeColor,
                    ParseColor(
                        _settings.AlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));

            Color indexColor =
                ParseColor(
                    _settings.AlarmCompanionIndexColor,
                    ParseColor(
                        _settings.AlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));

            _timeColorPreview.Background =
                new SolidColorBrush(
                    timeColor);

            _indexColorPreview.Background =
                new SolidColorBrush(
                    indexColor);

            _alarmColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _settings.AlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));

            string[] availableFiles =
                _alarmSoundComboBox.Items
                    .Cast<string>()
                    .Where(
                        item =>
                            !string.Equals(
                                item,
                                _systemDefaultSound,
                                StringComparison.Ordinal))
                    .ToArray();

            _alarmSoundComboBox.SelectedItem =
                SelectSound(
                    _settings.AlarmSoundFile,
                    availableFiles);

            _opacityUseGeneralCheckBox.IsChecked =
                !_settings.AlarmCompanionSettingsOpacityOverrideEnabled;

            _blurUseGeneralCheckBox.IsChecked =
                !_settings.AlarmCompanionSettingsBlurOverrideEnabled;

            _opacitySlider.Value =
                GetEffectiveOpacity() *
                100.0;

            _blurSlider.Value =
                GetEffectiveBlurRadius();

            _opacitySlider.IsEnabled =
                _settings.AlarmCompanionSettingsOpacityOverrideEnabled;

            _blurSlider.IsEnabled =
                _settings.AlarmCompanionSettingsBlurOverrideEnabled;

            _opacityValueText.Text =
                $"{_opacitySlider.Value:0}%";

            _blurValueText.Text =
                $"{_blurSlider.Value:0}";

            UpdateIndexPositionValue();
            RefreshLanguage();
        }
        finally
        {
            _loading =
                false;
        }
    }

    private void SaveFontValues()
    {
        if (_loading)
        {
            return;
        }

        _settings.AlarmCompanionTimeFontSize =
            _timeFontSizeSlider.Value;

        _settings.AlarmCompanionPositionFontSize =
            _positionFontSizeSlider.Value;

        _timeFontSizeValueText.Text =
            $"{_timeFontSizeSlider.Value:0} pt";

        _positionFontSizeValueText.Text =
            $"{_positionFontSizeSlider.Value:0} pt";

        _applyAndSave();
    }

    private void SaveAppearanceValues()
    {
        if (_loading)
        {
            return;
        }

        _settings.AlarmCompanionSettingsOpacityOverrideEnabled =
            _opacityUseGeneralCheckBox.IsChecked != true;

        _settings.AlarmCompanionSettingsBlurOverrideEnabled =
            _blurUseGeneralCheckBox.IsChecked != true;

        if (_settings.AlarmCompanionSettingsOpacityOverrideEnabled)
        {
            _settings.AlarmCompanionSettingsOpacity =
                Math.Clamp(
                    _opacitySlider.Value / 100.0,
                    0.10,
                    1.00);
        }

        if (_settings.AlarmCompanionSettingsBlurOverrideEnabled)
        {
            _settings.AlarmCompanionSettingsBlurRadius =
                Math.Clamp(
                    _blurSlider.Value,
                    0,
                    100);
        }

        _opacitySlider.IsEnabled =
            _settings.AlarmCompanionSettingsOpacityOverrideEnabled;

        _blurSlider.IsEnabled =
            _settings.AlarmCompanionSettingsBlurOverrideEnabled;

        _opacitySlider.Value =
            GetEffectiveOpacity() *
            100.0;

        _blurSlider.Value =
            GetEffectiveBlurRadius();

        _opacityValueText.Text =
            $"{_opacitySlider.Value:0}%";

        _blurValueText.Text =
            $"{_blurSlider.Value:0}";

        ApplyWindowMaterial();
        _applyAndSave();
    }

    private void ChooseAlarmColor()
    {
        Color? selectedColor =
            ChooseColor(
                ParseColor(
                    _settings.AlarmColor,
                    Color.FromRgb(
                        255,
                        159,
                        10)));

        if (selectedColor is null)
        {
            return;
        }

        _settings.AlarmColor =
            ToColorString(
                selectedColor.Value);

        RefreshControls();
        _applyAndSave();
    }

    private void SaveAlarmSoundValue()
    {
        if (_loading)
        {
            return;
        }

        string selectedSound =
            _alarmSoundComboBox.SelectedItem as string ??
            _systemDefaultSound;

        _settings.AlarmSoundFile =
            string.Equals(
                selectedSound,
                _systemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : selectedSound;

        _applyAndSave();
    }

    private void ChooseTimeColor()
    {
        Color currentColor =
            ParseColor(
                _settings.AlarmCompanionTimeColor,
                ParseColor(
                    _settings.AlarmColor,
                    Color.FromRgb(
                        255,
                        159,
                        10)));

        Color? selectedColor =
            ChooseColor(
                currentColor);

        if (selectedColor is null)
        {
            return;
        }

        _settings.AlarmCompanionTimeColor =
            ToColorString(
                selectedColor.Value);

        RefreshControls();
        _applyAndSave();
    }

    private void ChooseIndexColor()
    {
        Color currentColor =
            ParseColor(
                _settings.AlarmCompanionIndexColor,
                ParseColor(
                    _settings.AlarmColor,
                    Color.FromRgb(
                        255,
                        159,
                        10)));

        Color? selectedColor =
            ChooseColor(
                currentColor);

        if (selectedColor is null)
        {
            return;
        }

        _settings.AlarmCompanionIndexColor =
            ToColorString(
                selectedColor.Value);

        RefreshControls();
        _applyAndSave();
    }

    private static Color? ChooseColor(
        Color currentColor)
    {
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

        return dialog.ShowDialog() ==
               WinForms.DialogResult.OK
            ? Color.FromArgb(
                dialog.Color.A,
                dialog.Color.R,
                dialog.Color.G,
                dialog.Color.B)
            : null;
    }

    private double GetEffectiveOpacity()
    {
        return _settings.AlarmCompanionSettingsOpacityOverrideEnabled
            ? Math.Clamp(
                _settings.AlarmCompanionSettingsOpacity,
                0.10,
                1.00)
            : Math.Clamp(
                _hostAppearance.Opacity,
                0.10,
                1.00);
    }

    private double GetEffectiveBlurRadius()
    {
        return _settings.AlarmCompanionSettingsBlurOverrideEnabled
            ? Math.Clamp(
                _settings.AlarmCompanionSettingsBlurRadius,
                0,
                100)
            : Math.Clamp(
                _hostAppearance.BlurRadius,
                0,
                100);
    }

    private void ApplyWindowMaterial()
    {
        double opacity =
            GetEffectiveOpacity();

        double blurRadius =
            GetEffectiveBlurRadius();

        _windowBorder.CornerRadius =
            new CornerRadius(
                Math.Max(
                    0,
                    _hostAppearance.GlassCornerRadius));

        _glassSurfaceBorder.CornerRadius =
            _windowBorder.CornerRadius;

        _glassHighlightBorder.CornerRadius =
            _windowBorder.CornerRadius;

        if (_hostAppearance.GlassSurfaceEnabled)
        {
            _windowBorder.Background =
                Brushes.Transparent;

            double gradientReferenceHeight =
                Math.Max(
                    1,
                    _hostAppearance.GlassGradientReferenceHeight);

            _glassSurfaceBorder.Background =
                new LinearGradientBrush(
                    _hostAppearance.GlassTopColor,
                    _hostAppearance.GlassBottomColor,
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
                        opacity
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
                    _hostAppearance.GlassHighlightColor,
                    0));

            highlightBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        0,
                        _hostAppearance.GlassHighlightColor.R,
                        _hostAppearance.GlassHighlightColor.G,
                        _hostAppearance.GlassHighlightColor.B),
                    0.55));

            _glassHighlightBorder.Background =
                highlightBrush;
        }
        else
        {
            _glassSurfaceBorder.Background =
                Brushes.Transparent;

            _glassHighlightBorder.Background =
                Brushes.Transparent;

            if (_windowBackgroundBrush is SolidColorBrush solidColorBrush)
            {
                Color color =
                    solidColorBrush.Color;

                _windowBorder.Background =
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
            else
            {
                _windowBorder.Background =
                    _windowBackgroundBrush;
            }
        }

        if (_initialBackdropReady)
        {
            _nativeBackdropHost.SetBlur(
                blurRadius);

            _nativeBackdropHost.Sync();
        }
    }

    private void WindowDragMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Left ||
            e.ButtonState !=
            MouseButtonState.Pressed ||
            IsInteractiveElement(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsInteractiveElement(
        DependencyObject? source)
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is ButtonBase ||
                current is Slider ||
                current is TextBox ||
                current is ComboBox ||
                current is CheckBox)
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
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return fallback;
        }

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

    private static string ToColorString(
        Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private Brush GetContrastingTextBrush(
        Brush backgroundBrush,
        Brush fallbackBrush)
    {
        return GlueDockWidgetUiContrast.GetContrastingTextBrush(
            backgroundBrush,
            _windowBackgroundBrush,
            fallbackBrush);
    }
}
