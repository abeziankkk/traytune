using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace TrayTune;

/// <summary>
/// Однофайловая (portable) сборка распаковывает свои внутренности в %TEMP%\.net.
/// Если хоть раз запустить её от имени администратора, папка получает уровень
/// целостности High — и после этого обычный запуск не может в неё писать
/// (ошибка 5: «Failed to create default extraction directory»). Приложение тогда
/// просто не стартует, хотя файл целый.
///
/// Пока процесс повышенный — возвращаем папке обычные права и метку, чтобы
/// следующий обычный запуск снова работал. Заодно создаём папку заранее, если её
/// нет: тогда её владельцем будет обычный процесс.
/// </summary>
internal static class BundleRepair
{
    /// <summary>Папка распаковки недоступна на запись — следующий запуск может не сработать.</summary>
    public static bool ExtractionDirUnusable { get; private set; }

    public static void Run()
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), ".net");

            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { /* не критично */ }
                return;
            }

            if (IsElevated())
            {
                Icacls(dir, "/reset", "/T", "/C", "/Q");
                Icacls(dir, "/setintegritylevel", "(OI)(CI)Medium", "/T", "/C", "/Q");
                Log.Write("bundle: returned normal rights and label to " + dir);
                return;
            }

            if (CanWrite(dir)) return;

            ExtractionDirUnusable = true;
            Log.Write("bundle: cannot write to " + dir + " (portable build will not start)");
        }
        catch (Exception e)
        {
            Log.Write("bundle check failed: " + e.Message);
        }
    }

    private static bool IsElevated()
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

    private static bool CanWrite(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".traytune-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Icacls(string dir, params string[] arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("icacls.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add(dir);
            foreach (var argument in arguments) psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Без icacls живём дальше: это подстраховка, а не основная логика.
        }
    }
}
