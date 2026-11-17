using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static class Program
{
    private static readonly object Gate = new object();
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 8 };
    private static readonly Dictionary<string, Run> Runs = new Dictionary<string, Run>();
    private static readonly SemaphoreSlim Slots = new SemaphoreSlim(12);
    private static string Output, Prefix, Origin;
    private static UdpClient Stun;
    private static int StunPackets;
    private static readonly string[] Contexts = { "main", "worker", "service-worker" };
    private static readonly string[] Families = { "ipv4", "ipv6" };
    private sealed class Control { internal IPAddress Address; internal string Status; internal long ElapsedMs; }
    private sealed class Run
    {
        internal string Id, Phase; internal DateTime Started; internal int StunStart;
        internal readonly Dictionary<string, Control> Controls = new Dictionary<string, Control>();
        internal readonly List<object> Routes = new List<object>(); internal object WebRtc, WebSocket; internal bool Complete;
        internal object Report() { return new { runId = Id, phase = Phase, startedUtc = Started.ToString("o"), complete = Complete,
            controls = Controls.Select(x => new { family = x.Key, status = x.Value.Status, elapsedMs = x.Value.ElapsedMs }).ToArray(),
            routes = Routes.ToArray(), webRtc = WebRtc, webSocket = WebSocket,
            scope = "Only public-address equality, three fetch contexts and local STUN packets. No addresses, account data or authentication state are recorded. This is not a complete DNS/background-traffic or anonymity audit." }; }
    }
    private static void Save()
    {
        lock (Gate)
        {
            string temporary = Path.Combine(Output, "results.tmp");
            File.WriteAllText(temporary, new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 }.Serialize(Runs.Values.Select(r => r.Report()).ToArray()));
            string target = Path.Combine(Output, "results.json");
            if (File.Exists(target)) File.Delete(target);
            File.Move(temporary, target);
        }
    }
    private static string TextValue(Dictionary<string, object> value, string key)
    { object item; return value.TryGetValue(key, out item) ? Convert.ToString(item) : null; }
    private static object Parse(string body) { lock (Json) return Json.DeserializeObject(body); }
    private static byte[] Serialize(object value) { lock (Json) return Encoding.UTF8.GetBytes(Json.Serialize(value)); }
    private static Run Find(Dictionary<string, object> body)
    { Run run; lock (Gate) if (Runs.TryGetValue(TextValue(body, "runId") ?? "", out run)) return run; throw new InvalidDataException(); }
    private static async Task<Control> Direct(string family)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(family == "ipv4" ? "https://api.ipify.org?format=json" : "https://api6.ipify.org?format=json");
            request.Proxy = null; request.Timeout = 8000; request.ReadWriteTimeout = 8000; request.AllowAutoRedirect = false;
            using (var timeout = new CancellationTokenSource(8000))
            using (timeout.Token.Register(() => request.Abort()))
            {
            request.UserAgent = "Harbor-Connected-Checks/1.0"; request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                if (response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException();
                using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream))
                {
                    char[] buffer = new char[512]; int count = await reader.ReadAsync(buffer, 0, buffer.Length);
                    var value = Parse(new string(buffer, 0, count)) as Dictionary<string, object>; IPAddress ip;
                    if (value == null || !IPAddress.TryParse(TextValue(value, "ip"), out ip)) throw new InvalidDataException();
                    bool proper = ip.AddressFamily == (family == "ipv4" ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);
                    return new Control { Address = proper ? ip : null, Status = proper ? "ok" : "unexpected_family", ElapsedMs = watch.ElapsedMilliseconds };
                }
            }
            }
        }
        catch { return new Control { Status = "unavailable", ElapsedMs = watch.ElapsedMilliseconds }; }
    }
    private static async Task ReceiveStun()
    {
        try { while (true) { var reply = await Stun.ReceiveAsync(); var b = reply.Buffer; if (b.Length >= 20 && b[0] == 0 && b[1] == 1 && b[4] == 0x21 && b[5] == 0x12 && b[6] == 0xa4 && b[7] == 0x42) Interlocked.Increment(ref StunPackets); } }
        catch (ObjectDisposedException) { } catch (SocketException) { }
    }
    private static async Task<object> Api(string route, Dictionary<string, object> body)
    {
        if (route == "begin")
        {
            string phase = TextValue(body, "phase"); if (!Regex.IsMatch(phase ?? "", "^[a-z0-9-]{1,32}$")) throw new InvalidDataException();
            var run = new Run { Id = Guid.NewGuid().ToString("N"), Phase = phase, Started = DateTime.UtcNow, StunStart = Volatile.Read(ref StunPackets) };
            var controls = await Task.WhenAll(Families.Select(Direct));
            for (int i = 0; i < Families.Length; i++) run.Controls[Families[i]] = controls[i];
            lock (Gate) Runs.Add(run.Id, run); Save();
            return new { runId = run.Id, stunPort = ((IPEndPoint)Stun.Client.LocalEndPoint).Port };
        }
        var found = Find(body);
        if (route == "route")
        {
            string context = TextValue(body, "context"), family = TextValue(body, "family");
            if (!Contexts.Contains(context) || !Families.Contains(family)) throw new InvalidDataException();
            string browserStatus = TextValue(body, "status");
            if (browserStatus != "ok" && browserStatus != "unavailable" && browserStatus != "timeout" && browserStatus != "unsupported") throw new InvalidDataException();
            long elapsed; if (!Int64.TryParse(TextValue(body, "elapsedMs"), out elapsed)) elapsed = 0;
            IPAddress browserIp = null; bool proper = browserStatus == "ok" && IPAddress.TryParse(TextValue(body, "ip"), out browserIp) && browserIp.AddressFamily == (family == "ipv4" ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);
            Control control = found.Controls[family]; bool? different = proper && control.Address != null ? (bool?)!browserIp.Equals(control.Address) : null;
            var result = new { context, family, browserStatus = browserStatus == "ok" && !proper ? "unexpected_reply" : browserStatus, directStatus = control.Status, differentFromDirect = different, elapsedMs = Math.Max(0, Math.Min(120000, elapsed)) };
            lock (Gate) found.Routes.Add(result); Save(); return result;
        }
        if (route == "webrtc")
        {
            object types; body.TryGetValue("candidateTypes", out types);
            var allowed = new[] { "host", "srflx", "prflx", "relay" };
            var strings = (types as object[] ?? new object[0]).Select(Convert.ToString).Where(allowed.Contains).Distinct().OrderBy(x => x).ToArray();
            int candidates; if (!Int32.TryParse(TextValue(body, "candidateCount"), out candidates)) candidates = 0;
            string status = TextValue(body, "status"); if (status != "ok" && status != "unsupported" && status != "unavailable") throw new InvalidDataException();
            var result = new { status, candidateTypes = strings, candidateCount = Math.Max(0, Math.Min(1000, candidates)), localStunPackets = Math.Max(0, Volatile.Read(ref StunPackets) - found.StunStart), cameraOrMicrophoneRequested = false };
            lock (Gate) found.WebRtc = result; Save(); return result;
        }
        if (route == "websocket")
        {
            object ok; if (!body.TryGetValue("ok", out ok) || !(ok is bool)) throw new InvalidDataException();
            long elapsed; if (!Int64.TryParse(TextValue(body, "elapsedMs"), out elapsed)) elapsed = 0;
            var result = new { echoed = (bool)ok, elapsedMs = Math.Max(0, Math.Min(120000, elapsed)), scope = "Functional secure WebSocket echo only; server-observed route is not measured." };
            lock (Gate) found.WebSocket = result; Save(); return result;
        }
        if (route == "finish") { lock (Gate) found.Complete = true; Save(); return found.Report(); }
        throw new InvalidDataException();
    }
    private static void Response(NetworkStream stream, int status, string type, byte[] body, string extra = "")
    {
        string head = "HTTP/1.1 " + status + (status == 200 ? " OK" : " Error") + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\n" + extra + "\r\n";
        byte[] bytes = Encoding.ASCII.GetBytes(head); stream.Write(bytes, 0, bytes.Length); stream.Write(body, 0, body.Length);
    }
    private static async Task Handle(TcpClient client)
    {
        await Slots.WaitAsync();
        using (client) try
        {
            client.ReceiveTimeout = 10000; client.SendTimeout = 10000;
            var stream = client.GetStream(); var header = new List<byte>();
            while (header.Count < 16384)
            {
                int next = stream.ReadByte(); if (next < 0) return; header.Add((byte)next);
                if (header.Count >= 4 && header[header.Count - 4] == 13 && header[header.Count - 3] == 10 && header[header.Count - 2] == 13 && header[header.Count - 1] == 10) break;
            }
            string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' '); if (first.Length < 2) return;
            var headers = lines.Skip(1).Where(x => x.Contains(":")).Select(x => x.Split(new[] { ':' }, 2)).ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);
            string host; if (!headers.TryGetValue("Host", out host) || "http://" + host != Origin) { Response(stream, 403, "text/plain", Encoding.UTF8.GetBytes("Not permitted")); return; }
            string target = first[1].Split('?')[0]; if (!target.StartsWith(Prefix, StringComparison.Ordinal)) { Response(stream, 404, "text/plain", new byte[0]); return; }
            string route = target.Substring(Prefix.Length);
            if (first[0] == "GET")
            {
                if (route == "status") { lock (Gate) Response(stream, 200, "application/json", Serialize(Runs.Values.Select(x => x.Report()).ToArray())); return; }
                string file = route == "" ? "index.html" : route;
                if (!new[] { "index.html", "app.js", "worker.js", "sw.js" }.Contains(file)) { Response(stream, 404, "text/plain", new byte[0]); return; }
                Response(stream, 200, file.EndsWith(".html") ? "text/html; charset=utf-8" : "application/javascript; charset=utf-8", File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file)), file == "sw.js" ? "Service-Worker-Allowed: " + Prefix + "\r\n" : ""); return;
            }
            string origin; if (first[0] != "POST" || !headers.TryGetValue("Origin", out origin) || origin != Origin || !route.StartsWith("api/")) { Response(stream, 403, "text/plain", new byte[0]); return; }
            int length; string contentLength; if (!headers.TryGetValue("Content-Length", out contentLength) || !Int32.TryParse(contentLength, out length) || length < 0 || length > 16384) throw new InvalidDataException();
            byte[] payload = new byte[length]; int at = 0; while (at < length) { int got = stream.Read(payload, at, length - at); if (got == 0) return; at += got; }
            var body = Parse(Encoding.UTF8.GetString(payload)) as Dictionary<string, object>; if (body == null) throw new InvalidDataException();
            Response(stream, 200, "application/json", Serialize(await Api(route.Substring(4), body)));
        }
        catch { try { Response(client.GetStream(), 400, "application/json", Encoding.UTF8.GetBytes("{\"error\":\"Check could not complete\"}")); } catch { } }
        finally { Slots.Release(); }
    }
    private static int Main(string[] args)
    {
        if (args.Length != 1) { Console.WriteLine("Usage: HarborConnectedChecks.exe <new evidence folder>"); return 2; }
        Output = Path.GetFullPath(args[0]); if (Directory.Exists(Output)) throw new InvalidOperationException("Use a new evidence folder."); Directory.CreateDirectory(Output);
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        Stun = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); _ = ReceiveStun();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; Origin = "http://127.0.0.1:" + port; Prefix = "/h/" + Guid.NewGuid().ToString("N") + "/";
        string url = Origin + Prefix;
        File.WriteAllText(Path.Combine(Output, "server.json"), Encoding.UTF8.GetString(Serialize(new { url, statusUrl = url + "status", connectedUrl = url + "?phase=connected&run=1", processId = Process.GetCurrentProcess().Id })));
        Console.WriteLine("Local test fixture ready. See server.json for the test URL. No account, address or URL logs are recorded.");
        Save();
        try { while (true) { var client = listener.AcceptTcpClient(); _ = Task.Run(() => Handle(client)); } }
        finally { listener.Stop(); Stun.Close(); }
    }
}
