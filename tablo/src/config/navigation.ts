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
  Boxes,
  ArrowLeftRight,
  ShieldCheck,
  Building2,
  FileText,
  Sliders,
  History,
  type LucideIcon
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

export type CapabilityKey = typeof CAPABILITIES[keyof typeof CAPABILITIES];

export interface NavItem {
  id: string;
  label: string;
  to: string;
  icon: LucideIcon;
  section: "main" | "operations" | "inventory" | "analytics" | "admin";
  requiredRoles?: ("Admin" | "Owner" | "Waiter" | "Kitchen")[];
  requiredCapabilities?: string[];
  tabParam?: string;
}

export const NAVIGATION_REGISTRY: NavItem[] = [
  // Main
  {
    id: "overview",
    label: "Overview",
    to: "/",
    icon: LayoutDashboard,
    section: "main"
  },

  // Operations
  {
    id: "orders-servicedesk",
    label: "Floor & Orders",
    to: "/dashboard",
    icon: UtensilsCrossed,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Waiter"],
    requiredCapabilities: [CAPABILITIES.StandardBilling]
  },
  {
    id: "kitchen-queue",
    label: "Kitchen Queue",
    to: "/dashboard",
    icon: ChefHat,
    section: "operations",
    requiredRoles: ["Admin", "Owner", "Kitchen"],
    requiredCapabilities: [CAPABILITIES.KitchenDisplay]
  },
  {
    id: "menu-management",
    label: "Menu & Categories",
    to: "/admin",
    icon: Receipt,
    section: "operations",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.MenuManagement]
  },
  {
    id: "tables-qr",
    label: "Tables & QR Codes",
    to: "/admin",
    icon: QrCode,
    section: "operations",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.TablesAndQr]
  },

  // Inventory & Purchasing (Premium tier)
  {
    id: "inventory-stock",
    label: "Stock & Recipes",
    to: "/inventory",
    icon: Package,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.InventoryCore]
  },
  {
    id: "purchasing-orders",
    label: "Purchasing & POs",
    to: "/inventory",
    icon: ShoppingCart,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.Purchasing]
  },
  {
    id: "lots-expiry",
    label: "Lots & Expiry",
    to: "/inventory",
    icon: Boxes,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.LotsAndExpiry]
  },
  {
    id: "outlet-transfers",
    label: "Outlet Transfers",
    to: "/inventory",
    icon: ArrowLeftRight,
    section: "inventory",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.OutletTransfers]
  },

  // Analytics & Reports
  {
    id: "financial-reports",
    label: "Payment Reports",
    to: "/reports",
    icon: BarChart3,
    section: "analytics",
    requiredRoles: ["Admin", "Owner"],
    requiredCapabilities: [CAPABILITIES.BasicReports]
  },

  // Staff & Administration
  {
    id: "staff-management",
    label: "Staff & Roles",
    to: "/staff",
    icon: Users,
    section: "admin",
    requiredRoles: ["Admin", "Owner"]
  }
];

export interface SuperAdminNavItem {
  id: string;
  label: string;
  tab: "overview" | "tenants" | "plans" | "subscriptions" | "audit";
  icon: LucideIcon;
  badge?: string;
}

export const SUPER_ADMIN_NAVIGATION: SuperAdminNavItem[] = [
  { id: "sa-overview", label: "Overview", tab: "overview", icon: LayoutDashboard },
  { id: "sa-tenants", label: "Restaurants", tab: "tenants", icon: Building2 },
  { id: "sa-plans", label: "Plans & Features", tab: "plans", icon: Sliders },
  { id: "sa-subscriptions", label: "Subscriptions", tab: "subscriptions", icon: ShieldCheck },
  { id: "sa-audit", label: "Audit Log", tab: "audit", icon: History }
];

export function getVisibleNavItems(role: string, capabilities: string[]): NavItem[] {
  const capSet = new HashSetCI(capabilities);

  return NAVIGATION_REGISTRY.filter(item => {
    // Check role restriction
    if (item.requiredRoles && !item.requiredRoles.some(r => r.toLowerCase() === role.toLowerCase())) {
      return false;
    }

    // Check capability restriction
    if (item.requiredCapabilities && !item.requiredCapabilities.some(c => capSet.has(c))) {
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
