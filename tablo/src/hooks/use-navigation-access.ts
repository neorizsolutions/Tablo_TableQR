import { useEffect } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useAuth } from "./use-auth";
import { apiClient, API_BASE_URL } from "@/services/apiClient";
import { getNavigationAccess, type MenuAccess } from "@/services/navigation-access";

export type { MenuAccess };

export const getEmployeeMenuAccess = (id: string) =>
  apiClient<MenuAccess>(`/employees/${id}/menu-access`);

export const saveEmployeeMenuAccess = (id: string, allowedMenuIds: string[] | null) =>
  apiClient<MenuAccess>(`/employees/${id}/menu-access`, {
    method: "PUT",
    body: JSON.stringify({ allowedMenuIds }),
  });

export function useNavigationAccess() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const restaurantId = user?.restaurantId;
  const userId = user?.id;
  const enabled = !!restaurantId && user?.role !== "SuperAdmin";

  useEffect(() => {
    if (!enabled || !restaurantId || !userId) return;

    const hubUrl = API_BASE_URL.replace(/\/api\/?$/, "/hubs/order");
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem("tablo_token") || "",
      })
      .withAutomaticReconnect()
      .build();

    let disposed = false;

    const applyUpdate = (payload?: { allowedMenuIds?: string[] | null; reason?: string }) => {
      if (payload && "allowedMenuIds" in payload) {
        queryClient.setQueryData<MenuAccess>(
          ["navigation-access", restaurantId, userId],
          { allowedMenuIds: payload.allowedMenuIds ?? null },
        );
      }
      void queryClient.invalidateQueries({ queryKey: ["navigation-access", restaurantId, userId] });
      void queryClient.invalidateQueries({ queryKey: ["effective_access", restaurantId, userId] });
    };

    connection.on("PermissionsUpdated", (payload: { allowedMenuIds?: string[] | null; reason?: string }) => {
      applyUpdate(payload);
    });

    connection.on("AccessRevoked", (reason?: string) => {
      if (reason) {
        toast.info(reason);
      }
      void queryClient.invalidateQueries({ queryKey: ["navigation-access", restaurantId, userId] });
      void queryClient.invalidateQueries({ queryKey: ["effective_access", restaurantId, userId] });
    });

    const join = () => connection.invoke("JoinPermissionUpdates").catch(() => undefined);
    connection.onreconnected(() => {
      if (!disposed) {
        void join();
        void queryClient.invalidateQueries({ queryKey: ["navigation-access", restaurantId, userId] });
      }
    });

    void connection
      .start()
      .then(() => {
        if (!disposed) return join();
      })
      .catch(() => undefined);

    return () => {
      disposed = true;
      void connection.stop();
    };
  }, [enabled, queryClient, restaurantId, userId]);

  return useQuery({
    queryKey: ["navigation-access", restaurantId, userId],
    queryFn: getNavigationAccess,
    enabled,
    staleTime: 0,
    refetchOnWindowFocus: "always",
    refetchInterval: 15000,
    retry: false,
  });
}
