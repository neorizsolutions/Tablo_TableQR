import { apiClient, apiDownloadBlob } from "./apiClient";

export interface Ingredient {
  id: string;
  name: string;
  baseUnit: string;
  lowStockThreshold: number | null;
  currentAverageCost: number;
  isActive: boolean;
  onHandQuantity: number;
  isLowStock: boolean;
  lastMovementAtUtc: string | null;
}

export interface CreateIngredientRequest {
  name: string;
  baseUnit: string;
  lowStockThreshold?: number | null;
  initialStock?: number | null;
  initialStockUnit?: string | null;
  initialCost?: number | null;
  operationId?: string;
}

export interface UpdateIngredientRequest {
  name?: string;
  lowStockThreshold?: number | null;
  currentAverageCost?: number | null;
  isActive?: boolean;
}

export interface RecordMovementRequest {
  operationId: string;
  quantity: number;
  unit?: string;
  reason: "Restock" | "Waste" | "ManualCorrection" | "OpeningStock";
  totalCost?: number | null;
  unitCost?: number | null;
  notes?: string;
}

export interface StockMovement {
  id: string;
  ingredientId: string;
  ingredientName: string;
  timestampUtc: string;
  quantityChange: number;
  unitSnapshot: string;
  reason: string;
  unitCostSnapshot?: number | null;
  totalCost?: number | null;
  referenceType?: string | null;
  referenceId?: string | null;
  notes?: string | null;
  actorName?: string | null;
  operationId?: string | null;
}

export interface RecipeItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  ingredientBaseUnit: string;
  usableQuantity: number;
  yieldPercent: number;
  rawQuantityPerServing: number;
  unitCost: number;
  costPerServing: number;
}

export interface MenuItemRecipe {
  menuItemId: string;
  menuItemName: string;
  categoryName: string;
  price: number;
  isConfigured: boolean;
  totalCostPerServing: number;
  grossProfitPerServing: number;
  grossMarginPercent: number;
  hasIncompleteCost: boolean;
  items: RecipeItem[];
}

export interface SetRecipeRequest {
  items: {
    ingredientId: string;
    usableQuantity: number;
    yieldPercent: number;
  }[];
}

export interface StocktakeItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  recordedStockSnapshot: number;
  physicalCount: number;
  variance: number;
  stockAtPosting?: number | null;
  finalAdjustment?: number | null;
}

export interface Stocktake {
  id: string;
  businessDateIst: string;
  countedAtUtc: string;
  postedAtUtc?: string | null;
  status: "Draft" | "Completed";
  operationId: string;
  notes?: string | null;
  actorName?: string | null;
  hasReconciliationConflict?: boolean;
  items: StocktakeItem[];
}

export interface PostStocktakeRequest {
  operationId: string;
  confirmReconciliation: boolean;
  counts?: {
    ingredientId: string;
    physicalCount: number;
  }[];
  notes?: string;
}

// --- REPORT TYPES ---

export interface StockReportFilter {
  startDate?: string;
  endDate?: string;
  ingredientId?: string;
  reason?: string;
  lowStockOnly?: boolean;
}

export interface StockReportMovement {
  id: string;
  timestampUtc: string;
  timestampIst: string;
  reason: string;
  quantityChange: number;
  unitSnapshot: string;
  unitCostSnapshot?: number | null;
  totalCost?: number | null;
  referenceType?: string | null;
  referenceId?: string | null;
  actorName?: string | null;
  notes?: string | null;
}

export interface StockReportRow {
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  lowStockThreshold: number | null;
  isLowStock: boolean;
  openingQuantity: number;
  receiptsQuantity: number;
  salesQuantity: number;
  wasteQuantity: number;
  adjustmentsQuantity: number;
  closingQuantity: number;
  costPerBaseUnit: number;
  closingStockValue: number;
  movements: StockReportMovement[];
}

export interface StockReport {
  startDate: string;
  endDate: string;
  totalClosingStockValue: number;
  totalIngredientsCount: number;
  lowStockCount: number;
  rows: StockReportRow[];
}

export interface SalesGrossProfitFilter {
  startDate?: string;
  endDate?: string;
  menuItemId?: string;
  categoryId?: string;
}

