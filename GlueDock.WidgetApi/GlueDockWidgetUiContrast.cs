using System.Windows.Media;

namespace GlueDock;

public static class GlueDockWidgetUiContrast
{
    public static Brush GetOpaqueSurfaceBrush(
        Brush surfaceBrush,
        Brush underlayBrush)
    {
        if (!TryGetRepresentativeColor(
                surfaceBrush,
                out Color surfaceColor) ||
            !TryGetRepresentativeColor(
                underlayBrush,
                out Color underlayColor))
        {
            return surfaceBrush;
        }

        Color effectiveColor =
            Composite(
                surfaceColor,
                underlayColor);

        return new SolidColorBrush(
            Color.FromRgb(
                effectiveColor.R,
                effectiveColor.G,
                effectiveColor.B));
    }

    public static Brush GetAdaptiveHoverBrush(
        Brush surfaceBrush,
        Brush underlayBrush)
    {
        Brush opaqueSurfaceBrush =
            GetOpaqueSurfaceBrush(
                surfaceBrush,
                underlayBrush);

        if (!TryGetRepresentativeColor(
                opaqueSurfaceBrush,
                out Color color))
        {
            return opaqueSurfaceBrush;
        }

        double luminance =
            GetRelativeLuminance(
                color);

        const double adjustment =
            0.08;

        byte Adjust(
            byte channel)
        {
            double adjusted =
                luminance < 0.5
                    ? channel +
                      ((255 - channel) *
                       adjustment)
                    : channel *
                      (1.0 -
                       adjustment);

            return
                (byte)Math.Clamp(
                    Math.Round(
                        adjusted),
                    0,
                    255);
        }

        return
            new SolidColorBrush(
                Color.FromRgb(
                    Adjust(
                        color.R),
                    Adjust(
                        color.G),
                    Adjust(
                        color.B)));
    }

    public static Brush GetContrastingTextBrush(
        Brush backgroundBrush,
        Brush underlayBrush,
        Brush fallbackBrush)
    {
        if (!TryGetRepresentativeColor(
                backgroundBrush,
                out Color backgroundColor) ||
            !TryGetRepresentativeColor(
                underlayBrush,
                out Color underlayColor))
        {
            return fallbackBrush;
        }

        Color effectiveColor =
            Composite(
                backgroundColor,
                underlayColor);

        double luminance =
            GetRelativeLuminance(
                effectiveColor);

        double blackContrast =
            (luminance + 0.05) /
            0.05;

        double whiteContrast =
            1.05 /
            (luminance + 0.05);

        return whiteContrast >=
               blackContrast
            ? Brushes.White
            : Brushes.Black;
    }

    private static bool TryGetRepresentativeColor(
        Brush brush,
        out Color color)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            color =
                solidColorBrush.Color;

            return true;
        }

        if (brush is GradientBrush gradientBrush &&
            gradientBrush.GradientStops.Count > 0)
        {
            double totalWeight = 0;
            double alpha = 0;
            double red = 0;
            double green = 0;
            double blue = 0;

            foreach (GradientStop gradientStop in
                     gradientBrush.GradientStops)
            {
                double weight =
                    1.0;

                totalWeight +=
                    weight;

                alpha +=
                    gradientStop.Color.A *
                    weight;

                red +=
                    gradientStop.Color.R *
                    weight;

                green +=
                    gradientStop.Color.G *
                    weight;

                blue +=
                    gradientStop.Color.B *
                    weight;
            }

            color =
                Color.FromArgb(
                    (byte)Math.Clamp(
                        Math.Round(
                            alpha /
                            totalWeight),
                        0,
                        255),
                    (byte)Math.Clamp(
                        Math.Round(
                            red /
                            totalWeight),
                        0,
                        255),
                    (byte)Math.Clamp(
                        Math.Round(
                            green /
                            totalWeight),
                        0,
                        255),
                    (byte)Math.Clamp(
                        Math.Round(
                            blue /
                            totalWeight),
                        0,
                        255));

            return true;
        }

        color =
            Colors.Transparent;

        return false;
    }

    private static Color Composite(
        Color foreground,
        Color background)
    {
        double foregroundAlpha =
            foreground.A /
            255.0;

        double backgroundAlpha =
            background.A /
            255.0;

        double outputAlpha =
            foregroundAlpha +
            backgroundAlpha *
            (1.0 -
             foregroundAlpha);

        if (outputAlpha <=
            0)
        {
            return Colors.Transparent;
        }

        byte red =
            CompositeChannel(
                foreground.R,
                foregroundAlpha,
                background.R,
                backgroundAlpha,
                outputAlpha);

        byte green =
            CompositeChannel(
                foreground.G,
                foregroundAlpha,
                background.G,
                backgroundAlpha,
                outputAlpha);

        byte blue =
            CompositeChannel(
                foreground.B,
                foregroundAlpha,
                background.B,
                backgroundAlpha,
                outputAlpha);

        return Color.FromArgb(
            (byte)Math.Clamp(
                Math.Round(
                    outputAlpha *
                    255.0),
                0,
                255),
            red,
            green,
            blue);
    }

    private static byte CompositeChannel(
        byte foreground,
        double foregroundAlpha,
        byte background,
        double backgroundAlpha,
        double outputAlpha)
    {
        double value =
            ((foreground *
              foregroundAlpha) +
             (background *
              backgroundAlpha *
              (1.0 -
               foregroundAlpha))) /
            outputAlpha;

        return (byte)Math.Clamp(
            Math.Round(
                value),
            0,
            255);
    }

    private static double GetRelativeLuminance(
        Color color)
    {
        return
            (0.2126 *
             ToLinear(
                 color.R)) +
            (0.7152 *
             ToLinear(
                 color.G)) +
            (0.0722 *
             ToLinear(
                 color.B));
    }

    private static double ToLinear(
        byte channel)
    {
        double value =
            channel /
            255.0;

        return value <=
               0.04045
            ? value /
              12.92
            : Math.Pow(
                (value +
                 0.055) /
                1.055,
                2.4);
    }
}
