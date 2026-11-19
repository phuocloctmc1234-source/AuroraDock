# Aurora Dock - bien dich host (dung csc.exe co san cua Windows, khong can cai gi them)
param([string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin'))
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw 'Khong tim thay csc.exe (.NET Framework 4).' }
$lib = Join-Path $PSScriptRoot 'lib'
$fw = Split-Path $csc
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Copy-Item (Join-Path $lib '*.dll') $Out -Force

$refs = @(
  'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Security.dll', 'System.Management.dll',
  (Join-Path $fw 'System.Runtime.dll'),
  (Join-Path $fw 'System.Runtime.WindowsRuntime.dll'),
  (Join-Path $lib 'Microsoft.Web.WebView2.Core.dll'),
  (Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll'),
  (Join-Path $winmd 'Windows.Foundation.winmd'),
  (Join-Path $winmd 'Windows.Media.winmd')
) | ForEach-Object { "/r:`"$_`"" }

$src = Get-ChildItem $PSScriptRoot -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$exe = Join-Path $Out 'AuroraHost.exe'
$cmd = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warn:3', "/out:`"$exe`"") + $refs + $src
$o = & $csc @cmd 2>&1
if ($LASTEXITCODE -ne 0) { $o | Write-Host; throw "Bien dich that bai ($LASTEXITCODE)" }
$o | Where-Object { $_ -match 'warning' } | Write-Host
# exe console nho cho statusLine cua Claude Code (phai la console de Claude Code doi no ket thuc)
$sl = Join-Path $Out 'aurora-statusline.exe'
$o2 = & $csc /nologo /target:exe /optimize+ "/out:`"$sl`"" '/r:System.dll' '/r:System.Core.dll' '/r:System.Web.Extensions.dll' (Join-Path $PSScriptRoot 'statusline\StatusLine.cs') 2>&1
if ($LASTEXITCODE -ne 0) { $o2 | Write-Host; throw "Bien dich statusline that bai" }
Write-Host "OK: $exe"
