import { createContext, useContext, useState, useEffect, type ReactNode } from "react";

type Role = "Admin" | "Kitchen" | "Waiter" | "Owner" | "Staff" | "Customer" | "SuperAdmin";

interface AuthUser {
  id: string;
  email: string;
  name?: string;
  restaurantId?: string;
  restaurantName?: string;
  role: Role;
}

interface AuthCtx {
  user: AuthUser | null;
  roles: Role[];
  loading: boolean;
  isStaff: boolean;
  signIn: (token: string) => void;
  signOut: (redirectPath?: string) => Promise<void>;
}

const Ctx = createContext<AuthCtx | null>(null);

function parseJwt(token: string) {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function(c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));
    return JSON.parse(jsonPayload);
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  
  useEffect(() => {
    const token = localStorage.getItem("tablo_token");
    if (token) {
      const payload = parseJwt(token);
      if (payload && payload.exp * 1000 > Date.now()) {
        const role = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || payload.role;
        const email = payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] || payload.email;
        setUser({
          id: payload.sub,
          email: email,
          name: typeof payload.name === "string" ? payload.name : undefined,
          restaurantId: payload.RestaurantId,
          restaurantName: payload.RestaurantName,
          role: role as Role
        });
        setRoles([role as Role]);
      } else {
        localStorage.removeItem("tablo_token");
      }
    }
    setLoading(false);
  }, []);

  const signIn = (token: string) => {
    localStorage.setItem("tablo_token", token);
    const payload = parseJwt(token);
    if (payload) {
      const role = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || payload.role;
      const email = payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] || payload.email;
      setUser({
        id: payload.sub,
        email: email,
        name: typeof payload.name === "string" ? payload.name : undefined,
        restaurantId: payload.RestaurantId,
        restaurantName: payload.RestaurantName,
        role: role as Role
      });
      setRoles([role as Role]);
    }
  };

  const signOut = async (redirectPath?: string | any) => {
    localStorage.removeItem("tablo_token");
    setUser(null);
    setRoles([]);
    const path = typeof redirectPath === "string" ? redirectPath : "/auth";
    window.location.href = path;
  };

  const isStaff = roles.includes("Admin") || roles.includes("Kitchen") || roles.includes("Waiter");

  return (
    <Ctx.Provider value={{ user, roles, loading, isStaff, signIn, signOut }}>
      {children}
    </Ctx.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
