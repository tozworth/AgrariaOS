param(
    [switch]$Install,
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
$taskBuildArgs = @('build', (Join-Path $PSScriptRoot 'AgrariaOS.csproj'), '-c', 'Release')
if ($GamePath) { $taskBuildArgs += "-p:GamePath=$GamePath" }
if ($Install) { $taskBuildArgs += '-p:EnableModDeploy=true' }
& dotnet @taskBuildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
