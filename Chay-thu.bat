@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"
if errorlevel 1 (
  echo.
  echo Co loi khi chay Aurora Dock. Xem thong bao o tren, hoac file %%APPDATA%%\AuroraDock\host.log
  pause
)
