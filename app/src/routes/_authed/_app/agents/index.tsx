import {
  IconBuilding,
  IconBuildingStore,
  IconExternalLink,
  IconPlus,
  IconRobot,
} from "@tabler/icons-react";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { z } from "zod";
import { AgentCard } from "@/components/agents/agent-card";
import { AgentDialog } from "@/components/agents/agent-dialog";
import { CreateAgentDialog } from "@/components/agents/create-agent-dialog";
import { AbstractAvatar } from "@/components/agents/abstract-avatar";
import {
  MarketplaceSearch,
  RedeemCodeBar,
} from "@/components/marketplace/redeem-bar";
import {
  SellBotPanel,
  SellCompanyPanel,
} from "@/components/marketplace/sell-panels";
import {
  LivePriceChip,
  LivePriceDisclaimer,
} from "@/components/marketplace/live-price-chip";
import { SidebarToggleBar } from "@/components/layout/sidebar-toggle";
import { StaggerItem } from "@/components/layout/stagger";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { Skeleton } from "@/components/ui/skeleton";
import {
  agentListQueryOptions,
  type AgentProfile,
} from "@/lib/agents/queries";
import { channelListQueryOptions } from "@/lib/channels/queries";
import {
  ensureMarketplaceSeed,
  listListings,
  listOwned,
  searchListings,
  subscribeMarketplace,
  type MarketplaceListing,
} from "@/lib/marketplace/store";
import { cn } from "@/lib/utils";

const agentsSearchSchema = z.object({
  new: z.boolean().optional(),
  agent: z.string().optional(),
  tab: z.enum(["mine", "search", "companies"]).optional(),
});

export const Route = createFileRoute("/_authed/_app/agents/")({
  validateSearch: agentsSearchSchema,
  component: MarketplaceScreen,
});

type Tab = "mine" | "search" | "companies";

/**
 * Bot marketplace: Meine Bots · Bots suchen · Unternehmen.
 * Affiliate discovery only — checkout / swipe on seller pages; live Mirks
 * prices are read-only; redeem unlocks after an off-app purchase.
 */
