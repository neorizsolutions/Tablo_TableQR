import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getSettings, updateAcceptingOrders } from "@/services/api";

export function useSettings(restaurantId: string | undefined) {
  const queryClient = useQueryClient();

  const settingsQuery = useQuery({
    queryKey: ["settings", restaurantId],
    queryFn: () => getSettings(restaurantId!),
    enabled: !!restaurantId,
  });

  const updateAcceptingOrdersMutation = useMutation({
    mutationFn: (isAcceptingOrders: boolean) => updateAcceptingOrders(restaurantId!, isAcceptingOrders),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings", restaurantId] });
    },
  });

  return {
    settings: settingsQuery.data,
    isLoading: settingsQuery.isLoading,
    error: settingsQuery.error,
    updateAcceptingOrders: updateAcceptingOrdersMutation.mutateAsync,
    isUpdating: updateAcceptingOrdersMutation.isPending,
  };
}
