using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GlueDock;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow(
        string versionText)
    {
        InitializeComponent();

        ApplyLanguage(
            versionText);

        LoadResourceImage(
            MolotovImageBrush,
            "molotov.jpg");

        LoadResourceImage(
            KoFiImage,
            "ko-fi.png");
    }

    private void ApplyLanguage(
        string versionText)
    {
        Title =
            App.Language["Welcome.Title"];

        HeadingText.Text =
            App.Language["Welcome.Heading"];

        VersionText.Text =
            string.Format(
                App.Language["Welcome.Version"],
                versionText);

        LicenseInfoText.Text =
            App.Language["Welcome.LicenseInfo"];

        ViewLicenseButton.Content =
            App.Language["Welcome.ViewLicense"];

        AcknowledgeCheckBox.Content =
            App.Language["Welcome.Acknowledge"];

        SupportText.Text =
            App.Language["Welcome.Support"];

        ContinueButton.Content =
            App.Language["Welcome.Continue"];
    }

    private void ViewLicenseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string licensePath =
            Path.Combine(
                AppContext.BaseDirectory,
                "LICENSE.txt");

        if (!File.Exists(
                licensePath))
        {
            return;
        }

        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    licensePath,
                UseShellExecute =
                    true
            });
    }

    private void KoFiImage_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    "https://ko-fi.com/uncleriot",
                UseShellExecute =
                    true
            });
    }

    private void AcknowledgeCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        ContinueButton.IsEnabled =
            AcknowledgeCheckBox.IsChecked == true;
    }

    private void ContinueButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AcknowledgeCheckBox.IsChecked != true)
        {
            return;
        }

        DialogResult =
            true;
    }

    private static void LoadResourceImage(
        System.Windows.Media.ImageBrush imageBrush,
        string fileName)
    {
        System.Windows.Resources.StreamResourceInfo? resource =
            System.Windows.Application.GetResourceStream(
                new Uri(
                    $"Resources/{fileName}",
                    UriKind.Relative));

        if (resource is null)
        {
            return;
        }

        using Stream stream =
            resource.Stream;

        BitmapImage bitmap =
            new();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.StreamSource =
            stream;
        bitmap.EndInit();
        bitmap.Freeze();

        imageBrush.ImageSource =
            bitmap;
    }

    private static void LoadResourceImage(
        System.Windows.Controls.Image image,
        string fileName)
    {
        System.Windows.Resources.StreamResourceInfo? resource =
            System.Windows.Application.GetResourceStream(
                new Uri(
                    $"Resources/{fileName}",
                    UriKind.Relative));

        if (resource is null)
        {
            return;
        }

        using Stream stream =
            resource.Stream;

        BitmapImage bitmap =
            new();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.StreamSource =
            stream;
        bitmap.EndInit();
        bitmap.Freeze();

        image.Source =
            bitmap;
    }
}
