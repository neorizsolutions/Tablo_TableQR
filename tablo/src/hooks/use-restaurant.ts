import { useQuery } from "@tanstack/react-query";
import { getRestaurantBySlug } from "@/services/api";

export function useRestaurant(slug: string | null) {
  return useQuery({
    queryKey: ["restaurant", slug],
    queryFn: () => getRestaurantBySlug(slug!),
    enabled: !!slug,
    staleTime: 1000 * 60 * 60, // 1 hour
  });
}
