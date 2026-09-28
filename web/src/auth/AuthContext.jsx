import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { authService, setUnauthorizedHandler } from '../services/api';

const STORAGE_KEY = 'pcb-aoi-auth';
const AuthContext = createContext(null);

function readStoredAuth() {
  try {
    const auth = JSON.parse(sessionStorage.getItem(STORAGE_KEY));
    if (!auth?.accessToken || !auth?.user) return null;
    const payload = JSON.parse(atob(auth.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return payload.exp * 1000 > Date.now() ? auth : null;
  } catch { return null; }
}

export function AuthProvider({ children }) {
  const [auth, setAuth] = useState(readStoredAuth);
  const logout = () => { sessionStorage.removeItem(STORAGE_KEY); setAuth(null); };
  useEffect(() => setUnauthorizedHandler(logout), []);
  const value = useMemo(() => ({
    user: auth?.user ?? null,
    token: auth?.accessToken ?? null,
    isAuthenticated: Boolean(auth),
    login: async (credentials) => {
      const result = await authService.login(credentials);
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(result));
      setAuth(result);
      return result;
    },
    logout,
  }), [auth]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
