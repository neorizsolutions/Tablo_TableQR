import { useState, useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Users,
  Search,
  Plus,
  Award,
  Star,
  History,
  Phone,
  Mail,
  Calendar,
  Sparkles,
  ShieldCheck,
  CheckCircle2,
  AlertCircle,
  MessageSquare,
  ArrowUpRight,
  ArrowDownLeft,
  RefreshCw,
  Tag,
  Filter,
} from "lucide-react";
import { formatINR } from "@/lib/mock-data";
import { crmApi, type CustomerProfile, type LoyaltyLedgerEntry, type CustomerFeedback } from "@/services/crm";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

interface CrmLoyaltyManagerProps {
  restaurantId: string;
  userRole?: string;
}

export function CrmLoyaltyManager({ restaurantId, userRole = "Admin" }: CrmLoyaltyManagerProps) {
  const queryClient = useQueryClient();
  const isAdminOrOwner = userRole === "Admin" || userRole === "Owner";

  const [activeTab, setActiveTab] = useState<"customers" | "loyalty" | "feedback">("customers");
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>("cust-1");
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isRedeemOpen, setIsRedeemOpen] = useState(false);
  const [redeemPoints, setRedeemPoints] = useState<number>(50);

  // Form state for creating customer
  const [formPhone, setFormPhone] = useState("");
  const [formName, setFormName] = useState("");
  const [formEmail, setFormEmail] = useState("");
  const [formNotes, setFormNotes] = useState("");
  const [formTags, setFormTags] = useState("Regular");

  // Fetch customers
  const { data: customers = [], isLoading: loadingCustomers, refetch: refetchCustomers } = useQuery({
    queryKey: ["crmCustomers", restaurantId, searchQuery],
    queryFn: () => crmApi.getCustomers(restaurantId, searchQuery),
  });

  // Fetch selected customer detail with ledger
  const { data: customerDetail, isLoading: loadingDetail, refetch: refetchDetail } = useQuery({
    queryKey: ["crmCustomerDetail", restaurantId, selectedCustomerId],
    queryFn: () => (selectedCustomerId ? crmApi.getCustomerById(restaurantId, selectedCustomerId) : null),
    enabled: !!selectedCustomerId,
  });

  // Fetch all feedback
  const { data: feedbacks = [], isLoading: loadingFeedback, refetch: refetchFeedback } = useQuery({
    queryKey: ["crmFeedback", restaurantId],
    queryFn: () => crmApi.getFeedback(restaurantId),
  });

  // Mask phone number for non-admin roles (e.g. Waiter)
  const maskPhone = (phone: string) => {
    if (isAdminOrOwner) return phone;
    if (!phone || phone.length < 6) return "***";
    return phone.slice(0, 3) + "****" + phone.slice(-3);
  };

  // Create customer mutation
  const createCustomerMutation = useMutation({
    mutationFn: (data: Partial<CustomerProfile>) => crmApi.createCustomer(restaurantId, data),
    onSuccess: (newCust) => {
      toast.success(`Customer ${newCust.name} created successfully.`);
      queryClient.invalidateQueries({ queryKey: ["crmCustomers", restaurantId] });
      setSelectedCustomerId(newCust.id);
      setIsCreateOpen(false);
      setFormPhone("");
      setFormName("");
      setFormEmail("");
      setFormNotes("");
    },
    onError: (err: any) => {
      toast.error(err.message || "Failed to create customer.");
    },
  });

  // Redeem points mutation
  const redeemPointsMutation = useMutation({
    mutationFn: (data: { customerId: string; billId: string; pointsToRedeem: number }) =>
      crmApi.redeemPoints(restaurantId, data),
    onSuccess: (res) => {
      toast.success(`Redeemed ${res.pointsRedeemed} points (₹${res.discountAmountInr} discount applied).`);
      queryClient.invalidateQueries({ queryKey: ["crmCustomers", restaurantId] });
      queryClient.invalidateQueries({ queryKey: ["crmCustomerDetail", restaurantId, selectedCustomerId] });
      setIsRedeemOpen(false);
    },
    onError: (err: any) => {
      toast.error(err.message || "Failed to redeem loyalty points.");
    },
  });

  // Feedback status update mutation
  const updateFeedbackStatusMutation = useMutation({
    mutationFn: ({ id, status, staffNotes }: { id: string; status: string; staffNotes?: string }) =>
      crmApi.updateFeedbackStatus(restaurantId, id, status, staffNotes),
    onSuccess: () => {
      toast.success("Feedback status updated.");
      queryClient.invalidateQueries({ queryKey: ["crmFeedback", restaurantId] });
    },
  });

  const selectedCustomer = customerDetail?.customer || customers.find((c) => c.id === selectedCustomerId);
  const ledgerEntries = customerDetail?.ledger || [];

  return (
    <div className="space-y-6">
      {/* Top Banner & Tab Navigation */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border/60 pb-4">
        <div>
          <h2 className="font-display text-2xl font-bold flex items-center gap-2">
            <Users className="h-6 w-6 text-primary" />
            Customer CRM &amp; Loyalty Ledger
          </h2>
          <p className="text-xs text-muted-foreground mt-1">
            Tenant-scoped customer profiles, retry-safe loyalty points ledger, and receipt-linked guest feedback.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex rounded-full border border-border bg-card p-1 text-xs">
            <button
              onClick={() => setActiveTab("customers")}
              className={`rounded-full px-3 py-1 font-semibold transition ${
                activeTab === "customers" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              Customer Directory
            </button>
            <button
              onClick={() => setActiveTab("loyalty")}
              className={`rounded-full px-3 py-1 font-semibold transition ${
                activeTab === "loyalty" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              Loyalty Audit Trail
            </button>
            <button
              onClick={() => setActiveTab("feedback")}
              className={`rounded-full px-3 py-1 font-semibold transition ${
                activeTab === "feedback" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              Guest Feedback ({feedbacks.length})
            </button>
          </div>
        </div>
      </div>

      {/* ========================================================
          TAB 1: CUSTOMER DIRECTORY & PROFILE DETAIL
          ======================================================== */}
      {activeTab === "customers" && (
        <div className="grid gap-6 lg:grid-cols-[380px_1fr]">
          {/* Left: Searchable Customer List */}
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search by phone, name, or tag..."
                  className="pl-9 text-xs"
                />
              </div>
              <Button size="sm" onClick={() => setIsCreateOpen(true)} className="gap-1 text-xs">
                <Plus className="h-3.5 w-3.5" /> Add
              </Button>
            </div>

            <div className="rounded-2xl border border-border bg-card divide-y divide-border/60 max-h-[600px] overflow-y-auto shadow-soft">
              {loadingCustomers ? (
                <div className="p-8 text-center text-xs text-muted-foreground">Loading customers...</div>
              ) : customers.length === 0 ? (
                <div className="p-8 text-center text-xs text-muted-foreground">
                  No customers found. Click "Add" to register a diner.
                </div>
              ) : (
                customers.map((c) => {
                  const isSelected = c.id === selectedCustomerId;
                  return (
                    <div
                      key={c.id}
                      onClick={() => setSelectedCustomerId(c.id)}
                      className={`p-3.5 cursor-pointer transition flex items-center justify-between ${
                        isSelected ? "bg-primary/10 border-l-4 border-l-primary" : "hover:bg-secondary/40"
                      }`}
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-sm">{c.name}</span>
                          {c.tags?.map((t) => (
                            <Badge key={t} variant="secondary" className="text-[10px] px-1.5 py-0">
                              {t}
                            </Badge>
                          ))}
                        </div>
                        <div className="flex items-center gap-3 text-xs text-muted-foreground">
                          <span className="flex items-center gap-1">
                            <Phone className="h-3 w-3" />
                            {maskPhone(c.phone)}
                          </span>
                          <span>•</span>
                          <span>{c.totalVisits} visits</span>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="flex items-center gap-1 justify-end font-bold text-xs text-amber-600">
                          <Award className="h-3.5 w-3.5" />
                          {c.loyaltyPointsBalance} pts
                        </div>
                        <span className="text-[11px] text-muted-foreground">{formatINR(c.totalSpend)} spent</span>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Right: Selected Customer Profile & Fast Actions */}
          <div className="space-y-5">
            {selectedCustomer ? (
              <div className="rounded-3xl border border-border bg-card p-6 shadow-soft space-y-6">
                {/* Header card */}
                <div className="flex flex-wrap items-center justify-between gap-4 pb-4 border-b border-border/60">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-display text-xl font-bold">{selectedCustomer.name}</h3>
                      {selectedCustomer.tags?.map((tag) => (
                        <Badge key={tag} className="bg-primary/15 text-primary text-xs font-semibold">
                          {tag}
                        </Badge>
                      ))}
                    </div>
                    <p className="text-xs text-muted-foreground flex items-center gap-3">
                      <span>Phone: <strong className="text-foreground">{maskPhone(selectedCustomer.phone)}</strong></span>
                      {selectedCustomer.email && (
                        <span>Email: <strong className="text-foreground">{selectedCustomer.email}</strong></span>
                      )}
                    </p>
                  </div>

                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setIsRedeemOpen(true)}
                      className="text-xs gap-1.5 border-amber-500/40 text-amber-600 hover:bg-amber-50"
                    >
                      <Award className="h-3.5 w-3.5" />
                      Redeem Points
                    </Button>
                  </div>
                </div>

                {/* Key Metrics Grid */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                  <div className="rounded-2xl border border-border bg-secondary/30 p-3.5">
                    <p className="text-xs text-muted-foreground">Loyalty Balance</p>
                    <p className="font-display text-2xl font-bold text-amber-600 mt-1 flex items-center gap-1.5">
                      <Award className="h-5 w-5" />
                      {selectedCustomer.loyaltyPointsBalance}
                    </p>
                    <p className="text-[10px] text-muted-foreground mt-0.5">Worth {formatINR(selectedCustomer.loyaltyPointsBalance)} off bill</p>
                  </div>

                  <div className="rounded-2xl border border-border bg-secondary/30 p-3.5">
                    <p className="text-xs text-muted-foreground">Total Spend</p>
                    <p className="font-display text-2xl font-bold text-foreground mt-1">
                      {formatINR(selectedCustomer.totalSpend)}
                    </p>
                    <p className="text-[10px] text-muted-foreground mt-0.5">Lifetime settled value</p>
                  </div>

                  <div className="rounded-2xl border border-border bg-secondary/30 p-3.5">
                    <p className="text-xs text-muted-foreground">Total Visits</p>
                    <p className="font-display text-2xl font-bold text-foreground mt-1">
                      {selectedCustomer.totalVisits}
                    </p>
                    <p className="text-[10px] text-muted-foreground mt-0.5">Dine-in &amp; takeaway</p>
                  </div>

                  <div className="rounded-2xl border border-border bg-secondary/30 p-3.5">
                    <p className="text-xs text-muted-foreground">Last Dining Date</p>
                    <p className="font-display text-sm font-semibold text-foreground mt-2">
                      {new Date(selectedCustomer.lastVisitUtc).toLocaleDateString("en-IN", {
                        day: "numeric",
                        month: "short",
                        year: "numeric",
                      })}
                    </p>
                    <p className="text-[10px] text-muted-foreground mt-0.5">Recent restaurant order</p>
                  </div>
                </div>

                {/* Staff Notes */}
                {selectedCustomer.notes && (
                  <div className="rounded-2xl border border-border bg-secondary/20 p-4">
                    <p className="text-xs font-semibold text-muted-foreground mb-1">Diner Preferences &amp; Allergies</p>
                    <p className="text-xs text-foreground">{selectedCustomer.notes}</p>
                  </div>
                )}

                {/* Recent Loyalty Transactions */}
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <h4 className="text-sm font-bold flex items-center gap-1.5">
                      <History className="h-4 w-4 text-muted-foreground" />
                      Points History for {selectedCustomer.name}
                    </h4>
                    <span className="text-[11px] text-muted-foreground">Idempotent Ledger</span>
                  </div>

                  <div className="rounded-2xl border border-border overflow-hidden">
                    <table className="w-full text-xs">
                      <thead className="bg-secondary/50 text-muted-foreground border-b border-border text-left">
                        <tr>
                          <th className="p-2.5">Date &amp; Time</th>
                          <th className="p-2.5">Type</th>
                          <th className="p-2.5">Description</th>
                          <th className="p-2.5 text-right">Points</th>
                          <th className="p-2.5 text-right">Balance</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-border/60">
                        {ledgerEntries.length === 0 ? (
                          <tr>
                            <td colSpan={5} className="p-4 text-center text-muted-foreground">
                              No loyalty transactions recorded yet.
                            </td>
                          </tr>
                        ) : (
                          ledgerEntries.map((l) => (
                            <tr key={l.id} className="hover:bg-secondary/20">
                              <td className="p-2.5 text-muted-foreground">
                                {new Date(l.createdAtUtc).toLocaleDateString("en-IN", {
                                  day: "numeric",
                                  month: "short",
                                  hour: "2-digit",
                                  minute: "2-digit",
                                })}
                              </td>
                              <td className="p-2.5">
                                <Badge
                                  variant="outline"
                                  className={`text-[10px] ${
                                    l.transactionType === "Earn"
                                      ? "text-emerald-600 border-emerald-500/30"
                                      : l.transactionType === "Redeem"
                                      ? "text-amber-600 border-amber-500/30"
                                      : "text-rose-600 border-rose-500/30"
                                  }`}
                                >
                                  {l.transactionType}
                                </Badge>
                              </td>
                              <td className="p-2.5 text-foreground">{l.description}</td>
                              <td
                                className={`p-2.5 text-right font-bold ${
                                  l.points > 0 ? "text-emerald-600" : "text-amber-600"
                                }`}
                              >
                                {l.points > 0 ? `+${l.points}` : l.points}
                              </td>
                              <td className="p-2.5 text-right font-semibold">{l.balanceAfter} pts</td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            ) : (
              <div className="rounded-3xl border border-border bg-card p-12 text-center text-muted-foreground">
                <Users className="mx-auto h-8 w-8 text-muted-foreground/60 mb-2" />
                <p className="text-sm font-semibold">Select a customer from the left directory to view profile details.</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ========================================================
          TAB 2: GLOBAL LOYALTY LEDGER AUDIT TRAIL
          ======================================================== */}
      {activeTab === "loyalty" && (
        <div className="space-y-4">
          <div className="rounded-2xl border border-border bg-card p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-soft">
            <div>
              <h3 className="font-bold text-sm">Restaurant Loyalty Ledger (Audit Trail)</h3>
              <p className="text-xs text-muted-foreground">
                All points earned on settled bills, redeemed against payable balances, and reversals. Idempotent keys protect against double awarding.
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => refetchDetail()} className="text-xs gap-1.5">
              <RefreshCw className="h-3 w-3" /> Refresh Ledger
            </Button>
          </div>

          <div className="rounded-2xl border border-border bg-card overflow-hidden shadow-soft">
            <table className="w-full text-xs">
              <thead className="bg-secondary/50 text-muted-foreground border-b border-border text-left">
                <tr>
                  <th className="p-3">Timestamp</th>
                  <th className="p-3">Customer ID</th>
                  <th className="p-3">Transaction</th>
                  <th className="p-3">Eligible Bill Amount</th>
                  <th className="p-3">Idempotency Key</th>
                  <th className="p-3 text-right">Points Delta</th>
                  <th className="p-3 text-right">Balance</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {ledgerEntries.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-muted-foreground">
                      No global ledger entries currently loaded. Select a customer or trigger an order settlement to record points.
                    </td>
                  </tr>
                ) : (
                  ledgerEntries.map((entry) => (
                    <tr key={entry.id} className="hover:bg-secondary/20">
                      <td className="p-3 text-muted-foreground">
                        {new Date(entry.createdAtUtc).toLocaleString("en-IN")}
                      </td>
                      <td className="p-3 font-mono text-[11px]">{entry.customerId}</td>
                      <td className="p-3">
                        <Badge
                          variant="outline"
                          className={entry.transactionType === "Earn" ? "text-emerald-600" : "text-amber-600"}
                        >
                          {entry.transactionType}
                        </Badge>
                      </td>
                      <td className="p-3">{entry.eligibleSalesAmount ? formatINR(entry.eligibleSalesAmount) : "—"}</td>
                      <td className="p-3 font-mono text-[11px] text-muted-foreground">{entry.idempotencyKey}</td>
                      <td
                        className={`p-3 text-right font-bold ${
                          entry.points > 0 ? "text-emerald-600" : "text-amber-600"
                        }`}
                      >
                        {entry.points > 0 ? `+${entry.points}` : entry.points}
                      </td>
                      <td className="p-3 text-right font-semibold">{entry.balanceAfter} pts</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ========================================================
          TAB 3: RECEIPT-LINKED GUEST FEEDBACK
          ======================================================== */}
      {activeTab === "feedback" && (
        <div className="space-y-4">
          <div className="rounded-2xl border border-border bg-card p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-soft">
            <div>
              <h3 className="font-bold text-sm">Receipt-Linked Guest Feedback</h3>
              <p className="text-xs text-muted-foreground">
                Authentic feedback collected from digital receipts and customer table QR views.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                Avg Rating: 4.8 / 5.0 ★
              </span>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {feedbacks.map((fb) => (
              <div key={fb.id} className="rounded-2xl border border-border bg-card p-4 shadow-soft space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-bold text-sm">{fb.customerName || "Diner"}</span>
                      <Badge variant="outline" className="text-[10px]">
                        {fb.channel} {fb.tableNumber ? `· Table ${fb.tableNumber}` : ""}
                      </Badge>
                      <Badge
                        variant="secondary"
                        className={`text-[10px] ${
                          fb.status === "Reviewed"
                            ? "bg-emerald-100 text-emerald-800"
                            : fb.status === "Resolved"
                            ? "bg-blue-100 text-blue-800"
                            : "bg-amber-100 text-amber-800"
                        }`}
                      >
                        {fb.status}
                      </Badge>
                    </div>
                    <p className="text-[11px] text-muted-foreground mt-0.5">
                      {maskPhone(fb.customerPhone || "")} • {new Date(fb.createdAtUtc).toLocaleDateString("en-IN", {
                        day: "numeric",
                        month: "short",
                        year: "numeric",
                      })}
                    </p>
                  </div>

                  {/* Rating Stars */}
                  <div className="flex items-center gap-0.5 text-amber-500 font-bold text-sm">
                    {Array.from({ length: fb.overallRating }).map((_, i) => (
                      <Star key={i} className="h-4 w-4 fill-amber-400 text-amber-400" />
                    ))}
                    <span className="ml-1 text-xs">{fb.overallRating}.0</span>
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-2 bg-secondary/30 rounded-xl p-2 text-center text-[11px]">
                  <div>
                    <span className="text-muted-foreground">Food</span>
                    <p className="font-bold">{fb.foodRating} ★</p>
                  </div>
                  <div>
                    <span className="text-muted-foreground">Service</span>
                    <p className="font-bold">{fb.serviceRating} ★</p>
                  </div>
                  <div>
                    <span className="text-muted-foreground">Ambiance</span>
                    <p className="font-bold">{fb.ambianceRating} ★</p>
                  </div>
                </div>

                {fb.comments && (
                  <p className="text-xs bg-card p-2.5 rounded-xl border border-border/80 text-foreground">
                    "{fb.comments}"
                  </p>
                )}

                {/* Staff Actions / Notes */}
                {isAdminOrOwner && (
                  <div className="pt-2 border-t border-border/60 flex items-center justify-between gap-2">
                    <span className="text-[11px] text-muted-foreground italic">
                      {fb.staffNotes ? `Staff note: ${fb.staffNotes}` : "No manager note"}
                    </span>
                    {fb.status === "New" && (
                      <Button
                        size="sm"
                        variant="outline"
                        className="text-xs h-7 px-2.5"
                        onClick={() =>
                          updateFeedbackStatusMutation.mutate({
                            id: fb.id,
                            status: "Reviewed",
                            staffNotes: "Acknowledged by manager",
                          })
                        }
                      >
                        Mark Reviewed
                      </Button>
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ========================================================
          MODAL: CREATE NEW CUSTOMER
          ======================================================== */}
      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Register Customer Profile</DialogTitle>
            <DialogDescription>
              Create a tenant-scoped customer record for Tablo Heritage Restaurant.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3.5 py-2">
            <div>
              <label className="text-xs font-semibold">10-Digit Mobile Number *</label>
              <Input
                value={formPhone}
                onChange={(e) => setFormPhone(e.target.value)}
                placeholder="e.g. 9876543210"
                className="mt-1 text-xs"
              />
            </div>
            <div>
              <label className="text-xs font-semibold">Full Name *</label>
              <Input
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                placeholder="e.g. Vikram Malhotra"
                className="mt-1 text-xs"
              />
            </div>
            <div>
              <label className="text-xs font-semibold">Email Address (Optional)</label>
              <Input
                value={formEmail}
                onChange={(e) => setFormEmail(e.target.value)}
                placeholder="e.g. vikram@example.test"
                className="mt-1 text-xs"
              />
            </div>
            <div>
              <label className="text-xs font-semibold">Customer Tag</label>
              <Input
                value={formTags}
                onChange={(e) => setFormTags(e.target.value)}
                placeholder="VIP, Regular, Vegetarian, Corporate"
                className="mt-1 text-xs"
              />
            </div>
            <div>
              <label className="text-xs font-semibold">Dining Preferences &amp; Allergies</label>
              <Input
                value={formNotes}
                onChange={(e) => setFormNotes(e.target.value)}
                placeholder="e.g. Prefers window seating, no peanuts"
                className="mt-1 text-xs"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setIsCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              size="sm"
              disabled={!formPhone || !formName || createCustomerMutation.isPending}
              onClick={() => {
                createCustomerMutation.mutate({
                  phone: formPhone.trim(),
                  name: formName.trim(),
                  email: formEmail.trim() || undefined,
                  notes: formNotes.trim() || undefined,
                  tags: formTags.split(",").map((t) => t.trim()).filter(Boolean),
                  consentGiven: true,
                });
              }}
            >
              Save Customer
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ========================================================
          MODAL: REDEEM LOYALTY POINTS
          ======================================================== */}
      <Dialog open={isRedeemOpen} onOpenChange={setIsRedeemOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Redeem Loyalty Points</DialogTitle>
            <DialogDescription>
              Redeem accumulated loyalty points as a discount against a payable bill.
            </DialogDescription>
          </DialogHeader>

          {selectedCustomer && (
            <div className="space-y-4 py-2">
              <div className="rounded-xl bg-amber-500/10 border border-amber-500/20 p-3 flex items-center justify-between">
                <div>
                  <p className="text-xs font-bold text-amber-700">Available Points</p>
                  <p className="text-xl font-black text-amber-600">{selectedCustomer.loyaltyPointsBalance} pts</p>
                </div>
                <div className="text-right">
                  <p className="text-xs text-muted-foreground">Max Discount</p>
                  <p className="text-sm font-bold text-foreground">{formatINR(selectedCustomer.loyaltyPointsBalance)}</p>
                </div>
              </div>

              <div>
                <label className="text-xs font-semibold">Points to Redeem</label>
                <Input
                  type="number"
                  min={1}
                  max={selectedCustomer.loyaltyPointsBalance}
                  value={redeemPoints}
                  onChange={(e) => setRedeemPoints(Math.min(selectedCustomer.loyaltyPointsBalance, Math.max(1, Number(e.target.value))))}
                  className="mt-1 text-sm font-bold"
                />
                <p className="text-[11px] text-muted-foreground mt-1">
                  1 Point = ₹1.00 INR discount. This redemption will deduct {redeemPoints} points.
                </p>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setIsRedeemOpen(false)}>
              Cancel
            </Button>
            <Button
              size="sm"
              disabled={
                !selectedCustomer ||
                redeemPoints <= 0 ||
                redeemPoints > (selectedCustomer?.loyaltyPointsBalance || 0) ||
                redeemPointsMutation.isPending
              }
              onClick={() => {
                if (!selectedCustomer) return;
                redeemPointsMutation.mutate({
                  customerId: selectedCustomer.id,
                  billId: "bill-table-1",
                  pointsToRedeem: redeemPoints,
                });
              }}
            >
              Confirm Redemption
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
