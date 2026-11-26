import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useAuth } from "@/hooks/use-auth";
import { useNavigationAccess } from "@/hooks/use-navigation-access";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { getEmployeeNavItems, getRouteMenuItem } from "@/config/navigation";

/** Workspace shortcuts share the sidebar's role, employee and plan restrictions. */
export function EmployeeMenuLink({
  to,
  search,
  children,
  className,
}: {
  to: string;
  search?: Record<string, string>;
  children: ReactNode;
  className?: string;
}) {
  const { user } = useAuth();
  const permissions = useNavigationAccess();
  const entitlement = useEffectiveAccess();
  if (!user || permissions.isError || entitlement.isError || !permissions.data) return null;
  const target = getRouteMenuItem(to, search?.tab, user.role, permissions.data.allowedMenuIds);
  const allowed = getEmployeeNavItems(user.role, permissions.data.allowedMenuIds);
  if (
    !target ||
    !allowed.some((item) => item.id === target.id) ||
    (target.requiredCapabilities?.length &&
      !target.requiredCapabilities.some(entitlement.hasCapability))
  )
    return null;
  return (
    <Link to={to} search={search} className={className}>
      {children}
    </Link>
  );
}
