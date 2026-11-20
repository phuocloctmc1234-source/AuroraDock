// Anh dai dien (profile) cua tung cua so Chrome: doc ten o nut profile tren thanh cong cu (UI Automation),
// doi chieu voi "Local State" cua Chrome de lay anh Google Profile Picture.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Automation;

namespace Aurora
{
    static class ChromeProf
    {
        static readonly Dictionary<long, Dictionary<string, object>> byWin = new Dictionary<long, Dictionary<string, object>>();
        static readonly Dictionary<long, int> tries = new Dictionary<long, int>();
        static readonly Dictionary<string, string> pics = new Dictionary<string, string>();
        static List<Dictionary<string, object>> profiles; static DateTime profAt = DateTime.MinValue;
        static readonly object gate = new object();

        static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"); } }

        static void LoadProfiles()
        {
            if (profiles != null && (DateTime.UtcNow - profAt).TotalMinutes < 5) return;
            var l = new List<Dictionary<string, object>>();
            try
            {
                var d = J.S.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Root, "Local State")));
                var ic = ((Dictionary<string, object>)((Dictionary<string, object>)d["profile"])["info_cache"]);
                foreach (var kv in ic)
                {
                    var v = kv.Value as Dictionary<string, object>; if (v == null) continue;
                    l.Add(new Dictionary<string, object> { { "dir", kv.Key }, { "name", J.Str(v, "name") ?? "" }, { "gaia", J.Str(v, "gaia_name") ?? "" }, { "pic", J.Str(v, "gaia_picture_file_name") ?? "" }, { "user", J.Str(v, "user_name") ?? "" },
                        { "color", v.ContainsKey("default_avatar_fill_color") ? Convert.ToInt64(v["default_avatar_fill_color"]) : 0L } });
                }
            }
            catch (Exception e) { App.Log("chromeprof state: " + e.Message); }
            profiles = l; profAt = DateTime.UtcNow;
        }

        static string Norm(string s) { return (s ?? "").Trim().ToLowerInvariant(); }

        static Dictionary<string, object> Match(string btn)
        {
            string n = Norm(btn); int i = n.IndexOf(". "); if (i > 0) n = n.Substring(0, i);
            if (n.Length == 0) return null;
            if (n == "work" || n == "school")   // Chrome gan nhan "Work" cho tai khoan Google Workspace (khong phai gmail)
            {
                Dictionary<string, object> only = null; int cnt = 0;
                foreach (var p in profiles) { string u = Norm((string)p["user"]); int at = u.IndexOf('@'); if (at > 0 && !u.EndsWith("@gmail.com") && !u.EndsWith("@googlemail.com")) { only = p; cnt++; } }
                if (cnt == 1) return only;
            }
            foreach (var p in profiles) if (Norm((string)p["name"]) == n || Norm((string)p["gaia"]) == n) return p;
            foreach (var p in profiles) { string g = Norm((string)p["gaia"]), nm = Norm((string)p["name"]); if ((g.Length > 0 && (g.StartsWith(n) || n.StartsWith(g))) || (nm.Length > 0 && (nm.StartsWith(n) || n.StartsWith(nm)))) return p; }
            return null;
        }

        static string Pic(Dictionary<string, object> p)
        {
            string dir = (string)p["dir"], fn = (string)p["pic"];
            lock (pics)
            {
                string c; if (pics.TryGetValue(dir, out c)) return c;
                string res = "";
                try
                {
                    string f = Path.Combine(Root, dir, fn.Length > 0 ? fn : "Google Profile Picture.png");
                    if (File.Exists(f))
                        using (var src = new Bitmap(f)) using (var bm = new Bitmap(40, 40)) using (var g = Graphics.FromImage(bm))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, 40, 40);
                            using (var ms = new MemoryStream()) { bm.Save(ms, System.Drawing.Imaging.ImageFormat.Png); res = Convert.ToBase64String(ms.ToArray()); }
                        }
                }
                catch { }
                pics[dir] = res; return res;
            }
        }

        static string ProfileButton(IntPtr h)
        {
            var e = AutomationElement.FromHandle(h);
            var cond = new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.NameProperty, "Chrome"));
            var m = e.FindFirst(TreeScope.Descendants, cond);
            if (m == null) { Thread.Sleep(700); m = e.FindFirst(TreeScope.Descendants, cond); }   // lan dau: danh thuc cay accessibility cua Chromium
            if (m == null) return null;
            var prev = TreeWalker.ControlViewWalker.GetPreviousSibling(m);
            return prev == null ? null : prev.Current.Name;
        }

        // hs: danh sach handle cua so Chrome; tra ve { "<h>": { name, png, color } } cho cac cua so xac dinh duoc
        public static Dictionary<string, object> Get(List<long> hs)
        {
            var res = new Dictionary<string, object>();
            var todo = new List<long>();
            lock (gate)
            {
                foreach (var h in hs) { Dictionary<string, object> v; if (byWin.TryGetValue(h, out v)) res[h.ToString()] = v; else { int t; tries.TryGetValue(h, out t); if (t < 3) todo.Add(h); } }
                if (todo.Count == 0) return res;
                var th = new Thread(delegate()
                {
                    try
                    {
                        LoadProfiles();
                        foreach (var h in todo)
                        {
                            try
                            {
                                int t; tries.TryGetValue(h, out t); tries[h] = t + 1;
                                string nm = ProfileButton(new IntPtr(h)); var p = nm == null ? null : Match(nm); if (p == null) continue;
                                var v = new Dictionary<string, object> { { "name", p["name"] }, { "png", Pic(p) }, { "color", p["color"] } };
                                byWin[h] = v; res[h.ToString()] = v;
                            }
                            catch (Exception e) { App.Log("chromeprof " + h + ": " + e.Message); }
                        }
                    }
                    catch (Exception e) { App.Log("chromeprof: " + e.Message); }
                });
                th.SetApartmentState(ApartmentState.MTA); th.IsBackground = true; th.Start(); th.Join(20000);
            }
            return res;
        }
    }
}
