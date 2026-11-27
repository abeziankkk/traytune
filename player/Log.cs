using System.IO;

namespace TrayTune;

/// <summary>
/// Крошечный файловый лог: нужен, когда что-то не срабатывает молча. Лежит рядом
/// с настройками (`%LOCALAPPDATA%\TrayTune\debug.log`), а не рядом с exe: иначе
/// он появляется как мусор в папке со сборкой и мешает публикации.
/// </summary>
internal static class Log
{
    private static readonly object Sync = new();

    private static string Folder =>
        Environment.GetEnvironmentVariable("TRAYTUNE_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayTune");

    public static string FilePath => System.IO.Path.Combine(Folder, "debug.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // лог не критичен
        }
    }
}
