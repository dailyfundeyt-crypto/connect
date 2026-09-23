import {
  IconBrandChrome,
  IconBrandGithub,
  IconChevronLeft,
  IconChevronRight,
  IconLoader2,
  IconPencil,
  IconStarFilled,
} from "@tabler/icons-react";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { useIframeBrowserNav } from "@/lib/companies/iframe-browser-nav";
import {
  getConnection,
  getLevel3Browser,
  resolveLabApp,
  resolveLabEngine,
  resolveTabUrl,
  setLabEngine,
  subscribeLevel3Browser,
  unstarToBrowser,
} from "@/lib/companies/level3-tools";
import {
  CHROME_START_PATH_HINT,
  openLabUrlInChrome,
} from "@/lib/ui/lab-prefs";

/**
 * Lab Browser — Connect IS the browser (Host-Chrome + Connect-Profil).
 * The pane is control/status, not an iframe pretending to be Chromium.
 * Optional `embed` keeps a preview only for legacy / mark tools.
 */
export function Level3SiteStudio({
  companyId,
  companyName,
}: {
  companyId: string;
  companyName: string;
  computerId?: string;
}) {
  const [state, setState] = useState(() => getLevel3Browser(companyId));
  const [frameKey, setFrameKey] = useState(0);

  useEffect(() => {
    setState(getLevel3Browser(companyId));
    return subscribeLevel3Browser(() =>
      setState(getLevel3Browser(companyId)),
    );
  }, [companyId]);

  const url = resolveTabUrl(state);
  const engine = resolveLabEngine(state.engine);

  useEffect(() => {
    setFrameKey((n) => n + 1);
  }, [url, companyId]);

  if (state.starred) {
    const starredConn = state.starredConnectionToolId
      ? getConnection(state, state.starredConnectionToolId)
      : undefined;
    const productUrl =
      starredConn?.projectUrl?.trim() ||
      resolveLabApp(state, state.starredConnectionToolId)?.url ||
      url;

    return (
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden bg-white text-neutral-900">
        <header className="flex h-11 shrink-0 items-center gap-2 border-b border-neutral-200 px-3">
          <IconStarFilled className="size-4 text-amber-500" />
          <p className="min-w-0 flex-1 truncate text-sm font-medium">
            {companyName}
            <span className="ml-2 text-[11px] font-normal text-neutral-400">
              Software läuft
            </span>
          </p>
          {starredConn?.githubUrl ? (
            <a
              className="hidden items-center gap-1 truncate text-[11px] text-neutral-500 hover:text-neutral-800 sm:inline-flex"
              href={starredConn.githubUrl}
              rel="noreferrer"
              target="_blank"
            >
              <IconBrandGithub className="size-3.5" />
              Repo
            </a>
          ) : null}
          <Button
            className="h-8 gap-1"
            onClick={() => setState(unstarToBrowser(companyId))}
            size="sm"
            type="button"
            variant="outline"
          >
            <IconPencil className="size-3.5" />
            Bearbeiten
          </Button>
        </header>
        {engine === "full" ? (
          <ConnectChromePane
            companyId={companyId}
            companyName={companyName}
            url={productUrl}
          />
        ) : (
          <EmbedPane
            companyId={companyId}
            companyName={companyName}
            frameKey={frameKey}
            url={productUrl}
          />
        )}
      </div>
    );
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden bg-white text-neutral-900">
      {engine === "full" ? (
        <ConnectChromePane
          companyId={companyId}
          companyName={companyName}
          url={url}
        />
      ) : (
        <EmbedPane
          companyId={companyId}
          companyName={companyName}
          frameKey={frameKey}
          url={url}
        />
      )}
    </div>
  );
}

