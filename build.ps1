param(
    [switch]$Install,
    [string]$GamePath,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$taskBuildArgs = @('build', (Join-Path $PSScriptRoot 'AgrariaOS.csproj'), '-c', 'Release')
if ($GamePath) { $taskBuildArgs += "-p:GamePath=$GamePath" }
if ($Install -or $Launch) { $taskBuildArgs += '-p:EnableModDeploy=true' }
& dotnet @taskBuildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

if ($Launch) {
    # Ask MSBuild for the same game path used by the mod build package.
    $taskPathArgs = @('msbuild', (Join-Path $PSScriptRoot 'AgrariaOS.csproj'), '-p:Configuration=Release', '-getProperty:GamePath')
    if ($GamePath) { $taskPathArgs += "-p:GamePath=$GamePath" }
    $taskResolvedGamePath = & dotnet @taskPathArgs
    if ($LASTEXITCODE -ne 0) { throw "Game path lookup failed with exit code $LASTEXITCODE." }
    $taskResolvedGamePath = ($taskResolvedGamePath -join [Environment]::NewLine).Trim()
    $taskSmapiPath = Join-Path $taskResolvedGamePath 'StardewModdingAPI.exe'
    if (!(Test-Path -LiteralPath $taskSmapiPath -PathType Leaf)) {
        throw "SMAPI launcher not found at '$taskSmapiPath'. Specify the game folder with -GamePath."
    }
    Start-Process -FilePath $taskSmapiPath -WorkingDirectory $taskResolvedGamePath
}
