// Aurora Dock host - ket noi Lark (OAuth nguoi dung) va lay cac tin @mention THAT.
// App ID/Secret va token luu ma hoa DPAPI trong host; giao dien khong doc lai duoc.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace Aurora
{
    static class Lark
    {
        const int PORT = 47653;
        public static string Redirect { get { return "http://127.0.0.1:" + PORT + "/lark/callback"; } }
        const string DEFAULT_SCOPES = "offline_access search:message im:message:readonly";
        static readonly object L = new object();
        static string lastError = ""; static bool waiting; static HttpListener listener; static string pendingState;
        static readonly Dictionary<string, Dictionary<string, object>> msgCache = new Dictionary<string, Dictionary<string, object>>();
        static readonly Dictionary<string, string> nameCache = new Dictionary<string, string>();
        static readonly Dictionary<string, string> chatCache = new Dictionary<string, string>();

        // ---------- cau hinh & token ----------
        static Dictionary<string, object> Load()
        {
            string s = Secrets.GetRaw("lark");
            if (s == null) return new Dictionary<string, object>();
            try { return J.S.Deserialize<Dictionary<string, object>>(s) ?? new Dictionary<string, object>(); } catch { return new Dictionary<string, object>(); }
        }
        static void Save(Dictionary<string, object> c) { Secrets.SetRaw("lark", J.S.Serialize(c)); }
        static string S(Dictionary<string, object> c, string k) { return J.Str(c, k) ?? ""; }
        static bool Feishu(Dictionary<string, object> c) { return S(c, "domain") == "feishu"; }
        static string Open(Dictionary<string, object> c) { return Feishu(c) ? "https://open.feishu.cn" : "https://open.larksuite.com"; }
        static string Accounts(Dictionary<string, object> c) { return Feishu(c) ? "https://accounts.feishu.cn" : "https://accounts.larksuite.com"; }
        static string Applink(Dictionary<string, object> c) { return Feishu(c) ? "https://applink.feishu.cn" : "https://applink.larksuite.com"; }

        public static object Status()
        {
            lock (L)
            {
                var c = Load();
                return new Dictionary<string, object> {
                    { "configured", S(c, "appId") != "" && S(c, "appSecret") != "" }, { "loggedIn", S(c, "refresh") != "" || S(c, "access") != "" },
                    { "name", S(c, "name") }, { "domain", S(c, "domain") == "" ? "lark" : S(c, "domain") }, { "appId", S(c, "appId") },
                    { "hasSecret", S(c, "appSecret") != "" }, { "scopes", S(c, "scopes") == "" ? DEFAULT_SCOPES : S(c, "scopes") },
                    { "redirect", Redirect }, { "waiting", waiting }, { "error", lastError }
                };
            }
        }

        public static object Configure(Dictionary<string, object> a)
        {
            lock (L)
            {
                var c = Load();
                string dom = J.Str(a, "domain") == "feishu" ? "feishu" : "lark";
                string id = (J.Str(a, "appId") ?? "").Trim(), sec = (J.Str(a, "appSecret") ?? "").Trim(), sc = (J.Str(a, "scopes") ?? "").Trim();
                if (id == "") throw new Exception("Thieu App ID");
                bool changed = id != S(c, "appId") || dom != S(c, "domain");
                if (sec == "") sec = S(c, "appSecret");
                if (sec == "") throw new Exception("Thieu App Secret");
                c["domain"] = dom; c["appId"] = id; c["appSecret"] = sec; c["scopes"] = sc == "" ? DEFAULT_SCOPES : sc;
                if (changed) { c["access"] = ""; c["refresh"] = ""; c["accessExp"] = 0; c["refreshExp"] = 0; c["openId"] = ""; c["name"] = ""; }
                Save(c); lastError = ""; return true;
            }
        }

        public static object Logout()
        {
            lock (L)
            {
                var c = Load(); c["access"] = ""; c["refresh"] = ""; c["accessExp"] = 0; c["refreshExp"] = 0; c["openId"] = ""; c["name"] = ""; Save(c);
                msgCache.Clear(); nameCache.Clear(); chatCache.Clear(); lastError = ""; StopListener(); return true;
            }
        }

        // ---------- dang nhap: mo trinh duyet, nhan ma o cong loopback ----------
        public static object Login(Dictionary<string, object> a)
        {
            Dictionary<string, object> c;
            lock (L)
            {
                c = Load();
                if (S(c, "appId") == "" || S(c, "appSecret") == "") throw new Exception("Hay luu App ID + App Secret truoc");
                StopListener(); lastError = "";
                pendingState = Guid.NewGuid().ToString("N");
                try
                {
                    listener = new HttpListener(); listener.Prefixes.Add("http://127.0.0.1:" + PORT + "/lark/"); listener.Start();
                }
                catch (Exception e) { listener = null; throw new Exception("Khong mo duoc cong " + PORT + " de nhan ma dang nhap: " + e.Message); }
                waiting = true;
                var lst = listener; string state = pendingState;
                var t = new Thread(delegate() { Serve(lst, state); }); t.IsBackground = true; t.Start();
            }
            string url = Accounts(c) + "/open-apis/authen/v1/authorize?client_id=" + Uri.EscapeDataString(S(c, "appId")) + "&redirect_uri=" + Uri.EscapeDataString(Redirect)
                + "&scope=" + Uri.EscapeDataString(S(c, "scopes") == "" ? DEFAULT_SCOPES : S(c, "scopes")) + "&state=" + pendingState;
            if (!J.Bool(a, "noBrowser", false)) Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return new Dictionary<string, object> { { "url", url } };
        }

        static void StopListener() { try { if (listener != null) { listener.Close(); } } catch { } listener = null; waiting = false; }

        static void Serve(HttpListener lst, string state)
        {
            DateTime end = DateTime.UtcNow.AddMinutes(5);
            try
            {
                IAsyncResult ar = null;   // chi giu MOT loi goi cho; tao moi moi vong se lam request den bi nuot mat
                while (DateTime.UtcNow < end && lst.IsListening)
                {
                    if (ar == null) ar = lst.BeginGetContext(null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(1000)) continue;
                    HttpListenerContext ctx; try { ctx = lst.EndGetContext(ar); } catch { break; }
                    ar = null;
                    string path = ctx.Request.Url.AbsolutePath, msg = null; bool done = false;
                    if (path == "/lark/callback")
                    {
                        string code = ctx.Request.QueryString["code"], st = ctx.Request.QueryString["state"], err = ctx.Request.QueryString["error"];
                        if (st != state) msg = "Sai ma xac thuc (state). Hay bam 'Dang nhap Lark' lai trong Aurora Dock.";
                        else if (!string.IsNullOrEmpty(err)) { lock (L) lastError = "Lark tu choi: " + err + " " + (ctx.Request.QueryString["error_description"] ?? ""); msg = lastError; done = true; }
                        else if (string.IsNullOrEmpty(code)) msg = "Thieu ma dang nhap.";
                        else
                        {
                            try { Exchange(code); msg = "OK"; }
                            catch (Exception e) { lock (L) lastError = e.Message; msg = e.Message; }
                            done = true;
                        }
                    }
                    else msg = "Khong co trang nay.";
                    string html = "<!doctype html><meta charset=utf-8><title>Aurora Dock</title><body style='font-family:Segoe UI,sans-serif;background:#10131f;color:#fff;display:grid;place-items:center;height:100vh;margin:0'><div style='text-align:center'><h2>"
                        + (msg == "OK" ? "&#10003; &#272;&#227; k&#7871;t n&#7889;i Lark" : "Kh&#244;ng k&#7871;t n&#7889;i &#273;&#432;&#7907;c") + "</h2><p>" + (msg == "OK" ? "B&#7841;n c&#243; th&#7875; &#273;&#243;ng tab n&#224;y v&#224; quay l&#7841;i Aurora Dock." : WebUtility.HtmlEncode(msg)) + "</p></div>";
                    var b = Encoding.UTF8.GetBytes(html); ctx.Response.ContentType = "text/html; charset=utf-8"; ctx.Response.StatusCode = msg == "OK" || done ? 200 : 400;
                    ctx.Response.OutputStream.Write(b, 0, b.Length); ctx.Response.Close();
                    if (done) break;
                }
            }
            catch (Exception e) { App.Log("lark serve: " + e.Message); }
            finally { lock (L) { if (listener == lst) { StopListener(); } } }
        }

        static void Exchange(string code)
        {
            var c = Load();
            var r = Post(Open(c) + "/open-apis/authen/v2/oauth/token", null, new Dictionary<string, object> {
                { "grant_type", "authorization_code" }, { "client_id", S(c, "appId") }, { "client_secret", S(c, "appSecret") }, { "code", code }, { "redirect_uri", Redirect } });
            StoreTokens(c, r);
            // lay ten + open_id cua ban
            var u = Api(c, "GET", "/open-apis/authen/v1/user_info", S(c, "access"), null);
            var d = u.ContainsKey("data") ? u["data"] as Dictionary<string, object> : null;
            if (d != null) { c["openId"] = J.Str(d, "open_id") ?? ""; c["name"] = J.Str(d, "name") ?? ""; }
            Save(c);
        }

        static void StoreTokens(Dictionary<string, object> c, Dictionary<string, object> r)
        {
            long now = App.NowMs();
            c["access"] = J.Str(r, "access_token") ?? ""; c["accessExp"] = now + (long)J.Num(r, "expires_in", 7200) * 1000;
            string rt = J.Str(r, "refresh_token");
            if (!string.IsNullOrEmpty(rt)) { c["refresh"] = rt; c["refreshExp"] = now + (long)J.Num(r, "refresh_token_expires_in", 604800) * 1000; }
            Save(c);
        }

        // ---------- goi API ----------
        static Dictionary<string, object> Post(string url, string token, Dictionary<string, object> body)
        {
            var hd = new Dictionary<string, object> { { "Content-Type", "application/json; charset=utf-8" } };
            if (token != null) hd["Authorization"] = "Bearer " + token;
            var res = (Dictionary<string, object>)Http.Do(new Dictionary<string, object> { { "url", url }, { "method", "POST" }, { "headers", hd }, { "body", J.S.Serialize(body) } });
            return Parse(res, url);
        }
        static Dictionary<string, object> Parse(Dictionary<string, object> res, string what)
        {
            Dictionary<string, object> d;
            try { d = J.S.Deserialize<Dictionary<string, object>>(Convert.ToString(res["body"])); } catch { throw new Exception("Lark tra ve khong phai JSON (HTTP " + res["status"] + ") tai " + new Uri(what).AbsolutePath); }
            if (d == null) throw new Exception("Lark tra ve rong");
            if (d.ContainsKey("error") && d["error"] is string) throw new Exception("Lark: " + d["error"] + " — " + (J.Str(d, "error_description") ?? ""));
            if (d.ContainsKey("code") && Convert.ToInt64(d["code"]) != 0) throw new Exception("Lark " + d["code"] + ": " + (J.Str(d, "msg") ?? J.Str(d, "error_description") ?? ""));
            return d;
        }
        static Dictionary<string, object> Api(Dictionary<string, object> c, string method, string path, string token, Dictionary<string, object> body)
        {
            var hd = new Dictionary<string, object> { { "Authorization", "Bearer " + token }, { "Content-Type", "application/json; charset=utf-8" } };
            var a = new Dictionary<string, object> { { "url", Open(c) + path }, { "method", method }, { "headers", hd } };
            if (body != null) a["body"] = J.S.Serialize(body);
            return Parse((Dictionary<string, object>)Http.Do(a), Open(c) + path);
        }

        static string Token(Dictionary<string, object> c)
        {
            lock (L)
            {
                c = Load(); long now = App.NowMs();
                if (S(c, "access") != "" && (long)J.Num(c, "accessExp", 0) > now + 120000) return S(c, "access");
                if (S(c, "refresh") == "") throw new Exception("CHUA_DANG_NHAP");
                if ((long)J.Num(c, "refreshExp", 0) > 0 && (long)J.Num(c, "refreshExp", 0) < now) { throw new Exception("Phien Lark het han (qua 7 ngay khong dung) — hay dang nhap lai"); }
                try
                {
                    var r = Post(Open(c) + "/open-apis/authen/v2/oauth/token", null, new Dictionary<string, object> {
                        { "grant_type", "refresh_token" }, { "client_id", S(c, "appId") }, { "client_secret", S(c, "appSecret") }, { "refresh_token", S(c, "refresh") } });
                    StoreTokens(c, r);   // refresh token chi dung 1 lan: luu cai moi NGAY
                    return S(c, "access");
                }
                catch (Exception e)
                {
                    if (e.Message.IndexOf("invalid_grant", StringComparison.OrdinalIgnoreCase) >= 0) { c["access"] = ""; c["refresh"] = ""; Save(c); throw new Exception("CHUA_DANG_NHAP"); }
                    throw;
                }
            }
        }

        // ---------- @mention ----------
        public static object Mentions(Dictionary<string, object> a)
        {
            var c = Load();
            if (S(c, "refresh") == "" && S(c, "access") == "") throw new Exception("CHUA_DANG_NHAP");
            string tok = Token(c); c = Load(); string me = S(c, "openId");
            if (me == "") throw new Exception("Chua biet open_id cua ban — hay dang nhap lai");
            double hours = J.Num(a, "hours", 24); long since = App.NowMs() / 1000 - (long)(hours * 3600);
            string mode = "search", note = ""; var msgs = new List<Dictionary<string, object>>();
            try { msgs = ViaSearch(c, tok, me, since); }
            catch (Exception e)
            {
                if (e.Message.StartsWith("Lark 99991") || e.Message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 && e.Message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0) throw;
                note = "Search khong dung duoc (" + e.Message + ") — dang quet cac chat gan day"; mode = "scan";
                msgs = ViaScan(c, tok, me, since);
            }
            var items = new List<object>();
            foreach (var m in msgs.OrderByDescending(x => (long)J.Num(x, "_t", 0)).Take(60))
            {
                string chat = J.Str(m, "chat_id") ?? "", sid = ""; var sd = m.ContainsKey("sender") ? m["sender"] as Dictionary<string, object> : null; if (sd != null) sid = J.Str(sd, "id") ?? "";
                string sender = Name(c, tok, sid, m);
                string cname = ChatName(c, tok, chat);
                items.Add(new Dictionary<string, object> {
                    { "id", J.Str(m, "message_id") }, { "chat", chat }, { "chatName", cname }, { "sender", sender }, { "senderId", sid },
                    { "text", Text(m) }, { "t", (long)J.Num(m, "_t", 0) }, { "url", Applink(c) + "/client/chat/open?openChatId=" + chat } });
            }
            return new Dictionary<string, object> { { "me", new Dictionary<string, object> { { "openId", me }, { "name", S(c, "name") } } }, { "mode", mode }, { "note", note }, { "items", items }, { "at", App.NowMs() } };
        }

        static List<Dictionary<string, object>> ViaSearch(Dictionary<string, object> c, string tok, string me, long since)
        {
            var ids = new List<string>(); string pt = "";
            for (int page = 0; page < 3; page++)
            {
                var body = new Dictionary<string, object> { { "query", "" }, { "at_chatter_ids", new object[] { me } }, { "start_time", since.ToString() } };
                var r = Api(c, "POST", "/open-apis/search/v2/message?user_id_type=open_id&page_size=50" + (pt != "" ? "&page_token=" + Uri.EscapeDataString(pt) : ""), tok, body);
                var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null; if (d == null) break;
                if (d.ContainsKey("items") && d["items"] is System.Collections.IEnumerable)
                    foreach (var it in (System.Collections.IEnumerable)d["items"])
                    {
                        if (it is string) ids.Add((string)it);
                        else { var dd = it as Dictionary<string, object>; if (dd != null && J.Str(dd, "message_id") != null) ids.Add(J.Str(dd, "message_id")); }
                    }
                pt = J.Str(d, "page_token") ?? ""; if (!J.Bool(d, "has_more", false) || pt == "") break;
            }
            var res = new List<Dictionary<string, object>>();
            foreach (var id in ids.Distinct().Take(80)) { var m = Detail(c, tok, id); if (m != null) res.Add(m); }
            return res;
        }

        static Dictionary<string, object> Detail(Dictionary<string, object> c, string tok, string id)
        {
            lock (msgCache) { Dictionary<string, object> h; if (msgCache.TryGetValue(id, out h)) return h; }
            var r = Api(c, "GET", "/open-apis/im/v1/messages/" + Uri.EscapeDataString(id) + "?user_id_type=open_id", tok, null);
            var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null; if (d == null || !d.ContainsKey("items")) return null;
            foreach (var it in (System.Collections.IEnumerable)d["items"]) { var m = it as Dictionary<string, object>; if (m != null) { Prep(m); lock (msgCache) msgCache[id] = m; return m; } }
            return null;
        }
        static void Prep(Dictionary<string, object> m) { long t; long.TryParse(J.Str(m, "create_time") ?? "0", out t); m["_t"] = t; }

        static List<Dictionary<string, object>> ViaScan(Dictionary<string, object> c, string tok, string me, long since)
        {
            var res = new List<Dictionary<string, object>>(); var chats = new List<string>(); string pt = "";
            for (int p = 0; p < 2; p++)
            {
                var r = Api(c, "GET", "/open-apis/im/v1/chats?page_size=50&sort_type=ByActiveTimeDesc" + (pt != "" ? "&page_token=" + Uri.EscapeDataString(pt) : ""), tok, null);
                var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null; if (d == null) break;
                if (d.ContainsKey("items")) foreach (var it in (System.Collections.IEnumerable)d["items"]) { var cd = it as Dictionary<string, object>; if (cd != null) { string cid = J.Str(cd, "chat_id"); if (cid != null) { chats.Add(cid); if (J.Str(cd, "name") != null) lock (chatCache) chatCache[cid] = J.Str(cd, "name"); } } }
                pt = J.Str(d, "page_token") ?? ""; if (!J.Bool(d, "has_more", false) || pt == "") break;
            }
            foreach (var cid in chats.Take(40))
            {
                try
                {
                    var r = Api(c, "GET", "/open-apis/im/v1/messages?container_id_type=chat&container_id=" + Uri.EscapeDataString(cid) + "&start_time=" + since + "&sort_type=ByCreateTimeDesc&page_size=50&user_id_type=open_id", tok, null);
                    var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null; if (d == null || !d.ContainsKey("items")) continue;
                    foreach (var it in (System.Collections.IEnumerable)d["items"])
                    {
                        var m = it as Dictionary<string, object>; if (m == null || !m.ContainsKey("mentions")) continue;
                        bool mine = false; foreach (var mo in (System.Collections.IEnumerable)m["mentions"]) { var md = mo as Dictionary<string, object>; if (md != null && J.Str(md, "id") == me) mine = true; }
                        if (mine) { Prep(m); res.Add(m); }
                    }
                }
                catch (Exception e) { App.Log("lark scan " + cid + ": " + e.Message); }
            }
            return res;
        }

        static string Name(Dictionary<string, object> c, string tok, string openId, Dictionary<string, object> m)
        {
            if (openId == "") return "Lark";
            lock (nameCache) { string n; if (nameCache.TryGetValue(openId, out n)) return n; }
            string name = "";
            try
            {
                var r = Post(Open(c) + "/open-apis/contact/v3/users/basic_batch?user_id_type=open_id", tok, new Dictionary<string, object> { { "user_ids", new object[] { openId } } });
                var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null;
                if (d != null && d.ContainsKey("users")) foreach (var u in (System.Collections.IEnumerable)d["users"]) { var ud = u as Dictionary<string, object>; if (ud != null) name = J.Str(ud, "name") ?? ""; }
            }
            catch (Exception e) { App.Log("lark name: " + e.Message); }
            if (name == "") name = "Ai do";
            lock (nameCache) nameCache[openId] = name; return name;
        }
        static string ChatName(Dictionary<string, object> c, string tok, string chat)
        {
            if (chat == "") return "";
            lock (chatCache) { string n; if (chatCache.TryGetValue(chat, out n)) return n; }
            string name = "";
            try { var r = Api(c, "GET", "/open-apis/im/v1/chats/" + Uri.EscapeDataString(chat), tok, null); var d = r.ContainsKey("data") ? r["data"] as Dictionary<string, object> : null; if (d != null) name = J.Str(d, "name") ?? ""; }
            catch (Exception e) { App.Log("lark chat: " + e.Message); }
            lock (chatCache) chatCache[chat] = name; return name;
        }

        // bien noi dung tin nhan (JSON) thanh chu thuong, thay @_user_1 bang @Ten
        static string Text(Dictionary<string, object> m)
        {
            string type = J.Str(m, "msg_type") ?? "", raw = "";
            var b = m.ContainsKey("body") ? m["body"] as Dictionary<string, object> : null; if (b != null) raw = J.Str(b, "content") ?? "";
            string text = "";
            try
            {
                var o = J.S.Deserialize<Dictionary<string, object>>(raw);
                if (type == "text") text = J.Str(o, "text") ?? "";
                else if (type == "post")
                {
                    var sb = new StringBuilder(); if (!string.IsNullOrEmpty(J.Str(o, "title"))) sb.Append(J.Str(o, "title")).Append(' ');
                    Dictionary<string, object> body = o;
                    if (!o.ContainsKey("content")) foreach (var kv in o) { var inner = kv.Value as Dictionary<string, object>; if (inner != null && inner.ContainsKey("content")) { body = inner; break; } }
                    if (body.ContainsKey("content")) foreach (var line in (System.Collections.IEnumerable)body["content"]) { if (line is System.Collections.IEnumerable) foreach (var seg in (System.Collections.IEnumerable)line) { var sd = seg as Dictionary<string, object>; if (sd == null) continue; string tag = J.Str(sd, "tag"); if (tag == "text") sb.Append(J.Str(sd, "text")); else if (tag == "at") sb.Append("@" + (J.Str(sd, "user_name") ?? J.Str(sd, "user_id") ?? "")); else if (tag == "a") sb.Append(J.Str(sd, "text")); } sb.Append(' '); }
                    text = sb.ToString().Trim();
                }
                else if (type == "image") text = "[Hinh anh]"; else if (type == "file") text = "[Tep] " + (J.Str(o, "file_name") ?? ""); else if (type == "audio") text = "[Tin nhan thoai]";
                else if (type == "interactive") text = "[The tuong tac]"; else text = "[" + type + "]";
            }
            catch { text = raw; }
            if (m.ContainsKey("mentions"))
                foreach (var mo in (System.Collections.IEnumerable)m["mentions"]) { var md = mo as Dictionary<string, object>; if (md != null && J.Str(md, "key") != null) text = text.Replace(J.Str(md, "key"), "@" + (J.Str(md, "name") ?? "")); }
            return text.Length > 400 ? text.Substring(0, 400) + "…" : text;
        }
    }
}
