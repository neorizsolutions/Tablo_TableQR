import { useState, type ReactNode } from "react";
import { AppHeader } from "./app-header";
import { AppSidebar } from "./app-sidebar";
import { useAuth } from "@/hooks/use-auth";

interface AppLayoutProps {
  children: ReactNode;
  selectedOutletId?: string;
  onSelectOutlet?: (outletId: string) => void;
}

export function AppLayout({
  children,
  selectedOutletId,
  onSelectOutlet,
}: AppLayoutProps) {
  const { user } = useAuth();
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  // If no logged in user, render children directly without sidebar
  if (!user) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <AppHeader
        onToggleMobileSidebar={() => setMobileSidebarOpen((prev) => !prev)}
        selectedOutletId={selectedOutletId}
        onSelectOutlet={onSelectOutlet}
      />
      <div className="flex flex-1 overflow-hidden">
        <AppSidebar
          mobileOpen={mobileSidebarOpen}
          onMobileOpenChange={setMobileSidebarOpen}
          selectedOutletId={selectedOutletId}
        />
        <main className="flex-1 overflow-y-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
