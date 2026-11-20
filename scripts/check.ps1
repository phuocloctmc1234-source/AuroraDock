# Aurora Dock - kiem tra nhanh truoc khi commit/push: bien dich host + kiem tra cu phap JS.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tmp = Join-Path $env:TEMP 'aurora-check'
Write-Host '1) Bien dich host...'
& (Join-Path $root 'host\build.ps1') -Out $tmp
Write-Host '2) Kiem tra cu phap JS...'
$node = Get-Command node -ErrorAction SilentlyContinue
if ($node) {
  foreach ($f in Get-ChildItem (Join-Path $root 'app') -Filter *.js) {
    & node -e "new Function(require('fs').readFileSync(process.argv[1],'utf8'))" $f.FullName
    if ($LASTEXITCODE -ne 0) { throw "Loi cu phap: $($f.Name)" }
  }
  Write-Host '   JS hop le.'
} else { Write-Host '   (khong co node - bo qua kiem tra JS)' }
Write-Host 'OK: san sang commit.'
