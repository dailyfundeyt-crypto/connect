using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ConnectDesktop;

/// <summary>
/// Manages a list of WebView2 browser tabs inside a single host Grid.
///
/// - Each tab is its own WebView2 instance (WebView2 has no native tab concept;
///   multiple WebViews share the same CoreWebView2Environment and therefore
///   share the userDataFolder, which means shared login cookies).
/// - Exactly one tab is "active" at a time; non-active tabs are Visibility=Collapsed.
/// - Tab list and active tab id are persisted to tabs.json next to WebView2Data
///   so the user reopens the same set of tabs across app restarts.
/// - Tab ids are stable strings ("tab-1", "tab-2", …) so that downstream
///   consumers (BrowserAutomationServer, AiPanel PostWebMessage) can refer to
///   a tab across renames.
/// </summary>
public sealed class BrowserTabs
{
    private readonly Grid _host;
    private readonly ListBox _tabStrip;
    private readonly Action<BrowserTab> _onActiveChanged;
    private readonly CoreWebView2Environment _env;

    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _active;

    public IReadOnlyList<BrowserTab> Tabs => _tabs;
    public BrowserTab? ActiveTab => _active;

    /// <summary>Path to the persistence file. Lives next to WebView2Data.</summary>
    public static string PersistencePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ConnectDesktop",
        "tabs.json");

    public BrowserTabs(
        Grid host,
        ListBox tabStrip,
        CoreWebView2Environment env,
        Action<BrowserTab> onActiveChanged)
    {
        _host = host;
        _tabStrip = tabStrip;
        _env = env;
        _onActiveChanged = onActiveChanged;

        _tabStrip.SelectionChanged += (_, _) =>
        {
            if (_tabStrip.SelectedItem is BrowserTab tab && tab != _active)
                SwitchTo(tab.Id);
        };
    }

    /// <summary>
    /// Load tabs from persistence if present, otherwise create the default
    /// single tab pointing at <paramref name="defaultUrl"/>. Always finishes
    /// with at least one tab and a selected tab.
    /// </summary>
    public async void InitializeOrRestore(string defaultUrl)
    {
        var restored = TryRestore();
        if (restored.Count == 0)
        {
            await CreateTabAsync(defaultUrl);
        }
        else
        {
            foreach (var (id, url) in restored)
            {
                await CreateTabAsync(url, id);
            }
            var activeId = ReadActiveId();
            if (activeId != null && _tabs.Any(t => t.Id == activeId))
            {
                SwitchTo(activeId);
            }
            else if (_tabs.Count > 0)
            {
                SwitchTo(_tabs[0].Id);
            }
        }
        RenderTabStrip();
    }

    /// <summary>
    /// Create a new tab. If <paramref name="id"/> is null, a fresh "tab-N" id
    /// is assigned. Always makes the new tab active.
    /// </summary>
    public async System.Threading.Tasks.Task<BrowserTab> CreateTabAsync(string url, string? id = null)
    {
        id ??= NextId();
        var view = new WebView2 { Visibility = Visibility.Collapsed };
        _host.Children.Add(view);
        await view.EnsureCoreWebView2Async(_env);
        WireTab(view, id);
        var tab = new BrowserTab(id, view, url);
        _tabs.Add(tab);
        view.CoreWebView2.Navigate(url);
        SwitchTo(id);
        RenderTabStrip();
        Persist();
        return tab;
    }

    /// <summary>
    /// Make the tab with the given id the visible one. No-op if id is unknown
    /// or already active.
    /// </summary>
    public void SwitchTo(string id)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == id);
        if (tab == null || tab == _active) return;

        foreach (var t in _tabs)
            t.View.Visibility = Visibility.Collapsed;

        tab.View.Visibility = Visibility.Visible;
        _active = tab;
        _onActiveChanged(tab);
        RenderTabStrip();
        Persist();
    }

    /// <summary>
    /// Close the tab with the given id. If it was the active tab, switch to
    /// the nearest neighbour. Refuses to close the last remaining tab (it
    /// navigates that tab to about:blank instead).
    /// </summary>
    public void Close(string id)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == id);
        if (tab == null) return;

        if (_tabs.Count == 1)
        {
            tab.View.CoreWebView2?.Navigate("about:blank");
            tab.Url = "about:blank";
            tab.Title = "New Tab";
            RenderTabStrip();
            Persist();
            return;
        }

        var idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        _host.Children.Remove(tab.View);
        tab.View.Dispose();

        var wasActive = _active == tab;
        if (wasActive)
        {
            var neighbour = _tabs[Math.Max(0, idx - 1)];
            SwitchTo(neighbour.Id);
        }
        RenderTabStrip();
        Persist();
    }

    /// <summary>Navigate the active tab to a URL. No-op if no active tab.</summary>
    public void NavigateActive(string url)
    {
        if (_active == null || string.IsNullOrWhiteSpace(url)) return;
        _active.View.CoreWebView2?.Navigate(url);
    }

    private void WireTab(WebView2 view, string id)
    {
        var core = view.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        core.DocumentTitleChanged += (_, _) =>
        {
            var tab = _tabs.FirstOrDefault(t => t.View == view);
            if (tab == null) return;
            tab.Title = core.DocumentTitle ?? tab.Url;
            RenderTabStrip();
            Persist();
        };

        core.SourceChanged += (_, _) =>
        {
            var tab = _tabs.FirstOrDefault(t => t.View == view);
            if (tab == null) return;
            tab.Url = view.Source?.ToString() ?? "";
            if (tab == _active)
                _onActiveChanged(tab);
            RenderTabStrip();
            Persist();
        };

        core.NavigationStarting += (_, args) =>
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var t = _tabs.FirstOrDefault(x => x.View == view);
                if (t == _active && Application.Current.MainWindow is MainWindow mw)
                    mw.SetStatus("AI-Browser lädt: " + args.Uri);
            }));
    }

    private void RenderTabStrip()
    {
        _tabStrip.Items.Clear();
        foreach (var tab in _tabs)
        {
            var item = new ListBoxItem
            {
                Content = tab.DisplayLabel,
                Tag = tab.Id,
                IsSelected = tab == _active,
                ToolTip = tab.Url,
            };
            _tabStrip.Items.Add(item);
        }
    }

    private string NextId()
    {
        var max = 0;
        foreach (var t in _tabs)
        {
            if (t.Id.StartsWith("tab-", StringComparison.Ordinal)
                && int.TryParse(t.Id.AsSpan(4), out var n)
                && n > max) max = n;
        }
        return $"tab-{max + 1}";
    }

    // ---- Persistence ----

    [Serializable]
    private class PersistedState
    {
        public List<string> Tabs { get; set; } = new();
        public List<string> Urls { get; set; } = new();
        public string? ActiveId { get; set; }
    }

    private List<(string id, string url)> TryRestore()
    {
        try
        {
            if (!File.Exists(PersistencePath)) return new List<(string, string)>();
            var json = File.ReadAllText(PersistencePath);
            var state = JsonSerializer.Deserialize<PersistedState>(json);
            if (state == null || state.Tabs.Count != state.Urls.Count) return new List<(string, string)>();
            var pairs = new List<(string, string)>(state.Tabs.Count);
            for (var i = 0; i < state.Tabs.Count; i++)
                pairs.Add((state.Tabs[i], state.Urls[i]));
            return pairs;
        }
        catch
        {
            return new List<(string, string)>();
        }
    }

    private string? ReadActiveId()
    {
        try
        {
            if (!File.Exists(PersistencePath)) return null;
            var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(PersistencePath));
            return state?.ActiveId;
        }
        catch { return null; }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(PersistencePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var state = new PersistedState
            {
                Tabs = _tabs.Select(t => t.Id).ToList(),
                Urls = _tabs.Select(t => t.Url).ToList(),
                ActiveId = _active?.Id,
            };
            File.WriteAllText(PersistencePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* persistence is best-effort */ }
    }
}

public sealed class BrowserTab
{
    public string Id { get; }
    public WebView2 View { get; }
    public string Url { get; set; }
    public string Title { get; set; }

    public BrowserTab(string id, WebView2 view, string url)
    {
        Id = id;
        View = view;
        Url = url;
        Title = id;
    }

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title) && Title != Id)
            {
                var t = Title.Length > 24 ? Title[..24] + "…" : Title;
                return $"{t}  ({Id})";
            }
            return Id;
        }
    }
}
