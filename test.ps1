$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $PSScriptRoot 'tests\bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
& $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $bin 'CoreTests.exe')) (Join-Path $PSScriptRoot 'src\Settings.cs') (Join-Path $PSScriptRoot 'tests\CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Core test build failed' }
& (Join-Path $bin 'CoreTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
& $compiler /nologo /target:exe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:' + (Join-Path $bin 'PreviewProbe.exe')) (Join-Path $PSScriptRoot 'tests\PreviewProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Preview test build failed' }
& (Join-Path $bin 'PreviewProbe.exe') (Join-Path $PSScriptRoot 'dist\Noel.exe')
if ($LASTEXITCODE -ne 0) { throw 'DPI-unaware preview test failed' }
& (Join-Path $bin 'PreviewProbe.exe') (Join-Path $PSScriptRoot 'dist\Noel.exe') --dpi-aware
if ($LASTEXITCODE -ne 0) { throw 'DPI-aware preview test failed' }
$sceneDir = Join-Path $PSScriptRoot 'docs\themes'
New-Item -ItemType Directory -Path $sceneDir -Force | Out-Null
for ($theme = 0; $theme -lt 5; $theme++) {
    $png = Join-Path $sceneDir (('theme-{0}.png' -f ($theme + 1)))
    $render = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\Noel.exe') -ArgumentList @('--snapshot-theme', [string]$theme, ('"' + $png + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($render.ExitCode -ne 0 -or !(Test-Path -LiteralPath $png)) { throw "Theme $theme render failed" }
}
