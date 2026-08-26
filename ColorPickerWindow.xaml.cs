using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;


namespace GlueDock;

public partial class ColorPickerWindow : Window
{
    private bool _isUpdating;
    private bool _isDraggingSaturationValue;
    private bool _isDraggingHue;

    private double _hue;
    private double _saturation;
    private double _value;

    public Color SelectedColor { get; private set; }

    public ColorPickerWindow(
        Color initialColor)
    {
        SelectedColor = initialColor;

        InitializeComponent();

        RgbToHsv(
            initialColor,
            out _hue,
            out _saturation,
            out _value);

        Loaded += ColorPickerWindow_Loaded;
    }

    private void ColorPickerWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        ApplyHsvToUi();
    }

    private void SaturationValueArea_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _isDraggingSaturationValue = true;
        SaturationValueArea.CaptureMouse();

        UpdateSaturationValue(
            e.GetPosition(
                SaturationValueArea));

        e.Handled = true;
    }

    private void SaturationValueArea_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_isDraggingSaturationValue ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSaturationValue(
            e.GetPosition(
                SaturationValueArea));

        e.Handled = true;
    }

    private void SaturationValueArea_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isDraggingSaturationValue)
        {
            return;
        }

        UpdateSaturationValue(
            e.GetPosition(
                SaturationValueArea));

        _isDraggingSaturationValue = false;
        SaturationValueArea.ReleaseMouseCapture();

        e.Handled = true;
    }

    private void HueArea_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _isDraggingHue = true;
        HueArea.CaptureMouse();

        UpdateHue(
            e.GetPosition(
                HueArea));

        e.Handled = true;
    }

    private void HueArea_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_isDraggingHue ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateHue(
            e.GetPosition(
                HueArea));

        e.Handled = true;
    }

    private void HueArea_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isDraggingHue)
        {
            return;
        }

        UpdateHue(
            e.GetPosition(
                HueArea));

        _isDraggingHue = false;
        HueArea.ReleaseMouseCapture();

        e.Handled = true;
    }

    private void UpdateSaturationValue(
        Point position)
    {
        double width =
            Math.Max(
                1,
                SaturationValueArea.ActualWidth);

        double height =
            Math.Max(
                1,
                SaturationValueArea.ActualHeight);

        _saturation =
            Math.Clamp(
                position.X / width,
                0,
                1);

        _value =
            1 -
            Math.Clamp(
                position.Y / height,
                0,
                1);

        ApplyHsvToUi();
    }

    private void UpdateHue(
        Point position)
    {
        double height =
            Math.Max(
                1,
                HueArea.ActualHeight);

        _hue =
            Math.Clamp(
                position.Y / height,
                0,
                1) * 360;

        if (_hue >= 360)
        {
            _hue = 0;
        }

        ApplyHsvToUi();
    }

    private void RgbTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdating ||
            RedTextBox is null ||
            GreenTextBox is null ||
            BlueTextBox is null)
        {
            return;
        }

        if (!byte.TryParse(
                RedTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out byte red) ||
            !byte.TryParse(
                GreenTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out byte green) ||
            !byte.TryParse(
                BlueTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out byte blue))
        {
            return;
        }

        SelectedColor =
            Color.FromRgb(
                red,
                green,
                blue);

        RgbToHsv(
            SelectedColor,
            out _hue,
            out _saturation,
            out _value);

        ApplyColorToUi();
    }

    private void HexTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdating ||
            HexTextBox is null)
        {
            return;
        }

        string hex =
            HexTextBox.Text.Trim();

        if (!hex.StartsWith('#'))
        {
            hex = "#" + hex;
        }

        if (hex.Length != 7)
        {
            return;
        }

        try
        {
            Color color =
                (Color)ColorConverter.ConvertFromString(
                    hex);

            SelectedColor = color;

            RgbToHsv(
                SelectedColor,
                out _hue,
                out _saturation,
                out _value);

            ApplyColorToUi();
        }
        catch
        {
        }
    }

    private void ApplyHsvToUi()
    {
        SelectedColor =
            HsvToRgb(
                _hue,
                _saturation,
                _value);

        ApplyColorToUi();
    }

    private void ApplyColorToUi()
    {
        _isUpdating = true;

        Color pureHue =
            HsvToRgb(
                _hue,
                1,
                1);

        HueSurface.Background =
            new SolidColorBrush(
                pureHue);

        ColorPreview.Background =
            new SolidColorBrush(
                SelectedColor);

        RedTextBox.Text =
            SelectedColor.R.ToString(
                CultureInfo.InvariantCulture);

        GreenTextBox.Text =
            SelectedColor.G.ToString(
                CultureInfo.InvariantCulture);

        BlueTextBox.Text =
            SelectedColor.B.ToString(
                CultureInfo.InvariantCulture);

        HexTextBox.Text =
            $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

        double surfaceWidth =
            Math.Max(
                1,
                SaturationValueArea.ActualWidth);

        double surfaceHeight =
            Math.Max(
                1,
                SaturationValueArea.ActualHeight);

        SaturationValueMarker.HorizontalAlignment =
            System.Windows.HorizontalAlignment.Left;

        SaturationValueMarker.VerticalAlignment =
            System.Windows.VerticalAlignment.Top;

        SaturationValueMarker.Margin =
            new Thickness(
                (_saturation * surfaceWidth) - 7,
                ((1 - _value) * surfaceHeight) - 7,
                0,
                0);

        double hueHeight =
            Math.Max(
                1,
                HueArea.ActualHeight);

        HueMarker.Margin =
            new Thickness(
                0,
                ((_hue / 360) * hueHeight) - 2,
                0,
                0);

        _isUpdating = false;
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static Color HsvToRgb(
        double hue,
        double saturation,
        double value)
    {
        double chroma =
            value * saturation;

        double hueSection =
            hue / 60.0;

        double x =
            chroma *
            (1 -
             Math.Abs(
                 (hueSection % 2) - 1));

        double red1;
        double green1;
        double blue1;

        if (hueSection < 1)
        {
            red1 = chroma;
            green1 = x;
            blue1 = 0;
        }
        else if (hueSection < 2)
        {
            red1 = x;
            green1 = chroma;
            blue1 = 0;
        }
        else if (hueSection < 3)
        {
            red1 = 0;
            green1 = chroma;
            blue1 = x;
        }
        else if (hueSection < 4)
        {
            red1 = 0;
            green1 = x;
            blue1 = chroma;
        }
        else if (hueSection < 5)
        {
            red1 = x;
            green1 = 0;
            blue1 = chroma;
        }
        else
        {
            red1 = chroma;
            green1 = 0;
            blue1 = x;
        }

        double match =
            value - chroma;

        return Color.FromRgb(
            (byte)Math.Round(
                (red1 + match) * 255),
            (byte)Math.Round(
                (green1 + match) * 255),
            (byte)Math.Round(
                (blue1 + match) * 255));
    }

    private static void RgbToHsv(
        Color color,
        out double hue,
        out double saturation,
        out double value)
    {
        double red =
            color.R / 255.0;

        double green =
            color.G / 255.0;

        double blue =
            color.B / 255.0;

        double max =
            Math.Max(
                red,
                Math.Max(
                    green,
                    blue));

        double min =
            Math.Min(
                red,
                Math.Min(
                    green,
                    blue));

        double delta =
            max - min;

        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == red)
        {
            hue =
                60 *
                (((green - blue) / delta) % 6);
        }
        else if (max == green)
        {
            hue =
                60 *
                (((blue - red) / delta) + 2);
        }
        else
        {
            hue =
                60 *
                (((red - green) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        saturation =
            max == 0
                ? 0
                : delta / max;

        value = max;
    }
}
