using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace GlueDock;

// GlueDock UI rule:
// DockDialogThemeService is the single central styling service for all GlueDock settings windows and dialogs.
// This applies to core settings, widget settings, companion settings, calendar/override dialogs and future dialogs.
// Do not introduce local visual variants when DockDialogTheme / DockDialogThemeService can represent the required UI.
// Existing dialogs are migrated step by step; migration must not change behavior.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
public static class DockDialogThemeService
{
    public static DockDialogThemePalette ApplyWindowControlResources(
        Window window,
        GlueDockWidgetAppearance appearance)
    {
        return ApplyControlResources(
            window,
            appearance);
    }

    public static DockDialogThemePalette ApplyWindowControlResources(
        DockSettingsSectionControl control,
        GlueDockWidgetAppearance appearance)
    {
        return ApplyControlResources(
            control,
            appearance);
    }

    private static DockDialogThemePalette ApplyControlResources(
        Control control,
        GlueDockWidgetAppearance appearance)
    {
        Brush windowBackgroundBrush =
            appearance.DockBackgroundBrush.Clone();

        Brush controlBackgroundBrush =
            appearance.DockItemBackgroundBrush.Clone();

        Brush controlBorderBrush =
            appearance.DockItemBorderBrush.Clone();

        Brush textBrush =
            appearance.DockTextBrush.Clone();

        Brush controlTextBrush =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                controlBackgroundBrush,
                windowBackgroundBrush,
                textBrush);

        Brush popupBackgroundBrush =
            GlueDockWidgetUiContrast.GetOpaqueSurfaceBrush(
                controlBackgroundBrush,
                windowBackgroundBrush);

