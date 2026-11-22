import { createFileRoute } from "@tanstack/react-router";
import { StorefrontOrdering } from "@/components/storefront-ordering";
import { UtensilsCrossed, ArrowLeft } from "lucide-react";

export const Route = createFileRoute("/order")({
  head: () => ({
    meta: [
      { title: "Direct Online Order — Tablo Heritage" },
      { name: "description", content: "Order online directly for pickup or delivery from Tablo Heritage Restaurant." },
    ],
  }),
  component: PublicStorefrontPage,
});

function PublicStorefrontPage() {
  return (
    <div className="min-h-screen bg-background">
      {/* Public Guest Header */}
      <header className="border-b border-border bg-card/80 backdrop-blur-md sticky top-0 z-40">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-tr from-primary to-primary-light text-primary-foreground shadow-soft">
              <UtensilsCrossed className="h-5 w-5" />
            </div>
            <div>
              <h1 className="font-display font-bold leading-none text-foreground text-lg">Tablo Storefront</h1>
              <p className="text-xs text-muted-foreground">Direct Kitchen-Fresh Ordering</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <a
              href="/"
              className="inline-flex items-center gap-1.5 rounded-full border border-border px-3 py-1.5 text-xs font-semibold text-muted-foreground hover:bg-secondary hover:text-foreground transition-colors"
            >
              <ArrowLeft className="h-3.5 w-3.5" />
              Restaurant Home
            </a>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="py-6">
        <StorefrontOrdering />
      </main>
    </div>
  );
}