function MarketplaceScreen() {
  const { new: isCreating, agent: selectedAgentId, tab: tabParam } =
    Route.useSearch();
  const navigate = Route.useNavigate();
  const [tab, setTab] = useState<Tab>(tabParam ?? "mine");
  const [query, setQuery] = useState("");
  const [tick, setTick] = useState(0);

  const {
    data: agents,
    isPending: loadingAgents,
    isError: failed,
  } = useQuery(agentListQueryOptions());
  const channels = useInfiniteQuery(channelListQueryOptions());

  useEffect(() => {
    ensureMarketplaceSeed();
    return subscribeMarketplace(() => setTick((n) => n + 1));
  }, []);

  useEffect(() => {
    if (tabParam) setTab(tabParam);
  }, [tabParam]);

  const mine = useMemo(
    () => (agents ?? []).filter((a) => a.mine),
    [agents],
  );

  const owned = useMemo(() => listOwned(), [tick]);
  const listings = useMemo(
    () => (query.trim() ? searchListings(query) : listListings()),
    [query, tick],
  );

  const { inUse, unused } = useMemo(() => {
    const inChannelIds = new Set<string>();
    for (const ch of channels.data ?? []) {
      for (const id of ch.agentIds) inChannelIds.add(id);
    }
    const used: AgentProfile[] = [];
    const idle: AgentProfile[] = [];
    for (const agent of mine) {
      if (inChannelIds.has(agent.id)) used.push(agent);
      else idle.push(agent);
    }
    const byName = (a: AgentProfile, b: AgentProfile) =>
      a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
    used.sort(byName);
    idle.sort(byName);
    return { inUse: used, unused: idle };
  }, [mine, channels.data]);

  const loading = loadingAgents || (channels.isPending && !channels.data);
  const showCreate = isCreating === true;
  const showProfile = !showCreate && selectedAgentId !== undefined;
  const close = () => navigate({ search: { tab } });

  const setTabNav = (next: Tab) => {
    setTab(next);
    void navigate({ search: { tab: next } });
  };

  return (
    <>
      <SidebarToggleBar />
      <div className="mx-auto w-full max-w-2xl px-4 pb-16">
        <div className="mt-12 w-full max-w-2xl">
          <div className="flex w-full flex-row items-center justify-between gap-2">
            <h2 className="text-lg font-bold">Marketplace</h2>
            <Button
              render={(props) => (
                <Link
                  search={{ new: true, tab: "mine" }}
                  to="/agents"
                  {...props}
                />
              )}
              size="sm"
              variant="ghost"
            >
              <IconPlus />
              New agent
            </Button>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Nummer eins zum Finden von Bots und Unternehmen. Hier nur lesen und
            weiterleiten — kaufen / swipen auf der Seite des Anbieters
            (Affiliate). Mirks-Kurse live mitlesen, Handel auf Solana /
            Pump.fun.
          </p>

          <div className="mt-4">
            <RedeemCodeBar onRedeemed={() => setTick((n) => n + 1)} />
          </div>

          <div className="mt-5 flex gap-1 rounded-xl border border-border bg-muted/40 p-1">
            {(
              [
                ["mine", "Meine Bots", IconRobot],
                ["search", "Bots suchen", IconBuildingStore],
                ["companies", "Unternehmen", IconBuilding],
              ] as const
            ).map(([id, label, Icon]) => (
              <button
                className={cn(
                  "flex flex-1 items-center justify-center gap-1.5 rounded-lg px-2 py-2 text-xs font-semibold transition",
                  tab === id
                    ? "bg-background text-foreground shadow-sm"
                    : "text-muted-foreground hover:text-foreground",
                )}
                key={id}
                onClick={() => setTabNav(id)}
                type="button"
              >
                <Icon className="size-3.5" />
                {label}
              </button>
            ))}
          </div>

          {tab === "mine" ? (
            <div className="mt-6 space-y-8">
              {owned.length > 0 ? (
                <section>
                  <h3 className="text-xs font-semibold uppercase tracking-[0.06em] text-muted-foreground">
                    Eingelöst / Besitz
                    <span className="ml-2 font-normal tabular-nums text-muted-foreground/70">
                      {owned.length}
                    </span>
                  </h3>
                  <ul className="mt-3 space-y-2">
                    {owned.map((asset) => (
                      <li
                        className="flex items-center justify-between rounded-xl border border-border px-3 py-2.5"
                        key={asset.id}
                      >
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">
                            {asset.title}
                          </p>
                          <p className="font-mono text-[11px] text-muted-foreground">
                            {asset.serial} · {asset.kind}
                          </p>
                        </div>
                        {asset.kind === "bot" ? (
                          <Button
                            onClick={() =>
                              void navigate({
                                search: { agent: asset.targetId, tab: "mine" },
                              })
                            }
                            size="sm"
                            type="button"
                            variant="outline"
                          >
                            Öffnen
                          </Button>
                        ) : (
                          <Button
                            render={(props) => (
                              <Link
                                params={{ companyId: asset.targetId }}
                                search={{ level: 2, profile: true }}
                                to="/company/$companyId"
                                {...props}
                              />
                            )}
                            size="sm"
                            variant="outline"
                          >
                            Company
                          </Button>
                        )}
                      </li>
                    ))}
                  </ul>
                </section>
              ) : null}

              {loading ? (
                <Skeleton className="h-[180px]" />
              ) : mine.length > 0 ? (
                <>
                  <AgentSection agents={inUse} title="In Benutzung" />
                  <AgentSection agents={unused} title="Unbenutzt" />
                </>
              ) : failed && agents === undefined ? (
                <Empty className="h-[180px] border border-dashed border-destructive">
                  <EmptyHeader>
                    <EmptyTitle className="text-destructive">
                      Agents konnten nicht geladen werden.
                    </EmptyTitle>
                  </EmptyHeader>
                </Empty>
              ) : (
                <Empty className="h-[180px] border border-dashed">
                  <EmptyHeader>
                    <EmptyTitle className="text-muted-foreground">
                      Noch keine eigenen Bots — Code einlösen oder neuen
                      erstellen.
                    </EmptyTitle>
                  </EmptyHeader>
                </Empty>
              )}
            </div>
          ) : null}

          {tab === "search" ? (
            <div className="mt-5 space-y-4">
              <MarketplaceSearch onChange={setQuery} value={query} />
              <SellBotPanel
                agents={agents ?? []}
                onPublished={() => setTick((n) => n + 1)}
              />
              <ListingGrid
                empty="Keine Listings. Anbieter tragen Bots ein — Kunden finden sie hier und kaufen per Swipe auf der Anbieter-Seite."
                listings={listings.filter((l) => l.kind === "bot")}
              />
            </div>
          ) : null}

          {tab === "companies" ? (
            <div className="mt-5 space-y-4">
              <MarketplaceSearch onChange={setQuery} value={query} />
              <SellCompanyPanel onPublished={() => setTick((n) => n + 1)} />
              <ListingGrid
                empty="Keine Unternehmen im Katalog. Listen = auffindbar machen; Kauf bleibt beim Anbieter."
                listings={listings.filter((l) => l.kind === "company")}
              />
            </div>
          ) : null}
        </div>
      </div>
      <CreateAgentDialog
        onClose={close}
        onCreated={(agentId) =>
          navigate({ search: { agent: agentId, tab: "mine" } })
        }
        open={showCreate}
      />
      <AgentDialog
        agentId={selectedAgentId ?? null}
        onClose={close}
        open={showProfile}
      />
    </>
  );
}

