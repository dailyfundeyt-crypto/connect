using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace ConnectDesktop;

public partial class MainWindow : Window
{
    public const string ConnectUrl = "http://127.0.0.1:3010";
    public const string DefaultBrowserHome = "https://www.google.com/";

    private BrowserAutomationServer? _automationServer;
    private BrowserTabs? _tabs;

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
            CompanyView.CoreWebView2.Navigate(ConnectUrl);
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CompanyView ready\n");

            // 2. Initialisiere Browser-Tabs (jeder Tab ist ein eigener WebView2, geteiltes userData → Login bleibt erhalten)
            _tabs = new BrowserTabs(
                BrowserTabHost,
                TabStrip,
                env,
                OnActiveTabChanged);
            _tabs.InitializeOrRestore(DefaultBrowserHome);
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] BrowserTabs ready\n");

            // 3. Starte lokalen Automation Server (Port 3002)
            try
            {
                _automationServer = new BrowserAutomationServer(this, _tabs, 3002);
                _automationServer.Start();
                AiServerStatus.Text = "AI Bridge: Aktiv (Port 3002)";
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Automation server started\n");
            }
            catch (Exception exServer)
            {
                AiServerStatus.Text = $"AI Bridge: FEHLER (Port 3002 belegt?)";
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Automation server failed: {exServer}\n");
                MessageBox.Show(
                    "BrowserAutomationServer konnte auf Port 3002 nicht starten.\n\n" +
                    "Häufigste Ursache: Port 3002 ist durch eine andere Instanz belegt.\n\nDetails siehe app.log.",
                    "Connect Desktop – AI Bridge",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            ShowBrowserTab();
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

    /// <summary>
    /// Called by <see cref="BrowserTabs"/> when the active tab changes (or when
    /// the active tab navigates). Re-pushes the current URL + tabId to the
    /// AI panel and refreshes the address bar / status text.
    /// </summary>
    private void OnActiveTabChanged(BrowserTab tab)
    {
        var url = tab.Url ?? "";
        if (!string.IsNullOrWhiteSpace(url)
            && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            AddressBar.Text = url;
        }
        StatusText.Text = $"AI-Browser: {tab.Id} · {tab.Title}";

        // Re-navigate the AI panel to update ?browserTab=... so the Comet banner
        // can show the right tab id.
        if (AiPanel.CoreWebView2 != null)
        {
            var target = $"http://127.0.0.1:3010/agents?browserTab={tab.Id}";
            var current = AiPanel.Source?.ToString() ?? "";
            if (!current.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                AiPanel.CoreWebView2.Navigate(target);
            }
        }

        // Broadcast to AI panel (in case the Web listener was set up after
        // navigation; re-post so the latest URL is always known).
        try
        {
            AiPanel.CoreWebView2?.PostWebMessageAsString(
                System.Text.Json.JsonSerializer.Serialize(new { type = "active_tab_url", url = url, tabId = tab.Id }));
        }
        catch { }
    }

    public void SetStatus(string text) => StatusText.Text = text;

    /// <summary>The active tab's WebView2, or null before tabs initialize.</summary>
    public Microsoft.Web.WebView2.Wpf.WebView2? ActiveTabView => _tabs?.ActiveTab?.View;

    /// <summary>Look up a tab's WebView2 by id, or null if not found.</summary>
    public Microsoft.Web.WebView2.Wpf.WebView2? TabView(string tabId)
        => _tabs?.Tabs.FirstOrDefault(t => t.Id == tabId)?.View;

    /// <summary>Expose the BrowserTabs for code paths that need the full list.</summary>
    public BrowserTabs? TabsAccessor => _tabs;

    public void ShowBrowserTab()
    {
        TabBrowser.IsChecked = true;
        TabWorkspace.IsChecked = false;
        CompanyView.Visibility = Visibility.Collapsed;
        BrowserToolbar.Visibility = Visibility.Visible;
        AiPanel.Visibility = Visibility.Visible;
        BrowserTabHost.Visibility = Visibility.Visible;
        TabStrip.Visibility = Visibility.Visible;
        StatusText.Text = "Ansicht: AI-Browser + Agent-Panel";
    }

    public void ShowWorkspaceTab()
    {
        TabBrowser.IsChecked = false;
        TabWorkspace.IsChecked = true;
        CompanyView.Visibility = Visibility.Visible;
        BrowserToolbar.Visibility = Visibility.Collapsed;
        BrowserTabHost.Visibility = Visibility.Collapsed;
        TabStrip.Visibility = Visibility.Collapsed;
        AiPanel.Visibility = Visibility.Visible;
        StatusText.Text = "Ansicht: Workspace + Agent-Panel";
    }

    public void NavigateBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        AddressBar.Text = url;
        _tabs?.NavigateActive(url);
    }

    private void TabWorkspace_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspaceTab();
    }

    private void TabBrowser_Click(object sender, RoutedEventArgs e)
    {
        ShowBrowserTab();
    }

    private async void BtnNewTab_Click(object sender, RoutedEventArgs e)
    {
        if (_tabs == null) return;
        await _tabs.CreateTabAsync(DefaultBrowserHome);
    }

    private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
    {
        var active = _tabs?.ActiveTab;
        if (active == null) return;
        _tabs?.Close(active.Id);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        var view = _tabs?.ActiveTab?.View;
        if (view?.CoreWebView2?.CanGoBack == true)
            view.CoreWebView2.GoBack();
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        var view = _tabs?.ActiveTab?.View;
        if (view?.CoreWebView2?.CanGoForward == true)
            view.CoreWebView2.GoForward();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _tabs?.ActiveTab?.View.CoreWebView2?.Reload();
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
            var view = _tabs?.ActiveTab?.View;
            if (view?.CoreWebView2 == null) return;
            var title = view.CoreWebView2.DocumentTitle;
            var url = view.Source?.ToString();

            // Schnappschuss als Test
            using var ms = new MemoryStream();
            await view.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png, ms);

            MessageBox.Show(
                $"AI-Browser erfasst:\n\nTab: {_tabs?.ActiveTab?.Id}\nTitel: {title}\nURL: {url}\nScreenshot: {ms.Length / 1024} KB\n\nAI-Endpunkt ist aktiv auf http://127.0.0.1:3002/api/browser/",
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
