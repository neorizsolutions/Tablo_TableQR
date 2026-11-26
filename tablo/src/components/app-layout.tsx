import { type ReactNode } from "react";
import { Navigate, useRouterState } from "@tanstack/react-router";
import { AppSidebar } from "./app-sidebar";
import { useAuth } from "@/hooks/use-auth";
import { useNavigationAccess } from "@/hooks/use-navigation-access";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { getEmployeeNavItems, getRouteMenuItem } from "@/config/navigation";

interface AppLayoutProps {
  children: ReactNode;
  selectedOutletId?: string;
  onSelectOutlet?: (outletId: string) => void;
}

export function AppLayout({ children, selectedOutletId, onSelectOutlet }: AppLayoutProps) {
  const { user, roles } = useAuth();
  const permissions = useNavigationAccess();
  const entitlement = useEffectiveAccess(selectedOutletId);
  const location = useRouterState({ select: (state) => state.location });
  if (!user) return <>{children}</>;
  let content = children;
  if (user.role !== "SuperAdmin") {
    const role = roles[0] || user.role;
    const tab =
      "tab" in location.search && typeof location.search.tab === "string"
        ? location.search.tab
        : undefined;
    const current = getRouteMenuItem(
      location.pathname,
      tab,
      role,
      permissions.data ? permissions.data.allowedMenuIds : undefined,
    );
    const items = getEmployeeNavItems(
      role,
      permissions.data ? permissions.data.allowedMenuIds : [],
    ).filter(
      (item) =>
        !item.requiredCapabilities?.length ||
        item.requiredCapabilities.some(entitlement.hasCapability),
    );
    if (permissions.isPending || entitlement.isLoading)
      content = (
        <p className="p-8" role="status">
          Loading menu access…
        </p>
      );
    else if (permissions.isError || entitlement.isError)
      content = (
        <div className="p-8" role="alert">
          <p>Unable to verify menu access.</p>
          <button
            className="mt-3 underline"
            onClick={() => {
              void permissions.refetch();
              void entitlement.refetch();
            }}
          >
            Retry
          </button>
        </div>
      );
    else if (!current || !items.some((item) => item.id === current.id)) {
      const first = items[0];
      content = first ? (
        <Navigate to={first.to} search={first.search} replace />
      ) : (
        <div className="p-8">
          <h1 className="text-xl font-semibold">No menu access</h1>
          <p className="mt-2">
            Ask your restaurant administrator to enable a workspace menu for your account.
          </p>
        </div>
      );
    }
  }
  return (
    <div className="flex h-dvh flex-col overflow-hidden bg-background md:flex-row">
      <AppSidebar selectedOutletId={selectedOutletId} onSelectOutlet={onSelectOutlet} />
      <main className="min-h-0 min-w-0 flex-1 overflow-auto">{content}</main>
    </div>
  );
}
