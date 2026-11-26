import {
  LayoutDashboard,
  UtensilsCrossed,
  ChefHat,
  Receipt,
  QrCode,
  Users,
  BarChart3,
  Package,
  ShoppingCart,
  ShoppingBag,
  Boxes,
  ArrowLeftRight,
  ShieldCheck,
  Building2,
  FileText,
  Sliders,
  History,
  CreditCard,
  Store,
  Factory,
  ClipboardCheck,
  FileSpreadsheet,
  LifeBuoy,
  Inbox,
  type LucideIcon,
} from "lucide-react";

export const CAPABILITIES = {
  // Basic
  QrOrdering: "qr_ordering",
  KitchenDisplay: "kitchen_display",
  StandardBilling: "standard_billing",
  BasicReports: "basic_reports",
  MenuManagement: "menu_management",
  TablesAndQr: "tables_qr",

  // Plus
  StaffOrdering: "staff_ordering",
  DiningGroups: "dining_groups",
  SplitBilling: "split_billing",
  MixedPayments: "mixed_payments",
  TableTransfers: "table_transfers",
  AdvancedPaymentReports: "advanced_payment_reports",
  ThermalPrinting: "thermal_printing",
  MultipleKitchenStations: "multiple_stations",
  ParcelCounter: "parcel_counter",
  HoldFireCourses: "hold_fire_courses",
  ItemModifiers: "item_modifiers",
  FreeRefills: "free_refills",
  ManagerWasteCancellation: "waste_cancellation",
  ScheduledPickup: "scheduled_pickup",
  HybridDineInParcel: "hybrid_dinein_parcel",
  CarSideOrdering: "carside_ordering",
  DynamicUpiQr: "dynamic_upi_qr",

  // Premium
  InventoryCore: "inventory_core",
  RecipesCosting: "recipes_costing",
  Stocktakes: "stocktakes",
  Purchasing: "purchasing",
  LotsAndExpiry: "lots_expiry",
  Preparations: "preparations",
  OutletTransfers: "outlet_transfers",
  StockProfitReports: "stock_profit_reports",
  AutoAvailability: "auto_availability",
} as const;

export type CapabilityKey = (typeof CAPABILITIES)[keyof typeof CAPABILITIES];

export interface NavItem {
  id: string;
  label: string;
  to: string;
  search?: Record<string, string>;
  tabParam?: string;
  isDefault?: boolean;
  icon: LucideIcon;
  section: "main" | "operations" | "management" | "inventory" | "analytics";
  requiredRoles?: ("Admin" | "Owner" | "Waiter" | "Kitchen" | "Cashier")[];
  requiredCapabilities?: string[];
  adminOnly?: boolean;
}

export const MENU_SECTIONS: NavItem["section"][] = [
  "main",
  "operations",
  "management",
  "inventory",
  "analytics",
];

export const MENU_SECTION_LABELS: Record<NavItem["section"], string> = {
  main: "Overview",
  operations: "Daily Operations",
  management: "Restaurant Management",
  inventory: "Inventory & Stock",
  analytics: "Reports & Analytics",
};

const CAPABILITY_MIN_PLAN: Record<string, "Basic" | "Plus" | "Premium"> = {
  [CAPABILITIES.QrOrdering]: "Basic",
  [CAPABILITIES.KitchenDisplay]: "Basic",
  [CAPABILITIES.StandardBilling]: "Basic",
  [CAPABILITIES.BasicReports]: "Basic",
  [CAPABILITIES.MenuManagement]: "Basic",
  [CAPABILITIES.TablesAndQr]: "Basic",
  [CAPABILITIES.StaffOrdering]: "Plus",
  [CAPABILITIES.DiningGroups]: "Plus",
  [CAPABILITIES.SplitBilling]: "Plus",
  [CAPABILITIES.MixedPayments]: "Plus",
  [CAPABILITIES.TableTransfers]: "Plus",
  [CAPABILITIES.AdvancedPaymentReports]: "Plus",
  [CAPABILITIES.ThermalPrinting]: "Plus",
  [CAPABILITIES.ParcelCounter]: "Plus",
  [CAPABILITIES.HoldFireCourses]: "Plus",
  [CAPABILITIES.ItemModifiers]: "Plus",
  [CAPABILITIES.FreeRefills]: "Plus",
  [CAPABILITIES.ManagerWasteCancellation]: "Plus",
  [CAPABILITIES.ScheduledPickup]: "Plus",
  [CAPABILITIES.HybridDineInParcel]: "Plus",
  [CAPABILITIES.CarSideOrdering]: "Plus",
  [CAPABILITIES.DynamicUpiQr]: "Plus",
  [CAPABILITIES.MultipleKitchenStations]: "Premium",
  [CAPABILITIES.InventoryCore]: "Premium",
  [CAPABILITIES.RecipesCosting]: "Premium",
  [CAPABILITIES.Stocktakes]: "Premium",
  [CAPABILITIES.Purchasing]: "Premium",
  [CAPABILITIES.LotsAndExpiry]: "Premium",
  [CAPABILITIES.Preparations]: "Premium",
  [CAPABILITIES.OutletTransfers]: "Premium",
  [CAPABILITIES.StockProfitReports]: "Premium",
  [CAPABILITIES.AutoAvailability]: "Premium",
};

