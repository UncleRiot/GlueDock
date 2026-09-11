using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

#if GLUEDOCK_PLUGIN
using GlueDock;
#endif

namespace GlueNotes;

public partial class MainWindow : Window
{
    private readonly LanguageService _language;
    private readonly NoteRepository _repository;
    private readonly GlueNotesSettings _settings;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _layoutSaveTimer;
    private readonly DocumentConverter _documentConverter;
    private readonly Action? _settingsChanged;
    private readonly Action<string>? _log;

#if GLUEDOCK_PLUGIN
    private DockDialogNativeBackdropHost? _nativeBackdropHost;
    private Action? _openSettings;
    private Func<string, string>? _hostLocalize;
#endif

    private ObservableCollection<NoteSummary> _notes =
        new();

    private ObservableCollection<NoteGroupViewModel> _groups =
        new();

    private ObservableCollection<NoteSummary> _ungroupedNotes =
        new();

    private ObservableCollection<object> _treeItems =
        new();

    private NoteData? _currentNote;
    private bool _loading;
    private bool _selectionSwitchInProgress;
    private Point _dragStartPoint;
    private object? _dragCandidate;
    private TextPointer? _formatSelectionStart;
    private TextPointer? _formatSelectionEnd;
    private bool _isDirty;
    private bool _documentDirty;
    private Task _pendingAutoSaveTask =
        Task.CompletedTask;
    private object? _dropPreviewItem;
    private bool _dropPreviewAfter;


    public MainWindow()
        : this(
            new NoteRepository(),
            new LanguageService(),
            new GlueNotesSettings(),
            null,
            null,
            null)
    {
    }

    public MainWindow(
        NoteRepository repository,
        LanguageService language,
        GlueNotesSettings settings,
        GlueNotesAppearance? appearance,
        Action? settingsChanged = null,
        Action<string>? log = null)
    {
        _repository =
            repository;

        _language =
            language;

        _settings =
            settings;

        _settingsChanged =
            settingsChanged;

        _log =
            log;

        InitializeComponent();

        RestoreWindowLayout();

        if (appearance is not null)
        {
            ApplyHostAppearance(
                appearance);
        }

        _documentConverter =
            new DocumentConverter(
                _repository,
                _language,
                _settings);

        _saveTimer =
            new DispatcherTimer(
                DispatcherPriority.ApplicationIdle)
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        _settings.AutoSaveDelayMilliseconds)
            };

        _saveTimer.Tick +=
            SaveTimer_Tick;

        _layoutSaveTimer =
            new DispatcherTimer(
                DispatcherPriority.ApplicationIdle)
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        350)
            };

        _layoutSaveTimer.Tick +=
            LayoutSaveTimer_Tick;

        LocationChanged +=
            MainWindow_LocationChanged;

        SizeChanged +=
            MainWindow_SizeChanged;

        StateChanged +=
            MainWindow_StateChanged;

        ApplyLanguage();
        InitializeToolbar();
        LoadNotes();
    }

#if GLUEDOCK_PLUGIN
    public MainWindow(
        NoteRepository repository,
        LanguageService language,
        GlueNotesSettings settings,
        GlueDockWidgetAppearance appearance,
        Action openSettings,
        Func<string, string>? hostLocalize,
        Action? settingsChanged = null,
        Action<string>? log = null)
        : this(
            repository,
            language,
            settings,
            null,
            settingsChanged,
            log)
    {
        _openSettings =
            openSettings;

        _hostLocalize =
            hostLocalize;

        _nativeBackdropHost =
            new DockDialogNativeBackdropHost(
                this);

        ShowInTaskbar =
            false;

        SettingsButton.Visibility =
            Visibility.Visible;

        ApplyHostAppearance(
            appearance);

        ApplyLanguage();
    }
#endif

    private void RestoreWindowLayout()
    {
        if (_settings.WindowWidth is double storedWidth &&
            double.IsFinite(
                storedWidth) &&
            storedWidth >=
            MinWidth)
        {
            Width =
                storedWidth;
        }

        if (_settings.WindowHeight is double storedHeight &&
            double.IsFinite(
                storedHeight) &&
            storedHeight >=
            MinHeight)
        {
            Height =
                storedHeight;
        }

        if (_settings.WindowLeft is double storedLeft &&
            _settings.WindowTop is double storedTop &&
            double.IsFinite(
                storedLeft) &&
            double.IsFinite(
                storedTop))
        {
            Rect storedBounds =
                new(
                    storedLeft,
                    storedTop,
                    Width,
                    Height);

            Rect virtualScreen =
                new(
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight);

            if (storedBounds.IntersectsWith(
                    virtualScreen))
            {
                WindowStartupLocation =
                    WindowStartupLocation.Manual;

                Left =
                    storedLeft;

                Top =
                    storedTop;
            }
        }

        if (_settings.WindowMaximized)
        {
            WindowState =
                WindowState.Maximized;
        }

        if (double.IsFinite(
                _settings.LeftPaneWidth) &&
            _settings.LeftPaneWidth >=
            LeftPaneColumn.MinWidth)
        {
            double maxPaneWidth =
                Math.Max(
                    LeftPaneColumn.MinWidth,
                    Width -
                    360);

            LeftPaneColumn.Width =
                new GridLength(
                    Math.Clamp(
                        _settings.LeftPaneWidth,
                        LeftPaneColumn.MinWidth,
                        maxPaneWidth));
        }
    }

    private void PersistWindowLayout()
    {
        Rect bounds =
            WindowState ==
            WindowState.Normal
                ? new Rect(
                    Left,
                    Top,
                    ActualWidth,
                    ActualHeight)
                : RestoreBounds;

        if (double.IsFinite(
                bounds.Left) &&
            double.IsFinite(
                bounds.Top) &&
            double.IsFinite(
                bounds.Width) &&
            double.IsFinite(
                bounds.Height) &&
            bounds.Width >=
            MinWidth &&
            bounds.Height >=
            MinHeight)
        {
            _settings.WindowLeft =
                bounds.Left;

            _settings.WindowTop =
                bounds.Top;

            _settings.WindowWidth =
                bounds.Width;

            _settings.WindowHeight =
                bounds.Height;
        }

        _settings.WindowMaximized =
            WindowState ==
            WindowState.Maximized;

        if (double.IsFinite(
                LeftPaneColumn.ActualWidth) &&
            LeftPaneColumn.ActualWidth >=
            LeftPaneColumn.MinWidth)
        {
            _settings.LeftPaneWidth =
                LeftPaneColumn.ActualWidth;
        }

        _settingsChanged?.Invoke();
    }

    private void QueueWindowLayoutSave()
    {
        _layoutSaveTimer.Stop();
        _layoutSaveTimer.Start();
    }

    private void LayoutSaveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _layoutSaveTimer.Stop();
        PersistWindowLayout();
    }

    private void MainWindow_LocationChanged(
        object? sender,
        EventArgs e)
    {
        QueueWindowLayoutSave();
    }

    private void MainWindow_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        QueueWindowLayoutSave();
    }

    private void MainWindow_StateChanged(
        object? sender,
        EventArgs e)
    {
        QueueWindowLayoutSave();
    }

    private void LeftPaneSplitter_DragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        if (double.IsFinite(
                LeftPaneColumn.ActualWidth) &&
            LeftPaneColumn.ActualWidth >=
            LeftPaneColumn.MinWidth)
        {
            _settings.LeftPaneWidth =
                LeftPaneColumn.ActualWidth;

            _settingsChanged?.Invoke();
        }
    }

    public void SetLanguageCode(
        string? languageCode)
    {
        _language.SetLanguageCode(
            languageCode);

        ApplyLanguage();
    }

    public void ApplyHostAppearance(
        GlueNotesAppearance appearance)
    {
        Resources["GlueNotesWindowBrush"] =
            appearance.WindowBrush;

        Resources["GlueNotesSurfaceBrush"] =
            appearance.SurfaceBrush;

        Resources["GlueNotesPopupSurfaceBrush"] =
            CreateFixedPopupBrush(
                appearance.SurfaceBrush);

        Resources["GlueNotesSurfaceHoverBrush"] =
            appearance.SurfaceHoverBrush;

        Resources["GlueNotesBorderBrush"] =
            appearance.BorderBrush;

        Resources["GlueNotesTextBrush"] =
            appearance.TextBrush;

        Resources["GlueNotesMutedTextBrush"] =
            appearance.MutedTextBrush;

        Resources["GlueNotesAccentBrush"] =
            appearance.AccentBrush;

        Resources["GlueNotesSelectionBrush"] =
            appearance.SelectionBrush;
    }

