// Icon khay he thong THAT cua Windows: Windows 11 luu moi icon khay trong HKCU\Control Panel\NotifyIconSettings
// (duong dan exe, tooltip, IsPromoted = hien tren taskbar, IconSnapshot = anh PNG cua icon). Chi lay app dang chay.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;

namespace Aurora
{
    static class TrayIcons
    {
        static string Resolve(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            var m = Regex.Match(p, @"^\{([0-9A-Fa-f\-]{36})\}\\?(.*)$");
            if (!m.Success) return p;
            string root = null;
            switch (m.Groups[1].Value.ToUpperInvariant())
            {
                case "6D809377-6AF0-444B-8957-A3773F02200E": root = Environment.GetEnvironmentVariable("ProgramW6432"); break;
                case "905E63B6-C1BF-494E-B29C-65B732D3D21A": root = Environment.GetEnvironmentVariable("ProgramW6432"); break;
                case "7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E": root = Environment.GetEnvironmentVariable("ProgramFiles(x86)"); break;
                case "1AC14E77-02E7-4E5D-B744-2EB1AE5198B7": root = Environment.GetFolderPath(Environment.SpecialFolder.System); break;
                case "F38BF404-1D43-42F2-9305-67DE0B28FC23": root = Environment.GetFolderPath(Environment.SpecialFolder.Windows); break;
                case "F1B32785-6FBA-4FCF-9D55-7B8E7F157091": root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); break;
                case "3EB685DB-65F9-4CF6-A03A-E3EF65729F3D": root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); break;
                case "62AB5D82-FDC1-4DC3-A9DD-070D1D495D97": root = Environment.GetEnvironmentVariable("ProgramData"); break;
            }
            return root == null ? null : Path.Combine(root, m.Groups[2].Value);
        }

        static readonly Regex Skip = new Regex(@"(^|\\)(explorer|taskmgr|chrome|msedge|aurorahost|wlrmdr)\.exe$", RegexOptions.IgnoreCase);
        static readonly Dictionary<int, KeyValuePair<TimeSpan, DateTime>> lastCpu = new Dictionary<int, KeyValuePair<TimeSpan, DateTime>>();

        class Info { public double Cpu, Mb; public int N; }

        // exe dang chay -> {cpu%, MB, so tien trinh}; CPU/RAM chi do cho cac exe co trong 'want' (nhe hon)
        static Dictionary<string, Info> Running(HashSet<string> want)
        {
            var run = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase); var seen = new HashSet<int>();
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    using (p)
                    {
                        string e = N.ExePath((uint)p.Id); if (string.IsNullOrEmpty(e)) continue;
                        Info i; if (!run.TryGetValue(e, out i)) { i = new Info(); run[e] = i; }
                        i.N++;
                        if (!want.Contains(e)) continue;
                        seen.Add(p.Id); i.Mb += p.WorkingSet64 / 1048576.0;
                        try
                        {
                            var t = p.TotalProcessorTime; var now = DateTime.UtcNow; KeyValuePair<TimeSpan, DateTime> o;
                            if (lastCpu.TryGetValue(p.Id, out o)) { double dt = (now - o.Value).TotalSeconds; if (dt > 0.2) i.Cpu += Math.Max(0, (t - o.Key).TotalSeconds / dt / Environment.ProcessorCount * 100); }
                            lastCpu[p.Id] = new KeyValuePair<TimeSpan, DateTime>(t, now);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            foreach (var k in new List<int>(lastCpu.Keys)) if (!seen.Contains(k)) lastCpu.Remove(k);
            return run;
        }

        public static List<object> List()
        {
            var res = new List<object>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var rk = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
            {
                if (rk == null) return res;
                var cand = new List<Dictionary<string, object>>(); var want = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in rk.GetSubKeyNames())
                {
                    try
                    {
                        using (var k = rk.OpenSubKey(name))
                        {
                            string exe = Resolve(k.GetValue("ExecutablePath") as string);
                            if (exe == null || Skip.IsMatch(exe)) continue;
                            var snap = k.GetValue("IconSnapshot") as byte[]; if (snap == null || snap.Length < 8) continue;
                            object pro = k.GetValue("IsPromoted");
                            string tip = k.GetValue("InitialTooltip") as string; if (tip != null) tip = tip.Replace("￿", "").Trim();
                            cand.Add(new Dictionary<string, object> { { "p", exe }, { "tip", tip ?? "" }, { "pro", pro != null && Convert.ToInt32(pro) == 1 }, { "png", Convert.ToBase64String(snap) } });
                            want.Add(exe);
                        }
                    }
                    catch { }
                }
                var run = Running(want);
                foreach (var c in cand)
                {
                    string exe = (string)c["p"]; Info i;
                    if (!run.TryGetValue(exe, out i) || !seen.Add(exe)) continue;
                    c["cpu"] = Math.Round(i.Cpu, 1); c["mb"] = Math.Round(i.Mb); c["n"] = i.N; res.Add(c);
                }
            }
            res.Sort((x, y) => ((bool)((Dictionary<string, object>)y)["pro"]).CompareTo((bool)((Dictionary<string, object>)x)["pro"]));
            return res;
        }

        // Thao tac tren app khay: close (dong nhe nhang), kill (ket thuc), folder (mo thu muc), noauto (tat tu khoi dong)
        public static int Act(string path, string act)
        {
            bool ok = false; foreach (var o in List()) if (string.Equals(((Dictionary<string, object>)o)["p"] as string, path, StringComparison.OrdinalIgnoreCase)) ok = true;
            if (!ok) return -1;
            int n = 0;
            if (act == "folder") { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); return 1; }
            if (act == "noauto")
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (rk != null) foreach (var v in rk.GetValueNames()) { string d = rk.GetValue(v) as string; if (d != null && d.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0) { rk.DeleteValue(v, false); n++; } }
                return n;
            }
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    using (p)
                    {
                        string pp = N.ExePath((uint)p.Id); if (pp == null || !string.Equals(pp, path, StringComparison.OrdinalIgnoreCase)) continue;
                        if (act == "kill") { p.Kill(); n++; } else if (act == "close") { if (p.CloseMainWindow()) n++; }
                    }
                }
                catch { }
            }
            return n;
        }

        // Bam nut "Show Hidden Icons" that cua Windows de mo khay day du (bam / chuot phai duoc nhu binh thuong)
        public static bool Flyout()
        {
            bool ok = false;
            var t = new Thread(delegate()
            {
                try
                {
                    IntPtr h = N.FindWindow("Shell_TrayWnd", null); if (h == IntPtr.Zero) return;
                    var e = AutomationElement.FromHandle(h);
                    AutomationElement b = null;
                    var all = e.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"));
                    foreach (AutomationElement x in all) if (x.Current.Name.StartsWith("Show Hidden", StringComparison.OrdinalIgnoreCase)) { b = x; break; }
                    object pat; if (b != null && b.TryGetCurrentPattern(InvokePattern.Pattern, out pat)) { ((InvokePattern)pat).Invoke(); ok = true; }
                }
                catch (Exception ex) { App.Log("trayFlyout: " + ex.Message); }
            });
            t.SetApartmentState(ApartmentState.MTA); t.Start(); t.Join(8000);
            return ok;
        }
    }
}
