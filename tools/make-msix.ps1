# Builds bin\SerialScope-<version>.msix for the Microsoft Store.
# The package is left unsigned: the Store signs it after certification.
# Needs the Windows 10/11 SDK (for makeappx.exe). Run build.bat first.
#
#   powershell -ExecutionPolicy Bypass -File tools\make-msix.ps1 [-DisplayName "Serial Scope"]

param([string]$DisplayName = "Serial Scope")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "bin\SerialScope.exe"
if (-not (Test-Path $exe)) { throw "bin\SerialScope.exe not found. Run build.bat first." }

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows SDK." }

# Store versions need four parts, and the last one must be 0
$appInfo = Get-Content (Join-Path $root "src\AppInfo.cs") -Raw
if ($appInfo -notmatch 'Version = "(\d+)\.(\d+)\.(\d+)"') { throw "Version not found in AppInfo.cs" }
$version = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

$stage = Join-Path $root "bin\msix"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage "Assets") | Out-Null
Copy-Item $exe $stage

$manifest = Get-Content (Join-Path $root "packaging\msix\AppxManifest.xml") -Raw
$manifest = $manifest.Replace("{VERSION}", $version).Replace("{DISPLAYNAME}", [Security.SecurityElement]::Escape($DisplayName))
[IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $manifest, (New-Object Text.UTF8Encoding $false))

# Logos, scaled down from the 256 px app icon
$icon = [Drawing.Image]::FromFile((Join-Path $root "docs\icon.png"))
function Save-Logo([string]$name, [int]$size) {
    $bmp = New-Object Drawing.Bitmap $size, $size
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = "HighQualityBicubic"
    $g.PixelOffsetMode = "HighQuality"
    $g.SmoothingMode = "HighQuality"
    $g.DrawImage($icon, 0, 0, $size, $size)
    $g.Dispose()
    $bmp.Save((Join-Path $stage "Assets\$name"), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Save-Logo "StoreLogo.png" 50
Save-Logo "StoreLogo.scale-200.png" 100
Save-Logo "Square150x150Logo.png" 150
Save-Logo "Square150x150Logo.scale-200.png" 300
Save-Logo "Square44x44Logo.png" 44
Save-Logo "Square44x44Logo.scale-200.png" 88
foreach ($s in 16, 24, 32, 48, 256) {
    Save-Logo "Square44x44Logo.targetsize-$s.png" $s
    Save-Logo "Square44x44Logo.targetsize-${s}_altform-unplated.png" $s
}
$icon.Dispose()

$out = Join-Path $root "bin\SerialScope-$version.msix"
& $makeappx.FullName pack /d $stage /p $out /o /h SHA256 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }
Write-Host "Built $out ($DisplayName, version $version)"
