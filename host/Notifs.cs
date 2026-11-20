// Aurora Dock host - doc thong bao (toast) THAT cua Windows tu Trung tam thong bao.
// Windows luu cac toast dang nam trong Trung tam thong bao vao wpndatabase.db (SQLite). Host sao chep tep (chi khi no doi),
// mo ban sao bang winsqlite3.dll co san cua Windows va doc chu cua toast. Khong gui di dau, khong sua gi tren he thong.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace Aurora
{
    static class Notifs
    {
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] file, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int n, out IntPtr st, IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr st);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr st, int i);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr st, int i);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_blob(IntPtr st, int i);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr st, int i);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr st);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);

        static readonly object L = new object();
        static string stamp = ""; static List<object> cache = new List<object>(); static string lastErr = "";

        static string Dir
        {
            get
            {
                string o = Environment.GetEnvironmentVariable("AURORA_WPN_DIR");   // chi de kiem thu bang CSDL gia
                return !string.IsNullOrEmpty(o) ? o : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Notifications");
            }
        }

        static bool ToastEnabled()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications")) { object v = k == null ? null : k.GetValue("ToastEnabled"); return v == null || Convert.ToInt32(v) != 0; } }
            catch { return true; }
        }

        static string Stamp(string db)
        {
            var sb = new StringBuilder();
            foreach (var f in new[] { db, db + "-wal" }) { try { var fi = new FileInfo(f); sb.Append(fi.Exists ? fi.Length + "@" + fi.LastWriteTimeUtc.Ticks : "-").Append('|'); } catch { sb.Append("?|"); } }
            return sb.ToString();
        }

        static void CopyShared(string src, string dst)
        {
            if (!File.Exists(src)) { try { if (File.Exists(dst)) File.Delete(dst); } catch { } return; }
            using (var a = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var b = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None)) a.CopyTo(b);
        }

        public static object Snapshot()
        {
            lock (L)
            {
                string db = Path.Combine(Dir, "wpndatabase.db");
                var res = new Dictionary<string, object> { { "enabled", ToastEnabled() }, { "at", App.NowMs() } };
                if (!File.Exists(db)) { res["ok"] = false; res["error"] = "Khong thay co so du lieu thong bao cua Windows"; res["items"] = new List<object>(); return res; }
                try
                {
                    string st = Stamp(db);
                    if (st != stamp)
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), "aurora-wpn"); Directory.CreateDirectory(tmp);
                        string c = Path.Combine(tmp, "wpndatabase.db");
                        CopyShared(db, c); CopyShared(db + "-wal", c + "-wal"); CopyShared(db + "-shm", c + "-shm");
                        cache = Read(c); stamp = st; lastErr = "";
                    }
                    res["ok"] = true; res["items"] = cache;
                }
                catch (Exception e) { lastErr = e.Message; res["ok"] = false; res["error"] = e.Message; res["items"] = cache; App.Log("notifs: " + e.Message); }
                return res;
            }
        }

        // ---------- An banner (popup goc man hinh) cua Windows cho app da bat bong bong; van giu trong Trung tam thong bao de doc ----------
        const string NSET = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\";
        static string lastSig = ""; static DateTime lastApply = DateTime.MinValue;
        static readonly HashSet<string> hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static object SetBanners(Dictionary<string, object> a)
        {
            var want = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var l = a != null && a.ContainsKey("aumids") ? a["aumids"] as System.Collections.IEnumerable : null;
            if (l != null) foreach (var o in l) { string s = Convert.ToString(o); if (!string.IsNullOrEmpty(s)) want.Add(s); }
            lock (hidden)
            {
                // khong gianh voi Cai dat Windows: chi ap lai toi da 1 lan / 30 giay (tru khi danh sach app doi)
                string sig = string.Join("|", want); var now = DateTime.UtcNow;
                if (sig == lastSig && (now - lastApply).TotalSeconds < 30) return hidden.Count;
                lastSig = sig; lastApply = now;
                foreach (var id in new List<string>(hidden)) if (!want.Contains(id)) { Banner(id, true); hidden.Remove(id); }
                foreach (var id in want) if (Banner(id, false)) hidden.Add(id);
                return hidden.Count;
            }
        }
        public static void RestoreBanners() { lock (hidden) { foreach (var id in hidden) Banner(id, true); hidden.Clear(); } }
        static bool Banner(string aumid, bool show)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(NSET + aumid, true))
                {
                    if (k == null) return false;   // KHONG tu tao muc: Windows tao khi app gui thong bao dau tien
                    // Chi dong vao muc do WINDOWS tao (co LastNotificationAddedTime) cua app dang bat thong bao; muc tu che/thieu gia tri lam Cai dat Windows khong bat lai duoc
                    if (!show && (k.GetValue("LastNotificationAddedTime") == null || Convert.ToInt32(k.GetValue("Enabled", 1)) == 0 || Convert.ToInt32(k.GetValue("ShowInActionCenter", 1)) == 0)) return false;
                    if (show) k.DeleteValue("ShowBanner", false); else k.SetValue("ShowBanner", 0, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch (Exception e) { App.Log("banner " + aumid + ": " + e.Message); return false; }
        }

        // ---------- danh sach app da cai (de chon bat bong bong truoc khi chung gui thong bao dau tien) ----------
        static List<object> apps; static readonly object AL = new object();
        public static object InstalledApps()
        {
            lock (AL)
            {
                if (apps != null) return apps;
                var l = new List<object>();
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo { FileName = "powershell.exe", Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; Get-StartApps | ForEach-Object { $_.Name + [char]9 + $_.AppID }\"", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                    using (var p = System.Diagnostics.Process.Start(psi))
                    {
                        string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(20000);
                        foreach (var ln in o.Split('\n')) { var t = ln.TrimEnd('\r').Split('\t'); if (t.Length == 2 && t[1].Length > 0 && !t[1].Contains("/") && !t[1].Contains("\\")) l.Add(new Dictionary<string, object> { { "name", t[0] }, { "aumid", t[1] } }); }
                    }
                }
                catch (Exception e) { App.Log("apps: " + e.Message); }
                apps = l; return apps;
            }
        }

        static byte[] Z(string s) { var b = Encoding.UTF8.GetBytes(s); var r = new byte[b.Length + 1]; Array.Copy(b, r, b.Length); return r; }
        static string Txt(IntPtr p) { return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p) == null ? "" : Utf8(p); }
        static string Utf8(IntPtr p) { int n = 0; while (Marshal.ReadByte(p, n) != 0) n++; var b = new byte[n]; Marshal.Copy(p, b, 0, n); return Encoding.UTF8.GetString(b); }

        static List<object> Read(string file)
        {
            IntPtr db;
            if (sqlite3_open_v2(Z(file), out db, 2 /*READWRITE tren ban sao*/, IntPtr.Zero) != 0) throw new Exception("Khong mo duoc co so du lieu thong bao");
            var items = new List<object>();
            try
            {
                IntPtr stmt;
                const string sql = "SELECT n.Id, n.ArrivalTime, n.Payload, h.PrimaryId FROM Notification n JOIN NotificationHandler h ON n.HandlerId = h.RecordId WHERE n.Type = 'toast' ORDER BY n.ArrivalTime DESC LIMIT 80";
                if (sqlite3_prepare_v2(db, Z(sql), -1, out stmt, IntPtr.Zero) != 0) throw new Exception("SQL: " + Utf8(sqlite3_errmsg(db)));
                try
                {
                    while (sqlite3_step(stmt) == 100)
                    {
                        long id = sqlite3_column_int64(stmt, 0), ft = sqlite3_column_int64(stmt, 1);
                        int n = sqlite3_column_bytes(stmt, 2); var buf = new byte[n]; IntPtr bp = sqlite3_column_blob(stmt, 2); if (n > 0 && bp != IntPtr.Zero) Marshal.Copy(bp, buf, 0, n);
                        string aumid = Utf8(sqlite3_column_text(stmt, 3));
                        var it = Parse(id, ft, aumid, Encoding.UTF8.GetString(buf).Trim('\0', '﻿', ' ', '\r', '\n'));
                        if (it != null) items.Add(it);
                    }
                }
                finally { sqlite3_finalize(stmt); }
            }
            finally { sqlite3_close(db); }
            return items;
        }

        static Dictionary<string, object> Parse(long id, long ft, string aumid, string xml)
        {
            string title = "", body = "", site = "";
            try
            {
                var d = new XmlDocument(); d.LoadXml(xml);
                var texts = new List<string>();
                foreach (XmlNode t in d.SelectNodes("//text"))
                {
                    string v = (t.InnerText ?? "").Trim(); if (v == "") continue;
                    XmlAttribute pl = t.Attributes == null ? null : t.Attributes["placement"];
                    if (pl != null && pl.Value == "attribution") site = v; else texts.Add(v);
                }
                if (texts.Count > 0) title = texts[0];
                if (texts.Count > 1) body = string.Join(" ", texts.GetRange(1, texts.Count - 1).ToArray());
            }
            catch { return null; }
            if (title == "" && body == "") return null;
            if (body.Length > 400) body = body.Substring(0, 400) + "…";
            long ms = ft / 10000 - 11644473600000L;
            return new Dictionary<string, object> { { "id", "n" + id + "@" + ft }, { "aumid", aumid }, { "app", Friendly(aumid) }, { "site", site }, { "title", title }, { "text", body }, { "t", ms } };
        }

        static string Friendly(string aumid)
        {
            string a = aumid ?? ""; string l = a.ToLowerInvariant();
            if (l == "chrome" || l.StartsWith("chrome.")) return "Chrome"; if (l.Contains("msedge")) return "Edge";
            if (l.Contains("lark")) return "Lark"; if (l.Contains("feishu")) return "Feishu"; if (l.Contains("messenger")) return "Messenger"; if (l.Contains("instagram")) return "Instagram";
            if (l.Contains("facebook")) return "Facebook"; if (l.Contains("whatsapp")) return "WhatsApp"; if (l.Contains("telegram")) return "Telegram"; if (l.Contains("zalo")) return "Zalo";
            if (l.Contains("discord")) return "Discord"; if (l.Contains("slack")) return "Slack"; if (l.Contains("teams")) return "Teams";
            int bang = a.IndexOf('!'); if (bang > 0) a = a.Substring(0, bang);
            int us = a.IndexOf('_'); if (us > 0) a = a.Substring(0, us);
            int dot = a.LastIndexOf('.'); if (dot > 0 && dot < a.Length - 1) a = a.Substring(dot + 1);
            return a.Length > 28 ? a.Substring(0, 28) : a;
        }
    }
}
