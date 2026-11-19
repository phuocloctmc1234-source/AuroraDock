# Aurora Dock - go cai dat (giu lai du lieu cham cong cua ban)
$dst = Join-Path $env:LOCALAPPDATA 'AuroraDock'
$exe = Join-Path $dst 'AuroraHost.exe'
if (Test-Path $exe) { Start-Process $exe -ArgumentList '--restore-taskbar' -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue }
Get-Process AuroraHost -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'AuroraDock' -ErrorAction SilentlyContinue
foreach ($f in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Startup'))) {
  $l = Join-Path $f 'Aurora Dock.lnk'
  if (Test-Path $l) { Remove-Item $l -Force }
}
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
Write-Host 'Da go Aurora Dock va tra lai taskbar Windows.'
Write-Host "Du lieu cham cong & khoa van con tai: $env:APPDATA\AuroraDock  (xoa tay neu khong can nua)"
