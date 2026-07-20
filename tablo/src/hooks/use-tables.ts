import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getTables, createTable, updateTable, regenerateTableQR, Table } from '../services/api';
import { toast } from 'sonner';

export function useTables(restaurantId: string | null) {
  return useQuery({
    queryKey: ['tables', restaurantId],
    queryFn: () => getTables(restaurantId!),
    enabled: !!restaurantId,
  });
}

export function useCreateTable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restaurantId, nameOrNumber }: { restaurantId: string; nameOrNumber: string }) => createTable(restaurantId, nameOrNumber),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['tables', variables.restaurantId] });
      toast.success("Table added successfully");
    },
    onError: (error: Error) => {
      toast.error(error.message || "Failed to add table");
    }
  });
}

export function useUpdateTable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restaurantId, tableId, data }: { restaurantId: string; tableId: string; data: { nameOrNumber: string; isActive: boolean } }) => updateTable(restaurantId, tableId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['tables', variables.restaurantId] });
      toast.success("Table updated");
    },
    onError: (error: Error) => {
      toast.error(error.message || "Failed to update table");
    }
  });
}

export function useRegenerateQR() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restaurantId, tableId }: { restaurantId: string; tableId: string }) => regenerateTableQR(restaurantId, tableId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['tables', variables.restaurantId] });
      toast.success("QR code regenerated securely");
    },
    onError: (error: Error) => {
      toast.error(error.message || "Failed to regenerate QR code");
    }
  });
}
