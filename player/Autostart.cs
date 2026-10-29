using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace TrayTune;

/// <summary>
/// Автозапуск. Приложение требует прав администратора, а HKCU\Run в этом случае
/// либо показывает UAC при каждом входе, либо молча не срабатывает. Поэтому при
/// повышенных правах используем задачу в планировщике с highest privileges,
/// а реестр — только как запасной вариант.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrayTune";
    private const string TaskName = "TrayTune";

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEnabled()
    {
        if (TaskExists()) return true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return;

        if (!enabled)
        {
            Run("schtasks", $"/Delete /TN \"{TaskName}\" /F");
            RemoveRunValue();
            return;
        }

        if (IsElevated())
        {
            // Задача с высокими правами: UAC при входе не показывается.
            var result = Run("schtasks",
                $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F");

            if (result == 0)
            {
                RemoveRunValue();   // чтобы не запускалось дважды
                return;
            }
        }

        SetRunValue(exe);
    }

    private static void SetRunValue(string exe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key?.SetValue(ValueName, $"\"{exe}\"");
        }
        catch
        {
            // политика может запрещать запись
        }
    }

    private static void RemoveRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // не критично
        }
    }

    private static bool TaskExists()
    {
        try
        {
            return Run("schtasks", $"/Query /TN \"{TaskName}\"") == 0;
        }
        catch
        {
            return false;
        }
    }

    private static int Run(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null) return -1;

            process.WaitForExit(5000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch
        {
            return -1;
        }
    }
}
