using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayTune;

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

/// <summary>Настройки. Лежат в %LOCALAPPDATA%\TrayTune\settings.json.</summary>
public sealed class Settings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public string? AppId { get; set; }

    /// <summary>
    /// Плавающий виджет вместо встроенного. По умолчанию false — приложение
    /// сразу встаёт в панель задач. Старое поле "Docked" из прежних версий
    /// игнорируется, поэтому у всех после обновления включается встроенный режим.
    /// </summary>
    public bool Floating { get; set; }

    /// <summary>Где в панели задач стоять.</summary>
    public TaskbarPlacement Placement { get; set; } = TaskbarPlacement.Left;

    public WidgetBackground Background { get; set; } = WidgetBackground.Transparent;

    /// <summary>Цвет акцента: авто по источнику или выбранный вручную.</summary>
    public AccentMode Accent { get; set; } = AccentMode.Auto;

    public bool ShowProgress { get; set; } = true;

    /// <summary>Ширина встроенного виджета в DIP: 260 / 300 / 380.</summary>
    public double WidgetWidth { get; set; } = 340;

    public bool Autostart { get; set; }

    /// <summary>
    /// Виджет был свёрнут в трей. При следующем запуске не показываем окно,
    /// пока пользователь сам не откроет его из трея.
    /// </summary>
    public bool Hidden { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayTune");

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

