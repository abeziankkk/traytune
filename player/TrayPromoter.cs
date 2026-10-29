using System.IO;
using Microsoft.Win32;

namespace TrayTune;

/// <summary>
/// Windows 11 прячет значки новых программ в переполнение, пока пользователь не
/// включит их вручную. Начиная с 22H2 это состояние лежит в реестре
/// (Control Panel\NotifyIconSettings\*), флаг IsPromoted = 1 — «показывать всегда».
/// Ставим его сами, чтобы иконка появилась рядом с часами.
/// </summary>
internal static class TrayPromoter
{
    private const string SettingsKey = @"Control Panel\NotifyIconSettings";

    /// <summary>Возвращает true, если запись найдена и флаг выставлен.</summary>
    public static bool TryPromote()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return false;

            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: true);
            if (root is null) return false;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var subKey = root.OpenSubKey(subKeyName, writable: true);
                if (subKey is null) continue;

                if (subKey.GetValue("ExecutablePath") is not string path || string.IsNullOrWhiteSpace(path)) continue;

                string normalized;
                try { normalized = Path.GetFullPath(path); }
                catch { continue; }

                if (!string.Equals(normalized, exe, StringComparison.OrdinalIgnoreCase)) continue;

                if (subKey.GetValue("IsPromoted") is int promoted && promoted == 1) return true;

                subKey.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                Log.Write($"tray: promoted {subKeyName}");
                return true;
            }

            Log.Write("tray: no NotifyIconSettings entry yet");
            return false;
        }
        catch (Exception e)
        {
            Log.Write("tray promote failed: " + e.Message);
            return false;
        }
    }
}