#if GLUEDOCK_PLUGIN
    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        Style? glueNotesButtonStyle =
            Resources[typeof(Button)] as Style;

        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                appearance);

        if (glueNotesButtonStyle is not null)
        {
            Resources[typeof(Button)] =
                glueNotesButtonStyle;
        }

        Resources["GlueNotesWindowBrush"] =
            palette.WindowBackgroundBrush;

        Resources["GlueNotesSurfaceBrush"] =
            palette.ControlBackgroundBrush;

        Resources["GlueNotesPopupSurfaceBrush"] =
            CreateFixedPopupBrush(
                palette.PopupBackgroundBrush);

        Resources["GlueNotesSurfaceHoverBrush"] =
            CreateSurfaceHoverBrush(
                palette.ControlBackgroundBrush);

        Resources["GlueNotesBorderBrush"] =
            palette.ControlBorderBrush;

        Resources["GlueNotesTextBrush"] =
            palette.TextBrush;

        System.Windows.Media.Brush mutedTextBrush =
            palette.TextBrush.Clone();

        mutedTextBrush.Opacity =
            0.65;

        Resources["GlueNotesMutedTextBrush"] =
            mutedTextBrush;

        Resources["GlueNotesAccentBrush"] =
            palette.PopupHighlightBrush;

        Resources["GlueNotesSelectionBrush"] =
            palette.PopupHighlightBrush;

        ApplyIntegratedButtonStyles();

        NotesTreeView.Resources[SystemColors.HighlightBrushKey] =
            System.Windows.Media.Brushes.Transparent;

        NotesTreeView.Resources[SystemColors.HighlightTextBrushKey] =
            palette.TextBrush;

        NotesTreeView.Resources[SystemColors.InactiveSelectionHighlightBrushKey] =
            System.Windows.Media.Brushes.Transparent;

        NotesTreeView.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] =
            palette.TextBrush;

        DockDialogThemeService.ApplyComboBox(
            FontFamilyComboBox,
            palette);

        DockDialogThemeService.ApplyComboBox(
            FontSizeComboBox,
            palette);

        TitleTextBox.Background =
            palette.ControlBackgroundBrush;

        TitleTextBox.Foreground =
            palette.ControlTextBrush;

        TitleTextBox.BorderBrush =
            palette.ControlBorderBrush;

        EditorRichTextBox.Background =
            palette.ControlBackgroundBrush;

        EditorRichTextBox.Foreground =
            palette.ControlTextBrush;

        EditorRichTextBox.BorderBrush =
            palette.ControlBorderBrush;

        TextColorPreview.BorderBrush =
            palette.ControlBorderBrush;

        if (_nativeBackdropHost is not null)
        {
            DockDialogThemeService.ApplyDialogWindowMaterial(
                WindowBorder,
                GlassSurfaceBorder,
                GlassHighlightBorder,
                _nativeBackdropHost,
                appearance,
                palette.WindowBackgroundBrush,
                Math.Clamp(
                    appearance.Opacity,
                    0.10,
                    1.00),
                Math.Clamp(
                    appearance.BlurRadius,
                    0,
                    100));
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    ApplyIntegratedButtonStyles();

                    ApplyScrollViewerTheme(
                        NotesTreeView,
                        palette);

                    ApplyScrollViewerTheme(
                        EditorRichTextBox,
                        palette);
                }));
    }

    private static Brush CreateFixedPopupBrush(
        Brush backgroundBrush)
    {
        const double opacity =
            0.9;

        if (backgroundBrush is
            SolidColorBrush solidColorBrush)
        {
            Color color =
                solidColorBrush.Color;

            return
                new SolidColorBrush(
                    Color.FromRgb(
                        color.R,
                        color.G,
                        color.B))
                {
                    Opacity =
                        opacity
                };
        }

        if (backgroundBrush is
            GradientBrush gradientBrush)
        {
            GradientBrush clone =
                gradientBrush.Clone();

            foreach (GradientStop gradientStop in
                     clone.GradientStops)
            {
                Color color =
                    gradientStop.Color;

                gradientStop.Color =
                    Color.FromRgb(
                        color.R,
                        color.G,
                        color.B);
            }

            clone.Opacity =
                opacity;

            return clone;
        }

        Brush fallback =
            backgroundBrush.Clone();

        fallback.Opacity =
            opacity;

        return fallback;
    }

    private static Brush CreateSurfaceHoverBrush(
        Brush backgroundBrush)
    {
        return
            DockDialogThemeService.GetAdaptiveHoverBrush(
                backgroundBrush,
                backgroundBrush);
    }

    private void ApplyIntegratedButtonStyles()
    {
        if (Resources[typeof(Button)] is not
            Style buttonStyle)
        {
            return;
        }

        foreach (Button button in
                 FindVisualChildren<Button>(
                     this))
        {
            button.Style =
                buttonStyle;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(
        DependencyObject root)
        where T : DependencyObject
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

            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in
                     FindVisualChildren<T>(
                         child))
            {
                yield return nested;
            }
        }
    }

    private static void ApplyScrollViewerTheme(
        DependencyObject root,
        DockDialogThemePalette palette)
    {
        ScrollViewer? scrollViewer =
            FindVisualChild<ScrollViewer>(
                root);

        if (scrollViewer is not null)
        {
            DockDialogThemeService.ApplyScrollViewer(
                scrollViewer,
                palette);
        }
    }

    private static T? FindVisualChild<T>(
        DependencyObject root)
        where T : DependencyObject
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

            if (child is T match)
            {
                return match;
            }

            T? nested =
                FindVisualChild<T>(
                    child);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
    public void ApplySettings(
        GlueNotesSettings settings,
        GlueDockWidgetAppearance appearance)
    {
        _settings.CopyFrom(
            settings);

        _saveTimer.Interval =
            TimeSpan.FromMilliseconds(
                _settings.AutoSaveDelayMilliseconds);

        ApplyHostAppearance(
            appearance);
    }

