# Можно ли через UI Automation узнать, где на панели задач заканчиваются значки приложений.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, "Shell_TrayWnd")
$tray = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if ($null -eq $tray) { "Shell_TrayWnd не найден через UIA"; exit 1 }

"tray: $($tray.Current.Name) rect=$($tray.Current.BoundingRectangle)"

$all = $tray.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
"descendants: $($all.Count)"

foreach ($el in $all) {
    $r = $el.Current.BoundingRectangle
    if ($r.Width -gt 0 -and $r.Height -gt 0) {
        "{0,-32} {1,-22} x={2,6:0} y={3,5:0} w={4,5:0} h={5,3:0}" -f $el.Current.ControlType.ProgrammaticName, $el.Current.Name, $r.X, $r.Y, $r.Width, $r.Height
    }
}