function AgentSection({
  agents,
  title,
}: {
  agents: AgentProfile[];
  title: string;
}) {
  return (
    <section>
      <h3 className="text-xs font-semibold uppercase tracking-[0.06em] text-muted-foreground">
        {title}
        <span className="ml-2 font-normal tabular-nums text-muted-foreground/70">
          {agents.length}
        </span>
      </h3>
      {agents.length === 0 ? (
        <p className="mt-3 text-sm text-muted-foreground">Keine Agents.</p>
      ) : (
        <div className="mt-3 grid grid-cols-[repeat(auto-fill,144px)] gap-4">
          {agents.map((agent, index) => (
            <StaggerItem index={index} key={agent.id}>
              <Link search={{ agent: agent.id, tab: "mine" }} to="/agents">
                <AgentCard agent={agent} />
              </Link>
            </StaggerItem>
          ))}
        </div>
      )}
    </section>
  );
}

function ListingGrid({
  listings,
  empty,
}: {
  listings: MarketplaceListing[];
  empty: string;
}) {
  if (listings.length === 0) {
    return (
      <p className="rounded-xl border border-dashed px-4 py-8 text-center text-sm text-muted-foreground">
        {empty}
      </p>
    );
  }
  return (
    <ul className="space-y-3">
      {listings.map((listing) => (
        <li
          className="rounded-2xl border border-border bg-card p-4"
          key={listing.id}
        >
          <div className="flex gap-3">
            {listing.kind === "bot" ? (
              <AbstractAvatar
                agentId={listing.targetId}
                name={listing.title}
                seed={listing.targetId}
                size={48}
              />
            ) : (
              <div className="flex size-12 items-center justify-center rounded-xl bg-muted text-xs font-bold">
                CO
              </div>
            )}
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-baseline gap-2">
                <p className="font-semibold">{listing.title}</p>
                <span className="rounded-full bg-foreground/5 px-2 py-0.5 text-[10px] font-semibold uppercase">
                  {listing.offer === "rent" ? "Miete" : "Kauf"}
                </span>
              </div>
              <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
                {listing.serial} · {listing.sellerName}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                {listing.description}
              </p>
              {listing.memecoinTicker ? (
                <div className="mt-2 space-y-1">
                  <LivePriceChip
                    marketUrl={listing.memecoinUrl}
                    ticker={listing.memecoinTicker}
                  />
                  <LivePriceDisclaimer />
                </div>
              ) : null}
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <span className="text-sm font-bold">{listing.priceLabel}</span>
                <a
                  className="inline-flex items-center gap-1 rounded-full bg-foreground px-3 py-1.5 text-xs font-semibold text-background"
                  href={listing.sellerUrl}
                  rel="noreferrer"
                  target="_blank"
                >
                  Zur Anbieter-Seite
                  <IconExternalLink className="size-3.5" />
                </a>
              </div>
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}
