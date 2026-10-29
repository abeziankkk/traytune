# Rename TrayTune -> TrayTune across text files.
# ASCII-only script: PowerShell 5.1 reads .ps1 as ANSI, Cyrillic breaks it.
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$From = "TrayTune",
    [string]$To = "TrayTune"
)

$utf8 = New-Object System.Text.UTF8Encoding($false)

$files = Get-ChildItem -Path $Root -Recurse -File -Include *.cs, *.xaml, *.csproj, *.md, *.yml, *.cmd, *.ps1 |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|dist|dist-portable|\.dotnet|\.nuget|\.git)\\' }

foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    if (-not $text.Contains($From)) { continue }

    $updated = $text.Replace($From, $To)
    [System.IO.File]::WriteAllText($file.FullName, $updated, $utf8)
    "updated: $($file.FullName.Substring($Root.Length + 1))"
}
