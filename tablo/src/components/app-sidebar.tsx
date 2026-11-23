import { useState, useMemo, useEffect } from "react";
import { Link, useRouterState } from "@tanstack/react-router";
import {
  ChevronDown,
  ChevronRight,
  Lock,
  ShieldCheck,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import {
  NAVIGATION_REGISTRY,
  type NavItem,
} from "@/config/navigation";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { FeatureLockedModal } from "./feature-locked-modal";

interface AppSidebarProps {
  mobileOpen?: boolean;
  onMobileOpenChange?: (open: boolean) => void;
  selectedOutletId?: string;
}

interface SectionGroup {
  id: string;
  label: string;
  items: NavItem[];
}

export function AppSidebar({
  mobileOpen = false,
  onMobileOpenChange,
  selectedOutletId,
}: AppSidebarProps) {
  const { user, roles } = useAuth();
  const { plan, hasCapability } = useEffectiveAccess(selectedOutletId);
  const routerState = useRouterState();
  const currentPath = routerState.location.pathname;
  const currentSearch = (routerState.location.search || {}) as Record<string, any>;

  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({
    operations: false,
    management: false,
    inventory: false,
    analytics: false,
  });

  const [lockedModal, setLockedModal] = useState<{
    open: boolean;
    featureName: string;
    requiredPlan: "Plus" | "Premium";
  }>({
    open: false,
    featureName: "",
    requiredPlan: "Premium",
  });

  const userRole = roles[0] || user?.role || "Staff";
  const isAdminOrOwner = userRole === "Admin" || userRole === "Owner";

  // Filter items applicable to user's role
  const sections: SectionGroup[] = useMemo(() => {
    const list = NAVIGATION_REGISTRY.filter((item) => {
      // Role filtering
      if (item.requiredRoles && !item.requiredRoles.some((r) => r.toLowerCase() === userRole.toLowerCase())) {
        return false;
      }
      return true;
    });

    const groups: Record<string, NavItem[]> = {
      main: [],
      operations: [],
      management: [],
      inventory: [],
      analytics: [],
    };

    for (const item of list) {
      if (groups[item.section]) {
        groups[item.section].push(item);
      }
    }

    const result: SectionGroup[] = [];

    if (groups.main.length > 0) {
      result.push({ id: "main", label: "Dashboard", items: groups.main });
    }
    if (groups.operations.length > 0) {
      result.push({ id: "operations", label: "Floor & Operations", items: groups.operations });
    }
    if (groups.management.length > 0 && isAdminOrOwner) {
      result.push({ id: "management", label: "Restaurant Management", items: groups.management });
    }
    if (groups.inventory.length > 0 && isAdminOrOwner) {
      result.push({ id: "inventory", label: "Inventory & Supply", items: groups.inventory });
    }
    if (groups.analytics.length > 0 && isAdminOrOwner) {
      result.push({ id: "analytics", label: "Analytics & Reports", items: groups.analytics });
    }

    return result;
  }, [userRole, isAdminOrOwner]);

  // Automatically expand group containing active item
  useEffect(() => {
    for (const sec of sections) {
      if (sec.id === "main") continue;
      const hasActive = sec.items.some((item) => {
        const isPathMatch = currentPath === item.to;
        const currentTab = currentSearch?.tab;
        return (
          isPathMatch &&
          (item.tabParam
            ? currentTab === item.tabParam || (!currentTab && !!item.isDefault)
            : !currentTab || !!item.isDefault || !item.tabParam)
        );
      });
      if (hasActive) {
        setCollapsedSections((prev) => (prev[sec.id] ? { ...prev, [sec.id]: false } : prev));
      }
    }
  }, [currentPath, currentSearch, sections]);

  const toggleSection = (sectionId: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [sectionId]: !prev[sectionId],
    }));
  };

  const handleLockedClick = (e: React.MouseEvent, item: NavItem) => {
    e.preventDefault();
    const isPremiumFeature = item.section === "inventory";
    setLockedModal({
      open: true,
      featureName: item.label,
      requiredPlan: isPremiumFeature ? "Premium" : "Plus",
    });
  };

  const renderNavContent = () => (
    <div className="flex h-full flex-col justify-between py-2">
      {/* Scrollable Nav Item Sections */}
      <div className="flex-1 overflow-y-auto px-2 space-y-4 pr-1">
        {sections.map((sec) => {
          const isCollapsed = !!collapsedSections[sec.id];
          return (
            <div key={sec.id} className="space-y-1">
              {sec.id !== "main" && (
                <button
                  type="button"
                  onClick={() => toggleSection(sec.id)}
                  className="flex w-full items-center justify-between px-2.5 py-1 text-[11px] font-bold uppercase tracking-wider text-muted-foreground hover:text-foreground transition"
                >
                  <span>{sec.label}</span>
                  {isCollapsed ? (
                    <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                  ) : (
                    <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                  )}
                </button>
              )}

              {!isCollapsed && (
                <div className="space-y-0.5">
                  {sec.items.map((item) => {
                    // Check if capability is satisfied
                    const isCapabilitySatisfied =
                      !item.requiredCapabilities ||
                      item.requiredCapabilities.length === 0 ||
                      item.requiredCapabilities.some((cap) => hasCapability(cap));

                    const isPathMatch = currentPath === item.to;
                    const currentTab = currentSearch?.tab;
                    const isActive =
                      isPathMatch &&
                      (item.tabParam
                        ? currentTab === item.tabParam || (!currentTab && !!item.isDefault)
                        : !currentTab || !!item.isDefault || !item.tabParam);
                    const Icon = item.icon;

                    if (!isCapabilitySatisfied) {
                      // Locked item
                      return (
                        <button
                          key={item.id}
                          type="button"
                          onClick={(e) => handleLockedClick(e, item)}
                          className="flex w-full items-center justify-between rounded-lg px-2.5 py-2 text-xs font-medium text-muted-foreground transition hover:bg-muted/50 text-left"
                        >
                          <div className="flex items-center gap-2.5">
                            <Icon className="h-4 w-4 shrink-0 text-muted-foreground/70" />
                            <span>{item.label}</span>
                          </div>
                          <span className="flex items-center gap-1 rounded bg-secondary/80 px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">
                            <Lock className="h-2.5 w-2.5" />
                            {item.section === "inventory" ? "Premium" : "Plus"}
                          </span>
                        </button>
                      );
                    }

                    return (
                      <Link
                        key={item.id}
                        to={item.to}
                        search={item.search}
                        aria-current={isActive ? "page" : undefined}
                        onClick={() => onMobileOpenChange?.(false)}
                        className={`flex items-center justify-between rounded-lg px-2.5 py-2 text-xs font-medium transition ${
                          isActive
                            ? "bg-primary text-primary-foreground font-semibold shadow-xs"
                            : "text-foreground hover:bg-secondary/80 hover:text-foreground"
                        }`}
                      >
                        <div className="flex items-center gap-2.5">
                          <Icon className={`h-4 w-4 shrink-0 ${isActive ? "text-primary-foreground" : "text-muted-foreground"}`} />
                          <span>{item.label}</span>
                        </div>
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Sidebar Footer info */}
      <div className="border-t border-border/60 p-3 mx-2 mt-2 shrink-0">
        <div className="rounded-xl border border-border/80 bg-secondary/30 p-2.5 text-xs">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-foreground flex items-center gap-1">
              <ShieldCheck className="h-3.5 w-3.5 text-primary" />
              {plan?.name || "Basic"} Plan
            </span>
            <span className="text-[10px] uppercase font-bold text-muted-foreground">
              {plan?.code || "Active"}
            </span>
          </div>
          {plan?.code !== "premium" && isAdminOrOwner && (
            <p className="mt-1 text-[11px] text-muted-foreground">
              Upgrade to Premium for full Recipe Costing & Stocktakes.
            </p>
          )}
        </div>
      </div>
    </div>
  );

  return (
    <>
      {/* Desktop Persistent Sidebar */}
      <aside className="hidden md:flex md:w-56 lg:w-60 flex-col border-r border-border/80 bg-card/60 backdrop-blur shrink-0 h-[calc(100vh-4rem)]">
        {renderNavContent()}
      </aside>

      {/* Mobile Drawer (Sheet) with keyboard escape and focus trap */}
      <Sheet open={mobileOpen} onOpenChange={onMobileOpenChange}>
        <SheetContent side="left" className="w-72 p-0">
          <SheetHeader className="border-b border-border/60 p-4 text-left">
            <SheetTitle className="flex items-center gap-2 text-base font-bold">
              <span>Navigation Menu</span>
            </SheetTitle>
          </SheetHeader>
          <div className="h-[calc(100vh-4rem)] overflow-y-auto">
            {renderNavContent()}
          </div>
        </SheetContent>
      </Sheet>

      {/* Feature Locked Modal */}
      <FeatureLockedModal
        open={lockedModal.open}
        onOpenChange={(open) => setLockedModal((prev) => ({ ...prev, open }))}
        featureName={lockedModal.featureName}
        requiredPlan={lockedModal.requiredPlan}
      />
    </>
  );
}
