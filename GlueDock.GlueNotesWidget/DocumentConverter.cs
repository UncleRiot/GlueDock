using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlueNotes;

public sealed class DocumentConverter
{
    private readonly NoteRepository _repository;
    private readonly LanguageService _language;
    private readonly GlueNotesSettings _settings;

    public DocumentConverter(
        NoteRepository repository,
        LanguageService language,
        GlueNotesSettings? settings = null)
    {
        _repository =
            repository;

        _language =
            language;

        _settings =
            settings ??
            new GlueNotesSettings();
    }

    public FlowDocument CreateDocument(
        NoteData note,
        RoutedEventHandler checklistChanged,
        TextChangedEventHandler checklistTextChanged,
        DragCompletedEventHandler imageResizeCompleted)
    {
        FlowDocument document =
            new()
            {
                PagePadding =
                    new Thickness(
                        0),
                FontFamily =
                    new System.Windows.Media.FontFamily(
                        "Segoe UI"),
                FontSize =
                    _settings.DefaultFontSize
            };

        Style paragraphStyle =
            new(
                typeof(Paragraph));

        paragraphStyle.Setters.Add(
            new Setter(
                Paragraph.MarginProperty,
                new Thickness(
                    0)));

        document.Resources.Add(
            typeof(Paragraph),
            paragraphStyle);

        foreach (NoteBlockData block in
                 note.Blocks)
        {
            switch (block)
            {
                case ParagraphBlockData paragraph:
                    document.Blocks.Add(
                        CreateParagraph(
                            paragraph,
                            imageResizeCompleted));
                    break;

                case ImageBlockData image:
                    Paragraph imageParagraph =
                        new()
                        {
                            Margin =
                                new Thickness(
                                    0)
                        };

                    imageParagraph.Inlines.Add(
                        CreateImageInline(
                            new ImageInlineData
                            {
                                AssetRelativePath =
                                    image.AssetRelativePath,
                                DisplayWidth =
                                    image.DisplayWidth
                            },
                            imageResizeCompleted));

                    document.Blocks.Add(
                        imageParagraph);
                    break;

                case CheckListBlockData checklist:
                    document.Blocks.Add(
                        CreateCheckListBlock(
                            checklist,
                            checklistChanged,
                            checklistTextChanged));
                    break;
            }
        }

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(
                new Paragraph
                {
                    Margin =
                        new Thickness(
                            0)
                });
        }

