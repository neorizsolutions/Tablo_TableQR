import { confirmClose, confirmAction } from "@/lib/confirmation";
import { useEffect, useState } from "react";
import { 
  Printer, 
  Settings, 
  Plus, 
  RefreshCw, 
  CheckCircle2, 
  AlertTriangle, 
  X, 
  Layers, 
  RotateCcw, 
  Copy, 
  Check, 
  FileText, 
  UtensilsCrossed, 
  Wine, 
  SlidersHorizontal,
  ChevronRight,
  ShieldCheck,
  AlertCircle
} from "lucide-react";
import { toast } from "sonner";
import { 
  printingService, 
  KitchenStation, 
  ThermalPrinter, 
  PrintJob, 
  PrintAgent, 
  KitchenPrintingSettings,
  StationRoutingPreview
} from "@/services/printing";
import { useAuth } from "@/hooks/use-auth";
import { useAdminMenu } from "@/hooks/use-menu";

interface Props {
  restaurantId: string;
  onClose: () => void;
}

export function KitchenPrintingSettingsModal({ restaurantId, onClose }: Props) {
  const { user } = useAuth();
  const { data: menuData } = useAdminMenu(restaurantId);

  const [activeTab, setActiveTab] = useState<"setup" | "stations" | "printers" | "operations" | "agent">("setup");
  const [loading, setLoading] = useState(true);

  const [settings, setSettings] = useState<KitchenPrintingSettings | null>(null);
  const [stations, setStations] = useState<KitchenStation[]>([]);
  const [printers, setPrinters] = useState<ThermalPrinter[]>([]);
  const [printJobs, setPrintJobs] = useState<PrintJob[]>([]);
  const [agents, setAgents] = useState<PrintAgent[]>([]);

  // Modals & sub-state
  const [showAddStation, setShowAddStation] = useState(false);
  const [editingStation, setEditingStation] = useState<KitchenStation | null>(null);
  const [showAddPrinter, setShowAddPrinter] = useState(false);
  const [editingPrinter, setEditingPrinter] = useState<ThermalPrinter | null>(null);
  const [selectedTicketPreview, setSelectedTicketPreview] = useState<{ number: string; text: string } | null>(null);

  // New Station Form
  const [stationName, setStationName] = useState("");
  const [stationCode, setStationCode] = useState("");
  const [stationPrinterId, setStationPrinterId] = useState<string>("");
  const [stationIsDefault, setStationIsDefault] = useState(false);

  // New Printer Form
  const [printerName, setPrinterName] = useState("");
  const [printerConnType, setPrinterConnType] = useState<"NetworkTcp" | "LocalAgentQueue">("NetworkTcp");
  const [printerIp, setPrinterIp] = useState("192.168.1.100");
  const [printerPort, setPrinterPort] = useState(9100);
  const [printerLocalQueue, setPrinterLocalQueue] = useState("");
  const [printerWidth, setPrinterWidth] = useState<58 | 80>(80);

  // Agent Registration
  const [newAgentName, setNewAgentName] = useState("");
  const [registeredKey, setRegisteredKey] = useState<string | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);

  // Routing Preview
  const [samplePreviews, setSamplePreviews] = useState<StationRoutingPreview[] | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  // Operations filter
  const [jobStatusFilter, setJobStatusFilter] = useState<string>("All");
  const [reprintReason, setReprintReason] = useState("");
  const [reprintingTicketId, setReprintingTicketId] = useState<string | null>(null);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [s, st, pr, j, ag] = await Promise.all([
        printingService.getSettings(restaurantId),
        printingService.getStations(restaurantId),
        printingService.getPrinters(restaurantId),
        printingService.getPrintJobs(restaurantId, { limit: 40 }),
        printingService.getAgents(restaurantId)
      ]);
      setSettings(s);
      setStations(st);
      setPrinters(pr);
      setPrintJobs(j);
      setAgents(ag);
    } catch (err: any) {
      toast.error(err.message || "Failed to load printing configurations.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [restaurantId]);

  // Handle switching setup mode
  const handleModeChange = async (mode: "SingleKitchen" | "SeparateFoodBeverage" | "CustomMultiStation") => {
    if (!settings) return;
    try {
      const updated = await printingService.updateKitchenPrintingSettings(restaurantId, {
        kotPrintingEnabled: settings.kotPrintingEnabled,
        kitchenSetupMode: mode,
        defaultKitchenStationId: settings.defaultKitchenStationId
      });
      setSettings(updated);
      toast.success(`Kitchen setup changed to ${mode === "SingleKitchen" ? "Single Kitchen" : "Separate Food & Beverages"}.`);
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Handle toggle KOT printing
  const handleTogglePrinting = async () => {
    if (!settings) return;
    const targetState = !settings.kotPrintingEnabled;
    try {
      const updated = await printingService.updateKitchenPrintingSettings(restaurantId, {
        kotPrintingEnabled: targetState,
        kitchenSetupMode: settings.kitchenSetupMode,
        defaultKitchenStationId: settings.defaultKitchenStationId
      });
      setSettings(updated);
      toast.success(targetState ? "Automatic KOT printing enabled." : "Automatic KOT printing paused.");
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Handle Create Station
  const handleCreateStation = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await printingService.createStation(restaurantId, {
        name: stationName.trim(),
        code: stationCode.trim().toUpperCase(),
        isDefault: stationIsDefault,
        printerId: stationPrinterId || undefined
      });
      toast.success("Kitchen station created.");
      setShowAddStation(false);
      setStationName("");
      setStationCode("");
      setStationPrinterId("");
      setStationIsDefault(false);
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Handle Create Printer
  const handleCreatePrinter = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await printingService.createPrinter(restaurantId, {
        name: printerName.trim(),
        connectionType: printerConnType,
        ipAddress: printerConnType === "NetworkTcp" ? printerIp.trim() : undefined,
        port: printerPort,
        localQueueName: printerConnType === "LocalAgentQueue" ? printerLocalQueue.trim() : undefined,
        paperWidthMm: printerWidth,
        charactersPerLine: printerWidth === 58 ? 32 : 48
      });
      toast.success("Thermal printer registered.");
      setShowAddPrinter(false);
      setPrinterName("");
      setPrinterIp("192.168.1.100");
      setPrinterPort(9100);
      setPrinterLocalQueue("");
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Test Print
  const handleTestPrint = async (printerId: string) => {
    try {
      const res = await printingService.testPrint(restaurantId, printerId);
      toast.success(res.message);
      setSelectedTicketPreview({ number: "TEST-PRINT", text: res.readablePreview });
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Run Sample Order Routing Preview
  const handleRunRoutingPreview = async () => {
    if (!menuData?.flatMenu || menuData.flatMenu.length === 0) {
      toast.info("Add some menu items first to preview routing.");
      return;
    }
    try {
      setPreviewLoading(true);
      // Pick up to 4 sample items from menu
      const sampleItems = menuData.flatMenu.slice(0, 4).map((m: any) => ({
        menuItemId: m.id,
        quantity: 2
      }));
      const res = await printingService.previewRouting(restaurantId, sampleItems);
      setSamplePreviews(res.stationTickets);
    } catch (err: any) {
      toast.error(err.message || "Failed to generate routing preview.");
    } finally {
      setPreviewLoading(false);
    }
  };

  // Consolidate stations
  const handleConsolidate = async (targetId: string) => {
    if (!await confirmAction("Are you sure you want to consolidate all routing into this single station? Secondary stations will be deactivated without losing historical tickets.")) {
      return;
    }
    try {
      const res = await printingService.consolidateStations(restaurantId, targetId, "Consolidated by admin");
      toast.success(res.message);
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Register Agent
  const handleRegisterAgent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newAgentName.trim()) return;
    try {
      const res = await printingService.registerAgent(restaurantId, newAgentName.trim());
      setRegisteredKey(res.plaintextApiKey);
      setNewAgentName("");
      toast.success("Print agent registered. Copy your secret key now.");
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  // Retry Print Job
  const handleRetryJob = async (jobId: string) => {
    try {
      await printingService.retryJob(restaurantId, jobId);
      toast.success("Print job re-queued.");
      fetchData();
    } catch (err: any) {
      toast.error(err.message);
    }
  };

  // Cancel Print Job
  const handleCancelJob = async (jobId: string) => {
    if (!await confirmAction({ title: "Cancel print job?", description: "This queued job will no longer print.", confirmLabel: "Cancel job", destructive: true })) return;
    try {
      await printingService.cancelJob(restaurantId, jobId);
      toast.success("Print job cancelled.");
      fetchData();
    } catch (err: any) {
      toast.error(err.message);
    }
  };

  // Submit Reprint
  const handleSubmitReprint = async () => {
    if (!reprintingTicketId || !reprintReason.trim()) return;
    try {
      await printingService.reprintTicket(restaurantId, reprintingTicketId, reprintReason.trim());
      toast.success("Audited reprint ticket queued.");
      setReprintingTicketId(null);
      setReprintReason("");
      fetchData();
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.message);
    }
  };

  const filteredJobs = printJobs.filter(j => jobStatusFilter === "All" || j.status === jobStatusFilter);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="flex h-[90vh] w-full max-w-5xl flex-col rounded-3xl border border-border bg-card shadow-2xl overflow-hidden">
        
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border bg-secondary/30 px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-2xl bg-primary-soft text-primary">
              <Printer className="h-5 w-5" />
            </span>
            <div>
              <h2 className="font-display text-xl font-bold">Kitchen & Thermal Printing</h2>
              <p className="text-xs text-muted-foreground">Configure kitchen stations, ESC/POS thermal printers, and live KOT routing.</p>
            </div>
          </div>
          <button 
            onClick={() => void confirmClose(onClose)}
            className="grid h-9 w-9 place-items-center rounded-full border border-border bg-card text-muted-foreground hover:bg-secondary hover:text-foreground transition"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="flex border-b border-border bg-card px-6">
          <button 
            onClick={() => setActiveTab("setup")}
            className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold transition ${activeTab === "setup" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}
          >
            <SlidersHorizontal className="h-4 w-4" /> Setup & Routing
          </button>
          <button 
            onClick={() => setActiveTab("stations")}
            className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold transition ${activeTab === "stations" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}
          >
            <Layers className="h-4 w-4" /> Kitchen Stations ({stations.length})
          </button>
          <button 
            onClick={() => setActiveTab("printers")}
            className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold transition ${activeTab === "printers" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}
          >
            <Printer className="h-4 w-4" /> Thermal Printers ({printers.length})
          </button>
          <button 
            onClick={() => setActiveTab("operations")}
            className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold transition ${activeTab === "operations" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}
          >
            <FileText className="h-4 w-4" /> Print Operations ({printJobs.length})
          </button>
          <button 
            onClick={() => setActiveTab("agent")}
            className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold transition ${activeTab === "agent" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}
          >
            <ShieldCheck className="h-4 w-4" /> Print Agent & Simulator
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {loading ? (
            <div className="grid h-64 place-items-center">
              <RefreshCw className="h-8 w-8 animate-spin text-primary" />
            </div>
          ) : (
            <>
              {/* TAB 1: SETUP & ROUTING */}
              {activeTab === "setup" && (
                <div className="space-y-6">
                  {/* Master Toggle Banner */}
                  <div className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-border bg-secondary/20 p-5">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className={`inline-block h-3 w-3 rounded-full ${settings?.kotPrintingEnabled ? "bg-emerald-500 animate-pulse" : "bg-muted-foreground/40"}`} />
                        <h3 className="font-display text-lg font-bold">Automatic KOT Printing</h3>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        When enabled, newly accepted orders and incremental additions automatically dispatch thermal KOT tickets to assigned stations.
                      </p>
                    </div>
                    <button
                      onClick={handleTogglePrinting}
                      className={`inline-flex items-center gap-2 rounded-full px-5 py-2.5 text-sm font-semibold transition ${settings?.kotPrintingEnabled ? "bg-red-500/10 text-red-600 hover:bg-red-500/20 border border-red-200" : "bg-gradient-primary text-primary-foreground shadow-glow"}`}
                    >
                      {settings?.kotPrintingEnabled ? "Pause KOT Printing" : "Enable KOT Printing"}
                    </button>
                  </div>

                  {/* Mode Selector */}
                  <div>
                    <h3 className="font-display text-base font-bold">1. Select Kitchen Setup</h3>
                    <p className="text-xs text-muted-foreground">Choose how orders route to your kitchen stations.</p>

                    <div className="mt-3 grid gap-4 sm:grid-cols-2">
                      {/* Single Kitchen Card */}
                      <div 
                        onClick={() => handleModeChange("SingleKitchen")}
                        className={`cursor-pointer rounded-2xl border p-5 transition ${settings?.kitchenSetupMode === "SingleKitchen" ? "border-primary bg-primary-soft/30 shadow-card" : "border-border hover:border-primary/50"}`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <span className="grid h-8 w-8 place-items-center rounded-xl bg-primary-soft text-primary">
                              <UtensilsCrossed className="h-4 w-4" />
                            </span>
                            <h4 className="font-bold">Single Kitchen</h4>
                          </div>
                          {settings?.kitchenSetupMode === "SingleKitchen" && (
                            <CheckCircle2 className="h-5 w-5 text-primary" />
                          )}
                        </div>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Food and beverages route to one kitchen station and printer. A mixed order produces a single consolidated KOT ticket.
                        </p>
                      </div>

                      {/* Separate Food & Beverages Card */}
                      <div 
                        onClick={() => handleModeChange("SeparateFoodBeverage")}
                        className={`cursor-pointer rounded-2xl border p-5 transition ${settings?.kitchenSetupMode === "SeparateFoodBeverage" ? "border-primary bg-primary-soft/30 shadow-card" : "border-border hover:border-primary/50"}`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <span className="grid h-8 w-8 place-items-center rounded-xl bg-primary-soft text-primary">
                              <Wine className="h-4 w-4" />
                            </span>
                            <h4 className="font-bold">Separate Food & Beverages</h4>
                          </div>
                          {settings?.kitchenSetupMode === "SeparateFoodBeverage" && (
                            <CheckCircle2 className="h-5 w-5 text-primary" />
                          )}
                        </div>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Food routes to Food Kitchen, drinks and beverages route to Beverage Station. A mixed order produces separate station tickets containing only relevant items.
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Precedence Rule Explanation */}
                  <div className="rounded-2xl border border-border bg-card p-4">
                    <h4 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Routing Precedence Rule</h4>
                    <p className="mt-1 text-sm font-medium">
                      1. Explicit Menu Item Override &rarr; 2. Category Default Station &rarr; 3. Restaurant Default Station
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Every accepted item resolves deterministically to exactly one station. If a printer is offline, the job waits in queue without silently rerouting.
                    </p>
                  </div>

                  {/* Sample Order Preview Simulator */}
                  <div className="rounded-2xl border border-border bg-card p-5">
                    <div className="flex items-center justify-between">
                      <div>
                        <h4 className="font-bold">Sample Order Routing Preview</h4>
                        <p className="text-xs text-muted-foreground">Test how a mixed sample order splits into station tickets under current settings.</p>
                      </div>
                      <button
                        onClick={handleRunRoutingPreview}
                        disabled={previewLoading}
                        className="inline-flex items-center gap-2 rounded-full border border-border bg-secondary/50 px-4 py-2 text-xs font-semibold hover:bg-secondary transition"
                      >
                        {previewLoading ? <RefreshCw className="h-3 w-3 animate-spin" /> : <ChevronRight className="h-3 w-3" />}
                        Preview Mixed Order
                      </button>
                    </div>

                    {samplePreviews && (
                      <div className="mt-4 space-y-3">
                        <div className="grid gap-3 sm:grid-cols-2">
                          {samplePreviews.map((st) => (
                            <div key={st.stationId} className="rounded-xl border border-border bg-secondary/10 p-3">
                              <div className="flex items-center justify-between">
                                <span className="font-bold text-sm text-primary">Ticket: {st.stationName}</span>
                                <span className="text-[10px] text-muted-foreground">Printer: {st.assignedPrinterName || "None"}</span>
                              </div>
                              <ul className="mt-2 space-y-1 text-xs">
                                {st.items.map((i, idx) => (
                                  <li key={idx} className="flex justify-between border-b border-border/40 py-1">
                                    <span>{i.quantity}x {i.itemName}</span>
                                    <span className="text-[10px] text-muted-foreground font-mono">[{i.resolvedBy}]</span>
                                  </li>
                                ))}
                              </ul>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* TAB 2: KITCHEN STATIONS */}
              {activeTab === "stations" && (
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="font-display text-lg font-bold">Kitchen Stations</h3>
                      <p className="text-xs text-muted-foreground">Manage order destination stations and their assigned printers.</p>
                    </div>
                    <button
                      onClick={() => setShowAddStation(true)}
                      className="inline-flex items-center gap-2 rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow"
                    >
                      <Plus className="h-3.5 w-3.5" /> Add Station
                    </button>
                  </div>

                  <div className="grid gap-3">
                    {stations.map((station) => (
                      <div key={station.id} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm">
                        <div className="flex items-center gap-3">
                          <span className="grid h-10 w-10 place-items-center rounded-xl bg-secondary text-foreground font-bold font-mono text-sm">
                            {station.code}
                          </span>
                          <div>
                            <div className="flex items-center gap-2">
                              <h4 className="font-bold text-sm">{station.name}</h4>
                              {station.isDefault && (
                                <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-semibold text-primary">
                                  Default
                                </span>
                              )}
                              {!station.isActive && (
                                <span className="rounded-full bg-red-500/10 px-2 py-0.5 text-[10px] font-semibold text-red-600">
                                  Inactive
                                </span>
                              )}
                            </div>
                            <p className="text-xs text-muted-foreground">
                              Printer: <span className="font-semibold">{station.printerName || "Not assigned"}</span> &middot; {station.assignedCategoryCount} categories &middot; {station.assignedMenuItemCount} item overrides
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center gap-2">
                          {!station.isDefault && (
                            <button
                              onClick={() => handleConsolidate(station.id)}
                              title="Make Default and Consolidate"
                              className="rounded-full border border-border px-3 py-1.5 text-xs font-medium hover:bg-secondary transition"
                            >
                              Make Default
                            </button>
                          )}
                          {!station.isDefault && station.isActive && (
                            <button
                              onClick={async () => { if (await confirmAction({ title: "Deactivate kitchen?", description: "New orders will no longer route to this kitchen. Review category assignments before continuing.", confirmLabel: "Deactivate", destructive: true })) { try { await printingService.deactivateStation(restaurantId, station.id); toast.success("Station deactivated."); fetchData(); } catch (error: any) { toast.error(error.message); } } }}
                              className="rounded-full border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 transition"
                            >
                              Deactivate
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Add Station Modal */}
                  {showAddStation && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
                      <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-xl">
                        <div className="flex items-center justify-between border-b border-border pb-3">
                          <h4 className="font-bold">Add Kitchen Station</h4>
                          <button onClick={() => void confirmClose(() => setShowAddStation(false))}><X className="h-4 w-4" /></button>
                        </div>
                        <form onSubmit={handleCreateStation} className="mt-4 space-y-3">
                          <div>
                            <label className="text-xs font-semibold">Station Name</label>
                            <input 
                              type="text" 
                              required 
                              placeholder="e.g. Food Kitchen, Beverage Station"
                              value={stationName}
                              onChange={e => setStationName(e.target.value)}
                              className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm"
                            />
                          </div>
                          <div>
                            <label className="text-xs font-semibold">Station Code</label>
                            <input 
                              type="text" 
                              required 
                              placeholder="e.g. FOOD, BEV, BAR"
                              value={stationCode}
                              onChange={e => setStationCode(e.target.value)}
                              className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm font-mono uppercase"
                            />
                          </div>
                          <div>
                            <label className="text-xs font-semibold">Assigned Thermal Printer</label>
                            <select
                              value={stationPrinterId}
                              onChange={e => setStationPrinterId(e.target.value)}
                              className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm"
                            >
                              <option value="">-- No Printer Assigned --</option>
                              {printers.map(p => (
                                <option key={p.id} value={p.id}>{p.name} ({p.paperWidthMm}mm)</option>
                              ))}
                            </select>
                          </div>
                          <div className="flex items-center gap-2 pt-2">
                            <input
                              type="checkbox"
                              id="isDef"
                              checked={stationIsDefault}
                              onChange={e => setStationIsDefault(e.target.checked)}
                              className="rounded"
                            />
                            <label htmlFor="isDef" className="text-xs font-medium">Set as Restaurant Default Station</label>
                          </div>
                          <div className="flex justify-end gap-2 pt-4">
                            <button 
                              type="button" 
                              onClick={() => void confirmClose(() => setShowAddStation(false))}
                              className="rounded-full border border-border px-4 py-2 text-xs font-semibold"
                            >
                              Cancel
                            </button>
                            <button 
                              type="submit" 
                              className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow"
                            >
                              Save Station
                            </button>
                          </div>
                        </form>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* TAB 3: THERMAL PRINTERS */}
              {activeTab === "printers" && (
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="font-display text-lg font-bold">ESC/POS Thermal Printers</h3>
                      <p className="text-xs text-muted-foreground">Register Network TCP printers or local printing agent queues.</p>
                    </div>
                    <button
                      onClick={() => setShowAddPrinter(true)}
                      className="inline-flex items-center gap-2 rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow"
                    >
                      <Plus className="h-3.5 w-3.5" /> Register Printer
                    </button>
                  </div>

                  <div className="grid gap-3">
                    {printers.map((printer) => (
                      <div key={printer.id} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm">
                        <div className="flex items-center gap-3">
                          <span className="grid h-10 w-10 place-items-center rounded-xl bg-secondary text-primary">
                            <Printer className="h-5 w-5" />
                          </span>
                          <div>
                            <div className="flex items-center gap-2">
                              <h4 className="font-bold text-sm">{printer.name}</h4>
                              <span className="rounded-full bg-secondary px-2 py-0.5 text-[10px] font-mono">
                                {printer.paperWidthMm}mm ({printer.charactersPerLine} cols)
                              </span>
                              <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${printer.status === "Online" ? "bg-emerald-500/10 text-emerald-600" : printer.status === "Offline" ? "bg-red-500/10 text-red-600" : "bg-muted text-muted-foreground"}`}>
                                {printer.status}
                              </span>
                            </div>
                            <p className="text-xs text-muted-foreground">
                              {printer.connectionType === "NetworkTcp" ? `Network TCP: ${printer.ipAddress}:${printer.port}` : `Local Queue: ${printer.localQueueName}`}
                              {printer.assignedStationNames.length > 0 && ` &middot; Assigned to: ${printer.assignedStationNames.join(", ")}`}
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => handleTestPrint(printer.id)}
                            className="rounded-full border border-border bg-secondary/30 px-3 py-1.5 text-xs font-semibold hover:bg-secondary transition"
                          >
                            Test Print
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Add Printer Modal */}
                  {showAddPrinter && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
                      <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-xl">
                        <div className="flex items-center justify-between border-b border-border pb-3">
                          <h4 className="font-bold">Register Thermal Printer</h4>
                          <button onClick={() => void confirmClose(() => setShowAddPrinter(false))}><X className="h-4 w-4" /></button>
                        </div>
                        <form onSubmit={handleCreatePrinter} className="mt-4 space-y-3">
                          <div>
                            <label className="text-xs font-semibold">Printer Name</label>
                            <input 
                              type="text" 
                              required 
                              placeholder="e.g. Kitchen Main 80mm, Bar POS-58"
                              value={printerName}
                              onChange={e => setPrinterName(e.target.value)}
                              className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm"
                            />
                          </div>
                          <div>
                            <label className="text-xs font-semibold">Connection Type</label>
                            <select
                              value={printerConnType}
                              onChange={e => setPrinterConnType(e.target.value as any)}
                              className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm"
                            >
                              <option value="NetworkTcp">Network TCP (Ethernet / Wi-Fi)</option>
                              <option value="LocalAgentQueue">Local Agent Print Queue / USB</option>
                            </select>
                          </div>
                          {printerConnType === "NetworkTcp" ? (
                            <div className="grid grid-cols-3 gap-2">
                              <div className="col-span-2">
                                <label className="text-xs font-semibold">IP Address (LAN)</label>
                                <input 
                                  type="text" 
                                  required 
                                  placeholder="192.168.1.100"
                                  value={printerIp}
                                  onChange={e => setPrinterIp(e.target.value)}
                                  className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm font-mono"
                                />
                              </div>
                              <div>
                                <label className="text-xs font-semibold">Port</label>
                                <input 
                                  type="number" 
                                  value={printerPort}
                                  onChange={e => setPrinterPort(parseInt(e.target.value, 10))}
                                  className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm font-mono"
                                />
                              </div>
                            </div>
                          ) : (
                            <div>
                              <label className="text-xs font-semibold">Local Queue Name</label>
                              <input 
                                type="text" 
                                required 
                                placeholder="POS-80 or USB001"
                                value={printerLocalQueue}
                                onChange={e => setPrinterLocalQueue(e.target.value)}
                                className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm font-mono"
                              />
                            </div>
                          )}
                          <div>
                            <label className="text-xs font-semibold">Paper Width</label>
                            <div className="mt-1 flex gap-3">
                              <label className="flex items-center gap-1.5 text-xs font-medium">
                                <input 
                                  type="radio" 
                                  name="pw" 
                                  checked={printerWidth === 80} 
                                  onChange={() => setPrinterWidth(80)} 
                                /> 80 mm (Standard, 48 cols)
                              </label>
                              <label className="flex items-center gap-1.5 text-xs font-medium">
                                <input 
                                  type="radio" 
                                  name="pw" 
                                  checked={printerWidth === 58} 
                                  onChange={() => setPrinterWidth(58)} 
                                /> 58 mm (Compact, 32 cols)
                              </label>
                            </div>
                          </div>
                          <div className="flex justify-end gap-2 pt-4">
                            <button 
                              type="button" 
                              onClick={() => void confirmClose(() => setShowAddPrinter(false))}
                              className="rounded-full border border-border px-4 py-2 text-xs font-semibold"
                            >
                              Cancel
                            </button>
                            <button 
                              type="submit" 
                              className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow"
                            >
                              Save Printer
                            </button>
                          </div>
                        </form>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* TAB 4: PRINT OPERATIONS */}
              {activeTab === "operations" && (
                <div className="space-y-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <h3 className="font-display text-lg font-bold">Print Operations Queue</h3>
                      <p className="text-xs text-muted-foreground">Monitor delivery, retry failed prints, and issue audited reprints.</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <select
                        value={jobStatusFilter}
                        onChange={e => setJobStatusFilter(e.target.value)}
                        className="rounded-full border border-border px-3 py-1.5 text-xs"
                      >
                        <option value="All">All Statuses</option>
                        <option value="Pending">Pending</option>
                        <option value="Claimed">Claimed</option>
                        <option value="Printed">Printed</option>
                        <option value="Failed">Failed</option>
                        <option value="UncertainDelivery">Uncertain Delivery</option>
                        <option value="Cancelled">Cancelled</option>
                      </select>
                      <button 
                        onClick={fetchData}
                        className="rounded-full border border-border p-2 hover:bg-secondary transition"
                      >
                        <RefreshCw className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </div>

                  <div className="overflow-x-auto rounded-2xl border border-border bg-card">
                    <table className="w-full text-left text-xs">
                      <thead className="border-b border-border bg-secondary/30 text-muted-foreground">
                        <tr>
                          <th className="p-3">Time (IST)</th>
                          <th className="p-3">Ticket / Job</th>
                          <th className="p-3">Station</th>
                          <th className="p-3">Printer</th>
                          <th className="p-3">Status</th>
                          <th className="p-3 text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-border">
                        {filteredJobs.length === 0 ? (
                          <tr>
                            <td colSpan={6} className="p-6 text-center text-muted-foreground">
                              No print jobs found matching filters.
                            </td>
                          </tr>
                        ) : (
                          filteredJobs.map((job) => (
                            <tr key={job.id} className="hover:bg-secondary/10">
                              <td className="p-3 whitespace-nowrap text-muted-foreground">
                                {new Date(job.createdAtUtc).toLocaleTimeString('en-IN', { timeZone: 'Asia/Kolkata', hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                              </td>
                              <td className="p-3 font-mono font-semibold">
                                {job.ticketNumber || job.jobType}
                              </td>
                              <td className="p-3">{job.stationName || "—"}</td>
                              <td className="p-3">{job.printerName}</td>
                              <td className="p-3">
                                <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${job.status === "Printed" ? "bg-emerald-500/10 text-emerald-600" : job.status === "Pending" ? "bg-amber-500/10 text-amber-600" : job.status === "Failed" ? "bg-red-500/10 text-red-600" : job.status === "UncertainDelivery" ? "bg-purple-500/10 text-purple-600" : "bg-muted text-muted-foreground"}`}>
                                  {job.status}
                                </span>
                                {job.lastError && (
                                  <p className="mt-0.5 text-[10px] text-red-500 truncate max-w-[180px]">{job.lastError}</p>
                                )}
                              </td>
                              <td className="p-3 text-right">
                                <div className="flex items-center justify-end gap-1.5">
                                  {job.readableText && (
                                    <button
                                      onClick={() => setSelectedTicketPreview({ number: job.ticketNumber || job.id, text: job.readableText })}
                                      className="rounded-full border border-border px-2.5 py-1 text-[10px] font-semibold hover:bg-secondary"
                                    >
                                      View
                                    </button>
                                  )}
                                  {(job.status === "Failed" || job.status === "UncertainDelivery") && (
                                    <button
                                      onClick={() => handleRetryJob(job.id)}
                                      className="rounded-full bg-primary/10 text-primary px-2.5 py-1 text-[10px] font-semibold hover:bg-primary/20"
                                    >
                                      Retry
                                    </button>
                                  )}
                                  {job.kotTicketId && (
                                    <button
                                      onClick={() => setReprintingTicketId(job.kotTicketId!)}
                                      className="rounded-full border border-border px-2.5 py-1 text-[10px] font-semibold hover:bg-secondary"
                                    >
                                      Reprint
                                    </button>
                                  )}
                                </div>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Monospace Preview Dialog */}
                  {selectedTicketPreview && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
                      <div className="w-full max-w-lg rounded-2xl border border-border bg-card p-6 shadow-2xl">
                        <div className="flex items-center justify-between border-b border-border pb-3">
                          <h4 className="font-bold text-sm">Ticket Preview — {selectedTicketPreview.number}</h4>
                          <button onClick={() => void confirmClose(() => setSelectedTicketPreview(null))}><X className="h-4 w-4" /></button>
                        </div>
                        <div className="mt-4 max-h-[60vh] overflow-y-auto rounded-xl bg-black p-4 font-mono text-xs text-emerald-400 whitespace-pre shadow-inner">
                          {selectedTicketPreview.text}
                        </div>
                        <div className="mt-4 flex justify-end">
                          <button 
                            onClick={() => void confirmClose(() => setSelectedTicketPreview(null))}
                            className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow"
                          >
                            Close
                          </button>
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Reprint Reason Modal */}
                  {reprintingTicketId && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
                      <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-2xl">
                        <div className="flex items-center justify-between border-b border-border pb-3">
                          <h4 className="font-bold">Authorize Ticket Reprint</h4>
                          <button onClick={() => void confirmClose(() => setReprintingTicketId(null))}><X className="h-4 w-4" /></button>
                        </div>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Reprints are logged in the audit trail and explicitly marked as <span className="font-bold text-red-600">*** REPRINT ***</span> on the printed ticket.
                        </p>
                        <div className="mt-4">
                          <label className="text-xs font-semibold">Reprint Reason</label>
                          <input 
                            type="text" 
                            required 
                            placeholder="e.g. Printer paper jam, kitchen misplaced ticket"
                            value={reprintReason}
                            onChange={e => setReprintReason(e.target.value)}
                            className="mt-1 w-full rounded-xl border border-border px-3 py-2 text-sm"
                          />
                        </div>
                        <div className="mt-4 flex justify-end gap-2">
                          <button 
                            type="button" 
                            onClick={() => void confirmClose(() => setReprintingTicketId(null))}
                            className="rounded-full border border-border px-4 py-2 text-xs font-semibold"
                          >
                            Cancel
                          </button>
                          <button 
                            onClick={handleSubmitReprint}
                            disabled={!reprintReason.trim()}
                            className="rounded-full bg-gradient-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
                          >
                            Confirm Reprint
                          </button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* TAB 5: PRINT AGENT & SIMULATOR */}
              {activeTab === "agent" && (
                <div className="space-y-6">
                  <div>
                    <h3 className="font-display text-lg font-bold">Local Printing Agent & Simulator</h3>
                    <p className="text-xs text-muted-foreground">
                      Connect local thermal printers securely without exposing printer TCP ports publicly.
                    </p>
                  </div>

                  {/* Registered Token Alert */}
                  {registeredKey && (
                    <div className="rounded-2xl border border-emerald-500/30 bg-emerald-500/10 p-5">
                      <h4 className="font-bold text-emerald-800 text-sm flex items-center gap-2">
                        <CheckCircle2 className="h-4 w-4" /> Agent Registered Successfully
                      </h4>
                      <p className="mt-1 text-xs text-emerald-700">
                        Copy this secret agent key now. It is never displayed again:
                      </p>
                      <div className="mt-2 flex items-center gap-2">
                        <input 
                          type="text" 
                          readOnly 
                          value={registeredKey}
                          className="flex-1 rounded-xl border border-emerald-400 bg-card px-3 py-2 font-mono text-xs font-bold"
                        />
                        <button
                          onClick={() => {
                            navigator.clipboard.writeText(registeredKey);
                            setCopiedKey(true);
                            setTimeout(() => setCopiedKey(false), 2000);
                          }}
                          className="rounded-xl bg-emerald-600 px-4 py-2 text-xs font-semibold text-white shadow"
                        >
                          {copiedKey ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                        </button>
                      </div>
                    </div>
                  )}

                  {/* Agent Registration Form */}
                  <div className="rounded-2xl border border-border bg-card p-5">
                    <h4 className="font-bold text-sm">Register Local Printing Agent</h4>
                    <form onSubmit={handleRegisterAgent} className="mt-3 flex gap-2">
                      <input 
                        type="text" 
                        required 
                        placeholder="e.g. POS Counter 1 Agent"
                        value={newAgentName}
                        onChange={e => setNewAgentName(e.target.value)}
                        className="flex-1 rounded-xl border border-border px-3 py-2 text-sm"
                      />
                      <button 
                        type="submit" 
                        className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow"
                      >
                        Register Agent
                      </button>
                    </form>
                  </div>

                  {/* Active Agents List */}
                  <div className="rounded-2xl border border-border bg-card p-5">
                    <h4 className="font-bold text-sm">Registered Print Agents</h4>
                    <div className="mt-3 divide-y divide-border">
                      {agents.length === 0 ? (
                        <p className="py-4 text-center text-xs text-muted-foreground">No agents registered yet.</p>
                      ) : (
                        agents.map((ag) => (
                          <div key={ag.id} className="flex items-center justify-between py-3 text-xs">
                            <div>
                              <span className="font-bold">{ag.agentName}</span>
                              <span className="ml-2 font-mono text-muted-foreground">({ag.keyPrefix}...)</span>
                              <p className="text-[10px] text-muted-foreground">
                                Last heartbeat: {ag.lastHeartbeatUtc ? new Date(ag.lastHeartbeatUtc).toLocaleString() : "Never"}
                              </p>
                            </div>
                            <button
                              onClick={async () => { if (await confirmAction({ title: "Revoke print agent?", description: "This agent will lose access to print jobs.", confirmLabel: "Revoke", destructive: true })) { try { await printingService.revokeAgent(restaurantId, ag.id); toast.success("Agent revoked."); fetchData(); } catch (error: any) { toast.error(error.message); } } }}
                              className="rounded-full border border-red-200 px-3 py-1 text-red-600 hover:bg-red-50"
                            >
                              Revoke
                            </button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                  {/* Run Commands Guide */}
                  <div className="rounded-2xl border border-border bg-secondary/20 p-5">
                    <h4 className="font-bold text-sm">Local Startup Commands</h4>
                    <div className="mt-3 space-y-3 text-xs">
                      <div>
                        <p className="font-semibold text-muted-foreground">To run in Development Simulator Mode:</p>
                        <pre className="mt-1 overflow-x-auto rounded-xl bg-black p-3 font-mono text-emerald-400">
npm run print:sim -- --api-url http://localhost:5166 --agent-key &lt;your-key&gt;
                        </pre>
                      </div>
                      <div>
                        <p className="font-semibold text-muted-foreground">To run with Live ESC/POS Network TCP Printers:</p>
                        <pre className="mt-1 overflow-x-auto rounded-xl bg-black p-3 font-mono text-emerald-400">
node scripts/print-agent.mjs --mode live --api-url http://localhost:5166 --agent-key &lt;your-key&gt;
                        </pre>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </>
          )}
        </div>

      </div>
    </div>
  );
}
