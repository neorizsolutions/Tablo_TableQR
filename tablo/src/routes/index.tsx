import { createFileRoute, Link, useRouter } from "@tanstack/react-router";
import { useEffect } from "react";
import { ArrowRight, Bell, CheckCircle2, ChefHat, Languages, QrCode, Smartphone, Sparkles, TimerReset, ToggleRight, Utensils } from "lucide-react";
import { SiteHeader } from "@/components/site-header";
import { useAuth } from "@/hooks/use-auth";
import heroImg from "@/assets/hero.jpg";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Tablo — Scan. Order. Serve. QR ordering for restaurants" },
      { name: "description", content: "Tablo lets diners scan, browse and order from their phone. Live order updates, menu control and faster service for modern restaurants." },
      { property: "og:title", content: "Tablo — Scan. Order. Serve." },
      { property: "og:description", content: "QR ordering, live kitchen updates and effortless menu control for restaurants." },
    ],
  }),
  component: LandingPage,
});

const features = [
  { icon: QrCode, title: "QR Ordering", desc: "Branded table QR codes open your menu instantly — no app, no signup." },
  { icon: Bell, title: "Live Order Updates", desc: "Tickets fly to the kitchen the moment a guest taps order." },
  { icon: ToggleRight, title: "Menu Availability", desc: "86 a dish in one tap. Guests see real-time stock instantly." },
  { icon: TimerReset, title: "Faster Table Service", desc: "Cut order-to-kitchen time by up to 40% during peak hours." },
];

const steps = [
  { n: "01", title: "Guest scans the QR", desc: "A frictionless menu opens on their phone in under a second." },
  { n: "02", title: "Order goes to kitchen", desc: "Staff get a live ticket with table, items and timing." },
  { n: "03", title: "Status updates flow back", desc: "Guests watch their order move from preparing to served." },
];

const benefits = [
  "Reduce wait times during peak hours",
  "Free up servers for hospitality, not order-taking",
  "Boost average ticket size with rich menu visuals",
  "Eliminate paper menus and reprinting costs",
  "Bilingual menus — English & Malayalam ready",
  "Real-time analytics on best-selling dishes",
];

