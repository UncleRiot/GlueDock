using System.Windows;

namespace GlueDock;

public partial class SubmenuNameWindow : Window
{
    public string SubmenuName => NameTextBox.Text.Trim();

    public SubmenuNameWindow()
        : this(
            App.Language["Submenu.DefaultName"],
            App.Language["Submenu.CreateTitle"])
    {
    }

    public SubmenuNameWindow(
        string initialName,
        string title)
    {
        InitializeComponent();

        Title =
            title;

        NameLabel.Text =
            App.Language["Submenu.Name"];

        CancelButton.Content =
            App.Language["ColorPicker.Cancel"];

        OkButton.Content =
            App.Language["ColorPicker.Ok"];

        NameTextBox.Text =
            initialName;

        Loaded +=
            (_, _) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
