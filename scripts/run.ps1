# Aurora Dock - chay thu ngay tu thu muc nay (khong cai). KHONG dong vao taskbar cua Windows.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root 'bin'
$exe = Join-Path $bin 'AuroraHost.exe'
# Go dau "tai tu Internet" (Mark of the Web) khi giai nen tu file zip: neu khong, Windows co the chan DLL/script va app khong mo duoc
Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
$newest = (Get-ChildItem (Join-Path $root 'host') -Filter *.cs | Measure-Object LastWriteTime -Maximum).Maximum
if (-not (Test-Path $exe) -or (Get-Item $exe).LastWriteTime -lt $newest) {
  Write-Host 'Dang bien dich Aurora Host...'
  & (Join-Path $root 'host\build.ps1') -Out $bin
}
if (Get-Process AuroraHost -ErrorAction SilentlyContinue) { Write-Host 'Aurora Dock dang chay roi.'; exit 0 }
Start-Process -FilePath $exe -ArgumentList '--no-taskbar'
Write-Host 'Da mo Aurora Dock (che do thu: giu nguyen taskbar Windows). Thoat: Cai dat > "Thoat & tra lai taskbar" hoac Ctrl+Alt+Shift+Q.'