function ConnectChromePane({
  companyId,
  companyName,
  url,
}: {
  companyId: string;
  companyName: string;
  url: string;
}) {
  const [busy, setBusy] = useState(false);
  const [ok, setOk] = useState<boolean | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const lastOpened = useRef("");

  const launch = async (target: string, force = false) => {
    const trimmed = target.trim();
    if (!trimmed) {
      setOk(null);
      setMsg(null);
      return;
    }
    if (!force && lastOpened.current === trimmed && ok === true) return;
    setBusy(true);
    setMsg(null);
    try {
      const result = await openLabUrlInChrome(trimmed);
      if (result.ok) {
        lastOpened.current = trimmed;
        setOk(true);
        setMsg(
          "Connect-Chrome ist der Browser — Fenster mit Connect-Profil auf dem Desktop.",
        );
        return;
      }
      setOk(false);
      setMsg(
        result.error?.trim() ||
          `Chrome-Start fehlgeschlagen. ${CHROME_START_PATH_HINT}`,
      );
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    if (!url) {
      lastOpened.current = "";
      setOk(null);
      setMsg(null);
      return;
    }
    void launch(url);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- relaunch when Lab URL changes
  }, [url, companyId]);

  if (!url) {
    return (
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 bg-white px-6 text-center">
        <IconBrandChrome className="size-8 text-neutral-400" stroke={1.25} />
        <p className="text-sm font-medium text-neutral-700">
          Connect-Browser · {companyName}
        </p>
        <p className="max-w-sm text-[12px] text-neutral-400">
          Links eine App öffnen oder Websearch nutzen. Connect startet Host-Chrome
          mit Connect-Profil — kein Website-Tab, kein iframe.
        </p>
      </div>
    );
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-4 bg-[#f5f5f7] px-6 text-center">
      <figure className="w-full max-w-lg overflow-hidden rounded-2xl border border-black/10 bg-white shadow-sm">
        <div className="flex items-center gap-2 border-b border-black/10 bg-[#ececef] px-3 py-2">
          <span className="flex gap-1">
            <span className="size-2.5 rounded-full bg-[#ff5f57]" />
            <span className="size-2.5 rounded-full bg-[#febc2e]" />
            <span className="size-2.5 rounded-full bg-[#28c840]" />
          </span>
          <div className="min-w-0 flex-1 truncate rounded-md bg-white px-2.5 py-1 text-left text-[11px] text-neutral-600 shadow-sm">
            {url}
          </div>
          <IconBrandChrome className="size-3.5 shrink-0 text-neutral-500" />
        </div>
        <div className="flex flex-col items-center gap-3 px-6 py-10">
          {busy ? (
            <IconLoader2 className="size-8 animate-spin text-neutral-400" />
          ) : (
            <IconBrandChrome
              className={
                ok === false ? "size-8 text-red-500" : "size-8 text-sky-600"
              }
              stroke={1.25}
            />
          )}
          <div className="space-y-1">
            <p className="text-sm font-medium text-neutral-900">
              {busy
                ? "Connect-Chrome startet…"
                : ok === false
                  ? "Connect-Chrome nicht gestartet"
                  : "Connect ist der Browser"}
            </p>
            <p
              className={
                ok === false
                  ? "max-w-md text-[12px] leading-relaxed text-red-600"
                  : "max-w-md text-[12px] leading-relaxed text-neutral-500"
              }
              role={ok === false ? "alert" : "status"}
            >
              {msg ||
                "Seiten und Extensions laufen im Host-Chrome mit Connect-Profil — nicht in diesem Web-Tab."}
            </p>
          </div>
          <div className="flex flex-wrap items-center justify-center gap-2">
            <Button
              className="h-9 gap-1.5"
              disabled={busy}
              onClick={() => void launch(url, true)}
              type="button"
            >
              <IconBrandChrome className="size-3.5" />
              {busy ? "Startet…" : "Connect-Chrome öffnen"}
            </Button>
            <Button
              className="h-9"
              onClick={() => setLabEngine(companyId, "embed")}
              type="button"
              variant="ghost"
            >
              Nur Vorschau (iframe)
            </Button>
          </div>
        </div>
      </figure>
      <p className="max-w-md text-[11px] text-neutral-400">
        Steuerung bleibt hier · Surfen = Connect-Chrome auf dem PC (
        <code className="rounded bg-neutral-200/80 px-1">./START-APP.sh</code>
        ).
      </p>
    </div>
  );
}

function EmbedPane({
  companyId,
  url,
  companyName,
  frameKey,
}: {
  companyId: string;
  url: string;
  companyName: string;
  frameKey: number;
}) {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const { goBack, goForward } = useIframeBrowserNav(iframeRef, Boolean(url));

  if (!url) {
    return (
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 bg-white px-6 text-center">
        <p className="text-sm font-medium text-neutral-700">App wählen</p>
        <p className="max-w-sm text-[12px] text-neutral-400">
          Iframe-Vorschau ohne echtes Chromium. Für Extensions und Logins:
          Connect-Chrome.
        </p>
      </div>
    );
  }

  return (
    <div className="relative min-h-0 w-full flex-1 bg-white">
      <div className="absolute inset-x-0 top-0 z-20 flex items-center justify-between gap-2 border-b border-amber-200/80 bg-amber-50 px-3 py-1.5 text-[11px] text-amber-900">
        <span>
          Iframe-Vorschau — kein echter Browser. Connect-Chrome für Extensions /
          Logins.
        </span>
        <Button
          className="h-7 gap-1 text-[11px]"
          onClick={() => setLabEngine(companyId, "full")}
          size="sm"
          type="button"
          variant="outline"
        >
          <IconBrandChrome className="size-3" />
          Connect-Chrome
        </Button>
      </div>
      <iframe
        allow="clipboard-read; clipboard-write; fullscreen; accelerometer; autoplay"
        className="absolute inset-0 size-full border-0 bg-white pt-9"
        key={`${frameKey}:${url}`}
        ref={iframeRef}
        referrerPolicy="no-referrer-when-downgrade"
        sandbox="allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox allow-downloads allow-modals"
        src={url}
        title={`${companyName} · Vorschau`}
      />
      <div className="pointer-events-none absolute left-3 top-12 z-10 flex gap-1">
        <Button
          aria-label="Zurück"
          className="pointer-events-auto size-8 rounded-full border border-neutral-200 bg-white/90 shadow-sm backdrop-blur"
          onClick={goBack}
          size="icon"
          title="Zurück · Alt← / ⌘["
          type="button"
          variant="ghost"
        >
          <IconChevronLeft className="size-3.5" />
        </Button>
        <Button
          aria-label="Vorwärts"
          className="pointer-events-auto size-8 rounded-full border border-neutral-200 bg-white/90 shadow-sm backdrop-blur"
          onClick={goForward}
          size="icon"
          title="Vorwärts · Alt→ / ⌘]"
          type="button"
          variant="ghost"
        >
          <IconChevronRight className="size-3.5" />
        </Button>
      </div>
    </div>
  );
}
