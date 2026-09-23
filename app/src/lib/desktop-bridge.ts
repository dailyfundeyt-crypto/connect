/**
 * Desktop IPC bridge for Connect Desktop (WPF / WebView2 / CEF).
 * Enables seamless communication between the Connect web UI and the native Chromium browser host.
 */

export function isDesktopApp(): boolean {
  return (
    typeof window !== "undefined" &&
    Boolean((window as any).chrome?.webview?.postMessage)
  );
}

export function sendDesktopMessage(payload: {
  type: string;
  [key: string]: unknown;
}): void {
  if (isDesktopApp()) {
    try {
      (window as any).chrome.webview.postMessage(payload);
    } catch (e) {
      console.warn("Desktop bridge postMessage failed:", e);
    }
  }
}

export function navigateDesktopBrowser(url: string): void {
  if (!url || typeof url !== "string") return;
  const target = url.trim();
  if (!target) return;
  sendDesktopMessage({ type: "navigate", url: target });
}

export function notifyDesktopLevel(level: number): void {
  sendDesktopMessage({ type: "level_changed", level });
}

export function notifyDesktopTheme(dark: boolean): void {
  sendDesktopMessage({ type: "theme_changed", dark });
}

export function notifyDesktopAuth(email?: string): void {
  sendDesktopMessage({ type: "auth_status", email: email || null });
}