function LandingPage() {
  const { user, roles, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && user) {
      if (!roles.includes("Admin") && !roles.includes("Owner")) {
        router.navigate({ to: "/dashboard", replace: true });
      }
    }
  }, [user, roles, loading, router]);

  if (loading) return null;

  return (
    <div className="min-h-screen bg-background">
      <SiteHeader />
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 -z-10 bg-gradient-hero" />
        <div className="mx-auto grid max-w-7xl gap-12 px-4 pb-20 pt-12 sm:px-6 md:grid-cols-[1.05fr_1fr] md:items-center md:pt-20 md:pb-28">
          <div className="animate-float-up">
            <span className="inline-flex items-center gap-2 rounded-full border border-primary/20 bg-card px-3 py-1 text-xs font-medium text-primary shadow-soft">
              <Sparkles className="h-3.5 w-3.5" /> Built for modern restaurants
            </span>
            <h1 className="mt-5 font-display text-5xl font-extrabold leading-[1.05] tracking-tight text-foreground sm:text-6xl md:text-7xl">
              Scan. <span className="text-primary">Order.</span> Serve.
            </h1>
            <p className="mt-5 max-w-xl text-base text-muted-foreground sm:text-lg">
              Tablo turns every table into a self-serve ordering counter. Diners order from their phone,
              your kitchen sees it instantly, and your floor staff finally have time for hospitality.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link to="/dashboard" className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-6 py-3 text-sm font-semibold text-foreground shadow-soft transition-colors hover:bg-secondary">
                <ChefHat className="h-4 w-4" /> Waiter dashboard
              </Link>
              <Link to="/admin" className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-6 py-3 text-sm font-semibold text-foreground shadow-soft transition-colors hover:bg-secondary">
                <Utensils className="h-4 w-4" /> Admin panel
              </Link>
            </div>
            <div className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-3 text-sm text-muted-foreground">
              <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-primary" /> No app download</div>
              <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-primary" /> Live order tracking</div>
              <div className="flex items-center gap-2"><Languages className="h-4 w-4 text-primary" /> EN · മലയാളം</div>
            </div>
          </div>
          <div className="relative">
            <div className="absolute -inset-6 -z-10 rounded-[2.5rem] bg-gradient-primary opacity-20 blur-3xl" />
            <div className="overflow-hidden rounded-[2rem] border border-border bg-card shadow-card">
              <img src={heroImg} alt="Tablo QR ordering preview" width={1536} height={1024} className="h-full w-full object-cover" />
            </div>
            <div className="absolute -left-4 bottom-6 hidden items-center gap-3 rounded-2xl border border-border bg-card p-3 pr-5 shadow-card sm:flex">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-primary-soft text-primary"><Bell className="h-4 w-4" /></span>
              <div>
                <p className="text-xs text-muted-foreground">Table 12</p>
                <p className="text-sm font-semibold">New order — 3 items</p>
              </div>
              <span className="ml-2 inline-flex items-center gap-1.5 rounded-full bg-primary-soft px-2 py-0.5 text-xs font-medium text-primary">
                <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Live
              </span>
            </div>
          </div>
        </div>
      </section>
      <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
        <div className="mb-12 max-w-2xl">
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">Why restaurants choose Tablo</p>
          <h2 className="mt-2 font-display text-3xl font-bold sm:text-4xl">Everything your floor needs, nothing it doesn't.</h2>
        </div>
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((f) => (
            <div key={f.title} className="group rounded-3xl border border-border bg-card p-6 shadow-soft transition-all hover:-translate-y-1 hover:shadow-card">
              <span className="grid h-12 w-12 place-items-center rounded-2xl bg-primary-soft text-primary transition-colors group-hover:bg-gradient-primary group-hover:text-primary-foreground">
                <f.icon className="h-5 w-5" />
              </span>
              <h3 className="mt-5 font-display text-lg font-semibold">{f.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">{f.desc}</p>
            </div>
          ))}
        </div>
      </section>
      <section className="bg-secondary/60 py-20">
        <div className="mx-auto max-w-7xl px-4 sm:px-6">
          <div className="mb-12 flex items-end justify-between gap-6">
            <div>
              <p className="text-sm font-semibold uppercase tracking-wider text-primary">How it works</p>
              <h2 className="mt-2 font-display text-3xl font-bold sm:text-4xl">Three steps from scan to served.</h2>
            </div>
            <Smartphone className="hidden h-10 w-10 text-primary md:block" />
          </div>
          <div className="grid gap-5 md:grid-cols-3">
            {steps.map((s) => (
              <div key={s.n} className="relative rounded-3xl border border-border bg-card p-7 shadow-soft">
                <span className="font-display text-5xl font-extrabold text-primary/15">{s.n}</span>
                <h3 className="mt-3 font-display text-xl font-semibold">{s.title}</h3>
                <p className="mt-2 text-sm text-muted-foreground">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>
      <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
        <div className="grid gap-10 rounded-[2rem] border border-border bg-gradient-hero p-8 shadow-card md:grid-cols-2 md:p-12">
          <div>
            <h2 className="font-display text-3xl font-bold sm:text-4xl">Built for the rhythm of a real restaurant.</h2>
            <p className="mt-4 text-muted-foreground">From quiet weekday lunches to a packed Saturday rush, Tablo keeps your floor calm and your kitchen in flow.</p>
          </div>
          <ul className="grid gap-3">
            {benefits.map((b) => (
              <li key={b} className="flex items-start gap-3 rounded-2xl bg-card/70 p-4 backdrop-blur">
                <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                <span className="text-sm font-medium text-foreground">{b}</span>
              </li>
            ))}
          </ul>
        </div>
      </section>
      <footer className="border-t border-border/60 py-10">
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-3 px-4 text-sm text-muted-foreground sm:flex-row sm:px-6">
          <p>© {new Date().getFullYear()} Tablo. Hospitality, simplified.</p>
          <p className="flex items-center gap-2"><span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Live demo environment</p>
        </div>
      </footer>
    </div>
  );
}