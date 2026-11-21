import { handleLocalModuleFallback } from "./localModuleFallback.ts";

export const API_BASE_URL =
  (typeof import.meta !== "undefined" && (import.meta as any).env?.VITE_API_URL) ||
  (typeof process !== "undefined" && process.env?.VITE_API_URL) ||
  "http://localhost:5166/api";

export class ApiError extends Error {
  public status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function apiClient<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;
  
  const token = typeof localStorage !== "undefined" ? localStorage.getItem("tablo_token") : null;
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...options?.headers as Record<string, string>,
  };

  if (options?.body instanceof FormData) {
    delete headers["Content-Type"];
  }

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(url, {
      ...options,
      headers,
    });
  } catch (networkError) {
    // Only in development offline mode allow fallback
    if (typeof import.meta !== "undefined" && import.meta.env?.DEV) {
      const fallback = handleLocalModuleFallback(endpoint, { ...options, headers });
      if (fallback !== null) {
        return fallback as T;
      }
    }
    throw networkError;
  }

  if (!response.ok) {
    let errorMessage = `Request failed with status ${response.status}.`;
    try {
      const errorData = await response.json();
      errorMessage = errorData.message || errorData.title || errorMessage;
    } catch {
      // Ignored
    }
    throw new ApiError(errorMessage, response.status);
  }

  // Handle empty responses (like 204 No Content)
  if (response.status === 204) {
    return null as T;
  }

  return response.json() as Promise<T>;
}

export async function apiDownloadBlob(endpoint: string, options?: RequestInit): Promise<Blob> {
  const url = `${API_BASE_URL}${endpoint}`;
  const token = typeof localStorage !== "undefined" ? localStorage.getItem("tablo_token") : null;
  const headers: Record<string, string> = {
    ...options?.headers as Record<string, string>,
  };

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new Error(data?.message || `Download failed (${response.status}). Please retry.`);
  }

  return response.blob();
}

