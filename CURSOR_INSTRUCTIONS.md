# Connect Desktop — Master-Anweisung für Cursor

> **Ziel:** Die Desktop-App (`Connect Desktop`) soll optisch und funktional auf das höchste Niveau von **Arc Browser** und **Perplexity Comet** gebracht werden. Alle Funktionen der Webversion (localhost:3010) müssen nahtlos im Desktop-Browser funktionieren, ohne Einschränkungen oder abgegrenzte Boxen.

---

## 1. Projekt-Architektur verstehen

Das Projekt besteht aus zwei verknüpften Teilen:

```
├── OpenBot\                                       [Web-App: Frontend + Backend]
│   ├── app\                                       (React 19, Vite, Tailwind CSS v4, TanStack Router)
│   │   ├── src\routes\_authed\_app.tsx            (Zentrales Dual-Role Layout für Desktop & Web)
│   │   ├── src\components\companies\              (LevelChromeSidebar, LabToolsNav, CompanyAgentsNav)
│   │   ├── src\lib\desktop-bridge.ts              (IPC-Brücke zwischen Web & C# WPF)
│   │   └── src\lib\companies\level3-tools.ts      (Tab-Ordner: Technische, Fundamentals, etc.)
│   ├── server\                                    (Hono API, PostgreSQL, Better-Auth)
│   │
│   └── EinfacherBrowser\                          [Direkt hier im Workspace verlinkt!]
│       ├── MainWindow.xaml                        (WPF Browser-Fenster, Toolbar, Splitter)
│       ├── MainWindow.xaml.cs                     (WebView2 Management, IPC-Handler, Theme-Sync)
│       └── App.xaml.cs                            (Single-Instance Mutex, Lifecycle)
```

### Wie der Desktop-Browser funktioniert (Dual-Role WebView2):
1. **Linke Spalte (`ConnectView`):**
   * Injiziert `window.__CONNECT_SIDEBAR__ = true`.
   * Rendert **nur** die Sidebar (Breite: 300px) mit Arc-Ordnern, Räumen und Agenten.
2. **Rechte Hauptfläche (`BrowserView`):**
   * Injiziert `window.__CONNECT_SIDEBAR__ = false`.
   * Rendert entweder die vollen internen Connect-Seiten (`/agents`, `/channel/:id`, `/settings`) oder externe Webseiten (TradingView, Lovable, Google, etc.).
   * Teilt exakt dasselbe Profil (`%LOCALAPPDATA%\ConnectDesktop\ChromiumProfile`) mit dauerhaften Cookies & Logins.
3. **IPC-Kommunikation (`desktop-bridge.ts`):**
   * Web sendet via `window.chrome.webview.postMessage`:
     * `{ type: "navigate", url }` -> Steuert die rechte Browser-Ansicht.
     * `{ type: "level_changed", level }` -> Passt Sidebar-Modus an.
     * `{ type: "theme_changed", dark }` -> Passt native WPF-Farben an.
   * C# sendet zurück:
     * `{ type: "browser_status", url, title }` -> Meldet aktuelle Seite an Connect.

---

## 2. Deine Aufgaben in Cursor

### Aufgabe A: UI-Design der Sidebar perfektionieren (`OpenBot/app`)
* **Datei:** `app/src/components/companies/level-chrome-sidebar.tsx` & `lab-tools-nav.tsx`
* **Vorgaben:**
  1. **Logo & Header:** Das 2. Connect-Logo (schwarzer Vortex/Spiral-Aperture) prominent und gestochen scharf oben platzieren.
  2. **Spaces & Ordner (Arc-Style):**
     * Die Ordnerstruktur (**Technische**, **Fundamentals**, **Sentimentalle**, **Sektorielle**, **Build**, **AI**) optisch aufwerten mit klaren Icons, feinen Zählern (Anzahl Tabs) und flüssigen Aufklapp-Animationen.
     * Ordnerfarben und Icons im minimalistischen Arc-Stil (z. B. dezente Pillen, farbige Akzente beim Überfahren).
  3. **Agenten-Bereich:**
     * Einzelne Agenten (`CompanyAgentsNav`) mit sauber gerenderten Avataren, Namenskürzeln, Status-Dots (Online / Aktiv) und direkter Klickbarkeit anzeigen.
     * Klick auf einen Agenten öffnet sofort den Chat im Hauptbereich (`/channel/$channelId`).
  4. **Footer:**
     * Minimalistische Benutzer-Karte mit Avatar, Email, Dark/Light-Toggle und direktem Einstellungs-Link.

