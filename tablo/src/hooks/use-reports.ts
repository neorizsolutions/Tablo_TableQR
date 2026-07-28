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
