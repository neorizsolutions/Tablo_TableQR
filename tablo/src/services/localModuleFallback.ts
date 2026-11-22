// Local Fallback Handler for Newly Implemented Modules
// Used seamlessly when the baseline backend returns 404 (due to Windows Application Control 0x800711C7)

export interface PlanItem {
  id: string;
  code: string;
  name: string;
  description: string;
  monthlyPriceInr: number;
  maxOutlets: number;
  maxTables: number;
  maxKitchenStations?: number;
  features: string[];
  isActive: boolean;
  displayOrder: number;
}

export const CANONICAL_BASIC_FEATURES = [
  "qr_ordering",
  "kitchen_display",
  "standard_billing",
  "basic_reports",
  "menu_management",
  "tables_qr",
  // Legacy aliases
  "qr-menu",
  "customer-ordering",
  "kitchen-display",
  "order-status-tracking",
  "bill-requests",
  "service-requests",
  "sales-reports"
];

export const CANONICAL_PLUS_FEATURES = [
  ...CANONICAL_BASIC_FEATURES,
  "staff_ordering",
  "dining_groups",
  "split_billing",
  "mixed_payments",
  "table_transfers",
  "advanced_payment_reports",
  "thermal_printing",
  "parcel_counter",
  "hold_fire_courses",
  "item_modifiers",
  "free_refills",
  "waste_cancellation",
  "scheduled_pickup",
  "hybrid_dinein_parcel",
  "carside_ordering",
  "dynamic_upi_qr",
  // Legacy aliases
  "staff-ordering",
  "dining-groups",
  "split-bills",
  "mixed-payments",
  "table-transfers",
  "payment-reports",
  "thermal-printing",
  "parcel-counter",
  "hold-fire",
  "priced-modifiers",
  "free-refills",
  "cancellation-waste",
  "scheduled-pickup",
  "hybrid-dine-in-parcel",
  "car-side-ordering",
  "dynamic-upi-qr"
];

export const CANONICAL_PREMIUM_FEATURES = [
  ...CANONICAL_PLUS_FEATURES,
  "multiple_stations",
  "inventory_core",
  "recipes_costing",
  "stocktakes",
  "purchasing",
  "lots_expiry",
  "preparations",
  "outlet_transfers",
  "stock_profit_reports",
  "auto_availability",
  // Legacy aliases
  "inventory-management",
  "recipe-costing",
  "supplier-purchasing",
  "batch-tracking",
  "inventory-reports",
  "outlet-stock-transfers"
];

let localPlans: PlanItem[] = [
  {
    id: "44444444-0000-0000-0000-000000000001",
    code: "basic",
    name: "Basic Plan",
    description: "QR menu and ordering, kitchen display, standard billing.",
    monthlyPriceInr: 999,
    maxOutlets: 1,
    maxTables: 15,
    maxKitchenStations: 1,
    features: [...CANONICAL_BASIC_FEATURES],
    isActive: true,
    displayOrder: 1
  },
  {
    id: "44444444-0000-0000-0000-000000000002",
    code: "plus",
    name: "Plus Plan",
    description: "Basic plus staff ordering, dining groups, split bills, mixed payments, table transfers.",
    monthlyPriceInr: 2499,
    maxOutlets: 3,
    maxTables: 40,
    maxKitchenStations: 2,
    features: [...CANONICAL_PLUS_FEATURES],
    isActive: true,
    displayOrder: 2
  },
  {
    id: "44444444-0000-0000-0000-000000000003",
    code: "premium",
    name: "Premium Plan",
    description: "Complete enterprise operations: inventory, production controls, thermal printing, parcel counter, car-side, dynamic UPI QR.",
    monthlyPriceInr: 4999,
    maxOutlets: 10,
    maxTables: 100,
    maxKitchenStations: 5,
    features: [...CANONICAL_PREMIUM_FEATURES],
    isActive: true,
    displayOrder: 3
  }
];


let localProductionBatches: any[] = [];
let localStockTransfers: any[] = [];
let localStorefrontOrders = new Map<string, any>();
let storefrontOrderSeq = 1000;
let storefrontTokenSeq = 0;

export interface FallbackOutletSettings {
  outletId: string;
  restaurantId: string;
  name: string;
  address: string;
  phone: string;
  isOpen: boolean;
  isPublished: boolean;
  timezone: string;
  openingTime: string; // e.g. "11:00"
  closingTime: string; // e.g. "23:30"
  isOvernight: boolean;
  leadTimeMinutes: number;
  minOrderAmount: number;
  deliveryBaseFee: number;
  freeDeliveryAboveAmount: number;
  isTemporarilyClosed: boolean;
  closureReason?: string;
}

let localOutlets: FallbackOutletSettings[] = [
  {
    outletId: "22222222-0000-0000-0000-000000000002",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    name: "Main Dining & Counter Outlet",
    address: "Block B, Connaught Place, New Delhi",
    phone: "+91 98765 43210",
    isOpen: true,
    isPublished: true,
    timezone: "Asia/Kolkata",
    openingTime: "00:00",
    closingTime: "23:59",
    isOvernight: false,
    leadTimeMinutes: 25,
    minOrderAmount: 200,
    deliveryBaseFee: 40,
    freeDeliveryAboveAmount: 799,
    isTemporarilyClosed: false,
  },
  {
    outletId: "22222222-0000-0000-0000-000000000001",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    name: "Tablo Heritage Restaurant",
    address: "Block B, Connaught Place, New Delhi",
    phone: "+91 98765 43210",
    isOpen: true,
    isPublished: true,
    timezone: "Asia/Kolkata",
    openingTime: "00:00",
    closingTime: "23:59",
    isOvernight: false,
    leadTimeMinutes: 25,
    minOrderAmount: 200,
    deliveryBaseFee: 40,
    freeDeliveryAboveAmount: 799,
    isTemporarilyClosed: false,
  },
  {
    outletId: "22222222-0000-0000-0000-000000000010",
    restaurantId: "22222222-0000-0000-0000-000000000009",
    name: "Coastal Cafe Main",
    address: "12 Beach Road, Mangaluru",
    phone: "+91 98765 00009",
    isOpen: true,
    isPublished: true,
    timezone: "Asia/Kolkata",
    openingTime: "10:00",
    closingTime: "23:00",
    isOvernight: false,
    leadTimeMinutes: 20,
    minOrderAmount: 150,
    deliveryBaseFee: 35,
    freeDeliveryAboveAmount: 699,
    isTemporarilyClosed: false,
  },
  {
    outletId: "22222222-0000-0000-0000-000000000009",
    restaurantId: "22222222-0000-0000-0000-000000000009",
    name: "Tablo Coastal Cafe",
    address: "12 Beach Road, Mangaluru",
    phone: "+91 98765 00009",
    isOpen: true,
    isPublished: true,
    timezone: "Asia/Kolkata",
    openingTime: "10:00",
    closingTime: "23:00",
    isOvernight: false,
    leadTimeMinutes: 20,
    minOrderAmount: 150,
    deliveryBaseFee: 35,
    freeDeliveryAboveAmount: 699,
    isTemporarilyClosed: false,
  },
];

function getAuthClaims(options?: RequestInit): { isAuthenticated: boolean; role?: string; restaurantId?: string; userId?: string } {
  let authHeader = "";
  if (options?.headers) {
    if (typeof (options.headers as any).get === "function") {
      authHeader = (options.headers as any).get("Authorization") || "";
    } else {
      const headers = options.headers as Record<string, string>;
      authHeader = headers["Authorization"] || headers["authorization"] || "";
    }
  }
  if (!authHeader && typeof localStorage !== "undefined") {
    const token = localStorage.getItem("tablo_token");
    if (token) authHeader = `Bearer ${token}`;
  }

  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    return { isAuthenticated: false };
  }

  const token = authHeader.substring(7);
  try {
    const parts = token.split(".");
    if (parts.length >= 2) {
      const b64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const json = typeof Buffer !== "undefined"
        ? Buffer.from(b64, "base64").toString("utf8")
        : atob(b64);
      const payload = JSON.parse(json);
      return {
        isAuthenticated: true,
        role: payload.role || payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"],
        restaurantId: payload.RestaurantId,
        userId: payload.sub,
      };
    }
  } catch {}
  return { isAuthenticated: false };
}

function evaluateOutletOpen(outlet: FallbackOutletSettings, dateUtc: Date = new Date()): { isOpenNow: boolean; message: string } {
  if (outlet.isTemporarilyClosed) {
    return { isOpenNow: false, message: `Outlet is temporarily closed: ${outlet.closureReason || "Please check back later."}` };
  }
  if (!outlet.isOpen) {
    return { isOpenNow: false, message: "Outlet is currently not accepting orders." };
  }

  // Convert UTC date to outlet's timezone (IST is UTC + 5:30)
  const istOffsetMs = 5.5 * 60 * 60 * 1000;
  const localDate = new Date(dateUtc.getTime() + istOffsetMs);
  const hours = localDate.getUTCHours();
  const minutes = localDate.getUTCMinutes();
  const currentMinutes = hours * 60 + minutes;

  const [openH, openM] = outlet.openingTime.split(":").map(Number);
  const [closeH, closeM] = outlet.closingTime.split(":").map(Number);
  const openMinutes = openH * 60 + openM;
  const closeMinutes = closeH * 60 + closeM;

  let open = false;
  if (closeMinutes > openMinutes) {
    // Normal daytime (e.g. 11:00 to 23:30)
    open = currentMinutes >= openMinutes && currentMinutes <= closeMinutes;
  } else {
    // Overnight (e.g. 19:00 to 02:00)
    open = currentMinutes >= openMinutes || currentMinutes <= closeMinutes;
  }

  if (!open) {
    return { isOpenNow: false, message: `Outlet is closed. Operating hours are ${outlet.openingTime} - ${outlet.closingTime} (${outlet.timezone}).` };
  }
  return { isOpenNow: true, message: "Open" };
}

