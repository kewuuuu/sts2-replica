<#
    Copies dist\sts2-replica\ into the game's mods folder.

    This script never runs on its own: you must invoke it yourself, and it asks for
    confirmation before copying. Add -Confirm:$false when running unattended.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$ModsDir = 'D:\software\steam\steamapps\common\Slay the Spire 2\mods',
    [string]$ModId = 'sts2-replica'
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
$files | ForEach-Object { Write-Host ("  " + $_.Name) }

if ($PSCmdlet.ShouldProcess($target, "Copy $($files.Count) file(s)")) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath $dist\* -Destination $target -Recurse -Force
    Write-Host "Installed. Remove the folder to uninstall: $target"
}
