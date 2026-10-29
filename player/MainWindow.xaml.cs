using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace TaskbarPlayer;

public partial class MainWindow : Window
{
    private const string PlayGlyph = "\uE768";
    private const string PauseGlyph = "\uE769";
    private const double WidgetWidth = 352;
    private const double WidgetHeight = 88;
    private const double DockedWidth = 300;

    private static readonly Brush SolidBackground = new SolidColorBrush(Color.FromArgb(0xF2, 0x18, 0x18, 0x20));
    private static readonly Brush TranslucentBackground = new SolidColorBrush(Color.FromArgb(0x59, 0x12, 0x12, 0x18));
    private static readonly Brush HoverBackground = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
    private static readonly Brush SolidBorder = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    private readonly MediaSessionService _media = new();
    private readonly Settings _settings = Settings.Load();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _dockTimer;
    private readonly DropShadowEffect _shadow;

    private WinForms.NotifyIcon? _tray;
    private bool _initialized;
    private bool _docked;
    private bool _hover;

    public MainWindow()
    {
        InitializeComponent();

        _shadow = RootShadow;
        Root.MouseEnter += (_, _) => { _hover = true; ApplyLook(); };
        Root.MouseLeave += (_, _) => { _hover = false; ApplyLook(); };

        SourceInitialized += (_, _) => Native.MakeNoActivateToolWindow(this);
        Loaded += OnLoadedAsync;
        Closing += (_, _) => SaveSettings();
        Closed += (_, _) => _tray?.Dispose();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) =>
        {
            _media.Tick();
            UpdateProgress();
        };

        // Панель задач могут перестроить (смена разрешения, обновление) — возвращаем окно на место.
        _dockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _dockTimer.Tick += (_, _) =>
        {
            if (!_docked) return;

            if (!TaskbarHost.IsAttached(this))
            {
                SetDocked(false, "встраивание потеряно — вернул обычный виджет");
                return;
            }
            TaskbarHost.Reposition(this);
        };

        _media.Changed += OnMediaChanged;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        PlaceWindow();
        ApplyLook();
        BuildTray();

        _media.SelectedAppId = _settings.AppId ?? "";
        _timer.Start();
        Render();

        try
        {
            await _media.InitializeAsync();
        }
        catch (Exception ex)
        {
            TitleText.Text = "Медиасессии недоступны";
            ArtistText.Text = ex.GetType().Name;
        }

        Render();

        var args = Environment.GetCommandLineArgs();
        var wantDock = _settings.Docked || args.Any(a => string.Equals(a, "--dock", StringComparison.OrdinalIgnoreCase));
        if (wantDock) SetDocked(true);

        if (args.Any(a => string.Equals(a, "--undock", StringComparison.OrdinalIgnoreCase)))
        {
            SetDocked(false);
        }

