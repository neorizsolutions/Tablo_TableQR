import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getEmployees, getEmployee, createEmployee, updateEmployee, updateEmployeeStatus, Employee } from "@/services/api";

export function useStaff() {
  return useQuery({
    queryKey: ["employees"],
    queryFn: getEmployees,
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
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
}

export function useUpdateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => updateEmployee(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
}

export function useUpdateEmployeeStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => updateEmployeeStatus(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
}
