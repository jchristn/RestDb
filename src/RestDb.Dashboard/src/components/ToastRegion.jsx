import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';

function ToastRegion() {
  const { t } = useTranslation('translation');
  const { dismissToast, toasts } = useApp();

  return (
    <div className="toast-region" aria-live="polite">
      {toasts.map((toast) => (
        <div className={`toast toast--${toast.tone}`} key={toast.id}>
          <div className="toast__copy">
            <strong>{toast.title}</strong>
            {toast.message ? <p>{toast.message}</p> : null}
          </div>
          <button
            aria-label={t('common.dismiss')}
            className="button button-ghost button-small"
            onClick={() => dismissToast(toast.id)}
            type="button"
          >
            {t('common.dismiss')}
          </button>
        </div>
      ))}
    </div>
  );
}

export default ToastRegion;
