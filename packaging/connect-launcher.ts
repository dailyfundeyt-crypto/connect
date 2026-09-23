/**
 * Connect Windows launcher — double-click .exe → copy into Downloads\Connect →
 * start stack → open app window → on Enter/Ctrl+C stop everything.
 *
 * Built with: bun build --compile --target=bun-windows-x64 …
 */

import { existsSync, mkdirSync, copyFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { spawn, spawnSync } from "node:child_process";

// Embedded by `bun build --asset=…/connect-src.zip`
import connectZip from "./connect-src.zip" with { type: "file" };

const APP_URL = process.env.CONNECT_URL ?? "http://127.0.0.1:3010";
const DEST = join(homedir(), "Downloads", "Connect");

function log(msg: string) {
  console.log(`[Connect] ${msg}`);
}

function which(cmd: string): string | null {
  const r = spawnSync("where.exe", [cmd], { encoding: "utf8" });
  if (r.status !== 0) return null;
  const line = (r.stdout || "").split(/\r?\n/).map((s) => s.trim()).find(Boolean);
  return line || null;
}

function run(
  cmd: string,
  args: string[],
  opts: { cwd?: string; env?: Record<string, string> } = {},
): number {
  const r = spawnSync(cmd, args, {
    cwd: opts.cwd,
    env: { ...process.env, ...opts.env },
    stdio: "inherit",
    shell: false,
  });
  return r.status ?? 1;
}

function waitForUrl(url: string, seconds: number): boolean {
  const deadline = Date.now() + seconds * 1000;
  while (Date.now() < deadline) {
    try {
      const res = spawnSync(
        "curl.exe",
        ["-sf", "-o", "NUL", "--max-time", "2", url],
        { encoding: "utf8" },
      );
      if (res.status === 0) return true;
    } catch {
      /* retry */
    }
    spawnSync("timeout.exe", ["/t", "2", "/nobreak"], { stdio: "ignore" });
  }
  return false;
}

async function extractProject(): Promise<void> {
  mkdirSync(DEST, { recursive: true });
  const marker = join(DEST, "START.sh");
  if (existsSync(marker) && !process.argv.includes("--reinstall")) {
    log(`Bereits installiert: ${DEST}`);
    return;
  }

  log(`Kopiere Connect nach ${DEST} …`);
  const zipPath = join(DEST, "_connect-src.zip");
  // `connectZip` is an absolute path to the embedded asset inside the exe.
  const src =
    typeof connectZip === "string"
      ? connectZip
      : String(connectZip);
  const bytes = await Bun.file(src).arrayBuffer();
  await Bun.write(zipPath, bytes);

  // Prefer PowerShell Expand-Archive (built into Windows).
  const ps = spawnSync(
    "powershell.exe",
    [
      "-NoProfile",
      "-Command",
      `Expand-Archive -LiteralPath '${zipPath.replace(/'/g, "''")}' -DestinationPath '${DEST.replace(/'/g, "''")}' -Force`,
    ],
    { stdio: "inherit" },
  );
  if (ps.status !== 0) {
    throw new Error("Entpacken fehlgeschlagen (Expand-Archive).");
  }

  try {
    spawnSync("cmd.exe", ["/c", "del", "/f", "/q", zipPath], { stdio: "ignore" });
  } catch {
    /* ignore */
  }

  // Seed .env from example if missing (never ship secrets).
  const envPath = join(DEST, ".env");
  const example = join(DEST, ".env.example");
  if (!existsSync(envPath) && existsSync(example)) {
    copyFileSync(example, envPath);
    log(".env aus .env.example angelegt — API-Keys ggf. ergänzen.");
  }

  log("Projekt liegt in Downloads\\Connect.");
}

function startStack(): void {
  const wsl = which("wsl.exe") || which("wsl");
  if (wsl) {
    log("Starte Stack über WSL …");
    // Convert Windows path to /mnt/c/...
    const win = DEST.replace(/\\/g, "/");
    const m = /^([A-Za-z]):\/(.*)$/.exec(win);
    const linuxPath = m
      ? `/mnt/${m[1].toLowerCase()}/${m[2]}`
      : win;
    const code = run(wsl, [
      "-e",
      "bash",
      "-lc",
      `cd '${linuxPath}' && chmod +x START.sh START-APP.sh scripts/*.sh 2>/dev/null; OPENBOT_FORCE_START=1 ./START.sh`,
    ]);
    if (code !== 0) {
      log("START.sh meldete einen Fehler — versuche trotzdem das App-Fenster.");
    }
    return;
  }

  log("Kein WSL gefunden. Starte nur das App-Fenster (Stack muss schon laufen).");
  log("Tipp: Docker Desktop + WSL2 installieren, dann Connect.exe erneut starten.");
}

function openAppWindow(): void {
  const edge =
    which("msedge.exe") ||
    [
      join(process.env["ProgramFiles(x86)"] || "", "Microsoft\\Edge\\Application\\msedge.exe"),
      join(process.env.ProgramFiles || "", "Microsoft\\Edge\\Application\\msedge.exe"),
    ].find((p) => p && existsSync(p));

  const chrome =
    which("chrome.exe") ||
    [
      join(process.env.ProgramFiles || "", "Google\\Chrome\\Application\\chrome.exe"),
      join(process.env.LOCALAPPDATA || "", "Google\\Chrome\\Application\\chrome.exe"),
    ].find((p) => p && existsSync(p));

  const bin = edge || chrome;
  const profile =
    process.env.CONNECT_CHROME_PROFILE?.trim() ||
    join(homedir(), ".connect-chrome-profile", "desktop");
  mkdirSync(profile, { recursive: true });

  if (!bin) {
    log(`Kein Edge/Chrome — Connect IST der Browser. Bitte Chrome/Edge installieren.`);
    log(`Steuerung wäre: ${APP_URL}`);
    return;
  }
  // Real browser with Connect profile — never --app= (website-in-window).
  log(`Connect-Browser → ${bin} · Profil ${profile}`);
  log(`Steuerung-Tab: ${APP_URL}`);
  spawn(
    bin,
    [
      `--user-data-dir=${profile}`,
      "--no-first-run",
      "--no-default-browser-check",
      "--new-window",
      APP_URL,
    ],
    {
      detached: true,
      stdio: "ignore",
    },
  ).unref();
}

function stopStack(): void {
  log("Stoppe Connect …");
  const wsl = which("wsl.exe") || which("wsl");
  if (!wsl) {
    log("Kein WSL — stoppe nur bekannte Ports falls möglich.");
    return;
  }
  const win = DEST.replace(/\\/g, "/");
  const m = /^([A-Za-z]):\/(.*)$/.exec(win);
  const linuxPath = m
    ? `/mnt/${m[1].toLowerCase()}/${m[2]}`
    : win;
  run(wsl, [
    "-e",
    "bash",
    "-lc",
    `cd '${linuxPath}' && bash scripts/stop.sh || true`,
  ]);
  log("Connect ist deaktiviert.");
}

async function main() {
  console.log("");
  console.log("  ╔══════════════════════════╗");
  console.log("  ║        Connect           ║");
  console.log("  ╚══════════════════════════╝");
  console.log("");

  await extractProject();
  startStack();

  log(`Warte auf ${APP_URL} …`);
  if (!waitForUrl(APP_URL, 90)) {
    log("App antwortet noch nicht — öffne Fenster trotzdem.");
  }
  openAppWindow();

  console.log("");
  log("Connect läuft. Fenster schließen ist ok.");
  log("Zum DEAKTIVIEREN: Enter drücken oder dieses Fenster schließen (Ctrl+C).");
  console.log("");

  const shutdown = () => {
    try {
      stopStack();
    } finally {
      process.exit(0);
    }
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);

  // Wait for Enter
  await new Promise<void>((resolve) => {
    process.stdin.setEncoding("utf8");
    process.stdin.resume();
    process.stdin.once("data", () => resolve());
  });
  shutdown();
}

main().catch((err) => {
  console.error("[Connect] Fehler:", err instanceof Error ? err.message : err);
  console.log("Enter zum Beenden…");
  process.stdin.resume();
  process.stdin.once("data", () => process.exit(1));
});