### Aufgabe B: Desktop Browser-Toolbar veredeln (`EinfacherBrowser`)
* **Dateien:** `MainWindow.xaml` und `MainWindow.xaml.cs`
* **Vorgaben:**
  1. **Nahtloser Übergang:** Die Toolbar muss dieselben Farben, Rundungen und Schatten wie die Webversion haben.
  2. **Smarte Adressleiste:**
     * Suchbegriffe automatisch über Google suchen.
     * URLs sauber formatieren.
     * Schloss-Symbol `🔒` bei HTTPS.
     * Tastaturkürzel: `Strg+L` oder `Strg+T` setzt den Fokus direkt in die Adressleiste.
  3. **Comet-Style Agenten-Button:**
     * Ein eleganter Pill-Button **✦ Agenten** bringt den Nutzer jederzeit mit einem Klick (oder `Strg+H`) zurück zum Agenten-Hub.
  4. **Dark/Light Mode Synchronisation:**
     * Sicherstellen, dass Hintergrund (`#09090b`), Rahmen (`#27272a`), Toolbar und Textfarben beim Umschalten im Frontend millimetergenau synchron umschalten.

### Aufgabe C: Comet AI-Assistant Slide-Over Panel
* **Ziel:** Wenn der Nutzer auf einer Website surft (z. B. TradingView oder Dokumentationen), soll wie bei **Comet** ein KI-Assistent die aktuelle Seite verstehen.
* **Umsetzung:**
  * In `MainWindow.xaml.cs` empfängt Connect bereits `{ type: "browser_status", url, title }`.
  * Baue in der Web-App (oder als Overlay) ein kompaktes KI-Eingabefeld ein, das Kontext-Aktionen anbietet:
    * *"Analysiere diese Seite mit Agent X"*
    * *"Erstelle Zusammenfassung"*
    * *"Führe Recherche auf dieser Domain durch"*

### Aufgabe D: Volle Funktionsabdeckung sicherstellen
* Alle Routen müssen im Desktop-Browser uneingeschränkt nutzbar sein:
  * `/agents` (Marketplace & Agenten-Hub)
  * `/channel/:channelId` (Chat mit Bots)
  * `/company/:companyId` (Firmenseite, Focus & Projekte)
  * `/settings` (Einstellungen, MCP-Konnektoren, API-Keys)
* **Wichtig:** Keine Funktion darf mehr ein externes Terminal oder externes Chrome-Fenster verlangen — alles muss innerhalb dieses Fensters ablaufen.

---

## 3. Befehle zum Bauen & Testen

### Frontend & Backend (WSL Ubuntu):
```bash
cd "/mnt/c/Users/Kunc GmbH/Desktop/OpenBot"
# Typen prüfen
bun run typecheck
# Dev-Server läuft via systemd im Hintergrund:
systemctl status openbot
```

### Desktop-App kompilieren & veröffentlichen (Windows PowerShell):
```powershell
cd "C:\Users\Kunc GmbH\Desktop\EinfacherBrowser"
dotnet publish -c Release -r win-x64 --self-contained false
Copy-Item "icon.ico" "bin\Release\net8.0-windows\win-x64\publish\icon.ico" -Force
```

### Desktop-App starten:
```powershell
Start-Process "C:\Users\Kunc GmbH\Desktop\EinfacherBrowser\bin\Release\net8.0-windows\win-x64\publish\Connect Desktop.exe"
```
