using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GlueDock;

public partial class App : Application
{
    public static LanguageService Language { get; } =
        new();

    protected override void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            StartApplication();
        }
        catch (Exception exception)
        {
            string debugDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GlueDock_Debug");

            string startupLogPath =
                Path.Combine(
                    debugDirectory,
                    "GlueDock_StartupError.log");

            try
            {
                Directory.CreateDirectory(
                    debugDirectory);

                File.WriteAllText(
                    startupLogPath,
                    exception.ToString());
            }
            catch
            {
            }

            MessageBox.Show(
                exception.ToString(),
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    private void StartApplication()
    {
        BackupRestoreResult restoreResult =
            BackupService.ApplyPendingRestore();

        if (!string.IsNullOrWhiteSpace(
                restoreResult.ErrorMessage))
        {
            MessageBox.Show(
                "The pending GlueDock backup could not be restored.\n\n" +
                restoreResult.ErrorMessage,
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        SettingsStore settingsStore =
            new();

        DockSettings settings =
            settingsStore.Load();

        Language.Load(
            settings.LanguageCode);

        string currentVersion =
            GitHubUpdateService.GetApplicationVersionText();

        bool showLicenseNotice =
            string.IsNullOrWhiteSpace(
                settings.LastLicenseNoticeVersion) ||
            GitHubUpdateService.IsApplicationVersionNewerThan(
                settings.LastLicenseNoticeVersion);

        if (showLicenseNotice)
        {
            System.Windows.ShutdownMode previousShutdownMode =
                ShutdownMode;

            ShutdownMode =
                System.Windows.ShutdownMode.OnExplicitShutdown;

            WelcomeWindow welcomeWindow =
                new(
                    currentVersion);

            bool acknowledged =
                welcomeWindow.ShowDialog() == true;

            if (!acknowledged)
            {
                Shutdown();
                return;
            }

            settings.LastLicenseNoticeVersion =
                currentVersion;

            try
            {
                settingsStore.Save(
                    settings);
            }
            catch
            {
            }

            ShutdownMode =
                previousShutdownMode;
        }

        MainWindow mainWindow =
            new();

        MainWindow = mainWindow;
        ShutdownMode =
            System.Windows.ShutdownMode.OnMainWindowClose;

        mainWindow.Show();

        if (restoreResult.Restored)
        {
            MessageBox.Show(
                "GlueDock backup restored successfully.",
                "GlueDock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
