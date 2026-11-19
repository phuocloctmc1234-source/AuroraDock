// Tu cap nhat: so sanh commit moi nhat tren GitHub (nhanh main) voi version.txt trong thu muc cai dat.
// Bam "Cap nhat": tai ma nguon, thoat app cu, chay scripts\install.ps1 -Silent, mo lai app moi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Aurora
{
    static class Updater
    {
        const string Repo = "phuocloctmc1234-source/AuroraDock";
        public static string Latest, LatestMsg;

        static string Dir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
        static string VerFile { get { return Path.Combine(Dir, "version.txt"); } }
        public static string Current() { try { return File.Exists(VerFile) ? File.ReadAllText(VerFile).Trim() : ""; } catch { return ""; } }
        // chi cap nhat duoc ban da cai (co thu muc app canh exe)
        public static bool Installed { get { return Directory.Exists(Path.Combine(Dir, "app")); } }

        // tra ve null neu khong co ban moi / khong kiem tra duoc
        public static Dictionary<string, object> Check()
        {
            if (!Installed) return null;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/commits/main");
                req.UserAgent = "AuroraDock-Updater"; req.Accept = "application/vnd.github+json"; req.Timeout = 15000;
                string body;
                using (var resp = req.GetResponse()) using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) body = rd.ReadToEnd();
                var d = J.S.Deserialize<Dictionary<string, object>>(body);
                string sha = J.Str(d, "sha"); if (string.IsNullOrEmpty(sha)) return null;
                string msg = "";
                var c = d.ContainsKey("commit") ? d["commit"] as Dictionary<string, object> : null;
                if (c != null) { msg = J.Str(c, "message") ?? ""; int i = msg.IndexOf('\n'); if (i > 0) msg = msg.Substring(0, i); }
                Latest = sha; LatestMsg = msg;
                if (string.Equals(Current(), sha, StringComparison.OrdinalIgnoreCase)) return null;
                return new Dictionary<string, object> { { "sha", sha.Substring(0, 7) }, { "msg", msg } };
            }
            catch (Exception e) { App.Log("update check: " + e.Message); return null; }
        }

        // Tao script cap nhat chay doc lap roi tra ve true; goi Quit() sau do.
        public static bool Start()
        {
            if (!Installed || string.IsNullOrEmpty(Latest)) return false;
            string tmp = Path.Combine(Path.GetTempPath(), "AuroraUpdate");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(tmp);
            string ps = Path.Combine(tmp, "update.ps1"), log = Path.Combine(App.Data, "update.log");
            string args = string.Join(" ", Host.StartArgs);
            string script =
                "$ErrorActionPreference='Stop'\r\n" +
                "$log='" + log.Replace("'", "''") + "'\r\n" +
                "function L($m){ try{ Add-Content -Path $log -Value ((Get-Date -f s)+' '+$m) }catch{} }\r\n" +
                "$exe='" + Application.ExecutablePath.Replace("'", "''") + "'\r\n" +
                "try {\r\n" +
                "  Start-Sleep -Seconds 2\r\n" +
                "  [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12\r\n" +
                "  $zip=Join-Path '" + tmp.Replace("'", "''") + "' 'src.zip'\r\n" +
                "  $x=Join-Path '" + tmp.Replace("'", "''") + "' 'src'\r\n" +
                "  L 'download " + Latest + "'\r\n" +
                "  Invoke-WebRequest -UseBasicParsing -Uri 'https://codeload.github.com/" + Repo + "/zip/" + Latest + "' -OutFile $zip\r\n" +
                "  Expand-Archive -Path $zip -DestinationPath $x -Force\r\n" +
                "  $root=(Get-ChildItem $x -Directory | Select-Object -First 1).FullName\r\n" +
                "  & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\\install.ps1') -Silent -Version '" + Latest + "'\r\n" +
                "  L 'installed'\r\n" +
                "} catch { L ('ERROR '+$_) }\r\n" +
                "Start-Process -FilePath $exe" + (args.Length > 0 ? " -ArgumentList '" + args.Replace("'", "''") + "'" : "") + "\r\n";
            File.WriteAllText(ps, script, new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ps + "\"", CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            return true;
        }
    }
}
