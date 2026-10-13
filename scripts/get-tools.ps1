<#
.SYNOPSIS
    Descarga yt-dlp, FFmpeg y Deno en src\JakuDownloader\tools.

.DESCRIPTION
    Baja versiones fijas desde los releases oficiales de GitHub, verifica el
    SHA-256 de cada archivo y deja solo lo necesario en la carpeta tools.
    Si una herramienta ya esta en la version indicada, la saltea.

.PARAMETER Force
    Vuelve a descargar todo aunque ya este presente.

.EXAMPLE
    .\scripts\get-tools.ps1
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
# En Windows PowerShell 5.1 la barra de progreso hace muy lento a Invoke-WebRequest
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Versiones fijas. Para actualizar: cambiar aca y correr de nuevo.
$YtDlpVersion = '2026.08.19'
$DenoVersion = 'v2.9.7'
# BtbN no mantiene builds viejas: se fija la rama estable, que solo recibe parches
$FfmpegBranch = '9.0'

$ToolsDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\JakuDownloader\tools'))
$VersionsFile = Join-Path $ToolsDir 'versions.txt'

function Get-InstalledVersions {
    $versions = @{}
    if (Test-Path $VersionsFile) {
        foreach ($line in Get-Content $VersionsFile) {
            $parts = $line -split '=', 2
            if ($parts.Count -eq 2) { $versions[$parts[0].Trim()] = $parts[1].Trim() }
        }
    }
    return $versions
}

function Save-InstalledVersions($versions) {
    $versions.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" } |
        Set-Content -Path $VersionsFile -Encoding ascii
}

function Test-UpToDate($name, $version, $files) {
    if ($Force) { return $false }
    if ($installed[$name] -ne $version) { return $false }
    foreach ($file in $files) {
        if (-not (Test-Path (Join-Path $ToolsDir $file))) { return $false }
    }
    return $true
}

function Get-RemoteFile($url, $destination) {
    Write-Host "  Bajando $url"
    Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
}

# Busca el hash del archivo en un listado tipo "hash  nombre" o en un archivo con un solo hash
function Get-ExpectedHash($sumsUrl, $fileName) {
    $content = (Invoke-WebRequest -Uri $sumsUrl -UseBasicParsing).Content
    if ($content -is [byte[]]) { $content = [Text.Encoding]::UTF8.GetString($content) }

    $lines = $content -split "`r?`n" | Where-Object { $_ -match '[0-9a-fA-F]{64}' }
    $match = $lines | Where-Object { $_ -match "[\s\*]$([regex]::Escape($fileName))\s*$" } | Select-Object -First 1
    if (-not $match -and @($lines).Count -eq 1) { $match = $lines }
    if (-not $match) { throw "No se encontro el hash de $fileName en $sumsUrl" }

    return ([regex]::Match($match, '[0-9a-fA-F]{64}')).Value.ToLowerInvariant()
}

function Assert-Hash($path, $expected) {
    $actual = (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Hash incorrecto en $(Split-Path $path -Leaf). Esperado $expected, obtenido $actual"
    }
    Write-Host "  SHA-256 verificado"
}

New-Item -ItemType Directory -Force $ToolsDir | Out-Null
$installed = Get-InstalledVersions
$tempDir = Join-Path ([IO.Path]::GetTempPath()) ("jaku-tools-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tempDir | Out-Null

try {
    # yt-dlp
    Write-Host "yt-dlp $YtDlpVersion"
    if (Test-UpToDate 'yt-dlp' $YtDlpVersion @('yt-dlp.exe')) {
        Write-Host "  Ya esta actualizado"
    }
    else {
        $base = "https://github.com/yt-dlp/yt-dlp/releases/download/$YtDlpVersion"
        $file = Join-Path $tempDir 'yt-dlp.exe'
        Get-RemoteFile "$base/yt-dlp.exe" $file
        Assert-Hash $file (Get-ExpectedHash "$base/SHA2-256SUMS" 'yt-dlp.exe')
        Copy-Item $file $ToolsDir -Force
        $installed['yt-dlp'] = $YtDlpVersion
    }

    # Deno
    Write-Host "Deno $DenoVersion"
    if (Test-UpToDate 'deno' $DenoVersion @('deno.exe')) {
        Write-Host "  Ya esta actualizado"
    }
    else {
        $asset = 'deno-x86_64-pc-windows-msvc.zip'
        $base = "https://github.com/denoland/deno/releases/download/$DenoVersion"
        $zip = Join-Path $tempDir $asset
        Get-RemoteFile "$base/$asset" $zip
        Assert-Hash $zip (Get-ExpectedHash "$base/$asset.sha256sum" $asset)
        $extractDir = Join-Path $tempDir 'deno'
        Expand-Archive -Path $zip -DestinationPath $extractDir -Force
        Copy-Item (Join-Path $extractDir 'deno.exe') $ToolsDir -Force
        $installed['deno'] = $DenoVersion
    }

    # FFmpeg (build LGPL compartida de BtbN: exe chicos + DLLs comunes)
    Write-Host "FFmpeg $FfmpegBranch"
    $ffmpegVersion = "n$FfmpegBranch-latest-lgpl-shared"
    if (Test-UpToDate 'ffmpeg' $ffmpegVersion @('ffmpeg.exe', 'ffprobe.exe')) {
        Write-Host "  Ya esta actualizado"
    }
    else {
        $asset = "ffmpeg-n$FfmpegBranch-latest-win64-lgpl-shared-$FfmpegBranch.zip"
        $base = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest'
        $zip = Join-Path $tempDir $asset
        Get-RemoteFile "$base/$asset" $zip
        Assert-Hash $zip (Get-ExpectedHash "$base/checksums.sha256" $asset)
        $extractDir = Join-Path $tempDir 'ffmpeg'
        Expand-Archive -Path $zip -DestinationPath $extractDir -Force
        $root = Get-ChildItem $extractDir -Directory | Select-Object -First 1
        $bin = Join-Path $root.FullName 'bin'

        Get-ChildItem $ToolsDir -Filter '*.dll' | Remove-Item -Force
        Copy-Item (Join-Path $bin 'ffmpeg.exe'), (Join-Path $bin 'ffprobe.exe') $ToolsDir -Force
        Copy-Item (Join-Path $bin '*.dll') $ToolsDir -Force
        # La LGPL pide distribuir la licencia junto a los binarios
        Copy-Item (Join-Path $root.FullName 'LICENSE.txt') (Join-Path $ToolsDir 'FFMPEG-LICENSE.txt') -Force
        $installed['ffmpeg'] = $ffmpegVersion
    }

    Save-InstalledVersions $installed
    Write-Host ""
    Write-Host "Listo. Herramientas en $ToolsDir"
}
finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