export const ADMIN_ONLY_MENU_IDS = new Set([
  "staff-management",
  "staff-menu-access",
  "support",
]);

export const RECOVERY_MENU_IDS = new Set([
  "staff-management",
  "staff-menu-access",
]);

export function isAdminRole(role?: string | null): boolean {
  const lower = (role || "").toLowerCase();
  return lower === "admin" || lower === "owner";
}

export function isMenuAssignableToRole(item: Pick<NavItem, "id" | "adminOnly">, role: string): boolean {
  return (!item.adminOnly && !ADMIN_ONLY_MENU_IDS.has(item.id)) || isAdminRole(role);
}

export function getAssignableMenuItems(role: string): NavItem[] {
  return NAVIGATION_REGISTRY.filter((item) => isMenuAssignableToRole(item, role));
}

export function getMenuRequiredPlanLabel(item: Pick<NavItem, "requiredCapabilities">): string | null {
  if (!item.requiredCapabilities?.length) return null;
  const ranks = { Basic: 1, Plus: 2, Premium: 3 } as const;
  let lowest: "Basic" | "Plus" | "Premium" = "Premium";
  let lowestRank = 99;
  for (const cap of item.requiredCapabilities) {
    const plan = CAPABILITY_MIN_PLAN[cap] ?? "Premium";
    if (ranks[plan] < lowestRank) {
      lowestRank = ranks[plan];
      lowest = plan;
    }
  }
  return `Requires ${lowest} plan`;
}

export function isNavigationItemActive(
  item: Pick<NavItem, "to" | "tabParam" | "isDefault">,
  pathname: string,
  tab?: string,
) {
  return pathname === item.to && (!item.tabParam || item.tabParam === tab || (!tab && !!item.isDefault));
}

export function getDashboardTab(
  tab?: string,
  role?: string,
  allowedMenuIds?: string[] | null,
): "floor" | "order" | "kitchen" | "billing" {
  if (tab === "floor" || tab === "order" || tab === "kitchen" || tab === "billing") {
    return tab;
  }
  if (allowedMenuIds !== undefined && role) {
    const allowed = new Set(getEmployeeNavItems(role, allowedMenuIds).map((item) => item.id));
    if (role === "Kitchen" && allowed.has("kitchen-queue")) return "kitchen";
    if (allowed.has("orders-servicedesk")) return "floor";
    if (allowed.has("staff-order-taking")) return "order";
    if (allowed.has("kitchen-queue")) return "kitchen";
    if (allowed.has("table-billing")) return "billing";
  }
  if (role === "Kitchen") return "kitchen";
  return "floor";
}

