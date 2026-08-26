using Microsoft.Win32;

namespace GlueDock;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GlueDock";

    public static void SetStartWithWindows(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            RunKeyPath,
            writable: true);

        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            string executablePath = Environment.ProcessPath ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                key.SetValue(
                    ValueName,
                    $"\"{executablePath}\"",
                    RegistryValueKind.String);
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