        if (args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            OpenSettings();
        }
    }

    // ------------------------------------------------------------- отрисовка

    private void OnMediaChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Render));
            return;
        }
        Render();
    }

    private void Render()
    {
        var st = _media.State;

        var title = string.IsNullOrWhiteSpace(st.Title) ? "Ничего не играет" : st.Title;
        var subtitle = st.HasSession
            ? (string.IsNullOrWhiteSpace(st.Artist) ? st.Source : st.Artist)
            : "Включи музыку в Spotify или браузере";

        TitleText.Text = title;
        ArtistText.Text = subtitle;
        CoverImage.Source = st.Art;
        CoverPlaceholder.Visibility = st.Art is null ? Visibility.Visible : Visibility.Collapsed;
        PlayPauseGlyph.Text = st.IsPlaying ? PauseGlyph : PlayGlyph;
        PlayButton.IsEnabled = st.HasSession;
        PrevButton.IsEnabled = st.HasSession;
        NextButton.IsEnabled = st.HasSession;

        TitleTextC.Text = title;
        ArtistTextC.Text = subtitle;
        CoverImageC.Source = st.Art;
        CoverPlaceholderC.Visibility = st.Art is null ? Visibility.Visible : Visibility.Collapsed;
        PlayPauseGlyphC.Text = st.IsPlaying ? PauseGlyph : PlayGlyph;
        PlayButtonC.IsEnabled = st.HasSession;
        PrevButtonC.IsEnabled = st.HasSession;
        NextButtonC.IsEnabled = st.HasSession;

        ToolTip = st.HasSession ? $"Источник: {st.Source}" : null;

        UpdateProgress();
        UpdateTrayText();
    }

    private void UpdateProgress()
    {
        var st = _media.State;
        var total = st.Duration.TotalSeconds;
        var fraction = total > 0.5 ? Math.Clamp(st.Position.TotalSeconds / total, 0, 1) : 0;

        ProgressFill.Width = Math.Max(0, ProgressHost.ActualWidth * fraction);
        ProgressFillC.Width = Math.Max(0, ProgressHostC.ActualWidth * fraction);
    }

    private void UpdateTrayText()
    {
        if (_tray is null) return;

        var st = _media.State;
        var text = st.HasSession
            ? $"{st.Artist} — {st.Title}".Trim(' ', '—')
            : "Мини-плеер";

        if (text.Length > 62) text = text[..62];
        try { _tray.Text = string.IsNullOrWhiteSpace(text) ? "Мини-плеер" : text; }
        catch { /* у NotifyIcon лимит на длину подсказки */ }
    }

    /// <summary>Фон, рамка, тень, прогресс — всё, что зависит от настроек, а не от трека.</summary>
    private void ApplyLook()
    {
        var background = _settings.Background;

        Root.Background = background switch
        {
            WidgetBackground.Transparent => _hover ? HoverBackground : Brushes.Transparent,
            WidgetBackground.Translucent => TranslucentBackground,
            _ => SolidBackground,
        };

        var solid = background == WidgetBackground.Solid;
        Root.BorderBrush = solid ? SolidBorder : Brushes.Transparent;
        Root.BorderThickness = new Thickness(solid ? 1 : 0);
        Root.CornerRadius = new CornerRadius(_docked ? 8 : 16);

        // Тень имеет смысл только на плотной плашке: на прозрачной она даёт грязный ореол.
        Root.Effect = solid ? _shadow : null;

        var progressVisibility = _settings.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
        ProgressHost.Visibility = progressVisibility;
        ProgressHostC.Visibility = progressVisibility;
    }

    // ------------------------------------------------------------- режим панели задач

    private void SetDocked(bool docked, string? note = null)
    {
        if (docked == _docked) return;

        if (!docked)
        {
            _dockTimer.Stop();
            TaskbarHost.Detach(this);

            _docked = false;
            ApplyCompactLayout(false);
            Width = WidgetWidth;
            Height = WidgetHeight;
            PlaceWindow();   // вернёт на позицию, сохранённую до стыковки
            _settings.Docked = false;
            SaveSettings();
            ApplyLook();
            if (note is not null) Notify(note);
            return;
        }

        // Запоминаем нормальную позицию ДО того, как окно станет дочерним:
        // внутри панели Left/Top означают уже совсем другое.
        _settings.Left = Left;
        _settings.Top = Top;

        ApplyCompactLayout(true);
        Width = DockedWidth;

        if (!TaskbarHost.TryAttach(this, DockedWidth, _settings.Placement, out var heightDip, out var info))
        {
            ApplyCompactLayout(false);
            Width = WidgetWidth;
            Height = WidgetHeight;
            PlaceWindow();
            ApplyLook();
            Notify($"Не удалось встроить: {info}");
            return;
        }

        Height = heightDip;
        _docked = true;
        _dockTimer.Start();
        TaskbarHost.Reposition(this);

        _settings.Docked = true;
        SaveSettings();
        ApplyLook();

        if (note is not null) Notify(note);
    }

    private void ApplyCompactLayout(bool compact)
    {
        FullView.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactView.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        ApplyLook();
    }

    private void Notify(string text)
    {
        try { _tray?.ShowBalloonTip(2500, "Мини-плеер", text, WinForms.ToolTipIcon.Info); }
        catch { /* не критично */ }
    }

    // ------------------------------------------------------------- настройки

    private void OpenSettings()
    {
        // На время диалога разрешаем окну забирать фокус, иначе клавиатура останется в другом приложении.
        Native.SetActivatable(this, true);
        try
        {
            var dialog = new SettingsWindow(_settings);

            // Владельцем может быть только top-level окно: встроенный в панель виджет — child.
            if (!_docked) dialog.Owner = this;

            if (dialog.ShowDialog() != true) return;

            Autostart.Set(_settings.Autostart);
            ApplyLook();

            if (_settings.Docked != _docked)
            {
                SetDocked(_settings.Docked);
            }
            else if (_docked)
            {
                TaskbarHost.SetPlacement(this, _settings.Placement);
            }
        }
        finally
        {
            Native.SetActivatable(this, false);
            if (!_docked)
            {
                Topmost = false;
                Topmost = true;
            }
        }
    }

    // ------------------------------------------------------------- команды

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        await _media.TogglePlayPauseAsync();
        await Task.Delay(120);
        Render();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await _media.NextAsync();
        await Task.Delay(120);
        Render();
    }

    private async void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        await _media.PreviousAsync();
        await Task.Delay(120);
        Render();
    }

    private async void ProgressHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var st = _media.State;
        if (!st.HasSession || !st.CanSeek || st.Duration <= TimeSpan.Zero) return;

        if (sender is not FrameworkElement host || host.ActualWidth <= 0) return;

        var fraction = Math.Clamp(e.GetPosition(host).X / host.ActualWidth, 0, 1);
        await _media.SeekAsync(TimeSpan.FromTicks((long)(st.Duration.Ticks * fraction)));
        UpdateProgress();
    }

    // ------------------------------------------------------------- окно

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_docked) return; // внутри панели задач окно не таскаем
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопку отпустили до начала перетаскивания */ }

        _settings.Left = Left;
        _settings.Top = Top;
        SaveSettings();
    }

    private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = BuildContextMenu();
        menu.PlacementTarget = Root;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var sources = new MenuItem { Header = "Источник" };
        var sessions = _media.GetSessions();
        if (sessions.Count == 0)
        {
            sources.Items.Add(new MenuItem { Header = "ничего не играет", IsEnabled = false });
        }
        else
        {
            foreach (var entry in sessions)
            {
                var appId = entry.AppId;
                var item = new MenuItem
                {
                    Header = entry.DisplayName,
                    IsCheckable = true,
                    IsChecked = string.Equals(appId, _media.SelectedAppId, StringComparison.OrdinalIgnoreCase),
                };
                item.Click += (_, _) =>
                {
                    _media.Select(appId);
                    _settings.AppId = appId;
                    SaveSettings();
                    Render();
                };
                sources.Items.Add(item);
            }
        }
        menu.Items.Add(sources);
        menu.Items.Add(new Separator());

        var dock = new MenuItem
        {
            Header = "Встроить в панель задач",
            IsCheckable = true,
            IsChecked = _docked,
        };
        dock.Click += (_, _) => SetDocked(!_docked);
        menu.Items.Add(dock);

        var settings = new MenuItem { Header = "Настройки…" };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        menu.Items.Add(new Separator());

        var hide = new MenuItem { Header = "Скрыть" };
        hide.Click += (_, _) => Hide();
        menu.Items.Add(hide);

        var exit = new MenuItem { Header = "Выход" };
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

        return menu;
    }

    private void PlaceWindow()
    {
        var work = SystemParameters.WorkArea;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        // По умолчанию — правый нижний угол, вплотную к панели задач.
        var left = _settings.Left ?? (work.Right - Width - 16);
        var top = _settings.Top ?? (work.Bottom - Height - 8);

        left = Math.Clamp(left, virtualLeft, Math.Max(virtualLeft, virtualRight - Width));
        top = Math.Clamp(top, virtualTop, Math.Max(virtualTop, virtualBottom - Height));

        Left = left;
        Top = top;
    }

    // ------------------------------------------------------------- трей

    private void BuildTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Мини-плеер",
            Visible = true,
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Показать", null, (_, _) => ShowWidget());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettings());
        menu.Items.Add("Встроить в панель задач", null, (_, _) => SetDocked(!_docked));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Close());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWidget();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon is not null) return icon;
            }
        }
        catch
        {
            // иконка — украшение, не повод падать
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void ShowWidget()
    {
        Show();
        if (_docked) return;

        Activate();
        Topmost = false;
        Topmost = true;
    }

    private void SaveSettings()
    {
        // Пока окно внутри панели задач, Left/Top — координаты относительно панели,
        // сохранять их как позицию виджета нельзя.
        if (!_docked)
        {
            _settings.Left = Left;
            _settings.Top = Top;
        }

        _settings.AppId = _media.SelectedAppId;
        _settings.Docked = _docked;
        _settings.Save();
    }
}
