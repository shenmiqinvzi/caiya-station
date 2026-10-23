param(
    [string]$GamePath = 'E:\Steam\steamapps\common\Escape from Duckov'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget\packages'

$project = Join-Path $projectRoot 'tests\CaiYaStation.Domain.Tests\CaiYaStation.Domain.Tests.csproj'
Write-Host 'Running CaiYaStation domain tests...'
& dotnet run --project $project --configuration Release --nologo "-p:DuckovPath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Domain tests failed.' }
Write-Host 'Domain tests passed.'

$panel = Join-Path $projectRoot 'tools\PanelPreview\PanelPreview.csproj'
Write-Host 'Checking shipment panel layout (headless)...'
& dotnet run --project $panel --configuration Release --nologo "-p:DuckovPath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Panel layout check failed.' }
Write-Host 'Panel layout check passed.'