export interface BillItemIngredientCost {
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  rawQuantityPerPortion: number;
  totalRawQuantity: number;
  unitCostSnapshot: number;
  totalCost: number;
}

export interface SalesGrossProfitDrillDown {
  billId: string;
  tableNumber: number;
  groupLabel?: string | null;
  settledAtUtc: string;
  settledAtIst: string;
  quantity: number;
  grossRevenue: number;
  allocatedDiscount: number;
  netRevenue: number;
  allocatedTax: number;
  totalIngredientCost: number;
  grossProfit: number;
  grossMarginPercent: number;
  isCostComplete: boolean;
  incompleteReason?: string | null;
  ingredientBreakdown: BillItemIngredientCost[];
}

export interface SalesGrossProfitRow {
  menuItemId: string;
  menuItemName: string;
  categoryName: string;
  quantitySold: number;
  grossSales: number;
  totalDiscount: number;
  netSalesExcludingTax: number;
  totalTax: number;
  totalCogs: number;
  grossProfit: number;
  grossMarginPercent: number;
  isCostComplete: boolean;
  incompleteReason?: string | null;
  drillDown: SalesGrossProfitDrillDown[];
}

export interface IncompleteCostingItem {
  menuItemId: string;
  menuItemName: string;
  quantitySold: number;
  netSales: number;
  reason: string;
}

export interface SalesGrossProfitReport {
  startDate: string;
  endDate: string;
  totalGrossSales: number;
  totalDiscounts: number;
  totalNetSalesExcludingTax: number;
  totalTax: number;
  totalCogs: number;
  totalGrossProfit: number;
  overallGrossMarginPercent: number;
  totalBillsSettled: number;
  totalPortionsSold: number;
  incompleteCostingCount: number;
  incompleteSalesAmount: number;
  incompleteCostingItems: IncompleteCostingItem[];
  rows: SalesGrossProfitRow[];
}

// --- API METHODS ---

export const getIngredients = (restaurantId: string) =>
  apiClient<Ingredient[]>(`/restaurants/${restaurantId}/inventory/ingredients`);

