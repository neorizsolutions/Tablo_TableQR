import { apiClient } from './apiClient';

export interface KitchenStation {
  id: string;
  restaurantId: string;
  outletId?: string;
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
  printerId?: string;
  printerName?: string;
  displayOrder: number;
  assignedCategoryCount: number;
  assignedMenuItemCount: number;
  createdAtUtc: string;
}

export interface CreateKitchenStationDto {
  name: string;
  code: string;
  isDefault: boolean;
  printerId?: string;
  displayOrder?: number;
}

export interface UpdateKitchenStationDto {
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
  printerId?: string;
  displayOrder: number;
}

export interface ThermalPrinter {
  id: string;
  restaurantId: string;
  outletId?: string;
  name: string;
  connectionType: 'NetworkTcp' | 'LocalAgentQueue';
  ipAddress?: string;
  port: number;
  localQueueName?: string;
  paperWidthMm: number;
  charactersPerLine: number;
  status: 'Online' | 'Offline' | 'PaperOut' | 'Error' | 'Unknown';
  lastSeenAtUtc?: string;
  lastStatusMessage?: string;
  isActive: boolean;
  assignedStationNames: string[];
  createdAtUtc: string;
}

export interface CreateThermalPrinterDto {
  name: string;
  connectionType: 'NetworkTcp' | 'LocalAgentQueue';
  ipAddress?: string;
  port?: number;
  localQueueName?: string;
  paperWidthMm: number;
  charactersPerLine?: number;
}

export interface UpdateThermalPrinterDto {
  name: string;
  connectionType: 'NetworkTcp' | 'LocalAgentQueue';
  ipAddress?: string;
  port?: number;
  localQueueName?: string;
  paperWidthMm: number;
  charactersPerLine?: number;
  isActive: boolean;
}

export interface PrintJob {
  id: string;
  kotTicketId?: string;
  ticketNumber?: string;
  printerId: string;
  printerName: string;
  stationId?: string;
  stationName?: string;
  jobType: string;
  status: 'Pending' | 'Claimed' | 'Sent' | 'Printed' | 'Failed' | 'UncertainDelivery' | 'Cancelled';
  paperWidthMm: number;
  attempts: number;
  lastAttemptUtc?: string;
  lastError?: string;
  createdBy: string;
  createdAtUtc: string;
  completedAtUtc?: string;
  readableText: string;
}

export interface RoutingPreviewItem {
  menuItemId: string;
  itemName: string;
  categoryName: string;
  quantity: number;
  resolvedBy: string;
}

export interface StationRoutingPreview {
  stationId: string;
  stationName: string;
  stationCode: string;
  assignedPrinterName?: string;
  items: RoutingPreviewItem[];
}

export interface RoutingPreviewResponse {
  kitchenSetupMode: string;
  stationTickets: StationRoutingPreview[];
}

export interface PrintAgent {
  id: string;
  agentName: string;
  keyPrefix: string;
  isActive: boolean;
  lastHeartbeatUtc?: string;
  version?: string;
  createdAtUtc: string;
}

export interface PrintAgentRegisteredResult {
  agentId: string;
  agentName: string;
  plaintextApiKey: string;
  keyPrefix: string;
  createdAtUtc: string;
}

export interface KitchenPrintingSettings {
  isAcceptingOrders: boolean;
  kotPrintingEnabled: boolean;
  kitchenSetupMode: 'SingleKitchen' | 'SeparateFoodBeverage' | 'CustomMultiStation';
  defaultKitchenStationId?: string;
  defaultKitchenStationName?: string;
}

