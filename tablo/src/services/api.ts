import { MenuItem, Order, OrderStatus, Restaurant, Category } from "@/types";
import { imageFor } from "@/lib/menu-images";

// --- DUMMY IN-MEMORY DATA ---


import { apiClient, API_BASE_URL } from "./apiClient";

let mockRestaurants: Restaurant[] = [
  {
    id: "29543ada-19bd-439e-a4b9-248aa43e58b5", // Update the ID to match our db seed!
    slug: "saffron-sage",
    name: "Saffron & Sage",
    tagline: "Modern Indian Kitchen",
  },
];

let mockOrders: Order[] = [
  {
    id: "ORD-2041",
    order_number: 2041,
    table_number: 4,
    table_id: "table1",
    items: [
      { id: "item1", menu_item_id: "m4", name: "Grilled Atlantic Salmon", qty: 2, price: 580 },
      { id: "item2", menu_item_id: "m6", name: "Mint Lemonade", qty: 2, price: 150 },
    ],
    subtotal: 1460, tax: 73, discount: 0, total: 1533,
    status: "preparing",
    placed_at: new Date(Date.now() - 6 * 60_000).toISOString(),
  },
  {
    id: "ORD-2042",
    order_number: 2042,
    table_number: 9,
    table_id: "table2",
    items: [
      { id: "item3", menu_item_id: "m3", name: "Wild Mushroom Risotto", qty: 1, price: 420 },
      { id: "item4", menu_item_id: "m8", name: "Molten Chocolate Cake", qty: 1, price: 260 },
    ],
    subtotal: 680, tax: 34, discount: 0, total: 714,
    status: "placed",
    placed_at: new Date(Date.now() - 2 * 60_000).toISOString(),
  },
];

let nextOrderNumber = 2045;

const delay = (ms: number) => new Promise(res => setTimeout(res, ms));

export async function getRestaurantBySlug(slug: string): Promise<Restaurant> {
  await delay(300);
  const rest = mockRestaurants.find(r => r.slug === slug);
  if (!rest) {
    throw new Error("Restaurant not found");
  }
  return { ...rest };
}

function getFullImageUrl(url: string | null | undefined): string | undefined {
  if (!url) return undefined;
  if (url.startsWith('http')) return url;
  const baseUrl = API_BASE_URL.replace(/\/api$/, '');
  return `${baseUrl}${url}`;
}

export async function getMenu(restaurantId: string): Promise<MenuItem[]> {
  const response = await apiClient<any>(`/restaurants/${restaurantId}/menu`);
  const flatMenu: MenuItem[] = [];
  
  if (response && response.categories) {
    for (const cat of response.categories) {
      if (cat.menuItems) {
        for (const item of cat.menuItems) {
          flatMenu.push({
            id: item.id,
            name: item.name,
            description: item.description,
            price: item.price,
            image: getFullImageUrl(item.imageUrl) || imageFor(item.name),
            category: cat.name,
            available: true, // DB only returns available items in this endpoint per our repository logic
            popular: false,
            special: false,
          });
        }
      }
    }
  }

  return flatMenu;
}

export async function getActiveOrders(restaurantId: string): Promise<Order[]> {
  const response = await apiClient<any[]>(`/restaurants/${restaurantId}/orders`);
  
  if (!response) return [];

  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();

  return response
    .map(dto => ({
      id: dto.id,
      order_number: dto.orderNumber,
      group_label: dto.groupLabel, dining_group_id:dto.diningGroupId,
    table_number: dto.tableNumber,
      table_id: dto.tableId,
      status: dto.status.toLowerCase() as OrderStatus,
      subtotal: dto.subtotal,
      tax: dto.tax,
      discount: dto.discount,
      total: dto.totalAmount,
      placed_at: dto.createdAt,
      items: dto.items.map((item: any) => ({
        id: item.id,
        menu_item_id: item.menuItemId,
        name: item.name,
        qty: item.quantity,
        price: item.unitPrice
      })),
      exception_reason: dto.exceptionReason
    }))
    .filter(order => {
      const isCompleted = ["served", "cancelled", "rejected"].includes(order.status);
      if (!isCompleted) return true; // keep all active orders
      const orderDate = new Date(order.placed_at).getTime();
      return orderDate >= todayStart; // keep completed orders only if they are from today
    });
}

