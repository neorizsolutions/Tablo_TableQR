import { useQuery } from "@tanstack/react-query";
import { useAuth } from "./use-auth";
import { apiClient } from "@/services/apiClient";

export interface MenuAccess {
  allowedMenuIds: string[] | null;
}
export const getEmployeeMenuAccess = (id: string) =>
  apiClient<MenuAccess>(`/employees/${id}/menu-access`);
export const saveEmployeeMenuAccess = (id: string, allowedMenuIds: string[] | null) =>
  apiClient<MenuAccess>(`/employees/${id}/menu-access`, {
    method: "PUT",
    body: JSON.stringify({ allowedMenuIds }),
  });

export function useNavigationAccess() {
  const { user } = useAuth();
  return useQuery({
    queryKey: ["navigation-access", user?.restaurantId, user?.id],
    queryFn: () => apiClient<MenuAccess>("/navigation-access"),
    enabled: !!user?.restaurantId && user.role !== "SuperAdmin",
    staleTime: 0,
    refetchOnWindowFocus: "always",
    refetchInterval: 15000,
    retry: 1,
  });
}
