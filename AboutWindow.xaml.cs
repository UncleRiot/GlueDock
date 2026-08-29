using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace GlueDock;

public partial class AboutWindow : Window
{
    private readonly Task<GitHubUpdateResult> _updateCheckTask;
    private string _releaseUrl =
        GitHubUpdateService.ReleasesUrl;

    public AboutWindow(
        Task<GitHubUpdateResult> updateCheckTask)
    {
        _updateCheckTask =
            updateCheckTask;

        InitializeComponent();

        VersionText.Text =
            $"Version: {GitHubUpdateService.GetApplicationVersionText()}";

        UpdateStatusText.Text =
            "Checking for updates...";

        LoadResourceImage(
            MolotovImageBrush,
            "molotov.jpg");

        LoadResourceImage(
            KoFiImage,
            "ko-fi.png");

        Loaded +=
            AboutWindow_Loaded;
    }

    private async void AboutWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        GitHubUpdateResult result =
            await _updateCheckTask;

        if (result.ErrorKind !=
            GitHubUpdateErrorKind.None)
        {
            UpdateStatusText.Text =
                "Update check unavailable.";

            OpenReleaseButton.Visibility =
                Visibility.Collapsed;

            return;
        }

        if (!result.UpdateAvailable)
        {
            UpdateStatusText.Text =
                "No new version available.";

            OpenReleaseButton.Visibility =
                Visibility.Collapsed;

            return;
        }

        _releaseUrl =
            string.IsNullOrWhiteSpace(
                result.DownloadUrl)
                ? GitHubUpdateService.ReleasesUrl
                : result.DownloadUrl;

        UpdateStatusText.Text =
            $"Update available: {result.LatestVersion}";

        OpenReleaseButton.Visibility =
            Visibility.Visible;
    }

    private static void LoadResourceImage(
        System.Windows.Media.ImageBrush imageBrush,
        string fileName)
    {
        string path =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                fileName);

        if (!File.Exists(path))
        {
            return;
        }

        BitmapImage bitmap =
            new();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.UriSource =
            new Uri(
                path,
                UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        imageBrush.ImageSource =
            bitmap;
    }

    private static void LoadResourceImage(
        System.Windows.Controls.Image image,
        string fileName)
    {
        string path =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                fileName);

        if (!File.Exists(path))
        {
            return;
        }

        BitmapImage bitmap =
            new();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.UriSource =
            new Uri(
                path,
                UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        image.Source =
            bitmap;
    }

    private void Hyperlink_RequestNavigate(
        object sender,
        RequestNavigateEventArgs e)
    {
        OpenUrl(
            e.Uri.AbsoluteUri);

        e.Handled = true;
    }

    private void OpenReleaseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenUrl(
            _releaseUrl);
    }

    private void KoFiImage_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenUrl(
            "https://ko-fi.com/uncleriot");
    }

    private static void OpenUrl(
        string url)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
