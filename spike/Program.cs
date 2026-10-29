using System.Text;
using Windows.Media.Control;
using Windows.Storage.Streams;

// Э0: спайк. Проверяем, что SMTC отдаёт сессии Spotify (десктоп) и Яндекс.Музыки (браузер):
// метаданные, обложку, состояние, таймлайн и команды управления.
//
// Запуск:
//   dotnet run -- dump   — один раз вывести все сессии
//   dotnet run -- watch  — обновлять строку статуса раз в секунду
//   dotnet run           — интерактивно: p=play/pause, n=next, b=prev, d=dump, q=выход

Console.OutputEncoding = Encoding.UTF8;

var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "repl";

var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
if (manager is null)
{
    Console.WriteLine("SMTC недоступен: RequestAsync вернул null.");
    return 1;
}

if (mode == "dump")
{
    await DumpAllAsync(manager);
    return 0;
}

if (mode == "watch")
{
    await WatchAsync(manager);
    return 0;
}

await ReplAsync(manager);
return 0;

// ---------------------------------------------------------------- helpers

static async Task DumpAllAsync(GlobalSystemMediaTransportControlsSessionManager manager)
{
    var sessions = manager.GetSessions();
    Console.WriteLine($"Сессий: {sessions.Count}  (сейчас активна: {manager.GetCurrentSession()?.SourceAppUserModelId ?? "нет"})");
    Console.WriteLine();

    var i = 0;
    foreach (var s in sessions)
    {
        i++;
        var info = s.GetPlaybackInfo();
        var props = await TryAsync(() => s.TryGetMediaPropertiesAsync());
        var timeline = s.GetTimelineProperties();
        var controls = info.Controls;

        Console.WriteLine($"[{i}] {s.SourceAppUserModelId}");
        Console.WriteLine($"    playback : {info.PlaybackStatus}  (rate {info.PlaybackRate:0.##})");
        Console.WriteLine($"    title    : {props?.Title ?? "<null>"}");
        Console.WriteLine($"    artist   : {props?.Artist ?? "<null>"}");
        Console.WriteLine($"    album    : {props?.AlbumTitle ?? "<null>"}");
        Console.WriteLine($"    track#   : {props?.TrackNumber ?? -1}");
        Console.WriteLine($"    timeline : {Format(timeline.Position)} / {Format(timeline.EndTime)}");
        Console.WriteLine($"    controls : playPause={controls.IsPlayPauseToggleEnabled} next={controls.IsNextEnabled} prev={controls.IsPreviousEnabled} seek={controls.IsPlaybackPositionEnabled}");
        Console.WriteLine($"    thumbnail: {await DescribeThumbnailAsync(props)}");
        Console.WriteLine();
    }
}

static async Task WatchAsync(GlobalSystemMediaTransportControlsSessionManager manager)
{
    string? last = null;
    while (true)
    {
        var line = await DescribeCurrentAsync(manager);
        if (line != last)
        {
            Console.WriteLine(line);
            last = line;
        }
        await Task.Delay(1000);
    }
}

static async Task ReplAsync(GlobalSystemMediaTransportControlsSessionManager manager)
{
    Console.WriteLine("SMTC-спайк. p=play/pause, n=next, b=prev, d=dump, q=выход");
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine(await DescribeCurrentAsync(manager));
        Console.Write("> ");
        var cmd = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (cmd is null or "q") return;

        var session = manager.GetCurrentSession();
        if (session is null && cmd is not "d")
        {
            Console.WriteLine("Нет активной сессии.");
            continue;
        }

        switch (cmd)
        {
            case "d":
                await DumpAllAsync(manager);
                break;
            case "p":
                Console.WriteLine(await session!.TryTogglePlayPauseAsync() ? "ok" : "не удалось");
                break;
            case "n":
                Console.WriteLine(await session!.TrySkipNextAsync() ? "ok" : "не удалось");
                break;
            case "b":
                Console.WriteLine(await session!.TrySkipPreviousAsync() ? "ok" : "не удалось");
                break;
            case "":
                break;
            default:
                Console.WriteLine($"Неизвестная команда: {cmd}");
                break;
        }
    }
}

static async Task<string> DescribeCurrentAsync(GlobalSystemMediaTransportControlsSessionManager manager)
{
    var session = manager.GetCurrentSession();
    if (session is null) return "нет активной сессии";

    var info = session.GetPlaybackInfo();
    var props = await TryAsync(() => session.TryGetMediaPropertiesAsync());
    var timeline = session.GetTimelineProperties();
    var art = props?.Thumbnail is null ? "noart" : "art";

    return $"[{session.SourceAppUserModelId}] {info.PlaybackStatus} | {props?.Artist} — {props?.Title} | {Format(timeline.Position)}/{Format(timeline.EndTime)} | {art}";
}

static async Task<string> DescribeThumbnailAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? props)
{
    if (props?.Thumbnail is null) return "нет";

    try
    {
        using var stream = await props.Thumbnail.OpenReadAsync();
        var size = stream.Size;
        var bytes = new byte[size];
        using var reader = new DataReader(stream);
        await reader.LoadAsync((uint)size);
        reader.ReadBytes(bytes);

        var outDir = Path.Combine(AppContext.BaseDirectory, "spike-out");
        Directory.CreateDirectory(outDir);
        var safe = string.Join("_", props.Title.Split(Path.GetInvalidFileNameChars())).Trim();
        var file = Path.Combine(outDir, $"{(safe.Length > 0 ? safe : "thumb")}.png");
        await File.WriteAllBytesAsync(file, bytes);
        return $"{size} байт -> {file}";
    }
    catch (Exception e)
    {
        return $"ошибка чтения: {e.Message}";
    }
}

static async Task<T?> TryAsync<T>(Func<Windows.Foundation.IAsyncOperation<T>> f) where T : class
{
    try { return await f(); }
    catch (Exception e) { Console.WriteLine($"    ! {e.GetType().Name}: {e.Message}"); return null; }
}

static string Format(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