export async function updateOrderStatus(orderId: string, status: OrderStatus, restaurantId: string, exceptionReason?: string): Promise<void> {
  const request = {
    restaurantId: restaurantId,
    status: status.charAt(0).toUpperCase() + status.slice(1), // e.g., "preparing" -> "Preparing"
    exceptionReason
  };

  await apiClient<any>(`/orders/${orderId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function placeOrder(qrToken: string, items: { item: MenuItem; qty: number }[], diningGroupId?: string): Promise<{ orderId: string }> {
  const payload = {
    qrToken, diningGroupId,
    items: items.map((i) => ({ menuItemId: i.item.id, quantity: i.qty })),
  };
  return apiClient<{ orderId: string }>(`/orders`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function getOrderById(restaurantId: string, orderId: string): Promise<Order> {
  const res = await apiClient<any>(`/restaurants/${restaurantId}/orders/${orderId}`);
  if (!res) throw new Error("Order not found");
  
  return {
    id: res.id,
    order_number: res.orderNumber,
    group_label: res.groupLabel, dining_group_id:res.diningGroupId,
    table_number: res.tableNumber,
    table_id: res.tableId,
    status: res.status.toLowerCase() as OrderStatus,
    subtotal: res.subtotal,
    tax: res.tax,
    discount: res.discount,
    total: res.totalAmount,
    placed_at: res.createdAt,
    items: res.items.map((item: any) => ({
      id: item.id,
      menu_item_id: item.menuItemId,
      name: item.name,
      qty: item.quantity,
      price: item.unitPrice
    })),
    exception_reason: res.exceptionReason
  };
}

export async function updateMenuItem(itemId: string, updates: Partial<MenuItem>): Promise<void> {
  if (updates.available !== undefined && Object.keys(updates).length === 1) {
    // Only updating availability
    await apiClient<any>(`/menu-items/${itemId}/availability`, {
      method: 'PATCH',
      body: JSON.stringify({ isAvailable: updates.available })
    });
    return;
  }

  // Find the category ID from the category name (needs a lookup if we only have the name)
  // But wait, the frontend currently doesn't map category ID on updates if we only have the name string. 
  // Let's implement full API methods for admin.
}

export async function getAdminMenu(restaurantId: string): Promise<{categories: any[], flatMenu: MenuItem[]}> {
  const response = await apiClient<any>(`/restaurants/${restaurantId}/menu/admin`);
  const flatMenu: MenuItem[] = [];
  const categories = response?.categories || [];
  
  if (categories) {
    for (const cat of categories) {
      if (cat.menuItems) {
        for (const item of cat.menuItems) {
          flatMenu.push({
            id: item.id,
            name: item.name,
            description: item.description,
            price: item.price,
            image: getFullImageUrl(item.imageUrl) || imageFor(item.name),
            category: cat.name,
            categoryId: cat.id, // we should pass categoryId for edits!
            available: item.isAvailable,
            popular: false,
            special: false,
          } as any);
        }
      }
    }
  }

  return { categories, flatMenu };
}

export async function createCategory(restaurantId: string, name: string): Promise<any> {
  return await apiClient<any>(`/restaurants/${restaurantId}/categories`, {
    method: 'POST',
    body: JSON.stringify({ name })
  });
}

export async function updateMenuItemAvailability(menuItemId: string, isAvailable: boolean): Promise<void> {
  await apiClient<any>(`/menu-items/${menuItemId}/availability`, {
    method: 'PATCH',
    body: JSON.stringify({ isAvailable })
  });
}

// Tables & QR Management API

export interface Table {
  id: string;
  restaurantId: string;
  tableNumber: number;
  qrToken: string;
  isActive: boolean;
}

export interface QRResolution {
  restaurantId: string;
  restaurantName: string;
  restaurantTagline: string;
  tableId: string;
  diningGroups: {id:string;label:string;tableId:string}[];
  tableNameOrNumber: string;
  isAcceptingOrders: boolean;
}

export async function resolveQRToken(qrToken: string): Promise<QRResolution> {
  return apiClient<QRResolution>(`/qr/${qrToken}`);
}

export async function getActiveOrderForTable(qrToken: string,diningGroupId?: string): Promise<any> {
  return await apiClient<any>(`/qr/${qrToken}/active-order${diningGroupId?`?diningGroupId=${diningGroupId}`:""}`);
}

export async function getTables(restaurantId: string): Promise<Table[]> {
  return apiClient<Table[]>(`/restaurants/${restaurantId}/tables`);
}

export async function createTable(restaurantId: string, tableNumber: number): Promise<Table> {
  return apiClient<Table>(`/restaurants/${restaurantId}/tables`, {
    method: "POST",
    body: JSON.stringify({ tableNumber }),
  });
}

export async function updateTable(restaurantId: string, tableId: string, data: { tableNumber: number; isActive: boolean }): Promise<Table> {
  return apiClient<Table>(`/restaurants/${restaurantId}/tables/${tableId}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });
}

