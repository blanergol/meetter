using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Meetter.Persistence;

public static class AutoStartManager
{
    private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "Meetter";
    // Must match StartupTask TaskId in MSIX Package.appxmanifest.
    private const string StartupTaskId = "MeetterStartupTask";
    private const int ErrorInsufficientBuffer = 122;

    [SupportedOSPlatform("windows")]
    public static bool IsEnabled()
        => IsEnabledAsync().GetAwaiter().GetResult();

    [SupportedOSPlatform("windows")]
    public static async Task<bool> IsEnabledAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (IsPackaged() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
        {
            return await IsStartupTaskEnabledAsync();
        }

        return IsRegistryAutoStartEnabled();
    }

    [SupportedOSPlatform("windows")]
    public static void SetAutoStart(bool enabled, bool quiet = true)
        => SetAutoStartAsync(enabled, quiet).GetAwaiter().GetResult();

    [SupportedOSPlatform("windows")]
    public static async Task SetAutoStartAsync(bool enabled, bool quiet = true)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (IsPackaged() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
        {
            await SetStartupTaskStateAsync(enabled);
            return;
        }

        SetRegistryAutoStart(enabled, quiet);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsPackaged()
    {
        var length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result == ErrorInsufficientBuffer && length > 0;
    }

    [SupportedOSPlatform("windows10.0.16299.0")]
    private static async Task<bool> IsStartupTaskEnabledAsync()
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            return IsEnabledState(startupTask.State);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows10.0.16299.0")]
    private static async Task SetStartupTaskStateAsync(bool enabled)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                if (IsEnabledState(startupTask.State))
                {
                    return;
                }

                _ = await startupTask.RequestEnableAsync();
                return;
            }

            startupTask.Disable();
        }
        catch
        {
            // Swallow exceptions to keep UI stable: settings still persist and user can retry.
        }
    }

    private static bool IsEnabledState(StartupTaskState state)
        => state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy;

    [SupportedOSPlatform("windows")]
    private static bool IsRegistryAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key == null) return false;
            var value = key.GetValue(AppName) as string;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var exePath = Environment.ProcessPath ?? string.Empty;
            // Values may be quoted.
            return Normalize(value).StartsWith(Normalize(exePath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetRegistryAutoStart(bool enabled, bool quiet = true)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exePath)) return;
                // Quotes in case the path contains spaces.
                var args = quiet ? " --autostart" : string.Empty;
                key.SetValue(AppName, $"\"{exePath}\"{args}");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Swallow exceptions to keep UI stable: settings still persist and user can retry.
        }
    }

    private static string Normalize(string path) => path.Trim().Trim('"');

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
