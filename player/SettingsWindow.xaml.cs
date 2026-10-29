using System.Windows;
using System.Windows.Input;

namespace TaskbarPlayer;

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

        ShowProgressCheck.IsChecked = _draft.ShowProgress;
        DockedCheck.IsChecked = _draft.Docked;
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Placement = _draft.Placement;
        _settings.Background = _draft.Background;
        _settings.ShowProgress = ShowProgressCheck.IsChecked == true;
        _settings.Docked = _draft.Docked;
        _settings.Autostart = AutostartCheck.IsChecked == true;
        _settings.Save();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