#endif

    private void ApplyLanguage()
    {
        Title =
            _language["App.Title"];

        WindowTitleText.Text =
            _language["App.Title"];

        GroupsHeaderText.Text =
            _language["Groups.Title"];

        Resources["GlueNotesGroupColorToolTip"] =
            _language["Groups.Color"];

        EmptyNotesText.Text =
            _language["Notes.Empty"];

        TitleTextBox.ToolTip =
            _language["Editor.TitlePlaceholder"];

        NewGroupButton.Content =
            _language["Groups.New"];

        NewGroupButton.ToolTip =
            _language["Groups.New"];

        NewNoteButton.ToolTip =
            _language["Notes.New"];

        ContextNewGroupMenuItem.Header =
            _language["Groups.New"];

        ContextNewNoteMenuItem.Header =
            _language["Notes.New"];

        ContextDeleteMenuItem.Header =
            _language["Context.Delete"];

        SettingsButton.ToolTip =
#if GLUEDOCK_PLUGIN
            _hostLocalize?.Invoke(
                "Settings.Title") ??
#endif
            _language["Settings.Title"];

        CloseButton.ToolTip =
#if GLUEDOCK_PLUGIN
            _hostLocalize?.Invoke(
                "Settings.Close") ??
#endif
            _language["Settings.Close"];

        BoldButton.ToolTip =
            _language["Toolbar.Bold"];

        ItalicButton.ToolTip =
            _language["Toolbar.Italic"];

        UnderlineButton.ToolTip =
            _language["Toolbar.Underline"];

        FontFamilyComboBox.ToolTip =
            _language["Toolbar.FontFamily"];

        FontSizeComboBox.ToolTip =
            _language["Toolbar.FontSize"];

        TextColorButton.ToolTip =
            _language["Toolbar.TextColor"];

        TextColorDropDownButton.ToolTip =
            _language["Toolbar.TextColor"];

        HighlightColorButton.ToolTip =
            _language["Toolbar.HighlightColor"];

        HighlightColorDropDownButton.ToolTip =
            _language["Toolbar.HighlightColor"];

        InsertChecklistButton.ToolTip =
            _language["Toolbar.InsertChecklist"];

        ExportButton.ToolTip =
            _language["Toolbar.Export"];

        SaveStatusText.Text =
            _language["Status.Saved"];
    }

    private void InitializeToolbar()
    {
        string[] fontFamilies =
            Fonts.SystemFontFamilies
                .Select(
                    fontFamily =>
                        fontFamily.Source)
                .Distinct(
                    StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(
                    name =>
                        name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        FontFamilyComboBox.ItemsSource =
            fontFamilies;

        string currentFontFamily =
            EditorRichTextBox.FontFamily.Source;

        FontFamilyComboBox.SelectedItem =
            fontFamilies.FirstOrDefault(
                name =>
                    string.Equals(
                        name,
                        currentFontFamily,
                        StringComparison.CurrentCultureIgnoreCase));

        FontSizeComboBox.ItemsSource =
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

        FontSizeComboBox.SelectedItem =
            FontSizeComboBox.Items
                .Cast<double>()
                .Contains(
                    _settings.DefaultFontSize)
                ? _settings.DefaultFontSize
                : 14d;
    }

    private void LoadNotes()
    {
        _notes =
            _repository.LoadSummaries();

        List<NoteGroupData> storedGroups =
            _repository.LoadGroups();

        if (storedGroups.Count == 0)
        {
            storedGroups.Add(
                new NoteGroupData
                {
                    Id =
                        Guid.NewGuid(),
                    Name =
                        _language["Groups.Default"],
                    ColorHex =
                        GetDefaultGroupColorHex(),
                    SortOrder =
                        0
                });

            _repository.SaveGroups(
                storedGroups);
        }

        _groups =
            new ObservableCollection<NoteGroupViewModel>(
                storedGroups
                    .OrderBy(
                        group =>
                            group.SortOrder)
                    .Select(
                        group =>
                            new NoteGroupViewModel
                            {
                                Id =
                                    group.Id,
                                Name =
                                    group.Name,
                                ColorHex =
                                    group.ColorHex,
                                SortOrder =
                                    group.SortOrder
                            }));

        _ungroupedNotes =
            new ObservableCollection<NoteSummary>();

        NoteGroupViewModel defaultGroup =
            _groups[0];

        foreach (NoteSummary note in
                 _notes)
        {
            if (note.GroupId ==
                Guid.Empty)
            {
                _ungroupedNotes.Add(
                    note);

                continue;
            }

            NoteGroupViewModel? group =
                _groups.FirstOrDefault(
                    item =>
                        item.Id ==
                        note.GroupId);

            if (group is null)
            {
                group =
                    defaultGroup;

                note.GroupId =
                    group.Id;

                _repository.UpdateNoteLocation(
                    note.Id,
                    note.GroupId,
                    note.SortOrder);
            }

            group.Notes.Add(
                note);
        }

        foreach (NoteGroupViewModel group in
                 _groups)
        {
            List<NoteSummary> ordered =
                group.Notes
                    .OrderBy(
                        note =>
                            note.SortOrder)
                    .ThenByDescending(
                        note =>
                            note.UpdatedAtUtc)
                    .ToList();

            group.Notes.Clear();

            for (int index = 0;
                 index < ordered.Count;
                 index++)
            {
                NoteSummary note =
                    ordered[index];

                note.SortOrder =
                    index;

                group.Notes.Add(
                    note);

                _repository.UpdateNoteLocation(
                    note.Id,
                    group.Id,
                    index);
            }
        }

        List<NoteSummary> orderedUngrouped =
            _ungroupedNotes
                .OrderBy(
                    note =>
                        note.SortOrder)
                .ThenByDescending(
                    note =>
                        note.UpdatedAtUtc)
                .ToList();

        _ungroupedNotes.Clear();

        for (int index = 0;
             index < orderedUngrouped.Count;
             index++)
        {
            NoteSummary note =
                orderedUngrouped[index];

            note.SortOrder =
                index;

            _ungroupedNotes.Add(
                note);

            _repository.UpdateNoteLocation(
                note.Id,
                Guid.Empty,
                index);
        }

        RefreshTreeItems();

        UpdateEmptyState();

        NoteSummary? firstNote =
            _ungroupedNotes.FirstOrDefault() ??
            _groups
                .SelectMany(
                    group =>
                        group.Notes)
                .FirstOrDefault();

        if (firstNote is not null)
        {
            LoadNote(
                firstNote);

            SelectNote(
                firstNote);
        }
        else
        {
            CreateNewNote();
        }
    }

    private void CreateNewNote(
        NoteGroupViewModel? targetGroup = null)
    {
        SaveCurrentNote();

        NoteGroupViewModel group =
            targetGroup ??
            GetSelectedGroup() ??
            _groups.First();

        DateTime now =
            DateTime.UtcNow;

        NoteData note =
            new()
            {
                Id =
                    Guid.NewGuid(),
                Title =
                    _language["Notes.Untitled"],
                CreatedAtUtc =
                    now,
                UpdatedAtUtc =
                    now,
                GroupId =
                    group.Id,
                SortOrder =
                    group.Notes.Count,
                Blocks =
                    new List<NoteBlockData>
                    {
                        new ParagraphBlockData()
                    }
            };

        _repository.Save(
            note);

        NoteSummary summary =
            new()
            {
                Id =
                    note.Id,
                Title =
                    note.Title,
                UpdatedAtUtc =
                    note.UpdatedAtUtc,
                GroupId =
                    note.GroupId,
                SortOrder =
                    note.SortOrder
            };

        _notes.Add(
            summary);

        group.Notes.Add(
            summary);

        UpdateEmptyState();

        LoadNote(
            summary);

        SelectNote(
            summary);
    }

    private void LoadNote(
        NoteSummary summary)
    {
        NoteData? note =
            _repository.Load(
                summary.Id);

        if (note is null)
        {
            return;
        }

        _loading =
            true;

        try
        {
            _currentNote =
                note;

            TitleTextBox.Text =
                note.Title;

            EditorRichTextBox.Document =
                _documentConverter.CreateDocument(
                    note,
                    CheckListCheckBox_Changed,
                    CheckListTextBox_TextChanged,
                    ImageResizeThumb_DragCompleted);

            _formatSelectionStart =
                EditorRichTextBox.Selection.Start;

            _formatSelectionEnd =
                EditorRichTextBox.Selection.End;
        }
        finally
        {
            _loading =
                false;
        }

        _isDirty =
            false;

        _documentDirty =
            false;

        SaveStatusText.Text =
            _language["Status.Saved"];

        EditorRichTextBox.Focus();
    }

    private void QueueSave(
        bool documentChanged = true)
    {
        if (_loading ||
            _currentNote is null)
        {
            return;
        }

        _isDirty =
            true;

        if (documentChanged)
        {
            _documentDirty =
                true;
        }

        SaveStatusText.Text =
            _language["Status.Saving"];

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private NoteData CaptureCurrentNoteForSave()
    {
        if (_currentNote is null)
        {
            throw new InvalidOperationException(
                "No current note is loaded.");
        }

        _currentNote.Title =
            string.IsNullOrWhiteSpace(
                TitleTextBox.Text)
                ? _language["Notes.Untitled"]
                : TitleTextBox.Text.Trim();

        if (_documentDirty)
        {
            _currentNote.Blocks =
                _documentConverter.ReadDocument(
                    EditorRichTextBox.Document);
        }

        _currentNote.UpdatedAtUtc =
            DateTime.UtcNow;

        NoteData snapshot =
            new()
            {
                Id =
                    _currentNote.Id,
                Title =
                    _currentNote.Title,
                CreatedAtUtc =
                    _currentNote.CreatedAtUtc,
                UpdatedAtUtc =
                    _currentNote.UpdatedAtUtc,
                GroupId =
                    _currentNote.GroupId,
                SortOrder =
                    _currentNote.SortOrder,
                Blocks =
                    _currentNote.Blocks
            };

        NoteSummary? summary =
            _notes.FirstOrDefault(
                item =>
                    item.Id ==
                    _currentNote.Id);

        if (summary is not null)
        {
            summary.Title =
                _currentNote.Title;

            summary.UpdatedAtUtc =
                _currentNote.UpdatedAtUtc;
        }

        _isDirty =
            false;

        _documentDirty =
            false;

        return snapshot;
    }

    private void SaveCurrentNote()
    {
        _saveTimer.Stop();

        if (!_pendingAutoSaveTask.IsCompleted)
        {
            _pendingAutoSaveTask
                .GetAwaiter()
                .GetResult();
        }

        if (_loading ||
            _currentNote is null ||
            !_isDirty)
        {
            return;
        }

        NoteData snapshot =
            CaptureCurrentNoteForSave();

        _repository.Save(
            snapshot,
            false);

        if (!_isDirty)
        {
            SaveStatusText.Text =
                _language["Status.Saved"];
        }
    }

    private async Task SaveCurrentNoteAsync()
    {
        _saveTimer.Stop();

        if (_loading ||
            _currentNote is null ||
            !_isDirty)
        {
            return;
        }

        if (!_pendingAutoSaveTask.IsCompleted)
        {
            _saveTimer.Start();
            return;
        }

        Guid noteId =
            _currentNote.Id;

        NoteData snapshot =
            CaptureCurrentNoteForSave();

        Task saveTask =
            Task.Run(
                () =>
                    _repository.Save(
                        snapshot,
                        false));

        _pendingAutoSaveTask =
            saveTask;

        try
        {
            await saveTask;

            if (_currentNote?.Id ==
                    noteId &&
                !_isDirty)
            {
                SaveStatusText.Text =
                    _language["Status.Saved"];
            }
        }
        catch
        {
            if (_currentNote?.Id ==
                noteId)
            {
                _isDirty =
                    true;

                _documentDirty =
                    true;

                SaveStatusText.Text =
                    _language["Status.Saving"];

                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }
    }

    private void UpdateEmptyState()
    {
        EmptyNotesText.Visibility =
            _notes.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ToggleSelectionProperty(
        DependencyProperty property,
        object enabledValue,
        object disabledValue)
    {
        object current =
            EditorRichTextBox.Selection.GetPropertyValue(
                property);

        bool enabled =
            current !=
            DependencyProperty.UnsetValue &&
            Equals(
                current,
                enabledValue);

        EditorRichTextBox.Selection.ApplyPropertyValue(
            property,
            enabled
                ? disabledValue
                : enabledValue);

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private TextRange GetFormattingSelection()
    {
        return
            _formatSelectionStart is not null &&
            _formatSelectionEnd is not null
                ? new TextRange(
                    _formatSelectionStart,
                    _formatSelectionEnd)
                : EditorRichTextBox.Selection;
    }

    private void ApplySelectedFontFamily(
        string fontFamilyName)
    {
        TextRange selection =
            GetFormattingSelection();

        selection.ApplyPropertyValue(
            TextElement.FontFamilyProperty,
            new System.Windows.Media.FontFamily(
                fontFamilyName));

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private void ApplySelectedFontSize(
        double size)
    {
        TextRange selection =
            GetFormattingSelection();

        selection.ApplyPropertyValue(
            TextElement.FontSizeProperty,
            size);

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private void ApplySelectedColor(
        System.Windows.Media.Color color)
    {
        SolidColorBrush brush =
            new(
                color);

        TextRange selection =
            GetFormattingSelection();

        selection.ApplyPropertyValue(
            TextElement.ForegroundProperty,
            brush);

        TextColorPreview.Background =
            brush;

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private void ApplySelectedHighlightColor(
        System.Windows.Media.Color color)
    {
        SolidColorBrush brush =
            new(
                color);

        TextRange selection =
            GetFormattingSelection();

        selection.ApplyPropertyValue(
            TextElement.BackgroundProperty,
            brush);

        HighlightColorPreview.Background =
            brush;

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private System.Windows.Media.Color ChooseColor(
        System.Windows.Media.Color currentColor,
        out bool accepted)
    {
        using WinForms.ColorDialog dialog =
            new()
            {
                FullOpen =
                    true,
                Color =
                    System.Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R,
                        currentColor.G,
                        currentColor.B)
            };

        accepted =
            dialog.ShowDialog() ==
            WinForms.DialogResult.OK;

        return
            accepted
                ? System.Windows.Media.Color.FromArgb(
                    dialog.Color.A,
                    dialog.Color.R,
                    dialog.Color.G,
                    dialog.Color.B)
                : currentColor;
    }

    private void InsertBlockAfterSelection(
        Block block)
    {
        TextPointer insertionPosition =
            EditorRichTextBox.CaretPosition;

        Paragraph? paragraph =
            insertionPosition.Paragraph;

        if (paragraph is not null)
        {
            EditorRichTextBox.Document.Blocks.InsertAfter(
                paragraph,
                block);
        }
        else
        {
            EditorRichTextBox.Document.Blocks.Add(
                block);
        }

        QueueSave();
    }

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
#if GLUEDOCK_PLUGIN
        _openSettings?.Invoke();
#endif
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveCurrentNote();
        Close();
    }

    private void NewNoteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        CreateNewNote(
            GetSelectedGroup());
    }

    private void NotesTreeView_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (_selectionSwitchInProgress)
        {
            return;
        }

        if (e.NewValue is
            NoteGroupViewModel selectedGroup)
        {
            SetActiveGroup(
                selectedGroup);

            SetActiveNote(
                null);

            return;
        }

        if (e.NewValue is not
            NoteSummary selected)
        {
            return;
        }

        NoteGroupViewModel? group =
            _groups.FirstOrDefault(
                item =>
                    item.Notes.Contains(
                        selected));

        SetActiveGroup(
            group);

        SetActiveNote(
            selected);

        _selectionSwitchInProgress =
            true;

        try
        {
            SaveCurrentNote();
            LoadNote(
                selected);
        }
        finally
        {
            _selectionSwitchInProgress =
                false;
        }
    }

    private void TitleTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        QueueSave(
            false);
    }

    private void EditorRichTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        QueueSave();
    }

    private void BoldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ToggleSelectionProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold,
            FontWeights.Normal);
    }

    private void ItalicButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ToggleSelectionProperty(
            TextElement.FontStyleProperty,
            FontStyles.Italic,
            FontStyles.Normal);
    }

    private void UnderlineButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        TextDecorationCollection? current =
            EditorRichTextBox.Selection.GetPropertyValue(
                Inline.TextDecorationsProperty) as
                TextDecorationCollection;

        bool underlined =
            current?.Contains(
                TextDecorations.Underline[0]) ==
            true;

        EditorRichTextBox.Selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            underlined
                ? null
                : TextDecorations.Underline);

        EditorRichTextBox.Focus();

        QueueSave();
    }

    private void FontFamilyComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loading ||
            FontFamilyComboBox.SelectedItem is not
                string fontFamilyName)
        {
            return;
        }

        ApplySelectedFontFamily(
            fontFamilyName);
    }

    private void FontSizeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loading ||
            FontSizeComboBox.SelectedItem is not
                double size)
        {
            return;
        }

        ApplySelectedFontSize(
            size);
    }

    private void TextColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        System.Windows.Media.Color color =
            TextColorPreview.Background is
                SolidColorBrush brush
                ? brush.Color
                : System.Windows.Media.Colors.White;

        ApplySelectedColor(
            color);
    }

    private void TextColorDropDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        System.Windows.Media.Color currentColor =
            TextColorPreview.Background is
                SolidColorBrush brush
                ? brush.Color
                : System.Windows.Media.Colors.White;

        System.Windows.Media.Color selectedColor =
            ChooseColor(
                currentColor,
                out bool accepted);

        if (!accepted)
        {
            return;
        }

        TextColorPreview.Background =
            new SolidColorBrush(
                selectedColor);

        ApplySelectedColor(
            selectedColor);
    }

    private void HighlightColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        System.Windows.Media.Color color =
            HighlightColorPreview.Background is
                SolidColorBrush brush
                ? brush.Color
                : System.Windows.Media.Colors.Yellow;

        ApplySelectedHighlightColor(
            color);
    }

    private void HighlightColorDropDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        System.Windows.Media.Color currentColor =
            HighlightColorPreview.Background is
                SolidColorBrush brush
                ? brush.Color
                : System.Windows.Media.Colors.Yellow;

        System.Windows.Media.Color selectedColor =
            ChooseColor(
                currentColor,
                out bool accepted);

        if (!accepted)
        {
            return;
        }

        HighlightColorPreview.Background =
            new SolidColorBrush(
                selectedColor);

        ApplySelectedHighlightColor(
            selectedColor);
    }

    private void EditorRichTextBox_Pasting(
        object sender,
        DataObjectPastingEventArgs e)
    {
        if (_currentNote is null ||
            !e.DataObject.GetDataPresent(
                System.Windows.DataFormats.Bitmap))
        {
            return;
        }

        if (e.DataObject.GetData(
                System.Windows.DataFormats.Bitmap) is not
            System.Windows.Media.Imaging.BitmapSource bitmapSource)
        {
            return;
        }

        e.CancelCommand();

        string relativePath =
            _repository.ImportClipboardImage(
                _currentNote.Id,
                bitmapSource);

        InlineUIContainer imageInline =
            _documentConverter.CreateImageInline(
                relativePath,
                ImageResizeThumb_DragCompleted);

        TextPointer insertionPosition =
            EditorRichTextBox.CaretPosition;

        insertionPosition.InsertTextInRun(
            string.Empty);

        Paragraph? paragraph =
            insertionPosition.Paragraph;

        if (paragraph is null)
        {
            paragraph =
                new Paragraph();

            EditorRichTextBox.Document.Blocks.Add(
                paragraph);

            paragraph.Inlines.Add(
                imageInline);
        }
        else
        {
            paragraph.Inlines.Add(
                imageInline);
        }

        QueueSave();
    }

    private void InsertChecklistButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InsertBlockAfterSelection(
            _documentConverter.CreateCheckListBlock(
                CheckListCheckBox_Changed,
                CheckListTextBox_TextChanged));
    }

    private void ImageResizeThumb_DragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        QueueSave();
    }

    private void CheckListCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        QueueSave();
    }

    private void CheckListTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        QueueSave();
    }

    private void EditorRichTextBox_SelectionChanged(
        object sender,
        RoutedEventArgs e)
    {
        _formatSelectionStart =
            EditorRichTextBox.Selection.Start;

        _formatSelectionEnd =
            EditorRichTextBox.Selection.End;

        object fontFamilyValue =
            EditorRichTextBox.Selection.GetPropertyValue(
                TextElement.FontFamilyProperty);

        object fontSizeValue =
            EditorRichTextBox.Selection.GetPropertyValue(
                TextElement.FontSizeProperty);

        object foregroundValue =
            EditorRichTextBox.Selection.GetPropertyValue(
                TextElement.ForegroundProperty);

        object backgroundValue =
            EditorRichTextBox.Selection.GetPropertyValue(
                TextElement.BackgroundProperty);

        bool previousLoading =
            _loading;

        _loading =
            true;

        try
        {
            if (fontFamilyValue is
                System.Windows.Media.FontFamily fontFamily &&
                FontFamilyComboBox.Items
                    .Cast<string>()
                    .Contains(
                        fontFamily.Source,
                        StringComparer.CurrentCultureIgnoreCase))
            {
                FontFamilyComboBox.SelectedItem =
                    FontFamilyComboBox.Items
                        .Cast<string>()
                        .First(
                            name =>
                                string.Equals(
                                    name,
                                    fontFamily.Source,
                                    StringComparison.CurrentCultureIgnoreCase));
            }

            if (fontSizeValue is
                    double fontSize &&
                FontSizeComboBox.Items
                    .Cast<double>()
                    .Contains(
                        fontSize))
            {
                FontSizeComboBox.SelectedItem =
                    fontSize;
            }

            if (foregroundValue is
                SolidColorBrush foregroundBrush)
            {
                TextColorPreview.Background =
                    foregroundBrush.Clone();
            }

            if (backgroundValue is
                SolidColorBrush backgroundBrush)
            {
                HighlightColorPreview.Background =
                    backgroundBrush.Clone();
            }
        }
        finally
        {
            _loading =
                previousLoading;
        }
    }

    private string GetDefaultGroupColorHex()
    {
        if (Resources["GlueNotesAccentBrush"] is
            SolidColorBrush brush)
        {
            return
                brush.Color.ToString();
        }

        return
            SystemColors.HighlightColor.ToString();
    }

    private NoteGroupViewModel? GetSelectedGroup()
    {
        NoteGroupViewModel? activeGroup =
            _groups.FirstOrDefault(
                group =>
                    group.IsActive);

        if (activeGroup is not null)
        {
            return activeGroup;
        }

        if (NotesTreeView.SelectedItem is
            NoteGroupViewModel selectedGroup)
        {
            return selectedGroup;
        }

        if (NotesTreeView.SelectedItem is
            NoteSummary selectedNote)
        {
            return
                _groups.FirstOrDefault(
                    group =>
                        group.Notes.Contains(
                            selectedNote));
        }

        if (_currentNote is not null)
        {
            return
                _groups.FirstOrDefault(
                    group =>
                        group.Id ==
                        _currentNote.GroupId);
        }

        return
            _groups.FirstOrDefault();
    }

    private void SetActiveGroup(
        NoteGroupViewModel? activeGroup)
    {
        foreach (NoteGroupViewModel group in
                 _groups)
        {
            group.IsActive =
                ReferenceEquals(
                    group,
                    activeGroup);
        }
    }

    private void SetActiveNote(
        NoteSummary? activeNote)
    {
        foreach (NoteSummary note in
                 _notes)
        {
            note.IsActive =
                ReferenceEquals(
                    note,
                    activeNote);
        }
    }

    private void SelectGroup(
        NoteGroupViewModel group,
        bool beginEdit = false)
    {
        SetActiveGroup(
            group);

        SetActiveNote(
            null);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    if (NotesTreeView.ItemContainerGenerator.ContainerFromItem(
                            group) is
                        TreeViewItem groupItem)
                    {
                        groupItem.IsSelected =
                            true;

                        groupItem.IsExpanded =
                            true;

                        groupItem.BringIntoView();

                        if (beginEdit)
                        {
                            groupItem.UpdateLayout();

                            TextBox? groupNameTextBox =
                                FindVisualChild<TextBox>(
                                    groupItem);

                            if (groupNameTextBox is not null)
                            {
                                groupNameTextBox.Focus();

                                groupNameTextBox.CaretIndex =
                                    groupNameTextBox.Text.Length;
                            }
                        }
                    }
                }));
    }

    private void SelectNote(
        NoteSummary note)
    {
        NoteGroupViewModel? group =
            _groups.FirstOrDefault(
                item =>
                    item.Notes.Contains(
                        note));

        SetActiveGroup(
            group);

        SetActiveNote(
            note);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    if (group is null)
                    {
                        if (NotesTreeView.ItemContainerGenerator.ContainerFromItem(
                                note) is
                            TreeViewItem ungroupedNoteItem)
                        {
                            ungroupedNoteItem.IsSelected =
                                true;

                            ungroupedNoteItem.BringIntoView();
                        }

                        return;
                    }

                    if (NotesTreeView.ItemContainerGenerator.ContainerFromItem(
                            group) is not
                        TreeViewItem groupItem)
                    {
                        return;
                    }

                    groupItem.IsExpanded =
                        true;

                    groupItem.UpdateLayout();

                    if (groupItem.ItemContainerGenerator.ContainerFromItem(
                            note) is
                        TreeViewItem noteItem)
                    {
                        noteItem.IsSelected =
                            true;

                        noteItem.BringIntoView();
                    }
                }));
    }

    private void NewGroupButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        NoteGroupViewModel group =
            new()
            {
                Id =
                    Guid.NewGuid(),
                Name =
                    _language["Groups.NewName"],
                ColorHex =
                    GetDefaultGroupColorHex(),
                SortOrder =
                    _groups.Count
            };

        _groups.Add(
            group);

        RefreshTreeItems();
        SaveGroups();

        SelectGroup(
            group,
            true);
    }

    private void GroupColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not
                NoteGroupViewModel group)
        {
            return;
        }

        SetActiveGroup(
            group);

        SetActiveNote(
            null);

        TreeViewItem? groupItem =
            FindVisualParent<TreeViewItem>(
                button);

        if (groupItem is not null)
        {
            groupItem.IsSelected =
                true;
        }

        System.Windows.Media.Color currentColor =
            SystemColors.HighlightColor;

        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(
                    group.ColorHex) is
                System.Windows.Media.Color parsedColor)
            {
                currentColor =
                    parsedColor;
            }
        }
        catch
        {
        }

        using WinForms.ColorDialog dialog =
            new()
            {
                FullOpen =
                    true,
                Color =
                    System.Drawing.Color.FromArgb(
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

        group.ColorHex =
            System.Windows.Media.Color.FromArgb(
                    dialog.Color.A,
                    dialog.Color.R,
                    dialog.Color.G,
                    dialog.Color.B)
                .ToString();

        SaveGroups();

        e.Handled =
            true;
    }

    private void GroupNameTextBox_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is not
                TextBox textBox ||
            textBox.DataContext is not
                NoteGroupViewModel group)
        {
            return;
        }

        SetActiveGroup(
            group);

        SetActiveNote(
            null);

        TreeViewItem? groupItem =
            FindVisualParent<TreeViewItem>(
                textBox);

        if (groupItem is not null)
        {
            groupItem.IsSelected =
                true;
        }
    }

    private void GroupNameTextBox_LostFocus(
        object sender,
        RoutedEventArgs e)
    {
        SaveGroupName(
            sender as TextBox);
    }

    private void GroupNameTextBox_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key !=
            Key.Enter)
        {
            return;
        }

        SaveGroupName(
            sender as TextBox);

        Keyboard.ClearFocus();

        e.Handled =
            true;
    }

    private void SaveGroupName(
        TextBox? textBox)
    {
        if (textBox?.DataContext is not
            NoteGroupViewModel group)
        {
            return;
        }

        group.Name =
            string.IsNullOrWhiteSpace(
                group.Name)
                ? _language["Groups.NewName"]
                : group.Name.Trim();

        SaveGroups();
    }

    private void SaveGroups()
    {
        for (int index = 0;
             index < _groups.Count;
             index++)
        {
            _groups[index].SortOrder =
                index;
        }

        _repository.SaveGroups(
            _groups.Select(
                group =>
                    group.ToData()));
    }

    private void NotesTreeView_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not
            DependencyObject source)
        {
            return;
        }

        TreeViewItem? item =
            FindVisualParent<TreeViewItem>(
                source);

        if (item is null)
        {
            return;
        }

        item.IsSelected =
            true;

        item.Focus();
    }

    private void GroupNameTextBox_ContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        if (sender is not
            TextBox textBox)
        {
            return;
        }

        TreeViewItem? item =
            FindVisualParent<TreeViewItem>(
                textBox);

        if (item is null)
        {
            return;
        }

        item.IsSelected =
            true;

        item.Focus();

        e.Handled =
            true;

        NotesTreeView.ContextMenu.PlacementTarget =
            item;

        NotesTreeView.ContextMenu.IsOpen =
            true;
    }

    private void GroupHeader_PreviewDragOver(
        object sender,
        DragEventArgs e)
    {
        if (sender is not
                FrameworkElement element ||
            element.DataContext is not
                NoteGroupViewModel targetGroup ||
            e.Data.GetData(
                typeof(NoteSummary)) is not
                NoteSummary)
        {
            return;
        }

        if (targetGroup.Notes.LastOrDefault() is
            NoteSummary lastGroupNote)
        {
            SetDropPreview(
                lastGroupNote,
                true);
        }
        else
        {
            SetDropPreview(
                targetGroup,
                true);
        }

        e.Effects =
            DragDropEffects.Move;

        e.Handled =
            true;
    }

    private void GroupHeader_PreviewDrop(
        object sender,
        DragEventArgs e)
    {
        if (sender is not
                FrameworkElement element ||
            element.DataContext is not
                NoteGroupViewModel targetGroup ||
            e.Data.GetData(
                typeof(NoteSummary)) is not
                NoteSummary draggedNote)
        {
            return;
        }

        ClearDropPreview();

        MoveNote(
            draggedNote,
            targetGroup,
            null);

        e.Handled =
            true;
    }

    private void TextEditContextMenu_Opened(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not
            ContextMenu contextMenu)
        {
            return;
        }

        string[] resourceKeys =
        {
            "GlueNotesPopupSurfaceBrush",
            "GlueNotesSurfaceHoverBrush",
            "GlueNotesBorderBrush",
            "GlueNotesTextBrush"
        };

        foreach (string resourceKey in
                 resourceKeys)
        {
            if (Resources[resourceKey] is
                object resource)
            {
                contextMenu.Resources[resourceKey] =
                    resource;
            }
        }

        if (Resources["GlueNotesPopupSurfaceBrush"] is
            Brush backgroundBrush)
        {
            contextMenu.Background =
                backgroundBrush;
        }

        if (Resources["GlueNotesBorderBrush"] is
            Brush borderBrush)
        {
            contextMenu.BorderBrush =
                borderBrush;
        }

        if (Resources["GlueNotesTextBrush"] is
            Brush foregroundBrush)
        {
            contextMenu.Foreground =
                foregroundBrush;
        }
    }

    private void NotesTreeContextMenu_Opened(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not
            ContextMenu contextMenu)
        {
            return;
        }

        string[] resourceKeys =
        {
            "GlueNotesPopupSurfaceBrush",
            "GlueNotesSurfaceHoverBrush",
            "GlueNotesBorderBrush",
            "GlueNotesTextBrush"
        };

        foreach (string resourceKey in
                 resourceKeys)
        {
            if (Resources[resourceKey] is
                object resource)
            {
                contextMenu.Resources[resourceKey] =
                    resource;
            }
        }

        if (Resources["GlueNotesPopupSurfaceBrush"] is
            Brush backgroundBrush)
        {
            contextMenu.Background =
                backgroundBrush;
        }

        if (Resources["GlueNotesBorderBrush"] is
            Brush borderBrush)
        {
            contextMenu.BorderBrush =
                borderBrush;
        }

        if (Resources["GlueNotesTextBrush"] is
            Brush foregroundBrush)
        {
            contextMenu.Foreground =
                foregroundBrush;
        }

        ContextDeleteMenuItem.IsEnabled =
            NotesTreeView.SelectedItem is
                NoteSummary or
                NoteGroupViewModel;
    }

    private void ContextNewGroupMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        NewGroupButton_Click(
            sender,
            e);
    }

    private void ContextNewNoteMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        CreateNewNote(
            GetSelectedGroup());
    }

    private void ContextDeleteMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        switch (NotesTreeView.SelectedItem)
        {
            case NoteSummary note:
                DeleteNoteWithConfirmation(
                    note);
                break;

            case NoteGroupViewModel group:
                DeleteGroupWithConfirmation(
                    group);
                break;
        }
    }

    private MessageBoxResult ShowGlueNotesConfirmation(
        string message,
        string title,
        MessageBoxButton buttons)
    {
        Window dialog =
            new()
            {
                Owner =
                    this,
                Title =
                    title,
                WindowStyle =
                    WindowStyle.None,
                ResizeMode =
                    ResizeMode.NoResize,
                SizeToContent =
                    SizeToContent.WidthAndHeight,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ShowInTaskbar =
                    false,
                AllowsTransparency =
                    true,
                Background =
                    Brushes.Transparent
            };

        foreach (string resourceKey in
                 new[]
                 {
                     "GlueNotesPopupSurfaceBrush",
                     "GlueNotesSurfaceBrush",
                     "GlueNotesSurfaceHoverBrush",
                     "GlueNotesBorderBrush",
                     "GlueNotesTextBrush",
                     "GlueNotesMutedTextBrush",
                     "GlueNotesAccentBrush",
                     "GlueNotesSelectionBrush"
                 })
        {
            if (Resources[resourceKey] is
                object resource)
            {
                dialog.Resources[resourceKey] =
                    resource;
            }
        }

        if (Resources[typeof(Button)] is
            Style buttonStyle)
        {
            dialog.Resources[typeof(Button)] =
                buttonStyle;
        }

        Border border =
            new()
            {
                MinWidth =
                    360,
                MaxWidth =
                    520,
                Padding =
                    new Thickness(
                        18),
                CornerRadius =
                    new CornerRadius(
                        6),
                Background =
                    Resources["GlueNotesPopupSurfaceBrush"] as Brush,
                BorderBrush =
                    Resources["GlueNotesBorderBrush"] as Brush,
                BorderThickness =
                    new Thickness(
                        1)
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
                    GridLength.Auto
            });

        TextBlock messageText =
            new()
            {
                Text =
                    message,
                TextWrapping =
                    TextWrapping.Wrap,
                Foreground =
                    Resources["GlueNotesTextBrush"] as Brush,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        16)
            };

        Grid.SetRow(
            messageText,
            0);

        StackPanel buttonPanel =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        MessageBoxResult result =
            MessageBoxResult.Cancel;

        void AddButton(
            string text,
            MessageBoxResult buttonResult,
            bool isDefault = false,
            bool isCancel = false)
        {
            Button button =
                new()
                {
                    Content =
                        text,
                    MinWidth =
                        84,
                    Margin =
                        new Thickness(
                            6,
                            0,
                            0,
                            0),
                    IsDefault =
                        isDefault,
                    IsCancel =
                        isCancel
                };

            button.Click +=
                (_, _) =>
                {
                    result =
                        buttonResult;

                    dialog.DialogResult =
                        true;
                };

            buttonPanel.Children.Add(
                button);
        }

        switch (buttons)
        {
            case MessageBoxButton.YesNo:
                AddButton(
                    _language["Dialog.No"],
                    MessageBoxResult.No,
                    isCancel:
                        true);

                AddButton(
                    _language["Dialog.Yes"],
                    MessageBoxResult.Yes,
                    isDefault:
                        true);
                break;

            case MessageBoxButton.YesNoCancel:
                AddButton(
                    _language["Dialog.Cancel"],
                    MessageBoxResult.Cancel,
                    isCancel:
                        true);

                AddButton(
                    _language["Dialog.No"],
                    MessageBoxResult.No);

                AddButton(
                    _language["Dialog.Yes"],
                    MessageBoxResult.Yes,
                    isDefault:
                        true);
                break;

            default:
                AddButton(
                    _language["Dialog.Yes"],
                    MessageBoxResult.Yes,
                    isDefault:
                        true);
                break;
        }

        Grid.SetRow(
            buttonPanel,
            1);

        layout.Children.Add(
            messageText);

        layout.Children.Add(
            buttonPanel);

        border.Child =
            layout;

        dialog.Content =
            border;

        dialog.ShowDialog();

        return result;
    }

    private void DeleteNoteWithConfirmation(
        NoteSummary note)
    {
        MessageBoxResult result =
            ShowGlueNotesConfirmation(
                string.Format(
                    CultureInfo.CurrentCulture,
                    _language["Notes.DeletePrompt"],
                    note.Title),
                _language["Notes.DeleteTitle"],
                MessageBoxButton.YesNo);

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        SaveCurrentNote();

        NoteGroupViewModel? group =
            _groups.FirstOrDefault(
                item =>
                    item.Notes.Contains(
                        note));

        bool deletedCurrentNote =
            _currentNote?.Id ==
            note.Id;

        _repository.Delete(
            note.Id);

        group?.Notes.Remove(
            note);

        _ungroupedNotes.Remove(
            note);

        _notes.Remove(
            note);

        if (group is not null)
        {
            PersistNoteOrder(
                group);
        }
        else
        {
            PersistUngroupedNoteOrder();
        }

        if (deletedCurrentNote)
        {
            _currentNote =
                null;
        }

        RefreshTreeItems();
        ShowRemainingNoteOrEmpty();
    }

    private void DeleteGroupWithConfirmation(
        NoteGroupViewModel group)
    {
        List<NoteSummary> groupNotes =
            group.Notes.ToList();

        MessageBoxResult result =
            groupNotes.Count == 0
                ? MessageBoxResult.Yes
                : ShowGlueNotesConfirmation(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        _language["Groups.DeletePrompt"],
                        group.Name),
                    _language["Groups.DeleteTitle"],
                    MessageBoxButton.YesNoCancel);

        if (result ==
            MessageBoxResult.Cancel)
        {
            return;
        }

        SaveCurrentNote();

        bool currentNoteBelongsToGroup =
            _currentNote is not null &&
            groupNotes.Any(
                note =>
                    note.Id ==
                    _currentNote.Id);

        if (result ==
            MessageBoxResult.Yes)
        {
            foreach (NoteSummary note in
                     groupNotes)
            {
                _repository.Delete(
                    note.Id);

                _notes.Remove(
                    note);
            }

            group.Notes.Clear();

            if (currentNoteBelongsToGroup)
            {
                _currentNote =
                    null;
            }
        }
        else
        {
            NoteGroupViewModel generalGroup =
                EnsureGeneralGroup(
                    group);

            foreach (NoteSummary note in
                     groupNotes)
            {
                group.Notes.Remove(
                    note);

                generalGroup.Notes.Add(
                    note);
            }

            PersistNoteOrder(
                generalGroup);
        }

        _groups.Remove(
            group);

        EnsureGeneralGroup();

        RefreshTreeItems();
        SaveGroups();

        ShowRemainingNoteOrEmpty();
    }

    private NoteGroupViewModel EnsureGeneralGroup(
        NoteGroupViewModel? excludedGroup = null)
    {
        NoteGroupViewModel? generalGroup =
            _groups.FirstOrDefault(
                group =>
                    !ReferenceEquals(
                        group,
                        excludedGroup) &&
                    string.Equals(
                        group.Name,
                        _language["Groups.Default"],
                        StringComparison.CurrentCultureIgnoreCase));

        if (generalGroup is not null)
        {
            return generalGroup;
        }

        generalGroup =
            new NoteGroupViewModel
            {
                Id =
                    Guid.NewGuid(),
                Name =
                    _language["Groups.Default"],
                ColorHex =
                    GetDefaultGroupColorHex(),
                SortOrder =
                    0
            };

        _groups.Insert(
            0,
            generalGroup);

        return generalGroup;
    }

    private void ShowRemainingNoteOrEmpty()
    {
        UpdateEmptyState();

        NoteSummary? note =
            _currentNote is null
                ? _ungroupedNotes.FirstOrDefault() ??
                  _groups
                      .SelectMany(
                          group =>
                              group.Notes)
                      .FirstOrDefault()
                : _notes.FirstOrDefault(
                    item =>
                        item.Id ==
                        _currentNote.Id);

        if (note is not null)
        {
            LoadNote(
                note);

            SelectNote(
                note);

            return;
        }

        _saveTimer.Stop();

        _currentNote =
            null;

        SetActiveNote(
            null);

        _loading =
            true;

        try
        {
            TitleTextBox.Text =
                string.Empty;

            EditorRichTextBox.Document =
                new FlowDocument();
        }
        finally
        {
            _loading =
                false;
        }

        SaveStatusText.Text =
            _language["Status.Saved"];

        NoteGroupViewModel? firstGroup =
            _groups.FirstOrDefault();

        if (firstGroup is not null)
        {
            SelectGroup(
                firstGroup);
        }
    }

    private void NotesTreeView_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _dragStartPoint =
            e.GetPosition(
                NotesTreeView);

        if (e.OriginalSource is not
            DependencyObject source)
        {
            _dragCandidate =
                null;

            return;
        }

        TreeViewItem? sourceItem =
            FindVisualParent<TreeViewItem>(
                source);

        if (FindVisualParent<Button>(
                source) is not null &&
            sourceItem?.DataContext is not
                NoteGroupViewModel)
        {
            _dragCandidate =
                null;

            return;
        }

        _dragCandidate =
            sourceItem?.DataContext;
    }

    private void NotesTreeView_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (e.LeftButton !=
                MouseButtonState.Pressed ||
            _dragCandidate is null)
        {
            return;
        }

        Point current =
            e.GetPosition(
                NotesTreeView);

        if (Math.Abs(
                current.X -
                _dragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(
                current.Y -
                _dragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        object dragCandidate =
            _dragCandidate;

        _dragCandidate =
            null;

        SaveCurrentNote();

        DataObject data =
            new();

        if (dragCandidate is
            NoteSummary note)
        {
            data.SetData(
                typeof(NoteSummary),
                note);
        }
        else if (dragCandidate is
                 NoteGroupViewModel group)
        {
            data.SetData(
                typeof(NoteGroupViewModel),
                group);
        }
        else
        {
            return;
        }

        LogDragDrop(
            $"Drag started; Item={GetDragDropItemDescription(dragCandidate)}");

        try
        {
            DragDrop.DoDragDrop(
                NotesTreeView,
                data,
                DragDropEffects.Move);
        }
        finally
        {
            ClearDropPreview();

            LogDragDrop(
                $"Drag finished; Item={GetDragDropItemDescription(dragCandidate)}");
        }
    }

    private void NotesTreeView_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                System.Windows.DataFormats.FileDrop))
        {
            ClearDropPreview();

            e.Effects =
                DragDropEffects.Copy;

            e.Handled =
                true;

            return;
        }

        DependencyObject? source =
            e.OriginalSource as
                DependencyObject;

        TreeViewItem? targetItem =
            source is null
                ? null
                : FindVisualParent<TreeViewItem>(
                    source);

        if (e.Data.GetData(
                typeof(NoteGroupViewModel)) is
            NoteGroupViewModel draggedGroup)
        {
            if (targetItem?.DataContext is
                    NoteGroupViewModel targetGroup &&
                !ReferenceEquals(
                    draggedGroup,
                    targetGroup))
            {
                SetDropPreview(
                    targetGroup,
                    IsDropAfter(
                        targetItem,
                        e));
            }
            else
            {
                ClearDropPreview();
            }

            e.Effects =
                DragDropEffects.Move;

            e.Handled =
                true;

            return;
        }

        if (e.Data.GetData(
                typeof(NoteSummary)) is not
            NoteSummary draggedNote)
        {
            return;
        }

        switch (targetItem?.DataContext)
        {
            case NoteSummary targetNote
                when !ReferenceEquals(
                    draggedNote,
                    targetNote):
                SetDropPreview(
                    targetNote,
                    IsDropAfter(
                        targetItem,
                        e));
                break;

            case NoteGroupViewModel targetGroup:
                if (targetGroup.Notes.LastOrDefault() is
                    NoteSummary lastGroupNote)
                {
                    SetDropPreview(
                        lastGroupNote,
                        true);
                }
                else
                {
                    SetDropPreview(
                        targetGroup,
                        true);
                }
                break;

            default:
                if (_ungroupedNotes.LastOrDefault() is
                    NoteSummary lastUngroupedNote)
                {
                    SetDropPreview(
                        lastUngroupedNote,
                        true);
                }
                else
                {
                    ClearDropPreview();
                }
                break;
        }

        e.Effects =
            DragDropEffects.Move;

        e.Handled =
            true;
    }

    private void NotesTreeView_Drop(
        object sender,
        DragEventArgs e)
    {
        DependencyObject? source =
            e.OriginalSource as
                DependencyObject;

        TreeViewItem? targetItem =
            source is null
                ? null
                : FindVisualParent<TreeViewItem>(
                    source);

        NoteGroupViewModel? targetGroup =
            GetDropGroup(
                source);

        NoteSummary? targetNote =
            GetDropNote(
                source);

        bool insertAfter =
            targetItem is not null &&
            IsDropAfter(
                targetItem,
                e);

        ClearDropPreview();

        if (e.Data.GetDataPresent(
                System.Windows.DataFormats.FileDrop) &&
            e.Data.GetData(
                System.Windows.DataFormats.FileDrop) is
                string[] files)
        {
            LogDragDrop(
                $"Files dropped; Count={files.Length}; TargetGroup={targetGroup?.Name ?? GetSelectedGroup()?.Name ?? _groups[0].Name}");

            ImportDroppedFiles(
                files,
                targetGroup ??
                GetSelectedGroup() ??
                _groups[0]);

            e.Handled =
                true;

            return;
        }

        if (e.Data.GetData(
                typeof(NoteSummary)) is
            NoteSummary draggedNote)
        {
            LogDragDrop(
                $"Note dropped; Item={GetDragDropItemDescription(draggedNote)}; TargetGroup={targetGroup?.Name ?? "<ungrouped>"}; TargetNote={targetNote?.Title ?? "<none>"}; Position={(insertAfter ? "After" : "Before")}");

            MoveNote(
                draggedNote,
                targetGroup,
                targetNote,
                insertAfter);

            e.Handled =
                true;

            return;
        }

        if (e.Data.GetData(
                typeof(NoteGroupViewModel)) is
            NoteGroupViewModel draggedGroup &&
            targetItem?.DataContext is
            NoteGroupViewModel targetGroupItem)
        {
            LogDragDrop(
                $"Group dropped; Item={GetDragDropItemDescription(draggedGroup)}; Target={GetDragDropItemDescription(targetGroupItem)}; Position={(insertAfter ? "After" : "Before")}");

            MoveGroup(
                draggedGroup,
                targetGroupItem,
                insertAfter);

            e.Handled =
                true;
        }
    }

    private void LogDragDrop(
        string message)
    {
        _log?.Invoke(
            $"DragDrop; {message}");
    }

    private static string GetDragDropItemDescription(
        object item)
    {
        return item switch
        {
            NoteSummary note =>
                $"Note:{note.Id}:{note.Title}",
            NoteGroupViewModel group =>
                $"Group:{group.Id}:{group.Name}",
            _ =>
                item.GetType().Name
        };
    }

    private static bool IsDropAfter(
        TreeViewItem targetItem,
        DragEventArgs e)
    {
        Point position =
            e.GetPosition(
                targetItem);

        return
            position.Y >
            targetItem.ActualHeight / 2.0;
    }

    private void SetDropPreview(
        object item,
        bool after)
    {
        if (ReferenceEquals(
                _dropPreviewItem,
                item) &&
            _dropPreviewAfter ==
            after)
        {
            return;
        }

        ClearDropPreview();

        _dropPreviewItem =
            item;

        _dropPreviewAfter =
            after;

        LogDragDrop(
            $"Preview changed; Target={GetDragDropItemDescription(item)}; Position={(after ? "After" : "Before")}");

        switch (item)
        {
            case NoteSummary note:
                note.IsDropPreviewBefore =
                    !after;

                note.IsDropPreviewAfter =
                    after;
                break;

            case NoteGroupViewModel group:
                group.IsDropPreviewBefore =
                    !after;

                group.IsDropPreviewAfter =
                    after;
                break;
        }
    }

    private void ClearDropPreview()
    {
        switch (_dropPreviewItem)
        {
            case NoteSummary note:
                note.IsDropPreviewBefore =
                    false;

                note.IsDropPreviewAfter =
                    false;
                break;

            case NoteGroupViewModel group:
                group.IsDropPreviewBefore =
                    false;

                group.IsDropPreviewAfter =
                    false;
                break;
        }

        _dropPreviewItem =
            null;

        _dropPreviewAfter =
            false;
    }

    private NoteGroupViewModel? GetDropGroup(
        DependencyObject? source)
    {
        TreeViewItem? item =
            source is null
                ? null
                : FindVisualParent<TreeViewItem>(
                    source);

        if (item?.DataContext is
            NoteGroupViewModel group)
        {
            return group;
        }

        if (item?.DataContext is
            NoteSummary note)
        {
            return
                _groups.FirstOrDefault(
                    candidate =>
                        candidate.Notes.Contains(
                            note));
        }

        return null;
    }

    private NoteSummary? GetDropNote(
        DependencyObject? source)
    {
        TreeViewItem? item =
            source is null
                ? null
                : FindVisualParent<TreeViewItem>(
                    source);

        return
            item?.DataContext as
                NoteSummary;
    }

    private static T? FindVisualParent<T>(
        DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current =
                current is ContentElement contentElement
                    ? ContentOperations.GetParent(
                        contentElement)
                    : VisualTreeHelper.GetParent(
                        current);
        }

        return null;
    }

    private void MoveNote(
        NoteSummary note,
        NoteGroupViewModel? targetGroup,
        NoteSummary? targetNote,
        bool insertAfter = false)
    {
        if (ReferenceEquals(
                note,
                targetNote))
        {
            return;
        }

        NoteGroupViewModel? sourceGroup =
            _groups.FirstOrDefault(
                group =>
                    group.Notes.Contains(
                        note));

        bool sourceIsUngrouped =
            _ungroupedNotes.Contains(
                note);

        if (sourceGroup is null &&
            !sourceIsUngrouped)
        {
            return;
        }

        sourceGroup?.Notes.Remove(
            note);

        if (sourceIsUngrouped)
        {
            _ungroupedNotes.Remove(
                note);
        }

        if (targetGroup is null)
        {
            int insertIndex =
                targetNote is not null &&
                _ungroupedNotes.Contains(
                    targetNote)
                    ? _ungroupedNotes.IndexOf(
                        targetNote) +
                      (insertAfter
                          ? 1
                          : 0)
                    : _ungroupedNotes.Count;

            insertIndex =
                Math.Clamp(
                    insertIndex,
                    0,
                    _ungroupedNotes.Count);

            _ungroupedNotes.Insert(
                insertIndex,
                note);

            note.GroupId =
                Guid.Empty;

            PersistUngroupedNoteOrder();
        }
        else
        {
            int insertIndex =
                targetNote is not null &&
                targetGroup.Notes.Contains(
                    targetNote)
                    ? targetGroup.Notes.IndexOf(
                        targetNote) +
                      (insertAfter
                          ? 1
                          : 0)
                    : targetGroup.Notes.Count;

            insertIndex =
                Math.Clamp(
                    insertIndex,
                    0,
                    targetGroup.Notes.Count);

            targetGroup.Notes.Insert(
                insertIndex,
                note);

            note.GroupId =
                targetGroup.Id;

            PersistNoteOrder(
                targetGroup);
        }

        if (sourceGroup is not null &&
            !ReferenceEquals(
                sourceGroup,
                targetGroup))
        {
            PersistNoteOrder(
                sourceGroup);
        }

        if (sourceIsUngrouped &&
            targetGroup is not null)
        {
            PersistUngroupedNoteOrder();
        }

        if (_currentNote?.Id ==
            note.Id)
        {
            _currentNote.GroupId =
                note.GroupId;

            _currentNote.SortOrder =
                note.SortOrder;
        }

        RefreshTreeItems();

        SelectNote(
            note);
    }

    private void MoveGroup(
        NoteGroupViewModel group,
        NoteGroupViewModel targetGroup,
        bool insertAfter = false)
    {
        if (ReferenceEquals(
                group,
                targetGroup))
        {
            return;
        }

        int oldIndex =
            _groups.IndexOf(
                group);

        if (oldIndex < 0)
        {
            return;
        }

        _groups.RemoveAt(
            oldIndex);

        int targetIndex =
            _groups.IndexOf(
                targetGroup);

        if (targetIndex < 0)
        {
            _groups.Insert(
                Math.Clamp(
                    oldIndex,
                    0,
                    _groups.Count),
                group);

            return;
        }

        int insertIndex =
            targetIndex +
            (insertAfter
                ? 1
                : 0);

        _groups.Insert(
            Math.Clamp(
                insertIndex,
                0,
                _groups.Count),
            group);

        RefreshTreeItems();
        SaveGroups();
    }

    private void PersistNoteOrder(
        NoteGroupViewModel group)
    {
        for (int index = 0;
             index < group.Notes.Count;
             index++)
        {
            NoteSummary note =
                group.Notes[index];

            note.GroupId =
                group.Id;

            note.SortOrder =
                index;

            if (_currentNote?.Id ==
                note.Id)
            {
                _currentNote.GroupId =
                    group.Id;

                _currentNote.SortOrder =
                    index;
            }

            _repository.UpdateNoteLocation(
                note.Id,
                group.Id,
                index);
        }
    }

    private void PersistUngroupedNoteOrder()
    {
        for (int index = 0;
             index < _ungroupedNotes.Count;
             index++)
        {
            NoteSummary note =
                _ungroupedNotes[index];

            note.GroupId =
                Guid.Empty;

            note.SortOrder =
                index;

            if (_currentNote?.Id ==
                note.Id)
            {
                _currentNote.GroupId =
                    Guid.Empty;

                _currentNote.SortOrder =
                    index;
            }

            _repository.UpdateNoteLocation(
                note.Id,
                Guid.Empty,
                index);
        }
    }

    private void RefreshTreeItems()
    {
        _treeItems.Clear();

        foreach (NoteGroupViewModel group in
                 _groups)
        {
            _treeItems.Add(
                group);
        }

        foreach (NoteSummary note in
                 _ungroupedNotes)
        {
            _treeItems.Add(
                note);
        }

        NotesTreeView.ItemsSource =
            _treeItems;
    }

    private void ImportDroppedFiles(
        IReadOnlyList<string> files,
        NoteGroupViewModel group)
    {
        string[] supportedExtensions =
        {
            ".txt",
            ".md",
            ".html",
            ".htm",
            ".rtf"
        };

        if (files.Count == 0 ||
            files.Any(
                file =>
                    !File.Exists(
                        file) ||
                    !supportedExtensions.Contains(
                        Path.GetExtension(
                                file)
                            .ToLowerInvariant())))
        {
            ShowUnsupportedImport(
                supportedExtensions);

            return;
        }

        NoteSummary? lastImported =
            null;

        foreach (string file in
                 files)
        {
            try
            {
                lastImported =
                    ImportNoteFile(
                        file,
                        group);
            }
            catch
            {
                ShowUnsupportedImport(
                    supportedExtensions);

                return;
            }
        }

        UpdateEmptyState();

        if (lastImported is not null)
        {
            LoadNote(
                lastImported);

            SelectNote(
                lastImported);
        }
    }

    private NoteSummary ImportNoteFile(
        string filePath,
        NoteGroupViewModel group)
    {
        string extension =
            Path.GetExtension(
                    filePath)
                .ToLowerInvariant();

        List<NoteBlockData> blocks;

        if (extension ==
            ".rtf")
        {
            FlowDocument document =
                new();

            using FileStream stream =
                File.OpenRead(
                    filePath);

            TextRange range =
                new(
                    document.ContentStart,
                    document.ContentEnd);

            range.Load(
                stream,
                System.Windows.DataFormats.Rtf);

            blocks =
                _documentConverter.ReadDocument(
                    document);
        }
        else
        {
            string text =
                File.ReadAllText(
                    filePath);

            if (extension is
                ".html" or
                ".htm")
            {
                text =
                    ConvertHtmlToPlainText(
                        text);
            }

            blocks =
                new List<NoteBlockData>
                {
                    new ParagraphBlockData
                    {
                        Inlines =
                            new List<NoteInlineData>
                            {
                                new TextRunData
                                {
                                    Text =
                                        text,
                                    FontSize =
                                        _settings.DefaultFontSize
                                }
                            }
                    }
                };
        }

        DateTime now =
            DateTime.UtcNow;

        NoteData note =
            new()
            {
                Id =
                    Guid.NewGuid(),
                Title =
                    Path.GetFileNameWithoutExtension(
                        filePath),
                CreatedAtUtc =
                    now,
                UpdatedAtUtc =
                    now,
                GroupId =
                    group.Id,
                SortOrder =
                    group.Notes.Count,
                Blocks =
                    blocks
            };

        _repository.Save(
            note);

        NoteSummary summary =
            new()
            {
                Id =
                    note.Id,
                Title =
                    note.Title,
                UpdatedAtUtc =
                    note.UpdatedAtUtc,
                GroupId =
                    note.GroupId,
                SortOrder =
                    note.SortOrder
            };

        _notes.Add(
            summary);

        group.Notes.Add(
            summary);

        return summary;
    }

    private static string ConvertHtmlToPlainText(
        string html)
    {
        string text =
            Regex.Replace(
                html,
                @"<(script|style)\b[^>]*>.*?</\1>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<(br\s*/?|/p|/div|/li|/h[1-6])\s*>",
                Environment.NewLine,
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"<[^>]+>",
                string.Empty,
                RegexOptions.Singleline);

        return
            WebUtility.HtmlDecode(
                    text)
                .Replace(
                    "\r\n\r\n\r\n",
                    "\r\n\r\n");
    }

    private void ShowUnsupportedImport(
        IReadOnlyList<string> supportedExtensions)
    {
        MessageBox.Show(
            this,
            string.Format(
                CultureInfo.CurrentCulture,
                _language["Import.UnsupportedMessage"],
                string.Join(
                    ", ",
                    supportedExtensions)),
            _language["Import.UnsupportedTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ExportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveCurrentNote();

        if (_currentNote is null)
        {
            return;
        }

        SaveFileDialog dialog =
            new()
            {
                Title =
                    _language["Export.Title"],
                Filter =
                    _language["Export.Filter"],
                FilterIndex =
                    1,
                DefaultExt =
                    ".rtf",
                AddExtension =
                    true,
                FileName =
                    GetSafeFileName(
                        _currentNote.Title)
            };

        if (dialog.ShowDialog(
                this) !=
            true)
        {
            return;
        }

        string extension =
            Path.GetExtension(
                    dialog.FileName)
                .ToLowerInvariant();

        try
        {
            switch (extension)
            {
                case ".rtf":
                    using (FileStream stream =
                           File.Create(
                               dialog.FileName))
                    {
                        TextRange range =
                            new(
                                EditorRichTextBox.Document.ContentStart,
                                EditorRichTextBox.Document.ContentEnd);

                        range.Save(
                            stream,
                            System.Windows.DataFormats.Rtf);
                    }

                    break;

                case ".md":
                    File.WriteAllText(
                        dialog.FileName,
                        BuildMarkdown(
                            _currentNote));
                    break;

                case ".html":
                case ".htm":
                    File.WriteAllText(
                        dialog.FileName,
                        BuildHtml(
                            _currentNote));
                    break;

                default:
                    File.WriteAllText(
                        dialog.FileName,
                        BuildPlainText(
                            _currentNote));
                    break;
            }
        }
        catch
        {
            MessageBox.Show(
                this,
                _language["Export.FailedMessage"],
                _language["Export.FailedTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private string GetSafeFileName(
        string title)
    {
        char[] invalid =
            Path.GetInvalidFileNameChars();

        string safe =
            new(
                title
                    .Select(
                        character =>
                            invalid.Contains(
                                character)
                                ? '_'
                                : character)
                    .ToArray());

        return
            string.IsNullOrWhiteSpace(
                safe)
                ? _language["Notes.Untitled"]
                : safe;
    }

    private static IEnumerable<NoteInlineData> GetParagraphInlines(
        ParagraphBlockData paragraph)
    {
        return
            paragraph.Inlines.Count > 0
                ? paragraph.Inlines
                : paragraph.Runs;
    }

    private string BuildPlainText(
        NoteData note)
    {
        StringBuilder builder =
            new();

        foreach (NoteBlockData block in
                 note.Blocks)
        {
            switch (block)
            {
                case ParagraphBlockData paragraph:
                    foreach (NoteInlineData inline in
                             GetParagraphInlines(
                                 paragraph))
                    {
                        if (inline is
                            TextRunData run)
                        {
                            builder.Append(
                                run.Text);
                        }
                    }

                    builder.AppendLine();
                    break;

                case CheckListBlockData checklist:
                    foreach (CheckListItemData item in
                             checklist.Items)
                    {
                        builder.Append(
                            item.IsChecked
                                ? "[x] "
                                : "[ ] ");

                        builder.AppendLine(
                            item.Text);
                    }

                    break;
            }
        }

        return
            builder.ToString();
    }

    private string BuildMarkdown(
        NoteData note)
    {
        StringBuilder builder =
            new();

        foreach (NoteBlockData block in
                 note.Blocks)
        {
            switch (block)
            {
                case ParagraphBlockData paragraph:
                    foreach (NoteInlineData inline in
                             GetParagraphInlines(
                                 paragraph))
                    {
                        switch (inline)
                        {
                            case TextRunData run:
                            {
                                string text =
                                    run.Text;

                                if (run.Bold)
                                {
                                    text =
                                        $"**{text}**";
                                }

                                if (run.Italic)
                                {
                                    text =
                                        $"*{text}*";
                                }

                                if (run.Underline)
                                {
                                    text =
                                        $"<u>{text}</u>";
                                }

                                builder.Append(
                                    text);
                                break;
                            }

                            case ImageInlineData image:
                                AppendMarkdownImage(
                                    builder,
                                    image.AssetRelativePath,
                                    image.DisplayWidth);
                                break;
                        }
                    }

                    builder.AppendLine();
                    builder.AppendLine();
                    break;

                case ImageBlockData image:
                    AppendMarkdownImage(
                        builder,
                        image.AssetRelativePath,
                        image.DisplayWidth);

                    builder.AppendLine();
                    builder.AppendLine();
                    break;

                case CheckListBlockData checklist:
                    foreach (CheckListItemData item in
                             checklist.Items)
                    {
                        builder.Append(
                            item.IsChecked
                                ? "- [x] "
                                : "- [ ] ");

                        builder.AppendLine(
                            item.Text);
                    }

                    builder.AppendLine();
                    break;
            }
        }

        return
            builder.ToString();
    }

    private string BuildHtml(
        NoteData note)
    {
        StringBuilder builder =
            new();

        builder.Append(
            "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>");

        foreach (NoteBlockData block in
                 note.Blocks)
        {
            switch (block)
            {
                case ParagraphBlockData paragraph:
                    builder.Append(
                        "<p>");

                    foreach (NoteInlineData inline in
                             GetParagraphInlines(
                                 paragraph))
                    {
                        switch (inline)
                        {
                            case TextRunData run:
                            {
                                string text =
                                    WebUtility.HtmlEncode(
                                            run.Text)
                                        .Replace(
                                            "\r\n",
                                            "<br>")
                                        .Replace(
                                            "\n",
                                            "<br>");

                                StringBuilder style =
                                    new();

                                if (run.FontSize > 0)
                                {
                                    style.Append(
                                        $"font-size:{run.FontSize.ToString("0.##", CultureInfo.InvariantCulture)}px;");
                                }

                                if (!string.IsNullOrWhiteSpace(
                                        run.FontFamilyName))
                                {
                                    style.Append(
                                        $"font-family:'{WebUtility.HtmlEncode(run.FontFamilyName)}';");
                                }

                                if (!string.IsNullOrWhiteSpace(
                                        run.ForegroundHex))
                                {
                                    style.Append(
                                        $"color:{WebUtility.HtmlEncode(run.ForegroundHex)};");
                                }

                                if (!string.IsNullOrWhiteSpace(
                                        run.BackgroundHex))
                                {
                                    style.Append(
                                        $"background-color:{WebUtility.HtmlEncode(run.BackgroundHex)};");
                                }

                                if (style.Length > 0)
                                {
                                    text =
                                        $"<span style=\"{style}\">{text}</span>";
                                }

                                if (run.Underline)
                                {
                                    text =
                                        $"<u>{text}</u>";
                                }

                                if (run.Italic)
                                {
                                    text =
                                        $"<em>{text}</em>";
                                }

                                if (run.Bold)
                                {
                                    text =
                                        $"<strong>{text}</strong>";
                                }

                                builder.Append(
                                    text);
                                break;
                            }

                            case ImageInlineData image:
                                AppendHtmlImage(
                                    builder,
                                    image.AssetRelativePath,
                                    image.DisplayWidth);
                                break;
                        }
                    }

                    builder.Append(
                        "</p>");
                    break;

                case ImageBlockData image:
                    AppendHtmlImage(
                        builder,
                        image.AssetRelativePath,
                        image.DisplayWidth);
                    break;

                case CheckListBlockData checklist:
                    builder.Append(
                        "<ul>");

                    foreach (CheckListItemData item in
                             checklist.Items)
                    {
                        builder.Append(
                            "<li>");

                        builder.Append(
                            item.IsChecked
                                ? "☑ "
                                : "☐ ");

                        builder.Append(
                            WebUtility.HtmlEncode(
                                item.Text));

                        builder.Append(
                            "</li>");
                    }

                    builder.Append(
                        "</ul>");
                    break;
            }
        }

        builder.Append(
            "</body></html>");

        return
            builder.ToString();
    }

    private void AppendMarkdownImage(
        StringBuilder builder,
        string assetRelativePath,
        double displayWidth)
    {
        string? dataUri =
            GetImageDataUri(
                assetRelativePath);

        if (dataUri is null)
        {
            return;
        }

        builder.Append(
            "<img src=\"");

        builder.Append(
            dataUri);

        builder.Append(
            "\"");

        if (displayWidth > 0)
        {
            builder.Append(
                $" width=\"{displayWidth.ToString("0.##", CultureInfo.InvariantCulture)}\"");
        }

        builder.Append(
            ">");
    }

    private void AppendHtmlImage(
        StringBuilder builder,
        string assetRelativePath,
        double displayWidth)
    {
        string? dataUri =
            GetImageDataUri(
                assetRelativePath);

        if (dataUri is null)
        {
            return;
        }

        builder.Append(
            "<img src=\"");

        builder.Append(
            dataUri);

        builder.Append(
            "\"");

        if (displayWidth > 0)
        {
            builder.Append(
                $" width=\"{displayWidth.ToString("0.##", CultureInfo.InvariantCulture)}\"");
        }

        builder.Append(
            ">");
    }

    private string? GetImageDataUri(
        string assetRelativePath)
    {
        string absolutePath =
            _repository.GetAbsoluteAssetPath(
                assetRelativePath);

        if (!File.Exists(
                absolutePath))
        {
            return null;
        }

        string mimeType =
            Path.GetExtension(
                    absolutePath)
                .ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" =>
                    "image/jpeg",
                ".gif" =>
                    "image/gif",
                ".bmp" =>
                    "image/bmp",
                ".webp" =>
                    "image/webp",
                _ =>
                    "image/png"
            };

        return
            $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(absolutePath))}";
    }

    private async void SaveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        await SaveCurrentNoteAsync();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        SaveCurrentNote();
        PersistWindowLayout();
        ClearDropPreview();

        _saveTimer.Stop();
        _layoutSaveTimer.Stop();

#if GLUEDOCK_PLUGIN
        _nativeBackdropHost?.Dispose();
        _nativeBackdropHost =
            null;
#endif

        base.OnClosed(
            e);
    }
}
