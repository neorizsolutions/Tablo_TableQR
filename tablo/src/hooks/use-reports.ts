import { useQuery } from "@tanstack/react-query";
import { getReports } from "@/services/api";

export function useReports(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ["reports", params],
    queryFn: () => getReports(params),
    refetchInterval: false,
  });
}

export function useAnalyticsSummary(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}) {
  return useQuery({
    queryKey: ["analytics-summary", params],
    queryFn: () => import("@/services/api").then(m => m.getAnalyticsSummary(params)),
    refetchInterval: false,
  });
}

export function useSalesOverTime(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}) {
  return useQuery({
    queryKey: ["sales-over-time", params],
    queryFn: () => import("@/services/api").then(m => m.getSalesOverTime(params)),
    refetchInterval: false,
  });
}

export function useTopItems(params: {
  startDate?: string;
  endDate?: string;
  status?: string;
  tableId?: string;
}) {
  return useQuery({
    queryKey: ["top-items", params],
    queryFn: () => import("@/services/api").then(m => m.getTopItems(params)),
    refetchInterval: false,
  });
}
