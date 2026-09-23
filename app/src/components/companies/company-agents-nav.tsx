import { IconSettings, IconUser } from "@tabler/icons-react";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AbstractAvatar } from "@/components/agents/abstract-avatar";
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuTrigger,
} from "@/components/ui/context-menu";
import {
  SidebarGroupLabel,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";
import {
  ensureAgentIdentities,
  formatSlackHandle,
  getAgentIdentity,
  subscribeAgentIdentities,
} from "@/lib/agents/agent-identity";
import {
  type AgentProfile,
  agentListQueryOptions,
} from "@/lib/agents/queries";
import { createChannelMutationOptions } from "@/lib/channels/mutations";
import {
  channelKeys,
  channelListQueryOptions,
} from "@/lib/channels/queries";
import { listProjects, subscribeProjects } from "@/lib/companies/projects";
import {
  applyIdOrder,
  DND_AGENT,
  getAgentSidebarOrder,
  moveIdBefore,
  setAgentSidebarOrder,
  subscribeSidebarOrder,
} from "@/lib/companies/sidebar-order";
import {
  getCompany,
  listCompanies,
  subscribeCompanies,
} from "@/lib/companies/store";
import {
  getActiveLevel,
  subscribeLevel,
  type CompanyLevel,
} from "@/lib/companies/level";
import { setSiteMarkAgent, getSiteMark, subscribeSiteMark } from "@/lib/companies/site-mark";
import { cn } from "@/lib/utils";

const ACTIVE_KEY = "connect.activeCompanyId";

function readActiveCompanyId(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(ACTIVE_KEY);
}

/**
 * Company Agents — only agents that belong to this company and are not
 * already nested in a Gruppe. Other / owned bots live under Marketplace → Meine Bots.
 */
export function CompanyAgentsNav({
  searching,
}: {
  searching: boolean;
}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const createChannel = useMutation(createChannelMutationOptions(queryClient));
  const agentsQuery = useQuery(agentListQueryOptions());
  const channels = useInfiniteQuery(channelListQueryOptions());
  const [companyId, setCompanyId] = useState<string | null>(() =>
    readActiveCompanyId(),
  );
  const [companies, setCompanies] = useState(() => listCompanies());
  const [projectTick, setProjectTick] = useState(0);
  const [identityTick, setIdentityTick] = useState(0);
  const [orderTick, setOrderTick] = useState(0);
  const [dragOverId, setDragOverId] = useState<string | null>(null);
  const [level, setLevel] = useState<CompanyLevel>(() => getActiveLevel());
  const [siteAgentId, setSiteAgentId] = useState<string | null>(() =>
    companyId ? getSiteMark(companyId).agentId : null,
  );

  useEffect(() => {
    const sync = () => {
      setCompanyId(readActiveCompanyId());
      setCompanies(listCompanies());
    };
    sync();
    window.addEventListener("connect-active-company", sync);
    const off = subscribeCompanies(() => {
      setCompanies(listCompanies());
      setCompanyId(readActiveCompanyId());
    });
    return () => {
      window.removeEventListener("connect-active-company", sync);
      off();
    };
  }, []);

  useEffect(() => subscribeLevel(() => setLevel(getActiveLevel())), []);

  useEffect(() => {
    if (!companyId) {
      setSiteAgentId(null);
      return;
    }
    setSiteAgentId(getSiteMark(companyId).agentId);
    return subscribeSiteMark(() =>
      setSiteAgentId(getSiteMark(companyId).agentId),
    );
  }, [companyId]);

  useEffect(() => {
    return subscribeProjects(() => setProjectTick((n) => n + 1));
  }, []);

  useEffect(() => {
    return subscribeAgentIdentities(() => setIdentityTick((n) => n + 1));
  }, []);

  useEffect(() => {
    return subscribeSidebarOrder(() => setOrderTick((n) => n + 1));
  }, []);

  const company = companyId
    ? (getCompany(companyId) ?? companies.find((c) => c.id === companyId))
    : companies[0];

  const roster = useMemo(() => {
    void projectTick;
    void identityTick;
    void orderTick;
    const all = agentsQuery.data ?? [];
    if (all.length > 0) ensureAgentIdentities(all);

    if (!company) {
      return {
        ungrouped: [] as AgentProfile[],
        companyAgentCount: 0,
        groupedCount: 0,
      };
    }

    const companyIds = new Set(company.agentIds);
    const inProjects = new Set(
      listProjects(company.id).flatMap((p) => p.agentIds),
    );

    // Only this company's agents — never marketplace / other-company bots.
    const companyAgents = all.filter((a) => companyIds.has(a.id));
    const ungrouped = companyAgents.filter((a) => !inProjects.has(a.id));
    const preferred = getAgentSidebarOrder(company.id);
    const orderedIds = applyIdOrder(
      ungrouped.map((a) => a.id),
      preferred,
    );
    const byId = new Map(ungrouped.map((a) => [a.id, a]));
    const ordered = orderedIds
      .map((id) => byId.get(id))
      .filter((a): a is AgentProfile => Boolean(a));

    return {
      ungrouped: ordered,
      companyAgentCount: companyAgents.length,
      groupedCount: companyAgents.length - ungrouped.length,
    };
  }, [agentsQuery.data, company, projectTick, identityTick, orderTick]);

  if (searching) return null;

  if (agentsQuery.isPending && roster.companyAgentCount === 0) {
    return (
      <div className="mb-2 group-data-[collapsible=icon]:hidden">
        <SidebarGroupLabel className="mb-0.5 h-7 px-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-sidebar-foreground/45">
          Agents
        </SidebarGroupLabel>
        <p className="px-2 py-1 text-xs text-sidebar-foreground/35">
          Loading coworkers…
        </p>
      </div>
    );
  }

  if (roster.ungrouped.length === 0) {
    return (
      <div className="mb-2 group-data-[collapsible=icon]:hidden">
        <SidebarGroupLabel className="mb-0.5 h-7 px-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-sidebar-foreground/45">
          Agents
        </SidebarGroupLabel>
        <p className="px-2 py-1 text-xs text-sidebar-foreground/35">
          {agentsQuery.isError
            ? "Coworkers couldn’t be loaded."
            : roster.companyAgentCount > 0
              ? "Alle Company-Agents sind Gruppen zugeordnet."
              : "Noch keine Agents in dieser Company. Andere Bots: Marketplace → Meine Bots."}
        </p>
      </div>
    );
  }

  const openAgent = async (agent: AgentProfile) => {
    // Unternehmen: select agent for site mark → task (stay on page).
    if (level === 4 && company) {
      setSiteMarkAgent(company.id, agent.id);
      return;
    }
    const existing = channels.data?.find((ch) =>
      ch.agentIds.includes(agent.id),
    );
    if (existing) {
      await navigate({
        to: "/channel/$channelId",
        params: { channelId: existing.id },
      });
      return;
    }
    const channel = await createChannel.mutateAsync([agent.id]);
    queryClient.setQueryData(channelKeys.detail(channel.id), channel);
    await navigate({
      to: "/channel/$channelId",
      params: { channelId: channel.id },
    });
  };

  const persistOrder = (draggedId: string, targetId: string) => {
    if (!company) return;
    const ids = roster.ungrouped.map((a) => a.id);
    setAgentSidebarOrder(
      company.id,
      moveIdBefore(ids, draggedId, targetId),
    );
  };

  const renderRow = (agent: AgentProfile) => {
    const identity = getAgentIdentity(agent.id);
    const handle = formatSlackHandle(identity.slackHandle);
    const over = dragOverId === agent.id;
    const siteSelected = level === 4 && siteAgentId === agent.id;
    return (
      <SidebarMenuItem
        className="group-data-[collapsible=icon]:flex group-data-[collapsible=icon]:w-auto group-data-[collapsible=icon]:justify-center"
        key={agent.id}
      >
        <ContextMenu>
          <ContextMenuTrigger className="block w-full group-data-[collapsible=icon]:flex group-data-[collapsible=icon]:w-auto group-data-[collapsible=icon]:justify-center">
            <SidebarMenuButton
              className={cn(
                "h-auto cursor-grab rounded-xl px-2 py-2 hover:bg-sidebar-accent active:cursor-grabbing",
                "group-data-[collapsible=icon]:size-9! group-data-[collapsible=icon]:shrink-0 group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:gap-0 group-data-[collapsible=icon]:overflow-hidden group-data-[collapsible=icon]:rounded-xl group-data-[collapsible=icon]:p-0!",
                over && "bg-sidebar-accent ring-1 ring-sky-400/50",
                siteSelected &&
                  "bg-sky-50 ring-1 ring-sky-400/60 dark:bg-sky-950/40",
              )}
              draggable
              isActive={siteSelected}
              onClick={() => void openAgent(agent)}
              onDragStart={(event) => {
                event.dataTransfer.setData(DND_AGENT, agent.id);
                event.dataTransfer.effectAllowed = "move";
              }}
              onDragOver={(event) => {
                if (![...event.dataTransfer.types].includes(DND_AGENT)) {
                  return;
                }
                event.preventDefault();
                event.dataTransfer.dropEffect = "move";
                setDragOverId(agent.id);
              }}
              onDragLeave={() =>
                setDragOverId((id) => (id === agent.id ? null : id))
              }
              onDrop={(event) => {
                event.preventDefault();
                setDragOverId(null);
                const dragged = event.dataTransfer.getData(DND_AGENT);
                if (dragged) persistOrder(dragged, agent.id);
              }}
              title={
                level === 4
                  ? `${agent.name} — für Firmenseiten-Auftrag wählen`
                  : agent.name
              }
              tooltip={
                agent.title?.trim()
                  ? `${agent.name} · ${agent.title.trim()}`
                  : agent.name
              }
            >
              <AbstractAvatar
                agentId={agent.id}
                name={agent.name}
                seed={agent.avatarSeed}
                size={28}
              />
              <span className="min-w-0 flex-1 group-data-[collapsible=icon]:hidden">
                <span className="block truncate text-sm tracking-tight">
                  {agent.name}
                </span>
                {handle ? (
                  <span className="block truncate text-[11px] text-sidebar-foreground/45">
                    {handle}
                  </span>
                ) : null}
              </span>
            </SidebarMenuButton>
          </ContextMenuTrigger>
          <ContextMenuContent className="min-w-52 rounded-xl p-1.5">
            <ContextMenuItem
              className="gap-2 rounded-lg px-2.5 py-2"
              onClick={() => void openAgent(agent)}
            >
              <IconUser className="size-4" />
              Open chat
            </ContextMenuItem>
            <ContextMenuItem
              className="gap-2 rounded-lg px-2.5 py-2"
              onClick={() =>
                void navigate({
                  to: "/agents",
                  search: { agent: agent.id, tab: "mine" },
                })
              }
            >
              <IconSettings className="size-4" />
              Agent settings
            </ContextMenuItem>
          </ContextMenuContent>
        </ContextMenu>
      </SidebarMenuItem>
    );
  };

  return (
    <div className="mb-2 w-full group-data-[collapsible=icon]:flex group-data-[collapsible=icon]:flex-col group-data-[collapsible=icon]:items-center">
      <SidebarGroupLabel className="mb-0.5 h-7 px-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-sidebar-foreground/45 group-data-[collapsible=icon]:sr-only">
        Agents
      </SidebarGroupLabel>
      <ul className="flex w-full min-w-0 flex-col gap-0.5 group-data-[collapsible=icon]:w-auto group-data-[collapsible=icon]:items-center group-data-[collapsible=icon]:gap-1">
        {roster.ungrouped.map(renderRow)}
      </ul>
    </div>
  );
}
