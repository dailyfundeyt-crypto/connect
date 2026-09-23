using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace ConnectDesktop;

public class BrowserAutomationServer
{
    private readonly HttpListener _listener;
    private readonly MainWindow _mainWindow;
    private readonly WebView2 _browserView;
    private bool _isRunning;
    public int Port { get; }

    public BrowserAutomationServer(MainWindow mainWindow, WebView2 browserView, int port = 3002)
    {
        _mainWindow = mainWindow;
        _browserView = browserView;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            _isRunning = true;
            Task.Run(ListenLoop);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Automation Server Error: {ex.Message}");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        try
        {
            _listener.Stop();
        }
        catch { }
    }

    private async Task<T> RunOnUiAsync<T>(Func<Task<T>> func)
    {
        var op = await Application.Current.Dispatcher.InvokeAsync(func);
        return await op;
    }

    private async Task RunOnUiAsync(Func<Task> func)
    {
        var op = await Application.Current.Dispatcher.InvokeAsync(func);
        await op;
    }

    private async Task ListenLoop()
    {
        while (_isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch when (!_isRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ListenLoop error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // CORS headers
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "";

            if (path == "/api/browser/status" && req.HttpMethod == "GET")
            {
                await HandleStatus(res);
                return;
            }

            if (path == "/api/browser/navigate" && req.HttpMethod == "POST")
            {
                await HandleNavigate(req, res);
                return;
            }

            if (path == "/api/browser/eval" && req.HttpMethod == "POST")
            {
                await HandleEval(req, res);
                return;
            }

            if (path == "/api/browser/click" && req.HttpMethod == "POST")
            {
                await HandleClick(req, res);
                return;
            }

            if (path == "/api/browser/type" && req.HttpMethod == "POST")
            {
                await HandleType(req, res);
                return;
            }

            if (path == "/api/browser/snapshot" && req.HttpMethod == "GET")
            {
                await HandleSnapshot(res);
                return;
            }

            if (path == "/api/browser/screenshot" && req.HttpMethod == "GET")
            {
                await HandleScreenshot(res);
                return;
            }

            res.StatusCode = 404;
            await SendJson(res, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            await SendJson(res, new { error = ex.Message });
        }
    }

    private async Task HandleStatus(HttpListenerResponse res)
    {
        string currentUrl = "";
        string currentTitle = "";
        bool isReady = false;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            currentUrl = _browserView.Source?.ToString() ?? "";
            currentTitle = _browserView.CoreWebView2?.DocumentTitle ?? "";
            isReady = _browserView.CoreWebView2 != null;
        });

        await SendJson(res, new
        {
            ok = true,
            browser = "WebView2",
            url = currentUrl,
            title = currentTitle,
            ready = isReady
        });
    }

    private async Task HandleNavigate(HttpListenerRequest req, HttpListenerResponse res)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(body);

        if (!data.TryGetProperty("url", out var urlProp) || string.IsNullOrWhiteSpace(urlProp.GetString()))
        {
            res.StatusCode = 400;
            await SendJson(res, new { error = "Missing 'url' parameter" });
            return;
        }

        var targetUrl = urlProp.GetString()!;
        if (!targetUrl.Contains("://")) targetUrl = "https://" + targetUrl;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _mainWindow.NavigateBrowser(targetUrl);
            _mainWindow.ShowBrowserTab();
        });

        await SendJson(res, new { ok = true, url = targetUrl, status = "navigating" });
    }

    private async Task HandleEval(HttpListenerRequest req, HttpListenerResponse res)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(body);

        if (!data.TryGetProperty("script", out var scriptProp))
        {
            res.StatusCode = 400;
            await SendJson(res, new { error = "Missing 'script' parameter" });
            return;
        }

        string result = await RunOnUiAsync(async () =>
        {
            if (_browserView.CoreWebView2 != null)
            {
                return await _browserView.CoreWebView2.ExecuteScriptAsync(scriptProp.GetString());
            }
            return "";
        });

        await SendJson(res, new { ok = true, result });
    }

    private async Task HandleClick(HttpListenerRequest req, HttpListenerResponse res)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(body);

        var selector = data.GetProperty("selector").GetString() ?? "";
        var script = $@"
            (() => {{
                const el = document.querySelector('{selector.Replace("'", "\\'")}');
                if (el) {{
                    el.scrollIntoView({{ behavior: 'instant', block: 'center' }});
                    el.click();
                    return true;
                }}
                return false;
            }})();
        ";

        string result = await RunOnUiAsync(async () =>
        {
            if (_browserView.CoreWebView2 != null)
            {
                return await _browserView.CoreWebView2.ExecuteScriptAsync(script);
            }
            return "false";
        });

        await SendJson(res, new { ok = result == "true", clicked = selector });
    }

    private async Task HandleType(HttpListenerRequest req, HttpListenerResponse res)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(body);

        var selector = data.GetProperty("selector").GetString() ?? "";
        var text = data.GetProperty("text").GetString() ?? "";

        var script = $@"
            (() => {{
                const el = document.querySelector('{selector.Replace("'", "\\'")}');
                if (el) {{
                    el.focus();
                    el.value = '{text.Replace("'", "\\'")}';
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }}
                return false;
            }})();
        ";

        string result = await RunOnUiAsync(async () =>
        {
            if (_browserView.CoreWebView2 != null)
            {
                return await _browserView.CoreWebView2.ExecuteScriptAsync(script);
            }
            return "false";
        });

        await SendJson(res, new { ok = result == "true", typed = text });
    }

    private async Task HandleSnapshot(HttpListenerResponse res)
    {
        string textSnapshot = "";
        string currentUrl = "";

        var result = await RunOnUiAsync<(string url, string raw)>(async () =>
        {
            if (_browserView.CoreWebView2 != null)
            {
                var url = _browserView.Source?.ToString() ?? "";
                var script = @"
                    (() => {
                        return JSON.stringify({
                            title: document.title,
                            url: location.href,
                            innerText: document.body ? document.body.innerText.substring(0, 10000) : ''
                        });
                    })();
                ";
                var raw = await _browserView.CoreWebView2.ExecuteScriptAsync(script);
                return (url, raw);
            }
            return ("", "");
        });

        currentUrl = result.url;
        textSnapshot = result.raw;

        await SendJson(res, new { ok = true, url = currentUrl, snapshot = textSnapshot });
    }

    private async Task HandleScreenshot(HttpListenerResponse res)
    {
        byte[]? imageBytes = await RunOnUiAsync(async () =>
        {
            if (_browserView.CoreWebView2 != null)
            {
                using var ms = new MemoryStream();
                await _browserView.CoreWebView2.CapturePreviewAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,
                    ms);
                return ms.ToArray();
            }
            return null;
        });

        if (imageBytes != null && imageBytes.Length > 0)
        {
            res.ContentType = "image/png";
            res.ContentLength64 = imageBytes.Length;
            await res.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
            res.Close();
        }
        else
        {
            res.StatusCode = 500;
            await SendJson(res, new { error = "Failed to capture preview" });
        }
    }

    private static async Task SendJson(HttpListenerResponse res, object obj)
    {
        res.ContentType = "application/json; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.Close();
    }
}
