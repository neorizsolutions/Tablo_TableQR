export const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5166/api";

export async function apiClient<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;
  
  const token = localStorage.getItem("tablo_token");
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

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    if ((response.status === 401 || response.status === 403) && !url.includes("/auth/login") && !url.includes("/sysadmin/login")) {
      // Remove redirect so we can debug which URL is failing
      // localStorage.removeItem("tablo_token");
      // window.location.href = "/auth";
      throw new Error(`Unauthorized (401/403) when fetching ${url}`);
    }

    let errorMessage = "An error occurred while fetching data.";
    try {
      const errorData = await response.json();
      errorMessage = errorData.message || errorMessage;
    } catch {
      // Ignored
    }
    throw new Error(errorMessage);
  }

  // Handle empty responses (like 204 No Content)
  if (response.status === 204) {
    return null as T;
  }

  return response.json() as Promise<T>;
}

export async function apiDownloadBlob(endpoint: string, options?: RequestInit): Promise<Blob> {
  const url = `${API_BASE_URL}${endpoint}`;
  const token = localStorage.getItem("tablo_token");
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
    throw new Error(`Failed to download from ${url}: ${response.statusText}`);
  }

  return response.blob();
}