export const printingService = {
  // Kitchen Stations
  async getStations(restaurantId: string): Promise<KitchenStation[]> {
    return apiClient<KitchenStation[]>(`/restaurants/${restaurantId}/kitchen-stations`);
  },

  async createStation(restaurantId: string, dto: CreateKitchenStationDto): Promise<KitchenStation> {
    return apiClient<KitchenStation>(`/restaurants/${restaurantId}/kitchen-stations`, {
      method: 'POST',
      body: JSON.stringify(dto)
    });
  },

  async updateStation(restaurantId: string, stationId: string, dto: UpdateKitchenStationDto): Promise<KitchenStation> {
    return apiClient<KitchenStation>(`/restaurants/${restaurantId}/kitchen-stations/${stationId}`, {
      method: 'PUT',
      body: JSON.stringify(dto)
    });
  },

  async deactivateStation(restaurantId: string, stationId: string): Promise<void> {
    return apiClient<void>(`/restaurants/${restaurantId}/kitchen-stations/${stationId}`, {
      method: 'DELETE'
    });
  },

  async consolidateStations(restaurantId: string, targetDefaultStationId: string, reason: string): Promise<any> {
    return apiClient<any>(`/restaurants/${restaurantId}/kitchen-stations/consolidate`, {
      method: 'POST',
      body: JSON.stringify({ targetDefaultStationId, reason })
    });
  },

  async previewRouting(restaurantId: string, items: { menuItemId: string; quantity: number }[]): Promise<RoutingPreviewResponse> {
    return apiClient<RoutingPreviewResponse>(`/restaurants/${restaurantId}/kitchen-stations/routing-preview`, {
      method: 'POST',
      body: JSON.stringify({ items })
    });
  },

  // Printers
  async getPrinters(restaurantId: string): Promise<ThermalPrinter[]> {
    return apiClient<ThermalPrinter[]>(`/restaurants/${restaurantId}/printers`);
  },

  async createPrinter(restaurantId: string, dto: CreateThermalPrinterDto): Promise<ThermalPrinter> {
    return apiClient<ThermalPrinter>(`/restaurants/${restaurantId}/printers`, {
      method: 'POST',
      body: JSON.stringify(dto)
    });
  },

  async updatePrinter(restaurantId: string, printerId: string, dto: UpdateThermalPrinterDto): Promise<ThermalPrinter> {
    return apiClient<ThermalPrinter>(`/restaurants/${restaurantId}/printers/${printerId}`, {
      method: 'PUT',
      body: JSON.stringify(dto)
    });
  },

  async deletePrinter(restaurantId: string, printerId: string): Promise<void> {
    return apiClient<void>(`/restaurants/${restaurantId}/printers/${printerId}`, {
      method: 'DELETE'
    });
  },

  async testPrint(restaurantId: string, printerId: string): Promise<{ jobId: string; message: string; readablePreview: string }> {
    return apiClient<{ jobId: string; message: string; readablePreview: string }>(`/restaurants/${restaurantId}/printers/${printerId}/test-print`, {
      method: 'POST'
    });
  },

  // Print Jobs & Operations
  async getPrintJobs(restaurantId: string, filters?: { stationId?: string; printerId?: string; status?: string; limit?: number }): Promise<PrintJob[]> {
    const params = new URLSearchParams();
    if (filters?.stationId) params.append('stationId', filters.stationId);
    if (filters?.printerId) params.append('printerId', filters.printerId);
    if (filters?.status) params.append('status', filters.status);
    if (filters?.limit) params.append('limit', filters.limit.toString());

    return apiClient<PrintJob[]>(`/restaurants/${restaurantId}/printers/jobs?${params.toString()}`);
  },

  async retryJob(restaurantId: string, jobId: string): Promise<{ message: string; jobId: string }> {
    return apiClient<{ message: string; jobId: string }>(`/restaurants/${restaurantId}/printers/jobs/${jobId}/retry`, {
      method: 'POST'
    });
  },

  async cancelJob(restaurantId: string, jobId: string): Promise<{ message: string; jobId: string }> {
    return apiClient<{ message: string; jobId: string }>(`/restaurants/${restaurantId}/printers/jobs/${jobId}/cancel`, {
      method: 'POST'
    });
  },

  async reprintTicket(restaurantId: string, ticketId: string, reason: string): Promise<any> {
    return apiClient<any>(`/restaurants/${restaurantId}/printers/tickets/${ticketId}/reprint`, {
      method: 'POST',
      body: JSON.stringify({ reason })
    });
  },

  // Agents
  async registerAgent(restaurantId: string, agentName: string): Promise<PrintAgentRegisteredResult> {
    return apiClient<PrintAgentRegisteredResult>(`/restaurants/${restaurantId}/printers/agents/register`, {
      method: 'POST',
      body: JSON.stringify({ agentName })
    });
  },

  async getAgents(restaurantId: string): Promise<PrintAgent[]> {
    return apiClient<PrintAgent[]>(`/restaurants/${restaurantId}/printers/agents`);
  },

  async revokeAgent(restaurantId: string, agentId: string): Promise<void> {
    return apiClient<void>(`/restaurants/${restaurantId}/printers/agents/${agentId}`, {
      method: 'DELETE'
    });
  },

  // Settings
  async getSettings(restaurantId: string): Promise<KitchenPrintingSettings> {
    return apiClient<KitchenPrintingSettings>(`/restaurants/${restaurantId}/settings`);
  },

  async updateKitchenPrintingSettings(restaurantId: string, settings: { kotPrintingEnabled: boolean; kitchenSetupMode: string; defaultKitchenStationId?: string }): Promise<KitchenPrintingSettings> {
    return apiClient<KitchenPrintingSettings>(`/restaurants/${restaurantId}/settings/kitchen-printing`, {
      method: 'PUT',
      body: JSON.stringify(settings)
    });
  }
};
