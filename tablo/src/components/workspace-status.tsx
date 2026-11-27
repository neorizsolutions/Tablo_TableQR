import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { AlertCircle, Loader2, LogIn, ShieldAlert } from "lucide-react";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";

export function WorkspaceStatus({
  title,
  description,
  state,
  action,
}: {
  title: string;
  description?: string;
  state: "loading" | "sign-in" | "restricted" | "error";
  action?: ReactNode;
}) {
  const Icon = { loading: Loader2, "sign-in": LogIn, restricted: ShieldAlert, error: AlertCircle }[state];
  const card = (
    <div className="mx-auto max-w-lg px-4 py-12 sm:py-20">
      <div
        className="rounded-3xl border border-border bg-card p-6 text-center shadow-card sm:p-10"
        role={state === "loading" ? "status" : state === "error" ? "alert" : undefined}
      >
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary-soft text-primary">
          <Icon aria-hidden="true" className={state === "loading" ? "h-5 w-5 animate-spin" : "h-5 w-5"} />
        </div>
        <h1 className="mt-4 font-display text-2xl font-bold">{title}</h1>
        {description && <p className="mt-2 text-sm text-muted-foreground">{description}</p>}
        {state === "sign-in" || action ? (
          <div className="mt-6 flex justify-center">
            {state === "sign-in" ? (
              <Button asChild className="rounded-full px-5">
                <Link to="/auth">Sign in</Link>
              </Button>
            ) : action}
          </div>
        ) : null}
      </div>
    </div>
  );

  if (state !== "sign-in") return card;
  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <main>{card}</main>
    </div>
  );
}