export async function regenerateTableQR(restaurantId: string, tableId: string): Promise<Table> {
  return apiClient<Table>(`/restaurants/${restaurantId}/tables/${tableId}/regenerate-qr`, {
    method: "POST"
  });
}

export async function updateCategory(categoryId: string, name: string): Promise<any> {
  return await apiClient<any>(`/categories/${categoryId}`, {
    method: 'PUT',
    body: JSON.stringify({ name })
  });
}

export async function deleteCategory(categoryId: string): Promise<void> {
  await apiClient<any>(`/categories/${categoryId}`, {
    method: 'DELETE'
  });
}

export async function createMenuItemApi(restaurantId: string, categoryId: string, item: Partial<MenuItem> & { imageFile?: File }): Promise<any> {
  const formData = new FormData();
  formData.append('categoryId', categoryId);
  if (item.name) formData.append('name', item.name);
  if (item.description) formData.append('description', item.description);
  if (item.price !== undefined) formData.append('price', item.price.toString());
  formData.append('isAvailable', (item.available ?? true).toString());
  if (item.imageFile) {
    formData.append('imageFile', item.imageFile);
  } else if (item.image) {
    formData.append('imageUrl', item.image);
  }

  return await apiClient<any>(`/restaurants/${restaurantId}/menu-items`, {
    method: 'POST',
    body: formData
  });
}

export async function updateMenuItemApi(itemId: string, categoryId: string, item: Partial<MenuItem> & { imageFile?: File }): Promise<any> {
  const formData = new FormData();
  formData.append('categoryId', categoryId);
  if (item.name) formData.append('name', item.name);
  if (item.description) formData.append('description', item.description);
  if (item.price !== undefined) formData.append('price', item.price.toString());
  if (item.imageFile) {
    formData.append('imageFile', item.imageFile);
  } else if (item.image) {
    formData.append('imageUrl', item.image);
  }

  return await apiClient<any>(`/menu-items/${itemId}`, {
    method: 'PUT',
    body: formData
  });
}

export async function deleteMenuItemApi(itemId: string): Promise<void> {
  await apiClient<any>(`/menu-items/${itemId}`, {
    method: 'DELETE'
  });
}

// Staff & Employee Management API

export interface Employee {
  id: string;
  restaurantId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isActive?: boolean;
}

export async function getEmployees(): Promise<Employee[]> {
  return apiClient<Employee[]>('/employees');
}

export async function getEmployee(id: string): Promise<Employee> {
  return apiClient<Employee>(`/employees/${id}`);
}

export async function createEmployee(data: any): Promise<Employee> {
  return apiClient<Employee>('/employees', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function updateEmployee(id: string, data: any): Promise<Employee> {
  return apiClient<Employee>(`/employees/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export async function updateEmployeeStatus(id: string, isActive: boolean): Promise<any> {
  return apiClient<any>(`/employees/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive }),
  });
}

// Reporting API

