# Aurora Dock - cai dat (khong can quyen Admin): bien dich host, chep app, tao shortcut.
param([switch]$Silent, [string]$Version = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dst = Join-Path $env:LOCALAPPDATA 'AuroraDock'
$exe = Join-Path $dst 'AuroraHost.exe'

# 1) Neu ban cu dang chay: tat va tra lai taskbar truoc khi ghi de
if (Get-Process AuroraHost -ErrorAction SilentlyContinue) {
  if (Test-Path $exe) { Start-Process $exe -ArgumentList '--restore-taskbar' -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue }
  Get-Process AuroraHost -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 1
}

# 2) Bien dich host thang vao thu muc cai dat
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Write-Host 'Dang bien dich Aurora Host...'
& (Join-Path $root 'host\build.ps1') -Out $dst

# 3) Chep giao dien (xoa ban cu de khong sot file thua)
$appDst = Join-Path $dst 'app'
if (Test-Path $appDst) { Remove-Item $appDst -Recurse -Force }
Copy-Item -Path (Join-Path $root 'app') -Destination $appDst -Recurse -Force
$oldIndex = Join-Path $dst 'index.html'          # ban 1.0 (Edge) de file o day
if (Test-Path $oldIndex) { Remove-Item $oldIndex -Force }

# 3b) Bo dau "tai tu Internet" (Mark of the Web) de Windows khong chan/canh bao khi chay
Get-ChildItem $dst -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue

# 4) Shortcut (ghi de shortcut cu cua ban Edge neu co)
function New-Lnk($path) {
  $ws = New-Object -ComObject WScript.Shell
  $l = $ws.CreateShortcut($path)
  $l.TargetPath = $exe
  $l.Arguments = ''            # xoa tham so cu (ban 1.0 chay bang Edge: --app=... --window-size=...)
  $l.WorkingDirectory = $dst
  $l.IconLocation = "$exe,0"
  $l.Description = 'Aurora Dock'
  $l.Save()
}
New-Lnk (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Aurora Dock.lnk')
New-Lnk (Join-Path ([Environment]::GetFolderPath('Programs')) 'Aurora Dock.lnk')
$oldStartup = Join-Path ([Environment]::GetFolderPath('Startup')) 'Aurora Dock.lnk'
if (Test-Path $oldStartup) { Remove-Item $oldStartup -Force }   # ban cu tu chay bang Edge

# 5) Ghi phien ban (commit) de ung dung biet khi nao co ban moi
if (-not $Version -and (Test-Path (Join-Path $root '.git'))) { try { $Version = (& git -C $root rev-parse HEAD).Trim() } catch { } }
if ($Version) { Set-Content -Path (Join-Path $dst 'version.txt') -Value $Version -Encoding ascii }

# Mac dinh TU KHOI DONG cung Windows. Chi tat khi nguoi dung da chu dong tat (file autostart.off).
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$optOut = Join-Path $dst 'autostart.off'
if (-not (Test-Path $optOut)) {
  Set-ItemProperty -Path $runKey -Name 'AuroraDock' -Value "`"$exe`""
  # neu Windows dang danh dau "Disabled" trong Task Manager > Startup thi bat lai
  Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run' -Name 'AuroraDock' -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host "Da cai vao: $dst"
Write-Host 'Mo "Aurora Dock" tren Desktop hoac Start menu.'
Write-Host ''
Write-Host 'LUU Y:'
Write-Host ' - Mac dinh Aurora Dock se TU AN thanh taskbar Windows (doi o Cai dat > Giao dien Windows).'
Write-Host ' - Can thoat khan cap: bam Ctrl+Alt+Shift+Q (se tra lai taskbar).'
Write-Host ' - Neu taskbar bi mat sau su co: chay Khoi-phuc-taskbar.bat.'
