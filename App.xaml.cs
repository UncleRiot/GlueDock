using System.Windows;
using Application = System.Windows.Application;

namespace GlueDock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow mainWindow = new();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
