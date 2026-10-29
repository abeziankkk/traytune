using System.Windows.Automation;

// Компилируется ли клиент UI Automation в .NET 10 (нужен для поиска кнопок панели задач).
var root = AutomationElement.RootElement;
var cond = new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd");
var tray = root.FindFirst(TreeScope.Children, cond);

if (tray is null)
{
    Console.WriteLine("Shell_TrayWnd через UIA не найден");
    return 1;
}

Console.WriteLine($"tray rect = {tray.Current.BoundingRectangle}");

var buttons = tray.FindAll(TreeScope.Descendants,
    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

double minLeft = double.MaxValue, maxRight = double.MinValue;

foreach (AutomationElement b in buttons)
{
    var r = b.Current.BoundingRectangle;
    if (r.Width <= 0 || r.Height <= 0) continue;
    if (r.Left < minLeft) minLeft = r.Left;
    if (r.Right > maxRight) maxRight = r.Right;
    Console.WriteLine($"  {b.Current.Name,-40} {r.Left:0}..{r.Right:0}");
}

Console.WriteLine($"buttons: left={minLeft:0} right={maxRight:0}");
return 0;
