using System.IO;
using Microsoft.Win32;

namespace TaskbarPlayer;

/// <summary>Автозапуск через HKCU\...\Run — без прав администратора и без задач в планировщике.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskbarPlayer";

    public static bool IsEnabled()
    {
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
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return;

            key.SetValue(ValueName, $"\"{exe}\"");
        }
        catch
        {
            // Политика может запрещать запись в реестр — молча пропускаем.
        }
    }
}
