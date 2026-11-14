# Print integrity level of a process (default: explorer).
param([string]$Name = "explorer")

Add-Type -Namespace Il -Name Api -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
[DllImport("kernel32.dll", SetLastError=true)] public static extern bool CloseHandle(IntPtr h);
[DllImport("advapi32.dll", SetLastError=true)] public static extern bool OpenProcessToken(IntPtr proc, uint access, out IntPtr token);
[DllImport("advapi32.dll", SetLastError=true)] public static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr info, int len, out int ret);
'@

function Get-Integrity([int]$ProcessId) {
    $PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    $TOKEN_QUERY = 0x0008
    $TokenIntegrityLevel = 25

    $h = [Il.Api]::OpenProcess($PROCESS_QUERY_LIMITED_INFORMATION, $false, $ProcessId)
    if ($h -eq [IntPtr]::Zero) { return "open failed: $([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message)" }

    $token = [IntPtr]::Zero
    if (-not [Il.Api]::OpenProcessToken($h, $TOKEN_QUERY, [ref]$token)) { return "token failed" }

    $len = 0
    [void][Il.Api]::GetTokenInformation($token, $TokenIntegrityLevel, [IntPtr]::Zero, 0, [ref]$len)
    $buf = [Runtime.InteropServices.Marshal]::AllocHGlobal($len)
    if (-not [Il.Api]::GetTokenInformation($token, $TokenIntegrityLevel, $buf, $len, [ref]$len)) { return "info failed" }

    # TOKEN_MANDATORY_LABEL: SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes; }
    $sidPtr = [Runtime.InteropServices.Marshal]::ReadIntPtr($buf)
    $subAuthCount = [Runtime.InteropServices.Marshal]::ReadByte($sidPtr, 1)
    $lastSub = [Runtime.InteropServices.Marshal]::ReadInt32($sidPtr, 8 + (4 * ($subAuthCount - 1)))

    [void][Il.Api]::CloseHandle($token)
    [void][Il.Api]::CloseHandle($h)
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buf)

    switch ($lastSub) {
        0x0000 { "Untrusted" }
        0x1000 { "Low" }
        0x2000 { "Medium" }
        0x2100 { "Medium Plus" }
        0x3000 { "High" }
        0x4000 { "System" }
        default { "0x{0:X}" -f $lastSub }
    }
}

foreach ($p in Get-Process -Name $Name -ErrorAction SilentlyContinue) {
    "pid $($p.Id) $($p.ProcessName): $(Get-Integrity $p.Id)"
}
"this shell: $(Get-Integrity $PID)"
