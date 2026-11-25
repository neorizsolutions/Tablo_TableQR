import { useQuery } from "@tanstack/react-query";
import { useAuth } from "./use-auth";
import { getEffectiveAccess, type EffectiveAccess } from "@/services/subscription";
import { useCallback, useMemo } from "react";

export function useEffectiveAccess(outletId?: string) {
  const { user } = useAuth();
  const isRestaurantUser = !!user && user.role !== "SuperAdmin" && !!user.restaurantId;

  const query = useQuery<EffectiveAccess, Error>({
    queryKey: ["effective_access", user?.restaurantId, user?.id, outletId],
    queryFn: () => getEffectiveAccess(outletId),
    enabled: isRestaurantUser,
    staleTime: 60 * 1000, // 1 minute fresh cache
    refetchOnWindowFocus: true
  });

  const capabilitiesSet = useMemo(() => {
    if (!query.data?.capabilities) return new Set<string>();
    return new Set(query.data.capabilities.map(c => c.toLowerCase()));
  }, [query.data?.capabilities]);

  const hasCapability = useCallback((cap: string): boolean => {
    if (!user) return false;
    if (user.role === "SuperAdmin") return true;
    return capabilitiesSet.has(cap.toLowerCase());
  }, [user, capabilitiesSet]);

  return {
    access: query.data,
    plan: query.data?.plan,
    capabilities: query.data?.capabilities || [],
    limits: query.data?.limits,
    hasCapability,
    isLoading: query.isLoading && isRestaurantUser,
    isError: query.isError,
    refetch: query.refetch
  };
}
