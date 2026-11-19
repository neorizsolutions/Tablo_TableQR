import { useState } from "react";
import { Link } from "@tanstack/react-router";
import {
  QrCode,
  LogOut,
  Menu,
  ShieldCheck,
  ChevronDown,
  Building2,
  Store,
  Sparkles,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SubscriptionDetailsModal } from "./subscription-details-modal";

interface AppHeaderProps {
  onToggleMobileSidebar?: () => void;
  selectedOutletId?: string;
  onSelectOutlet?: (outletId: string) => void;
}

export function AppHeader({
  onToggleMobileSidebar,
  selectedOutletId,
  onSelectOutlet,
}: AppHeaderProps) {
  const { user, roles, signOut } = useAuth();
  const { access, plan } = useEffectiveAccess(selectedOutletId);
  const [subModalOpen, setSubModalOpen] = useState(false);

  const role = roles[0] || user?.role || "Staff";
  const planCode = plan?.code || "basic";
  const planName = plan?.name || "Standard";

  const getPlanBadgeClass = (code: string) => {
    switch (code) {
      case "premium":
        return "bg-primary text-primary-foreground hover:bg-primary/90 border-transparent";
      case "plus":
        return "bg-primary/20 text-primary border-primary/40 hover:bg-primary/30";
      default:
        return "bg-secondary text-secondary-foreground border-border";
    }
  };

  return (
    <>
      <header className="sticky top-0 z-30 flex h-14 w-full items-center justify-between border-b border-border/80 bg-background/95 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/80 sm:px-6">
        {/* Left: Hamburger (mobile), Logo, Restaurant name, Outlets */}
        <div className="flex items-center gap-3">
          {onToggleMobileSidebar && (
            <Button
              variant="ghost"
              size="icon"
              className="md:hidden h-9 w-9 text-muted-foreground hover:text-foreground"
              onClick={onToggleMobileSidebar}
              aria-label="Toggle navigation menu"
            >
              <Menu className="h-5 w-5" />
            </Button>
          )}

          <Link to="/" className="flex items-center gap-2 font-display text-base font-bold">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-gradient-primary text-primary-foreground shadow-soft">
              <QrCode className="h-4 w-4" />
            </span>
            <span className="hidden font-display text-base sm:inline-block">
              Tablo<span className="text-primary">.</span>
            </span>
          </Link>

          {/* Restaurant / Tenant Indicator */}
          {user && user.role !== "SuperAdmin" && (
            <div className="hidden items-center gap-2 border-l border-border/60 pl-3 sm:flex">
              <div className="flex items-center gap-1.5 text-xs font-semibold text-foreground">
                <Building2 className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="max-w-[140px] truncate md:max-w-[200px]">
                  {user.restaurantName || "My Restaurant"}
                </span>
              </div>
            </div>
          )}

          {/* Outlet Switcher if multi-outlet */}
          {access?.outlets && access.outlets.length > 1 && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm" className="h-7 gap-1 px-2 text-xs">
                  <Store className="h-3 w-3 text-muted-foreground" />
                  <span className="max-w-[90px] truncate">
                    {access.outlets.find((o) => o.id === selectedOutletId)?.name || "All Outlets"}
                  </span>
                  <ChevronDown className="h-3 w-3 text-muted-foreground opacity-60" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-48">
                <DropdownMenuLabel className="text-xs">Select Outlet</DropdownMenuLabel>
                <DropdownMenuSeparator />
                {access.outlets.map((o) => (
                  <DropdownMenuItem
                    key={o.id}
                    onClick={() => onSelectOutlet?.(o.id)}
                    className="text-xs flex items-center justify-between"
                  >
                    <span className="truncate">{o.name}</span>
                    {o.isMain && (
                      <span className="text-[10px] text-muted-foreground bg-secondary px-1.5 py-0.5 rounded">
                        Main
                      </span>
                    )}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>

        {/* Right: Plan Badge, Role Badge, User dropdown */}
        <div className="flex items-center gap-2">
          {/* Plan badge (click to view subscription modal) */}
          {user && user.role !== "SuperAdmin" && (
            <button
              onClick={() => setSubModalOpen(true)}
              className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold transition hover:opacity-90 cursor-pointer shadow-xs border"
              title="Click to view subscription details and entitlements"
            >
              <Badge
                variant="outline"
                className={`text-[11px] font-bold capitalize transition-colors ${getPlanBadgeClass(
                  planCode
                )}`}
              >
                {planCode === "premium" && <Sparkles className="h-3 w-3 mr-1" />}
                {planName}
              </Badge>
            </button>
          )}

          {/* Role badge */}
          <span className="hidden sm:inline-flex items-center rounded-md bg-secondary/80 px-2 py-0.5 text-xs font-medium text-secondary-foreground">
            {role}
          </span>

          {/* User Profile / Menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-8 gap-2 px-2 text-xs">
                <div className="grid h-6 w-6 place-items-center rounded-full bg-primary/10 text-primary font-bold text-xs">
                  {user?.email?.charAt(0).toUpperCase() || "U"}
                </div>
                <span className="hidden max-w-[100px] truncate text-muted-foreground lg:inline-block">
                  {user?.email}
                </span>
                <ChevronDown className="h-3 w-3 text-muted-foreground opacity-60" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel className="font-normal">
                <div className="flex flex-col space-y-1">
                  <p className="text-sm font-medium leading-none truncate">{user?.email}</p>
                  <p className="text-xs leading-none text-muted-foreground">
                    Role: <span className="font-semibold text-foreground">{role}</span>
                  </p>
                  {user?.restaurantName && (
                    <p className="text-xs leading-none text-muted-foreground truncate">
                      {user.restaurantName}
                    </p>
                  )}
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              {user?.role !== "SuperAdmin" && (
                <DropdownMenuItem onClick={() => setSubModalOpen(true)} className="text-xs">
                  <ShieldCheck className="h-4 w-4 mr-2 text-primary" />
                  Subscription & Entitlements
                </DropdownMenuItem>
              )}
              {user?.role === "SuperAdmin" && (
                <DropdownMenuItem asChild>
                  <Link to="/super-admin" className="text-xs">
                    Super Admin Console
                  </Link>
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onClick={() => void signOut()}
                className="text-xs text-destructive focus:text-destructive"
              >
                <LogOut className="h-4 w-4 mr-2" />
                Sign Out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </header>

      {/* Subscription details modal */}
      <SubscriptionDetailsModal
        open={subModalOpen}
        onOpenChange={setSubModalOpen}
        access={access}
        restaurantName={user?.restaurantName}
      />
    </>
  );
}
