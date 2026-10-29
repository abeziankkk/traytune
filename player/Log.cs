using System.IO;

namespace TrayTune;

/// <summary>Крошечный файловый лог рядом с exe: нужен, когда что-то не срабатывает молча.</summary>
internal static class Log
{
    private static readonly object Sync = new();

    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "debug.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // лог не критичен
        }
    }
}
