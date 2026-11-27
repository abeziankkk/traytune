using System.Runtime.InteropServices;

namespace TrayTune;

/// <summary>
/// Громкость системного микшера. Основной путь — Core Audio
/// (<c>IAudioEndpointVolume</c>), запасной — эмуляция клавиш громкости, если COM
/// почему-то не отдал интерфейс.
/// </summary>
internal static class VolumeControl
{
    private const float StepSize = 0.02f;

    private static IAudioEndpointVolume? _volume;
    private static bool _tried;
    private static bool _fallbackKeys;

    public static bool Available
    {
        get
        {
            EnsureVolume();
            return _volume is not null || _fallbackKeys;
        }
    }

    /// <summary>Текущая громкость 0..1 (null, если узнать не удалось).</summary>
    public static float? Get()
    {
        EnsureVolume();

        if (_volume is not null)
        {
            try
            {
                _volume.GetMasterVolumeLevelScalar(out var level);
                return level;
            }
            catch
            {
                _volume = null;
            }
        }

        return null;
    }

    /// <summary>Сдвинуть громкость на один шаг вверх (+1) или вниз (-1).</summary>
    public static float? Step(int direction)
    {
        EnsureVolume();

        if (_volume is null && _fallbackKeys)
        {
            // Клавиши громкости: Windows сама показывает свой индикатор.
            var key = direction > 0 ? VkVolumeUp : VkVolumeDown;
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            keybd_event((byte)key, 0, KeyEventKeyUp, UIntPtr.Zero);
            return null;
        }

        if (_volume is null) return null;

        try
        {
            _volume.GetMasterVolumeLevelScalar(out var level);
            var next = Math.Clamp(level + StepSize * direction, 0f, 1f);
            _volume.SetMasterVolumeLevelScalar(next, Guid.Empty);
            return next;
        }
        catch
        {
            // Устройство могло смениться — в следующий раз попробуем заново.
            _volume = null;
            _tried = false;
            return null;
        }
    }

    private static void EnsureVolume()
    {
        if (_tried) return;
        _tried = true;

        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var endpoint);
            _volume = endpoint as IAudioEndpointVolume;

            if (_volume is null) _fallbackKeys = true;
        }
        catch
        {
            Log.Write("volume: Core Audio недоступен, работаю клавишами громкости");
            _fallbackKeys = true;
        }
    }

    // ------------------------------------------------------------ Win32

    private const int VkVolumeUp = 0xAF;
    private const int VkVolumeDown = 0xAE;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint ClsCtxAll = 0x17;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    private enum EDataFlow
    {
        Render = 0,
    }

    private enum ERole
    {
        Multimedia = 1,
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, Guid context);
        int SetMasterVolumeLevelScalar(float level, Guid context);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
    }
}
