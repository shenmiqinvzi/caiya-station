param(
    [string]$GamePath = 'E:\Steam\steamapps\common\Escape from Duckov'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget\packages'

$project = Join-Path $projectRoot 'src\CaiYaStation\CaiYaStation.csproj'
& dotnet build $project --configuration Release --nologo --verbosity minimal "-p:DuckovPath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }

$projectVersion = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$infoVersion = ((Get-Content -LiteralPath (Join-Path $projectRoot 'mod\info.ini') -Encoding UTF8 |
    Where-Object { $_ -match '^version=' }) -split '=', 2)[1].Trim()
if ($projectVersion -ne $infoVersion) { throw "Project version $projectVersion does not match info.ini $infoVersion." }

$buildOutput = Join-Path $projectRoot 'src\CaiYaStation\bin\Release\netstandard2.1'
$packageParent = Join-Path $projectRoot 'artifacts\package'
$package = Join-Path $packageParent 'CaiYaStation'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
New-Item -ItemType Directory -Path $package -Force | Out-Null

$files = @(
    @{ Source = (Join-Path $buildOutput 'CaiYaStation.dll'); Name = 'CaiYaStation.dll' },
    @{ Source = (Join-Path $buildOutput 'CaiYaStation.pdb'); Name = 'CaiYaStation.pdb' },
    @{ Source = (Join-Path $projectRoot 'mod\info.ini'); Name = 'info.ini' },
    @{ Source = (Join-Path $projectRoot 'mod\preview.png'); Name = 'preview.png' },
    @{ Source = (Join-Path $projectRoot 'mod\config.json'); Name = 'config.json' },
    @{ Source = (Join-Path $projectRoot 'mod\station-layout.json'); Name = 'station-layout.json' },
    @{ Source = (Join-Path $projectRoot 'mod\README.txt'); Name = 'README.txt' },
    @{ Source = (Join-Path $projectRoot 'vendor\Harmony\0Harmony.dll'); Name = '0Harmony.dll' },
    @{ Source = (Join-Path $projectRoot 'vendor\Harmony\LICENSE.txt'); Name = 'LICENSE-Harmony.txt' },
    @{ Source = (Join-Path $projectRoot 'vendor\Harmony\PROVENANCE.md'); Name = 'THIRD-PARTY.md' }
)
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file.Source)) { throw "Missing build input: $($file.Source)" }
    Copy-Item -LiteralPath $file.Source -Destination (Join-Path $package $file.Name) -Force
}

$unexpected = @(Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -notin $files.Name })
if ($unexpected.Count -gt 0) { throw 'Package contains unexpected files.' }
$dlls = @(Get-ChildItem -LiteralPath $package -Filter '*.dll' -File)
$allowedDlls = @('CaiYaStation.dll', '0Harmony.dll')
if ($dlls.Count -ne 2 -or @($dlls | Where-Object { $_.Name -notin $allowedDlls }).Count -ne 0) {
    throw 'Package must contain only CaiYaStation.dll and the vendored 0Harmony.dll; game libraries must not be redistributed.'
}

$archive = Join-Path $projectRoot ("artifacts\CaiYaStation-$projectVersion.zip")
Compress-Archive -LiteralPath $package -DestinationPath $archive -Force

$records = foreach ($file in Get-ChildItem -LiteralPath $package -File | Sort-Object Name) {
    [pscustomobject]@{
        name = $file.Name
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}
$verification = Join-Path $projectRoot "artifacts\verification"
New-Item -ItemType Directory -Path $verification -Force | Out-Null
$report = [pscustomobject]@{
    version = $projectVersion
    generatedAt = (Get-Date).ToString('o')
    gamePath = $GamePath
    package = $package
    archive = $archive
    archiveSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    files = $records
}
$reportPath = Join-Path $verification "release-$projectVersion.json"
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Package: $package"
Write-Host "Archive: $archive"
Write-Host "SHA256: $($report.archiveSha256)"
Write-Host "Report: $reportPath"
