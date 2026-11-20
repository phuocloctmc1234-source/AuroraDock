// Aurora Dock host - cac dich vu lay DU LIEU THAT (khong co du lieu gia o day).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Aurora
{
    static class J
    {
        public static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        public static string Str(Dictionary<string, object> d, string k)
        {
            object v; if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToString(v); return null;
        }
        public static double Num(Dictionary<string, object> d, string k, double def)
        {
            object v; if (d != null && d.TryGetValue(k, out v) && v != null) { try { return Convert.ToDouble(v); } catch { } } return def;
        }
        public static bool Bool(Dictionary<string, object> d, string k, bool def)
        {
            object v; if (d != null && d.TryGetValue(k, out v) && v != null) { try { return Convert.ToBoolean(v); } catch { } } return def;
        }
    }

    static class App
    {
        public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuroraDock");
        public static readonly object LogLock = new object();
        public static void Warn(string s) { Log("WARN " + s); }
        public static void Log(string s)
        {
            try
            {
                lock (LogLock)
                {
                    Directory.CreateDirectory(Data);
                    string f = Path.Combine(Data, "host.log");
                    if (File.Exists(f) && new FileInfo(f).Length > 512 * 1024) { string o = f + ".old"; if (File.Exists(o)) File.Delete(o); File.Move(f, o); }
                    File.AppendAllText(f, DateTime.Now.ToString("s") + " " + s + "\r\n");
                }
            }
            catch { }
        }
        public static long NowMs() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }
        public static DateTime FromMs(long ms) { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime(); }
    }

    // ---------- CPU / RAM / pin / mang / o dia ----------
    static class Sys
    {
        static readonly object L = new object();
        static long pI, pK, pU; static double cpu;
        static long pRx, pTx; static DateTime pT = DateTime.MinValue; static double rx, tx;
        static long ping = -1; static DateTime pingT = DateTime.MinValue; static bool pinging;
        static int procs; static DateTime procT = DateTime.MinValue;
        static NetworkInterface[] nics; static DateTime nicT = DateTime.MinValue;

        public static object Snapshot()
        {
            lock (L)
            {
                long i, k, u; N.GetSystemTimes(out i, out k, out u);
                if (pK != 0)
                {
                    long tot = (k - pK) + (u - pU), idle = i - pI;
                    if (tot > 0) cpu = Math.Max(0, Math.Min(100, (tot - idle) * 100.0 / tot));
                }
                pI = i; pK = k; pU = u;

                var ms = new N.MEMSTAT(); ms.dwLength = (uint)Marshal.SizeOf(typeof(N.MEMSTAT)); N.GlobalMemoryStatusEx(ref ms);

                long rxb = 0, txb = 0; string kind = ""; bool up = false;
                try
                {
                    // GetAllNetworkInterfaces() tốn ~20ms và sinh nhiều rác: lưu tạm 20 giây (số byte vẫn đọc mới mỗi lần)
                    if (nics == null || (DateTime.UtcNow - nicT).TotalSeconds > 20) { nics = NetworkInterface.GetAllNetworkInterfaces(); nicT = DateTime.UtcNow; }
                    foreach (var ni in nics)
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up) continue;
                        var t = ni.NetworkInterfaceType;
                        if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel) continue;
                        var st = ni.GetIPv4Statistics(); rxb += st.BytesReceived; txb += st.BytesSent; up = true;
                        if (kind == "") kind = t == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : t == NetworkInterfaceType.Ethernet ? "Ethernet" : t.ToString();
                    }
                }
                catch { }
                var now = DateTime.UtcNow;
                if (pT == DateTime.MinValue) { pRx = rxb; pTx = txb; pT = now; }
                else
                {
                    double dt = (now - pT).TotalSeconds;
                    if (dt >= 1.0) { rx = Math.Max(0, (rxb - pRx) * 8 / 1e6 / dt); tx = Math.Max(0, (txb - pTx) * 8 / 1e6 / dt); pRx = rxb; pTx = txb; pT = now; }
                }
                if ((DateTime.UtcNow - pingT).TotalSeconds > 10 && !pinging)
                {
                    pinging = true; pingT = DateTime.UtcNow;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        long r = -1;
                        try { using (var p = new Ping()) { var rep = p.Send("1.1.1.1", 1500); if (rep.Status == IPStatus.Success) r = rep.RoundtripTime; } } catch { }
                        lock (L) { ping = r; pinging = false; }
                    });
                }
                if ((DateTime.UtcNow - procT).TotalSeconds > 15) { try { var ps = Process.GetProcesses(); procs = ps.Length; foreach (var p in ps) p.Dispose(); } catch { } procT = DateTime.UtcNow; }

                var ps2 = SystemInformation.PowerStatus;
                bool hasBat = (ps2.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps2.BatteryChargeStatus != BatteryChargeStatus.Unknown;
                var disks = new List<object>();
                foreach (var d in DriveInfo.GetDrives())
                {
                    try { if (d.DriveType == DriveType.Fixed && d.IsReady) disks.Add(new Dictionary<string, object> { { "name", d.Name }, { "total", d.TotalSize }, { "free", d.AvailableFreeSpace } }); } catch { }
                }
                return new Dictionary<string, object> {
                    { "cpu", cpu }, { "ram", (double)ms.dwMemoryLoad }, { "ramTotal", ms.TotalPhys }, { "ramFree", ms.AvailPhys },
                    { "net", new Dictionary<string,object>{ {"up", up}, {"kind", kind}, {"down", rx}, {"upl", tx}, {"ping", ping} } },
                    { "bat", hasBat ? new Dictionary<string,object>{ {"pct", Math.Round(ps2.BatteryLifePercent*100.0)}, {"charging", ps2.PowerLineStatus==PowerLineStatus.Online}, {"secs", ps2.BatteryLifeRemaining} } : null },
                    { "disks", disks }, { "procs", procs }
                };
            }
        }
    }

    // ---------- Cua so dang mo (thay phan viec cua taskbar) ----------
    static class Wins
    {
        static readonly Dictionary<string, object> icons = new Dictionary<string, object>();
        static readonly uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        public static List<object> List(IntPtr self)
        {
            var res = new List<object>();
            N.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                try
                {
                    if (h == self || !N.IsWindowVisible(h)) return true;
                    { uint wp; N.GetWindowThreadProcessId(h, out wp); if (wp == myPid) return true; }   // cua so phu cua chinh Aurora (VD Shift Desk) khong hien trong dock
                    if (N.GetWindow(h, 4) != IntPtr.Zero) return true; // co owner
                    long ex = (long)N.GetWindowLongPtr(h, N.GWL_EXSTYLE);
                    bool app = (ex & N.WS_EX_APPWINDOW) != 0;
                    if (!app && (ex & (N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE)) != 0) return true;
                    int cloaked; N.DwmGetWindowAttribute(h, 14, out cloaked, 4); if (cloaked != 0) return true;
                    var t = new StringBuilder(256); N.GetWindowText(h, t, 256); if (t.Length == 0) return true;
                    var c = new StringBuilder(64); N.GetClassName(h, c, 64); string cls = c.ToString();
                    if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
                    uint pid; N.GetWindowThreadProcessId(h, out pid);
                    string exe = N.ExePath(pid);
                    res.Add(new Dictionary<string, object> { { "h", h.ToInt64() }, { "t", t.ToString() }, { "p", exe }, { "m", N.IsIconic(h) } });
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return res;
        }

        public static object GetIcon(string path)
        {
            lock (icons)
            {
                object c; if (icons.TryGetValue(path, out c)) return c;
                string name = Path.GetFileNameWithoutExtension(path); string png = null;
                try { var fv = FileVersionInfo.GetVersionInfo(path); if (!string.IsNullOrEmpty(fv.FileDescription)) name = fv.FileDescription; } catch { }
                try
                {
                    using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    using (var bmp = ic.ToBitmap())
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); png = Convert.ToBase64String(ms.ToArray()); }
                }
                catch { }
                var r = new Dictionary<string, object> { { "name", name }, { "png", png } };
                icons[path] = r; return r;
            }
        }

        public static void Activate(IntPtr h, bool minimizeIfActive, IntPtr lastFg)
        {
            if (!N.IsWindow(h)) return;
            if (minimizeIfActive && h == lastFg && !N.IsIconic(h)) { N.ShowWindow(h, 6); return; }
            if (N.IsIconic(h)) N.ShowWindow(h, 9);
            uint pid; uint fgT = N.GetWindowThreadProcessId(N.GetForegroundWindow(), out pid); uint me = N.GetCurrentThreadId();
            if (fgT != me) N.AttachThreadInput(me, fgT, true);
            N.BringWindowToTop(h); N.SetForegroundWindow(h);
            if (fgT != me) N.AttachThreadInput(me, fgT, false);
        }

        public static void Close(IntPtr h) { if (N.IsWindow(h)) N.PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); }
    }

    // ---------- Screen Time that: cong don thoi gian cua so dang dung ----------
    static class ScreenTime
    {
        static readonly object L = new object();
        static Dictionary<string, double> today = new Dictionary<string, double>(); static string day = "";
        static DateTime lastSave = DateTime.UtcNow; static System.Threading.Timer timer; static IntPtr self;

        static string File_(string d) { return Path.Combine(App.Data, "screentime-" + d + ".json"); }
        static string DayKey(DateTime t) { return t.ToString("yyyy-MM-dd"); }

        public static void Start(IntPtr selfHwnd)
        {
            self = selfHwnd;
            Load(DayKey(DateTime.Now));
            timer = new System.Threading.Timer(delegate { Tick(); }, null, 2000, 2000);
        }
        static void Load(string d)
        {
            day = d; today = new Dictionary<string, double>();
            try { if (File.Exists(File_(d))) { var o = J.S.Deserialize<Dictionary<string, double>>(File.ReadAllText(File_(d))); if (o != null) today = o; } } catch { }
        }
        static void Tick()
        {
            try
            {
                lock (L)
                {
                    string d = DayKey(DateTime.Now);
                    if (d != day) { Save(); Load(d); }
                    var li = new N.LASTINPUT(); li.cbSize = (uint)Marshal.SizeOf(typeof(N.LASTINPUT));
                    if (N.GetLastInputInfo(ref li))
                    {
                        uint idleMs = (uint)Environment.TickCount - li.dwTime;
                        if (idleMs > 120000) return; // roi may > 2 phut thi khong tinh
                    }
                    IntPtr fg = N.GetForegroundWindow(); if (fg == IntPtr.Zero || fg == self) return;
                    uint pid; N.GetWindowThreadProcessId(fg, out pid);
                    string exe = N.ExePath(pid); if (exe == "") return;
                    string n = Path.GetFileNameWithoutExtension(exe);
                    if (n.Equals("LockApp", StringComparison.OrdinalIgnoreCase)) return;
                    double v; today.TryGetValue(exe, out v); today[exe] = v + 2;
                    if ((DateTime.UtcNow - lastSave).TotalSeconds > 30) Save();
                }
            }
            catch { }
        }
        static void Save()
        {
            try { Directory.CreateDirectory(App.Data); File.WriteAllText(File_(day), J.S.Serialize(today)); lastSave = DateTime.UtcNow; } catch { }
        }
        public static void Flush() { lock (L) Save(); }

        public static object Snapshot()
        {
            lock (L)
            {
                double total = 0; foreach (var v in today.Values) total += v;
                var top = today.OrderByDescending(kv => kv.Value).Take(6).Select(kv => (object)new Dictionary<string, object> { { "p", kv.Key }, { "s", kv.Value } }).ToList();
                double week = total;
                for (int i = 1; i < 7; i++)
                {
                    try { string f = File_(DayKey(DateTime.Now.AddDays(-i))); if (File.Exists(f)) { var o = J.S.Deserialize<Dictionary<string, double>>(File.ReadAllText(f)); foreach (var v in o.Values) week += v; } } catch { }
                }
                return new Dictionary<string, object> { { "today", total }, { "week", week }, { "top", top } };
            }
        }
    }

    // ---------- Dung luong Claude Code that: doc log local ~/.claude/projects ----------
    static class ClaudeUse
    {
        class Ev { public long T; public long Tok; public long CacheRead; public string Model; }
        class FileCache { public long Pos; public long Mtime; }
        static readonly object L = new object();
        static readonly Dictionary<string, FileCache> files = new Dictionary<string, FileCache>();
        static readonly HashSet<string> seen = new HashSet<string>();
        static readonly List<Ev> evs = new List<Ev>();
        static readonly Regex reTs = new Regex("\"timestamp\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        static IEnumerable<string> Roots()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".claude", "projects");
            yield return Path.Combine(home, ".config", "claude", "projects");
            string cd = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (!string.IsNullOrEmpty(cd)) foreach (var p in cd.Split(',')) yield return Path.Combine(p.Trim(), "projects");
        }

        static long Dt(object o) { try { return Convert.ToInt64(o); } catch { return 0; } }

        static void ParseLine(string line)
        {
            if (line.IndexOf("\"output_tokens\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"usage\"", StringComparison.Ordinal) < 0) return;
            Dictionary<string, object> o;
            try { o = J.S.Deserialize<Dictionary<string, object>>(line); } catch { return; }
            if (o == null) return;
            object mo; if (!o.TryGetValue("message", out mo)) return;
            var m = mo as Dictionary<string, object>; if (m == null) return;
            object uo; if (!m.TryGetValue("usage", out uo)) return;
            var u = uo as Dictionary<string, object>; if (u == null) return;
            string id = J.Str(m, "id") ?? ""; string rid = J.Str(o, "requestId") ?? "";
            if (id != "" || rid != "") { if (!seen.Add(id + ":" + rid)) return; }
            string model = J.Str(m, "model") ?? "";
            if (model == "<synthetic>") return;
            string ts = J.Str(o, "timestamp"); if (ts == null) return;
            DateTime dt; if (!DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out dt)) return;
            object x;
            long inp = u.TryGetValue("input_tokens", out x) ? Dt(x) : 0, outp = u.TryGetValue("output_tokens", out x) ? Dt(x) : 0;
            long cc = u.TryGetValue("cache_creation_input_tokens", out x) ? Dt(x) : 0, cr = u.TryGetValue("cache_read_input_tokens", out x) ? Dt(x) : 0;
            long t = (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            evs.Add(new Ev { T = t, Tok = inp + outp + cc, CacheRead = cr, Model = model });
        }

        static void Scan()
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-8);
            foreach (var root in Roots())
            {
                if (!Directory.Exists(root)) continue;
                IEnumerable<string> list;
                try { list = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories); } catch { continue; }
                foreach (var f in list)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTimeUtc < cutoff) continue;
                        FileCache c; if (!files.TryGetValue(f, out c)) { c = new FileCache(); files[f] = c; }
                        if (fi.Length < c.Pos) { c.Pos = 0; }
                        if (fi.Length == c.Pos) continue;
                        using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            fs.Seek(c.Pos, SeekOrigin.Begin);
                            var buf = new byte[fs.Length - c.Pos]; int n = fs.Read(buf, 0, buf.Length);
                            int last = Array.LastIndexOf(buf, (byte)'\n', n - 1); if (last < 0) continue;
                            string text = Encoding.UTF8.GetString(buf, 0, last + 1);
                            c.Pos += last + 1;
                            foreach (var line in text.Split('\n')) { if (line.Length > 20) ParseLine(line); }
                        }
                    }
                    catch (Exception e) { App.Log("claude scan " + f + ": " + e.Message); }
                }
            }
            long cut = App.NowMs() - 8L * 86400000; evs.RemoveAll(e => e.T < cut);
        }

        public static object Snapshot()
        {
            lock (L)
            {
                Scan();
                long now = App.NowMs(); const long H5 = 5L * 3600000;
                var sorted = evs.OrderBy(e => e.T).ToList();
                // chia thanh "block" 5 gio: block bat dau tu gio tron cua tin nhan dau tien, het sau 5 gio
                var starts = new List<long>(); var toks = new List<long>(); var msgs = new List<long>(); long last = 0;
                foreach (var e in sorted)
                {
                    if (starts.Count == 0 || e.T >= starts[starts.Count - 1] + H5 || e.T - last >= H5) { starts.Add((e.T / 3600000) * 3600000); toks.Add(0); msgs.Add(0); }
                    toks[toks.Count - 1] += e.Tok; msgs[msgs.Count - 1]++; last = e.T;
                }
                bool have = starts.Count > 0;
                long curStart = have ? starts[starts.Count - 1] : 0;
                bool active = have && now < curStart + H5;
                long bTok = active ? toks[toks.Count - 1] : 0, bMsgs = active ? msgs[msgs.Count - 1] : 0;
                long prevMax = 0; // block cao nhat trong qua khu (khong tinh block dang chay)
                for (int i = 0; i < toks.Count - (active ? 1 : 0); i++) if (toks[i] > prevMax) prevMax = toks[i];
                var dayStart = (long)(DateTime.Today.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                long dayTok = 0, dayMsgs = 0, weekTok = 0; var byModel = new Dictionary<string, long>();
                foreach (var e in sorted)
                {
                    if (e.T >= now - 7L * 86400000) weekTok += e.Tok;
                    if (e.T >= dayStart) { dayTok += e.Tok; dayMsgs++; long v; byModel.TryGetValue(e.Model, out v); byModel[e.Model] = v + e.Tok; }
                }
                return new Dictionary<string, object> {
                    { "found", files.Count > 0 }, { "events", sorted.Count },
                    { "active", active }, { "blockStart", have ? curStart : 0 }, { "blockEnd", have ? curStart + H5 : 0 },
                    { "blockTok", bTok }, { "blockMsgs", bMsgs }, { "prevMax", prevMax },
                    { "dayTok", dayTok }, { "dayMsgs", dayMsgs }, { "weekTok", weekTok },
                    { "models", byModel.OrderByDescending(kv => kv.Value).Take(4).Select(kv => (object)new Dictionary<string,object>{{"m",kv.Key},{"t",kv.Value}}).ToList() }
                };
            }
        }
    }

    // ---------- Tep ----------
    static class Files
    {
        public static object Snapshot()
        {
            string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var d = new Dictionary<string, object>();
            try
            {
                var di = new DirectoryInfo(dl); var fs = di.GetFiles();
                var recent = fs.OrderByDescending(f => f.LastWriteTime).Take(4).Select(f => (object)new Dictionary<string, object> { { "n", f.Name }, { "t", f.LastWriteTime.ToString("o") }, { "s", f.Length } }).ToList();
                d["dl"] = new Dictionary<string, object> { { "path", dl }, { "count", fs.Length }, { "size", fs.Sum(f => f.Length) }, { "recent", recent } };
            }
            catch { d["dl"] = null; }
            try
            {
                var rb = new N.RBINFO(); rb.cbSize = (uint)Marshal.SizeOf(typeof(N.RBINFO));
                if (N.SHQueryRecycleBin(null, ref rb) == 0) d["bin"] = new Dictionary<string, object> { { "size", rb.i64Size }, { "count", rb.i64NumItems } };
            }
            catch { }
            return d;
        }
    }

    // ---------- Bi mat (ma hoa DPAPI theo tai khoan Windows) ----------
    static class Secrets
    {
        static readonly object L = new object();
        static readonly byte[] ent = Encoding.UTF8.GetBytes("AuroraDock.v1");
        static string F { get { return Path.Combine(App.Data, "secrets.json"); } }
        // moi bi mat chi duoc gui toi dung ten mien cua no
        static readonly Dictionary<string, string> hosts = new Dictionary<string, string> { { "github", "api.github.com" }, { "stripe", "api.stripe.com" }, { "calendar", "*" } };
        // URL lich ICS cua Google/Outlook chinh la mat khau: chi host moi doc duoc, giao dien khong lay lai duoc
        public static object FetchUrlSecret(string name)
        {
            if (name != "calendar") throw new Exception("khong ho tro");
            return Http.Do(new Dictionary<string, object> { { "url", Get(name) } });
        }

        static Dictionary<string, string> Load()
        {
            try { if (File.Exists(F)) return J.S.Deserialize<Dictionary<string, string>>(File.ReadAllText(F)) ?? new Dictionary<string, string>(); } catch { }
            return new Dictionary<string, string>();
        }
        public static void Set(string name, string val)
        {
            lock (L)
            {
                if (!hosts.ContainsKey(name)) throw new Exception("Ten bi mat khong hop le");
                var d = Load(); d[name] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(val), ent, DataProtectionScope.CurrentUser));
                Directory.CreateDirectory(App.Data); File.WriteAllText(F, J.S.Serialize(d));
            }
        }
        public static void Del(string name) { lock (L) { var d = Load(); d.Remove(name); Directory.CreateDirectory(App.Data); File.WriteAllText(F, J.S.Serialize(d)); } }
        public static List<object> Names() { lock (L) { return Load().Keys.Where(k => hosts.ContainsKey(k)).Select(k => (object)k).ToList(); } }
        // Luu/doc chuoi bat ky (dung rieng cho host, giao dien khong goi duoc)
        public static void SetRaw(string name, string val)
        {
            lock (L)
            {
                var d = Load(); d[name] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(val), ent, DataProtectionScope.CurrentUser));
                Directory.CreateDirectory(App.Data); File.WriteAllText(F, J.S.Serialize(d));
            }
        }
        public static string GetRaw(string name)
        {
            lock (L) { var d = Load(); string v; if (!d.TryGetValue(name, out v)) return null; try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), ent, DataProtectionScope.CurrentUser)); } catch { return null; } }
        }
        static string Get(string name)
        {
            lock (L) { var d = Load(); string v; if (!d.TryGetValue(name, out v)) throw new Exception("Chua co '" + name + "' — hay nhap trong Cai dat > Ket noi du lieu"); return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), ent, DataProtectionScope.CurrentUser)); }
        }
        public static string Subst(string s, string urlHost)
        {
            if (s == null) return null;
            return Regex.Replace(s, @"\$secret:([a-z0-9_]+)", m =>
            {
                string n = m.Groups[1].Value; string h;
                if (!hosts.TryGetValue(n, out h) || h == "*" || !string.Equals(h, urlHost, StringComparison.OrdinalIgnoreCase)) throw new Exception("Bi mat '" + n + "' khong duoc phep gui toi " + urlHost);
                return Get(n);
            });
        }
    }

    // ---------- HTTP qua host (khong vuong CORS, bi mat khong ra khoi may) ----------
    static class Http
    {
        static Http()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 12288); }
            catch { try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { } }
        }
        public static object Do(Dictionary<string, object> a)
        {
            string url0 = J.Str(a, "url"); if (url0 == null || !url0.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new Exception("Chi cho phep https://");
            string host = new Uri(url0).Host;
            string url = Secrets.Subst(url0, host);
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = (J.Str(a, "method") ?? "GET").ToUpperInvariant(); req.Timeout = 15000; req.ReadWriteTimeout = 15000;
            req.UserAgent = "AuroraDock/1.0"; req.AllowAutoRedirect = true;
            var hd = a.ContainsKey("headers") ? a["headers"] as Dictionary<string, object> : null;
            if (hd != null)
                foreach (var kv in hd)
                {
                    string k = kv.Key.ToLowerInvariant(), v = Secrets.Subst(Convert.ToString(kv.Value), host);
                    if (k == "accept") req.Accept = v; else if (k == "content-type") req.ContentType = v; else if (k == "user-agent") req.UserAgent = v; else req.Headers[kv.Key] = v;
                }
            string body = J.Str(a, "body");
            if (body != null) { var b = Encoding.UTF8.GetBytes(body); req.ContentLength = b.Length; using (var s = req.GetRequestStream()) s.Write(b, 0, b.Length); }
            WebResponse resp;
            try { resp = req.GetResponse(); } catch (WebException ex) { if (ex.Response == null) throw; resp = ex.Response; }
            using (resp)
            using (var rs = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                var buf = new byte[16384]; int n; while ((n = rs.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 3 * 1024 * 1024) break; }
                return new Dictionary<string, object> { { "status", (int)((HttpWebResponse)resp).StatusCode }, { "body", Encoding.UTF8.GetString(ms.ToArray()) } };
            }
        }
    }

    // ---------- Cham cong: nhat ky su kien that, ghi ben vung vao dia ----------
    static class Timesheet
    {
        static readonly object L = new object();
        static HashSet<string> ids; static string F { get { return Path.Combine(App.Data, "timesheet.jsonl"); } }
        static void Init()
        {
            if (ids != null) return; ids = new HashSet<string>();
            try { if (File.Exists(F)) foreach (var line in File.ReadAllLines(F)) { try { var o = J.S.Deserialize<Dictionary<string, object>>(line); ids.Add(J.Str(o, "id")); } catch { } } } catch { }
        }
        public static object Append(Dictionary<string, object> a)
        {
            lock (L)
            {
                Init(); string id = J.Str(a, "id"); if (string.IsNullOrEmpty(id)) throw new Exception("thieu id");
                if (ids.Contains(id)) return new Dictionary<string, object> { { "ok", true }, { "duplicate", true } };
                Directory.CreateDirectory(App.Data);
                File.AppendAllText(F, J.S.Serialize(new Dictionary<string, object> { { "id", id }, { "name", J.Str(a, "name") }, { "at", (long)J.Num(a, "at", App.NowMs()) } }) + "\n");
                ids.Add(id); return new Dictionary<string, object> { { "ok", true } };
            }
        }
        public static object List(Dictionary<string, object> a)
        {
            lock (L)
            {
                long since = (long)J.Num(a, "since", 0); var res = new List<object>();
                try { if (File.Exists(F)) foreach (var line in File.ReadAllLines(F)) { try { var o = J.S.Deserialize<Dictionary<string, object>>(line); if (Convert.ToInt64(o["at"]) >= since) res.Add(o); } catch { } } } catch { }
                return res;
            }
        }
    }

    // ---------- Nhac dang phat that (Windows media session) ----------
    static class Media
    {
        static Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager mgr;
        static T Wait<T>(Windows.Foundation.IAsyncOperation<T> op)
        {
            var ev = new ManualResetEvent(false);
            op.Completed = new Windows.Foundation.AsyncOperationCompletedHandler<T>(delegate(Windows.Foundation.IAsyncOperation<T> o, Windows.Foundation.AsyncStatus st) { ev.Set(); });
            if (!ev.WaitOne(8000)) throw new Exception("Windows media session khong tra loi");
            return op.GetResults();
        }
        static Windows.Media.Control.GlobalSystemMediaTransportControlsSession Cur()
        {
            if (mgr == null) mgr = Wait(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            return mgr.GetCurrentSession();
        }
        public static object Get()
        {
            var s = Cur(); if (s == null) return new Dictionary<string, object> { { "none", true } };
            var p = Wait(s.TryGetMediaPropertiesAsync()); var pb = s.GetPlaybackInfo();
            string app = s.SourceAppUserModelId ?? "";
            return new Dictionary<string, object> { { "none", false }, { "title", p.Title }, { "artist", p.Artist }, { "album", p.AlbumTitle }, { "playing", pb.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing }, { "app", app } };
        }
        public static void Ctl(string cmd)
        {
            var s = Cur(); if (s == null) return;
            if (cmd == "toggle") Wait(s.TryTogglePlayPauseAsync());
            else if (cmd == "next") Wait(s.TrySkipNextAsync());
            else if (cmd == "prev") Wait(s.TrySkipPreviousAsync());
        }
    }
}
