using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetMerossID
{
    public partial class Form1 : Form
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;

        private readonly List<(string ip, string uuid, string msgid, string ts, string sign)> _tokens
            = new List<(string, string, string, string, string)>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        private string _capturedIp = "";
        private string _capturedUuid = "";
        private bool _autoSaved = false;

        private const int MinTokens = 21;
        private const int ProxyPort = 8080;

        public Form1()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────
        //  起動・終了
        // ─────────────────────────────────────────

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = "";
            string localIp = GetLocalIPv4();

            labelStatus.Text =
                $"HTTPプロキシ起動中: {localIp}:{ProxyPort}\r\n"
              + $"Android の Wi-Fi プロキシ設定 → ホスト名: {localIp}  ポート: {ProxyPort}\r\n"
              + "設定後、Meross アプリで消費電力を表示するとデータが流れ始めます。";

            UpdateProgress();

            try
            {
                StartProxy();
            }
            catch (Exception ex)
            {
                labelStatus.Text = $"プロキシ起動失敗: {ex.Message}";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _listener?.Stop();
            base.OnFormClosing(e);
        }

        // ─────────────────────────────────────────
        //  プロキシサーバー
        // ─────────────────────────────────────────

        private void StartProxy()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, ProxyPort);
            _listener.Start();
            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch { }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    var clientStream = client.GetStream();
                    var (headerText, bodyBytes) = await ReadHttpRequestAsync(clientStream, ct);
                    if (headerText == null) return;

                    // リクエスト行のパース
                    string[] rawLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string requestLine = rawLines[0];
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpperInvariant();
                    string target = parts[1];
                    string httpVer = parts.Length > 2 ? parts[2] : "HTTP/1.1";

                    // HTTPS CONNECT トンネル
                    if (method == "CONNECT")
                    {
                        byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                        await clientStream.WriteAsync(ok, 0, ok.Length, ct);

                        string[] hp = target.Split(':');
                        string cHost = hp[0];
                        int cPort = hp.Length > 1 && int.TryParse(hp[1], out int p) ? p : 443;

                        using (var upstream = new TcpClient())
                        {
                            await upstream.ConnectAsync(cHost, cPort);
                            await RelayBidirectionalAsync(clientStream, upstream.GetStream(), ct);
                        }
                        return;
                    }

                    if (!target.StartsWith("http://")) return;

                    Uri uri = new Uri(target);
                    string host = uri.Host;
                    int port = uri.IsDefaultPort ? 80 : uri.Port;

                    // ヘッダーを上流向けに再構成（絶対URIを相対URIに変換、接続を close に）
                    string newRequestLine = $"{method} {uri.PathAndQuery} {httpVer}";
                    string restHeaders = headerText.Substring(requestLine.Length); // "\r\n" + headers + "\r\n\r\n"
                    restHeaders = Regex.Replace(restHeaders, @"\r\nProxy-Connection:[^\r\n]*", "", RegexOptions.IgnoreCase);
                    restHeaders = Regex.Replace(restHeaders, @"\r\nConnection:[^\r\n]*", "\r\nConnection: close", RegexOptions.IgnoreCase);
                    if (!restHeaders.Contains("Connection:"))
                        restHeaders = restHeaders.Replace("\r\n\r\n", "\r\nConnection: close\r\n\r\n");

                    byte[] fwdHeader = Encoding.UTF8.GetBytes(newRequestLine + restHeaders);

                    using (var upstream = new TcpClient())
                    {
                        await upstream.ConnectAsync(host, port);
                        var upStream = upstream.GetStream();

                        await upStream.WriteAsync(fwdHeader, 0, fwdHeader.Length, ct);
                        if (bodyBytes != null && bodyBytes.Length > 0)
                            await upStream.WriteAsync(bodyBytes, 0, bodyBytes.Length, ct);

                        byte[] response = await ReadHttpResponseAsync(upStream, ct);
                        if (response != null && response.Length > 0)
                            await clientStream.WriteAsync(response, 0, response.Length, ct);
                    }

                    // ローカル IP への HTTP リクエストのみトークン抽出
                    if (IsLocalIP(host) && bodyBytes != null && bodyBytes.Length > 0)
                    {
                        string bodyText = Encoding.UTF8.GetString(bodyBytes);
                        TryExtractTokens(host, bodyText);
                    }
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────
        //  HTTP 読み込みヘルパー
        // ─────────────────────────────────────────

        private async Task<(string headers, byte[] body)> ReadHttpRequestAsync(
            NetworkStream stream, CancellationToken ct)
        {
            var buf = new List<byte>(2048);
            var oneByte = new byte[1];

            using (var timedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timedCts.CancelAfter(10000);
                try
                {
                    while (true)
                    {
                        int r = await stream.ReadAsync(oneByte, 0, 1, timedCts.Token);
                        if (r == 0) return (null, null);
                        buf.Add(oneByte[0]);

                        int n = buf.Count;
                        if (n >= 4
                            && buf[n - 4] == '\r' && buf[n - 3] == '\n'
                            && buf[n - 2] == '\r' && buf[n - 1] == '\n')
                            break;

                        if (n > 65536) return (null, null);
                    }
                }
                catch { return (null, null); }
            }

            string headerText = Encoding.UTF8.GetString(buf.ToArray());

            // Content-Length があればボディを読む
            int bodyLen = 0;
            Match clMatch = Regex.Match(headerText, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
            if (clMatch.Success) int.TryParse(clMatch.Groups[1].Value, out bodyLen);

            byte[] bodyBytes = new byte[bodyLen];
            int read = 0;

            if (bodyLen > 0)
            {
                using (var timedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timedCts.CancelAfter(10000);
                    try
                    {
                        while (read < bodyLen)
                        {
                            int r = await stream.ReadAsync(bodyBytes, read, bodyLen - read, timedCts.Token);
                            if (r == 0) break;
                            read += r;
                        }
                    }
                    catch { }
                }
            }

            return (headerText, bodyBytes);
        }

        private async Task<byte[]> ReadHttpResponseAsync(NetworkStream stream, CancellationToken ct)
        {
            var result = new MemoryStream();
            var oneByte = new byte[1];
            var headerBuf = new List<byte>(2048);

            using (var timedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timedCts.CancelAfter(8000);
                try
                {
                    // ヘッダーを \r\n\r\n まで読む
                    while (true)
                    {
                        int r = await stream.ReadAsync(oneByte, 0, 1, timedCts.Token);
                        if (r == 0) break;
                        headerBuf.Add(oneByte[0]);
                        result.WriteByte(oneByte[0]);

                        int n = headerBuf.Count;
                        if (n >= 4
                            && headerBuf[n - 4] == '\r' && headerBuf[n - 3] == '\n'
                            && headerBuf[n - 2] == '\r' && headerBuf[n - 1] == '\n')
                            break;
                        if (n > 65536) break;
                    }

                    string headerText = Encoding.UTF8.GetString(headerBuf.ToArray());

                    Match clMatch = Regex.Match(headerText, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
                    if (clMatch.Success && int.TryParse(clMatch.Groups[1].Value, out int bodyLen))
                    {
                        byte[] body = new byte[bodyLen];
                        int read = 0;
                        while (read < bodyLen)
                        {
                            int r = await stream.ReadAsync(body, read, bodyLen - read, timedCts.Token);
                            if (r == 0) break;
                            read += r;
                        }
                        result.Write(body, 0, read);
                    }
                    else
                    {
                        // Connection: close の場合は閉じるまで読む
                        byte[] tmp = new byte[4096];
                        int r;
                        while ((r = await stream.ReadAsync(tmp, 0, tmp.Length, timedCts.Token)) > 0)
                            result.Write(tmp, 0, r);
                    }
                }
                catch { }
            }

            return result.ToArray();
        }

        private Task RelayBidirectionalAsync(Stream a, Stream b, CancellationToken ct)
        {
            async Task Copy(Stream src, Stream dst)
            {
                byte[] buf = new byte[4096];
                try
                {
                    int r;
                    while ((r = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                        await dst.WriteAsync(buf, 0, r, ct);
                }
                catch { }
            }

            var t1 = Task.Run(() => Copy(a, b), ct);
            var t2 = Task.Run(() => Copy(b, a), ct);
            return Task.WhenAny(t1, t2);
        }

        // ─────────────────────────────────────────
        //  トークン抽出・UI 更新
        // ─────────────────────────────────────────

        private bool IsLocalIP(string ip)
        {
            return Regex.IsMatch(ip, @"^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)");
        }

        private void TryExtractTokens(string ip, string body)
        {
            Match msgMatch  = Regex.Match(body, "\"messageId\":\"([0-9a-f]+)\"");
            Match signMatch = Regex.Match(body, "\"sign\":\"([0-9a-f]+)\"");
            Match tsMatch   = Regex.Match(body, "\"timestamp\":(\\d+)");
            Match uuidMatch = Regex.Match(body, "\"uuid\":\"([0-9a-fA-F]+)\"");

            if (!msgMatch.Success || !signMatch.Success || !tsMatch.Success) return;

            string msgid = msgMatch.Groups[1].Value;
            string sign  = signMatch.Groups[1].Value;
            string ts    = tsMatch.Groups[1].Value;
            string uuid  = uuidMatch.Success ? uuidMatch.Groups[1].Value : "";
            string key   = $"{msgid}|{ts}|{sign}";

            lock (_seen)
            {
                if (_seen.Contains(key)) return;
                _seen.Add(key);
                _tokens.Add((ip, uuid, msgid, ts, sign));

                if (string.IsNullOrEmpty(_capturedIp)) _capturedIp = ip;
                if (string.IsNullOrEmpty(_capturedUuid) && !string.IsNullOrEmpty(uuid))
                    _capturedUuid = uuid;
            }

            this.BeginInvoke(new Action(UpdateProgress));
        }

        private void UpdateProgress()
        {
            int count;
            lock (_seen) { count = _tokens.Count; }

            labelProgress.Text = $"取得済みトークン: {count} / {MinTokens}";

            var sb = new StringBuilder();
            lock (_seen)
            {
                string ip   = !string.IsNullOrEmpty(_capturedIp)   ? _capturedIp   : "UNKNOWN";
                string uuid = !string.IsNullOrEmpty(_capturedUuid) ? _capturedUuid : "UNKNOWN";

                sb.Append($"IP,{ip}\r\n");
                sb.Append($"UUID,{uuid}\r\n");
                sb.Append("rowno,msgid,ts,sign\r\n");

                // 元の実装に合わせ index 0 はスキップして 1 から出力
                for (int i = 1; i < _tokens.Count; i++)
                {
                    var t = _tokens[i];
                    sb.Append($"{i:00},{t.msgid},{t.ts},{t.sign}\r\n");
                }
            }
            textBox1.Text = sb.ToString();

            if (count >= MinTokens && !_autoSaved)
            {
                _autoSaved = true;
                SaveCsv();
                labelProgress.Text += "  →  merossId.csv を自動保存しました！";
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text)) return;
            SaveCsv();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            lock (_seen)
            {
                _tokens.Clear();
                _seen.Clear();
                _capturedIp = "";
                _capturedUuid = "";
                _autoSaved = false;
            }
            textBox1.Text = "";
            UpdateProgress();
        }

        private void SaveCsv()
        {
            string fileName = "merossId.csv";
            File.WriteAllText(fileName, textBox1.Text, Encoding.UTF8);

            ProcessStartInfo psi = new ProcessStartInfo(
                "explorer.exe", $"/select,{Path.GetFullPath(fileName)}");
            Process.Start(psi);
        }

        // ─────────────────────────────────────────
        //  ユーティリティ
        // ─────────────────────────────────────────

        private string GetLocalIPv4()
        {
            try
            {
                foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(addr))
                        return addr.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
