// Aurora Dock host - cai/go "statusLine" cua Claude Code de nhan % han muc THAT (5 gio / 7 ngay) tu chinh Claude Code.
// An toan: sao luu settings.json truoc khi sua, giu nguyen moi khoa khac, luu statusLine cu de goi tiep va de khoi phuc khi go.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aurora
{
    static class ClaudeHook
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern uint GetShortPathName(string p, StringBuilder b, uint n);

        static string ConfigDir()
        {
            string e = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (!string.IsNullOrEmpty(e)) return e.Split(',')[0].Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }
        static string Settings { get { return Path.Combine(ConfigDir(), "settings.json"); } }
        static string PrevFile { get { return Path.Combine(App.Data, "claude-prev-statusline.json"); } }
        static string RateFile { get { return Path.Combine(App.Data, "claude-statusline.json"); } }

        static string ExePath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aurora-statusline.exe"); }
        // duong dan khong khoang trang, dau "/" : chay duoc ca trong Git Bash lan PowerShell
        static string Cmd()
        {
            string p = ExePath();
            if (p.IndexOf(' ') >= 0) { var sb = new StringBuilder(260); if (GetShortPathName(p, sb, 260) > 0) p = sb.ToString(); }   // chi khi co khoang trang
            return p.Replace('\\', '/');
        }
        static string Marker { get { return Path.Combine(App.Data, "claude-hook-cmd.txt"); } }
        static bool Ours(object sl)
        {
            var d = sl as Dictionary<string, object>; if (d == null) return false; string c = J.Str(d, "command") ?? "";
            if (c.IndexOf("aurora-statusline", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            try { return File.Exists(Marker) && string.Equals(c, File.ReadAllText(Marker).Trim(), StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }

        static Dictionary<string, object> Load()
        {
            if (!File.Exists(Settings)) return new Dictionary<string, object>();
            var d = J.S.Deserialize<Dictionary<string, object>>(File.ReadAllText(Settings)); return d ?? new Dictionary<string, object>();
        }
        static void Save(Dictionary<string, object> d)
        {
            Directory.CreateDirectory(ConfigDir());
            string bak = Settings + ".aurora-bak";
            if (File.Exists(Settings) && !File.Exists(bak)) File.Copy(Settings, bak);   // sao luu ban goc, chi lan dau
            File.WriteAllText(Settings, Pretty(J.S.Serialize(d)), new UTF8Encoding(false));
        }

        public static object Status()
        {
            var d = Load(); object sl = d.ContainsKey("statusLine") ? d["statusLine"] : null;
            return new Dictionary<string, object> {
                { "installed", Ours(sl) }, { "hasOther", sl != null && !Ours(sl) }, { "hasPrev", File.Exists(PrevFile) }, { "settings", Settings },
                { "exeFound", File.Exists(ExePath()) }, { "backup", File.Exists(Settings + ".aurora-bak") } };
        }

        public static object Install()
        {
            if (!File.Exists(ExePath())) throw new Exception("Khong thay aurora-statusline.exe canh AuroraHost.exe (hay build/cai dat lai)");
            var d = Load(); object sl = d.ContainsKey("statusLine") ? d["statusLine"] : null;
            if (sl != null && !Ours(sl)) { Directory.CreateDirectory(App.Data); File.WriteAllText(PrevFile, J.S.Serialize(sl)); }   // giu statusLine cu de goi tiep
            Directory.CreateDirectory(App.Data); File.WriteAllText(Marker, Cmd());
            d["statusLine"] = new Dictionary<string, object> { { "type", "command" }, { "command", Cmd() }, { "refreshInterval", 60 } };
            Save(d); return Status();
        }

        public static object Remove()
        {
            var d = Load();
            if (d.ContainsKey("statusLine") && Ours(d["statusLine"]))
            {
                if (File.Exists(PrevFile)) { d["statusLine"] = J.S.Deserialize<Dictionary<string, object>>(File.ReadAllText(PrevFile)); File.Delete(PrevFile); }
                else d.Remove("statusLine");
                Save(d);
            }
            return Status();
        }

        public static object Rate()
        {
            if (!File.Exists(RateFile)) return null;
            try { return J.S.Deserialize<Dictionary<string, object>>(File.ReadAllText(RateFile)); } catch { return null; }
        }

        // dinh dang JSON co thut le cho de doc (JavaScriptSerializer chi xuat 1 dong)
        static string Pretty(string j)
        {
            var sb = new StringBuilder(); int ind = 0; bool str = false;
            for (int i = 0; i < j.Length; i++)
            {
                char c = j[i];
                if (str) { sb.Append(c); if (c == '\\' && i + 1 < j.Length) { sb.Append(j[++i]); } else if (c == '"') str = false; continue; }
                switch (c)
                {
                    case '"': str = true; sb.Append(c); break;
                    case '{': case '[':
                        if (i + 1 < j.Length && (j[i + 1] == '}' || j[i + 1] == ']')) { sb.Append(c).Append(j[++i]); break; }
                        sb.Append(c).Append('\n').Append(' ', ++ind * 2); break;
                    case '}': case ']': sb.Append('\n').Append(' ', --ind * 2).Append(c); break;
                    case ',': sb.Append(c).Append('\n').Append(' ', ind * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString() + "\n";
        }
    }
}
