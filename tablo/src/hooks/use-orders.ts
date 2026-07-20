import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getActiveOrders, getOrderById, updateOrderStatus } from "@/services/api";
import { OrderStatus } from "@/types";
import { useEffect } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { API_BASE_URL } from "@/services/apiClient";

export function useOrders(restaurantId: string | null) {
  const queryClient = useQueryClient();
  const queryKey = ["orders", restaurantId];

  const query = useQuery({
    queryKey,
    queryFn: () => getActiveOrders(restaurantId!),
    enabled: !!restaurantId,
    staleTime: 1000 * 60, // 1 minute
  });

  useEffect(() => {
    if (!restaurantId) return;
    
    const hubUrl = API_BASE_URL.replace(/\/api\/?$/, "/hubs/order");
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.on("OrderStatusUpdated", () => {
      queryClient.invalidateQueries({ queryKey });
    });

    connection.start().catch(err => console.error("SignalR connection error:", err));

    return () => {
      connection.stop();
    };
  }, [restaurantId, queryClient, queryKey]);

  return query;
}

export function useUpdateOrderStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ orderId, status }: { orderId: string; status: OrderStatus }) =>
      updateOrderStatus(orderId, status),
    onSuccess: () => {
      // Invalidate queries so the UI fetches the updated in-memory data immediately
      queryClient.invalidateQueries({ queryKey: ["orders"] });
      queryClient.invalidateQueries({ queryKey: ["order"] });
    },
  });
}

export function useOrderTracking(restaurantId: string | null, orderId: string | null) {
  const queryClient = useQueryClient();
  const queryKey = ["order", orderId];

  const query = useQuery({
    queryKey,
    queryFn: async () => {
      if (!restaurantId || !orderId) throw new Error("Missing params");
      const order = await getOrderById(restaurantId, orderId);
      return order;
    },
    enabled: !!orderId && !!restaurantId,
  });

  useEffect(() => {
    if (!orderId) return;
    
    const hubUrl = API_BASE_URL.replace(/\/api\/?$/, "/hubs/order");
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.on("OrderStatusUpdated", (updatedOrderId: string, newStatus: string) => {
      if (updatedOrderId.toLowerCase() === orderId.toLowerCase()) {
        queryClient.setQueryData(queryKey, (oldData: any) => {
          if (!oldData) return oldData;
          return { ...oldData, status: newStatus.toLowerCase() as OrderStatus };
        });
      }
    });

    connection.start().catch(err => console.error("SignalR connection error:", err));

    return () => {
      connection.stop();
    };
  }, [orderId, queryClient, queryKey]);

  return query;
}
