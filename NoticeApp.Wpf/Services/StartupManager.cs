using Microsoft.Win32;

namespace NoticeApp.Wpf.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "NoticeApp";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key is null)
            {
                return false;
            }

            var expectedValue = BuildStartupValue();
            var existingValue = key.GetValue(StartupValueName)?.ToString();
            return string.Equals(existingValue, expectedValue, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetAutoStartEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(StartupValueName, BuildStartupValue());
            }
            else if (key.GetValue(StartupValueName) is not null)
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildStartupValue()
    {
        var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        return $"\"{exePath}\"";
    }
}
