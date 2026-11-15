<#
    Copies dist\sts2-replica\ into the game's mods folder.

    This script never runs on its own: it lists what it will copy and asks for a typed
    confirmation first. Pass -Yes to skip the prompt when running unattended.
#>
param(
    [string]$ModsDir = 'D:\software\steam\steamapps\common\Slay the Spire 2\mods',
    [string]$ModId = 'sts2-replica',
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$dist = Join-Path $PSScriptRoot "dist\$ModId"
if (-not (Test-Path -LiteralPath $dist)) {
    throw "Not built yet: $dist does not exist. Run 'dotnet build' first."
}

$files = @(Get-ChildItem -LiteralPath $dist -File)
if ($files.Count -eq 0) {
    throw "Nothing to install: $dist is empty."
}

$target = Join-Path $ModsDir $ModId

Write-Host "source : $dist"
Write-Host "target : $target"
$files | ForEach-Object { Write-Host ("  " + $_.Name + "  " + $_.Length + " bytes") }

if (-not $Yes) {
    $answer = Read-Host "Copy these files into the game's mods folder? [y/N]"
    if ($answer -notmatch '^(y|yes)$') {
        Write-Host 'Aborted, nothing was copied.'
        return
    }
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
# -LiteralPath takes the path verbatim, so the wildcard has to be expanded by hand:
# a literal '*' matches nothing and the copy silently becomes a no-op.
foreach ($file in $files) {
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
Write-Host "Installed. Remove the folder to uninstall: $target"
