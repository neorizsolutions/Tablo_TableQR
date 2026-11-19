import { useState } from "react";
import { Link } from "@tanstack/react-router";
import { LogIn, LogOut, QrCode, Menu, LayoutDashboard } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";

export function SiteHeader() {
  const { user, roles, signOut } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const publicNavLinks = [
    { href: "/#overview", label: "Overview" },
    { href: "/#features", label: "Features" },
    { href: "/#pricing", label: "Pricing" },
  ];

  const appDestination =
    roles.includes("Admin") || roles.includes("Owner")
      ? "/admin"
      : "/dashboard";

  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6">
        {/* Brand */}
        <Link to="/" className="flex items-center gap-2 font-display text-lg font-bold">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-primary text-primary-foreground shadow-soft">
            <QrCode className="h-4 w-4" />
          </span>
          <span className="flex items-center">
            Tablo<span className="text-primary">.</span>
            {user && user.role !== "SuperAdmin" && (
              <span className="text-foreground font-bold text-sm border-l-2 pl-3 ml-3 border-border/60 max-w-[150px] sm:max-w-xs truncate">
                {user.restaurantName || "My Restaurant"}
              </span>
            )}
          </span>
        </Link>

        {/* Desktop Navigation */}
        <nav className="hidden items-center gap-1 md:flex">
          {publicNavLinks.map((l) => (
            <a
              key={l.href}
              href={l.href}
              className="rounded-full px-4 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
            >
              {l.label}
            </a>
          ))}
          {user && (
            <Link
              to={appDestination}
              className="ml-2 inline-flex items-center gap-1.5 rounded-full bg-primary/10 px-4 py-2 text-sm font-semibold text-primary transition-colors hover:bg-primary/20"
            >
              <LayoutDashboard className="h-3.5 w-3.5" />
              Go to Workspace
            </Link>
          )}
        </nav>

        {/* Desktop Auth Actions */}
        <div className="hidden items-center gap-2 sm:flex">
          {user ? (
            <button
              onClick={() => void signOut()}
              className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3.5 py-2 text-xs font-semibold hover:bg-secondary transition"
            >
              <LogOut className="h-3.5 w-3.5" /> Sign out
            </button>
          ) : (
            <Link
              to="/auth"
              className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold shadow-soft hover:bg-secondary transition"
            >
              <LogIn className="h-3.5 w-3.5" /> Staff sign in
            </Link>
          )}
        </div>

        {/* Mobile Hamburger Button */}
        <div className="flex items-center gap-2 md:hidden">
          {user ? (
            <Link
              to={appDestination}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-3 py-1.5 text-xs font-semibold text-primary"
            >
              Workspace
            </Link>
          ) : (
            <Link
              to="/auth"
              className="inline-flex items-center gap-1 rounded-full border border-border px-3 py-1.5 text-xs font-semibold"
            >
              Sign in
            </Link>
          )}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setMobileMenuOpen(true)}
            aria-label="Open mobile navigation"
            className="h-9 w-9"
          >
            <Menu className="h-5 w-5" />
          </Button>
        </div>
      </div>

      {/* Mobile Drawer */}
      <Sheet open={mobileMenuOpen} onOpenChange={setMobileMenuOpen}>
        <SheetContent side="right" className="w-64 p-0">
          <SheetHeader className="border-b border-border/60 p-4 text-left">
            <SheetTitle className="flex items-center gap-2 text-base font-bold">
              <QrCode className="h-5 w-5 text-primary" />
              Tablo Navigation
            </SheetTitle>
          </SheetHeader>
          <div className="flex flex-col gap-1 p-4">
            {publicNavLinks.map((l) => (
              <a
                key={l.href}
                href={l.href}
                onClick={() => setMobileMenuOpen(false)}
                className="rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-secondary hover:text-foreground"
              >
                {l.label}
              </a>
            ))}
            <div className="my-3 border-t border-border/60" />
            {user ? (
              <>
                <Link
                  to={appDestination}
                  onClick={() => setMobileMenuOpen(false)}
                  className="flex items-center gap-2 rounded-lg bg-primary/10 px-3 py-2 text-sm font-semibold text-primary"
                >
                  <LayoutDashboard className="h-4 w-4" />
                  Restaurant Workspace
                </Link>
                <button
                  onClick={() => {
                    setMobileMenuOpen(false);
                    void signOut();
                  }}
                  className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-destructive hover:bg-destructive/10"
                >
                  <LogOut className="h-4 w-4" />
                  Sign out
                </button>
              </>
            ) : (
              <Link
                to="/auth"
                onClick={() => setMobileMenuOpen(false)}
                className="flex items-center gap-2 rounded-lg bg-foreground px-3 py-2 text-sm font-semibold text-background"
              >
                <LogIn className="h-4 w-4" />
                Staff Sign In
              </Link>
            )}
          </div>
        </SheetContent>
      </Sheet>
    </header>
  );
}
