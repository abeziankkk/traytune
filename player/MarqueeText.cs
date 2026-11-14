using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TrayTune;

/// <summary>
/// Бегущая строка для длинных названий. Текст лежит в <see cref="Canvas"/>, поэтому
/// его ширина — это полная ширина строки, а не обрезанная до виджета. Когда строка
/// не влезает, две её копии едут влево с зазором и зацикливаются без рывка: вторая
/// копия встаёт ровно туда, где была первая.
///
/// Если строка помещается или прокрутка выключена в настройках — текст стоит.
/// </summary>
internal sealed class MarqueeText
{
    private const double Speed = 28;    // px в секунду
    private const double Gap = 48;      // зазор между копиями, px
    private const double Dwell = 1.2;   // секунд стоим перед началом движения

    private readonly FrameworkElement _viewport;
    private readonly TextBlock _first;
    private readonly TextBlock _second;
    private readonly TranslateTransform _shift;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastFrame;
    private double _offset;
    private double _dwellLeft = Dwell;
    private bool _scrolling;

    public MarqueeText(FrameworkElement viewport, TextBlock first, TextBlock second, TranslateTransform shift)
    {
        _viewport = viewport;
        _first = first;
        _second = second;
        _shift = shift;
        _lastFrame = _clock.Elapsed;
    }

    /// <summary>Вызывать на каждом кадре. dt берём из собственных часов.</summary>
    public void Update(bool enabled)
    {
        var now = _clock.Elapsed;
        var dt = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        if (dt < 0 || dt > 0.5) dt = 0;   // окно подвисло — не прыгаем

        if (!enabled)
        {
            Stop();
            return;
        }

        var textWidth = _first.ActualWidth;
        var viewWidth = _viewport.ActualWidth;
        if (textWidth <= 0 || viewWidth <= 0) return;

        if (textWidth <= viewWidth + 1)
        {
            Stop();
            return;
        }

        if (!_scrolling)
        {
            _scrolling = true;
            _offset = 0;
            _dwellLeft = Dwell;
            Canvas.SetLeft(_second, textWidth + Gap);
            _second.Visibility = Visibility.Visible;
        }
        else if (Math.Abs(Canvas.GetLeft(_second) - (textWidth + Gap)) > 0.5)
        {
            // Ширина строки изменилась (другой трек, другой шрифт) — переставляем копию.
            Canvas.SetLeft(_second, textWidth + Gap);
        }

        if (_dwellLeft > 0)
        {
            _dwellLeft -= dt;
        }
        else
        {
            _offset -= Speed * dt;
        }

        var travel = textWidth + Gap;
        if (_offset <= -travel)
        {
            _offset += travel;   // бесшовно: вторая копия уже стоит на месте первой
            _dwellLeft = Dwell;
        }

        _shift.X = _offset;
    }

    private void Stop()
    {
        if (!_scrolling) return;

        _scrolling = false;
        _offset = 0;
        _dwellLeft = Dwell;
        _shift.X = 0;
        _second.Visibility = Visibility.Collapsed;
    }
}
