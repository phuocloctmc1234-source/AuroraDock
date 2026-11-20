// Aurora Dock host - cua so chinh: AppBar + Acrylic + WebView2, quan ly taskbar Windows, dieu phoi lenh tu giao dien.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace Aurora
{
    // ---------- Thanh taskbar cua Windows: an / tu an / khoi phuc (luon khoi phuc duoc) ----------
    static class Taskbar
    {
        static string StateFile { get { return Path.Combine(App.Data, "taskbar-state.json"); } }
        static string mode = "keep";

        static N.APPBARDATA Abd() { var d = new N.APPBARDATA(); d.cbSize = (uint)Marshal.SizeOf(typeof(N.APPBARDATA)); d.hWnd = N.FindWindow("Shell_TrayWnd", null); return d; }
        static uint GetState() { var d = Abd(); return (uint)N.SHAppBarMessage(4, ref d).ToUInt64(); }
        static void SetState(uint s) { var d = Abd(); d.lParam = (IntPtr)s; N.SHAppBarMessage(0xA, ref d); }

        static List<IntPtr> Bars()
        {
            var l = new List<IntPtr>(); IntPtr p = N.FindWindow("Shell_TrayWnd", null); if (p != IntPtr.Zero) l.Add(p);
            N.EnumWindows(delegate(IntPtr h, IntPtr x) { var c = new System.Text.StringBuilder(64); N.GetClassName(h, c, 64); if (c.ToString() == "Shell_SecondaryTrayWnd") l.Add(h); return true; }, IntPtr.Zero);
            return l;
        }
        // Chi an taskbar cua man hinh CHINH (noi dock chay). Taskbar man hinh phu giu nguyen de van dung duoc.
        static void Show(bool on)
        {
            IntPtr primary = N.FindWindow("Shell_TrayWnd", null);
            foreach (var h in Bars()) { if (on || h != primary) N.ShowWindow(h, 8); else if (N.IsWindowVisible(h)) N.ShowWindow(h, 0); }
        }

        static void SaveOriginal()
        {
            if (File.Exists(StateFile)) return;
            Directory.CreateDirectory(App.Data);
            File.WriteAllText(StateFile, J.S.Serialize(new Dictionary<string, object> { { "state", (long)GetState() } }));
        }
        public static void Set(string m)
        {
            if (m == "keep") { Restore(); mode = "keep"; return; }
            SaveOriginal(); mode = m;
            SetState(GetState() | 1u);          // tu an: giai phong vung lam viec ma taskbar chiem
            Show(m != "hide");
            if (m == "hide") Show(false);
        }
        // chay moi 0,5s o che do "An han": chi kiem tra 1 cua so, khong duyet toan bo cua so nua
        public static void Reassert() { if (mode == "hide") { IntPtr p = N.FindWindow("Shell_TrayWnd", null); if (p != IntPtr.Zero && N.IsWindowVisible(p)) N.ShowWindow(p, 0); } }
        public static void Restore()
        {
            try
            {
                if (File.Exists(StateFile))
                {
                    var d = J.S.Deserialize<Dictionary<string, object>>(File.ReadAllText(StateFile));
                    SetState((uint)Convert.ToInt64(d["state"]));   // tra lai dung trang thai tu an ban dau cua nguoi dung
                    File.Delete(StateFile);
                }
                Show(true);
            }
            catch (Exception e) { App.Log("restore taskbar: " + e.Message); Show(true); }
            mode = "keep";
        }
        public static bool NeedsRescue() { return File.Exists(StateFile); }
    }

    class Host : Form
    {
        WebView2 wv = new WebView2();
        readonly string appDir; readonly bool debug; public static bool noTaskbar;
        uint appBarMsg; bool barRegistered; string edge = "bottom"; double barSize = 80; bool reserve = true;
        IntPtr lastFg = IntPtr.Zero;
        System.Windows.Forms.Timer fgTimer;
        const int HOTKEY_QUIT = 0x51;

        public Host(string appDir, bool debug)
        {
            this.appDir = appDir; this.debug = debug; Instance = this;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black; AutoScaleMode = AutoScaleMode.None; Text = "Aurora Dock";
            wv.Dock = DockStyle.Fill; wv.DefaultBackgroundColor = Color.Transparent;
            Controls.Add(wv);
            Bounds = Screen.PrimaryScreen.Bounds;
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.Style = unchecked((int)0x80000000); cp.ExStyle |= (int)(N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE); return cp; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            appBarMsg = N.RegisterWindowMessage("AuroraDockAppBar");
            N.RegisterHotKey(Handle, HOTKEY_QUIT, 0x1 | 0x2 | 0x4, 0x51); // Ctrl+Alt+Shift+Q = thoat & tra lai taskbar
            N.SetWindowRgn(Handle, N.CreateRectRgn(0, 0, 0, 0), false);    // an het cho toi khi giao dien bao vung can ve
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PositionBar(); InitTray();
            ScreenTime.Start(Handle);
            fgTimer = new System.Windows.Forms.Timer { Interval = 500 };
            fgTimer.Tick += delegate
            {
                IntPtr fg = N.GetForegroundWindow(); if (fg != IntPtr.Zero && fg != Handle) lastFg = fg;
                Taskbar.Reassert();
            };
            fgTimer.Start();
            Boot();
        }

        async void Boot()
        {
            try
            {
                string ud = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraDock", "WebView2");
                // Cá» giáº£m RAM (Ä‘o trÃªn mÃ¡y 8GB: Private 327 -> 212 MB, bá»›t 1 tiáº¿n trÃ¬nh): GPU cháº¡y chung tiáº¿n trÃ¬nh trÃ¬nh duyá»‡t,
                // chá»‰ 1 tiáº¿n trÃ¬nh render, vÃ  khÃ´ng Ä‘á»ƒ Chromium tá»± coi cá»­a sá»• lÃ  "bá»‹ che" rá»“i tÃ­nh toÃ¡n láº¡i liÃªn tá»¥c.
                string flags = Environment.GetEnvironmentVariable("AURORA_WV_FLAGS") ?? "--in-process-gpu --renderer-process-limit=1 --disable-features=CalculateNativeWinOcclusion";   // biáº¿n mÃ´i trÆ°á»ng chá»‰ Ä‘á»ƒ Ä‘o thá»­
                var opts = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = flags + (debug ? " --remote-debugging-port=9333" : "") };
                // mo lai ngay sau khi tat: WebView2 cu co the chua thoat xong va con giu thu muc (0x800700AA) -> thu lai vai lan
                for (int attempt = 1; ; attempt++)
                {
                    bool busy = false;
                    try { Env = await CoreWebView2Environment.CreateAsync(null, ud, opts); await wv.EnsureCoreWebView2Async(Env); break; }
                    catch (System.Runtime.InteropServices.COMException ce) { if (attempt >= 40 || (uint)ce.ErrorCode != 0x800700AA) throw; busy = true; App.Log("webview busy, thu lai " + attempt); }
                    if (busy)
                    {
                        SetTray("busy", "Aurora Dock — đang khởi động (WebView đang bận, thử lại " + attempt + ")");
                        await System.Threading.Tasks.Task.Delay(1000);
                        // dieu khien WebView2 da hong khong the khoi tao lai: phai tao dieu khien MOI cho lan thu sau
                        try { Controls.Remove(wv); wv.Dispose(); } catch { }
                        wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent }; Controls.Add(wv);
                    }
                }
                var c = wv.CoreWebView2;
                c.ProcessFailed += delegate(object s, CoreWebView2ProcessFailedEventArgs a)
                {
                    App.Log("webview process failed: " + a.ProcessFailedKind);
                    if (a.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited) Relaunch();
                    else if (a.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited || a.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive) { try { c.Reload(); } catch { } }
                };
                c.Settings.AreDevToolsEnabled = debug; c.Settings.AreDefaultContextMenusEnabled = debug;
                c.Settings.AreBrowserAcceleratorKeysEnabled = debug; c.Settings.IsStatusBarEnabled = false; c.Settings.IsZoomControlEnabled = false;
                c.SetVirtualHostNameToFolderMapping("aurora.local", appDir, CoreWebView2HostResourceAccessKind.Allow);
                c.NavigationStarting += delegate(object s, CoreWebView2NavigationStartingEventArgs a) { if (!a.Uri.StartsWith("https://aurora.local/")) a.Cancel = true; };
                c.NewWindowRequested += delegate(object s, CoreWebView2NewWindowRequestedEventArgs a) { a.Handled = true; };
                c.WebMessageReceived += OnMsg;
                c.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs a) { if (c.Source.StartsWith("https://aurora.local/")) SetTray(a.IsSuccess ? "ok" : "err", a.IsSuccess ? "Aurora Dock — đang chạy" : "Aurora Dock — lỗi tải giao diện"); };
                c.Navigate("https://aurora.local/index.html?host=1&t=" + App.NowMs() + (noTaskbar ? "&notb=1" : ""));
            }
            catch (Exception ex)
            {
                App.Log("boot: " + ex); SetTray("err", "Aurora Dock — LỖI khởi động: " + ex.Message);
                MessageBox.Show("Khong khoi dong duoc giao dien (WebView2).\r\n\r\n" + ex.Message, "Aurora Dock");
                Quit();
            }
        }

        // ---------- tin nhan giao dien <-> host ----------
        void OnMsg(object s, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw = e.TryGetWebMessageAsString();
            ThreadPool.QueueUserWorkItem(delegate
            {
                object id = null; string resp;
                try
                {
                    var m = J.S.Deserialize<Dictionary<string, object>>(raw); id = m["id"];
                    var a = m.ContainsKey("a") ? m["a"] as Dictionary<string, object> : null;
                    object r = Dispatch(J.Str(m, "op"), a ?? new Dictionary<string, object>());
                    resp = J.S.Serialize(new Dictionary<string, object> { { "id", id }, { "ok", true }, { "d", r } });
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    App.Log("op error: " + ex.Message);
                    resp = J.S.Serialize(new Dictionary<string, object> { { "id", id }, { "ok", false }, { "e", ex.Message } });
                }
                Post(resp);
            });
        }
        void Post(string json)
        {
            try { BeginInvoke(new Action(delegate { try { wv.CoreWebView2.PostWebMessageAsString(json); } catch { } })); } catch { }
        }
        // ---------- Bieu tuong khay he thong: cho biet app dang chay hay chua (vang = dang khoi dong, xanh = chay, do = loi) ----------
        NotifyIcon tray; ToolStripMenuItem trayStatus; string trayState = "";
        static Icon MakeIcon(Color c)
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(Color.FromArgb(235, 40, 44, 70))) g.FillRectangle(b, 0, 4, 16, 8);          // "dock"
                    using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillRectangle(b, 1, 5, 14, 1);
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, 9, 0, 7, 7);                                          // den trang thai
                    using (var p = new Pen(Color.White, 1)) g.DrawEllipse(p, 9, 0, 6, 6);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
        void InitTray()
        {
            trayStatus = new ToolStripMenuItem("Aurora Dock — đang khởi động") { Enabled = false };
            var menu = new ContextMenuStrip();
            menu.Items.Add(trayStatus); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Mở Cài đặt", null, delegate { Post(J.S.Serialize(new Dictionary<string, object> { { "ev", "openPanel" } })); });
            menu.Items.Add("Khởi động lại", null, delegate { Relaunch(); });
            menu.Items.Add("Kiểm tra cập nhật", null, delegate { CheckUpdate(true); });
            menu.Items.Add("Exit — Thoát và trả lại taskbar", null, delegate { Quit(); });
            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            var pin = new System.Windows.Forms.Timer { Interval = 2500 }; pin.Tick += delegate { pin.Stop(); pin.Dispose(); PromoteTray(); }; pin.Start();
            tray.DoubleClick += delegate { Post(J.S.Serialize(new Dictionary<string, object> { { "ev", "openPanel" } })); };
            SetTray("busy", "Aurora Dock — đang khởi động");
            var upd = new System.Windows.Forms.Timer { Interval = 25000 };   // kiem tra ban dau sau 25 giay, roi moi 30 phut
            upd.Tick += delegate { upd.Interval = 30 * 60 * 1000; CheckUpdate(false); }; upd.Start();
        }
        bool updating, updNotified;
        void CheckUpdate(bool manual)
        {
            if (updating) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var r = Updater.Check();
                if (r == null) { if (manual) Post(J.S.Serialize(new Dictionary<string, object> { { "ev", "updateNone" } })); return; }
                r["ev"] = "update"; Post(J.S.Serialize(r));
                if (!updNotified || manual) { updNotified = true; try { BeginInvoke(new Action(delegate { if (tray != null) tray.ShowBalloonTip(6000, "Aurora Dock có bản cập nhật", Convert.ToString(r["msg"]), ToolTipIcon.Info); })); } catch { } }
            });
        }
        // Windows 11 cat bieu tuong moi vao muc an: ghim RIENG muc cua Aurora ra khay chinh de nhin thay (giong bam "Hien thi bieu tuong")
        static void PromoteTray()
        {
            try
            {
                string me = Application.ExecutablePath;
                using (var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
                {
                    if (root == null) return;
                    foreach (var name in root.GetSubKeyNames())
                        using (var k = root.OpenSubKey(name, true))
                        {
                            object p = k == null ? null : k.GetValue("ExecutablePath");
                            if (p != null && string.Equals(Convert.ToString(p), me, StringComparison.OrdinalIgnoreCase) && !(k.GetValue("IsPromoted") is int && (int)k.GetValue("IsPromoted") == 1))
                                k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                        }
                }
            }
            catch (Exception e) { App.Log("promote tray: " + e.Message); }
        }
        public void SetTray(string state, string tip)
        {
            if (tray == null) return;
            Action a = delegate
            {
                if (tray == null) return;
                if (trayState != state)
                {
                    trayState = state;
                    var old = tray.Icon; tray.Icon = MakeIcon(state == "ok" ? Color.FromArgb(52, 197, 138) : state == "err" ? Color.FromArgb(255, 90, 90) : Color.FromArgb(255, 190, 40));
                    if (old != null) old.Dispose();
                }
                tray.Text = tip.Length > 60 ? tip.Substring(0, 60) : tip; if (trayStatus != null) trayStatus.Text = tip;
            };
            if (InvokeRequired) BeginInvoke(a); else a();
        }
        public static Host Instance;
        public static CoreWebView2Environment Env;     // dung chung cho cua so Shift Desk: khong tao them bo tien trinh WebView2 thu hai
        public static string[] StartArgs = new string[0];
        // WebView sap han (VD loi driver GPU khi chay chung tien trinh): tra taskbar, mo lai app sau vai giay
        static void Relaunch()
        {
            try
            {
                string exe = Application.ExecutablePath, a = string.Join(" ", StartArgs);
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c ping -n 4 127.0.0.1 >nul & start \"\" \"" + exe + "\" " + a, CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch (Exception e) { App.Log("relaunch: " + e.Message); }
            if (Instance != null) Instance.BeginInvoke(new Action(Instance.Quit));
        }
        public T OnUi<T>(Func<T> f) { if (!IsHandleCreated) return default(T); if (!InvokeRequired) return f(); return (T)Invoke(f); }

        object Dispatch(string op, Dictionary<string, object> a)
        {
            switch (op)
            {
                case "ping": return "pong";
                case "info": return new Dictionary<string, object> { { "version", "2.0" }, { "dpi", OnUi<double>(Dpi) }, { "screen", Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height }, { "monitors", Screen.AllScreens.Length } };
                case "sys": return Sys.Snapshot();
                case "windows": return new Dictionary<string, object> { { "list", Wins.List(Handle) }, { "fg", lastFg.ToInt64() } };
                case "icon": return Wins.GetIcon(J.Str(a, "path"));
                case "activate": Wins.Activate(new IntPtr((long)J.Num(a, "h", 0)), true, lastFg); return true;
                case "close": Wins.Close(new IntPtr((long)J.Num(a, "h", 0))); return true;
                case "launch": return Launch(J.Str(a, "target"), J.Str(a, "args"));
                case "key": return Key(J.Str(a, "k"));
                case "screentime": return ScreenTime.Snapshot();
                case "files": return Files.Snapshot();
                case "claude": return ClaudeUse.Snapshot();
                case "media": return Media.Get();
                case "mediaCtl": Media.Ctl(J.Str(a, "cmd")); return true;
                case "http": return Http.Do(a);
                case "secretSet": Secrets.Set(J.Str(a, "name"), J.Str(a, "value")); return true;
                case "secretDel": Secrets.Del(J.Str(a, "name")); return true;
                case "secretList": return Secrets.Names();
                case "secretFetch": return Secrets.FetchUrlSecret(J.Str(a, "name"));
                case "notifApps": return Notifs.InstalledApps();
                case "notifBanners": return Notifs.SetBanners(a);
                case "notifs": return Notifs.Snapshot();
                case "claudeHook":
                    { string act = J.Str(a, "action"); return act == "install" ? ClaudeHook.Install() : act == "remove" ? ClaudeHook.Remove() : ClaudeHook.Status(); }
                case "claudeRate": return ClaudeHook.Rate();
                case "sdRead": return ShiftDesk.Read(a);
                case "sdClick": return ShiftDesk.Click(a);
                case "sdLogin": return ShiftDesk.ShowLogin(a);
                case "sdHide": return ShiftDesk.Hide();
                case "larkStatus": return Lark.Status();
                case "larkConfig": return Lark.Configure(a);
                case "larkLogin": return Lark.Login(a);
                case "larkLogout": return Lark.Logout();
                case "larkReply": return Lark.Reply(a);
                case "larkMentions": return Lark.Mentions(a);
                case "glass": return true;   // mau phu do CSS dam nhiem; blur la DWM blur-behind theo vung
                case "defaults": return Defaults();
                case "tsAppend": return Timesheet.Append(a);
                case "tsList": return Timesheet.List(a);
                case "theme": return Theme();
                case "autostart": return Autostart(J.Bool(a, "on", false), a.ContainsKey("on"));
                case "taskbar": Taskbar.Set(J.Str(a, "mode") ?? "keep"); return true;
                case "bar": return OnUi<object>(delegate { edge = J.Str(a, "edge") == "top" ? "top" : "bottom"; barSize = J.Num(a, "size", 80); reserve = J.Bool(a, "reserve", true); PositionBar(); return true; });
                case "region": return OnUi<object>(delegate { SetRegion(a.ContainsKey("rects") ? a["rects"] as System.Collections.IEnumerable : null); return true; });
                case "interactive": return OnUi<object>(delegate { SetInteractive(J.Bool(a, "on", false)); return true; });
                case "wallpaper": return OnUi<object>(delegate { return PickWallpaper(); });
                case "updateApply":
                    if (!Updater.Start()) throw new Exception("Khong cap nhat duoc (ban chay thu, hoac chua kiem tra)");
                    updating = true; BeginInvoke(new Action(Quit)); return true;
                case "quit": BeginInvoke(new Action(Quit)); return true;
                default: throw new Exception("Lenh khong biet: " + op);
            }
        }

        double Dpi() { uint d = N.GetDpiForWindow(Handle); return d == 0 ? 1.0 : d / 96.0; }

        // ---------- AppBar: chua vung lam viec de cua so maximize khong de len dock ----------
        N.APPBARDATA Abd() { var d = new N.APPBARDATA(); d.cbSize = (uint)Marshal.SizeOf(typeof(N.APPBARDATA)); d.hWnd = Handle; return d; }
        void PositionBar()
        {
            var b = Screen.PrimaryScreen.Bounds; int insetTop = 0, insetBottom = 0;
            if (reserve)
            {
                if (!barRegistered) { var n = Abd(); n.uCallbackMessage = appBarMsg; N.SHAppBarMessage(0, ref n); barRegistered = true; }
                uint e = edge == "top" ? 1u : 3u; int th = (int)Math.Round(barSize * Dpi());
                var d = Abd(); d.uEdge = e; d.rc.left = b.Left; d.rc.right = b.Right;
                if (e == 3) { d.rc.top = b.Bottom - th; d.rc.bottom = b.Bottom; } else { d.rc.top = b.Top; d.rc.bottom = b.Top + th; }
                N.SHAppBarMessage(2, ref d);   // Windows lui vao neu da co appbar khac (VD taskbar) o canh nay
                if (e == 3) { insetBottom = b.Bottom - d.rc.bottom; d.rc.top = d.rc.bottom - th; } else { insetTop = d.rc.top - b.Top; d.rc.bottom = d.rc.top + th; }
                N.SHAppBarMessage(3, ref d);
            }
            else
            {
                if (barRegistered) { var r = Abd(); N.SHAppBarMessage(1, ref r); barRegistered = false; }
                var wa = Screen.PrimaryScreen.WorkingArea;   // khong chua cho: van tranh de len taskbar
                if (edge == "top") insetTop = Math.Max(0, wa.Top - b.Top); else insetBottom = Math.Max(0, b.Bottom - wa.Bottom);
            }
            double sc = Dpi();
            try { Post(J.S.Serialize(new Dictionary<string, object> { { "ev", "inset" }, { "top", Math.Round(insetTop / sc, 1) }, { "bottom", Math.Round(insetBottom / sc, 1) } })); } catch { }
            // cua so phu toan man hinh (chi ve o cac vung duoc bao), tru 1px de Windows khong coi la "toan man hinh"
            N.SetWindowPos(Handle, N.HWND_TOPMOST, b.Left, b.Top, b.Width - 1, b.Height, N.SWP_NOACTIVATE);
        }

        // vung ve = hop cua dock + popover + panel...; ngoai vung thi chuot xuyen xuong cua so phia sau
        void SetRegion(System.Collections.IEnumerable rects)
        {
            double sc = Dpi(); IntPtr acc = N.CreateRectRgn(0, 0, 0, 0), blur = N.CreateRectRgn(0, 0, 0, 0);
            if (rects != null)
                foreach (var o in rects)
                {
                    var d = o as Dictionary<string, object>; if (d == null) continue;
                    int x = (int)Math.Floor(J.Num(d, "x", 0) * sc), y = (int)Math.Floor(J.Num(d, "y", 0) * sc);
                    int w = (int)Math.Ceiling(J.Num(d, "w", 0) * sc), h = (int)Math.Ceiling(J.Num(d, "h", 0) * sc), r = (int)Math.Round(J.Num(d, "r", 0) * sc);
                    if (w <= 0 || h <= 0) continue;
                    IntPtr rg = N.CreateRoundRectRgn(x, y, x + w + 1, y + h + 1, r * 2, r * 2); N.CombineRgn(acc, acc, rg, 2);
                    if (!J.Bool(d, "hit", false)) N.CombineRgn(blur, blur, rg, 2);   // "hit" = chi nhan chuot, khong lam mo (VD luc keo bong bong)
                    N.DeleteObject(rg);
                }
            N.SetWindowRgn(Handle, acc, true);   // he thong so huu `acc`
            int hr = N.BlurRegion(Handle, blur); N.DeleteObject(blur);
            if (hr != 0) App.Log("DwmEnableBlurBehindWindow hr=0x" + hr.ToString("x"));
        }

        void SetInteractive(bool on)
        {
            long ex = (long)N.GetWindowLongPtr(Handle, N.GWL_EXSTYLE);
            if (on) ex &= ~N.WS_EX_NOACTIVATE; else ex |= N.WS_EX_NOACTIVATE;
            N.SetWindowLongPtr(Handle, N.GWL_EXSTYLE, (IntPtr)ex);
            if (on) { N.SetForegroundWindow(Handle); wv.Focus(); }
            else if (N.GetForegroundWindow() == Handle && lastFg != IntPtr.Zero && N.IsWindow(lastFg))
                Wins.Activate(lastFg, false, IntPtr.Zero);   // tra focus ve app dang dung truoc do
        }

        object PickWallpaper()
        {
            using (var dlg = new OpenFileDialog { Filter = "Anh|*.jpg;*.jpeg;*.png;*.bmp", Title = "Chon hinh nen Windows" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                return N.SystemParametersInfo(0x14, 0, dlg.FileName, 3);
            }
        }

        // ---------- lenh he thong ----------
        static object Launch(string target, string args)
        {
            if (string.IsNullOrEmpty(target)) throw new Exception("thieu duong dan");
            Process.Start(new ProcessStartInfo { FileName = target, Arguments = args ?? "", UseShellExecute = true });
            return true;
        }
        static object Key(string k)
        {
            byte LWIN = 0x5B;
            Action<byte> dn = delegate(byte v) { N.keybd_event(v, 0, 0, UIntPtr.Zero); };
            Action<byte> up = delegate(byte v) { N.keybd_event(v, 0, 2, UIntPtr.Zero); };
            Action<byte, byte> combo = delegate(byte m, byte v) { dn(m); dn(v); up(v); up(m); };
            switch (k)
            {
                case "start": dn(LWIN); up(LWIN); break;
                case "desktop": combo(LWIN, 0x44); break;
                case "search": combo(LWIN, 0x53); break;
                case "taskview": combo(LWIN, 0x09); break;
                case "actioncenter": combo(LWIN, 0x41); break;
                case "play": dn(0xB3); up(0xB3); break;
                case "next": dn(0xB0); up(0xB0); break;
                case "prev": dn(0xB1); up(0xB1); break;
                default: throw new Exception("phim khong ho tro");
            }
            return true;
        }
        public static object Theme()
        {
            bool light = false;
            try { using (var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) { object v = rk == null ? null : rk.GetValue("AppsUseLightTheme"); light = v != null && Convert.ToInt32(v) == 1; } } catch { }
            return new Dictionary<string, object> { { "light", light } };
        }
        static object Defaults()
        {
            string win = Environment.GetEnvironmentVariable("WINDIR"), pf = Environment.GetEnvironmentVariable("ProgramFiles"), pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)"), la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string[] c = {
                win + @"\explorer.exe", pf86 + @"\Microsoft\Edge\Application\msedge.exe", pf + @"\Google\Chrome\Application\chrome.exe",
                la + @"\Programs\Microsoft VS Code\Code.exe", win + @"\System32\notepad.exe"
            };
            var l = new List<object>(); foreach (var p in c) if (File.Exists(p)) l.Add(p); return l;
        }
        static object Autostart(bool on, bool set)
        {
            const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (var rk = Registry.CurrentUser.OpenSubKey(key, true))
            {
                if (set) { if (on) rk.SetValue("AuroraDock", "\"" + Application.ExecutablePath + "\""); else rk.DeleteValue("AuroraDock", false); }
                return rk.GetValue("AuroraDock") != null;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (appBarMsg != 0 && (uint)m.Msg == appBarMsg)
            {
                int code = m.WParam.ToInt32();
                if (code == 2) N.SetWindowPos(Handle, m.LParam != IntPtr.Zero ? N.HWND_BOTTOM : N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                else if (code == 1) PositionBar();
            }
            else if (m.Msg == 0x007E || m.Msg == 0x02E0) { PositionBar(); }          // DISPLAYCHANGE / DPICHANGED
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_QUIT) { Quit(); }
            base.WndProc(ref m);
        }

        bool quitting;
        public void Quit()
        {
            if (quitting) return; quitting = true;
            try { N.UnregisterHotKey(Handle, HOTKEY_QUIT); } catch { }
            try { if (barRegistered) { var r = Abd(); N.SHAppBarMessage(1, ref r); barRegistered = false; } } catch { }
            try { Taskbar.Restore(); } catch { }
            try { Notifs.RestoreBanners(); } catch { }
            try { ScreenTime.Flush(); } catch { }
            try { ShiftDesk.Shutdown(); } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch { }
            Application.Exit();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Host.StartArgs = args;
            string dir = Path.GetDirectoryName(Application.ExecutablePath);

            if (args.Length > 0 && args[0] == "--restore-taskbar") { Taskbar.Restore(); Console.WriteLine("Da tra lai thanh taskbar Windows."); return 0; }
            if (args.Length > 1 && args[0] == "--op") return Cli(args[1], args.Length > 2 ? args[2] : "{}", args.Length > 3 ? args[3] : null);

            bool createdNew;
            using (var mtx = new Mutex(true, "Local\\AuroraDockHost", out createdNew))
            {
                if (!createdNew) return 0;
                // neu lan truoc bi crash khi dang an taskbar thi tra lai truoc
                if (Taskbar.NeedsRescue()) Taskbar.Restore();

                bool debug = Array.IndexOf(args, "--debug") >= 0;
                Host.noTaskbar = Array.IndexOf(args, "--no-taskbar") >= 0;
                string appDir = Path.Combine(dir, "app");                       // ban da cai
                if (!Directory.Exists(appDir)) appDir = Path.GetFullPath(Path.Combine(dir, @"..\app")); // chay tu thu muc phat trien
                if (!File.Exists(Path.Combine(appDir, "index.html"))) { MessageBox.Show("Khong thay thu muc app\\index.html canh host.", "Aurora Dock"); return 1; }

                Application.EnableVisualStyles();
                var host = new Host(appDir, debug);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { App.Log("ui exception: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { App.Log("fatal: " + e.ExceptionObject); try { Taskbar.Restore(); } catch { } };
                AppDomain.CurrentDomain.ProcessExit += delegate { try { Taskbar.Restore(); } catch { } };
                Application.Run(host);
                return 0;
            }
        }

        // Che do dong lenh de kiem tra du lieu that: AuroraHost.exe --op sys {} ketqua.json
        static int Cli(string op, string json, string outFile)
        {
            string text; int code = 0;
            try
            {
                object r;
                switch (op)
                {
                    case "sys": Sys.Snapshot(); Thread.Sleep(1200); r = Sys.Snapshot(); break;
                    case "claude": r = ClaudeUse.Snapshot(); break;
                    case "files": r = Files.Snapshot(); break;
                    case "media": r = Media.Get(); break;
                    case "windows": r = Wins.List(IntPtr.Zero); break;
                    case "http": r = Http.Do(J.S.Deserialize<Dictionary<string, object>>(json)); break;
                    case "theme": r = Host.Theme(); break;
                    default: r = "op khong ho tro trong CLI"; code = 2; break;
                }
                text = J.S.Serialize(r);
            }
            catch (Exception e) { while (e.InnerException != null) e = e.InnerException; text = "LOI: " + e.Message; code = 1; }
            if (outFile != null) File.WriteAllText(outFile, text); else Console.WriteLine(text);
            return code;
        }
    }
}


