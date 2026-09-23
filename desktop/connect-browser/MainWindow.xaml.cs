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
        try
        {
            StatusText.Text = "Initialisiere Connect Desktop & AI-Browser…";

            // 1. Initialisiere Workspace-Ansicht
            await CompanyView.EnsureCoreWebView2Async();
            WireCompanyView(CompanyView.CoreWebView2);
            _companyReady = true;
            CompanyView.CoreWebView2.Navigate(ConnectUrl);

            // 2. Initialisiere echten AI-Browser (WebView2)
            await BrowserView.EnsureCoreWebView2Async();
            WireBrowserView(BrowserView.CoreWebView2);
            _browserReady = true;

            // 3. Starte lokalen Automation Server (Port 3002)
            _automationServer = new BrowserAutomationServer(this, BrowserView, 3002);
            _automationServer.Start();
            AiServerStatus.Text = "AI Bridge: Aktiv (Port 3002)";

            ShowCompanyTab();
            StatusText.Text = "Connect Desktop & AI-Browser bereit";
        }
        catch (Exception ex)
        {
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
