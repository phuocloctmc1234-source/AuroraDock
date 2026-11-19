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
        readonly WebView2 wv = new WebView2();
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
            PositionBar();
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
                // Cờ giảm RAM (đo trên máy 8GB: Private 327 -> 212 MB, bớt 1 tiến trình): GPU chạy chung tiến trình trình duyệt,
                // chỉ 1 tiến trình render, và không để Chromium tự coi cửa sổ là "bị che" rồi tính toán lại liên tục.
                string flags = Environment.GetEnvironmentVariable("AURORA_WV_FLAGS") ?? "--in-process-gpu --renderer-process-limit=1 --disable-features=CalculateNativeWinOcclusion";   // biến môi trường chỉ để đo thử
                var opts = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = flags + (debug ? " --remote-debugging-port=9333" : "") };
                // mo lai ngay sau khi tat: WebView2 cu co the chua thoat xong va con giu thu muc (0x800700AA) -> thu lai vai lan
                for (int attempt = 1; ; attempt++)
                {
                    bool busy = false;
                    try { Env = await CoreWebView2Environment.CreateAsync(null, ud, opts); await wv.EnsureCoreWebView2Async(Env); break; }
                    catch (System.Runtime.InteropServices.COMException ce) { if (attempt >= 8 || (uint)ce.ErrorCode != 0x800700AA) throw; busy = true; App.Log("webview busy, thu lai " + attempt); }
                    if (busy) await System.Threading.Tasks.Task.Delay(2500);
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
                c.Navigate("https://aurora.local/index.html?host=1&t=" + App.NowMs() + (noTaskbar ? "&notb=1" : ""));
            }
            catch (Exception ex)
            {
                App.Log("boot: " + ex);
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
            try { ScreenTime.Flush(); } catch { }
            try { ShiftDesk.Shutdown(); } catch { }
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
