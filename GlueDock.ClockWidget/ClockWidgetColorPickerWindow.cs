using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace GlueDock.ClockWidget;

// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
internal static class ClockWidgetColorPickerWindow
{
    public static bool TryChooseColor(
        Color initialColor,
        out Color selectedColor)
    {
        using WinForms.ColorDialog dialog =
            new()
            {
                AnyColor = true,
                FullOpen = true,
                SolidColorOnly = false,
                Color =
                    Drawing.Color.FromArgb(
                        initialColor.A,
                        initialColor.R,
                        initialColor.G,
                        initialColor.B)
            };

        if (dialog.ShowDialog() !=
            WinForms.DialogResult.OK)
        {
            selectedColor =
                initialColor;

            return false;
        }

        selectedColor =
            Color.FromArgb(
                dialog.Color.A,
                dialog.Color.R,
                dialog.Color.G,
                dialog.Color.B);

        return true;
    }
}
