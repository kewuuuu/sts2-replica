# Builds the mod and packs a release zip: dist/sts2-replica-<version>.zip
# The zip holds a single top-level folder 'sts2-replica/', so it extracts straight into the game's mods/.
param(
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root 'sts2-replica.csproj'
$manifestPath = Join-Path $root 'mod\sts2-replica.json'
$dist = Join-Path $root 'dist\sts2-replica'

if (-not $NoBuild) {
    dotnet build $csproj -c $Configuration -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'build failed'
    }
}

# Read as UTF-8 explicitly: Windows PowerShell 5.1 decodes a BOM-less file with the ANSI
# codepage, which mangles the Chinese description and makes the JSON unparsable.
$manifestText = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$manifest = $manifestText | ConvertFrom-Json
$modId = $manifest.id
$version = $manifest.version
if (-not $version) {
    throw "manifest has no version: $manifestPath"
}

$required = @("$modId.dll", "$modId.json")
foreach ($name in $required) {
    $path = Join-Path $dist $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "missing build output: $path"
    }
}

$zip = Join-Path $root ("dist\" + $modId + "-" + $version + ".zip")
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

$stage = Join-Path $env:TEMP ("sts2-replica-pack-" + [guid]::NewGuid().ToString('N'))
$packDir = Join-Path $stage $modId
New-Item -ItemType Directory -Path $packDir -Force | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $dist -File) {
    Copy-Item -LiteralPath $file.FullName -Destination $packDir -Force
}

try {
    # Compress-Archive in Windows PowerShell 5.1 writes entry names with backslashes, which the zip
    # spec forbids (unzip warns about it, and some tools treat the whole name as a bare filename).
    # Build the entries by hand so the separator stays a forward slash.
    # Windows PowerShell 5.1 needs both assemblies: ZipFile lives in ...FileSystem, ZipArchiveMode in
    # the base System.IO.Compression.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $packDir -File) {
            $entryName = $modId + '/' + $file.Name
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ("packed " + $zip)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($entry in $archive.Entries) {
        Write-Host ("  " + $entry.FullName + "  " + $entry.Length + " bytes")
    }
}
finally {
    $archive.Dispose()
}