let localTenants: any[] = [
  {
    id: "22222222-0000-0000-0000-000000000001",
    name: "Tablo Heritage Restaurant",
    description: "Fine dining, live counter, takeaway & car-side service.",
    address: "42 Heritage Lane, MG Road, Bengaluru",
    contactEmail: "contact@tablodemo.com",
    contactPhone: "+91 98765 43210",
    isActive: true,
    planCode: "premium",
    planName: "Premium Plan",
    subscriptionStatus: "Active",
    createdAt: new Date(Date.now() - 30 * 86400000).toISOString()
  }
];


let localSubscriptions: any[] = [
  {
    id: "44444444-0000-0000-0000-000000000004",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    restaurantName: "Tablo Heritage Restaurant",
    planId: "44444444-0000-0000-0000-000000000003",
    planCode: "premium",
    planName: "Premium Plan",
    status: "Active",
    startDateUtc: new Date(Date.now() - 30 * 86400000).toISOString(),
    endDateUtc: new Date(Date.now() + 335 * 86400000).toISOString(),
    autoRenew: true,
    notes: "Full demo subscription with all 28 capabilities active.",
    enabledFeatures: [...CANONICAL_PREMIUM_FEATURES]
  }
];


let localAuditLogs: any[] = [
  {
    id: "1",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    restaurantName: "Tablo Heritage Restaurant",
    actorEmail: "superadmin@example.test",
    action: "AssignedPlan",
    fromPlanCode: undefined,
    toPlanCode: "premium",
    fromStatus: undefined,
    toStatus: "Active",
    reason: "Initial provision of enterprise premium plan with full production, parcel and thermal printing entitlements",
    timestampUtc: new Date(Date.now() - 3600000).toISOString()
  }
];


