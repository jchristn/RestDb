import { createContext, useContext, useEffect, useState } from 'react';
import { useLocalStorage } from '../hooks/useLocalStorage';
import ApiClient from '../utils/api';

const AuthContext = createContext(null);
export const DEFAULT_API_KEY_HEADER = 'x-api-key';
export const DEFAULT_AUTH_MODE = 'header';

export function createDefaultServerUrl() {
  if (typeof window === 'undefined') {
    return 'http://localhost:8000';
  }

  const protocol = window.location.protocol || 'http:';
  const hostname = window.location.hostname || 'localhost';
  return `${protocol}//${hostname}:8000`;
}

function createApiClient(session) {
  if (!session?.serverUrl) {
    return null;
  }

  return new ApiClient(session.serverUrl, {
    apiKey: session.apiKey,
    apiKeyHeader: session.apiKeyHeader,
    authMode: session.authMode
  });
}

export function AuthProvider({ children }) {
  const [storedSession, setStoredSession, removeStoredSession] = useLocalStorage(
    'restdb.dashboard.session',
    null
  );
  const [isLoading, setIsLoading] = useState(true);
  const [session, setSession] = useState(null);
  const [apiClient, setApiClient] = useState(null);

  useEffect(() => {
    let cancelled = false;

    async function restoreSession() {
      if (!storedSession?.serverUrl) {
        if (!cancelled) {
          setIsLoading(false);
        }
        return;
      }

      const restoredSession = {
        serverUrl: (storedSession.serverUrl || '').trim().replace(/\/$/, ''),
        apiKey: (storedSession.apiKey || '').trim(),
        apiKeyHeader: (storedSession.apiKeyHeader || '').trim() || DEFAULT_API_KEY_HEADER,
        authMode: storedSession.authMode === 'bearer' ? 'bearer' : DEFAULT_AUTH_MODE
      };
      const restoredClient = createApiClient(restoredSession);

      try {
        await restoredClient.probe();
        if (!cancelled) {
          setSession(restoredSession);
          setApiClient(restoredClient);
        }
      } catch {
        if (!cancelled) {
          removeStoredSession();
          setSession(null);
          setApiClient(null);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    restoreSession();

    return () => {
      cancelled = true;
    };
    // Restore from the initial local-storage snapshot only.
    // Login and logout manage runtime session state directly.
  }, []);

  async function login(serverUrl, apiKey, apiKeyHeader, authMode = DEFAULT_AUTH_MODE) {
    const trimmedSession = {
      serverUrl: (serverUrl || '').trim().replace(/\/$/, ''),
      apiKey: (apiKey || '').trim(),
      apiKeyHeader: (apiKeyHeader || '').trim() || DEFAULT_API_KEY_HEADER,
      authMode: authMode === 'bearer' ? 'bearer' : DEFAULT_AUTH_MODE
    };

    const client = createApiClient(trimmedSession);

    await client.probe();

    setStoredSession(trimmedSession);
    setSession(trimmedSession);
    setApiClient(client);
  }

  function logout() {
    removeStoredSession();
    setSession(null);
    setApiClient(null);
  }

  return (
    <AuthContext.Provider
      value={{
        apiClient,
        isAuthenticated: !!apiClient,
        isLoading,
        login,
        logout,
        session
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }

  return context;
}
