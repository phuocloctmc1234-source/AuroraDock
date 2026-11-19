// Aurora Dock host - ket noi Shift Desk (Apps Script web app cua cong ty, chi co giao dien, khong co API).
// Cach lam: mo trang trong mot WebView2 RIENG (phien dang nhap cua ban luu ma hoa tren may), doc chu tren trang
// va bam dung nut "Bat dau nghi" / "Ket thuc nghi" khi duoc yeu cau. Khong luu hay gui mat khau di dau.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Aurora
{
    class SdForm : Form
    {
        public readonly WebView2 wv = new WebView2();
        public bool allowClose, quiet = true;
        public SdForm()
        {
            Text = "Shift Desk â€” Aurora Dock"; Width = 1100; Height = 820; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            wv.Dock = DockStyle.Fill; Controls.Add(wv);
        }
        protected override bool ShowWithoutActivation { get { return quiet; } }
        protected override void OnFormClosing(FormClosingEventArgs e) { if (!allowClose) { e.Cancel = true; ShiftDesk.HideWindow(); } base.OnFormClosing(e); }
    }

    static class ShiftDesk
    {
        static readonly object gate = new object(), fl = new object();
        static SdForm form; static readonly List<CoreWebView2Frame> frames = new List<CoreWebView2Frame>();
        static volatile bool loaded; static DateTime lastLoad = DateTime.MinValue; static string loadedUrl = "";
        const string SNAP = "(function(){try{var t=document.body?document.body.innerText:'';var b=[].slice.call(document.querySelectorAll('button,[role=button],input[type=button],input[type=submit],a')).filter(function(e){return e.offsetParent!==null}).map(function(e){return (e.innerText||e.value||'').trim()}).filter(function(x){return x});return JSON.stringify({t:t,b:b,h:location.host})}catch(e){return JSON.stringify({t:'',b:[],h:'',err:String(e)})}})()";
        static List<object> lastDiag = new List<object>();

        static T Ui<T>(Func<T> f) { return Host.Instance.OnUi(f); }

        static void CheckUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new Exception("Chua nhap URL Shift Desk");
            Uri u; if (!Uri.TryCreate(url, UriKind.Absolute, out u)) throw new Exception("URL khong hop le");
            bool loop = u.Host == "127.0.0.1" || u.Host == "localhost";
            if (u.Scheme != "https" && !(loop && u.Scheme == "http")) throw new Exception("Chi cho phep https://");
        }

        static void Track(CoreWebView2Frame f)
        {
            lock (fl) frames.Add(f);
            f.Destroyed += delegate { lock (fl) frames.Remove(f); };
            f.FrameCreated += delegate(object s, CoreWebView2FrameCreatedEventArgs e) { Track(e.Frame); };
        }

        static void Ensure(string url)
        {
            CheckUrl(url);
            if (form != null) return;
            Ui<object>(delegate { form = new SdForm(); form.Location = new Point(-32000, -32000); form.Show(); return null; });
            string ud = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraDock", "WebView2-shiftdesk");
            // dung chung moi truong voi giao dien chinh: khong sinh them bo tien trinh WebView2 thu hai (tiet kiem ~100+ MB)
            CoreWebView2Environment env = Host.Env;
            if (env == null)
            {
                var et = Ui<Task<CoreWebView2Environment>>(() => CoreWebView2Environment.CreateAsync(null, ud));
                if (!et.Wait(30000)) throw new Exception("Khong tao duoc moi truong WebView2"); env = et.Result;
            }
            var init = Ui<Task>(() => form.wv.EnsureCoreWebView2Async(env));
            if (!init.Wait(30000)) throw new Exception("WebView2 khong khoi dong duoc");
            Ui<object>(delegate
            {
                var c = form.wv.CoreWebView2;
                c.FrameCreated += delegate(object s, CoreWebView2FrameCreatedEventArgs e) { Track(e.Frame); };
                c.NavigationCompleted += delegate { loaded = true; };
                return null;
            });
        }

        // Cua so an chi "tinh" luc dang doc: doc xong thi an di va cho WebView ngu (giam CPU/RAM luc roi)
        static void Wake()
        {
            Ui<object>(delegate { form.wv.Visible = true; var c = form.wv.CoreWebView2; if (c.IsSuspended) c.Resume(); c.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal; return null; });
        }
        static void Rest()
        {
            try
            {
                Ui<object>(delegate
                {
                    // KHONG an/ngu WebView: Apps Script chi dung giao dien khi trang "visible" (da gap loi: khung bao hidden, khong co chu nao).
                    // Chi ha muc bo nho; cua sinh van nam ngoai man hinh.
                    if (form == null || !form.quiet) return null;
                    form.wv.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low; return null;
                });
            }
            catch (Exception e) { App.Log("sd rest: " + e.Message); }
        }

        static void Load(string url, int maxAgeSec)
        {
            if (loadedUrl == url && (DateTime.UtcNow - lastLoad).TotalSeconds <= maxAgeSec) return;
            loaded = false; lock (fl) frames.Clear();
            Ui<object>(delegate { form.wv.CoreWebView2.Navigate(url); return null; });
            for (int i = 0; i < 40 && !loaded; i++) Thread.Sleep(500);
            loadedUrl = url; lastLoad = DateTime.UtcNow;
        }

        static string Exec(CoreWebView2Frame fr, string js)
        {
            var t = Ui<Task<string>>(() => fr == null ? form.wv.CoreWebView2.ExecuteScriptAsync(js) : fr.ExecuteScriptAsync(js));
            if (!t.Wait(8000)) throw new Exception("script timeout");
            string r = t.Result; if (string.IsNullOrEmpty(r) || r == "null") return "{}";   // khung chan script (sandbox) tra ve null
            return J.S.Deserialize<string>(r) ?? "{}";
        }

        class Snapshot { public CoreWebView2Frame Frame; public string Text = ""; public List<object> Buttons = new List<object>(); public bool Found; }

        static Snapshot Snap(string marker)
        {
            var cands = new List<CoreWebView2Frame>(); lock (fl) cands.AddRange(frames);
            cands.Insert(0, null);   // null = khung chinh
            Snapshot best = null; var diag = new List<object>(); lastDiag = diag;
            foreach (var fr in cands)
            {
                try
                {
                    var o = J.S.Deserialize<Dictionary<string, object>>(Exec(fr, SNAP)); string t = J.Str(o, "t") ?? "";
                    diag.Add((fr == null ? "khung chinh " : "khung con ") + (J.Str(o, "h") ?? "") + " chu=" + t.Length + " nut=" + (o.ContainsKey("b") ? ((System.Collections.ICollection)o["b"]).Count : 0));
                    bool hit = false; foreach (var mk in marker.Split('|')) if (mk != "" && t.IndexOf(mk, StringComparison.OrdinalIgnoreCase) >= 0) hit = true;   // nhieu dau hieu, chi can 1
                    var s = new Snapshot { Frame = fr, Text = t, Found = hit };
                    if (o.ContainsKey("b")) foreach (var b in (System.Collections.IEnumerable)o["b"]) s.Buttons.Add(Convert.ToString(b));
                    if (s.Found) return s;
                    if (best == null || t.Length > best.Text.Length) best = s;
                }
                catch (Exception e) { App.Log("sd snap: " + e.Message); diag.Add("loi doc khung: " + e.Message); }
            }
            return best ?? new Snapshot();
        }

        static bool NeedLogin(string top) { return top.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) >= 0 || top.IndexOf("ServiceLogin", StringComparison.OrdinalIgnoreCase) >= 0; }

        // Doc trang. maxAge = bao nhieu giay thi tai lai trang (gan moc quan trong thi truyen so nho)
        public static object Read(Dictionary<string, object> a)
        {
            lock (gate)
            {
                string url = J.Str(a, "url"), marker = J.Str(a, "marker") ?? "Giờ nghỉ của bạn|Đợt nghỉ tiếp theo|Bắt đầu ca"; int maxAge = (int)J.Num(a, "maxAge", 50);
                Ensure(url); Wake();
                try
                {
                    Load(url, maxAge);
                    Snapshot s = null;
                    for (int i = 0; i < 40; i++) { s = Snap(marker); if (s.Found) break; string top0 = Ui<string>(() => form.wv.CoreWebView2.Source); if (NeedLogin(top0) && i >= 4 && s.Text.Length > 0) break; Thread.Sleep(500); }
                    string top = Ui<string>(() => form.wv.CoreWebView2.Source);
                    return new Dictionary<string, object> { { "url", top }, { "needLogin", NeedLogin(top) }, { "found", s.Found }, { "text", s.Text }, { "buttons", s.Buttons }, { "diag", lastDiag }, { "at", App.NowMs() } };
                }
                finally { Rest(); }
            }
        }

        public static object Click(Dictionary<string, object> a)
        {
            lock (gate)
            {
                string url = J.Str(a, "url"), what = J.Str(a, "what"), marker = J.Str(a, "marker") ?? "Giờ nghỉ của bạn|Đợt nghỉ tiếp theo|Bắt đầu ca";
                string re = what == "startBreak" ? "^\\s*báº¯t Ä‘áº§u nghá»‰\\s*$" : what == "endBreak" ? "^\\s*káº¿t thÃºc nghá»‰\\s*$" : null;
                if (re == null) throw new Exception("Hanh dong khong ho tro");
                Ensure(url); Wake();
                try
                {
                var s = Snap(marker);
                if (!s.Found) throw new Exception("Khong thay trang Shift Desk (can dang nhap hoac chua tai xong)");
                string js = "(function(re){var els=[].slice.call(document.querySelectorAll('button,[role=button],input[type=button],input[type=submit],a'));for(var i=0;i<els.length;i++){var e=els[i],tx=(e.innerText||e.value||'').trim();if(new RegExp(re,'i').test(tx)&&!e.disabled&&e.offsetParent!==null){e.click();return JSON.stringify({clicked:true,text:tx})}}return JSON.stringify({clicked:false})})(" + J.S.Serialize(re) + ")";
                var o = J.S.Deserialize<Dictionary<string, object>>(Exec(s.Frame, js));
                if (!J.Bool(o, "clicked", false)) throw new Exception("Khong thay nut " + (what == "startBreak" ? "'Bat dau nghi'" : "'Ket thuc nghi'") + " tren trang (trang thai co the da doi)");
                lastLoad = DateTime.MinValue;   // lan doc tiep theo tai lai de thay trang thai moi
                return new Dictionary<string, object> { { "clicked", true }, { "text", J.Str(o, "text") } };
                }
                finally { Rest(); }
            }
        }

        public static object ShowLogin(Dictionary<string, object> a)
        {
            string url = J.Str(a, "url"); lock (gate) { Ensure(url); Wake(); }
            Ui<object>(delegate
            {
                var wa = Screen.PrimaryScreen.WorkingArea; form.quiet = false; form.ShowInTaskbar = true;
                form.Size = new Size(Math.Min(1100, wa.Width - 80), Math.Min(820, wa.Height - 80));
                form.Location = new Point(wa.Left + (wa.Width - form.Width) / 2, wa.Top + (wa.Height - form.Height) / 2);
                form.Activate(); return null;
            });
            lock (gate) { Load(url, 0); }
            return true;
        }

        public static void HideWindow()
        {
            if (form == null) return;
            Action f = delegate { form.ShowInTaskbar = false; form.Location = new Point(-32000, -32000); form.quiet = true; Rest(); };
            if (Host.Instance.InvokeRequired) Host.Instance.BeginInvoke(f); else f();
        }
        public static object Hide() { HideWindow(); return true; }

        public static void Shutdown()
        {
            try { if (form != null) { form.allowClose = true; form.Close(); } } catch { }
        }
    }
}

