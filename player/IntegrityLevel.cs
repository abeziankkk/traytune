using System.Runtime.InteropServices;

namespace TrayTune;

/// <summary>
/// Проверка уровня целостности процесса. Если приложение запущено с Low,
/// Windows запрещает SetParent в панель задач и не показывает значок в трее —
/// так бывает при запуске из песочниц и отладчиков.
/// </summary>
internal static class IntegrityLevel
{
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static bool IsLow()
    {
        var token = IntPtr.Zero;
        var buffer = IntPtr.Zero;

        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token)) return false;

            GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out var size);
            if (size <= 0) return false;

            buffer = Marshal.AllocHGlobal(size);
            if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _)) return false;

            // TOKEN_MANDATORY_LABEL = SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes; }
            var sid = Marshal.ReadIntPtr(buffer);
            var subAuthorities = Marshal.ReadByte(sid, 1);
            var last = Marshal.ReadInt32(sid, 8 + 4 * (subAuthorities - 1));

            return last is >= 0x1000 and < 0x2000;   // SECURITY_MANDATORY_LOW_RID
        }
        catch
        {
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }
}
