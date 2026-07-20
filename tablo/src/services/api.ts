import { MenuItem, Order, OrderStatus, Restaurant, Category } from "@/types";
import { imageFor } from "@/lib/menu-images";

// --- DUMMY IN-MEMORY DATA ---


import { apiClient } from "./apiClient";

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
    items: [
      { id: "item1", menu_item_id: "m4", name: "Grilled Atlantic Salmon", qty: 2, price: 580 },
      { id: "item2", menu_item_id: "m6", name: "Mint Lemonade", qty: 2, price: 150 },
    ],
    total: 1460,
    status: "preparing",
    placed_at: new Date(Date.now() - 6 * 60_000).toISOString(),
  },
  {
    id: "ORD-2042",
    order_number: 2042,
    table_number: 9,
    items: [
      { id: "item3", menu_item_id: "m3", name: "Wild Mushroom Risotto", qty: 1, price: 420 },
      { id: "item4", menu_item_id: "m8", name: "Molten Chocolate Cake", qty: 1, price: 260 },
    ],
    total: 680,
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
            image: item.imageUrl || imageFor(item.name),
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

  return response.map(dto => ({
    id: dto.id,
    order_number: dto.orderNumber,
    table_number: dto.tableNumber,
    table_id: dto.tableId,
    status: dto.status.toLowerCase() as OrderStatus,
    total: dto.totalAmount,
    placed_at: dto.createdAt,
    items: dto.items.map((item: any) => ({
      id: item.id,
      menu_item_id: item.menuItemId,
      name: item.name,
      qty: item.quantity,
      price: item.unitPrice
    }))
  }));
}

export async function updateOrderStatus(orderId: string, status: OrderStatus): Promise<void> {
  const request = {
    // We send restaurantId since the backend requires it for UpdateOrderStatusRequestDto
    restaurantId: mockRestaurants[0].id, 
    status: status.charAt(0).toUpperCase() + status.slice(1) // e.g., "preparing" -> "Preparing"
  };

  await apiClient<any>(`/orders/${orderId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function placeOrder(qrToken: string, items: { item: MenuItem; qty: number }[]): Promise<{ orderId: string }> {
  const payload = {
    qrToken,
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
    table_number: res.tableNumber,
    table_id: res.tableId,
    status: res.status.toLowerCase() as OrderStatus,
    total: res.totalAmount,
    placed_at: res.createdAt,
    items: res.items.map((item: any) => ({
      id: item.id,
      menu_item_id: item.menuItemId,
      name: item.name,
      qty: item.quantity,
      price: item.unitPrice
    }))
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
            image: item.imageUrl || imageFor(item.name),
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
  nameOrNumber: string;
  qrToken: string;
  isActive: boolean;
}

export interface QRResolution {
  restaurantId: string;
  restaurantName: string;
  restaurantTagline: string;
  tableId: string;
  tableNameOrNumber: string;
}

export async function resolveQRToken(qrToken: string): Promise<QRResolution> {
  return apiClient<QRResolution>(`/qr/${qrToken}`);
}

export async function getActiveOrderForTable(restaurantId: string, tableId: string): Promise<any> {
  return await apiClient<any>(`/restaurants/${restaurantId}/tables/${tableId}/active-order`);
}

export async function getTables(restaurantId: string): Promise<Table[]> {
  return apiClient<Table[]>(`/restaurants/${restaurantId}/tables`);
}

export async function createTable(restaurantId: string, nameOrNumber: string): Promise<Table> {
  return apiClient<Table>(`/restaurants/${restaurantId}/tables`, {
    method: "POST",
    body: JSON.stringify({ nameOrNumber }),
  });
}

export async function updateTable(restaurantId: string, tableId: string, data: { nameOrNumber: string; isActive: boolean }): Promise<Table> {
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

export async function createMenuItemApi(restaurantId: string, categoryId: string, item: Partial<MenuItem>): Promise<any> {
  return await apiClient<any>(`/restaurants/${restaurantId}/menu-items`, {
    method: 'POST',
    body: JSON.stringify({
      categoryId,
      name: item.name,
      description: item.description,
      price: item.price,
      isAvailable: item.available ?? true,
      imageUrl: item.image
    })
  });
}

export async function updateMenuItemApi(itemId: string, categoryId: string, item: Partial<MenuItem>): Promise<any> {
  return await apiClient<any>(`/menu-items/${itemId}`, {
    method: 'PUT',
    body: JSON.stringify({
      categoryId,
      name: item.name,
      description: item.description,
      price: item.price,
      imageUrl: item.image
    })
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

