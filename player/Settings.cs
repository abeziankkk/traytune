using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarPlayer;

public enum TaskbarPlacement
{
    Left,
    Center,
    Right,
}

public enum WidgetBackground
{
    /// <summary>Полностью прозрачный — под прозрачную панель задач (TranslucentTB и т.п.).</summary>
    Transparent,

    /// <summary>Лёгкая тёмная подложка, видно сквозь.</summary>
    Translucent,

    /// <summary>Плотная тёмная плашка.</summary>
    Solid,
}

/// <summary>Настройки. Лежат в %LOCALAPPDATA%\TaskbarPlayer\settings.json.</summary>
public sealed class Settings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public string? AppId { get; set; }

    /// <summary>Режим «внутри панели задач».</summary>
    public bool Docked { get; set; }

    /// <summary>Где в панели задач стоять.</summary>
    public TaskbarPlacement Placement { get; set; } = TaskbarPlacement.Left;

    public WidgetBackground Background { get; set; } = WidgetBackground.Transparent;

    public bool ShowProgress { get; set; } = true;

    public bool Autostart { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarPlayer");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
            }
        }
        catch
        {
            // Битый или старый файл — начинаем с значений по умолчанию.
        }
        return new Settings();
    }

    public Settings Clone() => (Settings)MemberwiseClone();

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Настройки — не повод падать.
        }
    }
}
