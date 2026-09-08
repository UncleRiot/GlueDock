using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;
using MediaOrientation = System.Windows.Controls.Orientation;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfPoint = System.Windows.Point;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace GlueDock;

public sealed class ThemePreviewControl : Grid
{
    private const double PreviewWidth = 330;
    private const double PreviewHeight = 104;

    private readonly Border _chrome;
    private readonly Border _glassSurface;
    private readonly Canvas _flameCanvas;
    private readonly Canvas _starCanvas;
    private readonly Border _glassHighlight;
    private readonly StackPanel _sampleItems;
    private readonly TextBlock _themeNameText;

    public ThemePreviewControl()
    {
        Width =
            PreviewWidth;

        Height =
            PreviewHeight;

        IsHitTestVisible =
            false;

        SnapsToDevicePixels =
            true;

        UseLayoutRounding =
            true;

        Effect =
            new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.45
            };

        _chrome =
            new Border
            {
                CornerRadius =
                    new CornerRadius(
                        18),
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        12),
                ClipToBounds =
                    true
            };

        Children.Add(
            _chrome);

        Grid layerGrid =
            new();

        _chrome.Child =
            layerGrid;

        _glassSurface =
            new Border
            {
                IsHitTestVisible =
                    false
            };

        layerGrid.Children.Add(
            _glassSurface);

        _flameCanvas =
            new Canvas
            {
                IsHitTestVisible =
                    false,
                ClipToBounds =
                    true
            };

        layerGrid.Children.Add(
            _flameCanvas);

        _starCanvas =
            new Canvas
            {
                IsHitTestVisible =
                    false,
                ClipToBounds =
                    true
            };

        layerGrid.Children.Add(
            _starCanvas);

        _glassHighlight =
            new Border
            {
                IsHitTestVisible =
                    false
            };

        layerGrid.Children.Add(
            _glassHighlight);

        Grid contentGrid =
            new();

        contentGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        contentGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        layerGrid.Children.Add(
            contentGrid);

        _themeNameText =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        6,
                        0,
                        6,
                        5),
                FontSize =
                    11,
                FontWeight =
                    FontWeights.SemiBold,
                Opacity =
                    0.88
            };

        contentGrid.Children.Add(
            _themeNameText);

        _sampleItems =
            new StackPanel
            {
                Orientation =
                    MediaOrientation.Horizontal,
                HorizontalAlignment =
                    WpfHorizontalAlignment.Center,
                VerticalAlignment =
                    WpfVerticalAlignment.Center
            };

        Grid.SetRow(
            _sampleItems,
            1);

        contentGrid.Children.Add(
            _sampleItems);
    }

    public void ShowTheme(
        string themeId,
        string displayName)
    {
        ClearPreview();

        DockTheme theme =
            DockThemeService.Load(
                themeId);

        MediaColor dockBackground =
            ParseColor(
                theme.DockBackgroundColor,
                MediaColor.FromRgb(
                    0x12,
                    0x16,
                    0x1C));

        MediaColor itemBackground =
            ParseColor(
                theme.ItemBackgroundColor,
                MediaColor.FromArgb(
                    0x22,
                    0xFF,
                    0xFF,
                    0xFF));

        MediaColor itemBorder =
            ParseColor(
                theme.ItemBorderColor,
                MediaColor.FromArgb(
                    0x22,
                    0xFF,
                    0xFF,
                    0xFF));

        MediaColor textColor =
            ParseColor(
                theme.TextColor,
                System.Windows.Media.Colors.White);

        _chrome.Background =
            new SolidColorBrush(
                dockBackground);

        _chrome.BorderBrush =
            new SolidColorBrush(
                MediaColor.FromArgb(
                    0x88,
                    itemBorder.R,
                    itemBorder.G,
                    itemBorder.B));

        _themeNameText.Text =
            displayName;

        _themeNameText.Foreground =
            new SolidColorBrush(
                textColor);

        double cornerRadius =
            Math.Clamp(
                theme.GlassCornerRadius,
                0,
                80);

        _chrome.CornerRadius =
            new CornerRadius(
                Math.Min(
                    20,
                    cornerRadius <= 0
                        ? 18
                        : cornerRadius));

        BuildGlassSurface(
            theme);

        BuildStars(
            theme);

        BuildFlames(
            theme);

        BuildSampleItems(
            itemBackground,
            itemBorder,
            textColor);
    }

    public void ClearPreview()
    {
        _glassSurface.BeginAnimation(
            OpacityProperty,
            null);

        if (_glassSurface.Background is LinearGradientBrush gradientBrush)
        {
            gradientBrush.BeginAnimation(
                LinearGradientBrush.StartPointProperty,
                null);

            gradientBrush.BeginAnimation(
                LinearGradientBrush.EndPointProperty,
                null);
        }

        _flameCanvas.Children.Clear();
        _starCanvas.Children.Clear();
        _sampleItems.Children.Clear();

        _glassSurface.Background =
            MediaBrushes.Transparent;

        _glassHighlight.Background =
            MediaBrushes.Transparent;

        _glassHighlight.BorderBrush =
            MediaBrushes.Transparent;

        _glassHighlight.BorderThickness =
            new Thickness(
                0);
    }

    private void BuildGlassSurface(
        DockTheme theme)
    {
        if (!theme.GlassSurfaceEnabled)
        {
            return;
        }

        MediaColor topColor =
            ParseColor(
                theme.GlassTopColor,
                MediaColor.FromArgb(
                    0xD0,
                    0xE8,
                    0xF2,
                    0xEC));

        MediaColor bottomColor =
            ParseColor(
                theme.GlassBottomColor,
                MediaColor.FromArgb(
                    0x80,
                    0xA8,
                    0xBE,
                    0xB4));

        LinearGradientBrush surfaceBrush =
            new(
                topColor,
                bottomColor,
                new WpfPoint(
                    theme.GlassGradientStartX,
                    theme.GlassGradientStartY),
                new WpfPoint(
                    theme.GlassGradientEndX,
                    theme.GlassGradientEndY));

        _glassSurface.Background =
            surfaceBrush;

        if (theme.GlassGradientAnimationEnabled)
        {
            Duration duration =
                new(
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            theme.GlassGradientAnimationDurationSeconds,
                            0.1,
                            120)));

            if (string.Equals(
                    theme.GlassGradientAnimationMode,
                    "RotateTransform",
                    StringComparison.OrdinalIgnoreCase))
            {
                RotateTransform rotateTransform =
                    new(
                        0,
                        PreviewWidth / 2,
                        PreviewHeight / 2);

                surfaceBrush.Transform =
                    rotateTransform;

                DoubleAnimation rotationAnimation =
                    new(
                        0,
                        theme.GlassGradientAnimationRotationDegrees,
                        duration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                rotateTransform.BeginAnimation(
                    RotateTransform.AngleProperty,
                    rotationAnimation);
            }
            else
            {
                PointAnimation startAnimation =
                    new(
                        surfaceBrush.StartPoint,
                        new WpfPoint(
                            theme.GlassGradientAnimationToStartX,
                            theme.GlassGradientAnimationToStartY),
                        duration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                PointAnimation endAnimation =
                    new(
                        surfaceBrush.EndPoint,
                        new WpfPoint(
                            theme.GlassGradientAnimationToEndX,
                            theme.GlassGradientAnimationToEndY),
                        duration)
                    {
                        AutoReverse =
                            theme.GlassGradientAnimationAutoReverse,
                        RepeatBehavior =
                            RepeatBehavior.Forever
                    };

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.StartPointProperty,
                    startAnimation);

                surfaceBrush.BeginAnimation(
                    LinearGradientBrush.EndPointProperty,
                    endAnimation);
            }
        }

        MediaColor highlightColor =
            ParseColor(
                theme.GlassHighlightColor,
                MediaColor.FromArgb(
                    0x90,
                    0xFF,
                    0xFF,
                    0xFF));

        LinearGradientBrush highlightBrush =
            new()
            {
                StartPoint =
                    new WpfPoint(
                        0.5,
                        0),
                EndPoint =
                    new WpfPoint(
                        0.5,
                        1)
            };

        highlightBrush.GradientStops.Add(
            new GradientStop(
                highlightColor,
                0));

        highlightBrush.GradientStops.Add(
            new GradientStop(
                MediaColor.FromArgb(
                    0,
                    highlightColor.R,
                    highlightColor.G,
                    highlightColor.B),
                0.58));

        _glassHighlight.Background =
            highlightBrush;

        _glassHighlight.BorderBrush =
            new SolidColorBrush(
                MediaColor.FromArgb(
                    (byte)Math.Clamp(
                        (int)highlightColor.A,
                        40,
                        180),
                    highlightColor.R,
                    highlightColor.G,
                    highlightColor.B));

        _glassHighlight.BorderThickness =
            new Thickness(
                1);

        _glassHighlight.CornerRadius =
            _chrome.CornerRadius;
    }

    private void BuildStars(
        DockTheme theme)
    {
        if (!theme.GlassStarEffectEnabled ||
            theme.GlassStarLayers.Count == 0)
        {
            return;
        }

        double surfaceWidth =
            PreviewWidth - 24;

        double surfaceHeight =
            PreviewHeight - 24;

        foreach (DockThemeStarLayer star in
                 theme.GlassStarLayers)
        {
            double size =
                Math.Clamp(
                    star.Size,
                    0.5,
                    12);

            MediaColor starColor =
                ParseColor(
                    star.Color,
                    System.Windows.Media.Colors.White);

            RadialGradientBrush brush =
                new()
                {
                    Center =
                        new WpfPoint(
                            0.5,
                            0.5),
                    GradientOrigin =
                        new WpfPoint(
                            0.5,
                            0.5)
                };

            brush.GradientStops.Add(
                new GradientStop(
                    starColor,
                    0));

            brush.GradientStops.Add(
                new GradientStop(
                    MediaColor.FromArgb(
                        0,
                        starColor.R,
                        starColor.G,
                        starColor.B),
                    1));

            Ellipse ellipse =
                new()
                {
                    Width =
                        size,
                    Height =
                        size,
                    Fill =
                        brush,
                    Opacity =
                        Math.Clamp(
                            star.MinimumOpacity,
                            0,
                            1),
                    RenderTransformOrigin =
                        new WpfPoint(
                            0.5,
                            0.5)
                };

            if (star.GlowRadius > 0)
            {
                ellipse.Effect =
                    new BlurEffect
                    {
                        Radius =
                            Math.Clamp(
                                star.GlowRadius,
                                0,
                                16)
                    };
            }

            ScaleTransform scale =
                new(
                    Math.Clamp(
                        star.MinimumScale,
                        0.2,
                        2.5),
                    Math.Clamp(
                        star.MinimumScale,
                        0.2,
                        2.5));

            ellipse.RenderTransform =
                scale;

            Canvas.SetLeft(
                ellipse,
                (surfaceWidth *
                 Math.Clamp(
                     star.X,
                     0,
                     1)) -
                (size / 2));

            Canvas.SetTop(
                ellipse,
                (surfaceHeight *
                 Math.Clamp(
                     star.Y,
                     0,
                     1)) -
                (size / 2));

            Duration duration =
                new(
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            star.DurationSeconds,
                            0.6,
                            30)));

            TimeSpan beginTime =
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        star.DelaySeconds,
                        0,
                        30));

            DoubleAnimation opacityAnimation =
                new(
                    Math.Clamp(
                        star.MinimumOpacity,
                        0,
                        1),
                    Math.Clamp(
                        star.MaximumOpacity,
                        0,
                        1),
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                };

            ellipse.BeginAnimation(
                OpacityProperty,
                opacityAnimation);

            DoubleAnimation scaleAnimation =
                new(
                    Math.Clamp(
                        star.MinimumScale,
                        0.2,
                        2.5),
                    Math.Clamp(
                        star.MaximumScale,
                        0.2,
                        3),
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                };

            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                scaleAnimation);

            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                scaleAnimation);

            _starCanvas.Children.Add(
                ellipse);
        }
    }

    private void BuildFlames(
        DockTheme theme)
    {
        if (!theme.GlassFlameEffectEnabled ||
            theme.GlassFlameLayers.Count == 0)
        {
            return;
        }

        double surfaceWidth =
            PreviewWidth - 24;

        double surfaceHeight =
            PreviewHeight - 24;

        int maximumPreviewFlames =
            Math.Min(
                theme.GlassFlameLayers.Count,
                96);

        foreach (DockThemeFlameLayer layer in
                 theme.GlassFlameLayers.Take(
                     maximumPreviewFlames))
        {
            double width =
                Math.Max(
                    4,
                    surfaceWidth *
                    Math.Clamp(
                        layer.WidthRatio,
                        0.004,
                        0.30));

            double height =
                Math.Max(
                    10,
                    surfaceHeight *
                    Math.Clamp(
                        layer.HeightRatio,
                        0.08,
                        1.40));

            MediaColor tipColor =
                ParseColor(
                    layer.TipColor,
                    MediaColor.FromArgb(
                        0,
                        0xFF,
                        0x3A,
                        0));

            MediaColor midColor =
                ParseColor(
                    layer.MidColor,
                    MediaColor.FromArgb(
                        0xDF,
                        0xFF,
                        0x65,
                        0));

            MediaColor coreColor =
                ParseColor(
                    layer.CoreColor,
                    MediaColor.FromArgb(
                        0xFF,
                        0xFF,
                        0xD3,
                        0x5A));

            LinearGradientBrush flameBrush =
                new()
                {
                    StartPoint =
                        new WpfPoint(
                            0.5,
                            0),
                    EndPoint =
                        new WpfPoint(
                            0.5,
                            1)
                };

            flameBrush.GradientStops.Add(
                new GradientStop(
                    tipColor,
                    Math.Clamp(
                        layer.TipStop,
                        0,
                        1)));

            flameBrush.GradientStops.Add(
                new GradientStop(
                    midColor,
                    Math.Clamp(
                        layer.MidStop,
                        0,
                        1)));

            flameBrush.GradientStops.Add(
                new GradientStop(
                    coreColor,
                    Math.Clamp(
                        layer.CoreStop,
                        0,
                        1)));

            Ellipse flame =
                new()
                {
                    Width =
                        width,
                    Height =
                        height,
                    Fill =
                        flameBrush,
                    Opacity =
                        Math.Clamp(
                            layer.Opacity,
                            0,
                            1),
                    RenderTransformOrigin =
                        new WpfPoint(
                            0.5,
                            1)
                };

            if (layer.BlurRadius > 0)
            {
                flame.Effect =
                    new BlurEffect
                    {
                        Radius =
                            Math.Clamp(
                                layer.BlurRadius,
                                0,
                                12)
                    };
            }

            TranslateTransform translate =
                new();

            ScaleTransform scale =
                new(
                    Math.Clamp(
                        layer.MinimumScaleX,
                        0.2,
                        2),
                    Math.Clamp(
                        layer.MinimumScaleY,
                        0.2,
                        2));

            RotateTransform rotate =
                new();

            TransformGroup transforms =
                new();

            transforms.Children.Add(
                scale);

            transforms.Children.Add(
                rotate);

            transforms.Children.Add(
                translate);

            flame.RenderTransform =
                transforms;

            Canvas.SetLeft(
                flame,
                (surfaceWidth *
                 Math.Clamp(
                     layer.X,
                     0,
                     1)) -
                (width / 2));

            Canvas.SetTop(
                flame,
                (surfaceHeight *
                 Math.Clamp(
                     layer.BaseYRatio,
                     0,
                     1.2)) -
                height);

            Duration duration =
                new(
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            layer.DurationSeconds,
                            0.5,
                            20)));

            TimeSpan beginTime =
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        layer.DelaySeconds,
                        0,
                        20));

            double rise =
                surfaceHeight *
                Math.Clamp(
                    layer.RiseRatio,
                    0,
                    1.2);

            DoubleAnimation riseAnimation =
                new(
                    0,
                    -rise,
                    duration)
                {
                    BeginTime =
                        beginTime,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseOut
                        }
                };

            translate.BeginAnimation(
                TranslateTransform.YProperty,
                riseAnimation);

            double drift =
                surfaceWidth *
                Math.Clamp(
                    layer.DriftRatio,
                    -0.20,
                    0.20);

            DoubleAnimation driftAnimation =
                new(
                    -drift,
                    drift,
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                };

            translate.BeginAnimation(
                TranslateTransform.XProperty,
                driftAnimation);

            DoubleAnimation scaleXAnimation =
                new(
                    Math.Clamp(
                        layer.MinimumScaleX,
                        0.2,
                        2),
                    Math.Clamp(
                        layer.MaximumScaleX,
                        0.2,
                        2.5),
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever
                };

            DoubleAnimation scaleYAnimation =
                new(
                    Math.Clamp(
                        layer.MinimumScaleY,
                        0.2,
                        2),
                    Math.Clamp(
                        layer.MaximumScaleY,
                        0.2,
                        2.5),
                    duration)
                {
                    BeginTime =
                        beginTime,
                    AutoReverse =
                        true,
                    RepeatBehavior =
                        RepeatBehavior.Forever
                };

            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                scaleXAnimation);

            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                scaleYAnimation);

            if (layer.SwayDegrees > 0)
            {
                DoubleAnimation swayAnimation =
                    new(
                        -Math.Clamp(
                            layer.SwayDegrees,
                            0,
                            35),
                        Math.Clamp(
                            layer.SwayDegrees,
                            0,
                            35),
                        duration)
                    {
                        BeginTime =
                            beginTime,
                        AutoReverse =
                            true,
                        RepeatBehavior =
                            RepeatBehavior.Forever,
                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };

                rotate.BeginAnimation(
                    RotateTransform.AngleProperty,
                    swayAnimation);
            }

            _flameCanvas.Children.Add(
                flame);
        }
    }

    private void BuildSampleItems(
        MediaColor itemBackground,
        MediaColor itemBorder,
        MediaColor textColor)
    {
        string[] glyphs =
        [
            "\uE7C3",
            "\uE8B7",
            "\uE8A5",
            "\uE713"
        ];

        foreach (string glyph in glyphs)
        {
            Border item =
                new()
                {
                    Width =
                        54,
                    Height =
                        48,
                    Margin =
                        new Thickness(
                            4,
                            0,
                            4,
                            0),
                    CornerRadius =
                        new CornerRadius(
                            12),
                    Background =
                        new SolidColorBrush(
                            itemBackground),
                    BorderBrush =
                        new SolidColorBrush(
                            itemBorder),
                    BorderThickness =
                        new Thickness(
                            1)
                };

            TextBlock icon =
                new()
                {
                    Text =
                        glyph,
                    FontFamily =
                        new MediaFontFamily(
                            "Segoe MDL2 Assets"),
                    FontSize =
                        21,
                    Foreground =
                        new SolidColorBrush(
                            textColor),
                    HorizontalAlignment =
                        WpfHorizontalAlignment.Center,
                    VerticalAlignment =
                        WpfVerticalAlignment.Center
                };

            item.Child =
                icon;

            _sampleItems.Children.Add(
                item);
        }
    }

    private static MediaColor ParseColor(
        string? value,
        MediaColor fallback)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return fallback;
        }

        try
        {
            object? converted =
                MediaColorConverter.ConvertFromString(
                    value);

            return converted is MediaColor color
                ? color
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
