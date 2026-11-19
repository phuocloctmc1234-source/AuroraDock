# Aurora Dock - CUU HO: tra lai thanh taskbar Windows (dung khi Aurora Dock bi tat dot ngot luc dang an taskbar)
$cands = @((Join-Path $env:LOCALAPPDATA 'AuroraDock\AuroraHost.exe'), (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\AuroraHost.exe'))
Get-Process AuroraHost -ErrorAction SilentlyContinue | Stop-Process -Force
$exe = $cands | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($exe) {
  Start-Process $exe -ArgumentList '--restore-taskbar' -Wait -WindowStyle Hidden
  Write-Host 'Da tra lai taskbar Windows.'
  exit 0
}
# Khong con file host: tu lam bang tay (hien taskbar + tat tu an)
Add-Type @"
using System; using System.Runtime.InteropServices;
public class TbFix {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l,t,r,b; }
  [StructLayout(LayoutKind.Sequential)] public struct ABD { public uint cb; public IntPtr hWnd; public uint cm; public uint edge; public RECT rc; public IntPtr lp; }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint m, ref ABD d);
  public static void Fix() {
    IntPtr h = FindWindow("Shell_TrayWnd", null);
    ABD d = new ABD(); d.cb = (uint)Marshal.SizeOf(typeof(ABD)); d.hWnd = h;
    uint st = (uint)SHAppBarMessage(4, ref d).ToUInt64(); d.lp = (IntPtr)(st & ~1u); SHAppBarMessage(10, ref d);
    ShowWindow(h, 8);
  }
}
"@
[TbFix]::Fix()
Write-Host 'Da tra lai taskbar Windows (che do thu cong).'
