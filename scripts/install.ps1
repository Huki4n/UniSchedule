param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('install', 'uninstall')]
    [string]$Action,

    [string]$Source
)

$ErrorActionPreference = 'Stop'

$appDir = Join-Path $env:LOCALAPPDATA 'UniSchedule\app'
$destExe = Join-Path $appDir 'UniSchedule.exe'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Расписание.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValue = 'UniSchedule'

function Get-NormalizedPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    $trimmed = $Path.Trim().Trim('"')
    try {
        return [IO.Path]::GetFullPath($trimmed)
    }
    catch {
        return $null
    }
}

function Test-InstalledRunning {
    if (-not (Test-Path -LiteralPath $destExe)) {
        return $false
    }

    $target = Get-NormalizedPath $destExe
    $procs = @(Get-CimInstance Win32_Process -Filter "Name = 'UniSchedule.exe'")
    foreach ($proc in $procs) {
        $path = Get-NormalizedPath $proc.ExecutablePath
        if ($path -and $target -and ($path -eq $target)) {
            return $true
        }
    }

    return $false
}

function Remove-AutostartIfInstalled {
    $property = Get-ItemProperty -Path $runKey -Name $runValue -ErrorAction SilentlyContinue
    if (-not $property) {
        return
    }

    $stored = Get-NormalizedPath $property.$runValue
    $target = Get-NormalizedPath $destExe
    if ($stored -and $target -and ($stored -eq $target)) {
        Remove-ItemProperty -Path $runKey -Name $runValue
    }
}

if ($Action -eq 'uninstall') {
    if (Test-InstalledRunning) {
        Write-Host 'Закройте установленное приложение: выход из меню трея. Затем повторите make uninstall.'
        exit 1
    }

    Remove-AutostartIfInstalled
    if (Test-Path -LiteralPath $shortcut) {
        Remove-Item -LiteralPath $shortcut -Force
    }

    if (Test-Path -LiteralPath $appDir) {
        Remove-Item -LiteralPath $appDir -Recurse -Force
    }

    Write-Host "Удалено: $appDir"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Source)) {
    Write-Host 'Сначала выполните make publish.'
    exit 1
}

$sourceExe = Join-Path $Source 'UniSchedule.exe'
if (-not (Test-Path -LiteralPath $sourceExe)) {
    Write-Host 'Сначала выполните make publish.'
    exit 1
}

if (Test-InstalledRunning) {
    Write-Host 'Закройте установленное приложение: выход из меню трея. Затем повторите make install.'
    exit 1
}

New-Item -ItemType Directory -Force -Path $appDir | Out-Null
& robocopy $Source $appDir /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) {
    Write-Host "Не удалось скопировать файлы, код $LASTEXITCODE."
    exit 1
}

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $destExe
$link.WorkingDirectory = $appDir
$link.Description = 'UniSchedule'
$link.Save()

Write-Host "Установлено: $destExe"
Write-Host "Ярлык: $shortcut"
exit 0
