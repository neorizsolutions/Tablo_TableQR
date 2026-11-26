import { useEffect, useState, type ReactElement } from "react";
import { Link, useRouterState } from "@tanstack/react-router";
import { ChevronLeft, ChevronRight, Lock, LogOut, Menu, QrCode, ShieldCheck, Store } from "lucide-react";
import { useNavigationAccess } from "@/hooks/use-navigation-access";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { useSupportUnreadCount } from "@/hooks/use-support";
import {
  getDashboardTab,
  getEmployeeNavItems,
  getMenuRequiredPlanLabel,
  isNavigationItemActive,
  SUPER_ADMIN_NAVIGATION,
  type NavItem,
  type SuperAdminNavItem,
} from "@/config/navigation";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import { FeatureLockedModal } from "./feature-locked-modal";
import { SubscriptionDetailsModal } from "./subscription-details-modal";

const STORAGE_KEY = "tablo-sidebar-collapsed";
const workspaceSections = [
  ["operations", "Daily Operations"],
  ["inventory", "Inventory & Stock"],
  ["analytics", "Reports & Analytics"],
  ["management", "Restaurant Administration"],
] as const;

const platformSections = [
  ["platform", "Platform Administration"],
] as const;

type SidebarRenderableItem = (NavItem | SuperAdminNavItem) & {
  tabParam?: string;
  isDefault?: boolean;
  requiredCapabilities?: string[];
};

interface AppSidebarProps {
  selectedOutletId?: string;
  onSelectOutlet?: (outletId: string) => void;
}

function RailTooltip({ enabled, label, children }: { enabled: boolean; label: string; children: ReactElement }) {
  return (
    <Tooltip open={enabled ? undefined : false}>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side="right" sideOffset={12} className="bg-foreground text-background shadow-md">
        {label}
      </TooltipContent>
    </Tooltip>
  );
}

