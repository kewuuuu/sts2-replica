# Prints the [sts2-replica] lines of the current game log, newest last.
# Exit code 1 means the mod never logged anything: it did not load, or its initializer did not run.
param(
    [string]$LogPath = (Join-Path $env:AppData 'SlayTheSpire2\logs\godot.log'),
    [int]$Tail = 200
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $LogPath)) {
    throw "log not found: $LogPath (has the game been started once?)"
}

$lines = @(Select-String -LiteralPath $LogPath -Pattern '\[sts2-replica\]' | ForEach-Object { $_.Line })
if ($lines.Count -eq 0) {
    Write-Host "no [sts2-replica] lines in $LogPath"
    Write-Host '=> the mod did not load, or its initializer never ran.'
    exit 1
}

Write-Host ("{0} line(s) in {1}" -f $lines.Count, $LogPath)
$lines | Select-Object -Last $Tail
