# Dump names of all tray overflow flyout items (to check whether TrayTune icon exists).
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)

foreach ($w in $windows) {
    $cls = $w.Current.ClassName
    if ($cls -notmatch "Overflow|TopLevelWindow|Tray") { continue }

    "WINDOW: $cls  name='$($w.Current.Name)'"
    $items = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($i in $items) {
        $n = $i.Current.Name
        if ([string]::IsNullOrWhiteSpace($n)) { continue }
        "    $($i.Current.ControlType.ProgrammaticName): $n"
    }
}
