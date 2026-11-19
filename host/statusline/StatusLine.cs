// aurora-statusline.exe: Claude Code goi file nay (statusLine) va dua JSON len stdin.
// Ta luu phan rate_limits (% hat muc 5 gio / 7 ngay + gio reset THAT do Claude Code bao) vao %APPDATA%\AuroraDock\claude-statusline.json
// de Aurora Dock doc. Neu truoc do ban da co statusLine rieng thi chay no va in lai ket qua, nen khong mat gi.
// Chay bang console (khong phai GUI) de Claude Code cho ket thuc dung cach. Luon thoat voi ma 0, khong bao gio lam hong status line.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

static class SL
{
    static readonly JavaScriptSerializer J = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static string Data { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuroraDock"); } }

    static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
    static object G(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v : null; }
    static double? Num(object o) { try { return o == null ? (double?)null : Convert.ToDouble(o); } catch { return null; } }

    static Dictionary<string, object> Win(object w)
    {
        var d = D(w); if (d == null) return null;
        double? p = Num(G(d, "used_percentage")), r = Num(G(d, "resets_at")); if (p == null) return null;
        return new Dictionary<string, object> { { "pct", p.Value }, { "resetsAt", r == null ? 0 : r.Value } };
    }

    static int Main()
    {
        string input = "";
        try { using (var r = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false))) input = r.ReadToEnd(); } catch { }
        try { Save(input); } catch { }
        try { Chain(input); } catch { }
        return 0;
    }

    static void Save(string input)
    {
        var o = D(J.DeserializeObject(input)); if (o == null) return;
        Directory.CreateDirectory(Data);
        string file = Path.Combine(Data, "claude-statusline.json");
        Dictionary<string, object> old = null;
        try { if (File.Exists(file)) old = D(J.DeserializeObject(File.ReadAllText(file))); } catch { }
        var rl = D(G(o, "rate_limits"));
        var res = new Dictionary<string, object>();
        res["at"] = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        res["session"] = G(o, "session_id");
        var m = D(G(o, "model")); res["model"] = m == null ? null : (G(m, "display_name") ?? G(m, "id"));
        var cw = D(G(o, "context_window")); res["ctxPct"] = cw == null ? null : G(cw, "used_percentage");
        var cost = D(G(o, "cost")); res["costUsd"] = cost == null ? null : G(cost, "total_cost_usd");
        // rate_limits co the vang o vai lan goi dau: giu gia tri cu neu chua het han
        var five = rl == null ? null : Win(G(rl, "five_hour")); var seven = rl == null ? null : Win(G(rl, "seven_day"));
        long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        if (five == null && old != null) { var f = D(G(old, "fiveHour")); if (f != null && Convert.ToInt64(G(f, "resetsAt")) > now) five = f; }
        if (seven == null && old != null) { var s = D(G(old, "sevenDay")); if (s != null && Convert.ToInt64(G(s, "resetsAt")) > now) seven = s; }
        res["fiveHour"] = five; res["sevenDay"] = seven;
        string tmp = file + ".tmp"; File.WriteAllText(tmp, J.Serialize(res), new UTF8Encoding(false));
        if (File.Exists(file)) File.Delete(file); File.Move(tmp, file);
    }

    static void Chain(string input)
    {
        string prev = Path.Combine(Data, "claude-prev-statusline.json"); if (!File.Exists(prev)) return;
        var d = D(J.DeserializeObject(File.ReadAllText(prev))); string cmd = d == null ? null : Convert.ToString(G(d, "command")); if (string.IsNullOrEmpty(cmd)) return;
        string bash = @"C:\Program Files\Git\bin\bash.exe";
        var psi = new ProcessStartInfo { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        if (File.Exists(bash)) { psi.FileName = bash; psi.Arguments = "-c \"" + cmd.Replace("\"", "\\\"") + "\""; }
        else { psi.FileName = "powershell.exe"; psi.Arguments = "-NoProfile -Command \"" + cmd.Replace("\"", "\\\"") + "\""; }
        using (var p = Process.Start(psi))
        {
            p.StandardInput.Write(input); p.StandardInput.Close();
            string outp = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(4000)) { try { p.Kill(); } catch { } }
            var so = Console.OpenStandardOutput(); var b = new UTF8Encoding(false).GetBytes(outp); so.Write(b, 0, b.Length); so.Flush();
        }
    }
}
