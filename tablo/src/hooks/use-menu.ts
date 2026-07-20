import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getMenu, updateMenuItem } from "@/services/api";
import { MenuItem } from "@/types";

export function useMenu(restaurantId: string | null) {
  const queryKey = ["menu", restaurantId];

  const query = useQuery({
    queryKey,
    queryFn: () => getMenu(restaurantId!),
    enabled: !!restaurantId,
    // Polling could be added here for a pseudo-realtime feel,
    // e.g. refetchInterval: 5000 
    staleTime: 1000 * 60 * 5, // 5 minutes
  });

  return query;
}

export function useAdminMenu(restaurantId: string | null) {
  const queryKey = ["adminMenu", restaurantId];

  return useQuery({
    queryKey,
    queryFn: () => import("@/services/api").then(m => m.getAdminMenu(restaurantId!)),
    enabled: !!restaurantId,
  });
}

export function useUpdateMenuItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ itemId, categoryId, updates }: { itemId: string; categoryId?: string; updates: Partial<MenuItem> }) => {
      // The original api.ts has a simpler updateMenuItem for availability toggle
      if (updates.available !== undefined && Object.keys(updates).length === 1) {
        return updateMenuItem(itemId, updates);
      }
      return import("@/services/api").then(m => m.updateMenuItemApi(itemId, categoryId!, updates));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}

export function useCreateMenuItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restaurantId, categoryId, item }: { restaurantId: string; categoryId: string; item: Partial<MenuItem> }) =>
      import("@/services/api").then(m => m.createMenuItemApi(restaurantId, categoryId, item)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}

export function useDeleteMenuItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (itemId: string) => import("@/services/api").then(m => m.deleteMenuItemApi(itemId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}

export function useCreateCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restaurantId, name }: { restaurantId: string; name: string }) =>
      import("@/services/api").then(m => m.createCategory(restaurantId, name)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}

export function useUpdateCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ categoryId, name }: { categoryId: string; name: string }) =>
      import("@/services/api").then(m => m.updateCategory(categoryId, name)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}

export function useDeleteCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (categoryId: string) => import("@/services/api").then(m => m.deleteCategory(categoryId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["menu"] });
      queryClient.invalidateQueries({ queryKey: ["adminMenu"] });
    },
  });
}
