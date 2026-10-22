param(
    [string]$GamePath = 'E:\Steam\steamapps\common\Escape from Duckov',
    [string]$PackagePath = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$modName = 'CaiYaStation'
$package = if ($PackagePath) { [System.IO.Path]::GetFullPath($PackagePath) } else { Join-Path $projectRoot 'artifacts\package\CaiYaStation' }
if (-not (Test-Path -LiteralPath $package)) { throw "Package not found: $package. Run scripts\Build.ps1 first." }

$game = [System.IO.Path]::GetFullPath($GamePath)
$mods = [System.IO.Path]::GetFullPath((Join-Path $game 'Duckov_Data\Mods'))
$destination = [System.IO.Path]::GetFullPath((Join-Path $mods $modName))
if (-not $destination.StartsWith($mods, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install outside the game Mods directory: $destination"
}
if (-not (Test-Path -LiteralPath (Join-Path $game 'Duckov.exe'))) {
    throw "Duckov.exe not found under $game"
}

$running = Get-Process -Name Duckov -ErrorAction SilentlyContinue
if ($running) { throw 'Duckov is running. Fully exit the game before installing.' }

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backup = Join-Path $projectRoot "artifacts\backups\before-install-$timestamp"
if (Test-Path -LiteralPath $destination) {
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination $backup -Recurse -Force
    Write-Host "Backed up existing mod to $backup"
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$sourceFiles = @(Get-ChildItem -LiteralPath $package -File)
foreach ($file in $sourceFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name) -Force
}
# Remove stale files from an older version, but never touch logs.
$expected = @($sourceFiles.Name)
foreach ($file in Get-ChildItem -LiteralPath $destination -File) {
    if ($file.Name -notin $expected) {
        Remove-Item -LiteralPath $file.FullName -Force
        Write-Host "Removed stale file: $($file.Name)"
    }
}

$records = foreach ($file in $sourceFiles | Sort-Object Name) {
    $installed = Join-Path $destination $file.Name
    if (-not (Test-Path -LiteralPath $installed)) { throw "Installed file missing: $($file.Name)" }
    $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash
    if ($sourceHash -ne $installedHash) { throw "SHA256 mismatch after install: $($file.Name)" }
    [pscustomobject]@{
        name = $file.Name
        bytes = (Get-Item -LiteralPath $installed).Length
        sha256 = $installedHash
    }
}

$infoVersion = ((Get-Content -LiteralPath (Join-Path $destination 'info.ini') -Encoding UTF8 |
    Where-Object { $_ -match '^version=' }) -split '=', 2)[1].Trim()
$verification = Join-Path $projectRoot 'artifacts\verification'
New-Item -ItemType Directory -Path $verification -Force | Out-Null
$report = [pscustomobject]@{
    mod = $modName
    version = $infoVersion
    installedAt = (Get-Date).ToString('o')
    gamePath = $game
    destination = $destination
    backup = if (Test-Path -LiteralPath $backup) { $backup } else { $null }
    files = $records
}
$reportPath = Join-Path $verification "install-$infoVersion.json"
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Installed $modName $infoVersion to $destination"
Write-Host "Verified $($records.Count) files by SHA256."
Write-Host "Report: $reportPath"
Write-Host 'Fully restart the game, enable 菜鸭驿站 in the mod menu, then enter 仓库区 or 农场镇.'