        return document;
    }

    public List<NoteBlockData> ReadDocument(
        FlowDocument document)
    {
        List<NoteBlockData> blocks =
            new();

        foreach (Block block in
                 document.Blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    blocks.Add(
                        ReadParagraph(
                            paragraph));
                    break;

                case BlockUIContainer uiContainer:
                    NoteBlockData? uiBlock =
                        ReadUiBlock(
                            uiContainer);

                    if (uiBlock is not null)
                    {
                        blocks.Add(
                            uiBlock);
                    }

                    break;
            }
        }

        return blocks;
    }

    public InlineUIContainer CreateImageInline(
        string assetRelativePath,
        DragCompletedEventHandler imageResizeCompleted)
    {
        return
            CreateImageInline(
                new ImageInlineData
                {
                    AssetRelativePath =
                        assetRelativePath,
                    DisplayWidth =
                        0
                },
                imageResizeCompleted);
    }

    public BlockUIContainer CreateCheckListBlock(
        RoutedEventHandler checklistChanged,
        TextChangedEventHandler checklistTextChanged)
    {
        return
            CreateCheckListBlock(
                new CheckListBlockData
                {
                    Items =
                        new List<CheckListItemData>
                        {
                            new()
                            {
                                Text =
                                    _language["Checklist.NewItem"]
                            }
                        }
                },
                checklistChanged,
                checklistTextChanged);
    }

    private Paragraph CreateParagraph(
        ParagraphBlockData data,
        DragCompletedEventHandler imageResizeCompleted)
    {
        Paragraph paragraph =
            new()
            {
                Margin =
                    new Thickness(
                        0)
            };

        IEnumerable<NoteInlineData> inlineData =
            data.Inlines.Count > 0
                ? data.Inlines
                : data.Runs;

        foreach (NoteInlineData item in
                 inlineData)
        {
            switch (item)
            {
                case TextRunData runData:
                {
                    Run run =
                        new(
                            runData.Text)
                        {
                            FontWeight =
                                runData.Bold
                                    ? FontWeights.Bold
                                    : FontWeights.Normal,
                            FontStyle =
                                runData.Italic
                                    ? FontStyles.Italic
                                    : FontStyles.Normal,
                            FontSize =
                                runData.FontSize > 0
                                    ? runData.FontSize
                                    : 14
                        };

                    if (!string.IsNullOrWhiteSpace(
                            runData.FontFamilyName))
                    {
                        run.FontFamily =
                            new System.Windows.Media.FontFamily(
                                runData.FontFamilyName);
                    }

                    if (runData.Underline)
                    {
                        run.TextDecorations =
                            TextDecorations.Underline;
                    }

                    if (TryParseBrush(
                            runData.ForegroundHex,
                            out System.Windows.Media.Brush? foregroundBrush))
                    {
                        run.Foreground =
                            foregroundBrush;
                    }

                    if (TryParseBrush(
                            runData.BackgroundHex,
                            out System.Windows.Media.Brush? backgroundBrush))
                    {
                        run.Background =
                            backgroundBrush;
                    }

                    paragraph.Inlines.Add(
                        run);

                    break;
                }

                case ImageInlineData imageData:
                    paragraph.Inlines.Add(
                        CreateImageInline(
                            imageData,
                            imageResizeCompleted));
                    break;
            }
        }

        return paragraph;
    }

    private ParagraphBlockData ReadParagraph(
        Paragraph paragraph)
    {
        ParagraphBlockData result =
            new();

        foreach (Inline inline in
                 paragraph.Inlines)
        {
            ReadInline(
                inline,
                result.Inlines);
        }

        return result;
    }

    private void ReadInline(
        Inline inline,
        List<NoteInlineData> target)
    {
        if (inline is Run run)
        {
            target.Add(
                new TextRunData
                {
                    Text =
                        run.Text,
                    Bold =
                        run.FontWeight >=
                        FontWeights.Bold,
                    Italic =
                        run.FontStyle ==
                        FontStyles.Italic,
                    Underline =
                        run.TextDecorations?.Contains(
                            TextDecorations.Underline[0]) ==
                        true,
                    FontSize =
                        run.FontSize,
                    FontFamilyName =
                        run.FontFamily?.Source,
                    ForegroundHex =
                        BrushToHex(
                            run.Foreground),
                    BackgroundHex =
                        BrushToHex(
                            run.Background)
                });

            return;
        }

        if (inline is LineBreak)
        {
            target.Add(
                new TextRunData
                {
                    Text =
                        Environment.NewLine
                });

            return;
        }

        if (inline is InlineUIContainer uiContainer)
        {
            System.Windows.Controls.Image? image =
                uiContainer.Child as
                    System.Windows.Controls.Image;

            if (image is null &&
                uiContainer.Child is Grid imageContainer)
            {
                image =
                    imageContainer.Children
                        .OfType<System.Windows.Controls.Image>()
                        .FirstOrDefault();
            }

            if (image?.Tag is string assetRelativePath)
            {
                target.Add(
                    new ImageInlineData
                    {
                        AssetRelativePath =
                            assetRelativePath,
                        DisplayWidth =
                            image.ActualWidth > 0
                                ? image.ActualWidth
                                : image.Width
                    });

                return;
            }
        }

        if (inline is Span span)
        {
            foreach (Inline child in
                     span.Inlines)
            {
                ReadInline(
                    child,
                    target);
            }
        }
    }

    private InlineUIContainer CreateImageInline(
        ImageInlineData data,
        DragCompletedEventHandler imageResizeCompleted)
    {
        string absolutePath =
            _repository.GetAbsoluteAssetPath(
                data.AssetRelativePath);

        System.Windows.Controls.Image image =
            new()
            {
                Stretch =
                    Stretch.Uniform,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Tag =
                    data.AssetRelativePath
            };

        if (File.Exists(
                absolutePath))
        {
            BitmapImage bitmap =
                new();

            bitmap.BeginInit();
            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;
            bitmap.UriSource =
                new Uri(
                    absolutePath,
                    UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            image.Source =
                bitmap;

            double naturalWidth =
                bitmap.Width;

            double requestedWidth =
                data.DisplayWidth > 0
                    ? data.DisplayWidth
                    : naturalWidth;

            double minimumWidth =
                Math.Min(
                    32,
                    Math.Max(
                        1,
                        _settings.MaxImageWidth));

            double maximumWidth =
                Math.Max(
                    minimumWidth,
                    _settings.MaxImageWidth);

            image.Width =
                Math.Clamp(
                    requestedWidth,
                    minimumWidth,
                    maximumWidth);
        }

        Grid imageContainer =
            new()
            {
                Tag =
                    data.AssetRelativePath
            };

        imageContainer.Children.Add(
            image);

        FrameworkElementFactory resizeThumbBorder =
            new(
                typeof(
                    Border));

        resizeThumbBorder.SetValue(
            Border.BackgroundProperty,
            System.Windows.Media.Brushes.Transparent);

        ControlTemplate resizeThumbTemplate =
            new(
                typeof(
                    Thumb))
            {
                VisualTree =
                    resizeThumbBorder
            };

        Thumb resizeThumb =
            new()
            {
                Width =
                    12,
                Height =
                    12,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                Cursor =
                    System.Windows.Input.Cursors.SizeNWSE,
                Background =
                    System.Windows.Media.Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        0),
                Focusable =
                    false,
                Template =
                    resizeThumbTemplate
            };

        resizeThumb.DragDelta +=
            (_, e) =>
            {
                double currentWidth =
                    double.IsNaN(
                        image.Width) ||
                    image.Width <= 0
                        ? image.ActualWidth
                        : image.Width;

                double minimumWidth =
                    Math.Min(
                        32,
                        Math.Max(
                            1,
                            _settings.MaxImageWidth));

                double maximumWidth =
                    Math.Max(
                        minimumWidth,
                        _settings.MaxImageWidth);

                image.Width =
                    Math.Clamp(
                        currentWidth +
                        e.HorizontalChange,
                        minimumWidth,
                        maximumWidth);

                e.Handled =
                    true;
            };

        resizeThumb.DragCompleted +=
            imageResizeCompleted;

        imageContainer.Children.Add(
            resizeThumb);

        return
            new InlineUIContainer(
                imageContainer)
            {
                BaselineAlignment =
                    BaselineAlignment.Center
            };
    }

    private BlockUIContainer CreateCheckListBlock(
        CheckListBlockData data,
        RoutedEventHandler checklistChanged,
        TextChangedEventHandler checklistTextChanged)
    {
        StackPanel panel =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        8)
            };

        foreach (CheckListItemData item in
                 data.Items)
        {
            Grid row =
                new()
                {
                    Margin =
                        new Thickness(
                            0,
                            2,
                            0,
                            2)
                };

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            row.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            System.Windows.Controls.CheckBox checkBox =
                new()
                {
                    IsChecked =
                        item.IsChecked,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            8,
                            0),
                    Cursor =
                        System.Windows.Input.Cursors.Arrow
                };

            checkBox.Checked +=
                checklistChanged;

            checkBox.Unchecked +=
                checklistChanged;

            System.Windows.Controls.TextBox textBox =
                new()
                {
                    Text =
                        item.Text,
                    BorderThickness =
                        new Thickness(
                            0),
                    Background =
                        System.Windows.Media.Brushes.Transparent,
                    MinHeight =
                        26,
                    VerticalContentAlignment =
                        VerticalAlignment.Center,
                    Cursor =
                        System.Windows.Input.Cursors.IBeam,
                    CaretBrush =
                        System.Windows.Application.Current.TryFindResource(
                            "GlueNotesTextBrush") as System.Windows.Media.Brush ??
                        System.Windows.Media.Brushes.White
                };

            textBox.TextChanged +=
                checklistTextChanged;

            Grid.SetColumn(
                textBox,
                1);

            row.Children.Add(
                checkBox);

            row.Children.Add(
                textBox);

            panel.Children.Add(
                row);
        }

        return
            new BlockUIContainer(
                panel);
    }

    private NoteBlockData? ReadUiBlock(
        BlockUIContainer container)
    {
        if (container.Child is StackPanel panel)
        {
            CheckListBlockData checklist =
                new();

            foreach (UIElement child in
                     panel.Children)
            {
                if (child is not Grid row)
                {
                    continue;
                }

                System.Windows.Controls.CheckBox? checkBox =
                    row.Children
                        .OfType<System.Windows.Controls.CheckBox>()
                        .FirstOrDefault();

                System.Windows.Controls.TextBox? textBox =
                    row.Children
                        .OfType<System.Windows.Controls.TextBox>()
                        .FirstOrDefault();

                if (checkBox is null ||
                    textBox is null)
                {
                    continue;
                }

                checklist.Items.Add(
                    new CheckListItemData
                    {
                        IsChecked =
                            checkBox.IsChecked ==
                            true,
                        Text =
                            textBox.Text
                    });
            }

            return checklist;
        }

        return null;
    }

    private static string? BrushToHex(
        System.Windows.Media.Brush brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return null;
        }

        System.Windows.Media.Color color =
            solid.Color;

        return
            $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseBrush(
        string? value,
        out System.Windows.Media.Brush? brush)
    {
        brush =
            null;

        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        try
        {
            object converted =
                System.Windows.Media.ColorConverter.ConvertFromString(
                    value);

            if (converted is System.Windows.Media.Color color)
            {
                brush =
                    new SolidColorBrush(
                        color);

                brush.Freeze();

                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