export const NAVIGATION_REGISTRY: NavItem[] = [
  // 1. MAIN / DASHBOARD
  {
    id: "overview",
    label: "Overview",
    to: "/",
    icon: LayoutDashboard,
    section: "main",
    requiredRoles: ["Admin", "Owner", "Waiter", "Kitchen"],
  },

  {
    id: "cashier-billing",
    label: "Cashier",
    to: "/cashier",
    icon: CreditCard,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Cashier"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  // 2. OPERATIONS (Floor, Orders, Kitchen, Billing & Parcel)
  {
    id: "orders-servicedesk",
    label: "Floor & Orders",
    to: "/dashboard",
    search: { tab: "floor" },
    tabParam: "floor",
    isDefault: true,
    icon: UtensilsCrossed,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Waiter"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  {
    id: "staff-order-taking",
    label: "Staff Order Taking",
    to: "/dashboard",
    search: { tab: "order" },
    tabParam: "order",
    icon: ShoppingCart,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Waiter"],
    requiredCapabilities: [CAPABILITIES.StaffOrdering, CAPABILITIES.StandardBilling],
  },
  {
    id: "kitchen-queue",
    label: "Kitchen Queue",
    to: "/dashboard",
    search: { tab: "kitchen" },
    tabParam: "kitchen",
    icon: ChefHat,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Kitchen"],
    requiredCapabilities: [CAPABILITIES.KitchenDisplay],
  },
  {
    id: "table-billing",
    label: "Billing & Settlement",
    to: "/dashboard",
    search: { tab: "billing" },
    tabParam: "billing",
    icon: CreditCard,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Waiter"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  {
    id: "parcel-counter",
    label: "Parcel Counter",
    to: "/admin",
    search: { tab: "parcel" },
    tabParam: "parcel",
    icon: Package,
    section: "operations",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },

  // 3. RESTAURANT MANAGEMENT (Menu, Tables, Staff, CRM, Storefront, Marketplace)
  {
    id: "menu-management",
    label: "Menu & Categories",
    to: "/admin",
    search: { tab: "menu" },
    tabParam: "menu",
    isDefault: true,
    icon: Receipt,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.MenuManagement],
  },
  {
    id: "tables-qr",
    label: "Tables & QR Codes",
    to: "/admin",
    search: { tab: "tables" },
    tabParam: "tables",
    icon: QrCode,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.TablesAndQr],
  },
  {
    id: "staff-management",
    label: "Staff & Roles",
    to: "/staff",
    icon: Users,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    adminOnly: true,
  },
  {
    id: "staff-menu-access",
    label: "Staff Menu Access",
    to: "/menu-access",
    icon: ShieldCheck,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    adminOnly: true,
  },
  {
    id: "crm-loyalty",
    label: "Customer CRM & Loyalty",
    to: "/admin",
    search: { tab: "crm" },
    tabParam: "crm",
    icon: Users,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  {
    id: "storefront-settings",
    label: "Storefront & Outlets",
    to: "/admin",
    search: { tab: "storefront" },
    tabParam: "storefront",
    icon: Store,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  {
    id: "marketplace-automation",
    label: "Marketplace Orders",
    to: "/admin",
    search: { tab: "marketplace" },
    tabParam: "marketplace",
    icon: ShoppingBag,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StandardBilling],
  },
  {
    id: "support",
    label: "Support",
    to: "/support",
    icon: LifeBuoy,
    section: "management",
    requiredRoles: ["Admin", "Owner"],
    adminOnly: true,
  },

  // 4. INVENTORY & SUPPLY (ERP)
  {
    id: "inventory-stock",
    label: "Ingredients & Stock",
    to: "/inventory",
    search: { tab: "ingredients" },
    tabParam: "ingredients",
    isDefault: true,
    icon: Package,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.InventoryCore],
  },
  {
    id: "inventory-recipes",
    label: "Recipes & Costing",
    to: "/inventory",
    search: { tab: "recipes" },
    tabParam: "recipes",
    icon: ChefHat,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.RecipesCosting],
  },
  {
    id: "purchasing-orders",
    label: "Purchasing & POs",
    to: "/inventory",
    search: { tab: "purchasing" },
    tabParam: "purchasing",
    icon: ShoppingCart,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Purchasing],
  },
  {
    id: "inventory-suppliers",
    label: "Suppliers",
    to: "/inventory",
    search: { tab: "suppliers" },
    tabParam: "suppliers",
    icon: Building2,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Purchasing],
  },
  {
    id: "inventory-preparations",
    label: "Preparations",
    to: "/inventory",
    search: { tab: "preparations" },
    tabParam: "preparations",
    icon: ChefHat,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Preparations],
  },
  {
    id: "inventory-movements",
    label: "Stock Movements",
    to: "/inventory",
    search: { tab: "movements" },
    tabParam: "movements",
    icon: History,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.InventoryCore],
  },
  {
    id: "lots-expiry",
    label: "Lots & Expiry",
    to: "/inventory",
    search: { tab: "lots" },
    tabParam: "lots",
    icon: Boxes,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.LotsAndExpiry],
  },
  {
    id: "outlet-transfers",
    label: "Outlet Transfers",
    to: "/inventory",
    search: { tab: "transfers" },
    tabParam: "transfers",
    icon: ArrowLeftRight,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.OutletTransfers],
  },
  {
    id: "inventory-stocktakes",
    label: "Physical Stocktakes",
    to: "/inventory",
    search: { tab: "stocktakes" },
    tabParam: "stocktakes",
    icon: ClipboardCheck,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Stocktakes],
  },
  {
    id: "central-kitchen",
    label: "Central Kitchen",
    to: "/inventory",
    search: { tab: "central-kitchen" },
    tabParam: "central-kitchen",
    icon: Factory,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Preparations, CAPABILITIES.OutletTransfers],
  },

  // 5. ANALYTICS & REPORTS
  {
    id: "financial-reports",
    label: "Payment Reports",
    to: "/reports",
    icon: BarChart3,
    section: "analytics",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.BasicReports],
  },
  {
    id: "stock-valuation-report",
    label: "Stock Valuation Report",
    to: "/inventory",
    search: { tab: "stock-report" },
    tabParam: "stock-report",
    icon: FileSpreadsheet,
    section: "analytics",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StockProfitReports],
  },
  {
    id: "gross-profit-report",
    label: "Sales & Gross Profit",
    to: "/inventory",
    search: { tab: "gross-profit" },
    tabParam: "gross-profit",
    icon: BarChart3,
    section: "analytics",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.StockProfitReports],
  },
];

