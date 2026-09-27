import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { 
  getActiveServiceRequests, 
  acknowledgeServiceRequest, 
  completeServiceRequest,
  createServiceRequest,
  getTableActiveServiceRequests
} from "@/services/api";
import { useEffect } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { API_BASE_URL } from "@/services/apiClient";
import { ServiceRequest } from "@/types";
import { toast } from "sonner";

export function useActiveServiceRequests(restaurantId: string | null) {
  const queryClient = useQueryClient();
  const queryKey = ["serviceRequests", restaurantId];

  const query = useQuery({
    queryKey,
    queryFn: () => getActiveServiceRequests(),
    enabled: !!restaurantId,
    staleTime: 1000 * 60, // 1 minute
  });

  useEffect(() => {
    if (!restaurantId) return;
    
    const hubUrl = API_BASE_URL.replace(/\/api\/?$/, "/hubs/order");
    const token = localStorage.getItem("tablo_token");
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token || ""
      })
      .withAutomaticReconnect()
      .build();

    connection.on("NewServiceRequest", (sr: ServiceRequest) => {
      queryClient.setQueryData(queryKey, (oldData: ServiceRequest[] | undefined) => {
        if (!oldData) return [sr];
        // Ensure no duplicates
        if (oldData.some(existing => existing.id === sr.id)) return oldData;
        
        toast.info(`New Service Request: Table ${sr.tableName} requested ${sr.type === 'CallWaiter' ? 'a waiter' : 'the bill'}!`);
        return [...oldData, sr];
      });
    });

    connection.on("ServiceRequestUpdated", (sr: ServiceRequest) => {
      queryClient.setQueryData(queryKey, (oldData: ServiceRequest[] | undefined) => {
        if (!oldData) return oldData;
        if (sr.status === "Completed" || sr.status === "Cancelled") {
          return oldData.filter(existing => existing.id !== sr.id);
        }
        return oldData.map(existing => existing.id === sr.id ? sr : existing);
      });
    });

    connection.start().then(() => {
      connection.invoke("JoinRestaurantGroup").catch(err => console.error("SignalR JoinGroup error:", err));
    }).catch(err => console.error("SignalR SR connection error:", err));

    return () => {
      connection.stop();
    };
  }, [restaurantId, queryClient, queryKey]);

  return query;
}

export function useManageServiceRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, action }: { id: string, action: "acknowledge" | "complete" }) => {
      if (action === "acknowledge") return acknowledgeServiceRequest(id);
      return completeServiceRequest(id);
    },
    onSuccess: (data, variables) => {
      // Invalidate or update query
      queryClient.invalidateQueries({ queryKey: ["serviceRequests"] });
    },
  });
}

export function useCreateServiceRequest(qrToken: string | null) {
  return useMutation({
    mutationFn: async (type: string) => {
      if (!qrToken) throw new Error("No QR token provided");
      return createServiceRequest(qrToken, type);
    },
    onSuccess: () => {
      toast.success("Request sent to staff!");
    },
    onError: (error: any) => {
      toast.error(error?.message || "Failed to send request. Waiter may already be notified.");
    }
  });
}

export function useTableActiveServiceRequests(qrToken: string | null) {
  return useQuery({
    queryKey: ["serviceRequests", "table", qrToken],
    queryFn: () => getTableActiveServiceRequests(qrToken!),
    enabled: !!qrToken,
    refetchInterval: 15000, // Poll every 15 seconds
  });
}
