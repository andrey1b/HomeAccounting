using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Windows.Threading;

namespace HomeAccounting.Services;

/// <summary>
/// Minimal HTTP server on 0.0.0.0:8771 (TcpListener — no admin rights needed).
/// GET  /ping   → {"ok":true,"app":"HomeAccounting"}
/// GET  /setup  → HTML page: download APK or open app with pre-filled config
/// GET  /apk    → serves comfortbuh.apk from Resources folder
/// GET  /*.user.js → Tampermonkey userscript
/// POST /xml    → raw XML bytes (Tampermonkey) → imports receipt
/// POST /qr     → QR URL text (ComfortBuh phone) → fetches XML from ДПС, imports
/// </summary>
public static class ReceiptHttpReceiver
{
    public const int    Port       = 8772;
    public const string ApkVersion = "2.4";  // версия встроенного ComfortBuh.apk

    static TcpListener?          _tcp;
    static Dispatcher?           _ui;
    static Action<byte[]>?       _onXml;
    static Action<ImportResult>? _onQr;

    public static string LocalIp { get; private set; } = "127.0.0.1";

    public static void Start(Dispatcher ui, Action<byte[]> onXml, Action<ImportResult> onQr)
    {
        _ui    = ui;
        _onXml = onXml;
        _onQr  = onQr;
        LocalIp = DetectLocalIp();

        _tcp = new TcpListener(IPAddress.Any, Port);
        try { _tcp.Start(); _ = Task.Run(LoopAsync); }
        catch { /* port in use — fail silently */ }
    }