export interface ReportOrderItemDto {
  itemNameSnapshot: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface ReportOrderDto {
  orderId: string;
  orderNumber: number;
  displayOrderNumber: string;
  tableId: string;
  tableName: string;
  createdAt: string;
  updatedAt: string;
  status: string;
  subtotal: number;
  tax: number;
  discount: number;
  totalAmount: number;
  exceptionReason?: string;
  items: ReportOrderItemDto[];
}

export interface PagedResponseDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export async function getReports(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResponseDto<ReportOrderDto>> {
  const searchParams = new URLSearchParams();
  if (params.startDate) searchParams.append("startDate", params.startDate);
  if (params.endDate) searchParams.append("endDate", params.endDate);
  if (params.status && params.status !== "all") searchParams.append("status", params.status);
  if (params.tableId && params.tableId !== "all") searchParams.append("tableId", params.tableId);
  if (params.search) searchParams.append("search", params.search);
  if (params.page) searchParams.append("page", params.page.toString());
  if (params.pageSize) searchParams.append("pageSize", params.pageSize.toString());

  return apiClient<PagedResponseDto<ReportOrderDto>>(`/reports/orders?${searchParams.toString()}`);
}

export async function exportOrderHistory(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
  search?: string;
}): Promise<void> {
  const searchParams = new URLSearchParams();
  if (params.startDate) searchParams.append("startDate", params.startDate);
  if (params.endDate) searchParams.append("endDate", params.endDate);
  if (params.status && params.status !== "all") searchParams.append("status", params.status);
  if (params.tableId && params.tableId !== "all") searchParams.append("tableId", params.tableId);
  if (params.search) searchParams.append("search", params.search);

  const token = localStorage.getItem("tablo_token");
  const headers: Record<string, string> = {};
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}/reports/orders/export?${searchParams.toString()}`, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    throw new Error("Failed to export Excel report.");
  }

  const blob = await response.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = downloadUrl;
  
  // Extract filename from Content-Disposition if possible, otherwise fallback
  const contentDisposition = response.headers.get("Content-Disposition");
  let fileName = `Tablo_Order_Report_${new Date().toISOString().split('T')[0]}.xlsx`;
  if (contentDisposition && contentDisposition.includes("filename=")) {
    const filenameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
    if (filenameMatch && filenameMatch.length > 1) {
      fileName = filenameMatch[1];
    }
  }
  
  link.setAttribute("download", fileName);
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(downloadUrl);
}

export interface AnalyticsSummaryDto {
  totalRevenue: number;
  totalOrders: number;
  totalItemsSold: number;
  averageOrderValue: number;
}

export interface SalesOverTimeDto {
  date: string;
  revenue: number;
  orderCount: number;
}

export interface TopItemDto {
  itemName: string;
  quantitySold: number;
  revenue: number;
}

export async function getAnalyticsSummary(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}): Promise<AnalyticsSummaryDto> {
  const searchParams = new URLSearchParams();
  if (params.startDate) searchParams.append("startDate", params.startDate);
  if (params.endDate) searchParams.append("endDate", params.endDate);
  if (params.status && params.status !== "all") searchParams.append("status", params.status);
  if (params.tableId && params.tableId !== "all") searchParams.append("tableId", params.tableId);

  return apiClient<AnalyticsSummaryDto>(`/reports/summary?${searchParams.toString()}`);
}

export async function getSalesOverTime(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}): Promise<SalesOverTimeDto[]> {
  const searchParams = new URLSearchParams();
  if (params.startDate) searchParams.append("startDate", params.startDate);
  if (params.endDate) searchParams.append("endDate", params.endDate);
  if (params.status && params.status !== "all") searchParams.append("status", params.status);
  if (params.tableId && params.tableId !== "all") searchParams.append("tableId", params.tableId);

  return apiClient<SalesOverTimeDto[]>(`/reports/sales-over-time?${searchParams.toString()}`);
}

export async function getTopItems(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}): Promise<TopItemDto[]> {
  const searchParams = new URLSearchParams();
  if (params.startDate) searchParams.append("startDate", params.startDate);
  if (params.endDate) searchParams.append("endDate", params.endDate);
  if (params.status && params.status !== "all") searchParams.append("status", params.status);
  if (params.tableId && params.tableId !== "all") searchParams.append("tableId", params.tableId);

  return apiClient<TopItemDto[]>(`/reports/top-items?${searchParams.toString()}`);
}

// Settings API

export interface RestaurantSettings {
  isAcceptingOrders: boolean;
}

export async function getSettings(restaurantId: string): Promise<RestaurantSettings> {
  return apiClient<RestaurantSettings>(`/restaurants/${restaurantId}/settings`);
}

export async function updateAcceptingOrders(restaurantId: string, isAcceptingOrders: boolean): Promise<RestaurantSettings> {
  return apiClient<RestaurantSettings>(`/restaurants/${restaurantId}/settings/accepting-orders`, {
    method: 'PATCH',
    body: JSON.stringify({ isAcceptingOrders })
  });
}

// Service Requests API

import { ServiceRequest } from "@/types";

export async function createServiceRequest(qrToken: string, type: string,diningGroupId?:string): Promise<ServiceRequest> {
  return apiClient<ServiceRequest>(`/ServiceRequests/qr/${qrToken}`, {
    method: 'POST',
    body: JSON.stringify({ type,diningGroupId })
  });
}

export async function getActiveServiceRequests(): Promise<ServiceRequest[]> {
  return apiClient<ServiceRequest[]>('/ServiceRequests');
}

export async function getTableActiveServiceRequests(qrToken: string,diningGroupId?:string): Promise<ServiceRequest[]> {
  return apiClient<ServiceRequest[]>(`/ServiceRequests/qr/${qrToken}${diningGroupId?`?diningGroupId=${diningGroupId}`:""}`);
}

export async function acknowledgeServiceRequest(id: string): Promise<ServiceRequest> {
  return apiClient<ServiceRequest>(`/ServiceRequests/${id}/acknowledge`, {
    method: 'PATCH'
  });
}

export async function completeServiceRequest(id: string): Promise<ServiceRequest> {
  return apiClient<ServiceRequest>(`/ServiceRequests/${id}/complete`, {
    method: 'PATCH'
  });
}



