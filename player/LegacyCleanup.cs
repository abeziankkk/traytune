using System.Diagnostics;

namespace TrayTune;

/// <summary>
/// Хвосты старых версий. До 0.2.0 приложение умело запускать себя через задачи
/// планировщика (чтобы стартовать без запроса UAC); от этого отказались, но созданные
/// тогда задачи остаются у пользователей и включают автозапуск сами по себе.
/// Повышенный процесс их молча удаляет — обычному прав на запись не хватает.
/// </summary>
internal static class LegacyCleanup
{
    private static readonly string[] LegacyTasks =
    {
        @"\TrayTune",
        @"\TrayTune.Run",
    };

    public static void Run()
    {
        if (!BundleRepair.IsElevated()) return;

        foreach (var task in LegacyTasks)
        {
            if (Delete(task)) Log.Write("legacy: removed scheduled task " + task);
        }
    }

    private static bool Delete(string task)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("/delete");
            psi.ArgumentList.Add("/tn");
            psi.ArgumentList.Add(task);
            psi.ArgumentList.Add("/f");

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            // Нет прав или задачи уже нет — не повод падать.
            return false;
        }
    }
}
