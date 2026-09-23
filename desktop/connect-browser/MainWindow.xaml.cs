using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace ConnectDesktop;

public partial class MainWindow : Window
{
    public const string ConnectUrl = "http://127.0.0.1:3010";
    public const string DefaultBrowserHome = "https://www.google.com/";

    private bool _companyReady;
    private bool _browserReady;
    private BrowserAutomationServer? _automationServer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        try
        {
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow_Loaded starting\n");
            StatusText.Text = "Initialisiere Connect Desktop & AI-Browser…";

            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConnectDesktop", "WebView2Data");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            // 1. Initialisiere Workspace-Ansicht
            await CompanyView.EnsureCoreWebView2Async(env);
            WireCompanyView(CompanyView.CoreWebView2);
            _companyReady = true;
            CompanyView.CoreWebView2.Navigate(ConnectUrl);
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CompanyView ready\n");

            // 2. Initialisiere echten AI-Browser (WebView2)
            await BrowserView.EnsureCoreWebView2Async(env);
            WireBrowserView(BrowserView.CoreWebView2);
            _browserReady = true;
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] BrowserView ready\n");

            // 3. Starte lokalen Automation Server (Port 3002)
            try
            {
                _automationServer = new BrowserAutomationServer(this, BrowserView, 3002);
                _automationServer.Start();
                AiServerStatus.Text = "AI Bridge: Aktiv (Port 3002)";
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Automation server started\n");
            }
            catch (Exception exServer)
            {
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Automation server failed: {exServer}\n");
            }

            ShowCompanyTab();
            StatusText.Text = "Connect Desktop & AI-Browser bereit";
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow_Loaded complete\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow_Loaded ERROR: {ex}\n");
            MessageBox.Show(
                "Fehler beim Starten von WebView2:\n\n" + ex.Message +
                "\n\nBitte sicherstellen, dass die Microsoft Edge WebView2 Runtime installiert ist.",
                "Connect Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow_Closing triggered (Cancel={e.Cancel})\n");
        _automationServer?.Stop();
    }

    private void WireCompanyView(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        core.NavigationStarting += (_, args) =>
        {
            StatusText.Text = "Connect Workspace: Lade…";
        };

        core.NavigationCompleted += (_, args) =>
        {
            StatusText.Text = args.IsSuccess
                ? "Connect Workspace bereit"
                : "Connect Workspace offline (Läuft 'START-PC.cmd' oder Server auf Port 3010?)";
        };
    }

    private void WireBrowserView(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        core.SourceChanged += (_, _) =>
        {
            try
            {
                var src = BrowserView.Source?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(src)
                    && !src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    && !src.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    AddressBar.Text = src;
                }
            }
            catch { }
        };

        core.NavigationStarting += (_, args) =>
        {
            StatusText.Text = "AI-Browser lädt: " + args.Uri;
        };

        core.NavigationCompleted += (_, args) =>
        {
            StatusText.Text = args.IsSuccess
                ? "AI-Browser bereit: " + (core.DocumentTitle ?? core.Source)
                : "Ladefehler: " + args.WebErrorStatus;
        };
    }

    public void ShowCompanyTab()
    {
        TabCompany.IsChecked = true;
        TabBrowser.IsChecked = false;
        CompanyView.Visibility = Visibility.Visible;
        BrowserView.Visibility = Visibility.Collapsed;
        BrowserToolbar.Visibility = Visibility.Collapsed;
        StatusText.Text = "Ansicht: Connect Workspace";
    }

    public void ShowBrowserTab()
    {
        TabCompany.IsChecked = false;
        TabBrowser.IsChecked = true;
        CompanyView.Visibility = Visibility.Collapsed;
        BrowserView.Visibility = Visibility.Visible;
        BrowserToolbar.Visibility = Visibility.Visible;
        StatusText.Text = "Ansicht: Echter AI-Browser";

        if (_browserReady)
        {
            var cur = BrowserView.Source?.ToString() ?? "";
            if (string.IsNullOrEmpty(cur) || cur.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                NavigateBrowser(DefaultBrowserHome);
            }
        }
    }

    public void NavigateBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        AddressBar.Text = url;
        if (_browserReady && BrowserView.CoreWebView2 != null)
        {
            BrowserView.CoreWebView2.Navigate(url);
        }
    }

    private void TabCompany_Click(object sender, RoutedEventArgs e)
    {
        ShowCompanyTab();
    }

    private void TabBrowser_Click(object sender, RoutedEventArgs e)
    {
        ShowBrowserTab();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CoreWebView2?.CanGoBack == true)
            BrowserView.CoreWebView2.GoBack();
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CoreWebView2?.CanGoForward == true)
            BrowserView.CoreWebView2.GoForward();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BrowserView.CoreWebView2?.Reload();
    }

    private void BtnHome_Click(object sender, RoutedEventArgs e)
    {
        NavigateBrowser(DefaultBrowserHome);
    }

    private void BtnGo_Click(object sender, RoutedEventArgs e)
    {
        NavigateFromAddressBar();
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        NavigateFromAddressBar();
    }

    private void NavigateFromAddressBar()
    {
        var raw = (AddressBar.Text ?? "").Trim();
        if (string.IsNullOrEmpty(raw)) return;

        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            // Wenn Domain-ähnlich oder Suchbegriff
            if (raw.Contains('.') && !raw.Contains(' '))
                raw = "https://" + raw;
            else
                raw = "https://www.google.com/search?q=" + Uri.EscapeDataString(raw);
        }

        NavigateBrowser(raw);
    }

    private async void BtnAiAutomate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BrowserView.CoreWebView2 == null) return;
            var title = BrowserView.CoreWebView2.DocumentTitle;
            var url = BrowserView.Source?.ToString();

            // Schnappschuss als Test
            using var ms = new MemoryStream();
            await BrowserView.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png, ms);

            MessageBox.Show(
                $"AI-Browser erfasst:\n\nTitel: {title}\nURL: {url}\nScreenshot: {ms.Length / 1024} KB\n\nAI-Endpunkt ist aktiv auf http://127.0.0.1:3002/api/browser/",
                "Connect AI Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fehler beim Erfassen: " + ex.Message, "Connect AI Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