    public static void Stop() { try { _tcp?.Stop(); } catch { } }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static string DetectLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            s.Connect("8.8.8.8", 53);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }

    static byte[]? LoadApk()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream("comfortbuh.apk");
        if (s == null) return null;
        var buf = new byte[s.Length];
        s.ReadExactly(buf);
        return buf;
    }

    // ── TCP accept loop ────────────────────────────────────────────────────────

    static async Task LoopAsync()
    {
        while (true)
        {
            try
            {
                var client = await _tcp!.AcceptTcpClientAsync();
                _ = Task.Run(() => ServeAsync(client));
            }
            catch { break; }
        }
    }

    static async Task ServeAsync(TcpClient client)
    {
        try
        {
            using var _ = client;
            client.ReceiveTimeout = 5_000;
            client.SendTimeout   = 30_000;
            var stream = client.GetStream();
            var req = await ReadRequestAsync(stream);
            if (req == null) return;
            await HandleAsync(stream, req.Value.method, req.Value.path, req.Value.body);
        }
        catch { }
    }

    // ── HTTP request reader ────────────────────────────────────────────────────

    static async Task<(string method, string path, byte[] body)?> ReadRequestAsync(NetworkStream stream)
    {
        // Read until we have all headers (\r\n\r\n)
        var hBuf  = new byte[8192];
        int hTotal = 0, headerEnd = -1, contentLength = 0;

        while (hTotal < hBuf.Length)
        {
            int n = await stream.ReadAsync(hBuf.AsMemory(hTotal));
            if (n == 0) break;
            hTotal += n;
            headerEnd = FindCRLF2(hBuf, hTotal);
            if (headerEnd >= 0) break;
        }

        if (headerEnd < 0) return null;

        var headerText = Encoding.ASCII.GetString(hBuf, 0, headerEnd);
        var rl = headerText.Split('\n', 2)[0].Trim().Split(' ');
        var method   = rl.Length > 0 ? rl[0] : "GET";
        var fullPath = rl.Length > 1 ? rl[1] : "/";
        var qIdx     = fullPath.IndexOf('?');
        var path     = qIdx >= 0 ? fullPath[..qIdx] : fullPath;
        contentLength = ParseContentLength(headerText);

        // Body: some bytes may already be in hBuf after the headers
        int bodyStartInBuf = headerEnd + 4;
        int alreadyRead    = hTotal - bodyStartInBuf;

        byte[] body;
        if (contentLength <= 0)
        {
            body = Array.Empty<byte>();
        }
        else
        {
            body = new byte[contentLength];
            if (alreadyRead > 0)
                Array.Copy(hBuf, bodyStartInBuf, body, 0, Math.Min(alreadyRead, contentLength));
            int remaining = contentLength - alreadyRead;
            int offset    = alreadyRead;
            while (remaining > 0)
            {
                int n = await stream.ReadAsync(body.AsMemory(offset, remaining));
                if (n == 0) break;
                offset    += n;
                remaining -= n;
            }
        }

        return (method, path, body);
    }

    static int FindCRLF2(byte[] buf, int len)
    {
        for (int i = 0; i < len - 3; i++)
            if (buf[i] == '\r' && buf[i+1] == '\n' && buf[i+2] == '\r' && buf[i+3] == '\n')
                return i;
        return -1;
    }

    static int ParseContentLength(string headers)
    {
        foreach (var line in headers.Split('\n'))
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                if (int.TryParse(line[15..].Trim(), out int v)) return v;
        return 0;
    }

    // ── HTTP response writers ──────────────────────────────────────────────────

    static async Task SendAsync(Stream s, int status, string ct, byte[] body, string? extraHeaders = null)
    {
        var st   = status switch { 200 => "OK", 404 => "Not Found", 400 => "Bad Request", _ => "Error" };
        var extra = extraHeaders != null ? extraHeaders + "\r\n" : "";
        var head = $"HTTP/1.1 {status} {st}\r\nContent-Type: {ct}\r\nContent-Length: {body.Length}\r\n" +
                   extra +
                   "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET,POST,OPTIONS\r\n" +
                   "Access-Control-Allow-Headers: Content-Type\r\nConnection: close\r\n\r\n";
        await s.WriteAsync(Encoding.ASCII.GetBytes(head));
        await s.WriteAsync(body);
        await s.FlushAsync();
    }

    static Task JsonAsync(Stream s, int code, string json) =>
        SendAsync(s, code, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));

    // ── Router ─────────────────────────────────────────────────────────────────

    static async Task HandleAsync(Stream stream, string method, string path, byte[] body)
    {
        if (method == "OPTIONS") { await JsonAsync(stream, 200, """{"ok":true}"""); return; }

        // GET /ping  — returns server's real LAN IP and port so the phone can auto-configure
        if (method == "GET" && path == "/ping")
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
            var verStr = $"{ver.Major}.{ver.Minor}.{ver.Build}";
            await JsonAsync(stream, 200,
                $$"""{"ok":true,"app":"HomeAccounting","ip":"{{LocalIp}}","port":{{Port}},"version":"{{verStr}}"}""");
            return;
        }

        // GET /web  — веб-интерфейс для iPhone: ввод URL чека и отправка на сервер
        if (method == "GET" && path == "/web")
        {
            await SendAsync(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(BuildWebHtml()));
            return;
        }

        // GET /setup  — HTML install page for phone
        if (method == "GET" && path == "/setup")
        {
            var html = BuildSetupHtml();
            await SendAsync(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
            return;
        }

        // GET /ComfortBuh_vX.X.apk  — serve ComfortBuh APK (имя файла в URL → правильное расширение на Android)
        if (method == "GET" && path.EndsWith(".apk"))
        {
            var apk = LoadApk();
            if (apk == null)
            {
                await JsonAsync(stream, 404, """{"ok":false,"error":"apk_not_found"}""");
                return;
            }
            await SendAsync(stream, 200, "application/vnd.android.package-archive", apk,
                $"Content-Disposition: attachment; filename=\"ComfortBuh_v{ApkVersion}.apk\"");
            return;
        }

        // GET /*.user.js  — Tampermonkey userscript (place comfort_homeb.user.js next to the exe)
        if (method == "GET" && path.EndsWith(".user.js"))
        {
            var js = Path.Combine(AppContext.BaseDirectory, "comfort_homeb.user.js");
            if (File.Exists(js))
                await SendAsync(stream, 200, "text/javascript; charset=utf-8", File.ReadAllBytes(js));
            else
                await JsonAsync(stream, 404, """{"ok":false,"error":"userscript_not_found"}""");
            return;
        }

        // POST /xml  — raw XML from Tampermonkey
        if (method == "POST" && path == "/xml")
        {
            if (body.Length == 0) { await JsonAsync(stream, 400, """{"ok":false,"error":"empty"}"""); return; }
            await JsonAsync(stream, 200, """{"ok":true}""");
            var b = body;
            _ui?.BeginInvoke(() => _onXml?.Invoke(b));
            return;
        }

        // POST /qr  — QR URL from ComfortBuh phone
        if (method == "POST" && path == "/qr")
        {
            var qrUrl = Encoding.UTF8.GetString(body).Trim();
            if (string.IsNullOrWhiteSpace(qrUrl))
            {
                await JsonAsync(stream, 400, """{"ok":false,"error":"empty_url"}""");
                return;
            }
            var (result, errMsg) = FetchAndImport(qrUrl);
            if (result != null)
            {
                var store = result.Store.Replace("\\", "\\\\").Replace("\"", "\\\"");
                await JsonAsync(stream, 200,
                    $$"""{"ok":true,"count":{{result.Count}},"store":"{{store}}"}""");
                var r = result;
                _ui?.BeginInvoke(() => _onQr?.Invoke(r));
            }
            else
            {
                var err = (errMsg ?? "unknown").Replace("\"", "\\\"");
                await JsonAsync(stream, 200, $$"""{"ok":false,"error":"{{err}}"}""");
            }
            return;
        }

        await JsonAsync(stream, 404, """{"ok":false,"error":"not_found"}""");
    }

    // ── Setup HTML page ────────────────────────────────────────────────────────

    static string BuildSetupHtml()
    {
        var ip = LocalIp;
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
        var verStr = $"{ver.Major}.{ver.Minor}.{ver.Build}";
        // Intent URL: if app is installed → opens it with config;
        //             if not installed    → falls back to /apk download
        var fallback   = Uri.EscapeDataString($"http://{ip}:{Port}/ComfortBuh_v{ApkVersion}.apk");
        var intentUrl  = $"intent://setup?ip={ip}&port={Port}" +
                         $"#Intent;scheme=comfortbuh;package=com.homeaccounting.comfortbuh;" +
                         $"S.browser_fallback_url={fallback};end";
        // $$""" uses {{expr}} for interpolation; single { and } are literal (CSS-safe)
        return $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ComfortBuh</title>
<style>
body{font-family:sans-serif;max-width:420px;margin:2em auto;padding:1em;text-align:center;background:#f5f5f5}
h2{color:#2e7d32;margin-bottom:4px}
p{color:#555;font-size:14px;margin:4px 0 16px}
.btn{display:block;width:100%;box-sizing:border-box;padding:14px;margin:10px 0;font-size:17px;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold}
.green{background:#2e7d32}.blue{background:#1565c0}
.warn{background:#fff3e0;border:1px solid #ff9800;padding:10px;border-radius:6px;font-size:13px;text-align:left;margin-top:16px;line-height:1.5}
</style>
</head>
<body>
<h2>ComfortBuh</h2>
<p>Сканируйте QR-чек &#8594; расходы HomeAccounting</p>
<p style="font-size:11px;color:#aaa">HomeAccounting v{{verStr}}</p>

<p style="font-size:12px;color:#888">Шаг 1 &#8212; скачайте и установите:</p>
<a class="btn green" href="http://{{ip}}:{{Port}}/ComfortBuh_v{{ApkVersion}}.apk">&#11015; Скачать ComfortBuh v{{ApkVersion}}</a>

<p style="font-size:12px;color:#888">Шаг 2 &#8212; откройте с адресом ПК:</p>
<a class="btn blue" href="{{intentUrl}}">&#9654; Открыть ComfortBuh</a>

<div class="warn">
&#9888; <b>Play Protect</b> &#8212; если появится «Подозрительное приложение»:<br>
большая кнопка <b>«OK» = ОТМЕНА</b> (это ловушка!).<br>
Нажимайте маленькую ссылку <b>«Всё равно установить»</b>.
</div>
</body>
</html>
""";
    }

    // ── iPhone web page ───────────────────────────────────────────────────────

    static string BuildWebHtml() => """
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Домашний бюджет — чек</title>
<style>
*{box-sizing:border-box}
body{font-family:-apple-system,sans-serif;max-width:460px;margin:0 auto;padding:1.2em;background:#f2f2f7;color:#1c1c1e}
h2{font-size:20px;margin:0 0 4px;color:#1c1c1e}
.sub{font-size:13px;color:#8e8e93;margin:0 0 20px}
.card{background:#fff;border-radius:12px;padding:16px;margin-bottom:16px;box-shadow:0 1px 3px rgba(0,0,0,.1)}
.step{font-size:13px;color:#3a3a3c;line-height:1.6;margin:0 0 14px}
.step b{color:#000}
textarea{width:100%;height:80px;border:1.5px solid #c7c7cc;border-radius:8px;padding:10px;font-size:14px;resize:none;outline:none;font-family:inherit}
textarea:focus{border-color:#007aff}
button{width:100%;padding:14px;background:#007aff;color:#fff;border:none;border-radius:10px;font-size:17px;font-weight:600;cursor:pointer;margin-top:10px}
button:active{background:#0062cc}
button:disabled{background:#aeaeb2}
#status{margin-top:14px;font-size:14px;text-align:center;min-height:20px;font-weight:500}
.ok{color:#34c759}.err{color:#ff3b30}
</style>
</head>
<body>
<h2>&#127968; Домашний бюджет</h2>
<p class="sub">Отправить чек с iPhone</p>

<div class="card">
<p class="step">
<b>Шаг 1.</b> Откройте камеру iPhone &#8594; наведите на <b>QR-код фискального чека</b> &#8594; нажмите на всплывающую ссылку.<br><br>
<b>Шаг 2.</b> Скопируйте URL из адресной строки браузера.<br><br>
<b>Шаг 3.</b> Вернитесь сюда, вставьте URL ниже и нажмите «Отправить».
</p>
<textarea id="url" placeholder="https://cabinet.tax.gov.ua/cashregs/check?id=..."></textarea>
<button id="btn" onclick="send()">Отправить чек</button>
<div id="status"></div>
</div>

<script>
async function send() {
  const url = document.getElementById('url').value.trim();
  const st  = document.getElementById('status');
  const btn = document.getElementById('btn');
  if (!url) { st.className='err'; st.textContent='Вставьте URL чека.'; return; }
  btn.disabled = true;
  st.className = ''; st.textContent = 'Отправляю…';
  try {
    const r = await fetch('/qr', {method:'POST', body: url,
      headers:{'Content-Type':'text/plain'}});
    const j = await r.json();
    if (j.ok) {
      st.className='ok';
      st.textContent = '✅ Импортировано: ' + j.count + ' позиц. (' + j.store + ')';
      document.getElementById('url').value = '';
    } else {
      st.className='err'; st.textContent = '❌ ' + (j.error || 'Ошибка');
    }
  } catch(e) { st.className='err'; st.textContent='❌ Нет связи с сервером'; }
  btn.disabled = false;
}
</script>
</body>
</html>
""";

    // ── Receipt fetch + import ─────────────────────────────────────────────────

    public static (ImportResult? result, string? error) FetchAndImport(string qrUrl)
    {
        try
        {
            // Validate URL params before making network call
            var id   = GetParam(qrUrl, "id");
            var date = GetParam(qrUrl, "date");
            if (string.IsNullOrEmpty(id))
                return (null, "URL не содержит параметр «id». Убедитесь, что вы вставляете URL с QR-кода чека ДПС.");
            if (string.IsNullOrEmpty(date) || date.Length < 8)
                return (null, "URL не содержит параметр «date» или он имеет неверный формат.");

            var bytes = FetchReceiptXml(qrUrl);
            if (bytes == null) return (null, "receipt_not_in_registry");

            var tmp = Path.Combine(Path.GetTempPath(), $"ha_qr_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            try
            {
                File.WriteAllBytes(tmp, bytes);
                var result = ReceiptImportService.Import(tmp, AppSettings.Load().DefaultAccountId);
                return (result, null);
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
        catch (Exception ex) { return (null, ex.Message); }
    }

    // Правильный API: /ws/api_public/rro/chkAllWeb → JSON { checkXml: "<base64 XML>" }
    static byte[]? FetchReceiptXml(string qrUrl)
    {
        var id   = GetParam(qrUrl, "id");
        var date = GetParam(qrUrl, "date");
        var time = GetParam(qrUrl, "time") ?? "";
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(date) || date.Length < 8)
            return null;

        var d = $"{date[..4]}-{date[4..6]}-{date[6..8]}";
        string t;
        if (date.Length >= 14)
            t = $"{date[8..10]}:{date[10..12]}:{date[12..14]}";
        else if (time.Length >= 6)
            t = $"{time[..2]}:{time[2..4]}:{time[4..6]}";
        else
            t = "00:00:00";

        var fn = GetParam(qrUrl, "fn") ?? "";
        var sm = GetParam(qrUrl, "sm") ?? "";

        var apiUrl = "https://cabinet.tax.gov.ua/ws/api_public/rro/chkAllWeb" +
                     $"?id={Uri.EscapeDataString(id)}" +
                     $"&date={Uri.EscapeDataString($"{d} {t}")}" +
                     "&type=0" +
                     $"&fn={Uri.EscapeDataString(fn)}" +
                     $"&sm={Uri.EscapeDataString(sm)}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept", "application/json, */*");
        http.DefaultRequestHeaders.Add("Referer", "https://cabinet.tax.gov.ua/cashregs/check");

        var response = http.GetAsync(apiUrl).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) return null;
        var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("checkXml", out var xmlProp)) return null;
        var b64 = xmlProp.GetString();
        if (string.IsNullOrEmpty(b64)) return null;

        var bytes = Convert.FromBase64String(b64);

        // Save for diagnostics: %APPDATA%\HomeAccounting\last_receipt.xml
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HomeAccounting");
            Directory.CreateDirectory(logDir);
            File.WriteAllBytes(Path.Combine(logDir, "last_receipt.xml"), bytes);
        }
        catch { }

        return bytes;
    }

    static string? GetParam(string url, string name)
    {
        var q = url.IndexOf('?');
        if (q < 0) return null;
        foreach (var part in url[(q + 1)..].Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq > 0 && part[..eq] == name)
                return Uri.UnescapeDataString(part[(eq + 1)..]);
        }
        return null;
    }
}
