param(
    [string]$GamePath = $env:GAME_PATH,
    [switch]$IncludeSmapi,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
if (!$GamePath) {
    $taskPath = & dotnet msbuild (Join-Path $PSScriptRoot 'AgrariaOS.csproj') -getProperty:GamePath
    if ($LASTEXITCODE -ne 0) { throw 'Game path lookup failed. Specify -GamePath.' }
    $GamePath = ($taskPath -join [Environment]::NewLine).Trim()
}
$taskGameDll = Join-Path $GamePath 'Stardew Valley.dll'
if (!(Test-Path -LiteralPath $taskGameDll -PathType Leaf)) {
    throw "Game assembly not found at '$taskGameDll'. Specify -GamePath or set GAME_PATH."
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$taskTools = Join-Path $PSScriptRoot '.artifacts/tools'
$taskIlspy = Join-Path $taskTools $(if ($IsWindows) { 'ilspycmd.exe' } else { 'ilspycmd' })
if (!(Test-Path -LiteralPath $taskIlspy -PathType Leaf)) {
    & dotnet tool install ilspycmd --tool-path $taskTools --allow-roll-forward
    if ($LASTEXITCODE -ne 0) { throw 'ILSpy installation failed.' }
}
$taskDecompilerVersion = (& $taskIlspy --version) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) { throw 'Could not read the ILSpy version.' }
$taskAssemblies = @(
    @{ File = 'Stardew Valley.dll'; Cache = 'stardew' },
    @{ File = 'StardewValley.GameData.dll'; Cache = 'game-data' }
)
if ($IncludeSmapi) { $taskAssemblies += @{ File = 'StardewModdingAPI.dll'; Cache = 'smapi' } }

foreach ($taskAssembly in $taskAssemblies) {
    $taskDll = Join-Path $GamePath $taskAssembly.File
    $taskHash = (Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash
    $taskCache = Join-Path $PSScriptRoot ".artifacts/decompiled/$($taskAssembly.Cache)"
    $taskMetadataPath = Join-Path $taskCache '.cache.json'
    if (!$Force -and (Test-Path -LiteralPath $taskMetadataPath -PathType Leaf)) {
        $taskMetadata = Get-Content -LiteralPath $taskMetadataPath -Raw | ConvertFrom-Json
        if ($taskMetadata.Sha256 -eq $taskHash) {
            Write-Host "$($taskAssembly.File): cache is current."
            continue
        }
    }

    # Promote only a successful, complete result; retain the previous cache on failure.
    $taskStaging = "$taskCache.pending-$([Guid]::NewGuid().ToString('N'))"
    Write-Host "Decompiling $($taskAssembly.File) into $taskCache ..."
    & $taskIlspy --disable-updatecheck --nested-directories -p -r $GamePath -o $taskStaging $taskDll
    if ($LASTEXITCODE -ne 0) {
        throw "Decompilation failed. The previous cache is unchanged; partial output is in '$taskStaging'."
    }
    if ((Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash -ne $taskHash) {
        throw 'The game assembly changed during decompilation. Run this command again.'
    }
    @{
        Assembly = $taskDll
        Sha256 = $taskHash
        FileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($taskDll).FileVersion
        Decompiler = $taskDecompilerVersion
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskStaging '.cache.json')
    if (Test-Path -LiteralPath $taskCache) { Remove-Item -LiteralPath $taskCache -Recurse -Force }
    Move-Item -LiteralPath $taskStaging -Destination $taskCache
    Write-Host "$($taskAssembly.File): cache refreshed."
}
