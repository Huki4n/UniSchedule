$ErrorActionPreference = 'Stop'

$candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Host 'Установите Inno Setup 6: winget install JRSoftware.InnoSetup'
    exit 1
}

$script = Join-Path $PSScriptRoot '..\installer\UniSchedule.iss'
& $iscc $script
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
