using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

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
    private readonly DocumentConverter _documentConverter;

#if GLUEDOCK_PLUGIN
    private DockDialogNativeBackdropHost? _nativeBackdropHost;
    private Action? _openSettings;
    private Func<string, string>? _hostLocalize;
#endif

    private ObservableCollection<NoteSummary> _notes =
        new();

    private NoteData? _currentNote;
    private bool _loading;
    private bool _selectionSwitchInProgress;

    public MainWindow()
        : this(
            new NoteRepository(),
            new LanguageService(),
            new GlueNotesSettings(),
            null)
    {
    }

    public MainWindow(
        NoteRepository repository,
        LanguageService language,
        GlueNotesSettings settings,
        GlueNotesAppearance? appearance)
    {
        _repository =
            repository;

        _language =
            language;

        _settings =
            settings;

        InitializeComponent();

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
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        _settings.AutoSaveDelayMilliseconds)
            };

        _saveTimer.Tick +=
            SaveTimer_Tick;

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
        Func<string, string>? hostLocalize)
        : this(
            repository,
            language,
            settings,
            null)
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
        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                appearance);

        Resources["GlueNotesWindowBrush"] =
            palette.WindowBackgroundBrush;

        Resources["GlueNotesSurfaceBrush"] =
            palette.ControlBackgroundBrush;

        Resources["GlueNotesSurfaceHoverBrush"] =
            palette.PopupHighlightBrush;

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

        DockDialogThemeService.ApplyListBox(
            NotesListBox,
            palette,
            false);

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
                    ApplyScrollViewerTheme(
                        NotesListBox,
                        palette);

                    ApplyScrollViewerTheme(
                        EditorRichTextBox,
                        palette);
                }));
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

        NotesHeaderText.Text =
            _language["App.Title"];

        EmptyNotesText.Text =
            _language["Notes.Empty"];

        TitleTextBox.ToolTip =
            _language["Editor.TitlePlaceholder"];

        NewNoteButton.ToolTip =
            _language["Notes.New"];

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

        FontSizeComboBox.ToolTip =
            _language["Toolbar.FontSize"];

        TextColorButton.ToolTip =
            _language["Toolbar.TextColor"];

        InsertChecklistButton.ToolTip =
            _language["Toolbar.InsertChecklist"];

        SaveStatusText.Text =
            _language["Status.Saved"];
    }

    private void InitializeToolbar()
    {
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

        NotesListBox.ItemsSource =
            _notes;

        UpdateEmptyState();

        if (_notes.Count > 0)
        {
            NotesListBox.SelectedIndex =
                0;
        }
        else
        {
            CreateNewNote();
        }
    }

    private void CreateNewNote()
    {
        SaveCurrentNote();

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
                    note.UpdatedAtUtc
            };

        _notes.Insert(
            0,
            summary);

        UpdateEmptyState();

        NotesListBox.SelectedItem =
            summary;
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
        }
        finally
        {
            _loading =
                false;
        }

        SaveStatusText.Text =
            _language["Status.Saved"];

        EditorRichTextBox.Focus();
    }

    private void QueueSave()
    {
        if (_loading ||
            _currentNote is null)
        {
            return;
        }

        SaveStatusText.Text =
            _language["Status.Saving"];

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveCurrentNote()
    {
        _saveTimer.Stop();

        if (_loading ||
            _currentNote is null)
        {
            return;
        }

        _currentNote.Title =
            string.IsNullOrWhiteSpace(
                TitleTextBox.Text)
                ? _language["Notes.Untitled"]
                : TitleTextBox.Text.Trim();

        _currentNote.Blocks =
            _documentConverter.ReadDocument(
                EditorRichTextBox.Document);

        _repository.Save(
            _currentNote);

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

        SaveStatusText.Text =
            _language["Status.Saved"];
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

    private void ApplySelectedFontSize(
        double size)
    {
        EditorRichTextBox.Selection.ApplyPropertyValue(
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

        EditorRichTextBox.Selection.ApplyPropertyValue(
            TextElement.ForegroundProperty,
            brush);

        TextColorPreview.Background =
            brush;

        EditorRichTextBox.Focus();

        QueueSave();
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
        CreateNewNote();
    }

    private void NotesListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_selectionSwitchInProgress ||
            NotesListBox.SelectedItem is not
                NoteSummary selected)
        {
            return;
        }

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
        QueueSave();
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
        System.Windows.Media.Color currentColor =
            TextColorPreview.Background is
                SolidColorBrush currentBrush
                ? currentBrush.Color
                : System.Windows.Media.Colors.White;

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

        ApplySelectedColor(
            System.Windows.Media.Color.FromArgb(
                dialog.Color.A,
                dialog.Color.R,
                dialog.Color.G,
                dialog.Color.B));
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
    }

    private void SaveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        SaveCurrentNote();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        SaveCurrentNote();

        _saveTimer.Stop();

#if GLUEDOCK_PLUGIN
        _nativeBackdropHost?.Dispose();
        _nativeBackdropHost =
            null;
#endif

        base.OnClosed(
            e);
    }
}
