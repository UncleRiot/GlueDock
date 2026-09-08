using System.Windows;
using System.Windows.Controls;

namespace GlueDock;

/// <summary>
/// Reusable settings surface that can be embedded directly in the Settings Hub
/// and can still be presented as a modal window for legacy/fallback entry points.
/// </summary>
public class DockSettingsSectionControl : UserControl
{
    private Window? _standaloneWindow;
    private bool? _dialogResult;
    private bool _closedRaised;

    public string Title { get; set; } =
        string.Empty;

    public SizeToContent SizeToContent { get; set; } =
        System.Windows.SizeToContent.Manual;

    public ResizeMode ResizeMode { get; set; } =
        System.Windows.ResizeMode.CanResize;

    public bool ShowInTaskbar { get; set; } =
        true;

    public WindowStyle WindowStyle { get; set; } =
        System.Windows.WindowStyle.SingleBorderWindow;

    public bool AllowsTransparency { get; set; }

    public WindowStartupLocation WindowStartupLocation { get; set; } =
        System.Windows.WindowStartupLocation.Manual;

    public Window? Owner { get; set; }

    public bool? DialogResult
    {
        get =>
            _standaloneWindow?.DialogResult ??
            _dialogResult;
        set
        {
            _dialogResult =
                value;

            if (_standaloneWindow is not null)
            {
                _standaloneWindow.DialogResult =
                    value;
            }
        }
    }

    public Window? StandaloneWindow =>
        _standaloneWindow;

    public event Action<Window>? StandaloneWindowCreated;

    public event EventHandler? Closed;

    public bool? ShowDialog()
    {
        if (_standaloneWindow is not null)
        {
            throw new InvalidOperationException(
                "This settings section is already hosted in a standalone window.");
        }

        _closedRaised =
            false;

        Window window =
            new()
            {
                Title =
                    Title,
                SizeToContent =
                    SizeToContent,
                ResizeMode =
                    ResizeMode,
                ShowInTaskbar =
                    ShowInTaskbar,
                WindowStyle =
                    WindowStyle,
                AllowsTransparency =
                    AllowsTransparency,
                WindowStartupLocation =
                    WindowStartupLocation,
                Background =
                    Background,
                Owner =
                    Owner,
                Content =
                    this
            };

        if (!double.IsNaN(
                Width))
        {
            window.Width =
                Width;
        }

        if (!double.IsNaN(
                Height))
        {
            window.Height =
                Height;
        }

        window.MinWidth =
            MinWidth;
        window.MinHeight =
            MinHeight;
        window.MaxWidth =
            MaxWidth;
        window.MaxHeight =
            MaxHeight;

        _standaloneWindow =
            window;

        window.Closed +=
            StandaloneWindow_Closed;

        StandaloneWindowCreated?.Invoke(
            window);

        try
        {
            return window.ShowDialog();
        }
        finally
        {
            if (_standaloneWindow == window)
            {
                window.Content =
                    null;
                _standaloneWindow =
                    null;
            }
        }
    }

    public void Close()
    {
        if (_standaloneWindow is not null)
        {
            _standaloneWindow.Close();
            return;
        }

        RaiseClosed();
    }

    public void DragMove()
    {
        _standaloneWindow?.DragMove();
    }

    private void StandaloneWindow_Closed(
        object? sender,
        EventArgs e)
    {
        if (sender is Window window)
        {
            window.Closed -=
                StandaloneWindow_Closed;

            if (ReferenceEquals(
                    window.Content,
                    this))
            {
                window.Content =
                    null;
            }
        }

        _standaloneWindow =
            null;

        RaiseClosed();
    }

    private void RaiseClosed()
    {
        if (_closedRaised)
        {
            return;
        }

        _closedRaised =
            true;

        Closed?.Invoke(
            this,
            EventArgs.Empty);
    }
}
