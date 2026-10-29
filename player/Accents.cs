using System.Windows.Media;

namespace TrayTune;

public enum AccentMode
{
    /// <summary>По источнику: Spotify — зелёный, Яндекс — жёлто-оранжевый, остальные — синий.</summary>
    Auto,

    Spotify,
    Yandex,

    /// <summary>Градиент «волны»: фиолетовый → розовый → оранжевый.</summary>
    Wave,

    /// <summary>Нейтральный синий — для VLC, AIMP и прочих.</summary>
    Other,
}

/// <summary>Кисти акцента: кнопка play и полоса прогресса.</summary>
internal static class Accents
{
    private static readonly Brush SpotifyBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)));

    private static readonly Brush YandexBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new System.Windows.Point(0, 0),
        EndPoint = new System.Windows.Point(1, 1),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xFF, 0xCC, 0x00), 0.0),
            new GradientStop(Color.FromRgb(0xFF, 0x8A, 0x00), 0.55),
            new GradientStop(Color.FromRgb(0xFF, 0x5C, 0x00), 1.0),
        },
    });

    /// <summary>
    /// «Цвет музыки» из коллекции Яндекс.Музыки: мягкий перелив фиолетового,
    /// малинового и тёплого оранжевого.
    /// </summary>
    private static readonly Brush WaveBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new System.Windows.Point(0, 0),
        EndPoint = new System.Windows.Point(1, 1),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0x6E, 0x4B, 0xFF), 0.0),
            new GradientStop(Color.FromRgb(0xE1, 0x4F, 0xD0), 0.5),
            new GradientStop(Color.FromRgb(0xFF, 0x9A, 0x3D), 1.0),
        },
    });

    private static readonly Brush OtherBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0xA9, 0xFF)));

    public static Brush Resolve(AccentMode mode, string? source) => mode switch
    {
        AccentMode.Spotify => SpotifyBrush,
        AccentMode.Yandex => YandexBrush,
        AccentMode.Wave => WaveBrush,
        AccentMode.Other => OtherBrush,
        _ => ResolveAuto(source),
    };

    private static Brush ResolveAuto(string? source)
    {
        var id = (source ?? "").ToLowerInvariant();

        if (id.Contains("spotify")) return SpotifyBrush;
        if (id.Contains("yandex")) return YandexBrush;

        // Браузер не сообщает, какой сайт играет, поэтому по умолчанию — нейтральный синий.
        return OtherBrush;
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
