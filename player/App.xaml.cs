using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace TrayTune;

public partial class App : Application
{
    /// <summary>Имя события, которым второй экземпляр просит показать настройки.</summary>
    internal const string ShowSettingsEventName = "TrayTune.ShowSettings";

    private Mutex? _singleInstance;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Стабильный AppUserModelID: Windows 11 привязывает к нему значок в трее,
        // без него иконка может не запоминаться и не показываться.
        try { SetCurrentProcessExplicitAppUserModelID("TrayTune.MiniPlayer"); } catch { }

        // Подстраховка для однофайловой сборки: не даём папке %TEMP%\.net
        // остаться с правами администратора (иначе следующий запуск не стартует).
        BundleRepair.Run();

        // Второй экземпляр не нужен: два виджета и две иконки в трее.
        var instanceName = Environment.GetEnvironmentVariable("TRAYTUNE_INSTANCE") ?? "TrayTune.SingleInstance";
        _singleInstance = new Mutex(true, instanceName, out var isFirst);
        if (!isFirst)
        {
            // Зато второй запуск умеет попросить уже работающую копию открыть настройки.
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var signal))
                {
                    signal.Set();
                    signal.Dispose();
                }
            }
            catch
            {
                // не смогли — просто выходим
            }

            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);

        base.OnStartup(e);
    }

    private static void Log(Exception? e)
    {
        if (e is null) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayTune");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:u}  {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // лог — не повод падать
        }
    }
}
