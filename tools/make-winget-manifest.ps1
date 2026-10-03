# Generates winget manifests for a published release, ready to submit to microsoft/winget-pkgs.
# Usage:  powershell -ExecutionPolicy Bypass -File tools\make-winget-manifest.ps1 -Version 1.1.0
#
# Then submit them, for example with Microsoft's wingetcreate tool:
#   wingetcreate submit packaging\winget\1.1.0
# or by opening a pull request that adds the folder to
#   manifests/t/Tjs4002/SerialScope/<version>/ in https://github.com/microsoft/winget-pkgs

param([Parameter(Mandatory = $true)][string]$Version)

$ErrorActionPreference = 'Stop'
$id = 'Tjs4002.SerialScope'
$url = "https://github.com/Tjs4002/SerialScope/releases/download/v$Version/SerialScope.exe"
$out = Join-Path $PSScriptRoot "..\packaging\winget\$Version"
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host "Downloading $url"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$tmp = Join-Path $env:TEMP "SerialScope-$Version.exe"
Invoke-WebRequest $url -OutFile $tmp -UseBasicParsing
$hash = (Get-FileHash $tmp -Algorithm SHA256).Hash.ToUpper()
Remove-Item $tmp

$schema = '1.6.0'

# UTF-8 without a byte-order mark, as winget-pkgs expects
function Write-Plain([string]$Path, [Parameter(ValueFromPipeline = $true)][string]$Text) {
    process { [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n") + "`n", (New-Object Text.UTF8Encoding $false)) }
}

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schema
"@ | Write-Plain (Join-Path $out "$id.yaml")

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
InstallerType: portable
Commands:
- serialscope
Installers:
- Architecture: neutral
  InstallerUrl: $url
  InstallerSha256: $hash
ManifestType: installer
ManifestVersion: $schema
"@ | Write-Plain (Join-Path $out "$id.installer.yaml")

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: Tjs4002
PublisherUrl: https://github.com/Tjs4002
PackageName: SerialScope
PackageUrl: https://github.com/Tjs4002/SerialScope
License: MIT
LicenseUrl: https://github.com/Tjs4002/SerialScope/blob/main/LICENSE
ShortDescription: A clean, lightweight serial monitor and plotter for Windows.
Description: SerialScope shows what your ESP32, Arduino or other serial device is saying, as text or as live graphs. Highlighting, search, hex view, CSV recording and auto-reconnect, in a single small exe.
Tags:
- serial
- serial-monitor
- serial-plotter
- arduino
- esp32
- terminal
ReleaseNotesUrl: https://github.com/Tjs4002/SerialScope/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $schema
"@ | Write-Plain (Join-Path $out "$id.locale.en-US.yaml")

Write-Host "Wrote winget manifests to $((Resolve-Path $out).Path)  (sha256 $hash)"
