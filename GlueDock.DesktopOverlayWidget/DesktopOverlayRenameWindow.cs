using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlueDock;

namespace GlueDock.DesktopOverlayWidget;

public sealed class DesktopOverlayRenameWindow : Window
{
    private readonly TextBox _nameTextBox;
    private readonly DockDialogNativeBackdropHost _nativeBackdropHost;

    public DesktopOverlayRenameWindow(
        string currentName,
        GlueDockWidgetAppearance appearance,
        Func<string, string> localize)
    {
        Title =
            localize(
                "Widget.DesktopOverlay.Rename.Title");

        WindowStyle =
            WindowStyle.None;

        AllowsTransparency =
            true;

        Background =
            Brushes.Transparent;

        ResizeMode =
            ResizeMode.NoResize;

        SizeToContent =
            System.Windows.SizeToContent.WidthAndHeight;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ShowInTaskbar =
            false;

        Border windowBorder =
            new();

        Border glassSurfaceBorder =
            new()
            {
                IsHitTestVisible =
                    false
            };

        Border glassHighlightBorder =
            new()
            {
                IsHitTestVisible =
                    false
            };

        TextBlock title =
            new()
            {
                Text =
                    localize(
                        "Widget.DesktopOverlay.Rename.Title"),
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        _nameTextBox =
            new TextBox
            {
                Text =
                    currentName,
                MinWidth =
                    300,
                MinHeight =
                    DockDialogTheme.StandardControlHeight,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        12)
            };

        Button okButton =
            new()
            {
                Content =
                    localize(
                        "ColorPicker.Ok"),
                MinWidth =
                    80,
                MinHeight =
                    DockDialogTheme.StandardControlHeight,
                Margin =
                    new Thickness(
                        0,
                        0,
                        8,
                        0)
            };

        Button cancelButton =
            new()
            {
                Content =
                    localize(
                        "ColorPicker.Cancel"),
                MinWidth =
                    80,
                MinHeight =
                    DockDialogTheme.StandardControlHeight
            };

        okButton.Click +=
            (_, _) =>
            {
                ResultName =
                    _nameTextBox.Text;

                DialogResult =
                    true;
            };

        cancelButton.Click +=
            (_, _) =>
                DialogResult =
                    false;

        _nameTextBox.KeyDown +=
            (_, e) =>
            {
                if (e.Key ==
                    Key.Enter)
                {
                    ResultName =
                        _nameTextBox.Text;

                    DialogResult =
                        true;

                    e.Handled =
                        true;
                }
            };

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        buttons.Children.Add(
            okButton);

        buttons.Children.Add(
            cancelButton);

        StackPanel content =
            new()
            {
                Margin =
                    new Thickness(
                        16)
            };

        content.Children.Add(
            title);

        content.Children.Add(
            _nameTextBox);

        content.Children.Add(
            buttons);

        Grid chrome =
            new();

        chrome.Children.Add(
            glassSurfaceBorder);

        chrome.Children.Add(
            glassHighlightBorder);

        chrome.Children.Add(
            content);

        windowBorder.Child =
            chrome;

        Content =
            windowBorder;

        _nativeBackdropHost =
            new DockDialogNativeBackdropHost(
                this);

        DockDialogThemeService.ApplyWindowControlResources(
            this,
            appearance);

        DockDialogThemeService.ApplyDialogWindowMaterial(
            windowBorder,
            glassSurfaceBorder,
            glassHighlightBorder,
            _nativeBackdropHost,
            appearance,
            appearance.DockBackgroundBrush,
            appearance.Opacity,
            appearance.BlurRadius);

        Loaded +=
            (_, _) =>
            {
                _nameTextBox.Focus();
                _nameTextBox.SelectAll();
            };

        Closed +=
            (_, _) =>
                _nativeBackdropHost.Dispose();
    }

    public string ResultName { get; private set; } =
        string.Empty;
}
