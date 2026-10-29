using System.Windows.Automation;

namespace TrayTune;

/// <summary>
/// Где на панели задач заканчиваются кнопки приложений.
/// Через UI Automation, потому что в Windows 11 значки рисует XAML и HWND на каждый нет.
/// </summary>
internal static class TaskbarLayout
{
    public readonly record struct ButtonBlock(int Left, int Right);

    public static ButtonBlock? GetButtonBlock(IntPtr tray)
    {
        try
        {
            var trayElement = AutomationElement.FromHandle(tray);
            if (trayElement is null) return null;

            var buttons = trayElement.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            if (buttons.Count == 0) return null;

            if (!Native.GetWindowRect(tray, out var trayRect)) return null;

            // Правую границу «своих» кнопок ограничиваем блоком трея.
            var notifyLeft = trayRect.Right;
            var notify = Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify != IntPtr.Zero && Native.GetWindowRect(notify, out var notifyRect) && notifyRect.Width > 40)
            {
                notifyLeft = notifyRect.Left;
            }

            var myPid = Environment.ProcessId;
            var left = int.MaxValue;
            var right = int.MinValue;

            foreach (AutomationElement button in buttons)
            {
                // Своё же окно не считаем: мы тоже дети панели задач.
                if (button.Current.ProcessId == myPid) continue;

                var r = button.Current.BoundingRectangle;
                if (r.Width < 4 || r.Height < 4) continue;
                if (r.Bottom <= trayRect.Top || r.Top >= trayRect.Bottom) continue;
                if (r.Left >= notifyLeft - 4) continue;   // это уже значки трея

                left = Math.Min(left, (int)Math.Round(r.Left));
                right = Math.Max(right, (int)Math.Round(r.Right));
            }

            return left == int.MaxValue ? null : new ButtonBlock(left, right);
        }
        catch
        {
            return null;
        }
    }
}
