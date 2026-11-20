using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TrayTune;

/// <summary>
/// Проверка новой версии на GitHub. Сеть трогаем не чаще раза в сутки, результат
/// кэшируем в файле рядом с настройками — иначе при каждом запуске будет запрос
/// к GitHub (лимит 60 в час на неавторизованный запрос).
/// </summary>
internal static class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/abeziankkk/traytune/releases/latest";

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private sealed class Cache
    {
        public DateTimeOffset CheckedAt { get; set; }
        public string? Tag { get; set; }
        public string? Url { get; set; }
    }

    private static string CachePath =>
        Path.Combine(Path.GetDirectoryName(Settings.FilePath) ?? ".", "update-check.json");

    /// <summary>Возвращает (версия, ссылка), если на GitHub лежит более новая, иначе null.</summary>
    public static async Task<(string Tag, string Url)?> CheckAsync()
    {
        var cache = LoadCache();

        if (cache is null || DateTimeOffset.UtcNow - cache.CheckedAt > Interval)
        {
            cache = await FetchAsync();
            if (cache is not null) SaveCache(cache);
        }

        if (cache?.Tag is null || cache.Url is null) return null;
        if (!IsNewer(cache.Tag)) return null;

        return (cache.Tag, cache.Url);
    }

    private static async Task<Cache?> FetchAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TrayTune");

            using var response = await http.GetAsync(LatestReleaseUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tag)) return null;

            return new Cache
            {
                CheckedAt = DateTimeOffset.UtcNow,
                Tag = tag.GetString(),
                Url = root.TryGetProperty("html_url", out var url) ? url.GetString() : null,
            };
        }
        catch
        {
            // Нет сети или GitHub недоступен — молчим, это фоновая проверка.
            return null;
        }
    }

    private static bool IsNewer(string tag)
    {
        var remote = ParseVersion(tag);
        var current = Assembly.GetExecutingAssembly().GetName().Version;
        if (remote is null || current is null) return false;

        return remote > current;
    }

    private static Version? ParseVersion(string tag)
    {
        var text = tag.TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash > 0) text = text.Substring(0, dash);

        return Version.TryParse(text, out var version) ? version : null;
    }

    private static Cache? LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            return JsonSerializer.Deserialize<Cache>(File.ReadAllText(CachePath));
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(Cache cache)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath) ?? ".");
            File.WriteAllText(CachePath, JsonSerializer.Serialize(cache));
        }
        catch
        {
            // Кэш — оптимизация, не повод падать.
        }
    }
}
