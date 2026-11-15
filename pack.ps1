# Packs the built mod into dist/ (dll + manifest), ready for mods/.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$modId = 'sts2-replica'

dotnet build (Join-Path $root "$modId.csproj") -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$out = Join-Path $root "dist\$modId"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item (Join-Path $root "bin\Release\net9.0\$modId.dll") $out -Force
Copy-Item (Join-Path $root "mod\$modId.json") $out -Force

Write-Host "packed:"
Get-ChildItem -LiteralPath $out -File | ForEach-Object { Write-Host ("  " + $_.Name + "  " + $_.Length + " bytes") }
