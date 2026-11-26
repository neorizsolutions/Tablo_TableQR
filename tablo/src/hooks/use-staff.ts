import { useQuery, useMutation, useQueryClient, type QueryClient } from "@tanstack/react-query";
import {
  getEmployees,
  getEmployeeCounts,
  getEmployee,
  createEmployee,
  updateEmployee,
  updateEmployeeStatus,
  deleteEmployee,
  type EmployeeStatusFilter,
} from "@/services/api";
import { useAuth } from "./use-auth";

export function invalidateStaffCaches(queryClient: QueryClient) {
  queryClient.invalidateQueries({ queryKey: ["employees"] });
  queryClient.invalidateQueries({ queryKey: ["employee-counts"] });
  queryClient.invalidateQueries({ queryKey: ["menu-access-employees"] });
}

export function useStaff(status: EmployeeStatusFilter = "active") {
  const { user } = useAuth();
  return useQuery({
    queryKey: ["employees", user?.restaurantId, user?.id, status],
    queryFn: () => getEmployees(status),
    enabled: !!user?.restaurantId && (user.role === "Admin" || user.role === "Owner"),
  });
}

export function useEmployeeCounts() {
  const { user } = useAuth();
  return useQuery({
    queryKey: ["employee-counts", user?.restaurantId, user?.id],
    queryFn: getEmployeeCounts,
    enabled: !!user?.restaurantId && (user.role === "Admin" || user.role === "Owner"),
  });
}

export function useEmployee(id: string | null) {
  return useQuery({
    queryKey: ["employee", id],
    queryFn: () => getEmployee(id!),
    enabled: !!id,
  });
}

export function useCreateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createEmployee,
    onSuccess: () => {
      invalidateStaffCaches(queryClient);
    },
  });
}

export function useUpdateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => updateEmployee(id, data),
    onSuccess: () => {
      invalidateStaffCaches(queryClient);
    },
  });
}

export function useUpdateEmployeeStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => updateEmployeeStatus(id, isActive),
    onSuccess: () => {
      invalidateStaffCaches(queryClient);
    },
  });
}

export function useDeleteEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteEmployee(id),
    onSuccess: () => {
      invalidateStaffCaches(queryClient);
    },
  });
}
