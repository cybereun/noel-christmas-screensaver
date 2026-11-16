$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$wpf = Join-Path $framework 'WPF'
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$refs = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path $wpf $_) }
$sources = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', ('/win32manifest:' + (Join-Path $root 'app.manifest')), ('/out:' + (Join-Path $out 'Noel.exe'))) + $refs + $sources
$icon = Join-Path $root 'assets\noel.ico'
if (Test-Path -LiteralPath $icon) { $arguments += '/win32icon:' + $icon }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $out 'Noel.exe') -Destination (Join-Path $out 'Noel.scr') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $out '사용안내.md') -Force
Write-Output "Built: $out"
