param(
    [string]$GamePath = 'E:\Steam\steamapps\common\Escape from Duckov'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget\packages'

$mod = Join-Path $projectRoot 'src\CaiYaStation\CaiYaStation.csproj'
& dotnet build $mod --configuration Release --nologo --verbosity minimal "-p:DuckovPath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }

$probe = Join-Path $projectRoot 'tools\ContractProbe\ContractProbe.csproj'
& dotnet run --project $probe --configuration Release --nologo -- "$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Native contract probe failed.' }
Write-Host 'Native contract and config probe passed.'
