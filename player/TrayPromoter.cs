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

            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: false);
            if (root is null)
            {
                Log.Write("tray: no NotifyIconSettings key");
                return false;
            }

            var names = root.GetSubKeyNames();
            if (names.Length == 0)
            {
                Log.Write("tray: NotifyIconSettings is empty yet");
                return false;
            }

            var sawOurEntry = false;

            foreach (var subKeyName in names)
            {
                string? path;
                using (var read = root.OpenSubKey(subKeyName, writable: false))
                {
                    path = read?.GetValue("ExecutablePath") as string;
                }

                if (string.IsNullOrWhiteSpace(path)) continue;

                string normalized;
                try { normalized = Path.GetFullPath(path); }
                catch { continue; }

                if (!string.Equals(normalized, exe, StringComparison.OrdinalIgnoreCase)) continue;

                sawOurEntry = true;

                // Сначала смотрим только на чтение: запись может быть уже выставлена
                // (например, администратором), а писать в ключ обычному процессу
                // Windows иногда не даёт — это не повод считать значок спрятанным.
                object? current;
                try
                {
                    using var read = root.OpenSubKey(subKeyName, writable: false);
                    current = read?.GetValue("IsPromoted");
                }
                catch (Exception inner)
                {
                    Log.Write($"tray: cannot read {subKeyName}: {inner.Message}");
                    continue;
                }

                if (current is int value && value == 1)
                {
                    Log.Write($"tray: already promoted {subKeyName}");
                    return true;
                }

                try
                {
                    using var subKey = root.OpenSubKey(subKeyName, writable: true);
                    if (subKey is null)
                    {
                        Log.Write($"tray: cannot open {subKeyName} for write");
                        continue;
                    }

                    subKey.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                    Log.Write($"tray: promoted {subKeyName}");
                    return true;
                }
                catch (Exception inner)
                {
                    Log.Write($"tray: promote denied for {subKeyName}: {inner.Message}");
                }
            }

            Log.Write(sawOurEntry
                ? "tray: entry found but not promoted"
                : "tray: no NotifyIconSettings entry for this exe yet");
            return false;
        }
        catch (Exception e)
        {
            Log.Write("tray promote failed: " + e.Message);
            return false;
        }
    }
}
