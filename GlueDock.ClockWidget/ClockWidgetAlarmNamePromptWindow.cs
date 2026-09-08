using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlueDock;

namespace GlueDock.ClockWidget;

// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
public sealed class ClockWidgetAlarmNamePromptWindow : Window
{
    private readonly Func<string, string> _localize;
    private readonly Action<string> _accept;
    private readonly TextBlock _titleText;
    private readonly TextBlock _labelText;
    private readonly TextBox _nameTextBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public ClockWidgetAlarmNamePromptWindow(
        GlueDockWidgetAppearance hostAppearance,
        Func<string, string> localize,
        Action<string> accept)
    {
        _localize =
            localize;

        _accept =
            accept;

        Width = 330;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        Brush background =
            hostAppearance.DockBackgroundBrush.Clone();

        Brush itemBackground =
            hostAppearance.DockItemBackgroundBrush.Clone();

        Brush border =
            hostAppearance.DockItemBorderBrush.Clone();

        Brush text =
            hostAppearance.DockTextBrush.Clone();

        Border root =
            new()
            {
                Background =
                    background,
                BorderBrush =
                    border,
                BorderThickness =
                    new Thickness(
                        1),
                CornerRadius =
                    new CornerRadius(
                        Math.Max(
                            0,
                            hostAppearance.GlassCornerRadius)),
                Padding =
                    new Thickness(
                        14)
            };

        root.MouseLeftButtonDown +=
            Root_MouseLeftButtonDown;

        StackPanel content =
            new();

        _titleText =
            new TextBlock
            {
                Foreground =
                    text,
                FontSize = 15,
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        _labelText =
            new TextBlock
            {
                Foreground =
                    text,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        4)
            };

        _nameTextBox =
            new TextBox
            {
                Background =
                    itemBackground,
                Foreground =
                    GetContrastingTextBrush(
                        itemBackground,
                        background,
                        text),
                BorderBrush =
                    border,
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        6,
                        4,
                        6,
                        4)
            };

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0)
            };

        _okButton =
            CreateButton(
                itemBackground,
                background,
                border,
                text);

        _cancelButton =
            CreateButton(
                itemBackground,
                background,
                border,
                text);

        _cancelButton.Margin =
            new Thickness(
                6,
                0,
                0,
                0);

        _okButton.Click +=
            (_, _) =>
                Accept();

        _cancelButton.Click +=
            (_, _) =>
                Close();

        _nameTextBox.KeyDown +=
            (_, e) =>
            {
                if (e.Key ==
                    Key.Enter)
                {
                    Accept();
                    e.Handled = true;
                }
            };

        buttons.Children.Add(
            _okButton);

        buttons.Children.Add(
            _cancelButton);

        content.Children.Add(
            _titleText);

        content.Children.Add(
            _labelText);

        content.Children.Add(
            _nameTextBox);

        content.Children.Add(
            buttons);

        root.Child =
            content;

        Content =
            root;

        RefreshLanguage();

        Loaded +=
            (_, _) =>
            {
                _nameTextBox.Focus();
                Keyboard.Focus(
                    _nameTextBox);
            };
    }

    public void RefreshLanguage()
    {
        Title =
            _localize(
                "Widget.Clock.AlarmCompanion.NewAlarmTitle");

        _titleText.Text =
            Title;

        _labelText.Text =
            _localize(
                "Widget.Clock.AlarmCompanion.NameOptional");

        _okButton.Content =
            _localize(
                "ColorPicker.Ok");

        _cancelButton.Content =
            _localize(
                "ColorPicker.Cancel");
    }

    private void Accept()
    {
        _accept(
            _nameTextBox.Text.Trim());

        Close();
    }

    private void Root_MouseLeftButtonDown(
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
            if (current is Button ||
                current is TextBox)
            {
                return true;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return false;
    }

    private static Button CreateButton(
        Brush background,
        Brush underlay,
        Brush border,
        Brush fallbackText)
    {
        return new Button
        {
            Width = 86,
            Height = 30,
            Background =
                background,
            Foreground =
                GetContrastingTextBrush(
                    background,
                    underlay,
                    fallbackText),
            BorderBrush =
                border,
            BorderThickness =
                new Thickness(
                    1)
        };
    }

    private static Brush GetContrastingTextBrush(
        Brush backgroundBrush,
        Brush underlayBrush,
        Brush fallbackBrush)
    {
        return GlueDockWidgetUiContrast.GetContrastingTextBrush(
            backgroundBrush,
            underlayBrush,
            fallbackBrush);
    }
}