// Service Desk Local State
let localServiceTables = [
  { id: "77777777-0000-0000-0000-000000000001", tableNumber: 1, isActive: true, status: "Occupied" as const, activeOrdersCount: 2, openBillTotal: 1533 },
  { id: "77777777-0000-0000-0000-000000000002", tableNumber: 2, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
  { id: "77777777-0000-0000-0000-000000000003", tableNumber: 3, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
  { id: "77777777-0000-0000-0000-000000000004", tableNumber: 4, isActive: true, status: "Occupied" as const, activeOrdersCount: 1, openBillTotal: 714 },
  { id: "77777777-0000-0000-0000-000000000005", tableNumber: 5, isActive: true, status: "Bill Requested" as const, activeOrdersCount: 1, openBillTotal: 980 },
  { id: "77777777-0000-0000-0000-000000000006", tableNumber: 6, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
  { id: "77777777-0000-0000-0000-000000000007", tableNumber: 7, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
  { id: "77777777-0000-0000-0000-000000000008", tableNumber: 8, isActive: true, status: "Occupied" as const, activeOrdersCount: 1, openBillTotal: 450 },
  { id: "77777777-0000-0000-0000-000000000009", tableNumber: 9, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
  { id: "77777777-0000-0000-0000-000000000010", tableNumber: 10, isActive: true, status: "Available" as const, activeOrdersCount: 0, openBillTotal: 0 },
];

let localDiningGroups: { id: string; tableId: string; label: string; createdAtUtc: string }[] = [
  { id: "group-4a", tableId: "77777777-0000-0000-0000-000000000004", label: "A", createdAtUtc: new Date().toISOString() },
  { id: "group-4b", tableId: "77777777-0000-0000-0000-000000000004", label: "B", createdAtUtc: new Date().toISOString() },
];

let localServiceBills: any[] = [
  {
    id: "bill-table-1",
    tableId: "77777777-0000-0000-0000-000000000001",
    tableNumber: 1,
    status: "Open",
    revision: "rev-1",
    createdAtUtc: new Date().toISOString(),
    settledAtUtc: null,
    paymentMethod: null,
    subtotal: 1460,
    packagingCharge: 0,
    tax: 73,
    discount: 0,
    totalAmount: 1533,
    paidAmount: 0,
    remainingAmount: 1533,
    canSettle: true,
    orders: [
      {
        id: "ORD-2041",
        orderNumber: 2041,
        status: "preparing",
        totalAmount: 1533,
        items: [
          { id: "item1", name: "Grilled Atlantic Salmon", quantity: 2, unitPrice: 580, releaseState: "Fired" },
          { id: "item2", name: "Mint Lemonade", quantity: 2, unitPrice: 150, releaseState: "Fired" }
        ]
      }
    ]
  },
  {
    id: "bill-table-4",
    tableId: "77777777-0000-0000-0000-000000000004",
    tableNumber: 4,
    diningGroupId: "group-4a",
    groupLabel: "A",
    status: "Open",
    revision: "rev-4a",
    createdAtUtc: new Date().toISOString(),
    settledAtUtc: null,
    paymentMethod: null,
    subtotal: 680,
    packagingCharge: 0,
    tax: 34,
    discount: 0,
    totalAmount: 714,
    paidAmount: 0,
    remainingAmount: 714,
    canSettle: true,
    orders: [
      {
        id: "ORD-2042",
        orderNumber: 2042,
        status: "placed",
        totalAmount: 714,
        items: [
          { id: "item3", name: "Wild Mushroom Risotto", quantity: 1, unitPrice: 420, releaseState: "Fired" },
          { id: "item4", name: "Molten Chocolate Cake", quantity: 1, unitPrice: 260, releaseState: "Fired" }
        ]
      }
    ]
  }
];


let localFallbackCustomers: any[] = [
  {
    id: "cust-1",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    phone: "9876543210",
    name: "Rahul Sharma",
    email: "rahul.sharma@example.test",
    notes: "Prefers corner tables, likes extra crispy garlic naan.",
    tags: ["VIP", "Regular"],
    loyaltyPointsBalance: 150,
    totalSpend: 8500,
    totalVisits: 6,
    firstVisitUtc: "2026-10-01T12:00:00Z",
    lastVisitUtc: "2026-11-19T19:30:00Z",
    consentGiven: true,
  },
  {
    id: "cust-2",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    phone: "9812345678",
    name: "Priya Patel",
    email: "priya.patel@example.test",
    notes: "Strict vegetarian, regular weekend diner.",
    tags: ["Regular", "Vegetarian"],
    loyaltyPointsBalance: 45,
    totalSpend: 3200,
    totalVisits: 3,
    firstVisitUtc: "2026-10-15T13:00:00Z",
    lastVisitUtc: "2026-11-18T20:15:00Z",
    consentGiven: true,
  },
  {
    id: "cust-3",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    phone: "9988776655",
    name: "Amit Verma",
    email: "amit.verma@example.test",
    notes: "Corporate takeaway client.",
    tags: ["New", "Parcel"],
    loyaltyPointsBalance: 10,
    totalSpend: 1400,
    totalVisits: 1,
    firstVisitUtc: "2026-11-20T18:00:00Z",
    lastVisitUtc: "2026-11-20T18:00:00Z",
    consentGiven: true,
  },
];

let localFallbackLoyaltyLedger: any[] = [
  {
    id: "loyalty-101",
    customerId: "cust-1",
    billId: "bill-table-1",
    transactionType: "Earn",
    points: 35,
    balanceAfter: 150,
    eligibleSalesAmount: 3500,
    idempotencyKey: "EARN-ORD-1002",
    description: "Earned 35 points on dine-in order #1002",
    createdAtUtc: "2026-11-19T19:40:00Z",
  },
  {
    id: "loyalty-102",
    customerId: "cust-2",
    billId: "bill-parcel-501",
    transactionType: "Earn",
    points: 20,
    balanceAfter: 45,
    eligibleSalesAmount: 2000,
    idempotencyKey: "EARN-ORD-1005",
    description: "Earned 20 points on takeaway order #1005",
    createdAtUtc: "2026-11-18T20:20:00Z",
  },
];

let localFallbackFeedback: any[] = [
  {
    id: "fb-101",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    billId: "bill-table-1",
    customerId: "cust-1",
    customerName: "Rahul Sharma",
    customerPhone: "9876543210",
    channel: "DineIn",
    tableNumber: 1,
    overallRating: 5,
    foodRating: 5,
    serviceRating: 5,
    ambianceRating: 5,
    comments: "Exceptional grilled salmon and hospitable table staff. Will definitely return!",
    status: "Reviewed",
    staffNotes: "VIP table guest. Chef personally greeted.",
    createdAtUtc: "2026-11-19T20:00:00Z",
  },
  {
    id: "fb-102",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    billId: "bill-parcel-501",
    customerId: "cust-2",
    customerName: "Priya Patel",
    customerPhone: "9812345678",
    channel: "Parcel",
    overallRating: 4,
    foodRating: 5,
    serviceRating: 4,
    ambianceRating: 4,
    comments: "Packaging was tight and leak-proof. Food remained steaming hot.",
    status: "New",
    createdAtUtc: "2026-11-18T20:45:00Z",
  },
];

let localFallbackMarketplaceStores: any[] = [
  {
    id: "mkt-store-1",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Swiggy",
    externalStoreId: "SWIGGY-BLR-001",
    storeStatus: "Online",
    autoAccept: true,
    lastSyncUtc: new Date().toISOString(),
  },
  {
    id: "mkt-store-2",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Zomato",
    externalStoreId: "ZOMATO-BLR-002",
    storeStatus: "Online",
    autoAccept: true,
    lastSyncUtc: new Date().toISOString(),
  },
];

let localFallbackMarketplaceItems: any[] = [
  {
    id: "mkt-item-1",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Swiggy",
    externalItemId: "SW-BC-01",
    menuItemId: "menu-item-1",
    menuItemName: "Butter Chicken",
    isAvailable: true,
    customPrice: 480,
    packagingCharge: 15,
  },
  {
    id: "mkt-item-2",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Swiggy",
    externalItemId: "SW-PBM-02",
    menuItemId: "menu-item-2",
    menuItemName: "Paneer Butter Masala",
    isAvailable: true,
    customPrice: 420,
    packagingCharge: 15,
  },
  {
    id: "mkt-item-3",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Zomato",
    externalItemId: "ZM-GN-03",
    menuItemId: "menu-item-3",
    menuItemName: "Garlic Naan",
    isAvailable: true,
    customPrice: 90,
    packagingCharge: 5,
  },
  {
    id: "mkt-item-4",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    provider: "Zomato",
    externalItemId: "ZM-FLS-04",
    menuItemId: "menu-item-4",
    menuItemName: "Fresh Lime Soda",
    isAvailable: true,
    customPrice: 120,
    packagingCharge: 10,
  },
];

let localFallbackInboundEvents: any[] = [];
let localFallbackOutboundUpdates: any[] = [
  {
    id: "upd-001",
    orderId: "ord-mkt-prior",
    provider: "Swiggy",
    externalOrderId: "SWIGGY-ORD-98711",
    targetStatus: "READY_FOR_PICKUP",
    attempts: 1,
    status: "Success",
    lastAttemptUtc: new Date().toISOString(),
  },
];

export function handleLocalModuleFallback(endpoint: string, options?: RequestInit): any {
  const method = options?.method?.toUpperCase() || "GET";
  const cleanEndpoint = endpoint.split("?")[0];
  let body: any = {};
  try {
    if (typeof options?.body === "string" && options.body.trim().length > 0) {
      body = JSON.parse(options.body);
    } else if (options?.body && typeof options.body === "object") {
      body = options.body;
    }
  } catch {
    body = {};
  }

  // 0. Super Admin: Login
  if (cleanEndpoint === "/sysadmin/login" && method === "POST") {
    return {
      token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzdXBlcmFkbWluIiwiZW1haWwiOiJzdXBlcmFkbWluIiwicm9sZSI6IlN1cGVyQWRtaW4ifQ.demo",
      user: {
        id: "00000000-0000-0000-0000-000000000000",
        firstName: "Super",
        lastName: "Admin",
        email: "superadmin@example.test",
        role: "SuperAdmin",
        restaurantId: "00000000-0000-0000-0000-000000000000"
      }
    };
  }

  // 1. Super Admin: Plans
  if (cleanEndpoint === "/sysadmin/plans" && method === "GET") {
    return [...localPlans];
  }

  const planMatch = cleanEndpoint.match(/^\/sysadmin\/plans\/([a-zA-Z0-9\-]+)$/);
  if (planMatch && method === "PUT") {
    const planId = planMatch[1];
    const plan = localPlans.find(p => p.id === planId || p.code === planId);
    if (plan) {
      if (body.features) plan.features = body.features;
      if (body.maxKitchenStations) plan.maxKitchenStations = body.maxKitchenStations;
      localAuditLogs.unshift({
        id: String(Date.now()),
        restaurantId: "22222222-0000-0000-0000-000000000001",
        restaurantName: "Tablo Heritage Restaurant",
        actorEmail: "superadmin@example.test",
        action: "UpdatedPlanFeatures",
        fromPlanCode: plan.code,
        toPlanCode: plan.code,
        reason: body.reason || "Updated plan features via Super Admin console",
        timestampUtc: new Date().toISOString()
      });
      return { ...plan };
    }
  }

  // 2. Super Admin: Overview
  if (cleanEndpoint === "/sysadmin/overview") {
    return {
      totalRestaurants: localTenants.length,
      activeRestaurants: localTenants.filter(t => t.isActive).length,
      disabledRestaurants: localTenants.filter(t => !t.isActive).length,
      totalSubscriptions: localSubscriptions.length,
      activeSubscriptions: localSubscriptions.filter(s => s.status === "Active").length,
      trialSubscriptions: 0,
      suspendedSubscriptions: 0,
      expiredSubscriptions: 0,
      expiringSoonCount: 0,
      totalEmployees: 4,
      subscriptionsByPlan: {
        premium: localSubscriptions.filter(s => s.planCode === "premium").length,
        plus: localSubscriptions.filter(s => s.planCode === "plus").length,
        basic: localSubscriptions.filter(s => s.planCode === "basic").length
      }
    };
  }

  // 3. Super Admin: Tenants
  if (cleanEndpoint === "/sysadmin/tenants" && method === "GET") {
    return [...localTenants];
  }

  const toggleTenantMatch = cleanEndpoint.match(/^\/sysadmin\/tenants\/([a-zA-Z0-9\-]+)\/toggle-status$/);
  if (toggleTenantMatch && method === "PUT") {
    const id = toggleTenantMatch[1];
    const tenant = localTenants.find(t => t.id === id);
    if (tenant) {
      tenant.isActive = !tenant.isActive;
      return { message: `Restaurant ${tenant.isActive ? "activated" : "disabled"} successfully.`, isActive: tenant.isActive };
    }
  }

  if (cleanEndpoint === "/sysadmin/onboard" && method === "POST") {
    const newId = crypto.randomUUID ? crypto.randomUUID() : `res-${Date.now()}`;
    const newTenant = {
      id: newId,
      name: body.restaurantName,
      description: "Newly onboarded restaurant",
      address: body.address,
      contactEmail: body.contactEmail,
      contactPhone: body.contactPhone,
      isActive: true,
      planCode: body.planCode || "basic",
      planName: body.planCode === "premium" ? "Premium Plan" : body.planCode === "plus" ? "Plus Plan" : "Basic Plan",
      subscriptionStatus: "Active",
      createdAt: new Date().toISOString()
    };
    localTenants.push(newTenant);
    localSubscriptions.push({
      id: crypto.randomUUID ? crypto.randomUUID() : `sub-${Date.now()}`,
      restaurantId: newId,
      restaurantName: body.restaurantName,
      planCode: newTenant.planCode,
      planName: newTenant.planName,
      status: "Active",
      startDateUtc: new Date().toISOString(),
      endDateUtc: new Date(Date.now() + 365 * 86400000).toISOString(),
      autoRenew: true,
      enabledFeatures: localPlans.find(p => p.code === newTenant.planCode)?.features || []
    });
    return { restaurantId: newId, message: "Restaurant onboarded successfully." };
  }

  // 4. Super Admin: Subscriptions
  if (cleanEndpoint === "/sysadmin/subscriptions" && method === "GET") {
    return [...localSubscriptions];
  }

  const previewMatch = endpoint.match(/\/sysadmin\/subscriptions\/([a-zA-Z0-9\-]+)\/preview\?targetPlanCode=([a-zA-Z0-9\-]+)/);
  if (previewMatch) {
    const [, , targetCode] = previewMatch;
    const currentSub = localSubscriptions[0];
    const currentPlan = localPlans.find(p => p.code === currentSub.planCode) || localPlans[2];
    const targetPlan = localPlans.find(p => p.code === targetCode) || localPlans[0];

    const curFeatures = new Set(currentPlan.features);
    const tarFeatures = new Set(targetPlan.features);

    return {
      fromPlanCode: currentPlan.code,
      toPlanCode: targetPlan.code,
      capabilitiesGained: targetPlan.features.filter(f => !curFeatures.has(f)),
      capabilitiesLost: currentPlan.features.filter(f => !tarFeatures.has(f)),
      unchangedCapabilities: targetPlan.features.filter(f => curFeatures.has(f))
    };
  }

  const changeSubMatch = cleanEndpoint.match(/^\/sysadmin\/subscriptions\/([a-zA-Z0-9\-]+)\/change-plan$/);
  if (changeSubMatch && method === "POST") {
    const restaurantId = changeSubMatch[1];
    const sub = localSubscriptions.find(s => s.restaurantId === restaurantId);
    if (sub) {
      const oldCode = sub.planCode;
      if (body.targetPlanCode) {
        sub.planCode = body.targetPlanCode;
        sub.planName = body.targetPlanCode === "premium" ? "Premium Plan" : body.targetPlanCode === "plus" ? "Plus Plan" : "Basic Plan";
        sub.enabledFeatures = localPlans.find(p => p.code === body.targetPlanCode)?.features || [];
        const tenant = localTenants.find(t => t.id === restaurantId);
        if (tenant) {
          tenant.planCode = sub.planCode;
          tenant.planName = sub.planName;
        }
      }
      if (body.newStatus) sub.status = body.newStatus;
      if (body.endDateUtc) sub.endDateUtc = body.endDateUtc;
      if (body.autoRenew !== undefined) sub.autoRenew = body.autoRenew;

      localAuditLogs.unshift({
        id: String(Date.now()),
        restaurantId,
        restaurantName: sub.restaurantName,
        actorEmail: "superadmin@example.test",
        action: "ChangedSubscription",
        fromPlanCode: oldCode,
        toPlanCode: sub.planCode,
        fromStatus: "Active",
        toStatus: sub.status,
        reason: body.reason || "Manual subscription plan update",
        timestampUtc: new Date().toISOString()
      });
      return { ...sub };
    }
  }

  // 5. Super Admin: Audit Logs
  if (endpoint.startsWith("/sysadmin/audit-logs")) {
    return [...localAuditLogs];
  }

  // 6. Subscriptions Public & Effective Access
  if (endpoint.startsWith("/subscriptions/effective-access")) {
    const sub = localSubscriptions[0];
    const plan = localPlans.find(p => p.code === sub.planCode) || localPlans[2];
    return {
      restaurantId: "22222222-0000-0000-0000-000000000001",
      restaurantName: "Tablo Heritage Restaurant",
      outletId: "22222222-0000-0000-0000-000000000002",
      role: "Admin",
      employeeName: "Aditi Sharma",
      employeeEmail: "admin@tablodemo.com",
      plan: {
        code: plan.code,
        name: plan.name,
        description: plan.description,
        status: sub.status,
        startDateUtc: sub.startDateUtc,
        endDateUtc: sub.endDateUtc,
        monthlyPriceInr: plan.monthlyPriceInr,
        autoRenew: sub.autoRenew
      },
      capabilities: [...plan.features],
      limits: {
        maxOutlets: plan.maxOutlets,
        maxTables: plan.maxTables
      },
      outlets: [
        { id: "22222222-0000-0000-0000-000000000002", name: "Main Dining & Counter Outlet", isMain: true }
      ]
    };
  }

  if (endpoint === "/subscriptions/current") {
    return { ...localSubscriptions[0] };
  }

  if (endpoint === "/subscriptions/plans") {
    return [...localPlans];
  }

  // 7. Kitchen Stations
  if (endpoint.includes("/kitchen-stations")) {
    return [
      {
        id: "66666666-0000-0000-0000-000000000001",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Food Kitchen Station",
        code: "FOOD-01",
        description: "Handles starters, main courses and tandoor",
        printerId: "66666666-1000-0000-0000-000000000001",
        isDefault: true,
        isActive: true,
        displayOrder: 1
      },
      {
        id: "66666666-0000-0000-0000-000000000002",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Beverage & Bar Station",
        code: "BEV-01",
        description: "Handles mocktails, sodas and filter coffee",
        printerId: "66666666-1000-0000-0000-000000000002",
        isDefault: false,
        isActive: true,
        displayOrder: 2
      },
      {
        id: "66666666-0000-0000-0000-000000000003",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Front Counter Bill Station",
        code: "FRONT-01",
        description: "Counter receipts & parcel handover",
        printerId: "66666666-1000-0000-0000-000000000003",
        isDefault: false,
        isActive: true,
        displayOrder: 3
      }
    ];
  }

  // 8. Printers
  if (endpoint.includes("/printers")) {
    return [
      {
        id: "66666666-1000-0000-0000-000000000001",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Food Kitchen 80mm Virtual Printer",
        connectionType: "NetworkTcp",
        ipAddress: "127.0.0.1",
        port: 9100,
        paperWidthMm: 80,
        charactersPerLine: 48,
        status: "Online",
        isActive: true
      },
      {
        id: "66666666-1000-0000-0000-000000000002",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Beverage Station 58mm Virtual Printer",
        connectionType: "NetworkTcp",
        ipAddress: "127.0.0.1",
        port: 9101,
        paperWidthMm: 58,
        charactersPerLine: 32,
        status: "Online",
        isActive: true
      },
      {
        id: "66666666-1000-0000-0000-000000000003",
        restaurantId: "22222222-0000-0000-0000-000000000001",
        outletId: "22222222-0000-0000-0000-000000000002",
        name: "Front Counter Bill Printer",
        connectionType: "NetworkTcp",
        ipAddress: "127.0.0.1",
        port: 9100,
        paperWidthMm: 80,
        charactersPerLine: 48,
        status: "Online",
        isActive: true
      }
    ];
  }

  // 9. Parcel Settings
  if (endpoint.includes("/parcel/settings")) {
    return {
      parcelPackagingChargeMode: "PerItem",
      parcelPackagingFlatRate: 20.00,
      parcelPackagingPercentage: 5.00,
      parcelPackagingTaxable: false,
      parcelPackagingTaxRate: 0.00,
      scheduledPickupEnabled: true,
      defaultPreparationLeadMinutes: 15,
      carSideOrderingEnabled: true,
      hybridDineInParcelEnabled: true,
      leftoverContainerCharge: 10.00,
      upiQrEnabled: true,
      upiVpa: "tablodemo@okhdfcbank",
      upiPayeeName: "Tablo Heritage Restaurant",
      upiMerchantCode: "5812"
    };
  }

  // 10. Modifiers
  if (endpoint.includes("/modifiers")) {
    return [
      {
        id: "AAAAAAAA-0000-0000-0000-000000000001",
        name: "Spice Level",
        minSelections: 1,
        maxSelections: 1,
        isActive: true,
        displayOrder: 1,
        options: [
          { id: "BBBBBBBB-0000-0000-0000-000000000001", name: "Mild", priceDelta: 0.00, isAvailable: true, displayOrder: 1 },
          { id: "BBBBBBBB-0000-0000-0000-000000000002", name: "Medium Spicy", priceDelta: 0.00, isAvailable: true, displayOrder: 2 },
          { id: "BBBBBBBB-0000-0000-0000-000000000003", name: "Extra Fiery Hot", priceDelta: 10.00, isAvailable: true, displayOrder: 3 }
        ]
      },
      {
        id: "AAAAAAAA-0000-0000-0000-000000000002",
        name: "Gourmet Add-ons",
        minSelections: 0,
        maxSelections: 3,
        isActive: true,
        displayOrder: 2,
        options: [
          { id: "BBBBBBBB-0000-0000-0000-000000000004", name: "Extra Amul Butter Cube", priceDelta: 20.00, isAvailable: true, displayOrder: 1 },
          { id: "BBBBBBBB-0000-0000-0000-000000000005", name: "Grated Processed Cheese", priceDelta: 35.00, isAvailable: true, displayOrder: 2 }
        ]
      }
    ];
  }

  // 11. Service Desk Endpoints
  // Tables
  if (cleanEndpoint.match(/\/service-desk\/tables$/) && method === "GET") {
    return [...localServiceTables];
  }

  // Dining Groups for Table
  const groupsMatch = cleanEndpoint.match(/\/service-desk\/tables\/([a-zA-Z0-9\-]+)\/groups$/);
  if (groupsMatch) {
    const tableId = groupsMatch[1];
    if (method === "GET") {
      return localDiningGroups.filter((g) => g.tableId === tableId);
    }
    if (method === "POST") {
      const newGroup = {
        id: `group-${Date.now()}`,
        tableId,
        label: body.label || String.fromCharCode(65 + localDiningGroups.filter((g) => g.tableId === tableId).length),
        createdAtUtc: new Date().toISOString(),
      };
      localDiningGroups.push(newGroup);
      return newGroup;
    }
  }

  // Bill Review for Table
  const billMatch = cleanEndpoint.match(/\/service-desk\/tables\/([a-zA-Z0-9\-]+)\/bill$/);
  if (billMatch && method === "GET") {
    const tableId = billMatch[1];
    const table = localServiceTables.find((t) => t.id === tableId);
    const tableNumber = table?.tableNumber || 1;
    let diningGroupId: string | null = null;
    try {
      const urlObj = new URL("http://dummy" + endpoint);
      diningGroupId = urlObj.searchParams.get("diningGroupId");
    } catch {
      diningGroupId = null;
    }

    let openBills = localServiceBills.filter((b) => b.tableId === tableId && b.status === "Open");
    if (diningGroupId) {
      openBills = openBills.filter((b) => b.diningGroupId === diningGroupId);
    }
    let openBill = openBills[0] || null;
    if (!openBill) {
      openBill = {
        id: `bill-${tableId}${diningGroupId ? `-${diningGroupId}` : ""}`,
        tableId,
        tableNumber,
        diningGroupId: diningGroupId || null,
        groupLabel: diningGroupId ? localDiningGroups.find((g) => g.id === diningGroupId)?.label || "A" : null,
        status: "Open",
        revision: `rev-${Date.now()}`,
        createdAtUtc: new Date().toISOString(),
        settledAtUtc: null,
        paymentMethod: null,
        subtotal: 0,
        packagingCharge: 0,
        tax: 0,
        discount: 0,
        totalAmount: 0,
        paidAmount: 0,
        remainingAmount: 0,
        canSettle: false,
        orders: [],
      };
    }
    return {
      tableId,
      tableNumber,
      openBills: openBills.length > 0 ? openBills : [openBill],
      openBill,
      unbilledActiveOrderCount: 0,
      recentSettlements: [],
    };
  }

  // Open Table Bill
  const openBillMatch = cleanEndpoint.match(/\/service-desk\/tables\/([a-zA-Z0-9\-]+)\/bill\/open$/);
  if (openBillMatch && method === "POST") {
    const tableId = openBillMatch[1];
    const table = localServiceTables.find((t) => t.id === tableId);
    const newBill = {
      id: `bill-${tableId}-${Date.now()}`,
      tableId,
      tableNumber: table?.tableNumber || 1,
      status: "Open",
      revision: `rev-${Date.now()}`,
      createdAtUtc: new Date().toISOString(),
      settledAtUtc: null,
      paymentMethod: null,
      subtotal: 0,
      packagingCharge: 0,
      tax: 0,
      discount: 0,
      totalAmount: 0,
      paidAmount: 0,
      remainingAmount: 0,
      canSettle: false,
      orders: [],
    };
    localServiceBills.push(newBill);
    return newBill;
  }

  // Staff Orders Submission
  if (cleanEndpoint.match(/\/service-desk\/orders$/) && method === "POST") {
    const orderId = `ORD-${Date.now()}`;
    const orderNumber = Math.floor(Math.random() * 9000 + 1000);
    const tableId = body.tableId;
    const table = localServiceTables.find((t) => t.id === tableId);
    if (table) {
      table.status = "Occupied";
      table.activeOrdersCount = (table.activeOrdersCount || 0) + 1;
    }
    const orderItems = (body.items || []).map((i: any, idx: number) => ({
      id: `item-${Date.now()}-${idx}`,
      menuItemId: i.menuItemId,
      name: i.name || "Special Item",
      quantity: i.quantity,
      unitPrice: i.price || 250,
      releaseState: i.releaseState || "Fired",
      courseName: i.courseName,
      packingIntent: i.packingIntent || "DineIn",
    }));
    const subtotal = orderItems.reduce((acc: number, item: any) => acc + item.quantity * item.unitPrice, 0);
    const tax = Math.round(subtotal * 0.05);
    const totalAmount = subtotal + tax;

    let bill = localServiceBills.find((b) => b.tableId === tableId && b.status === "Open" && (!body.diningGroupId || b.diningGroupId === body.diningGroupId));
    if (!bill) {
      bill = {
        id: `bill-${tableId}-${Date.now()}`,
        tableId,
        tableNumber: table?.tableNumber || 1,
        diningGroupId: body.diningGroupId || null,
        groupLabel: body.diningGroupId ? localDiningGroups.find((g) => g.id === body.diningGroupId)?.label || "A" : null,
        status: "Open",
        revision: `rev-${Date.now()}`,
        createdAtUtc: new Date().toISOString(),
        settledAtUtc: null,
        paymentMethod: null,
        subtotal: 0,
        packagingCharge: 0,
        tax: 0,
        discount: 0,
        totalAmount: 0,
        paidAmount: 0,
        remainingAmount: 0,
        canSettle: true,
        orders: [],
      };
      localServiceBills.push(bill);
    }
    bill.orders.push({
      id: orderId,
      orderNumber,
      status: "placed",
      totalAmount,
      items: orderItems,
    });
    bill.subtotal += subtotal;
    bill.tax += tax;
    bill.totalAmount += totalAmount;
    bill.remainingAmount += totalAmount;
    bill.canSettle = true;
    if (table) {
      table.openBillTotal = (table.openBillTotal || 0) + totalAmount;
    }
    return { orderId, orderNumber };
  }

  // Bill Split Preview
  const splitPreviewMatch = cleanEndpoint.match(/\/service-desk\/bills\/([a-zA-Z0-9\-]+)\/split-preview$/);
  if (splitPreviewMatch && method === "POST") {
    const billId = splitPreviewMatch[1];
    const bill = localServiceBills.find((b) => b.id === billId) || localServiceBills[0];
    const total = bill?.totalAmount || 0;
    if (body.splitMode === "Amount") {
      const amounts: number[] = body.splitAmounts || [];
      const sum = amounts.reduce((a, b) => a + b, 0);
      const unallocated = Math.round((total - sum) * 100) / 100;
      return {
        originalBillTotal: total,
        newBillTotal: sum,
        unallocatedBalance: unallocated,
        canSplit: Math.abs(unallocated) < 0.01,
      };
    } else {
      return {
        originalBillTotal: Math.round(total / 2),
        newBillTotal: Math.round(total / 2),
        unallocatedBalance: 0,
        canSplit: true,
      };
    }
  }

  // Bill Split Execute
  const splitMatch = cleanEndpoint.match(/\/service-desk\/bills\/([a-zA-Z0-9\-]+)\/split$/);
  if (splitMatch && method === "POST") {
    const billId = splitMatch[1];
    const bill = localServiceBills.find((b) => b.id === billId) || localServiceBills[0];
    const half = Math.round((bill.totalAmount || 1000) / 2);
    const newBill = {
      ...bill,
      id: `bill-split-${Date.now()}`,
      subtotal: Math.round(bill.subtotal / 2),
      tax: Math.round(bill.tax / 2),
      totalAmount: half,
      remainingAmount: half,
      orders: [...bill.orders],
    };
    bill.totalAmount -= half;
    bill.remainingAmount -= half;
    bill.subtotal = Math.round(bill.subtotal / 2);
    bill.tax = Math.round(bill.tax / 2);
    localServiceBills.push(newBill);
    return { originalBill: bill, newBills: [newBill] };
  }

  // Bill Settlement
  const settleMatch = cleanEndpoint.match(/\/service-desk\/bills\/([a-zA-Z0-9\-]+)\/settle$/);
  if (settleMatch && method === "POST") {
    const billId = settleMatch[1];
    const bill = localServiceBills.find((b) => b.id === billId);
    if (bill) {
      bill.status = "Settled";
      bill.settledAtUtc = new Date().toISOString();
      bill.paymentMethod = body.paymentMethod || "Cash";
      bill.remainingAmount = 0;
      bill.paidAmount = bill.totalAmount;
      const table = localServiceTables.find((t) => t.id === bill.tableId);
      if (table) {
        table.status = "Available";
        table.activeOrdersCount = 0;
        table.openBillTotal = 0;
      }
      return { ...bill };
    }
  }

  // Table Visit Transfer
  if (cleanEndpoint.match(/\/service-desk\/transfer$/) && method === "POST") {
    const { sourceTableId, targetTableId } = body;
    const sourceTable = localServiceTables.find((t) => t.id === sourceTableId);
    const targetTable = localServiceTables.find((t) => t.id === targetTableId);
    if (sourceTable && targetTable) {
      targetTable.status = sourceTable.status;
      targetTable.activeOrdersCount = sourceTable.activeOrdersCount;
      targetTable.openBillTotal = sourceTable.openBillTotal;
      sourceTable.status = "Available";
      sourceTable.activeOrdersCount = 0;
      sourceTable.openBillTotal = 0;
      localServiceBills.filter((b) => b.tableId === sourceTableId).forEach((b) => {
        b.tableId = targetTableId;
        b.tableNumber = targetTable.tableNumber;
      });
      return { success: true, message: `Table ${sourceTable.tableNumber} transferred to Table ${targetTable.tableNumber}.` };
    }
    return { success: true, message: "Transfer completed successfully." };
  }

  // ==========================================
  // BATCH 1: CRM, LOYALTY, FEEDBACK & MARKETPLACE
  // ==========================================

  // 1. CRM Customers List & Search
  if (cleanEndpoint.match(/\/crm\/customers$/)) {
    if (method === "GET") {
      const searchParam = endpoint.includes("search=")
        ? decodeURIComponent(endpoint.split("search=")[1].split("&")[0]).toLowerCase()
        : "";
      let list = [...localFallbackCustomers];
      if (searchParam) {
        list = list.filter(
          (c) =>
            c.phone.toLowerCase().includes(searchParam) ||
            c.name.toLowerCase().includes(searchParam) ||
            c.tags.some((t: string) => t.toLowerCase().includes(searchParam))
        );
      }
      return list;
    }
    if (method === "POST") {
      const existing = localFallbackCustomers.find((c) => c.phone === body.phone);
      if (existing) {
        return existing;
      }
      const newCust = {
        id: `cust-${Date.now()}`,
        restaurantId: "22222222-0000-0000-0000-000000000001",
        phone: body.phone,
        name: body.name || "Guest Customer",
        email: body.email || "",
        notes: body.notes || "",
        tags: body.tags || ["Regular"],
        loyaltyPointsBalance: 0,
        totalSpend: 0,
        totalVisits: 1,
        firstVisitUtc: new Date().toISOString(),
        lastVisitUtc: new Date().toISOString(),
        consentGiven: body.consentGiven !== false,
      };
      localFallbackCustomers.unshift(newCust);
      return newCust;
    }
  }

  // 2. CRM Single Customer & Ledger History
  const custIdMatch = cleanEndpoint.match(/\/crm\/customers\/([a-zA-Z0-9\-]+)$/);
  if (custIdMatch) {
    const customerId = custIdMatch[1];
    const customer = localFallbackCustomers.find((c) => c.id === customerId);
    if (!customer) {
      return null;
    }
    if (method === "GET") {
      const ledger = localFallbackLoyaltyLedger.filter((l) => l.customerId === customerId);
      return { customer, ledger };
    }
    if (method === "PUT") {
      if (body.name !== undefined) customer.name = body.name;
      if (body.email !== undefined) customer.email = body.email;
      if (body.notes !== undefined) customer.notes = body.notes;
      if (body.tags !== undefined) customer.tags = body.tags;
      return customer;
    }
  }

  // 3. Loyalty Earning (Idempotent per Bill)
  if (cleanEndpoint.match(/\/crm\/loyalty\/earn$/) && method === "POST") {
    const { customerId, billId, eligibleAmount, idempotencyKey } = body;
    const key = idempotencyKey || `EARN-${billId}`;
    const existingEntry = localFallbackLoyaltyLedger.find((l) => l.idempotencyKey === key);
    const customer = localFallbackCustomers.find((c) => c.id === customerId);
    if (!customer) {
      throw new Error("Customer not found");
    }
    if (existingEntry) {
      return {
        pointsEarned: existingEntry.points,
        newBalance: customer.loyaltyPointsBalance,
        entry: existingEntry,
      };
    }
    // 1 point per 100 INR eligible amount
    const pointsEarned = Math.max(1, Math.floor((eligibleAmount || 0) / 100));
    customer.loyaltyPointsBalance += pointsEarned;
    customer.totalSpend += eligibleAmount || 0;
    customer.totalVisits += 1;
    customer.lastVisitUtc = new Date().toISOString();

    const entry = {
      id: `loyalty-${Date.now()}`,
      customerId,
      billId,
      transactionType: "Earn",
      points: pointsEarned,
      balanceAfter: customer.loyaltyPointsBalance,
      eligibleSalesAmount: eligibleAmount || 0,
      idempotencyKey: key,
      description: `Earned ${pointsEarned} points for order #${billId?.slice(0, 8)}`,
      createdAtUtc: new Date().toISOString(),
    };
    localFallbackLoyaltyLedger.unshift(entry);
    return { pointsEarned, newBalance: customer.loyaltyPointsBalance, entry };
  }

  // 4. Loyalty Redemption
  if (cleanEndpoint.match(/\/crm\/loyalty\/redeem$/) && method === "POST") {
    const { customerId, billId, pointsToRedeem, idempotencyKey } = body;
    const key = idempotencyKey || `REDEEM-${billId}-${Date.now()}`;
    const customer = localFallbackCustomers.find((c) => c.id === customerId);
    if (!customer) {
      throw new Error("Customer not found");
    }
    if (customer.loyaltyPointsBalance < pointsToRedeem) {
      throw new Error(`Insufficient loyalty balance. Available: ${customer.loyaltyPointsBalance}, Requested: ${pointsToRedeem}`);
    }
    customer.loyaltyPointsBalance -= pointsToRedeem;
    const discountAmountInr = pointsToRedeem; // 1 point = 1 INR

    const entry = {
      id: `loyalty-${Date.now()}`,
      customerId,
      billId,
      transactionType: "Redeem",
      points: -pointsToRedeem,
      balanceAfter: customer.loyaltyPointsBalance,
      eligibleSalesAmount: 0,
      idempotencyKey: key,
      description: `Redeemed ${pointsToRedeem} points (₹${discountAmountInr} discount) on bill #${billId?.slice(0, 8)}`,
      createdAtUtc: new Date().toISOString(),
    };
    localFallbackLoyaltyLedger.unshift(entry);
    return { pointsRedeemed: pointsToRedeem, discountAmountInr, newBalance: customer.loyaltyPointsBalance, entry };
  }

  // 5. Customer Feedback List & Submission
  if (cleanEndpoint.match(/\/crm\/feedback$/)) {
    if (method === "GET") {
      return localFallbackFeedback;
    }
    if (method === "POST") {
      const newFeedback = {
        id: `fb-${Date.now()}`,
        restaurantId: "22222222-0000-0000-0000-000000000001",
        billId: body.billId,
        orderId: body.orderId,
        customerId: body.customerId,
        customerName: body.customerName || "Verified Guest",
        customerPhone: body.customerPhone || "",
        channel: body.channel || "DineIn",
        tableNumber: body.tableNumber,
        overallRating: body.overallRating || 5,
        foodRating: body.foodRating || 5,
        serviceRating: body.serviceRating || 5,
        ambianceRating: body.ambianceRating || 5,
        comments: body.comments || "Great food and service!",
        status: "New",
        staffNotes: "",
        createdAtUtc: new Date().toISOString(),
      };
      localFallbackFeedback.unshift(newFeedback);
      return newFeedback;
    }
  }

  const feedbackStatusMatch = cleanEndpoint.match(/\/crm\/feedback\/([a-zA-Z0-9\-]+)$/);
  if (feedbackStatusMatch && method === "PATCH") {
    const fbId = feedbackStatusMatch[1];
    const item = localFallbackFeedback.find((f) => f.id === fbId);
    if (item) {
      if (body.status) item.status = body.status;
      if (body.staffNotes !== undefined) item.staffNotes = body.staffNotes;
      return item;
    }
  }

  // 6. Marketplace Stores
  if (cleanEndpoint.match(/\/marketplace\/stores$/) && method === "GET") {
    return localFallbackMarketplaceStores;
  }

  const storeStatusMatch = cleanEndpoint.match(/\/marketplace\/stores\/([a-zA-Z0-9\-]+)\/toggle-status$/);
  if (storeStatusMatch && method === "POST") {
    const provider = storeStatusMatch[1].toLowerCase();
    const store = localFallbackMarketplaceStores.find((s) => s.provider.toLowerCase() === provider);
    if (store) {
      store.storeStatus = body.status || (store.storeStatus === "Online" ? "Paused" : "Online");
      store.lastSyncUtc = new Date().toISOString();
      return store;
    }
  }

  const storeAutoAcceptMatch = cleanEndpoint.match(/\/marketplace\/stores\/([a-zA-Z0-9\-]+)\/auto-accept$/);
  if (storeAutoAcceptMatch && method === "POST") {
    const provider = storeAutoAcceptMatch[1].toLowerCase();
    const store = localFallbackMarketplaceStores.find((s) => s.provider.toLowerCase() === provider);
    if (store) {
      store.autoAccept = body.autoAccept !== undefined ? body.autoAccept : !store.autoAccept;
      store.lastSyncUtc = new Date().toISOString();
      return store;
    }
  }

  // 7. Marketplace Mapped Items
  if (cleanEndpoint.match(/\/marketplace\/items$/) && method === "GET") {
    return localFallbackMarketplaceItems;
  }

  const itemAvailMatch = cleanEndpoint.match(/\/marketplace\/items\/([a-zA-Z0-9\-]+)\/toggle-availability$/);
  if (itemAvailMatch && method === "POST") {
    const itemId = itemAvailMatch[1];
    const item = localFallbackMarketplaceItems.find((i) => i.id === itemId);
    if (item) {
      item.isAvailable = body.isAvailable !== undefined ? body.isAvailable : !item.isAvailable;
      return item;
    }
  }

  // 8. Marketplace Inbound Webhook (Idempotent Deduplication & Station KOT Generation)
  const webhookMatch = cleanEndpoint.match(/\/marketplace\/webhook\/([a-zA-Z0-9\-]+)$/);
  if (webhookMatch && method === "POST") {
    const providerName = webhookMatch[1].toLowerCase() === "zomato" ? "Zomato" : "Swiggy";
    const eventId = body.eventId || `EVT-${Date.now()}`;
    const existingEvt = localFallbackInboundEvents.find((e) => e.eventId === eventId);
    if (existingEvt) {
      return {
        status: "Duplicate",
        message: `Webhook event ${eventId} has already been processed. No duplicate order or KOT created.`,
        orderId: existingEvt.linkedOrderId,
        generatedKotTicketIds: [],
      };
    }

    const orderId = `mkt-ord-${Date.now()}`;
    const orderNumber = 4000 + localFallbackInboundEvents.length + 1;
    const tokenNumber = 800 + localFallbackInboundEvents.length + 1;

    // Classify items into Food vs Beverage stations
    const foodItems = (body.items || []).filter(
      (i: any) =>
        !i.name.toLowerCase().includes("soda") &&
        !i.name.toLowerCase().includes("coffee") &&
        !i.name.toLowerCase().includes("shake") &&
        !i.name.toLowerCase().includes("beverage")
    );
    const bevItems = (body.items || []).filter(
      (i: any) =>
        i.name.toLowerCase().includes("soda") ||
        i.name.toLowerCase().includes("coffee") ||
        i.name.toLowerCase().includes("shake") ||
        i.name.toLowerCase().includes("beverage")
    );

    const generatedKotTicketIds: string[] = [];
    if (foodItems.length > 0 || (body.items || []).length === 0) {
      generatedKotTicketIds.push(`KOT-FOOD-${Date.now()}`);
    }
    if (bevItems.length > 0) {
      generatedKotTicketIds.push(`KOT-BEV-${Date.now()}`);
    }

    localFallbackInboundEvents.push({
      id: `evt-${Date.now()}`,
      restaurantId: "22222222-0000-0000-0000-000000000001",
      provider: providerName,
      eventId,
      externalOrderId: body.externalOrderId || `${providerName.toUpperCase()}-${Date.now().toString().slice(-5)}`,
      eventType: body.eventType || "ORDER_PLACED",
      payloadJson: JSON.stringify(body),
      status: "Processed",
      linkedOrderId: orderId,
      receivedAtUtc: new Date().toISOString(),
      processedAtUtc: new Date().toISOString(),
    });

    // Create corresponding pending outbound status update
    localFallbackOutboundUpdates.push({
      id: `upd-${Date.now()}`,
      orderId,
      provider: providerName,
      externalOrderId: body.externalOrderId || `${providerName.toUpperCase()}-${Date.now().toString().slice(-5)}`,
      targetStatus: "CONFIRMED",
      attempts: 1,
      status: "Success",
      lastAttemptUtc: new Date().toISOString(),
    });

    return {
      status: "Processed",
      message: `Order ${body.externalOrderId} accepted from ${providerName}. Token #${tokenNumber} assigned.`,
      orderId,
      orderNumber,
      tokenNumber,
      generatedKotTicketIds,
    };
  }

  // 9. Marketplace Outbound Updates Queue & Retry
  if (cleanEndpoint.match(/\/marketplace\/outbound-updates$/) && method === "GET") {
    return localFallbackOutboundUpdates;
  }

  const retryUpdateMatch = cleanEndpoint.match(/\/marketplace\/outbound-updates\/([a-zA-Z0-9\-]+)\/retry$/);
  if (retryUpdateMatch && method === "POST") {
    const updateId = retryUpdateMatch[1];
    const update = localFallbackOutboundUpdates.find((u) => u.id === updateId);
    if (update) {
      update.attempts += 1;
      update.status = "Success";
      update.lastError = undefined;
      update.lastAttemptUtc = new Date().toISOString();
      return { success: true, message: `Outbound update for order ${update.externalOrderId} dispatched successfully.` };
    }
  }

  // 10. Central Kitchen Production Batches, Transfers & Menu Publishing
  const batchMatch = cleanEndpoint.match(/\/central-kitchen\/([^/]+)\/batches$/);
  if (batchMatch) {
    const routeRestId = batchMatch[1];
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    const allowedRoles = ["Admin", "Owner", "SuperAdmin", "Kitchen", "StockJournalManager"];
    if (!allowedRoles.includes(auth.role || "")) {
      throw new Error("403 Forbidden: Insufficient role permissions for central kitchen.");
    }
    if (auth.role !== "SuperAdmin" && auth.restaurantId !== routeRestId) {
      throw new Error("403 Forbidden: Cross-tenant access forbidden.");
    }

    if (method === "GET") {
      return localProductionBatches.filter((b) => b.restaurantId === routeRestId);
    }
    if (method === "POST") {
      const batchId = `batch-${Date.now()}`;
      const batch = {
        id: batchId,
        restaurantId: routeRestId,
        batchCode: `BATCH-${new Date().toISOString().slice(0, 10).replace(/-/g, "")}-${batchId.slice(-4).toUpperCase()}`,
        recipeName: body.recipeName,
        targetQuantity: body.targetQuantity,
        unit: body.unit,
        unitProductionCost: body.unitProductionCost,
        totalCost: Math.round(body.targetQuantity * body.unitProductionCost * 100) / 100,
        status: "Completed",
        createdAtUtc: new Date().toISOString(),
      };
      localProductionBatches.unshift(batch);
      return batch;
    }
  }

  const dispatchMatch = cleanEndpoint.match(/\/central-kitchen\/([^/]+)\/transfers\/dispatch$/);
  if (dispatchMatch && method === "POST") {
    const routeRestId = dispatchMatch[1];
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    const allowedRoles = ["Admin", "Owner", "SuperAdmin", "Kitchen", "StockJournalManager"];
    if (!allowedRoles.includes(auth.role || "")) {
      throw new Error("403 Forbidden: Insufficient role permissions for central kitchen.");
    }
    if (auth.role !== "SuperAdmin" && auth.restaurantId !== routeRestId) {
      throw new Error("403 Forbidden: Cross-tenant access forbidden.");
    }

    const transferId = `trf-${Date.now()}`;
    const transfer = {
      transferId,
      transferCode: `TRF-${new Date().toISOString().slice(0, 10).replace(/-/g, "")}-${Math.floor(100 + Math.random() * 900)}`,
      sourceKitchenId: body.sourceKitchenId || routeRestId,
      destinationOutletId: body.destinationOutletId,
      itemName: body.itemName,
      quantityDispatched: body.quantityDispatched,
      quantityReceived: 0,
      shortageQuantity: 0,
      dispatchedCost: Math.round(body.quantityDispatched * body.unitCost * 100) / 100,
      receivedCost: 0,
      varianceLossCost: 0,
      status: "Dispatched",
      unitCost: body.unitCost,
      dispatchedAtUtc: new Date().toISOString(),
    };
    localStockTransfers.unshift(transfer);
    return transfer;
  }

  const receiveMatch = cleanEndpoint.match(/\/central-kitchen\/transfers\/([^/]+)\/receive$/);
  if (receiveMatch && method === "POST") {
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    const allowedRoles = ["Admin", "Owner", "SuperAdmin", "Kitchen", "StockJournalManager"];
    if (!allowedRoles.includes(auth.role || "")) {
      throw new Error("403 Forbidden: Insufficient role permissions for central kitchen.");
    }

    const transferId = receiveMatch[1];
    let transfer = localStockTransfers.find((t) => t.transferId === transferId);
    if (!transfer) {
      transfer = {
        transferId,
        transferCode: `TRF-${new Date().toISOString().slice(0, 10).replace(/-/g, "")}-101`,
        sourceKitchenId: "22222222-0000-0000-0000-000000000001",
        destinationOutletId: auth.restaurantId || "22222222-0000-0000-0000-000000000009",
        itemName: "Central Recipe Batch",
        quantityDispatched: body.quantityReceived,
        quantityReceived: 0,
        shortageQuantity: 0,
        dispatchedCost: body.quantityReceived * 100,
        receivedCost: 0,
        varianceLossCost: 0,
        status: "Dispatched",
        unitCost: 100,
        dispatchedAtUtc: new Date().toISOString(),
      };
      localStockTransfers.push(transfer);
    }

    // Verify authorized destination or source
    if (auth.role !== "SuperAdmin" && auth.restaurantId !== transfer.destinationOutletId && auth.restaurantId !== transfer.sourceKitchenId) {
      // Check if destinationOutlet belongs to tenant
      const destOut = localOutlets.find((o) => o.outletId === transfer.destinationOutletId);
      if (destOut && destOut.restaurantId !== auth.restaurantId) {
        throw new Error("403 Forbidden: Cross-tenant transfer receipt forbidden.");
      }
    }

    const shortage = Math.max(0, transfer.quantityDispatched - body.quantityReceived);
    transfer.quantityReceived = body.quantityReceived;
    transfer.shortageQuantity = shortage;
    transfer.receivedCost = Math.round(body.quantityReceived * (transfer.unitCost || 100) * 100) / 100;
    transfer.varianceLossCost = Math.round(shortage * (transfer.unitCost || 100) * 100) / 100;
    transfer.status = shortage > 0 ? "PartiallyReceived" : "FullyReceived";
    transfer.discrepancyReason = body.discrepancyReason;
    transfer.receivedAtUtc = new Date().toISOString();
    return transfer;
  }

  if (cleanEndpoint.match(/\/central-kitchen(\/[^/]+)?\/transfers$/) && method === "GET") {
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    return localStockTransfers;
  }

  const menuPreviewMatch = cleanEndpoint.match(/\/central-kitchen\/([^/]+)\/menu(-publish)?\/preview$/);
  if (menuPreviewMatch && method === "POST") {
    const routeRestId = menuPreviewMatch[1];
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    if (auth.role !== "SuperAdmin" && auth.restaurantId !== routeRestId) {
      throw new Error("403 Forbidden: Cross-tenant access forbidden.");
    }

    return {
      centralRestaurantId: routeRestId,
      targetOutletIds: body.targetOutletIds || ["22222222-0000-0000-0000-000000000009"],
      itemsToAddCount: 9,
      itemsToUpdateCount: 0,
      overridesPreview: [
        {
          menuItemId: "99999999-0000-0000-0000-000000000001",
          itemName: "Paneer Tikka Angara",
          centralPrice: 280,
          outletPriceOverride: null,
          isAvailableOverride: null,
        },
        {
          menuItemId: "99999999-0000-0000-0000-000000000003",
          itemName: "Butter Chicken Handi",
          centralPrice: 360,
          outletPriceOverride: null,
          isAvailableOverride: null,
        },
      ],
    };
  }

  const menuPublishMatch = cleanEndpoint.match(/\/central-kitchen\/([^/]+)\/menu(-publish)?\/(publish|execute)$/);
  if (menuPublishMatch && method === "POST") {
    const routeRestId = menuPublishMatch[1];
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required for central kitchen operations.");
    }
    if (auth.role !== "SuperAdmin" && auth.restaurantId !== routeRestId) {
      throw new Error("403 Forbidden: Cross-tenant access forbidden.");
    }

    return {
      publishJobId: `pub-${Date.now()}`,
      publishedAtUtc: new Date().toISOString(),
      outletsUpdatedCount: body.targetOutletIds?.length || 1,
      itemsUpdatedCount: (body.itemOverrides || []).length || 9,
      success: true,
    };
  }

  // 11. Storefront Direct Customer Ordering & Admin Configuration
  const adminOutletMatch = cleanEndpoint.match(/\/storefront\/admin\/outlets\/([^/]+)$/);
  if (adminOutletMatch && method === "PUT") {
    const auth = getAuthClaims(options);
    if (!auth.isAuthenticated) {
      throw new Error("401 Unauthorized: Authentication required to configure outlets.");
    }
    if (!["Admin", "Owner", "SuperAdmin"].includes(auth.role || "")) {
      throw new Error("403 Forbidden: Insufficient permissions to update outlet settings.");
    }

    const oId = adminOutletMatch[1];
    const outlet = localOutlets.find((o) => o.outletId === oId);
    if (!outlet) {
      throw new Error("404 Not Found: Outlet not found.");
    }
    if (auth.role !== "SuperAdmin" && outlet.restaurantId !== auth.restaurantId) {
      throw new Error("403 Forbidden: Cannot update outlet of another tenant.");
    }

    if (body.isOpen !== undefined) outlet.isOpen = Boolean(body.isOpen);
    if (body.isPublished !== undefined) outlet.isPublished = Boolean(body.isPublished);
    if (body.openingTime) outlet.openingTime = body.openingTime.trim();
    if (body.closingTime) outlet.closingTime = body.closingTime.trim();
    if (body.isOvernight !== undefined) outlet.isOvernight = Boolean(body.isOvernight);
    if (body.timezone) outlet.timezone = body.timezone.trim();
    if (body.leadTimeMinutes !== undefined) outlet.leadTimeMinutes = Number(body.leadTimeMinutes);
    if (body.minOrderAmount !== undefined) outlet.minOrderAmount = Number(body.minOrderAmount);
    if (body.deliveryBaseFee !== undefined) outlet.deliveryBaseFee = Number(body.deliveryBaseFee);
    if (body.freeDeliveryAboveAmount !== undefined) outlet.freeDeliveryAboveAmount = Number(body.freeDeliveryAboveAmount);
    if (body.phone) outlet.phone = body.phone.trim();
    if (body.isTemporarilyClosed !== undefined) outlet.isTemporarilyClosed = Boolean(body.isTemporarilyClosed);
    if (body.closureReason !== undefined) outlet.closureReason = body.closureReason;

    const { isOpenNow } = evaluateOutletOpen(outlet);
    return {
      outletId: outlet.outletId,
      restaurantId: outlet.restaurantId,
      name: outlet.name,
      address: outlet.address,
      phone: outlet.phone,
      isOpenNow,
      operatingHours: `${outlet.openingTime} - ${outlet.closingTime}`,
      leadTimeMinutes: outlet.leadTimeMinutes,
      minOrderAmount: outlet.minOrderAmount,
      deliveryBaseFee: outlet.deliveryBaseFee,
      freeDeliveryAboveAmount: outlet.freeDeliveryAboveAmount,
      isPublished: outlet.isPublished,
      isTemporarilyClosed: outlet.isTemporarilyClosed,
      closureReason: outlet.closureReason,
    };
  }

  if (cleanEndpoint.match(/\/storefront\/outlets$/) && method === "GET") {
    const rawQuery = endpoint.includes("?") ? endpoint.split("?")[1] : "";
    const params = new URLSearchParams(rawQuery);
    const targetRestId = params.get("restaurantId");

    const matched = localOutlets.filter((o) => {
      if (!o.isPublished) return false;
      if (targetRestId && o.restaurantId !== targetRestId) return false;
      return true;
    });

    return matched.map((o) => {
      const { isOpenNow } = evaluateOutletOpen(o);
      return {
        outletId: o.outletId,
        restaurantId: o.restaurantId,
        name: o.name,
        address: o.address,
        phone: o.phone,
        isOpenNow,
        operatingHours: `${o.openingTime} - ${o.closingTime}`,
        leadTimeMinutes: o.leadTimeMinutes,
        minOrderAmount: o.minOrderAmount,
        deliveryBaseFee: o.deliveryBaseFee,
        freeDeliveryAboveAmount: o.freeDeliveryAboveAmount,
        isPublished: o.isPublished,
        isTemporarilyClosed: o.isTemporarilyClosed,
        closureReason: o.closureReason,
      };
    });
  }

  const outletDetailMatch = cleanEndpoint.match(/\/storefront\/outlets\/([^/]+)$/);
  if (outletDetailMatch && method === "GET") {
    const oId = outletDetailMatch[1];
    const outlet = localOutlets.find((o) => o.outletId === oId && o.isPublished);
    if (!outlet) {
      throw new Error("404 Not Found: Outlet not found or is currently unpublished.");
    }

    const { isOpenNow } = evaluateOutletOpen(outlet);
    return {
      outletId: outlet.outletId,
      restaurantId: outlet.restaurantId,
      name: outlet.name,
      address: outlet.address,
      phone: outlet.phone,
      isOpenNow,
      operatingHours: `${outlet.openingTime} - ${outlet.closingTime}`,
      leadTimeMinutes: outlet.leadTimeMinutes,
      minOrderAmount: outlet.minOrderAmount,
      deliveryBaseFee: outlet.deliveryBaseFee,
      freeDeliveryAboveAmount: outlet.freeDeliveryAboveAmount,
      isPublished: outlet.isPublished,
      isTemporarilyClosed: outlet.isTemporarilyClosed,
      closureReason: outlet.closureReason,
    };
  }

  const outletMenuMatch = cleanEndpoint.match(/\/storefront\/outlets\/([^/]+)\/menu$/);
  if (outletMenuMatch && method === "GET") {
    const oId = outletMenuMatch[1];
    const outlet = localOutlets.find((o) => o.outletId === oId && o.isPublished);
    if (!outlet) {
      throw new Error("404 Not Found: Outlet not found or is currently unpublished.");
    }

    // Return menu strictly belonging to this outlet's restaurant
    const isCoastal = outlet.restaurantId === "22222222-0000-0000-0000-000000000009";
    return isCoastal
      ? [
          {
            id: "item-coastal-fish",
            name: "Mangalore Fish Curry",
            description: "Fresh coastal catch simmered in rich coconut gravy.",
            price: 380,
            isAvailable: true,
            category: "Coastal Specials",
            categoryId: "cat-coastal-1",
            restaurantId: "22222222-0000-0000-0000-000000000009",
            modifiers: [
              { id: "opt-spice-coastal", name: "High Spice", priceDelta: 0, isAvailable: true },
              { id: "opt-extra-gravy", name: "Extra Gravy", priceDelta: 30, isAvailable: true },
            ],
          },
          {
            id: "item-neer-dosa",
            name: "Neer Dosa (3 pcs)",
            description: "Soft delicate rice crepes served with chutney.",
            price: 90,
            isAvailable: true,
            category: "Breads & Rice",
            categoryId: "cat-coastal-2",
            restaurantId: "22222222-0000-0000-0000-000000000009",
            modifiers: [],
          },
        ]
      : [
          {
            id: "item-pbm",
            name: "Paneer Butter Masala",
            description: "Cottage cheese cubes simmered in spiced tomato butter gravy.",
            price: 350,
            isAvailable: true,
            category: "Main Course",
            categoryId: "cat-heritage-1",
            restaurantId: "22222222-0000-0000-0000-000000000001",
            modifiers: [
              { id: "opt-spice", name: "Medium Spicy", priceDelta: 0, isAvailable: true },
              { id: "opt-extra-butter", name: "Extra Butter", priceDelta: 15, isAvailable: true },
            ],
          },
          {
            id: "item-naan",
            name: "Butter Garlic Naan",
            description: "Tandoori flatbread with crushed garlic and melted butter.",
            price: 75,
            isAvailable: true,
            category: "Tandoor Breads",
            categoryId: "cat-heritage-2",
            restaurantId: "22222222-0000-0000-0000-000000000001",
            modifiers: [
              { id: "opt-crispy", name: "Extra Crispy", priceDelta: 0, isAvailable: true },
            ],
          },
        ];
  }

  if (cleanEndpoint.match(/\/storefront\/checkout$/) && method === "POST") {
    const outlet = localOutlets.find((o) => o.outletId === body.outletId && o.isPublished);
    if (!outlet) {
      throw new Error("400 Bad Request: Invalid, inactive, or unpublished outlet specified.");
    }

    // Validate operating hours
    const now = new Date();
    if (body.fulfillmentTiming === "Scheduled") {
      if (!body.scheduledForUtc) {
        throw new Error("400 Bad Request: Scheduled pickup time is required.");
      }
      const schedTime = new Date(body.scheduledForUtc).getTime();
      if (schedTime < now.getTime() + outlet.leadTimeMinutes * 60000) {
        throw new Error(`400 Bad Request: Scheduled pickup time must be at least ${outlet.leadTimeMinutes} minutes in advance.`);
      }
      const schedCheck = evaluateOutletOpen(outlet, new Date(body.scheduledForUtc));
      if (!schedCheck.isOpenNow) {
        throw new Error(`400 Bad Request: Selected scheduled time is outside operating hours (${outlet.openingTime} - ${outlet.closingTime} ${outlet.timezone}).`);
      }
    } else {
      const immCheck = evaluateOutletOpen(outlet, now);
      if (!immCheck.isOpenNow) {
        throw new Error(`400 Bad Request: ${immCheck.message}`);
      }
    }

    // Validate cross-tenant item isolation
    for (const item of body.items || []) {
      const isHeritage = outlet.restaurantId === "22222222-0000-0000-0000-000000000001";
      if (isHeritage && (item.menuItemId?.includes("coastal") || item.name?.includes("Coastal") || item.name?.includes("Neer Dosa"))) {
        throw new Error("400 Bad Request: One or more items do not belong to this outlet's menu or are unavailable.");
      }
      if (!isHeritage && (item.menuItemId?.includes("pbm") || item.name?.includes("Paneer Butter Masala"))) {
        throw new Error("400 Bad Request: One or more items do not belong to this outlet's menu or are unavailable.");
      }
    }

    // Check idempotency
    const key = body.idempotencyKey;
    if (key && localStorefrontOrders.has(key)) {
      const existing = localStorefrontOrders.get(key);
      const fingerprint = JSON.stringify(body.items);
      if (existing._fingerprint !== fingerprint) {
        throw new Error("409 Conflict: This idempotency key was previously used for different order details.");
      }
      return { ...existing, isReplay: true };
    }

    const subtotal = (body.items || []).reduce((sum: number, it: any) => {
      const modDelta = (it.modifiers || []).reduce((ms: number, m: any) => ms + (m.priceDelta || 0), 0);
      return sum + (it.unitPrice + modDelta) * it.quantity;
    }, 0);

    // Validate minimum order amount
    if (subtotal < outlet.minOrderAmount) {
      throw new Error(`400 Bad Request: Minimum order amount is ₹${outlet.minOrderAmount.toFixed(2)}. Current subtotal is ₹${subtotal.toFixed(2)}.`);
    }

    const tax = Math.round(subtotal * 0.05);
    const deliveryFee = body.orderChannel === "DirectDelivery" ? (subtotal >= outlet.freeDeliveryAboveAmount ? 0 : outlet.deliveryBaseFee) : 0;
    const packagingFee = body.orderChannel === "TakeawayPickup" || body.orderChannel === "DirectDelivery" ? 15 : 0;
    const totalPayable = subtotal + tax + deliveryFee + packagingFee;

    const orderNum = ++storefrontOrderSeq;
    const token = `#TK-${String(++storefrontTokenSeq).padStart(3, "0")}`;
    const ref = `TABLO-${orderNum}`;
    const upiUri = `upi://pay?pa=tablo.heritage@icici&pn=Tablo%20Heritage&am=${totalPayable}&cu=INR&tr=${ref}&tn=Order%20${orderNum}`;
    const trackingToken = `trk-${orderNum}-${Date.now().toString(36)}`;

    const result = {
      orderId: `ord-${Date.now()}`,
      orderNumber: String(orderNum),
      token,
      status: "Placed",
      subtotal,
      tax,
      deliveryFee,
      packagingFee,
      totalPayable,
      paymentStatus: body.paymentMethod === "UpiOnline" ? "Paid" : "Pending",
      upiUri,
      payeeVpa: "tablo.heritage@icici",
      reference: ref,
      estimatedFulfillmentUtc: body.scheduledForUtc || new Date(Date.now() + outlet.leadTimeMinutes * 60000).toISOString(),
      isReplay: false,
      trackingToken,
      items: body.items,
      channel: body.orderChannel,
      outletName: outlet.name,
      _fingerprint: JSON.stringify(body.items),
    };

    if (key) {
      localStorefrontOrders.set(key, result);
    }
    return result;
  }

  const trackByTokenMatch = cleanEndpoint.match(/\/storefront\/orders\/track\/([^/]+)$/);
  if (trackByTokenMatch && method === "GET") {
    const trk = decodeURIComponent(trackByTokenMatch[1]);
    for (const val of localStorefrontOrders.values()) {
      if (val.trackingToken === trk) {
        return {
          orderNumber: `ORD-${val.orderNumber}`,
          token: val.token,
          status: val.status,
          channel: val.channel,
          outletName: val.outletName,
          placedAtUtc: new Date().toISOString(),
          estimatedFulfillmentUtc: val.estimatedFulfillmentUtc,
          items: (val.items || []).map((i: any) => ({
            name: i.name,
            quantity: i.quantity,
            unitPrice: i.unitPrice,
            totalPrice: i.quantity * i.unitPrice,
          })),
          subtotal: val.subtotal,
          tax: val.tax,
          deliveryFee: val.deliveryFee,
          packagingFee: val.packagingFee,
          totalPayable: val.totalPayable,
          paymentStatus: val.paymentStatus,
          upiUri: val.upiUri,
          payeeVpa: val.payeeVpa,
        };
      }
    }
    throw new Error("404 Not Found: Order not found.");
  }

  const storefrontOrderMatch = cleanEndpoint.match(/\/storefront\/orders\/([^/]+)$/);
  if (storefrontOrderMatch && method === "GET") {
    const oId = storefrontOrderMatch[1];
    const rawQuery = endpoint.includes("?") ? endpoint.split("?")[1] : "";
    const params = new URLSearchParams(rawQuery);
    const tokenQuery = params.get("token");

    for (const val of localStorefrontOrders.values()) {
      if (val.orderId === oId) {
        if (tokenQuery && val.trackingToken !== tokenQuery) {
          throw new Error("404 Not Found: Order not found.");
        }
        return val;
      }
    }
    return {
      orderId: oId,
      orderNumber: "1001",
      token: "#TK-001",
      status: "Preparing",
      subtotal: 400,
      tax: 20,
      deliveryFee: 40,
      packagingFee: 15,
      totalPayable: 475,
      paymentStatus: "Pending",
      estimatedFulfillmentUtc: new Date(Date.now() + 20 * 60000).toISOString(),
      isReplay: false,
      trackingToken: "trk-1001-default",
    };
  }

  return null;
}