export interface SuperAdminNavItem {
  id: string;
  label: string;
  tab: "overview" | "tenants" | "plans" | "subscriptions" | "audit" | "inbox";
  to: string;
  search: { tab: string };
  icon: LucideIcon;
  section: "platform";
}

export const SUPER_ADMIN_NAVIGATION: SuperAdminNavItem[] = [
  {
    id: "sa-overview",
    label: "Platform Overview",
    tab: "overview",
    to: "/super-admin",
    search: { tab: "overview" },
    icon: LayoutDashboard,
    section: "platform",
  },
  {
    id: "sa-support-inbox",
    label: "Support Inbox",
    tab: "inbox",
    to: "/super-admin",
    search: { tab: "inbox" },
    icon: Inbox,
    section: "platform",
  },
  {
    id: "sa-tenants",
    label: "Restaurants & Tenants",
    tab: "tenants",
    to: "/super-admin",
    search: { tab: "tenants" },
    icon: Building2,
    section: "platform",
  },
  {
    id: "sa-plans",
    label: "Plans & Features",
    tab: "plans",
    to: "/super-admin",
    search: { tab: "plans" },
    icon: Sliders,
    section: "platform",
  },
  {
    id: "sa-subscriptions",
    label: "Tenant Subscriptions",
    tab: "subscriptions",
    to: "/super-admin",
    search: { tab: "subscriptions" },
    icon: ShieldCheck,
    section: "platform",
  },
  {
    id: "sa-audit",
    label: "Platform Audit Log",
    tab: "audit",
    to: "/super-admin",
    search: { tab: "audit" },
    icon: History,
    section: "platform",
  },
];

export function getVisibleNavItems(role: string, capabilities: string[]): NavItem[] {
  const capSet = new HashSetCI(capabilities);

  return NAVIGATION_REGISTRY.filter((item) => {
    // Check role restriction
    if (item.requiredRoles && !item.requiredRoles.some((r) => r.toLowerCase() === role.toLowerCase())) {
      return false;
    }

    // Check capability restriction
    if (item.requiredCapabilities && !item.requiredCapabilities.some((c) => capSet.has(c))) {
      return false;
    }

    return true;
  });
}

class HashSetCI {
  private set = new Set<string>();
  constructor(items: string[]) {
    for (const item of items) {
      this.set.add(item.toLowerCase());
    }
  }
  has(item: string) {
    return this.set.has(item.toLowerCase());
  }
}

export function getEmployeeNavItems(role: string, allowedMenuIds: string[] | null): NavItem[] {
  if (allowedMenuIds === null) {
    return NAVIGATION_REGISTRY.filter(
      (item) =>
        !item.requiredRoles ||
        item.requiredRoles.some((r) => r.toLowerCase() === role.toLowerCase()),
    );
  }
  const allowed = new Set(allowedMenuIds);
  const adminRecovery = isAdminRole(role);
  return NAVIGATION_REGISTRY.filter(
    (item) =>
      (adminRecovery && RECOVERY_MENU_IDS.has(item.id)) ||
      (isMenuAssignableToRole(item, role) && allowed.has(item.id)),
  );
}

export function getRouteMenuItem(
  pathname: string,
  tab: string | undefined,
  role: string,
  allowedMenuIds?: string[] | null,
): NavItem | undefined {
  const resolvedTab =
    pathname === "/dashboard" ? getDashboardTab(tab, role, allowedMenuIds) : tab;
  // Unknown tabs must never inherit access from a default tab.
  if (pathname === "/dashboard" && tab && !["floor", "order", "kitchen", "billing"].includes(tab)) return undefined;
  return NAVIGATION_REGISTRY.find(item => isNavigationItemActive(item, pathname, resolvedTab));
}
