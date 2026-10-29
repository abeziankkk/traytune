using System.Windows;
using System.Windows.Input;

namespace TrayTune;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly Settings _draft;
    private bool _loading;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();

        _settings = settings;
        _draft = settings.Clone();

        _loading = true;

        PlacementLeft.IsChecked = _draft.Placement == TaskbarPlacement.Left;
        PlacementCenter.IsChecked = _draft.Placement == TaskbarPlacement.Center;
        PlacementRight.IsChecked = _draft.Placement == TaskbarPlacement.Right;

        BgTransparent.IsChecked = _draft.Background == WidgetBackground.Transparent;
        BgTranslucent.IsChecked = _draft.Background == WidgetBackground.Translucent;
        BgSolid.IsChecked = _draft.Background == WidgetBackground.Solid;

        AccentAuto.IsChecked = _draft.Accent == AccentMode.Auto;
        AccentSpotify.IsChecked = _draft.Accent == AccentMode.Spotify;
        AccentYandex.IsChecked = _draft.Accent == AccentMode.Yandex;
        AccentWave.IsChecked = _draft.Accent == AccentMode.Wave;
        AccentOther.IsChecked = _draft.Accent == AccentMode.Other;

        WidthNarrow.IsChecked = _draft.WidgetWidth <= 300;
        WidthNormal.IsChecked = _draft.WidgetWidth > 300 && _draft.WidgetWidth < 380;
        WidthWide.IsChecked = _draft.WidgetWidth >= 380;

        ShowProgressCheck.IsChecked = _draft.ShowProgress;
        DockedCheck.IsChecked = !_draft.Floating;
        AutostartCheck.IsChecked = _draft.Autostart;

        _loading = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void Placement_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _draft.Placement = PlacementLeft.IsChecked == true ? TaskbarPlacement.Left
            : PlacementCenter.IsChecked == true ? TaskbarPlacement.Center
            : TaskbarPlacement.Right;
    }

    private void Background_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _draft.Background = BgTransparent.IsChecked == true ? WidgetBackground.Transparent
            : BgTranslucent.IsChecked == true ? WidgetBackground.Translucent
            : WidgetBackground.Solid;
    }

    private void Accent_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _draft.Accent = AccentSpotify.IsChecked == true ? AccentMode.Spotify
            : AccentYandex.IsChecked == true ? AccentMode.Yandex
            : AccentWave.IsChecked == true ? AccentMode.Wave
            : AccentOther.IsChecked == true ? AccentMode.Other
            : AccentMode.Auto;
    }

    private void Width_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _draft.WidgetWidth = WidthNarrow.IsChecked == true ? 280
            : WidthWide.IsChecked == true ? 400
            : 340;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Placement = _draft.Placement;
        _settings.Background = _draft.Background;
        _settings.Accent = _draft.Accent;
        _settings.WidgetWidth = _draft.WidgetWidth;
        _settings.ShowProgress = ShowProgressCheck.IsChecked == true;
        _settings.Floating = DockedCheck.IsChecked != true;
        _settings.Autostart = AutostartCheck.IsChecked == true;
        _settings.Save();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

