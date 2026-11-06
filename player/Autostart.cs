using System.IO;
using Microsoft.Win32;

namespace TrayTune;

/// <summary>
/// Автозапуск через HKCU\...\Run. Приложение больше не требует прав
/// администратора, поэтому UAC при входе не появляется и задача в
/// Планировщике не нужна.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrayTune";

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
            // политика может запрещать запись в реестр — молча пропускаем
        }
    }
}