        Brush popupTextBrush =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                popupBackgroundBrush,
                popupBackgroundBrush,
                textBrush);

        Brush popupHighlightBrush =
            GlueDockWidgetUiContrast.GetOpaqueSurfaceBrush(
                controlBorderBrush,
                popupBackgroundBrush);

        Brush popupHighlightTextBrush =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                popupHighlightBrush,
                popupBackgroundBrush,
                popupTextBrush);

        Style comboBoxStyle =
            CreateThemedComboBoxStyle(
                controlBackgroundBrush,
                controlBorderBrush,
                controlTextBrush,
                popupBackgroundBrush);

        Style comboBoxItemStyle =
            CreateComboBoxItemStyle(
                popupBackgroundBrush,
                popupTextBrush,
                popupHighlightBrush,
                popupHighlightTextBrush);

        control.Foreground =
            textBrush;

        control.Resources[typeof(Button)] =
            CreateThemedButtonStyle(
                controlBackgroundBrush,
                windowBackgroundBrush,
                controlBorderBrush,
                textBrush);

        control.Resources[typeof(ComboBox)] =
            comboBoxStyle;

        control.Resources[typeof(ComboBoxItem)] =
            comboBoxItemStyle;

        control.Resources[typeof(TextBox)] =
            CreateThemedControlStyle(
                typeof(TextBox),
                controlBackgroundBrush,
                controlBorderBrush,
                controlTextBrush);

        control.Resources[typeof(CheckBox)] =
            CreateThemedCheckBoxStyle(
                textBrush,
                controlBackgroundBrush,
                controlBorderBrush);

        Style textBlockStyle =
            new(
                typeof(
                    TextBlock));

        textBlockStyle.Setters.Add(
            new Setter(
                TextBlock.ForegroundProperty,
                textBrush));

        control.Resources[typeof(TextBlock)] =
            textBlockStyle;

        control.Resources[SystemColors.WindowBrushKey] =
            controlBackgroundBrush;

        control.Resources[SystemColors.WindowTextBrushKey] =
            controlTextBrush;

        control.Resources[SystemColors.ControlBrushKey] =
            controlBackgroundBrush;

        control.Resources[SystemColors.ControlTextBrushKey] =
            controlTextBrush;

        control.Resources[SystemColors.HighlightBrushKey] =
            popupHighlightBrush;

        control.Resources[SystemColors.HighlightTextBrushKey] =
            popupHighlightTextBrush;

        return new DockDialogThemePalette(
            windowBackgroundBrush,
            controlBackgroundBrush,
            controlBorderBrush,
            textBrush,
            controlTextBrush,
            popupBackgroundBrush,
            popupTextBrush,
            popupHighlightBrush,
            popupHighlightTextBrush,
            comboBoxStyle,
            comboBoxItemStyle);
    }

    // GlueDock UI rule:
    // Dialog/settings opacity is the final effective surface opacity.
    // Theme brush alpha must never reduce the configured dialog opacity a second time.
    // 100% therefore always means a fully opaque dialog surface.
    public static Brush CreateDialogSurfaceBrush(
        Brush sourceBrush,
        double opacity)
    {
        double effectiveOpacity =
            Math.Clamp(
                opacity,
                0.0,
                1.0);

        if (sourceBrush is SolidColorBrush solidColorBrush)
        {
            Color color =
                solidColorBrush.Color;

            return new SolidColorBrush(
                Color.FromArgb(
                    255,
                    color.R,
                    color.G,
                    color.B))
            {
                Opacity =
                    effectiveOpacity
            };
        }

        if (sourceBrush is GradientBrush gradientBrush)
        {
            GradientBrush normalizedBrush =
                (GradientBrush)gradientBrush.Clone();

            foreach (GradientStop gradientStop in
                     normalizedBrush.GradientStops)
            {
                Color color =
                    gradientStop.Color;

                gradientStop.Color =
                    Color.FromArgb(
                        255,
                        color.R,
                        color.G,
                        color.B);
            }

            normalizedBrush.Opacity =
                effectiveOpacity;

            return normalizedBrush;
        }

        Brush fallbackBrush =
            sourceBrush.Clone();

        fallbackBrush.Opacity =
            effectiveOpacity;

        return fallbackBrush;
    }

    public static void ApplyScrollViewer(
        ScrollViewer scrollViewer,
        DockDialogThemePalette palette)
    {
        Style scrollBarStyle =
            new(
                typeof(ScrollBar));

        scrollBarStyle.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                Brushes.Transparent));

        scrollBarStyle.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                palette.ControlBorderBrush));

        scrollBarStyle.Setters.Add(
            new Setter(
                UIElement.OpacityProperty,
                0.50));

        Trigger verticalTrigger =
            new()
            {
                Property =
                    ScrollBar.OrientationProperty,
                Value =
                    Orientation.Vertical
            };

        verticalTrigger.Setters.Add(
            new Setter(
                FrameworkElement.WidthProperty,
                8.0));

        Trigger horizontalTrigger =
            new()
            {
                Property =
                    ScrollBar.OrientationProperty,
                Value =
                    Orientation.Horizontal
            };

        horizontalTrigger.Setters.Add(
            new Setter(
                FrameworkElement.HeightProperty,
                8.0));

        scrollBarStyle.Triggers.Add(
            verticalTrigger);

        scrollBarStyle.Triggers.Add(
            horizontalTrigger);

        Style thumbStyle =
            new(
                typeof(Thumb));

        thumbStyle.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                palette.ControlBorderBrush));

        thumbStyle.Setters.Add(
            new Setter(
                UIElement.OpacityProperty,
                0.70));

        Style repeatButtonStyle =
            new(
                typeof(RepeatButton));

        repeatButtonStyle.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                Brushes.Transparent));

        repeatButtonStyle.Setters.Add(
            new Setter(
                Control.BorderBrushProperty,
                Brushes.Transparent));

        repeatButtonStyle.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(
                    0)));

        repeatButtonStyle.Setters.Add(
            new Setter(
                UIElement.OpacityProperty,
                0.0));

        scrollViewer.Resources[typeof(ScrollBar)] =
            scrollBarStyle;

        scrollViewer.Resources[typeof(Thumb)] =
            thumbStyle;

        scrollViewer.Resources[typeof(RepeatButton)] =
            repeatButtonStyle;

        scrollViewer.Resources[SystemColors.ControlBrushKey] =
            Brushes.Transparent;

        scrollViewer.Resources[SystemColors.ControlLightBrushKey] =
            Brushes.Transparent;

        scrollViewer.Resources[SystemColors.ControlLightLightBrushKey] =
            Brushes.Transparent;

        scrollViewer.Resources[SystemColors.ControlDarkBrushKey] =
            palette.ControlBorderBrush;

        scrollViewer.Resources[SystemColors.ControlDarkDarkBrushKey] =
            palette.ControlBorderBrush;

        scrollViewer.Resources[SystemColors.ControlTextBrushKey] =
            palette.ControlBorderBrush;
    }

    public static void ApplyDialogWindowBorder(
        Border windowBorder,
        GlueDockWidgetAppearance appearance,
        bool preserveLayoutSpace)
    {
        if (appearance.DockBorderEnabled)
        {
            if (appearance.GlassSurfaceEnabled)
            {
                LinearGradientBrush dockBorderBrush =
                    new()
                    {
                        StartPoint =
                            new Point(
                                0.5,
                                0),
                        EndPoint =
                            new Point(
                                0.5,
                                1)
                    };

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0xC8,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x78,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0.55));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x28,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        1));

                windowBorder.BorderBrush =
                    dockBorderBrush;
            }
            else
            {
                windowBorder.BorderBrush =
                    new SolidColorBrush(
                        appearance.DockBorderColor);
            }

            windowBorder.BorderThickness =
                new Thickness(
                    1);

            return;
        }

        windowBorder.BorderBrush =
            Brushes.Transparent;

        windowBorder.BorderThickness =
            preserveLayoutSpace
                ? new Thickness(
                    1)
                : new Thickness(
                    0);
    }

    public static void ApplyDialogWindowMaterial(
        Border windowBorder,
        Border glassSurfaceBorder,
        Border glassHighlightBorder,
        DockDialogNativeBackdropHost nativeBackdropHost,
        GlueDockWidgetAppearance appearance,
        Brush windowBackgroundBrush,
        double opacity,
        double blurRadius)
    {
        double cornerRadius =
            Math.Clamp(
                appearance.GlassCornerRadius,
                0,
                80);

        CornerRadius effectiveCornerRadius =
            new(
                cornerRadius);

        windowBorder.CornerRadius =
            effectiveCornerRadius;

        glassSurfaceBorder.CornerRadius =
            effectiveCornerRadius;

        glassHighlightBorder.CornerRadius =
            effectiveCornerRadius;

        ApplyDialogWindowBorder(
            windowBorder,
            appearance,
            preserveLayoutSpace:
                false);

        double effectiveOpacity =
            Math.Clamp(
                opacity,
                0.10,
                1.00);

        if (!appearance.GlassSurfaceEnabled)
        {
            windowBorder.Background =
                CreateDialogSurfaceBrush(
                    windowBackgroundBrush,
                    effectiveOpacity);

            glassSurfaceBorder.Background =
                Brushes.Transparent;

            glassSurfaceBorder.Visibility =
                Visibility.Collapsed;

            glassHighlightBorder.Background =
                Brushes.Transparent;

            glassHighlightBorder.BorderBrush =
                Brushes.Transparent;

            glassHighlightBorder.BorderThickness =
                new Thickness(
                    0);

            glassHighlightBorder.Visibility =
                Visibility.Collapsed;

            nativeBackdropHost.SetBlur(
                Math.Clamp(
                    blurRadius,
                    0,
                    100));

            return;
        }

        windowBorder.Background =
            Brushes.Transparent;

        double gradientReferenceHeight =
            Math.Max(
                1,
                appearance.GlassGradientReferenceHeight);

        LinearGradientBrush surfaceBrush =
            new(
                Color.FromArgb(
                    255,
                    appearance.GlassTopColor.R,
                    appearance.GlassTopColor.G,
                    appearance.GlassTopColor.B),
                Color.FromArgb(
                    255,
                    appearance.GlassBottomColor.R,
                    appearance.GlassBottomColor.G,
                    appearance.GlassBottomColor.B),
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
                    effectiveOpacity
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
                appearance.GlassHighlightColor,
                0));

        highlightBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        LinearGradientBrush frameBrush =
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

        frameBrush.GradientStops.Add(
            new GradientStop(
                appearance.GlassHighlightColor,
                0));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    (byte)(appearance.GlassHighlightColor.A * 0.35),
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                1));

        glassSurfaceBorder.Background =
            surfaceBrush;

        glassSurfaceBorder.Visibility =
            Visibility.Visible;

        glassHighlightBorder.Background =
            highlightBrush;

        glassHighlightBorder.BorderBrush =
            frameBrush;

        glassHighlightBorder.BorderThickness =
            new Thickness(
                1);

        glassHighlightBorder.Visibility =
            Visibility.Visible;

        nativeBackdropHost.SetBlur(
            Math.Clamp(
                blurRadius,
                0,
                100));
    }

    public static void ApplyListBox(
        ListBox listBox,
        DockDialogThemePalette palette,
        bool transparentBackground)
    {
        listBox.Background =
            transparentBackground
                ? Brushes.Transparent
                : palette.ControlBackgroundBrush;

        listBox.Foreground =
            palette.TextBrush;

        Style itemStyle =
            new(
                typeof(
                    ListBoxItem));

        itemStyle.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                palette.TextBrush));

        itemStyle.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                Brushes.Transparent));

        itemStyle.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(
                    8,
                    5,
                    8,
                    5)));

        Trigger selectedTrigger =
            new()
            {
                Property =
                    ListBoxItem.IsSelectedProperty,
                Value =
                    true
            };

        selectedTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                palette.PopupHighlightBrush));

        selectedTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                palette.PopupHighlightTextBrush));

        itemStyle.Triggers.Add(
            selectedTrigger);

        Trigger hoverTrigger =
            new()
            {
                Property =
                    UIElement.IsMouseOverProperty,
                Value =
                    true
            };

        hoverTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                palette.ControlBackgroundBrush));

        hoverTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                palette.ControlTextBrush));

        itemStyle.Triggers.Add(
            hoverTrigger);

        listBox.ItemContainerStyle =
            itemStyle;
    }

    public static void ApplyComboBox(
        ComboBox comboBox,
        DockDialogThemePalette palette)
    {
        comboBox.Style =
            palette.ComboBoxStyle;

        comboBox.Background =
            palette.ControlBackgroundBrush;

        comboBox.Foreground =
            palette.ControlTextBrush;

        comboBox.BorderBrush =
            palette.ControlBorderBrush;

        comboBox.ItemContainerStyle =
            palette.ComboBoxItemStyle;
    }

    private static Style CreateThemedComboBoxStyle(
        Brush backgroundBrush,
        Brush borderBrush,
        Brush foregroundBrush,
        Brush popupBackgroundBrush)
    {
        Style style =
            new(
                typeof(ComboBox));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                backgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.BorderBrushProperty,
                borderBrush));

        style.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(
                    1)));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                foregroundBrush));

        style.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(
                    7,
                    3,
                    28,
                    3)));

        style.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                CreateComboBoxTemplate(
                    popupBackgroundBrush)));

        return style;
    }

    private static ControlTemplate CreateComboBoxTemplate(
        Brush popupBackgroundBrush)
    {
        FrameworkElementFactory root =
            new(
                typeof(Grid));

        FrameworkElementFactory surface =
            new(
                typeof(Border));

        surface.SetBinding(
            Border.BackgroundProperty,
            new Binding(
                nameof(Control.Background))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        surface.SetBinding(
            Border.BorderBrushProperty,
            new Binding(
                nameof(Control.BorderBrush))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        surface.SetBinding(
            Border.BorderThicknessProperty,
            new Binding(
                nameof(Control.BorderThickness))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        FrameworkElementFactory selection =
            new(
                typeof(ContentPresenter));

        selection.SetValue(
            FrameworkElement.MarginProperty,
            new Thickness(
                7,
                3,
                28,
                3));

        selection.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        selection.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Left);

        selection.SetBinding(
            ContentPresenter.ContentProperty,
            new Binding(
                nameof(ComboBox.SelectionBoxItem))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        selection.SetBinding(
            ContentPresenter.ContentTemplateProperty,
            new Binding(
                nameof(ComboBox.SelectionBoxItemTemplate))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        selection.SetBinding(
            TextElement.ForegroundProperty,
            new Binding(
                nameof(Control.Foreground))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        FrameworkElementFactory arrow =
            new(
                typeof(TextBlock));

        arrow.SetValue(
            TextBlock.TextProperty,
            "▼");

        arrow.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Right);

        arrow.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        arrow.SetValue(
            FrameworkElement.MarginProperty,
            new Thickness(
                0,
                0,
                8,
                0));

        arrow.SetBinding(
            TextElement.ForegroundProperty,
            new Binding(
                nameof(Control.Foreground))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        FrameworkElementFactory toggle =
            new(
                typeof(ToggleButton));

        toggle.SetValue(
            Control.BackgroundProperty,
            Brushes.Transparent);

        toggle.SetValue(
            Control.BorderBrushProperty,
            Brushes.Transparent);

        toggle.SetValue(
            Control.BorderThicknessProperty,
            new Thickness(
                0));

        toggle.SetValue(
            Control.TemplateProperty,
            CreateTransparentToggleButtonTemplate());

        toggle.SetValue(
            UIElement.FocusableProperty,
            false);

        toggle.SetBinding(
            ToggleButton.IsCheckedProperty,
            new Binding(
                nameof(ComboBox.IsDropDownOpen))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent),
                Mode =
                    BindingMode.TwoWay
            });

        FrameworkElementFactory popup =
            new(
                typeof(Popup));

        popup.Name =
            "PART_Popup";

        popup.SetValue(
            Popup.PlacementProperty,
            PlacementMode.Bottom);

        popup.SetValue(
            Popup.AllowsTransparencyProperty,
            true);

        popup.SetValue(
            UIElement.FocusableProperty,
            false);

        popup.SetBinding(
            Popup.IsOpenProperty,
            new Binding(
                nameof(ComboBox.IsDropDownOpen))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        FrameworkElementFactory popupBorder =
            new(
                typeof(Border));

        popupBorder.SetValue(
            Border.BackgroundProperty,
            popupBackgroundBrush);

        popupBorder.SetBinding(
            Border.BorderBrushProperty,
            new Binding(
                nameof(Control.BorderBrush))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        popupBorder.SetValue(
            Border.BorderThicknessProperty,
            new Thickness(
                1));

        popupBorder.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(
                DockDialogTheme.StandardCornerRadiusValue));

        popupBorder.SetBinding(
            FrameworkElement.MinWidthProperty,
            new Binding(
                nameof(FrameworkElement.ActualWidth))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        FrameworkElementFactory scrollViewer =
            new(
                typeof(ScrollViewer));

        scrollViewer.SetValue(
            ScrollViewer.VerticalScrollBarVisibilityProperty,
            ScrollBarVisibility.Auto);

        scrollViewer.SetValue(
            ScrollViewer.HorizontalScrollBarVisibilityProperty,
            ScrollBarVisibility.Disabled);

        FrameworkElementFactory itemsPresenter =
            new(
                typeof(ItemsPresenter));

        scrollViewer.AppendChild(
            itemsPresenter);

        popupBorder.AppendChild(
            scrollViewer);

        popup.AppendChild(
            popupBorder);

        root.AppendChild(
            surface);

        root.AppendChild(
            selection);

        root.AppendChild(
            arrow);

        root.AppendChild(
            toggle);

        root.AppendChild(
            popup);

        return new ControlTemplate(
            typeof(ComboBox))
        {
            VisualTree =
                root
        };
    }

    private static ControlTemplate CreateTransparentToggleButtonTemplate()
    {
        FrameworkElementFactory border =
            new(
                typeof(Border));

        border.SetValue(
            Border.BackgroundProperty,
            Brushes.Transparent);

        return new ControlTemplate(
            typeof(ToggleButton))
        {
            VisualTree =
                border
        };
    }

    private static Style CreateComboBoxItemStyle(
        Brush backgroundBrush,
        Brush foregroundBrush,
        Brush highlightBackgroundBrush,
        Brush highlightForegroundBrush)
    {
        Style style =
            new(
                typeof(ComboBoxItem));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                backgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                foregroundBrush));

        style.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(
                    6,
                    4,
                    6,
                    4)));

        Trigger highlightedTrigger =
            new()
            {
                Property =
                    ComboBoxItem.IsHighlightedProperty,
                Value =
                    true
            };

        highlightedTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                highlightBackgroundBrush));

        highlightedTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                highlightForegroundBrush));

        style.Triggers.Add(
            highlightedTrigger);

        Trigger selectedTrigger =
            new()
            {
                Property =
                    ComboBoxItem.IsSelectedProperty,
                Value =
                    true
            };

        selectedTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                highlightBackgroundBrush));

        selectedTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                highlightForegroundBrush));

        style.Triggers.Add(
            selectedTrigger);

        return style;
    }

    private static Style CreateThemedCheckBoxStyle(
        Brush foregroundBrush,
        Brush backgroundBrush,
        Brush borderBrush)
    {
        FrameworkElementFactory root =
            new(
                typeof(StackPanel));

        root.SetValue(
            StackPanel.OrientationProperty,
            Orientation.Horizontal);

        root.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        FrameworkElementFactory box =
            new(
                typeof(Border));

        box.Name =
            "CheckBoxBorder";

        box.SetValue(
            FrameworkElement.WidthProperty,
            15.0);

        box.SetValue(
            FrameworkElement.HeightProperty,
            15.0);

        box.SetValue(
            FrameworkElement.MarginProperty,
            new Thickness(
                0,
                0,
                6,
                0));

        box.SetValue(
            Border.BackgroundProperty,
            Brushes.White);

        box.SetValue(
            Border.BorderBrushProperty,
            borderBrush);

        box.SetValue(
            Border.BorderThicknessProperty,
            new Thickness(
                1));

        FrameworkElementFactory checkMark =
            new(
                typeof(TextBlock));

        checkMark.Name =
            "CheckMark";

        checkMark.SetValue(
            TextBlock.TextProperty,
            "✓");

        checkMark.SetValue(
            TextBlock.ForegroundProperty,
            Brushes.Black);

        checkMark.SetValue(
            TextBlock.FontSizeProperty,
            12.0);

        checkMark.SetValue(
            TextBlock.FontWeightProperty,
            FontWeights.Bold);

        checkMark.SetValue(
            TextBlock.TextAlignmentProperty,
            TextAlignment.Center);

        checkMark.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        checkMark.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        checkMark.SetValue(
            UIElement.VisibilityProperty,
            Visibility.Collapsed);

        box.AppendChild(
            checkMark);

        FrameworkElementFactory content =
            new(
                typeof(ContentPresenter));

        content.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        content.SetBinding(
            TextElement.ForegroundProperty,
            new Binding(
                nameof(Control.Foreground))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        root.AppendChild(
            box);

        root.AppendChild(
            content);

        ControlTemplate template =
            new(
                typeof(CheckBox))
            {
                VisualTree =
                    root
            };

        Trigger checkedTrigger =
            new()
            {
                Property =
                    ToggleButton.IsCheckedProperty,
                Value =
                    true
            };

        checkedTrigger.Setters.Add(
            new Setter(
                UIElement.VisibilityProperty,
                Visibility.Visible,
                "CheckMark"));

        template.Triggers.Add(
            checkedTrigger);

        Style style =
            new(
                typeof(CheckBox));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                foregroundBrush));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                backgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.BorderBrushProperty,
                borderBrush));

        style.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                template));

        return style;
    }

    private static Style CreateThemedControlStyle(
        Type targetType,
        Brush backgroundBrush,
        Brush borderBrush,
        Brush foregroundBrush)
    {
        Style style =
            new(
                targetType);

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                backgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.BorderBrushProperty,
                borderBrush));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                foregroundBrush));

        return style;
    }

    private static Style CreateThemedButtonStyle(
        Brush backgroundBrush,
        Brush underlayBrush,
        Brush borderBrush,
        Brush foregroundBrush)
    {
        Brush normalForeground =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                backgroundBrush,
                underlayBrush,
                foregroundBrush);

        Brush hoverBackground =
            foregroundBrush;

        Brush hoverForeground =
            GlueDockWidgetUiContrast.GetContrastingTextBrush(
                hoverBackground,
                backgroundBrush,
                backgroundBrush);

        Style style =
            CreateThemedControlStyle(
                typeof(Button),
                backgroundBrush,
                borderBrush,
                normalForeground);

        style.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(
                    1)));

        style.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                CreateButtonTemplate()));

        Trigger hoverTrigger =
            new()
            {
                Property =
                    UIElement.IsMouseOverProperty,
                Value =
                    true
            };

        hoverTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                hoverBackground));

        hoverTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                hoverForeground));

        style.Triggers.Add(
            hoverTrigger);

        Trigger pressedTrigger =
            new()
            {
                Property =
                    ButtonBase.IsPressedProperty,
                Value =
                    true
            };

        pressedTrigger.Setters.Add(
            new Setter(
                UIElement.OpacityProperty,
                0.78));

        style.Triggers.Add(
            pressedTrigger);

        return style;
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        FrameworkElementFactory border =
            new(
                typeof(Border));

        border.SetBinding(
            Border.BackgroundProperty,
            new Binding(
                nameof(Control.Background))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        border.SetBinding(
            Border.BorderBrushProperty,
            new Binding(
                nameof(Control.BorderBrush))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        border.SetBinding(
            Border.BorderThicknessProperty,
            new Binding(
                nameof(Control.BorderThickness))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(
                2));

        FrameworkElementFactory presenter =
            new(
                typeof(ContentPresenter));

        presenter.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        presenter.SetBinding(
            TextElement.ForegroundProperty,
            new Binding(
                nameof(Control.Foreground))
            {
                RelativeSource =
                    new RelativeSource(
                        RelativeSourceMode.TemplatedParent)
            });

        border.AppendChild(
            presenter);

        return new ControlTemplate(
            typeof(Button))
        {
            VisualTree =
                border
        };
    }
}

