using System.Globalization;

namespace TrayTune;

public enum AppLanguage
{
    /// <summary>Русский, если система русская, иначе английский.</summary>
    Auto,

    Ru,
    En,
}

/// <summary>
/// Строки интерфейса. Русские по умолчанию, английские — когда выбран English
/// или система не русская. Ключи — плоские, чтобы их не искать по всему коду.
/// </summary>
internal static class Loc
{
    private static readonly Dictionary<string, (string Ru, string En)> Strings = new()
    {
        // окно настроек
        ["settings.title"] = ("Настройки", "Settings"),
        ["settings.card.placement"] = ("ПОЛОЖЕНИЕ В ПАНЕЛИ", "TASKBAR POSITION"),
        ["placement.left"] = ("Слева", "Left"),
        ["placement.center"] = ("По центру", "Center"),
        ["placement.right"] = ("Справа", "Right"),

        ["settings.card.background"] = ("ФОН", "BACKGROUND"),
        ["bg.transparent"] = ("Прозрачный", "Transparent"),
        ["bg.translucent"] = ("Полупрозрачный", "Translucent"),
        ["bg.solid"] = ("Тёмный", "Solid"),

        ["settings.card.accent"] = ("ЦВЕТ АКЦЕНТА", "ACCENT COLOR"),
        ["accent.auto"] = ("Авто", "Auto"),
        ["accent.yandex"] = ("Яндекс", "Yandex"),
        ["accent.wave"] = ("Волна", "Wave"),
        ["accent.cover"] = ("Из обложки", "From artwork"),
        ["accent.other"] = ("Другой", "Custom"),

        ["settings.card.width"] = ("ШИРИНА", "WIDTH"),
        ["width.narrow"] = ("Узкая", "Narrow"),
        ["width.normal"] = ("Обычная", "Normal"),
        ["width.wide"] = ("Широкая", "Wide"),

        ["settings.card.behavior"] = ("ПОВЕДЕНИЕ", "BEHAVIOR"),
        ["behavior.progress"] = ("Показывать полосу прогресса", "Show the progress bar"),
        ["behavior.marquee"] = ("Прокручивать длинные названия", "Scroll long titles"),
        ["behavior.docked"] = ("Встроить в панель задач", "Dock into the taskbar"),
        ["behavior.autostart"] = ("Запускать при входе в Windows", "Start with Windows"),
        ["behavior.volume"] = ("Менять громкость колесом мыши", "Change volume with the mouse wheel"),

        ["settings.card.language"] = ("ЯЗЫК", "LANGUAGE"),
        ["language.auto"] = ("Как в системе", "Same as system"),

        ["settings.card.updates"] = ("ОБНОВЛЕНИЯ", "UPDATES"),
        ["updates.check"] = ("Проверять новые версии", "Check for new versions"),

        ["settings.hint"] = ("Изменения применяются сразу после сохранения", "Changes apply right after saving"),
        ["button.cancel"] = ("Отмена", "Cancel"),
        ["button.save"] = ("Сохранить", "Save"),

        // виджет
        ["widget.no_track"] = ("Ничего не играет", "Nothing is playing"),
        ["widget.default_artist"] = ("Spotify или Яндекс.Музыка", "Spotify or Yandex Music"),
        ["widget.no_session"] = ("Включи музыку в Spotify или браузере", "Start music in Spotify or a browser"),
        ["widget.no_sessions"] = ("Медиасессии недоступны", "Media sessions are not available"),
        ["widget.tooltip_source"] = ("Источник: {0}", "Source: {0}"),
        ["widget.tray_tooltip"] = ("Мини-плеер", "Mini player"),
        ["widget.volume"] = ("Громкость {0}%", "Volume {0}%"),

        // меню
        ["menu.show"] = ("Показать", "Show"),
        ["menu.hide"] = ("Скрыть", "Hide"),
        ["menu.settings"] = ("Настройки…", "Settings…"),
        ["menu.dock"] = ("Встроить в панель задач", "Dock into the taskbar"),
        ["menu.exit"] = ("Выход", "Exit"),
        ["menu.sources"] = ("Источник", "Source"),
        ["menu.no_sources"] = ("ничего не играет", "nothing is playing"),
        ["menu.update"] = ("Доступна версия {0}", "Version {0} is available"),

        // уведомления
        ["notify.hidden"] = ("TrayTune свёрнут в трей", "TrayTune was minimized to the tray"),
        ["notify.compat"] = ("Панель задач не отдала окно — виджет поставлен поверх неё (режим совместимости)",
            "Windows did not hand over the taskbar — the widget sits on top of it (compatibility mode)"),
        ["notify.dock_lost"] = ("встраивание потеряно — вернул обычный виджет",
            "docking was lost — back to the normal widget"),
        ["notify.dock_failed"] = ("Не удалось встроить: {0}", "Could not dock: {0}"),
        ["notify.update"] = ("Доступна версия {0}", "Version {0} is available"),

        // трей
        ["tray.working"] = ("TrayTune работает", "TrayTune is running"),
        ["tray.hint"] = ("Значок спрятан в переполнении панели задач. Включить: Параметры → Персонализация → Панель задач → Другие значки в области уведомлений → TrayTune.",
            "The icon is hidden in the taskbar overflow. To show it: Settings → Personalization → Taskbar → Other system tray icons → TrayTune."),
    };

    private static bool _english;

    public static bool IsEnglish => _english;

    public static void Configure(AppLanguage language)
    {
        _english = language switch
        {
            AppLanguage.En => true,
            AppLanguage.Ru => false,
            _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("ru", StringComparison.OrdinalIgnoreCase),
        };
    }

    public static string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var pair)) return key;
        return _english ? pair.En : pair.Ru;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);
}