export const createIngredient = (restaurantId: string, data: CreateIngredientRequest) =>
  apiClient<Ingredient>(`/restaurants/${restaurantId}/inventory/ingredients`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const updateIngredient = (restaurantId: string, id: string, data: UpdateIngredientRequest) =>
  apiClient<Ingredient>(`/restaurants/${restaurantId}/inventory/ingredients/${id}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const recordStockMovement = (restaurantId: string, ingredientId: string, data: RecordMovementRequest) =>
  apiClient<StockMovement>(`/restaurants/${restaurantId}/inventory/ingredients/${ingredientId}/movements`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const getMovements = (restaurantId: string, ingredientId?: string, limit = 200) => {
  const query = ingredientId ? `?ingredientId=${ingredientId}&limit=${limit}` : `?limit=${limit}`;
  return apiClient<StockMovement[]>(`/restaurants/${restaurantId}/inventory/movements${query}`);
};

export const getRecipes = (restaurantId: string) =>
  apiClient<MenuItemRecipe[]>(`/restaurants/${restaurantId}/inventory/recipes`);

export const setRecipe = (restaurantId: string, menuItemId: string, data: SetRecipeRequest) =>
  apiClient<MenuItemRecipe>(`/restaurants/${restaurantId}/inventory/recipes/${menuItemId}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const getStocktakes = (restaurantId: string) =>
  apiClient<Stocktake[]>(`/restaurants/${restaurantId}/inventory/stocktakes`);

export const startStocktake = (restaurantId: string, operationId: string, notes?: string) =>
  apiClient<Stocktake>(`/restaurants/${restaurantId}/inventory/stocktakes/start`, {
    method: "POST",
    body: JSON.stringify({ operationId, notes }),
  });

export const postStocktake = (restaurantId: string, stocktakeId: string, data: PostStocktakeRequest) =>
  apiClient<Stocktake>(`/restaurants/${restaurantId}/inventory/stocktakes/${stocktakeId}/post`, {
    method: "POST",
    body: JSON.stringify(data),
  });

// --- REPORTS API CALLS & EXPORTS ---

const buildQuery = (params: Record<string, any>) =>
  new URLSearchParams(
    Object.entries(params)
      .filter(([, v]) => v !== undefined && v !== "" && v !== null)
      .map(([k, v]) => [k, String(v)])
  ).toString();

export const getStockReport = (restaurantId: string, filter: StockReportFilter) =>
  apiClient<StockReport>(`/restaurants/${restaurantId}/inventory/reports/stock?${buildQuery(filter)}`);

export async function exportStockReport(restaurantId: string, filter: StockReportFilter) {
  const blob = await apiDownloadBlob(`/restaurants/${restaurantId}/inventory/reports/stock/export?${buildQuery(filter)}`);
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Tablo_Stock_Report_${new Date().toISOString().slice(0, 10)}.xlsx`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export const getSalesGrossProfitReport = (restaurantId: string, filter: SalesGrossProfitFilter) =>
  apiClient<SalesGrossProfitReport>(`/restaurants/${restaurantId}/inventory/reports/sales-gross-profit?${buildQuery(filter)}`);

export async function exportSalesGrossProfitReport(restaurantId: string, filter: SalesGrossProfitFilter) {
  const blob = await apiDownloadBlob(`/restaurants/${restaurantId}/inventory/reports/sales-gross-profit/export?${buildQuery(filter)}`);
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Tablo_Sales_Gross_Profit_Report_${new Date().toISOString().slice(0, 10)}.xlsx`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}


// --- 1. SUPPLIERS ---

export interface Supplier {
  id: string;
  name: string;
  contactPerson?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  taxId?: string | null;
  isActive: boolean;
  ingredientsCount: number;
  invoicesCount: number;
  createdAtUtc: string;
}

export interface CreateSupplierRequest {
  name: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  address?: string;
  taxId?: string;
}

export interface UpdateSupplierRequest {
  name: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  address?: string;
  taxId?: string;
  isActive?: boolean;
}

export interface SupplierIngredient {
  id: string;
  supplierId: string;
  supplierName: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  packUnit: string;
  packQuantity: number;
  packUnitType: string;
  conversionFactor: number;
  lastPurchasePricePerPack: number;
  normalizedPricePerBaseUnit: number;
}

export interface SetSupplierIngredientRequest {
  supplierId: string;
  ingredientId: string;
  packUnit: string;
  packQuantity: number;
  packUnitType: string;
  conversionFactor: number;
  lastPurchasePricePerPack: number;
}

export interface SupplierPriceComparisonItem {
  supplierId: string;
  supplierName: string;
  packUnit: string;
  packQuantity: number;
  lastPricePerPack: number;
  normalizedPricePerBaseUnit: number;
  lastPurchasedAtUtc: string;
}

export interface IngredientPriceComparison {
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  suppliers: SupplierPriceComparisonItem[];
}

export const getSuppliers = (restaurantId: string, includeInactive = true) =>
  apiClient<Supplier[]>(`/restaurants/${restaurantId}/inventory/suppliers?includeInactive=${includeInactive}`);

export const createSupplier = (restaurantId: string, data: CreateSupplierRequest) =>
  apiClient<Supplier>(`/restaurants/${restaurantId}/inventory/suppliers`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const updateSupplier = (restaurantId: string, id: string, data: UpdateSupplierRequest) =>
  apiClient<Supplier>(`/restaurants/${restaurantId}/inventory/suppliers/${id}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const getSupplierIngredients = (restaurantId: string, supplierId: string) =>
  apiClient<SupplierIngredient[]>(`/restaurants/${restaurantId}/inventory/suppliers/${supplierId}/ingredients`);

export const setSupplierIngredient = (restaurantId: string, supplierId: string, data: SetSupplierIngredientRequest) =>
  apiClient<SupplierIngredient>(`/restaurants/${restaurantId}/inventory/suppliers/${supplierId}/ingredients`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const compareIngredientPrices = (restaurantId: string, ingredientId: string) =>
  apiClient<IngredientPriceComparison>(`/restaurants/${restaurantId}/inventory/ingredients/${ingredientId}/price-comparison`);

// --- 2. PURCHASE ORDERS & INVOICES ---

export interface PurchaseOrderItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  packUnit: string;
  orderedPackQuantity: number;
  receivedPackQuantity: number;
  unitPricePerPack: number;
  lineTotal: number;
  conversionFactor: number;
}

export interface PurchaseOrder {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierName: string;
  status: "Draft" | "Approved" | "PartiallyReceived" | "Received" | "Cancelled";
  orderDateUtc: string;
  expectedDeliveryDateUtc?: string | null;
  approvedAtUtc?: string | null;
  totalAmount: number;
  notes?: string | null;
  items: PurchaseOrderItem[];
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  expectedDeliveryDateUtc?: string;
  notes?: string;
  items: {
    ingredientId: string;
    packUnit: string;
    orderedPackQuantity: number;
    unitPricePerPack: number;
    conversionFactor: number;
  }[];
}

export interface ReceiveGoodsRequest {
  operationId: string;
  supplierId: string;
  invoiceNumber: string;
  invoiceDate: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  shippingCharges: number;
  grandTotal: number;
  notes?: string;
  purchaseOrderId?: string;
  items: {
    ingredientId: string;
    purchaseOrderItemId?: string;
    packUnit: string;
    receivedPackQuantity: number;
    conversionFactor: number;
    packPrice: number;
    lineDiscount: number;
    lineTax: number;
    totalLineCost: number;
    expiryDate?: string;
    lotCode?: string;
  }[];
}

export interface PurchaseInvoiceItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  packUnit: string;
  receivedPackQuantity: number;
  conversionFactor: number;
  baseQuantity: number;
  packPrice: number;
  lineDiscount: number;
  lineTax: number;
  totalLineCost: number;
  unitCostPerBaseUnit: number;
  stockLotId?: string | null;
  lotCode?: string | null;
  returnedBaseQuantity: number;
}

export interface PurchaseInvoice {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  purchaseOrderId?: string | null;
  purchaseOrderNumber?: string | null;
  invoiceDate: string;
  receivedAtUtc: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  shippingCharges: number;
  grandTotal: number;
  notes?: string | null;
  items: PurchaseInvoiceItem[];
}

export const getPurchaseOrders = (restaurantId: string, status?: string) => {
  const query = status ? `?status=${status}` : "";
  return apiClient<PurchaseOrder[]>(`/restaurants/${restaurantId}/inventory/purchase-orders${query}`);
};

export const createPurchaseOrder = (restaurantId: string, data: CreatePurchaseOrderRequest) =>
  apiClient<PurchaseOrder>(`/restaurants/${restaurantId}/inventory/purchase-orders`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const updatePurchaseOrderStatus = (restaurantId: string, id: string, status: string) =>
  apiClient<PurchaseOrder>(`/restaurants/${restaurantId}/inventory/purchase-orders/${id}/status`, {
    method: "PUT",
    body: JSON.stringify({ status }),
  });

export const receiveGoods = (restaurantId: string, data: ReceiveGoodsRequest) =>
  apiClient<PurchaseInvoice>(`/restaurants/${restaurantId}/inventory/goods-receipts`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const getInvoices = (restaurantId: string, supplierId?: string) => {
  const query = supplierId ? `?supplierId=${supplierId}` : "";
  return apiClient<PurchaseInvoice[]>(`/restaurants/${restaurantId}/inventory/invoices${query}`);
};

export const getInvoiceById = (restaurantId: string, id: string) =>
  apiClient<PurchaseInvoice>(`/restaurants/${restaurantId}/inventory/invoices/${id}`);

// --- 3. PURCHASE RETURNS ---

export interface PurchaseReturnItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  returnQuantity: number;
  unitCostSnapshot: number;
  totalCredit: number;
  reason?: string | null;
}

export interface PurchaseReturn {
  id: string;
  returnNumber: string;
  supplierId: string;
  supplierName: string;
  purchaseInvoiceId: string;
  invoiceNumber: string;
  creditNoteReference: string;
  reason: string;
  returnDateUtc: string;
  totalCreditAmount: number;
  notes?: string | null;
  items: PurchaseReturnItem[];
}

export interface CreatePurchaseReturnRequest {
  operationId: string;
  purchaseInvoiceId: string;
  creditNoteReference: string;
  reason: string;
  notes?: string;
  items: {
    purchaseInvoiceItemId: string;
    returnBaseQuantity: number;
    reason?: string;
  }[];
}

export const createPurchaseReturn = (restaurantId: string, data: CreatePurchaseReturnRequest) =>
  apiClient<PurchaseReturn>(`/restaurants/${restaurantId}/inventory/purchase-returns`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const getPurchaseReturns = (restaurantId: string) =>
  apiClient<PurchaseReturn[]>(`/restaurants/${restaurantId}/inventory/purchase-returns`);

// --- 4. PREPARATION RECIPES & BATCHES ---

export interface PreparationRecipeInput {
  id: string;
  inputIngredientId: string;
  inputIngredientName: string;
  inputBaseUnit: string;
  quantity: number;
  yieldPercent: number;
  unitCost: number;
  extendedCost: number;
}

export interface PreparationRecipe {
  id: string;
  outputIngredientId: string;
  outputIngredientName: string;
  outputBaseUnit: string;
  expectedYieldQuantity: number;
  notes?: string | null;
  inputs: PreparationRecipeInput[];
}

export interface SavePreparationRecipeRequest {
  outputIngredientId: string;
  expectedYieldQuantity: number;
  notes?: string;
  inputs: {
    inputIngredientId: string;
    quantity: number;
    yieldPercent: number;
  }[];
}

export interface PreparationBatch {
  id: string;
  preparationRecipeId: string;
  outputIngredientId: string;
  outputIngredientName: string;
  outputBaseUnit: string;
  preparedAtUtc: string;
  multiplier: number;
  expectedYieldQuantity: number;
  actualYieldQuantity: number;
  wasteQuantity: number;
  wasteReason?: string | null;
  totalInputCost: number;
  costPerBaseUnit: number;
  outputStockLotId?: string | null;
  lotCode?: string | null;
}

export interface RecordPreparationBatchRequest {
  operationId: string;
  preparationRecipeId: string;
  multiplier: number;
  actualYieldQuantity: number;
  wasteQuantity?: number;
  wasteReason?: string;
  expiryDate?: string;
  notes?: string;
}

export const getPreparationRecipes = (restaurantId: string) =>
  apiClient<PreparationRecipe[]>(`/restaurants/${restaurantId}/inventory/preparation-recipes`);

export const savePreparationRecipe = (restaurantId: string, data: SavePreparationRecipeRequest) =>
  apiClient<PreparationRecipe>(`/restaurants/${restaurantId}/inventory/preparation-recipes`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const recordPreparationBatch = (restaurantId: string, data: RecordPreparationBatchRequest) =>
  apiClient<PreparationBatch>(`/restaurants/${restaurantId}/inventory/preparation-batches`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const getPreparationBatches = (restaurantId: string) =>
  apiClient<PreparationBatch[]>(`/restaurants/${restaurantId}/inventory/preparation-batches`);

// --- 5. LOTS & EXPIRY ---

export interface StockLot {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  lotCode: string;
  initialQuantity: number;
  availableQuantity: number;
  unitCost: number;
  receivedOrProducedDateUtc: string;
  expiryDate?: string | null;
  status: "Active" | "Quarantined" | "WrittenOff" | "Exhausted";
  sourceType?: string | null;
  sourceReferenceId?: string | null;
  isExpiringSoon: boolean;
  isExpired: boolean;
}

export const getLots = (restaurantId: string, status?: string, expiringSoon?: boolean) => {
  const params: Record<string, any> = {};
  if (status) params.status = status;
  if (expiringSoon !== undefined) params.expiringSoon = expiringSoon;
  return apiClient<StockLot[]>(`/restaurants/${restaurantId}/inventory/lots?${buildQuery(params)}`);
};

export const quarantineLot = (restaurantId: string, id: string, reason: string) =>
  apiClient<StockLot>(`/restaurants/${restaurantId}/inventory/lots/${id}/quarantine`, {
    method: "POST",
    body: JSON.stringify({ operationId: crypto.randomUUID(), reason }),
  });

export const writeOffLot = (restaurantId: string, id: string, quantity: number, reason: string, notes?: string) =>
  apiClient<StockLot>(`/restaurants/${restaurantId}/inventory/lots/${id}/write-off`, {
    method: "POST",
    body: JSON.stringify({ operationId: crypto.randomUUID(), quantity, reason, notes }),
  });

// --- 6. COSTING CONFIGURATION ---

export interface InventorySettings {
  costingMethod: "WeightedAverage" | "FIFO" | "LIFO";
  autoManageAvailability: boolean;
}

export const getInventorySettings = (restaurantId: string) =>
  apiClient<InventorySettings>(`/restaurants/${restaurantId}/inventory/settings`);

export const updateInventorySettings = (restaurantId: string, data: InventorySettings) =>
  apiClient<InventorySettings>(`/restaurants/${restaurantId}/inventory/settings`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

// --- 7. OUTLETS & TRANSFERS ---

export interface Outlet {
  id: string;
  name: string;
  code: string;
  isCentralKitchen: boolean;
  isActive: boolean;
  address?: string | null;
}

export interface CreateOutletRequest {
  name: string;
  code: string;
  isCentralKitchen?: boolean;
  address?: string;
}

export interface StockTransferItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  requestedQuantity: number;
  dispatchedQuantity: number;
  receivedQuantity: number;
  discrepancyQuantity: number;
  discrepancyReason?: string | null;
  unitCostSnapshot: number;
}

export interface StockTransfer {
  id: string;
  transferNumber: string;
  sourceOutletId: string;
  sourceOutletName: string;
  destinationOutletId: string;
  destinationOutletName: string;
  status: "Requested" | "Approved" | "Dispatched" | "Received" | "Cancelled";
  createdAtUtc: string;
  dispatchedAtUtc?: string | null;
  receivedAtUtc?: string | null;
  notes?: string | null;
  items: StockTransferItem[];
}

export interface CreateStockTransferRequest {
  sourceOutletId: string;
  destinationOutletId: string;
  notes?: string;
  items: {
    ingredientId: string;
    requestedQuantity: number;
  }[];
}

export const getOutlets = (restaurantId: string) =>
  apiClient<Outlet[]>(`/restaurants/${restaurantId}/inventory/outlets`);

export const createOutlet = (restaurantId: string, data: CreateOutletRequest) =>
  apiClient<Outlet>(`/restaurants/${restaurantId}/inventory/outlets`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const getTransfers = (restaurantId: string) =>
  apiClient<StockTransfer[]>(`/restaurants/${restaurantId}/inventory/transfers`);

export const createTransfer = (restaurantId: string, data: CreateStockTransferRequest) =>
  apiClient<StockTransfer>(`/restaurants/${restaurantId}/inventory/transfers`, {
    method: "POST",
    body: JSON.stringify(data),
  });

export const dispatchTransfer = (restaurantId: string, id: string, items: { itemId: string; dispatchedQuantity: number }[]) =>
  apiClient<StockTransfer>(`/restaurants/${restaurantId}/inventory/transfers/${id}/dispatch`, {
    method: "POST",
    body: JSON.stringify({ operationId: crypto.randomUUID(), items }),
  });

export const receiveTransfer = (
  restaurantId: string,
  id: string,
  items: { itemId: string; receivedQuantity: number; discrepancyReason?: string }[]
) =>
  apiClient<StockTransfer>(`/restaurants/${restaurantId}/inventory/transfers/${id}/receive`, {
    method: "POST",
    body: JSON.stringify({ operationId: crypto.randomUUID(), items }),
  });

// --- 8. MENU AVAILABILITY ---

export interface IngredientAvailability {
  ingredientId: string;
  ingredientName: string;
  baseUnit: string;
  requiredPerPortion: number;
  availableStock: number;
  portionsPossible: number;
}

export interface MenuItemAvailability {
  menuItemId: string;
  menuItemName: string;
  isConfigured: boolean;
  isAvailable: boolean;
  maxPortionsAvailable: number;
  limitingIngredients: IngredientAvailability[];
}

export const getMenuAvailability = (restaurantId: string) =>
  apiClient<MenuItemAvailability[]>(`/restaurants/${restaurantId}/inventory/menu-availability`);
