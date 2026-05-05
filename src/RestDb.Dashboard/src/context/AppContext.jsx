import { createContext, useCallback, useContext, useEffect, useState } from 'react';

const AppContext = createContext(null);
const DEFAULT_TOAST_TIMEOUT_MS = 4200;
const THEME_STORAGE_KEY = 'restdb.dashboard.theme';

export function AppProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const [theme, setTheme] = useState(() => {
    if (typeof window === 'undefined') {
      return 'light';
    }

    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
    return storedTheme === 'dark' ? 'dark' : 'light';
  });

  const dismissToast = useCallback((id) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const addToast = useCallback((input) => {
    const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const nextToast = {
      id,
      tone: input?.tone ?? 'info',
      title: input?.title ?? '',
      message: input?.message ?? ''
    };

    setToasts((current) => [...current, nextToast]);
    return id;
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme((current) => (current === 'dark' ? 'light' : 'dark'));
  }, []);

  useEffect(() => {
    if (toasts.length < 1) {
      return undefined;
    }

    const latestToast = toasts[toasts.length - 1];
    const handle = window.setTimeout(() => {
      dismissToast(latestToast.id);
    }, DEFAULT_TOAST_TIMEOUT_MS);

    return () => window.clearTimeout(handle);
  }, [dismissToast, toasts]);

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.dataset.theme = theme;
    }

    if (typeof window !== 'undefined') {
      window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    }
  }, [theme]);

  return (
    <AppContext.Provider
      value={{
        toasts,
        addToast,
        dismissToast,
        theme,
        toggleTheme
      }}
    >
      {children}
    </AppContext.Provider>
  );
}

export function useApp() {
  const context = useContext(AppContext);

  if (!context) {
    throw new Error('useApp must be used inside AppProvider.');
  }

  return context;
}
