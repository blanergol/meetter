using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Meetter.Persistence;

public static class AutoStartManager
{
    private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "Meetter";

    [SupportedOSPlatform("windows")]
    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key == null) return false;
            var value = key.GetValue(AppName) as string;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var exePath = Environment.ProcessPath ?? string.Empty;
            // значения могут быть в кавычках
            return Normalize(value).StartsWith(Normalize(exePath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static void SetAutoStart(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                           ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null) return;
            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exePath)) return;
                // Кавычки на случай пробелов в пути
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // проглатываем, чтобы UI не падал — настройка сохранится, пользователь сможет повторить
        }
    }

    private static string Normalize(string path) => path.Trim().Trim('"');
}


