import { apiClient, ApiError } from "./apiClient.ts";
export interface MenuAccess {
  allowedMenuIds: string[] | null;
}
export async function getNavigationAccess(): Promise<MenuAccess> {
  try {
    return await apiClient<MenuAccess>("/navigation-access");
  } catch (error) {
    // Older servers do not yet expose employee overrides; retain their role defaults.
    if (error instanceof ApiError && error.status === 404) return { allowedMenuIds: null };
    throw error;
  }
}
