# Repair files mangled by encoding round-trip: text was read as cp1251 and written as UTF-8.
# ASCII-only on purpose: PowerShell 5.1 reads .ps1 as ANSI, Cyrillic in scripts breaks.
param(
    [Parameter(Mandatory = $true)][string[]]$Files
)

$utf8 = New-Object System.Text.UTF8Encoding($false)
$cp1251 = [System.Text.Encoding]::GetEncoding(1251)

foreach ($file in $Files) {
    if (-not (Test-Path $file)) { "missing: $file"; continue }

    $bytes = [System.IO.File]::ReadAllBytes($file)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)

    if ($text -notmatch "[\u0400-\u04FF]") {
        "no cyrillic, skip: $file"
        continue
    }

    # Heuristic: mojibake keeps only chars representable in cp1251; real text round-trips cleanly.
    $recovered = [System.Text.Encoding]::UTF8.GetString($cp1251.GetBytes($text))
    if ($recovered -match "[\u0400-\u04FF]") {
        [System.IO.File]::WriteAllText($file, $recovered, $utf8)
        "recovered: $file"
    }
    else {
        "cannot recover: $file"
    }
}
