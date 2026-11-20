// Lark ve popup thong bao RIENG (cua so Chrome_WidgetWin_1 o goc man hinh), khong qua thong bao Windows.
// Module nay nhan ra cua so do, doc chu bang UI Automation, (tuy chon) day no ra ngoai man hinh de khong hien banner,
// roi dua tin vao danh sach thong bao -> giao dien hien thanh bong bong.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;

namespace Aurora
{
    static class LarkPopup
    {
        delegate bool EP(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EP f, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

        public static volatile bool Hide = true;
        public static Action OnNew;
        public static Action<string> OnWarn;
        static readonly List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
        static readonly HashSet<long> seen = new HashSet<long>();
        static Thread th; static volatile bool idleNoLark; static int tick; static IntPtr warmed;

        public static void Start()
        {
            if (th != null) return;
            th = new Thread(Loop) { IsBackground = true, Name = "LarkPopup" };
            th.SetApartmentState(ApartmentState.MTA); th.Start();
        }

        public static List<object> Items()
        {
            long lim = App.NowMs() - 3 * 3600 * 1000L;
            lock (items) { items.RemoveAll(i => (long)i["t"] < lim); return new List<object>(items.ConvertAll(i => (object)i)); }
        }

        static void Loop()
        {
            while (true)
            {
                try
                {
                    var pids = new HashSet<uint>();
                    foreach (var n in new[] { "Lark", "Feishu" }) foreach (var p in Process.GetProcessesByName(n)) { pids.Add((uint)p.Id); p.Dispose(); }
                    idleNoLark = pids.Count == 0;
                    if (pids.Count > 0 && ++tick % 40 == 1)
                    {
                        // "danh thuc" tro nang truy cap cua Lark (Chromium chi mo cay chu khi co chuong trinh UI Automation hoi): de luc bam bong bong doc ket qua tim kiem ngay
                        IntPtr m = FindMain(pids);
                        if (m != IntPtr.Zero && m != warmed) { warmed = m; try { AutomationElement.FromHandle(m).FindAll(TreeScope.Descendants, Condition.TrueCondition); } catch { } }
                    }
                    if (pids.Count > 0)
                    {
                        var found = new List<IntPtr>();
                        EnumWindows(delegate(IntPtr h, IntPtr l)
                        {
                            uint pid; GetWindowThreadProcessId(h, out pid); if (!pids.Contains(pid) || !IsWindowVisible(h)) return true;
                            var c = new StringBuilder(64); GetClassName(h, c, 64); if (c.ToString() != "Chrome_WidgetWin_1") return true;
                            int ex = GetWindowLong(h, -20); if ((ex & 0x80) == 0 || (ex & 8) == 0) return true;   // TOOLWINDOW + TOPMOST
                            RECT r; GetWindowRect(h, out r); int w = r.R - r.L, hh = r.B - r.T;
                            if (r.L < -5000 || w < 240 || w > 520 || hh < 50 || hh > 200) return true;                // dung cua so popup (~360x80)
                            found.Add(h); return true;
                        }, IntPtr.Zero);
                        foreach (var h in found) { if (seen.Add(h.ToInt64())) Handle(h); }
                        if (seen.Count > 500) seen.Clear();
                    }
                }
                catch (Exception e) { App.Log("larkpopup: " + e.Message); }
                Thread.Sleep(idleNoLark ? 1500 : 120);   // khong co Lark: quet thua de tiet kiem CPU
            }
        }

        // ---------- mo dung cuoc tro chuyen trong Lark: bat Lark len, Ctrl+K (tim kiem), dan ten, Enter ----------
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint f);
        // cua so goc nam duoi diem (x,y) co phai la w khong (dung truoc khi bam chuot: tranh bam nham vao cua so khac che len tren)
        static bool IsTopAt(IntPtr w, int x, int y) { POINT p; p.X = x; p.Y = y; IntPtr h = WindowFromPoint(p); return h != IntPtr.Zero && GetAncestor(h, 2) == w; }
        static uint GetWindowPid(IntPtr w) { uint p; GetWindowThreadProcessId(w, out p); return p; }
        static void Focus(IntPtr w)
        {
            uint wpid; GetWindowThreadProcessId(w, out wpid);
            for (int attempt = 0; attempt < 3 && GetForegroundWindow() != w; attempt++)
            {
                IntPtr fg = GetForegroundWindow(); uint ft = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, IntPtr.Zero), me = GetCurrentThreadId();
                if (ft != 0 && ft != me) AttachThreadInput(me, ft, true);
                keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);   // phim Alt: mo khoa "chi cua so dang dung moi duoc lay focus"
                ShowWindow(w, 9); SetForegroundWindow(w);
                if (ft != 0 && ft != me) AttachThreadInput(me, ft, false);
                for (int i = 0; i < 10 && GetForegroundWindow() != w; i++) Thread.Sleep(30);
                if (GetForegroundWindow() != w) { try { Microsoft.VisualBasic.Interaction.AppActivate((int)wpid); } catch { } Thread.Sleep(150); }
            }
        }
        static IntPtr FindMain(HashSet<uint> pids)
        {
            IntPtr best = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint pid; GetWindowThreadProcessId(h, out pid); if (!pids.Contains(pid)) return true;
                var c = new StringBuilder(64); GetClassName(h, c, 64); if (c.ToString() != "Chrome_WidgetWin_1") return true;
                var t = new StringBuilder(128); GetWindowText(h, t, 128); string ts = t.ToString(); if (ts != "Lark" && ts != "Feishu" && ts != "飞书") return true;
                RECT r; GetWindowRect(h, out r); if (r.R - r.L < 600) return true;
                best = h; return false;
            }, IntPtr.Zero);
            return best;
        }
        // Mo bang tim kiem bang cach BAM vao o "Search (Ctrl+K)" (Ctrl+K khong dung duoc khi con tro dang nam trong o soan tin: se dan chu vao tin dang soan)
        static bool OpenSearch(IntPtr hwnd)
        {
            bool ok = false;
            var mt = new Thread(delegate()
            {
                try
                {
                    var root = AutomationElement.FromHandle(hwnd); RECT wrc; GetWindowRect(hwnd, out wrc);
                    if (FindDialog(root, wrc) != null) { ok = true; return; }   // da mo san
                    AutomationElement btn = null;
                    foreach (AutomationElement b in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
                        if ((b.Current.Name ?? "").StartsWith("Search (Ctrl")) { btn = b; break; }
                    if (btn == null) return;
                    var r = btn.Current.BoundingRectangle; POINT o; GetCursorPos(out o);
                    SetCursorPos((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2)); Thread.Sleep(30);
                    mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    SetCursorPos(o.X, o.Y);
                    for (int i = 0; i < 25 && !ok; i++) { if (FindDialog(root, wrc) != null) ok = true; else Thread.Sleep(60); }
                    Thread.Sleep(120);   // o nhap dang nhan focus
                }
                catch (Exception e) { App.Warn("larkpopup opensearch: " + e.Message); }
            });
            mt.SetApartmentState(ApartmentState.MTA); mt.Start(); mt.Join(6000);
            return ok;
        }
        static AutomationElement FindDialog(AutomationElement root, RECT wrc)
        {
            foreach (AutomationElement g in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "scrollable content")))
            {
                var gr = g.Current.BoundingRectangle;   // bang tim kiem: giua cua so, ngay duoi hang o tim kiem va cac the loc
                if (gr.Width > 800 && Math.Abs((gr.Left + gr.Width / 2) - (wrc.L + wrc.R) / 2.0) < 16 && gr.Top >= wrc.T + 110 && gr.Top <= wrc.T + 260) return g;   // bang tim kiem nam CHINH GIUA cua so (khac cac khung noi dung lech phai)
            }
            return null;
        }
        // tieu de dong ket qua == ten (cho phep hau to "(so thanh vien)" cua nhom / nhan Department, Bot)
        static bool RowMatches(string row, string want)
        {
            if (row == want) return true;
            if (!row.StartsWith(want)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(row.Substring(want.Length), @"^(\(\d+\))?(department|bot|external)?$");
        }
        // tim dong cuoc tro chuyen co ten khop trong danh sach Chats (cot trai, duoi hang bo loc). Tra ve toa do giua dong.
        static bool FindInList(IntPtr hwnd, string name, out int x, out int y)
        {
            x = y = 0; string want = Norm(name);
            try
            {
                var root = AutomationElement.FromHandle(hwnd); RECT wrc; GetWindowRect(hwnd, out wrc);
                if (FindDialog(root, wrc) != null) return false;   // bang tim kiem dang mo: de duong tim kiem xu ly
                foreach (AutomationElement t in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
                {
                    var r = t.Current.BoundingRectangle; if (r.IsEmpty || r.Width <= 0) continue;
                    if (r.Left < wrc.L + 180 || r.Left > wrc.L + 430 || r.Top < wrc.T + 150) continue;   // cot danh sach chat, duoi hang Filter/Chats
                    if (Norm(t.Current.Name) != want) continue;
                    x = (int)(wrc.L + 330 + (wrc.R - wrc.L > 1000 ? 0 : 0)); x = (int)(r.Left + Math.Min(r.Width / 2, 120)); y = (int)(r.Top + r.Height / 2);
                    return true;
                }
            }
            catch (Exception e) { App.Warn("larkpopup list: " + e.Message); }
            return false;
        }
        // gia tri o nhap cua bang tim kiem (nam ngay tren khung ket qua) co phai la ten dang tim khong
        static bool InputIs(AutomationElement root, AutomationElement grp, string want)
        {
            var gb = grp.Current.BoundingRectangle;
            foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)))
            {
                var r = e.Current.BoundingRectangle;
                if (r.Top >= gb.Top || r.Top < gb.Top - 160 || r.Left < gb.Left - 20 || r.Left > gb.Right) continue;
                string v = null; object po;
                if (e.TryGetCurrentPattern(ValuePattern.Pattern, out po)) v = ((ValuePattern)po).Current.Value;
                if (string.IsNullOrEmpty(v)) v = e.Current.Name;
                return Norm(v) == want;
            }
            return true;   // khong doc duoc o nhap: bo qua kiem tra nay (van con kiem tra dong ket qua)
        }
        static string Norm(string t) { return System.Text.RegularExpressions.Regex.Replace(t ?? "", @"\s+", "").ToLowerInvariant(); }
        // Tra ve toa do giua dong ket qua co tieu de == ten (cac manh chu to mau goc nhau ghep lai). Cho toi da ~3.5 giay.
        static bool WaitResult(IntPtr hwnd, string name, out int x, out int y)
        {
            x = y = 0; string want = Norm(name); int stable = 0;
            for (int i = 0; i < 60; i++)
            {
                try
                {
                    var root = AutomationElement.FromHandle(hwnd);
                    RECT wrc; GetWindowRect(hwnd, out wrc); AutomationElement grp = FindDialog(root, wrc);
                    if (grp != null && !InputIs(root, grp, want)) { stable = 0; Thread.Sleep(70); continue; }   // o nhap CHUA co ten can tim: ket qua dang hien la cua lan tim truoc
                    if (grp != null)
                    {
                        var rows = new SortedDictionary<int, List<AutomationElement>>();
                        foreach (AutomationElement t in grp.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
                        {
                            var r = t.Current.BoundingRectangle; if (r.IsEmpty || r.Width <= 0) continue;
                            List<AutomationElement> l; int key = (int)(r.Top / 4);
                            bool found = false; foreach (var k in new List<int>(rows.Keys)) if (Math.Abs(k - key) <= 1) { rows[k].Add(t); found = true; break; }
                            if (!found) { l = new List<AutomationElement>(); l.Add(t); rows[key] = l; }
                        }
                        bool hit = false;
                        foreach (var kv in rows)
                        {
                            var parts = kv.Value; parts.Sort(delegate(AutomationElement p, AutomationElement q) { return p.Current.BoundingRectangle.Left.CompareTo(q.Current.BoundingRectangle.Left); });
                            var sb = new StringBuilder(); foreach (var pa in parts) sb.Append(pa.Current.Name);
                            if (RowMatches(Norm(sb.ToString()), want)) { if (++stable < 2) { hit = true; break; } var rc = parts[0].Current.BoundingRectangle; var gb = grp.Current.BoundingRectangle; x = (int)(gb.Left + gb.Width / 2); y = (int)(rc.Top + rc.Height + 6); return true; }
                        }
                        if (!hit) stable = 0;
                    }
                }
                catch (Exception e) { if (i % 10 == 0) App.Warn("larkpopup wait: " + e.GetType().Name + " " + e.Message + " " + (e.StackTrace ?? "").Split((char)10)[0]); }
                Thread.Sleep(70);
            }
            App.Warn("larkpopup: khong thay ket qua cho [" + name + "]");
            return false;
        }
        public static object OpenChat(string name, string paste)
        {
            if (string.IsNullOrEmpty(name)) throw new Exception("thieu ten cuoc tro chuyen");
            string url = Lark.FindChatUrl(name);   // uu tien API: mo thang bang deep link, khong can dieu khien chuot/phim
            if (url != null)
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                if (!string.IsNullOrEmpty(paste)) { Thread.Sleep(2500); var ct = new Thread(delegate() { try { Clipboard.SetText(paste); SendKeys.SendWait("^v"); } catch { } }); ct.SetApartmentState(ApartmentState.STA); ct.Start(); ct.Join(5000); }
                return true;
            }
            Exception err = null;
            var t = new Thread(delegate()
            {
                try
                {
                    var pids = new HashSet<uint>();
                    foreach (var n in new[] { "Lark", "Feishu" }) foreach (var p in Process.GetProcessesByName(n)) { pids.Add((uint)p.Id); p.Dispose(); }
                    IntPtr w = FindMain(pids);   // Lark dang chay + cua so san sang: khong can khoi dong lai, vao thang
                    if (w == IntPtr.Zero || !IsWindowVisible(w))   // Lark dang an o khay he thong: goi lai de no tu hien cua so
                    {
                        Process.Start(new ProcessStartInfo { FileName = @"shell:AppsFolder\com.electron.Lark", UseShellExecute = true });
                        if (pids.Count == 0) Thread.Sleep(3000);
                    }
                    for (int i = 0; i < 40 && (w == IntPtr.Zero || !IsWindowVisible(w)); i++)
                    {
                        pids.Clear(); foreach (var n in new[] { "Lark", "Feishu" }) foreach (var p in Process.GetProcessesByName(n)) { pids.Add((uint)p.Id); p.Dispose(); }
                        w = FindMain(pids); if (w == IntPtr.Zero || !IsWindowVisible(w)) Thread.Sleep(150);
                    }
                    if (w == IntPtr.Zero || !IsWindowVisible(w)) throw new Exception("Khong thay cua so Lark (dang an?)");
                    if (IsIconic(w) || !IsWindowVisible(w)) ShowWindow(w, 9);
                    Focus(w);
                    if (GetForegroundWindow() != w) throw new Exception("Windows khong cho dua Lark len truoc - hay bam vao cua so Lark roi thu lai");   // KHONG gui phim/chuot neu Lark chua o truoc (tranh bam nham app khac)
                    { RECT fr; GetWindowRect(w, out fr); if (!IsTopAt(w, (fr.L + fr.R) / 2, (fr.T + fr.B) / 2)) { try { Microsoft.VisualBasic.Interaction.AppActivate((int)GetWindowPid(w)); } catch { } Thread.Sleep(250); } }
                    string old = null; int cx = 0, cy = 0;
                    // DUONG NHANH: cuoc tro chuyen vua co tin moi thuong nam ngay tren danh sach "Chats" ben trai -> bam thang vao do (khong can tim kiem)
                    bool direct = false; int dx = 0, dy = 0;
                    var lt = new Thread(delegate() { direct = FindInList(w, name, out dx, out dy); }); lt.SetApartmentState(ApartmentState.MTA); lt.Start(); lt.Join(4000);
                    if (direct) { cx = dx; cy = dy; }
                    else
                    {
                        try { if (Clipboard.ContainsText()) old = Clipboard.GetText(); } catch { }
                        Clipboard.SetText(name);
                        if (!OpenSearch(w)) throw new Exception("Khong mo duoc o tim kiem cua Lark");
                        SendKeys.SendWait("^a"); Thread.Sleep(40); SendKeys.SendWait("^v");   // ^a: thay het chu cu neu bang tim kiem da mo san
                        // Doi ket qua tim kiem HIEN DUNG dong co ten khop (doc bang UI Automation), roi bam dung vao dong do: nhanh va khong bam nham
                        bool okRes = false; int rx = 0, ry = 0;
                        var mt = new Thread(delegate() { okRes = WaitResult(w, name, out rx, out ry); }); mt.SetApartmentState(ApartmentState.MTA); mt.Start(); mt.Join(12000);   // UI Automation phai chay o luong MTA
                        cx = rx; cy = ry;
                        if (!okRes) { SendKeys.SendWait("{ESC}"); throw new Exception("Khong thay \"" + name + "\" trong ket qua tim kiem Lark"); }
                    }
                    if (!IsTopAt(w, cx, cy)) throw new Exception("Lark dang bi cua so khac che — khong bam (hay dua Lark len truoc roi thu lai)");
                    POINT old0; GetCursorPos(out old0);
                    Thread.Sleep(direct ? 10 : 90); SetCursorPos(cx, cy); Thread.Sleep(direct ? 30 : 70); mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(40); SetCursorPos(old0.X, old0.Y); Thread.Sleep(string.IsNullOrEmpty(paste) ? 30 : 500);
                    if (!string.IsNullOrEmpty(paste)) { Clipboard.SetText(paste); SendKeys.SendWait("^v"); Thread.Sleep(300); }   // chi dan vao o soan tin, KHONG gui
                    else if (old != null) { try { Clipboard.SetText(old); } catch { } }
                }
                catch (Exception e) { err = e; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(30000);
            if (err != null) throw err;
            return true;
        }

        static void Handle(IntPtr h)
        {
            try
            {
                if (Hide) SetWindowPos(h, IntPtr.Zero, -32000, -32000, 0, 0, 0x0001 | 0x0004 | 0x0010);   // NOSIZE | NOZORDER | NOACTIVATE
                List<string> texts = null;
                for (int i = 0; i < 12; i++)
                {
                    texts = Read(h);
                    if (texts.Count >= 2) break;
                    Thread.Sleep(120);
                }
                if (texts == null || texts.Count == 0) { App.Warn("larkpopup: thay popup nhung khong doc duoc chu"); var wcb = OnWarn; if (wcb != null) wcb("Lark đổi giao diện popup — không đọc được tin để tạo bong bóng"); return; }
                string title = texts[0], body = texts.Count > 1 ? string.Join(" ", texts.GetRange(1, texts.Count - 1).ToArray()) : "";
                if (body.Length > 400) body = body.Substring(0, 400) + "…";
                long now = App.NowMs();
                lock (items)
                {
                    foreach (var it in items) if ((string)it["title"] == title && (string)it["text"] == body && now - (long)it["t"] < 4000) return;   // cung mot popup
                    items.Insert(0, new Dictionary<string, object> { { "id", "lp" + now + "-" + h.ToInt64() }, { "aumid", "com.electron.Lark" }, { "app", "Lark" }, { "site", "" }, { "title", title }, { "text", body }, { "t", now } });
                    if (items.Count > 60) items.RemoveRange(60, items.Count - 60);
                }
                var cb = OnNew; if (cb != null) cb();
            }
            catch (Exception e) { App.Log("larkpopup handle: " + e.Message); }
        }

        static List<string> Read(IntPtr h)
        {
            var l = new List<string>();
            try
            {
                var el = AutomationElement.FromHandle(h);
                foreach (AutomationElement a in el.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
                {
                    string n = (a.Current.Name ?? "").Trim(); if (n != "") l.Add(n);
                }
            }
            catch { }
            return l;
        }
    }
}
