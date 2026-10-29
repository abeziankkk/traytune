using System.IO;
using System.Windows;

namespace TaskbarPlayer;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Второй экземпляр не нужен: два виджета и две иконки в трее.
        _singleInstance = new Mutex(true, "TaskbarPlayer.SingleInstance", out var isFirst);
        if (!isFirst)
        {
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
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarPlayer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:u}  {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // лог — не повод падать
        }
    }
}