export function AppSidebar({ selectedOutletId, onSelectOutlet }: AppSidebarProps) {
  const { user, roles, signOut } = useAuth();
  const permissions = useNavigationAccess();
  const { access, hasCapability } = useEffectiveAccess(selectedOutletId);
  const location = useRouterState({ select: (state) => state.location });
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === "true";
    } catch {
      return false;
    }
  });
  const [mobileOpen, setMobileOpen] = useState(false);
  const [subscriptionOpen, setSubscriptionOpen] = useState(false);
  const [lockedFeature, setLockedFeature] = useState<{ label: string; plan: "Plus" | "Premium" }>();

  const role = roles[0] || user?.role || "Staff";
  const platform = role === "SuperAdmin";
  const brand = platform ? "Tablo Platform" : user?.restaurantName || "Tablo POS";
  const roleLabel = role === "Admin" ? "Restaurant Admin" : platform ? "Super Admin" : role;
  const profileName = user?.name || user?.email || roleLabel;
  const outlets = access?.outlets;
  const rawTab: unknown = "tab" in location.search ? location.search.tab : undefined;
  const tab = typeof rawTab === "string" ? rawTab : undefined;
  const activeTab = platform
    ? SUPER_ADMIN_NAVIGATION.find((item) => item.tab === tab)?.tab ?? "overview"
    : location.pathname === "/dashboard" ? getDashboardTab(tab, role, permissions.data?.allowedMenuIds) : tab;
  const items = platform
    ? SUPER_ADMIN_NAVIGATION.map((item) => ({
        ...item,
        tabParam: item.tab,
        isDefault: item.tab === "overview",
        requiredCapabilities: undefined,
      }))
    : getEmployeeNavItems(role, permissions.isError || !permissions.data ? [] : permissions.data.allowedMenuIds);
  const canUseRestaurantSupport = !platform && items.some((item) => item.id === "support");
  const supportUnreadCount = useSupportUnreadCount(
    platform ? "superadmin" : "restaurant",
    platform || canUseRestaurantSupport,
  );

  useEffect(() => {
    if (!mobileOpen) return;
    const desktop = window.matchMedia("(min-width: 768px)");
    const closeAtDesktop = () => {
      if (desktop.matches) setMobileOpen(false);
    };
    closeAtDesktop();
    desktop.addEventListener("change", closeAtDesktop);
    return () => desktop.removeEventListener("change", closeAtDesktop);
  }, [mobileOpen]);

  function toggleCollapsed() {
    const next = !collapsed;
    setCollapsed(next);
    try {
      localStorage.setItem(STORAGE_KEY, String(next));
    } catch {
      // The sidebar remains usable when browser storage is unavailable.
    }
  }

  function renderNavItem(item: SidebarRenderableItem, rail: boolean) {
    const active = isNavigationItemActive(item, location.pathname, activeTab);
    const locked = Boolean(item.requiredCapabilities?.length && !item.requiredCapabilities.some(hasCapability));
    const planLabel = getMenuRequiredPlanLabel(item);
    const plan: "Plus" | "Premium" = planLabel?.includes("Premium") ? "Premium" : "Plus";
    const Icon = item.icon;
    const unreadCount = (item.id === "support" || item.id === "sa-support-inbox") ? supportUnreadCount : 0;
    const className = cn(
      "relative flex h-10 items-center rounded-xl text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-ring",
      rail ? "mx-auto w-10 justify-center" : "w-full gap-2.5 overflow-hidden px-3 text-left",
      active ? "bg-emerald-700 font-semibold text-white" : "font-medium text-muted-foreground hover:bg-muted hover:text-foreground",
    );
    const content = (
      <>
        <Icon className="size-4 shrink-0" aria-hidden="true" />
        {!rail && <span className="min-w-0 flex-1 truncate">{item.label}</span>}
        {unreadCount > 0 && !locked ? (
          <span
            data-testid={`sidebar-unread-${item.id}`}
            className={cn(
              "inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[10px] font-bold leading-4",
              active ? "bg-white text-emerald-700" : "bg-primary text-primary-foreground",
              rail && "absolute right-0.5 top-0.5 h-4 min-w-4 px-1",
            )}
          >
            {unreadCount}
          </span>
        ) : null}
        {locked ? <Lock className={cn("size-3 shrink-0", rail && "absolute right-1 top-1")} aria-hidden="true" /> : null}
      </>
    );

    return (
      <RailTooltip key={item.id} enabled={rail} label={locked ? `${item.label} · ${plan}` : item.label}>
        {locked ? (
          <button
            type="button"
            aria-label={`${item.label}, requires ${plan}`}
            className={className}
            onClick={() => {
              setMobileOpen(false);
              setLockedFeature({ label: item.label, plan });
            }}
          >
            {content}
          </button>
        ) : (
          <Link
            to={item.to}
            search={item.search}
            aria-label={item.label}
            aria-current={active ? "page" : undefined}
            activeOptions={{ exact: true, includeSearch: Boolean(item.tabParam) }}
            activeProps={{}}
            className={className}
            onClick={() => setMobileOpen(false)}
          >
            {content}
          </Link>
        )}
      </RailTooltip>
    );
  }

  function renderSidebar(rail: boolean, mobile = false) {
    const actionClass = cn("h-9 shrink-0 rounded-xl", rail ? "w-10 px-0" : "w-full justify-start gap-2.5 px-3");
    const activeSections = platform ? platformSections : workspaceSections;
    const utilityLinks = platform ? [] : items.filter((item) => item.section === "utility");
    return (
      <>
        <div className={cn("flex h-16 shrink-0 items-center gap-2.5", rail ? "justify-center" : "px-3", mobile && "pr-10")}>
          {!rail && (
            <>
              <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-primary text-primary-foreground">
                <QrCode className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-bold" title={brand}>{brand}</div>
                <div className="truncate text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">{roleLabel}</div>
              </div>
            </>
          )}
          {!mobile && (
            <RailTooltip enabled={rail} label="Expand sidebar">
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className={cn("shrink-0 text-muted-foreground", rail ? "size-10 rounded-xl" : "size-8")}
                aria-label={rail ? "Expand sidebar" : "Collapse sidebar"}
                aria-expanded={!rail}
                onClick={toggleCollapsed}
              >
                {rail ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
              </Button>
            </RailTooltip>
          )}
        </div>

        {!platform && outlets && outlets.length > 1 && (
          <div className={cn("flex shrink-0 pb-2", rail ? "justify-center" : "px-3")}>
            <DropdownMenu>
              <RailTooltip enabled={rail} label="Select outlet">
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" className={actionClass} aria-label="Select outlet">
                    <Store className="size-4" />
                    {!rail && <span className="truncate">{outlets.find((outlet) => outlet.id === selectedOutletId)?.name || "All Outlets"}</span>}
                  </Button>
                </DropdownMenuTrigger>
              </RailTooltip>
              <DropdownMenuContent align="start" side={mobile ? "bottom" : "right"} sideOffset={8}>
                <DropdownMenuLabel>Select Outlet</DropdownMenuLabel>
                {outlets.map((outlet) => (
                  <DropdownMenuItem key={outlet.id} onSelect={() => onSelectOutlet?.(outlet.id)}>
                    {outlet.name}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        )}

        <nav aria-label={platform ? "Platform navigation" : "Workspace navigation"} className={cn("min-h-0 flex-1 space-y-4 overflow-y-auto overflow-x-hidden py-2", rail ? "px-0" : "px-3")}>
          {activeSections.map(([section, label]) => {
            const links = items.filter((item) => item.section === section);
            if (!links.length) return null;

            return (
              <div key={section} role="group" aria-label={label} className="space-y-1">
                {rail
                  ? <div aria-hidden="true" className="mx-auto mb-2 w-5 border-t border-border" />
                  : <div className="truncate px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">{label}</div>}
                {links.map((item) => renderNavItem(item, rail))}
              </div>
            );
          })}
        </nav>

        {!platform && (
          <div
            aria-label="Workspace utilities"
            className={cn(
              "shrink-0 space-y-1 border-t border-border py-2",
              rail ? "flex flex-col items-center px-0" : "px-3",
            )}
          >
            {utilityLinks.map((item) => renderNavItem(item, rail))}
            <RailTooltip enabled={rail} label="Subscription & Entitlements">
              <Button
                type="button"
                variant="ghost"
                className={cn(
                  actionClass,
                  "font-medium text-muted-foreground hover:bg-muted hover:text-foreground",
                  rail && "mx-auto flex w-10 justify-center",
                )}
                aria-label="Subscription & Entitlements"
                onClick={() => {
                  setMobileOpen(false);
                  setSubscriptionOpen(true);
                }}
              >
                <ShieldCheck className="size-4 shrink-0" />
                {!rail && "Subscription"}
              </Button>
            </RailTooltip>
          </div>
        )}

        <footer className={cn("flex shrink-0 flex-col gap-2.5 border-t border-border p-3", rail && "items-center px-0")}>
          <div className="flex min-w-0 items-center gap-2.5">
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-primary/10 text-sm font-bold text-primary" title={rail ? profileName : undefined} aria-label={rail ? profileName : undefined}>
              {profileName.charAt(0).toUpperCase()}
            </span>
            {!rail && (
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold" title={profileName}>{profileName}</div>
                <span className="inline-flex rounded-md bg-muted px-1.5 text-[10px] font-medium text-muted-foreground">{roleLabel}</span>
              </div>
            )}
          </div>
          <RailTooltip enabled={rail} label="Logout">
            <Button
              type="button"
              variant="outline"
              className={cn("h-9 shrink-0 rounded-xl", rail ? "w-10 justify-center px-0" : "w-full justify-start gap-2.5 px-3")}
              aria-label="Logout"
              onClick={() => void signOut(platform ? "/super-admin" : undefined)}
            >
              <LogOut className="h-4 w-4" />
              {!rail && "Logout"}
            </Button>
          </RailTooltip>
        </footer>
      </>
    );
  }

  return (
    <TooltipProvider delayDuration={200}>
      <aside className={cn(
        "hidden min-h-0 shrink-0 flex-col overflow-hidden border-r border-border bg-card transition-all duration-200 ease-in-out motion-reduce:transition-none md:flex",
        collapsed ? "w-[72px]" : "w-64",
      )}>
        {renderSidebar(collapsed)}
      </aside>
      <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
        <div className="flex shrink-0 items-center gap-2 border-b border-border bg-card px-3 py-1.5 md:hidden">
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Open navigation menu">
              <Menu className="size-5" />
            </Button>
          </SheetTrigger>
          <span className="truncate text-sm font-semibold">{brand}</span>
        </div>
        <SheetContent side="left" className="flex w-72 max-w-[90vw] flex-col gap-0 bg-card p-0 md:hidden" aria-describedby={undefined}>
          <SheetTitle className="sr-only">Navigation menu</SheetTitle>
          {renderSidebar(false, true)}
        </SheetContent>
      </Sheet>
      <FeatureLockedModal
        open={Boolean(lockedFeature)}
        onOpenChange={(open) => { if (!open) setLockedFeature(undefined); }}
        featureName={lockedFeature?.label || ""}
        requiredPlan={lockedFeature?.plan || "Plus"}
      />
      {!platform && (
        <SubscriptionDetailsModal
          open={subscriptionOpen}
          onOpenChange={setSubscriptionOpen}
          access={access}
          restaurantName={user?.restaurantName}
        />
      )}
    </TooltipProvider>
  );
}
