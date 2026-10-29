using System.IO;
using System.Windows.Media.Imaging;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TaskbarPlayer;

/// <summary>Одна медиасессия Windows: приложение, которое сейчас что-то публикует.</summary>
public sealed class SessionEntry
{
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public GlobalSystemMediaTransportControlsSession Session { get; init; } = null!;
}

/// <summary>Состояние для отрисовки. Обновляется сервисом, читается окном.</summary>
public sealed class PlayerState
{
    public bool HasSession { get; set; }
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public BitmapSource? Art { get; set; }
    public bool IsPlaying { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public bool CanPlayPause { get; set; }
    public bool CanNext { get; set; }
    public bool CanPrevious { get; set; }
    public bool CanSeek { get; set; }
}

/// <summary>
/// Обёртка над SMTC. Один экземпляр обслуживает все плееры сразу:
/// Spotify, Яндекс.Музыку в браузере, VLC — что угодно, что публикует сессию.
/// </summary>
public sealed class MediaSessionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>? _propsHandler;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>? _playbackHandler;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, TimelinePropertiesChangedEventArgs>? _timelineHandler;

    private string? _artKey;
    private TimeSpan _basePosition;
    private DateTimeOffset _stamp;
    private double _rate = 1;
    private bool _playing;

    public PlayerState State { get; } = new();
    public string SelectedAppId { get; set; } = "";
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        if (_manager is null)
        {
            State.HasSession = false;
            Changed?.Invoke();
            return;
        }

        _manager.SessionsChanged += (_, _) => Rebind(SelectedAppId);
        Rebind(SelectedAppId);
    }

    public IReadOnlyList<SessionEntry> GetSessions()
    {
        if (_manager is null) return [];

        var list = new List<SessionEntry>();
        foreach (var s in _manager.GetSessions())
        {
            var id = s.SourceAppUserModelId ?? "";
            list.Add(new SessionEntry { AppId = id, DisplayName = PrettyName(id), Session = s });
        }
        return list;
    }

    public void Select(string appId)
    {
        SelectedAppId = appId;
        Rebind(appId);
    }

    /// <summary>Пересчитывает позицию между обновлениями SMTC (они приходят редко).</summary>
    public void Tick()
    {
        if (!_playing) return;

        var elapsed = DateTimeOffset.UtcNow - _stamp;
        var pos = _basePosition + TimeSpan.FromSeconds(elapsed.TotalSeconds * _rate);
        if (State.Duration > TimeSpan.Zero && pos > State.Duration) pos = State.Duration;
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        State.Position = pos;
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        var s = _session;
        if (s is null) return false;
        try { return await s.TryTogglePlayPauseAsync(); }
        catch { return false; }
    }

    public async Task<bool> NextAsync()
    {
        var s = _session;
        if (s is null) return false;
        try { return await s.TrySkipNextAsync(); }
        catch { return false; }
    }

    public async Task<bool> PreviousAsync()
    {
        var s = _session;
        if (s is null) return false;
        try { return await s.TrySkipPreviousAsync(); }
        catch { return false; }
    }

    public async Task<bool> SeekAsync(TimeSpan position)
    {
        var s = _session;
        if (s is null) return false;
        try
        {
            var ok = await s.TryChangePlaybackPositionAsync(position.Ticks);
            if (ok)
            {
                _basePosition = position;
                _stamp = DateTimeOffset.UtcNow;
                State.Position = position;
            }
            return ok;
        }
        catch { return false; }
    }

    private void Rebind(string? preferredAppId)
    {
        if (_manager is null) return;

        var sessions = _manager.GetSessions();
        var pick =
            sessions.FirstOrDefault(s => string.Equals(s.SourceAppUserModelId, preferredAppId, StringComparison.OrdinalIgnoreCase))
            ?? _manager.GetCurrentSession()
            ?? sessions.FirstOrDefault();

        Attach(pick);
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? next)
    {
        if (ReferenceEquals(next, _session))
        {
            _ = RefreshAsync();
            return;
        }

        if (_session is not null)
        {
            if (_propsHandler is not null) _session.MediaPropertiesChanged -= _propsHandler;
            if (_playbackHandler is not null) _session.PlaybackInfoChanged -= _playbackHandler;
            if (_timelineHandler is not null) _session.TimelinePropertiesChanged -= _timelineHandler;
        }

        _session = next;
        _artKey = null;

        if (_session is not null)
        {
            _propsHandler = (_, _) => _ = RefreshAsync();
            _playbackHandler = (_, _) => _ = RefreshAsync();
            _timelineHandler = (_, _) => _ = RefreshAsync();

            _session.MediaPropertiesChanged += _propsHandler;
            _session.PlaybackInfoChanged += _playbackHandler;
            _session.TimelinePropertiesChanged += _timelineHandler;
        }

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!await _gate.WaitAsync(0)) return;
        try
        {
            var s = _session;
            if (s is null)
            {
                State.HasSession = false;
                State.Title = "";
                State.Artist = "";
                State.Art = null;
                State.Source = "";
                State.IsPlaying = false;
                State.Position = TimeSpan.Zero;
                State.Duration = TimeSpan.Zero;
                Changed?.Invoke();
                return;
            }

            var info = s.GetPlaybackInfo();
            var timeline = s.GetTimelineProperties();
            var props = await s.TryGetMediaPropertiesAsync();

            _playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            _rate = 1;
            _basePosition = timeline.Position;
            _stamp = DateTimeOffset.UtcNow;

            State.HasSession = true;
            State.Source = PrettyName(s.SourceAppUserModelId ?? "");
            State.Title = props?.Title ?? "";
            State.Artist = props?.Artist ?? "";
            State.IsPlaying = _playing;
            State.Position = _basePosition;
            State.Duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : TimeSpan.Zero;

            var controls = info.Controls;
            State.CanPlayPause = controls.IsPlayPauseToggleEnabled;
            State.CanNext = controls.IsNextEnabled;
            State.CanPrevious = controls.IsPreviousEnabled;
            State.CanSeek = controls.IsPlaybackPositionEnabled;

            await LoadArtAsync(props);

            Changed?.Invoke();
        }
        catch
        {
            // Сессия могла закрыться между вызовами — следующий тик всё поправит.
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadArtAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? props)
    {
        var key = $"{props?.Title}|{props?.Artist}";
        if (string.Equals(key, _artKey, StringComparison.Ordinal)) return;
        _artKey = key;

        if (props?.Thumbnail is null)
        {
            State.Art = null;
            return;
        }

        try
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            var size = stream.Size;
            if (size == 0 || size > 32 * 1024 * 1024)
            {
                State.Art = null;
                return;
            }

            var bytes = new byte[size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)size);
            reader.ReadBytes(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            State.Art = image;
        }
        catch
        {
            State.Art = null;
        }
    }

    public static string PrettyName(string appId)
    {
        var id = (appId ?? "").ToLowerInvariant();

        if (id.Contains("spotify")) return "Spotify";
        if (id.Contains("yandex")) return "Яндекс.Музыка";
        if (id.Contains("msedge")) return "Edge";
        if (id.Contains("chrome")) return "Chrome";
        if (id.Contains("firefox")) return "Firefox";
        if (id.Contains("opera")) return "Opera";
        if (id.Contains("vivaldi")) return "Vivaldi";
        if (id.Contains("brave")) return "Brave";
        if (id.Contains("vlc")) return "VLC";
        if (id.Contains("aimp")) return "AIMP";

        var name = Path.GetFileNameWithoutExtension(appId ?? "");
        return string.IsNullOrWhiteSpace(name) ? (appId ?? "Источник") : name;
    }
}
