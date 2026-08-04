import { Navigate, Route, Routes } from 'react-router';
import { useTranslation } from 'react-i18next';
import Login from './components/Login';
import ToastRegion from './components/ToastRegion';
import { useAuth } from './context/AuthContext';
import QueryView from './views/QueryView';
import WorkspaceView from './views/WorkspaceView';

function LoadingScreen() {
  const { t } = useTranslation('translation');

  return (
    <div className="app-loading">
      <div className="app-loading__mark">
        <img src="/assets/database-icon.png" alt="RestDb" />
      </div>
      <div className="loading-spinner" />
      <p>{t('app.preparingWorkspace')}</p>
    </div>
  );
}

function ProtectedRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return children;
}

function PublicRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (isAuthenticated) {
    return <Navigate to="/workspace" replace />;
  }

  return children;
}

function App() {
  return (
    <>
      <Routes>
        <Route
          path="/"
          element={
            <PublicRoute>
              <Login />
            </PublicRoute>
          }
        />
        <Route
          path="/workspace"
          element={
            <ProtectedRoute>
              <WorkspaceView />
            </ProtectedRoute>
          }
        />
        <Route
          path="/query"
          element={
            <ProtectedRoute>
              <QueryView />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <ToastRegion />
    </>
  );
}

export default App;
